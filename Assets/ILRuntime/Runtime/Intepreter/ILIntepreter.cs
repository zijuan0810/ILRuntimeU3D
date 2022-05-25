using System;
using System.Collections.Generic;
using System.Threading;
using ILRuntime.Runtime.Enviorment;
using ILRuntime.Runtime.Stack;
using ILRuntime.CLR.Method;
using ILRuntime.CLR.TypeSystem;
using ILRuntime.Runtime.Debugger;
using ILRuntime.CLR.Utils;

namespace ILRuntime.Runtime.Intepreter
{
    #pragma warning disable CS0414
    
    public unsafe partial class ILIntepreter
    {
        private Enviorment.AppDomain domain;
        private object _lockObj;
        private bool allowUnboundCLRMethod;

        internal RuntimeStack Stack { get; }

        public bool ShouldBreak { get; set; }
        public StepTypes CurrentStepType { get; set; }
        public StackObject* LastStepFrameBase { get; set; }
        public int LastStepInstructionIndex { get; set; }
        StackObject* ValueTypeBasePointer;
        bool mainthreadLock;

        public ILIntepreter(Enviorment.AppDomain domain)
        {
            this.domain = domain;
            Stack = new RuntimeStack(this);
            allowUnboundCLRMethod = domain.AllowUnboundCLRMethod;
#if DEBUG && !DISABLE_ILRUNTIME_DEBUG
            _lockObj = new object();
#endif
        }

        public Enviorment.AppDomain AppDomain
        {
            get { return domain; }
        }

        public void Break()
        {
            //Clear old debug state
            ClearDebugState();
#if DEBUG && !NO_PROFILER
            if (domain.UnityMainThreadID == Thread.CurrentThread.ManagedThreadId)
            {
                mainthreadLock = true;
                while (mainthreadLock)
                {
                    domain.DebugService.ResolvePendingRequests();
                    Thread.Sleep(10);
                }

                return;
            }
#endif
            lock (_lockObj)
            {
                Monitor.Wait(_lockObj);
            }
        }

        public void Resume()
        {
            mainthreadLock = false;
            lock (_lockObj)
                Monitor.Pulse(_lockObj);
        }

        public void ClearDebugState()
        {
            ShouldBreak = false;
            CurrentStepType = StepTypes.None;
            LastStepFrameBase = (StackObject*)0;
            LastStepInstructionIndex = 0;
        }

        public object Run(ILMethod method, object instance, object[] p)
        {
            IList<object> mStack = Stack.ManagedStack;
            int mStackBase = mStack.Count;
            StackObject* esp = Stack.StackBase;
            Stack.ResetValueTypePointer();
            if (method.HasThis)
            {
                if (instance is CrossBindingAdaptorType)
                    instance = ((CrossBindingAdaptorType)instance).ILInstance;
                if (instance == null)
                    throw new NullReferenceException("instance should not be null!");
                esp = PushObject(esp, mStack, instance);
            }

            esp = PushParameters(method, esp, p);
            esp = Execute(method, esp, out _);
            object result = method.ReturnType != domain.VoidType
                ? method.ReturnType.TypeForCLR.CheckCLRTypes(StackObject.ToObject((esp - 1), domain, mStack))
                : null;
            //ClearStack
#if DEBUG && !DISABLE_ILRUNTIME_DEBUG
            ((List<object>)mStack).RemoveRange(mStackBase, mStack.Count - mStackBase);
#else
            ((UncheckedList<object>)mStack).RemoveRange(mStackBase, mStack.Count - mStackBase);
#endif
            return result;
        }

        ExceptionHandler FindExceptionHandlerByBranchTarget(int addr, int branchTarget, ExceptionHandler[] ehs)
        {
            ExceptionHandler eh = null;
            for (int i = 0; i < ehs.Length; i++)
            {
                var e = ehs[i];
                if (addr >= e.TryStart && addr <= e.TryEnd && (branchTarget < e.TryStart || branchTarget > e.TryEnd) &&
                    e.HandlerType == ExceptionHandlerType.Finally)
                {
                    eh = e;
                    break;
                }
            }

            return eh;
        }

        void PrepareRegisterCallStack(StackObject* esp, IList<object> mStack, ILMethod method)
        {
            var pCnt = method.HasThis ? method.ParameterCount + 1 : method.ParameterCount;
            StackObject* basePointer = esp - pCnt;
            int mBase = mStack.Count;
            int existing = 0;
            for (int i = 0; i < pCnt; i++)
            {
                StackObject* cur = basePointer + i;
                if (cur->ObjectType < ObjectTypes.Object)
                {
                    mStack.Add(null);
                }
                else
                    existing++;
            }

            if (existing > 0)
            {
                mBase = mBase - existing;
                for (int i = pCnt - 1; i >= 0; i--)
                {
                    StackObject* cur = basePointer + i;
                    if (cur->ObjectType >= ObjectTypes.Object)
                    {
                        mStack[mBase + i] = mStack[cur->Value];
                        cur->Value = mBase + i;
                    }
                    else
                    {
                        if (cur->ObjectType == ObjectTypes.Null)
                        {
                            cur->ObjectType = ObjectTypes.Object;
                            cur->Value = mBase + i;
                        }

                        mStack[mBase + i] = null;
                    }
                }
            }
        }

