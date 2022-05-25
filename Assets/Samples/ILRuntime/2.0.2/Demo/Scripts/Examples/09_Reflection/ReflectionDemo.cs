using System;
using UnityEngine;
using System.IO;
using System.Reflection;
using ILRuntime.CLR.TypeSystem;
using AppDomain = ILRuntime.Runtime.Enviorment.AppDomain;

public class ReflectionDemo : MonoBehaviour
{
    //AppDomain是ILRuntime的入口，最好是在一个单例类中保存，整个游戏全局就一个，这里为了示例方便，每个例子里面都单独做了一个
    //大家在正式项目中请全局只创建一个AppDomain
    private AppDomain _appDomain;
    private MemoryStream _stream;
    private MemoryStream _symbol;

    private void Start()
    {
        LoadHotFixAssembly();
    }

    private void LoadHotFixAssembly()
    {
        _appDomain = new AppDomain() {Name = "ReflectionDemo"};
        _stream = new MemoryStream(File.ReadAllBytes("Library/ScriptAssemblies/Hotfix.dll"));
        _symbol = new MemoryStream(File.ReadAllBytes("Library/ScriptAssemblies/Hotfix.pdb"));
        try
        {
            _appDomain.LoadAssembly(_stream, _symbol, new ILRuntime.Mono.Cecil.Pdb.PdbReaderProvider());
        }
        catch
        {
            Debug.LogError("加载热更DLL失败，请确保已经通过VS打开Assets/Samples/ILRuntime/1.6/Demo/Hotfix/Hotfix.sln编译过热更DLL");
        }

        InitializeILRuntime();
        OnHotFixLoaded();
    }

    private void InitializeILRuntime()
    {
#if DEBUG && (UNITY_EDITOR || UNITY_ANDROID || UNITY_IPHONE)
        //由于Unity的Profiler接口只允许在主线程使用，为了避免出异常，需要告诉ILRuntime主线程的线程ID才能正确将函数运行耗时报告给Profiler
        _appDomain.UnityMainThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
        //这里做一些ILRuntime的注册，比如委托的适配器，但是为了演示不些适配器的报错，注册写在了OnHotFixLoaded里
    }

    private void OnHotFixLoaded()
    {
        Debug.Log("C#工程中反射是一个非常经常用到功能，ILRuntime也对反射进行了支持，在热更DLL中使用反射跟原生C#没有任何区别，故不做介绍");
        Debug.Log("这个Demo主要是介绍如何在主工程中反射热更DLL中的类型");
        Debug.Log("假设我们要通过反射创建HotFix_Project.InstanceClass的实例");
        Debug.Log("显然我们通过Activator或者Type.GetType(\"Hotfix.InstanceClass\")是无法取到类型信息的");
        Debug.Log("热更DLL中的类型我们均需要通过AppDomain取得");
        
        var it = _appDomain.LoadedTypes["Hotfix.InstanceClass"];
        Debug.Log("LoadedTypes返回的是IType类型，但是我们需要获得对应的System.Type才能继续使用反射接口");
        var type = it.ReflectionType;
        Debug.Log("取得Type之后就可以按照我们熟悉的方式来反射调用了");
        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor != null)
        {
            var obj = ctor.Invoke(null);
            Debug.Log("打印一下结果");
            Debug.Log(obj);
            Debug.Log("我们试一下用反射给字段赋值");
            var fi = type.GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null)
                fi.SetValue(obj, 111111);
            Debug.Log("我们用反射调用属性检查刚刚的赋值");
            var pi = type.GetProperty("ID");
            if (pi != null)
                Debug.Log("ID = " + pi.GetValue(obj, null));

            obj = ((ILType) it).Instantiate();
            pi = type.GetProperty("ID");
            if (pi != null)
                Debug.Log("ID2 = " + pi.GetValue(obj, null));
        }

        if (type is ILRuntime.Reflection.ILRuntimeType ilt)
        {
            var iltype = ilt.ILType;
            var gargs = ilt.GenericTypeArguments;
        }
        else if (type is ILRuntime.Reflection.ILRuntimeWrapperType clrType)
        {
            var gargs = clrType.GenericTypeArguments;
        }
    }

    private void OnDestroy()
    {
        _stream?.Close();
        _symbol?.Close();
        _stream = null;
        _symbol = null;
    }
}