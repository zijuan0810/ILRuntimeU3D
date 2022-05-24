using UnityEngine;
using System.IO;
using ILRuntime.CLR.Method;
using ILRuntime.Runtime.Enviorment;
using UnityEngine.Profiling;

public class CLRBindingTestClass
{
    public static float DoSomeTest(int a, float b)
    {
        return a + b;
    }
}

public class CLRBindingDemo : MonoBehaviour
{
    //AppDomain是ILRuntime的入口，最好是在一个单例类中保存，整个游戏全局就一个，这里为了示例方便，每个例子里面都单独做了一个
    //大家在正式项目中请全局只创建一个AppDomain
    private AppDomain _appDomain;
    private MemoryStream _stream;
    private MemoryStream _symbol;

    private bool _executed;
    private bool _ilruntimeReady;

    private void Start()
    {
        LoadHotFixAssembly();
    }

    private void LoadHotFixAssembly()
    {
        _appDomain = new AppDomain() {Name = "CLRBindingDemo"};
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
        //这里做一些ILRuntime的注册，如委托适配器，值类型绑定等等


        //初始化CLR绑定请放在初始化的最后一步！！
        //初始化CLR绑定请放在初始化的最后一步！！
        //初始化CLR绑定请放在初始化的最后一步！！

        //请在生成了绑定代码后解除下面这行的注释
        //请在生成了绑定代码后解除下面这行的注释
        //请在生成了绑定代码后解除下面这行的注释
        ILRuntime.Runtime.Generated.CLRBindings.Initialize(_appDomain);
    }

    private void OnHotFixLoaded()
    {
        _ilruntimeReady = true;
    }


    private void Update()
    {
        if (_ilruntimeReady && !_executed && Time.realtimeSinceStartup > 3)
        {
            _executed = true;
            //这里为了方便看Profiler，代码挪到Update中了
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Debug.LogWarning("运行这个Demo前请先点击菜单ILRuntime->Generate来生成所需的绑定代码，并按照提示解除下面相关代码的注释");
            Debug.Log("默认情况下，从热更DLL里调用Unity主工程的方法，是通过反射的方式调用的，这个过程中会产生GC Alloc，并且执行效率会偏低");

            Debug.Log("请在Unity菜单里面的ILRuntime->Generate CLR Binding Code by Analysis来生成绑定代码");

            var type = _appDomain.LoadedTypes["Hotfix.TestCLRBinding"];
            var m = type.GetMethod("RunTest", 0);
            Debug.Log("请解除InitializeILRuntime方法中的注释对比有无CLR绑定对运行耗时和GC开销的影响");
            sw.Reset();
            sw.Start();
            Profiler.BeginSample("RunTest2");
            _appDomain.Invoke(m, null, null);
            Profiler.EndSample();
            sw.Stop();
            Debug.LogFormat("刚刚的方法执行了:{0} ms", sw.ElapsedMilliseconds);

            Debug.Log(
                "可以看到运行时间和GC Alloc有大量的差别，RunTest2之所以有20字节的GC Alloc是因为Editor模式ILRuntime会有调试支持，正式发布（关闭Development Build）时这20字节也会随之消失");
        }
    }

    private void RunTest()
    {
        _appDomain.Invoke("Hotfix.TestCLRBinding", "RunTest", null, null);
    }

    private void RunTest2(IMethod m)
    {
        _appDomain.Invoke(m, null, null);
    }

    private void OnDestroy()
    {
        _stream?.Close();
        _symbol?.Close();
        _stream = null;
        _symbol = null;
    }
}