        void DumpStack(StackObject* esp)
        {
            AppDomain.DebugService.DumpStack(esp, Stack);
        }

        void CloneStackValueType(StackObject* src, StackObject* dst, IList<object> mStack)
        {
            StackObject* descriptor = ILIntepreter.ResolveReference(src);
            Stack.AllocValueType(dst, AppDomain.GetTypeByIndex(descriptor->Value));
            StackObject* dstDescriptor = ILIntepreter.ResolveReference(dst);
            int cnt = descriptor->ValueLow;
            for (int i = 0; i < cnt; i++)
            {
                StackObject* val = Minus(descriptor, i + 1);
                CopyToValueTypeField(dstDescriptor, i, val, mStack);
            }
        }

        bool CanCastTo(StackObject* src, StackObject* dst)
        {
            var sType = AppDomain.GetTypeByIndex(src->Value);
            var dType = AppDomain.GetTypeByIndex(dst->Value);
            return sType.CanAssignTo(dType);
        }

        bool CanCopyStackValueType(StackObject* src, StackObject* dst)
        {
            if (src->ObjectType == ObjectTypes.ValueTypeObjectReference && dst->ObjectType == ObjectTypes.ValueTypeObjectReference)
            {
                StackObject* descriptor = ILIntepreter.ResolveReference(src);
                StackObject* dstDescriptor = ILIntepreter.ResolveReference(dst);
                return CanCastTo(descriptor, dstDescriptor);
            }
            else
                return false;
        }
#if DEBUG
        public void CopyStackValueType(StackObject* src, StackObject* dst, IList<object> mStack, bool noCheck = false)
#else
        public void CopyStackValueType(StackObject* src, StackObject* dst, IList<object> mStack)
#endif
        {
#if DEBUG
            CopyStackValueType(src, dst, mStack, mStack, noCheck);
#else
            CopyStackValueType(src, dst, mStack, mStack);
#endif
        }
#if DEBUG
        public void CopyStackValueType(StackObject* src, StackObject* dst, IList<object> mStack, IList<object> dstmStack,
            bool noCheck = false)
#else
        public void CopyStackValueType(StackObject* src, StackObject* dst, IList<object> mStack, IList<object> dstmStack)
#endif
        {
            StackObject* descriptor = ILIntepreter.ResolveReference(src);
            StackObject* dstDescriptor = ILIntepreter.ResolveReference(dst);
#if DEBUG
            if (!CanCastTo(descriptor, dstDescriptor))
                throw new InvalidCastException();
#endif
            int cnt = descriptor->ValueLow;
            for (int i = 0; i < cnt; i++)
            {
                StackObject* srcVal = Minus(descriptor, i + 1);
                StackObject* dstVal = Minus(dstDescriptor, i + 1);
#if DEBUG
                if (!noCheck && srcVal->ObjectType != dstVal->ObjectType)
                    throw new NotSupportedException();
#endif
                switch (srcVal->ObjectType)
                {
                    case ObjectTypes.Object:
                    case ObjectTypes.ArrayReference:
                    case ObjectTypes.FieldReference:
                        dstmStack[dstVal->Value] = mStack[srcVal->Value];
                        break;
                    case ObjectTypes.ValueTypeObjectReference:
                        CopyStackValueType(srcVal, dstVal, mStack, dstmStack);
                        break;
                    default:
                        *dstVal = *srcVal;
                        break;
                }
            }
        }

        void CopyValueTypeToStack(StackObject* dst, object ins, IList<object> mStack)
        {
            if (ins is ILTypeInstance)
            {
                ((ILTypeInstance)ins).CopyValueTypeToStack(dst, mStack);
            }
            else
            {
                if (ins is CrossBindingAdaptorType)
                {
                    ((CrossBindingAdaptorType)ins).ILInstance.CopyValueTypeToStack(dst, mStack);
                }
                else
                {
                    var vb = ((CLRType)domain.GetTypeByIndex(dst->Value)).ValueTypeBinder;
                    vb.CopyValueTypeToStack(ins, dst, mStack);
                }
            }
        }

        void CopyToValueTypeField(StackObject* obj, int idx, StackObject* val, IList<object> mStack)
        {
            StackObject* dst = Minus(obj, idx + 1);
            switch (val->ObjectType)
            {
                case ObjectTypes.Null:
                {
                    mStack[dst->Value] = null;
                }
                    break;
                case ObjectTypes.Object:
                case ObjectTypes.FieldReference:
                case ObjectTypes.ArrayReference:
                {
                    if (dst->ObjectType == ObjectTypes.ValueTypeObjectReference)
                    {
                        var ins = mStack[val->Value];
                        dst = ILIntepreter.ResolveReference(dst);

                        CopyValueTypeToStack(dst, ins, mStack);
                    }
                    else
                    {
                        mStack[dst->Value] = CheckAndCloneValueType(mStack[val->Value], domain);
                    }
                }
                    break;
                case ObjectTypes.ValueTypeObjectReference:
                {
                    if (dst->ObjectType == ObjectTypes.ValueTypeObjectReference)
                    {
                        CopyStackValueType(val, dst, mStack);
                    }
                    else
                        throw new NotImplementedException();
                }
                    break;
                default:
                    *dst = *val;
                    break;
            }
        }

