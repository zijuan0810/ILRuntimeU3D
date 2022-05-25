using UnityEngine;
using System.Collections.Generic;
using System.IO;
using ILRuntime.CLR.TypeSystem;
using ILRuntime.CLR.Method;
using ILRuntime.Runtime.Enviorment;
using System.Threading;

public class Invocation : MonoBehaviour
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
        _appDomain = new AppDomain() {Name = "Invocation"};
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
        _appDomain.UnityMainThreadID = Thread.CurrentThread.ManagedThreadId;
#endif
        //这里做一些ILRuntime的注册，这个示例暂时没有需要注册的
    }

    private void OnHotFixLoaded()
    {
        Debug.Log("调用无参数静态方法");
        //调用无参数静态方法，appdomain.Invoke("类名", "方法名", 对象引用, 参数列表);
        _appDomain.Invoke("Hotfix.InstanceClass", "StaticFunTest", null, null);
        //调用带参数的静态方法
        Debug.Log("调用带参数的静态方法");
        _appDomain.Invoke("Hotfix.InstanceClass", "StaticFunTest2", null, 123);


        Debug.Log("通过IMethod调用方法");
        //预先获得IMethod，可以减低每次调用查找方法耗用的时间
        IType type = _appDomain.LoadedTypes["Hotfix.InstanceClass"];
        //根据方法名称和参数个数获取方法
        IMethod method = type.GetMethod("StaticFunTest2", 1);

        _appDomain.Invoke(method, null, 123);

        Debug.Log("通过无GC Alloc方式调用方法");
        using (var ctx = _appDomain.BeginInvoke(method))
        {
            ctx.PushInteger(123);
            ctx.Invoke();
        }

        Debug.Log("指定参数类型来获得IMethod");
        IType intType = _appDomain.GetType(typeof(int));
        //参数类型列表
        List<IType> paramList = new List<IType>();
        paramList.Add(intType);
        //根据方法名称和参数类型列表获取方法
        method = type.GetMethod("StaticFunTest2", paramList, null);
        _appDomain.Invoke(method, null, 456);

        Debug.Log("实例化热更里的类");
        object obj = _appDomain.Instantiate("Hotfix.InstanceClass", new object[] {233});
        //第二种方式
        object obj2 = ((ILType) type).Instantiate();

        Debug.Log("调用成员方法");
        method = type.GetMethod("get_ID", 0);
        using (var ctx = _appDomain.BeginInvoke(method))
        {
            ctx.PushObject(obj);
            ctx.Invoke();
            int id = ctx.ReadInteger();
            Debug.Log("!! Hotfix.InstanceClass.ID = " + id);
        }

        using (var ctx = _appDomain.BeginInvoke(method))
        {
            ctx.PushObject(obj2);
            ctx.Invoke();
            int id = ctx.ReadInteger();
            Debug.Log("!! Hotfix.InstanceClass.ID = " + id);
        }

        Debug.Log("调用泛型方法");
        IType stringType = _appDomain.GetType(typeof(string));
        IType[] genericArguments = new IType[] {stringType};
        _appDomain.InvokeGenericMethod("HotFix.InstanceClass", "GenericMethod", genericArguments, null,
            "TestString");

        Debug.Log("获取泛型方法的IMethod");
        paramList.Clear();
        paramList.Add(intType);
        genericArguments = new[] {intType};
        method = type.GetMethod("GenericMethod", paramList, genericArguments);
        _appDomain.Invoke(method, null, 33333);

        Debug.Log("调用带Ref/Out参数的方法");
        method = type.GetMethod("RefOutMethod", 3);
        int initialVal = 500;
        using (var ctx = _appDomain.BeginInvoke(method))
        {
            //第一个ref/out参数初始值
            ctx.PushObject(null);
            //第二个ref/out参数初始值
            ctx.PushInteger(initialVal);
            //压入this
            ctx.PushObject(obj);
            //压入参数1:addition
            ctx.PushInteger(100);
            //压入参数2: lst,由于是ref/out，需要压引用，这里是引用0号位，也就是第一个PushObject的位置
            ctx.PushReference(0);
            //压入参数3,val，同ref/out
            ctx.PushReference(1);
            ctx.Invoke();
            //读取0号位的值
            List<int> lst = ctx.ReadObject<List<int>>(0);
            initialVal = ctx.ReadInteger(1);

            Debug.Log(string.Format("lst[0]={0}, initialVal={1}", lst[0], initialVal));
        }
    }

    private void OnDestroy()
    {
        _appDomain.Dispose();
        _stream?.Close();
        _symbol?.Close();
        _stream = null;
        _symbol = null;
    }
}