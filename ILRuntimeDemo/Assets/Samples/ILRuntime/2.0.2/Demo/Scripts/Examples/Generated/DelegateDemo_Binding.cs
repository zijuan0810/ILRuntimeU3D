using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using ILRuntime.CLR.TypeSystem;
using ILRuntime.CLR.Method;
using ILRuntime.Runtime.Enviorment;
using ILRuntime.Runtime.Intepreter;
using ILRuntime.Runtime.Stack;
using ILRuntime.Reflection;
using ILRuntime.CLR.Utils;

namespace ILRuntime.Runtime.Generated
{
    internal static unsafe class DelegateDemo_Binding
    {
        public static void Register(ILRuntime.Runtime.Enviorment.AppDomain app)
        {
            var flag = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            FieldInfo field;
            Type[] args;
            Type type = typeof(global::DelegateDemo);

            field = type.GetField("MainMethodDelegate", flag);
            app.RegisterCLRFieldGetter(field, get_MainMethodDelegate_0);
            app.RegisterCLRFieldSetter(field, set_MainMethodDelegate_0);
            app.RegisterCLRFieldBinding(field, CopyToStack_MainMethodDelegate_0, AssignFromStack_MainMethodDelegate_0);
            field = type.GetField("MainFunctionDelegate", flag);
            app.RegisterCLRFieldGetter(field, get_MainFunctionDelegate_1);
            app.RegisterCLRFieldSetter(field, set_MainFunctionDelegate_1);
            app.RegisterCLRFieldBinding(field, CopyToStack_MainFunctionDelegate_1, AssignFromStack_MainFunctionDelegate_1);
            field = type.GetField("MainActionDelegate", flag);
            app.RegisterCLRFieldGetter(field, get_MainActionDelegate_2);
            app.RegisterCLRFieldSetter(field, set_MainActionDelegate_2);
            app.RegisterCLRFieldBinding(field, CopyToStack_MainActionDelegate_2, AssignFromStack_MainActionDelegate_2);


        }



        static object get_MainMethodDelegate_0(ref object o)
        {
            return global::DelegateDemo.MainMethodDelegate;
        }

        static StackObject* CopyToStack_MainMethodDelegate_0(ref object o, ILIntepreter __intp, StackObject* __ret, IList<object> __mStack)
        {
            var result_of_this_method = global::DelegateDemo.MainMethodDelegate;
            return ILIntepreter.PushObject(__ret, __mStack, result_of_this_method);
        }

        static void set_MainMethodDelegate_0(ref object o, object v)
        {
            global::DelegateDemo.MainMethodDelegate = (global::MainDelegateMethod)v;
        }

        static StackObject* AssignFromStack_MainMethodDelegate_0(ref object o, ILIntepreter __intp, StackObject* ptr_of_this_method, IList<object> __mStack)
        {
            ILRuntime.Runtime.Enviorment.AppDomain __domain = __intp.AppDomain;
            global::MainDelegateMethod @MainMethodDelegate = (global::MainDelegateMethod)typeof(global::MainDelegateMethod).CheckCLRTypes(StackObject.ToObject(ptr_of_this_method, __domain, __mStack), (CLR.Utils.Extensions.TypeFlags)8);
            global::DelegateDemo.MainMethodDelegate = @MainMethodDelegate;
            return ptr_of_this_method;
        }

        static object get_MainFunctionDelegate_1(ref object o)
        {
            return global::DelegateDemo.MainFunctionDelegate;
        }

        static StackObject* CopyToStack_MainFunctionDelegate_1(ref object o, ILIntepreter __intp, StackObject* __ret, IList<object> __mStack)
        {
            var result_of_this_method = global::DelegateDemo.MainFunctionDelegate;
            return ILIntepreter.PushObject(__ret, __mStack, result_of_this_method);
        }

        static void set_MainFunctionDelegate_1(ref object o, object v)
        {
            global::DelegateDemo.MainFunctionDelegate = (global::MainDelegateFunction)v;
        }

        static StackObject* AssignFromStack_MainFunctionDelegate_1(ref object o, ILIntepreter __intp, StackObject* ptr_of_this_method, IList<object> __mStack)
        {
            ILRuntime.Runtime.Enviorment.AppDomain __domain = __intp.AppDomain;
            global::MainDelegateFunction @MainFunctionDelegate = (global::MainDelegateFunction)typeof(global::MainDelegateFunction).CheckCLRTypes(StackObject.ToObject(ptr_of_this_method, __domain, __mStack), (CLR.Utils.Extensions.TypeFlags)8);
            global::DelegateDemo.MainFunctionDelegate = @MainFunctionDelegate;
            return ptr_of_this_method;
        }

        static object get_MainActionDelegate_2(ref object o)
        {
            return global::DelegateDemo.MainActionDelegate;
        }

        static StackObject* CopyToStack_MainActionDelegate_2(ref object o, ILIntepreter __intp, StackObject* __ret, IList<object> __mStack)
        {
            var result_of_this_method = global::DelegateDemo.MainActionDelegate;
            return ILIntepreter.PushObject(__ret, __mStack, result_of_this_method);
        }

        static void set_MainActionDelegate_2(ref object o, object v)
        {
            global::DelegateDemo.MainActionDelegate = (System.Action<System.String>)v;
        }

        static StackObject* AssignFromStack_MainActionDelegate_2(ref object o, ILIntepreter __intp, StackObject* ptr_of_this_method, IList<object> __mStack)
        {
            ILRuntime.Runtime.Enviorment.AppDomain __domain = __intp.AppDomain;
            System.Action<System.String> @MainActionDelegate = (System.Action<System.String>)typeof(System.Action<System.String>).CheckCLRTypes(StackObject.ToObject(ptr_of_this_method, __domain, __mStack), (CLR.Utils.Extensions.TypeFlags)8);
            global::DelegateDemo.MainActionDelegate = @MainActionDelegate;
            return ptr_of_this_method;
        }



    }
}