        void StLocSub(StackObject* esp, StackObject* v, int idx, IList<object> mStack)
        {
            switch (esp->ObjectType)
            {
                case ObjectTypes.Null:
                    v->ObjectType = ObjectTypes.Object;
                    v->Value = idx;
                    mStack[idx] = null;
                    break;
                case ObjectTypes.Object:
                case ObjectTypes.FieldReference:
                case ObjectTypes.ArrayReference:
                    if (v->ObjectType == ObjectTypes.ValueTypeObjectReference)
                    {
                        var obj = mStack[esp->Value];
                        if (obj is ILTypeInstance)
                        {
                            var dst = ILIntepreter.ResolveReference(v);
                            ((ILTypeInstance)obj).CopyValueTypeToStack(dst, mStack);
                        }
                        else
                        {
                            var dst = ILIntepreter.ResolveReference(v);
                            var ct = domain.GetTypeByIndex(dst->Value) as CLRType;
                            var binder = ct.ValueTypeBinder;
                            binder.CopyValueTypeToStack(obj, dst, mStack);
                        }
                    }
                    else
                    {
                        *v = *esp;
                        mStack[idx] = CheckAndCloneValueType(mStack[v->Value], domain);
                        v->Value = idx;
                    }

                    Free(esp);
                    break;
                case ObjectTypes.ValueTypeObjectReference:
                    if (v->ObjectType == ObjectTypes.ValueTypeObjectReference)
                    {
                        CopyStackValueType(esp, v, mStack);
                    }
                    else
                        throw new NotImplementedException();

                    FreeStackValueType(esp);
                    break;
                default:
                    *v = *esp;
                    mStack[idx] = null;
                    break;
            }
        }

        public object RetriveObject(StackObject* esp, IList<object> mStack)
        {
            StackObject* objRef = GetObjectAndResolveReference(esp);
            if (objRef->ObjectType == ObjectTypes.Null)
                return null;
            object obj;
            switch (objRef->ObjectType)
            {
                case ObjectTypes.Object:
                    obj = mStack[objRef->Value];
                    break;
                case ObjectTypes.FieldReference:
                {
                    obj = mStack[objRef->Value];
                    int idx = objRef->ValueLow;
                    if (obj is ILTypeInstance)
                    {
                        obj = ((ILTypeInstance)obj)[idx];
                    }
                    else
                    {
                        var t = AppDomain.GetType(obj.GetType());
                        obj = ((CLRType)t).GetFieldValue(idx, obj);
                    }
                }
                    break;
                case ObjectTypes.ArrayReference:
                {
                    Array arr = mStack[objRef->Value] as Array;
                    int idx = objRef->ValueLow;
                    obj = arr.GetValue(idx);
                }
                    break;
                case ObjectTypes.StaticFieldReference:
                {
                    var t = AppDomain.GetType(objRef->Value);
                    int idx = objRef->ValueLow;
                    if (t is ILType)
                    {
                        obj = ((ILType)t).StaticInstance[idx];
                    }
                    else
                    {
                        obj = ((CLRType)t).GetFieldValue(idx, null);
                    }
                }
                    break;
                case ObjectTypes.ValueTypeObjectReference:
                    obj = StackObject.ToObject(objRef, domain, mStack);
                    break;
                default:
                    throw new NotImplementedException();
            }

            return obj;
        }

        public int RetriveInt32(StackObject* esp, IList<object> mStack)
        {
            StackObject* objRef = GetObjectAndResolveReference(esp);
            if (objRef->ObjectType == ObjectTypes.Null)
                return 0;
            int res;
            switch (objRef->ObjectType)
            {
                case ObjectTypes.Object:
                    res = (int)mStack[objRef->Value];
                    break;
                case ObjectTypes.Integer:
                    res = objRef->Value;
                    break;
                case ObjectTypes.FieldReference:
                {
                    var obj = mStack[objRef->Value];
                    int idx = objRef->ValueLow;
                    if (obj is ILTypeInstance)
                    {
                        res = ((ILTypeInstance)obj).Fields[idx].Value;
                    }
                    else
                    {
                        var t = AppDomain.GetType(obj.GetType());
                        StackObject so;
                        var sop = &so;
                        if (!((CLRType)t).CopyFieldToStack(idx, obj, this, ref sop, mStack))
                            res = (int)((CLRType)t).GetFieldValue(idx, obj);
                        else
                        {
                            res = so.Value;
                        }
                    }
                }
                    break;
                case ObjectTypes.ArrayReference:
                {
                    Array arr = mStack[objRef->Value] as Array;
                    int idx = objRef->ValueLow;
                    if (arr is int[])
                        res = ((int[])arr)[idx];
                    else
                    {
                        res = Convert.ToInt32(arr.GetValue(idx));
                    }
                }
                    break;
                case ObjectTypes.StaticFieldReference:
                {
                    var t = AppDomain.GetType(objRef->Value);
                    int idx = objRef->ValueLow;
                    if (t is ILType)
                    {
                        res = ((ILType)t).StaticInstance.Fields[idx].Value;
                    }
                    else
                    {
                        StackObject so;
                        var sop = &so;
                        if (!((CLRType)t).CopyFieldToStack(idx, null, this, ref sop, mStack))
                            res = (int)((CLRType)t).GetFieldValue(idx, null);
                        else
                        {
                            res = so.Value;
                        }
                    }
                }
                    break;
                default:
                    throw new NotImplementedException();
            }

            return res;
        }

        public long RetriveInt64(StackObject* esp, IList<object> mStack)
        {
            StackObject* objRef = GetObjectAndResolveReference(esp);
            if (objRef->ObjectType == ObjectTypes.Null)
                return 0;
            object obj;
            long res;
            switch (objRef->ObjectType)
            {
                case ObjectTypes.Object:
                    res = (long)mStack[objRef->Value];
                    break;
                case ObjectTypes.Long:
                    res = *(long*)&objRef->Value;
                    break;
                case ObjectTypes.FieldReference:
                {
                    obj = mStack[objRef->Value];
                    int idx = objRef->ValueLow;
                    StackObject so;
                    if (obj is ILTypeInstance)
                    {
                        so = ((ILTypeInstance)obj).Fields[idx];
                        res = *(long*)&so.Value;
                    }
                    else
                    {
                        var t = AppDomain.GetType(obj.GetType());
                        var sop = &so;
                        if (!((CLRType)t).CopyFieldToStack(idx, obj, this, ref sop, mStack))
                            res = (long)((CLRType)t).GetFieldValue(idx, obj);
                        else
                        {
                            res = *(long*)&so.Value;
                        }
                    }
                }
                    break;
                case ObjectTypes.ArrayReference:
                {
                    Array arr = mStack[objRef->Value] as Array;
                    int idx = objRef->ValueLow;
                    if (arr is long[])
                        res = ((long[])arr)[idx];
                    else
                    {
                        res = (long)arr.GetValue(idx);
                    }
                }
                    break;
                case ObjectTypes.StaticFieldReference:
                {
                    var t = AppDomain.GetType(objRef->Value);
                    int idx = objRef->ValueLow;
                    StackObject so;
                    if (t is ILType)
                    {
                        so = ((ILType)t).StaticInstance.Fields[idx];
                        res = *(long*)&so.Value;
                    }
                    else
                    {
                        var sop = &so;
                        if (!((CLRType)t).CopyFieldToStack(idx, null, this, ref sop, mStack))
                            res = (long)((CLRType)t).GetFieldValue(idx, null);
                        else
                        {
                            res = *(long*)&so.Value;
                        }
                    }
                }
                    break;
                default:
                    throw new NotImplementedException();
            }

            return res;
        }

        public float RetriveFloat(StackObject* esp, IList<object> mStack)
        {
            StackObject* objRef = GetObjectAndResolveReference(esp);
            if (objRef->ObjectType == ObjectTypes.Null)
                return 0;
            object obj;
            float res;
            switch (objRef->ObjectType)
            {
                case ObjectTypes.Object:
                    res = (float)mStack[objRef->Value];
                    break;
                case ObjectTypes.Float:
                    res = *(float*)&objRef->Value;
                    break;
                case ObjectTypes.FieldReference:
                {
                    obj = mStack[objRef->Value];
                    int idx = objRef->ValueLow;
                    StackObject so;
                    if (obj is ILTypeInstance)
                    {
                        so = ((ILTypeInstance)obj).Fields[idx];
                        res = *(float*)&so.Value;
                    }
                    else
                    {
                        var t = AppDomain.GetType(obj.GetType());
                        var sop = &so;
                        if (!((CLRType)t).CopyFieldToStack(idx, obj, this, ref sop, mStack))
                            res = (float)((CLRType)t).GetFieldValue(idx, obj);
                        else
                        {
                            res = *(float*)&so.Value;
                        }
                    }
                }
                    break;
                case ObjectTypes.ArrayReference:
                {
                    Array arr = mStack[objRef->Value] as Array;
                    int idx = objRef->ValueLow;
                    if (arr is float[])
                        res = ((float[])arr)[idx];
                    else
                    {
                        res = (float)arr.GetValue(idx);
                    }
                }
                    break;
                case ObjectTypes.StaticFieldReference:
                {
                    var t = AppDomain.GetType(objRef->Value);
                    int idx = objRef->ValueLow;
                    StackObject so;
                    if (t is ILType)
                    {
                        so = ((ILType)t).StaticInstance.Fields[idx];
                        res = *(float*)&so.Value;
                    }
                    else
                    {
                        var sop = &so;
                        if (!((CLRType)t).CopyFieldToStack(idx, null, this, ref sop, mStack))
                            res = (float)((CLRType)t).GetFieldValue(idx, null);
                        else
                        {
                            res = *(float*)&so.Value;
                        }
                    }
                }
                    break;
                default:
                    throw new NotImplementedException();
            }

            return res;
        }

        public double RetriveDouble(StackObject* esp, IList<object> mStack)
        {
            StackObject* objRef = GetObjectAndResolveReference(esp);
            if (objRef->ObjectType == ObjectTypes.Null)
                return 0;
            object obj;
            double res;
            switch (objRef->ObjectType)
            {
                case ObjectTypes.Object:
                    res = (double)mStack[objRef->Value];
                    break;
                case ObjectTypes.Double:
                    res = *(double*)&objRef->Value;
                    break;
                case ObjectTypes.FieldReference:
                {
                    obj = mStack[objRef->Value];
                    int idx = objRef->ValueLow;
                    StackObject so;
                    if (obj is ILTypeInstance)
                    {
                        so = ((ILTypeInstance)obj).Fields[idx];
                        res = *(double*)&so.Value;
                    }
                    else
                    {
                        var t = AppDomain.GetType(obj.GetType());
                        var sop = &so;
                        if (!((CLRType)t).CopyFieldToStack(idx, obj, this, ref sop, mStack))
                            res = (double)((CLRType)t).GetFieldValue(idx, obj);
                        else
                        {
                            res = *(double*)&so.Value;
                        }
                    }
                }
                    break;
                case ObjectTypes.ArrayReference:
                {
                    Array arr = mStack[objRef->Value] as Array;
                    int idx = objRef->ValueLow;
                    if (arr is double[])
                        res = ((double[])arr)[idx];
                    else
                    {
                        res = (double)arr.GetValue(idx);
                    }
                }
                    break;
                case ObjectTypes.StaticFieldReference:
                {
                    var t = AppDomain.GetType(objRef->Value);
                    int idx = objRef->ValueLow;
                    StackObject so;
                    if (t is ILType)
                    {
                        so = ((ILType)t).StaticInstance.Fields[idx];
                        res = *(double*)&so.Value;
                    }
                    else
                    {
                        var sop = &so;
                        if (!((CLRType)t).CopyFieldToStack(idx, null, this, ref sop, mStack))
                            res = (double)((CLRType)t).GetFieldValue(idx, null);
                        else
                        {
                            res = *(double*)&so.Value;
                        }
                    }
                }
                    break;
                default:
                    throw new NotImplementedException();
            }

            return res;
        }

        void ArraySetValue(Array arr, object obj, int idx)
        {
            if (obj == null)
                arr.SetValue(null, idx);
            else
            {
                arr.SetValue(arr.GetType().GetElementType().CheckCLRTypes(obj), idx);
            }
        }

        void StoreIntValueToArray(Array arr, StackObject* val, StackObject* idx)
        {
            {
                int[] tmp = arr as int[];
                if (tmp != null)
                {
                    tmp[idx->Value] = val->Value;
                    return;
                }
            }
            {
                short[] tmp = arr as short[];
                if (tmp != null)
                {
                    tmp[idx->Value] = (short)val->Value;
                    return;
                }
            }
            {
                byte[] tmp = arr as byte[];
                if (tmp != null)
                {
                    tmp[idx->Value] = (byte)val->Value;
                    return;
                }
            }
            {
                bool[] tmp = arr as bool[];
                if (tmp != null)
                {
                    tmp[idx->Value] = val->Value == 1;
                    return;
                }
            }
            {
                uint[] tmp = arr as uint[];
                if (tmp != null)
                {
                    tmp[idx->Value] = (uint)val->Value;
                    return;
                }
            }
            {
                ushort[] tmp = arr as ushort[];
                if (tmp != null)
                {
                    tmp[idx->Value] = (ushort)val->Value;
                    return;
                }
            }
            {
                char[] tmp = arr as char[];
                if (tmp != null)
                {
                    tmp[idx->Value] = (char)val->Value;
                    return;
                }
            }
            {
                sbyte[] tmp = arr as sbyte[];
                if (tmp != null)
                {
                    tmp[idx->Value] = (sbyte)val->Value;
                    return;
                }
            }
            throw new NotImplementedException();
        }

        ExceptionHandler GetCorrespondingExceptionHandler(ExceptionHandler[] eh, object obj, int addr, ExceptionHandlerType type,
            bool explicitMatch)
        {
            ExceptionHandler res = null;
            int distance = int.MaxValue;
            Exception ex = obj is ILRuntimeException ? ((ILRuntimeException)obj).InnerException : obj as Exception;
            foreach (var i in eh)
            {
                if (i.HandlerType == type)
                {
                    if (addr >= i.TryStart && addr <= i.TryEnd)
                    {
                        if (CheckExceptionType(i.CatchType, ex, explicitMatch))
                        {
                            int d = addr - i.TryStart;
                            if (d < distance)
                            {
                                distance = d;
                                res = i;
                            }
                        }
                    }
                }
            }

            return res;
        }

        void LoadFromFieldReference(object obj, int idx, StackObject* dst, IList<object> mStack)
        {
            if (obj is ILTypeInstance)
            {
                ((ILTypeInstance)obj).PushToStack(idx, dst, this, mStack);
            }
            else
            {
                CLRType t = AppDomain.GetType(obj.GetType()) as CLRType;
                if (!t.CopyFieldToStack(idx, obj, this, ref dst, mStack))
                    ILIntepreter.PushObject(dst, mStack, t.GetFieldValue(idx, obj));
            }
        }

        void StoreValueToFieldReference(ref object obj, int idx, StackObject* val, IList<object> mStack)
        {
            if (obj is ILTypeInstance)
            {
                ((ILTypeInstance)obj).AssignFromStack(idx, val, AppDomain, mStack);
            }
            else
            {
                CLRType t = AppDomain.GetType(obj.GetType()) as CLRType;
                //It's impossible to garantee this field reference is a direct reference, it'll cause problem if it's not
                //if (!t.AssignFieldFromStack(idx, ref obj, this, val, mStack))
                {
                    var v = obj.GetType().CheckCLRTypes(CheckAndCloneValueType(StackObject.ToObject(val, AppDomain, mStack), AppDomain));
                    t.SetFieldValue(idx, ref obj, v, true);
                }
            }
        }

        void LoadFromArrayReference(object obj, int idx, StackObject* objRef, IType t, IList<object> mStack, int managedIdx = -1)
        {
            var nT = t.TypeForCLR;
            LoadFromArrayReference(obj, idx, objRef, nT, mStack, managedIdx);
        }

        void LoadFromArrayReference(object obj, int idx, StackObject* objRef, Type nT, IList<object> mStack, int managedIdx = -1)
        {
            if (nT.IsPrimitive)
            {
                if (nT == typeof(int))
                {
                    int[] arr = obj as int[];
                    objRef->ObjectType = ObjectTypes.Integer;
                    objRef->Value = arr[idx];
                    objRef->ValueLow = 0;
                }
                else if (nT == typeof(short))
                {
                    short[] arr = obj as short[];
                    objRef->ObjectType = ObjectTypes.Integer;
                    objRef->Value = arr[idx];
                    objRef->ValueLow = 0;
                }
                else if (nT == typeof(long))
                {
                    long[] arr = obj as long[];
                    objRef->ObjectType = ObjectTypes.Long;
                    *(long*)&objRef->Value = arr[idx];
                }
                else if (nT == typeof(float))
                {
                    float[] arr = obj as float[];
                    objRef->ObjectType = ObjectTypes.Float;
                    *(float*)&objRef->Value = arr[idx];
                    objRef->ValueLow = 0;
                }
                else if (nT == typeof(double))
                {
                    double[] arr = obj as double[];
                    objRef->ObjectType = ObjectTypes.Double;
                    *(double*)&objRef->Value = arr[idx];
                }
                else if (nT == typeof(byte))
                {
                    byte[] arr = obj as byte[];
                    objRef->ObjectType = ObjectTypes.Integer;
                    objRef->Value = arr[idx];
                    objRef->ValueLow = 0;
                }
                else if (nT == typeof(char))
                {
                    char[] arr = obj as char[];
                    objRef->ObjectType = ObjectTypes.Integer;
                    objRef->Value = arr[idx];
                    objRef->ValueLow = 0;
                }
                else if (nT == typeof(uint))
                {
                    uint[] arr = obj as uint[];
                    objRef->ObjectType = ObjectTypes.Integer;
                    *(uint*)&objRef->Value = arr[idx];
                    objRef->ValueLow = 0;
                }
                else if (nT == typeof(sbyte))
                {
                    sbyte[] arr = obj as sbyte[];
                    objRef->ObjectType = ObjectTypes.Integer;
                    objRef->Value = arr[idx];
                    objRef->ValueLow = 0;
                }
                else if (nT == typeof(ulong))
                {
                    ulong[] arr = obj as ulong[];
                    objRef->ObjectType = ObjectTypes.Long;
                    *(ulong*)&objRef->Value = arr[idx];
                }
                else
                    throw new NotImplementedException();
            }
            else
            {
                Array arr = obj as Array;
                objRef->ObjectType = ObjectTypes.Object;
                if (managedIdx >= 0)
                {
                    objRef->Value = managedIdx;
                    mStack[managedIdx] = arr.GetValue(idx);
                }
                else
                {
                    objRef->Value = mStack.Count;
                    mStack.Add(arr.GetValue(idx));
                }

                objRef->ValueLow = 0;
            }
        }

        void StoreValueToArrayReference(StackObject* objRef, StackObject* val, IType t, IList<object> mStack)
        {
            var nT = t.TypeForCLR;
            StoreValueToArrayReference(objRef, val, nT, mStack);
        }

        void StoreValueToArrayReference(StackObject* objRef, StackObject* val, Type nT, IList<object> mStack)
        {
            if (nT.IsPrimitive)
            {
                if (nT == typeof(int))
                {
                    int[] arr = mStack[objRef->Value] as int[];
                    arr[objRef->ValueLow] = val->Value;
                }
                else if (nT == typeof(short))
                {
                    short[] arr = mStack[objRef->Value] as short[];
                    arr[objRef->ValueLow] = (short)val->Value;
                }
                else if (nT == typeof(long))
                {
                    long[] arr = mStack[objRef->Value] as long[];
                    arr[objRef->ValueLow] = *(long*)&val->Value;
                }
                else if (nT == typeof(float))
                {
                    float[] arr = mStack[objRef->Value] as float[];
                    arr[objRef->ValueLow] = *(float*)&val->Value;
                }
                else if (nT == typeof(double))
                {
                    double[] arr = mStack[objRef->Value] as double[];
                    arr[objRef->ValueLow] = *(double*)&val->Value;
                }
                else if (nT == typeof(byte))
                {
                    byte[] arr = mStack[objRef->Value] as byte[];
                    arr[objRef->ValueLow] = (byte)val->Value;
                }
                else if (nT == typeof(char))
                {
                    char[] arr = mStack[objRef->Value] as char[];
                    arr[objRef->ValueLow] = (char)val->Value;
                }
                else if (nT == typeof(uint))
                {
                    uint[] arr = mStack[objRef->Value] as uint[];
                    arr[objRef->ValueLow] = (uint)val->Value;
                }
                else if (nT == typeof(sbyte))
                {
                    sbyte[] arr = mStack[objRef->Value] as sbyte[];
                    arr[objRef->ValueLow] = (sbyte)val->Value;
                }
                else
                    throw new NotImplementedException();
            }
            else
            {
                Array arr = mStack[objRef->Value] as Array;
                arr.SetValue(StackObject.ToObject(val, domain, mStack), objRef->ValueLow);
            }
        }

        bool CheckExceptionType(IType catchType, object exception, bool explicitMatch)
        {
            if (catchType == null)
                return true;
            if (catchType is CLRType)
            {
                if (explicitMatch)
                    return exception.GetType() == catchType.TypeForCLR;
                else
                    return catchType.TypeForCLR.IsAssignableFrom(exception.GetType());
            }
            else
                throw new NotImplementedException();
        }
#if NET_4_6 || NET_STANDARD_2_0
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public static StackObject* ResolveReference(StackObject* esp)
        {
            var addr = *(long*)&esp->Value;
            return (StackObject*)addr;
        }

#if NET_4_6 || NET_STANDARD_2_0
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public static StackObject* GetObjectAndResolveReference(StackObject* esp)
        {
            if (esp->ObjectType == ObjectTypes.StackObjectReference)
            {
                return ResolveReference(esp);
            }
            else
                return esp;
        }

        StackObject* PushParameters(IMethod method, StackObject* esp, object[] p)
        {
            IList<object> mStack = Stack.ManagedStack;
            var plist = method.Parameters;
            int pCnt = plist != null ? plist.Count : 0;
            int pCnt2 = p != null ? p.Length : 0;
            if (pCnt != pCnt2)
                throw new ArgumentOutOfRangeException($"Parameter mismatch");
            if (pCnt2 > 0)
            {
                for (int i = 0; i < p.Length; i++)
                {
                    bool isBox = false;
                    if (plist != null && i < plist.Count)
                        isBox = plist[i] == AppDomain.ObjectType;
                    object obj = p[i];
                    if (obj is CrossBindingAdaptorType)
                        obj = ((CrossBindingAdaptorType)obj).ILInstance;
                    var res = ILIntepreter.PushObject(esp, mStack, obj, isBox);
                    esp = res;
                }
            }

            return esp;
        }

        public void CopyToStack(StackObject* dst, StackObject* src, IList<object> mStack)
        {
            CopyToStack(dst, src, mStack, mStack);
        }

        void CopyToStack(StackObject* dst, StackObject* src, IList<object> mStack, IList<object> dstmStack)
        {
            if (src->ObjectType == ObjectTypes.ValueTypeObjectReference)
            {
                var descriptor = ResolveReference(src);
                var t = domain.GetTypeByIndex(descriptor->Value);
                AllocValueType(dst, t);
                CopyStackValueType(src, dst, mStack, dstmStack);
            }
            else
            {
                *dst = *src;
                if (dst->ObjectType >= ObjectTypes.Object)
                {
                    dst->Value = dstmStack.Count;
                    var obj = mStack[src->Value];
                    dstmStack.Add(obj);
                }
            }
        }

        internal static object CheckAndCloneValueType(object obj, Enviorment.AppDomain domain)
        {
            if (obj != null)
            {
                if (obj is ILTypeInstance)
                {
                    ILTypeInstance ins = obj as ILTypeInstance;
                    if (ins.IsValueType)
                    {
                        return ins.Clone();
                    }
                }
                else
                {
                    var type = obj.GetType();
                    var typeFlags = type.GetTypeFlags();

                    var isPrimitive = (typeFlags & CLR.Utils.Extensions.TypeFlags.IsPrimitive) != 0;
                    var isValueType = (typeFlags & CLR.Utils.Extensions.TypeFlags.IsValueType) != 0;

                    if (!isPrimitive && isValueType)
                    {
                        var t = domain.GetType(type);
                        return ((CLRType)t).PerformMemberwiseClone(obj);
                    }
                }
            }

            return obj;
        }
#if NET_4_6 || NET_STANDARD_2_0
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public static StackObject* PushOne(StackObject* esp)
        {
            esp->ObjectType = ObjectTypes.Integer;
            esp->Value = 1;
            return esp + 1;
        }

#if NET_4_6 || NET_STANDARD_2_0
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public static StackObject* PushZero(StackObject* esp)
        {
            esp->ObjectType = ObjectTypes.Integer;
            esp->Value = 0;
            return esp + 1;
        }

#if NET_4_6 || NET_STANDARD_2_0
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public static StackObject* PushNull(StackObject* esp)
        {
            esp->ObjectType = ObjectTypes.Null;
            esp->Value = -1;
            esp->ValueLow = 0;
            return esp + 1;
        }

        public static void UnboxObject(StackObject* esp, object obj, IList<object> mStack = null, Enviorment.AppDomain domain = null)
        {
            if (esp->ObjectType == ObjectTypes.ValueTypeObjectReference && domain != null)
            {
                var dst = ILIntepreter.ResolveReference(esp);
                var vt = domain.GetTypeByIndex(dst->Value);

                if (obj == null) //Nothing to do
                    return;
                if (obj is ILTypeInstance)
                {
                    var ins = (ILTypeInstance)obj;
                    ins.CopyValueTypeToStack(dst, mStack);
                }
                else if (obj is CrossBindingAdaptorType)
                {
                    var ins = ((CrossBindingAdaptorType)obj).ILInstance;
                    ins.CopyValueTypeToStack(dst, mStack);
                }
                else
                {
                    ((CLRType)vt).ValueTypeBinder.CopyValueTypeToStack(obj, dst, mStack);
                }
            }
            else if (obj == null)
            {
                return;
            }
            else if (obj is int)
            {
                esp->ObjectType = ObjectTypes.Integer;
                esp->Value = (int)obj;
            }
            else if (obj is bool)
            {
                esp->ObjectType = ObjectTypes.Integer;
                esp->Value = (bool)(obj) ? 1 : 0;
            }
            else if (obj is short)
            {
                esp->ObjectType = ObjectTypes.Integer;
                esp->Value = (short)obj;
            }
            else if (obj is long)
            {
                esp->ObjectType = ObjectTypes.Long;
                *(long*)(&esp->Value) = (long)obj;
            }
            else if (obj is float)
            {
                esp->ObjectType = ObjectTypes.Float;
                *(float*)(&esp->Value) = (float)obj;
            }
            else if (obj is byte)
            {
                esp->ObjectType = ObjectTypes.Integer;
                esp->Value = (byte)obj;
            }
            else if (obj is uint)
            {
                esp->ObjectType = ObjectTypes.Integer;
                esp->Value = (int)(uint)obj;
            }
            else if (obj is ushort)
            {
                esp->ObjectType = ObjectTypes.Integer;
                esp->Value = (int)(ushort)obj;
            }
            else if (obj is char)
            {
                esp->ObjectType = ObjectTypes.Integer;
                esp->Value = (int)(char)obj;
            }
            else if (obj is double)
            {
                esp->ObjectType = ObjectTypes.Double;
                *(double*)(&esp->Value) = (double)obj;
            }
            else if (obj is ulong)
            {
                esp->ObjectType = ObjectTypes.Long;
                *(ulong*)(&esp->Value) = (ulong)obj;
            }
            else if (obj is sbyte)
            {
                esp->ObjectType = ObjectTypes.Integer;
                esp->Value = (sbyte)obj;
            }
            else if (obj is Enum)
            {
                esp->ObjectType = ObjectTypes.Integer;
                esp->Value = Convert.ToInt32(obj);
            }
            else
                throw new NotImplementedException();
        }

        public static StackObject* PushObject(StackObject* esp, IList<object> mStack, object obj, bool isBox = false)
        {
            if (obj != null)
            {
                if (!isBox)
                {
                    var typeFlags = obj.GetType().GetTypeFlags();

                    if ((typeFlags & CLR.Utils.Extensions.TypeFlags.IsPrimitive) != 0)
                    {
                        UnboxObject(esp, obj, mStack);
                    }
                    else if ((typeFlags & CLR.Utils.Extensions.TypeFlags.IsEnum) != 0)
                    {
                        esp->ObjectType = ObjectTypes.Integer;
                        esp->Value = Convert.ToInt32(obj);
                    }
                    else
                    {
                        esp->ObjectType = ObjectTypes.Object;
                        esp->Value = mStack.Count;
                        mStack.Add(obj);
                    }
                }
                else
                {
                    esp->ObjectType = ObjectTypes.Object;
                    esp->Value = mStack.Count;
                    mStack.Add(obj);
                }
            }
            else
            {
                if (isBox)
                {
                    esp->ObjectType = ObjectTypes.Object;
                    esp->Value = mStack.Count;
                    mStack.Add(obj);
                }
                else
                    return PushNull(esp);
            }

            return esp + 1;
        }

        //Don't ask me why add this funky method for this, otherwise Unity won't calculate the right value
#if NET_4_6 || NET_STANDARD_2_0
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public static StackObject* Add(StackObject* a, int b)
        {
            return (StackObject*)((long)a + sizeof(StackObject) * b);
        }

#if NET_4_6 || NET_STANDARD_2_0
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public static StackObject* Minus(StackObject* a, int b)
        {
            return (StackObject*)((long)a - sizeof(StackObject) * b);
        }

#if NET_4_6 || NET_STANDARD_2_0
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public void Free(StackObject* esp)
        {
            switch (esp->ObjectType)
            {
                case ObjectTypes.Object:
                case ObjectTypes.FieldReference:
                case ObjectTypes.ArrayReference:
                    var mStack = Stack.ManagedStack;
                    if (esp->Value == mStack.Count - 1)
                        mStack.RemoveAt(esp->Value);
                    break;
                case ObjectTypes.ValueTypeObjectReference:
                    FreeStackValueType(esp);
                    break;
            }
#if DEBUG && !DISABLE_ILRUNTIME_DEBUG
            esp->ObjectType = ObjectTypes.Null;
            esp->Value = -1;
            esp->ValueLow = 0;
#endif
        }

        public void FreeStackValueType(StackObject* esp)
        {
            if (esp->ObjectType == ObjectTypes.ValueTypeObjectReference)
            {
                var addr = ILIntepreter.ResolveReference(esp);
                if (addr <= ValueTypeBasePointer) //Only Stack allocation after base pointer should be freed, local variable are freed automatically
                    Stack.FreeValueTypeObject(esp);
                esp->ObjectType = ObjectTypes.Null;
            }
        }

        public void AllocValueType(StackObject* ptr, IType type)
        {
            Stack.AllocValueType(ptr, type);
        }
    }
}