using System;
using UnityEngine;
using System.IO;
using AppDomain = ILRuntime.Runtime.Enviorment.AppDomain;

public delegate void MainDelegateMethod(int a);

public delegate string MainDelegateFunction(int a);


public class DelegateDemo : MonoBehaviour
{
    public static MainDelegateMethod MainMethodDelegate;
    public static MainDelegateFunction MainFunctionDelegate;
    public static Action<string> MainActionDelegate;

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
        _appDomain.UnityMainThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif

        //热更工程中创建的委托实例可以直接在热更工程里面使用，无须注册
        // _appDomain.Invoke("Hotfix.TestDelegate", "Initialize", null, null);
        // _appDomain.Invoke("Hotfix.TestDelegate", "RunTest", null, null);

        //TestDelegateMethod, 这个委托类型为有个参数为int的方法，注册仅需要注册不同的参数搭配即可
        _appDomain.DelegateManager.RegisterMethodDelegate<int>();
        //ILRuntime内部是用Action和Func这两个系统内置的委托类型来创建实例的，所以其他的委托类型都需要写转换器
        //将Action或者Func转换成目标委托类型
        _appDomain.DelegateManager.RegisterDelegateConvertor<MainDelegateMethod>((action) =>
        {
            //转换器的目的是把Action或者Func转换成正确的类型，这里则是把Action<int>转换成TestDelegateMethod
            return new MainDelegateMethod((a) =>
            {
                //调用委托实例
                ((Action<int>) action)(a);
            });
        });

        //带返回值的委托的话需要用RegisterFunctionDelegate，返回类型为最后一个
        _appDomain.DelegateManager.RegisterFunctionDelegate<int, string>();
        _appDomain.DelegateManager.RegisterDelegateConvertor<MainDelegateFunction>(action =>
        {
            return new MainDelegateFunction(a => ((Func<int, string>) action)(a));
        });

        //Action<string> 的参数为一个string
        _appDomain.DelegateManager.RegisterMethodDelegate<string>();

        try
        {
            _appDomain.Invoke("Hotfix.TestDelegate", "Initialize2", null, null);
            _appDomain.Invoke("Hotfix.TestDelegate", "RunTest2", null, null);
        }
        catch (Exception e)
        {
            Debug.LogError(e.ToString());
        }

        
    }

    private void OnHotFixLoaded()
    {
        // Debug.Log("完全在热更DLL内部使用的委托，直接可用，不需要做任何处理");
        // _appDomain.Invoke("Hotfix.TestDelegate", "Initialize", null, null);
        // _appDomain.Invoke("Hotfix.TestDelegate", "RunTest", null, null);

        // Debug.Log("如果需要跨域调用委托（将热更DLL里面的委托实例传到Unity主工程用）, 就需要注册适配器");
        // Debug.Log("这是因为iOS的IL2CPP模式下，不能动态生成类型，为了避免出现不可预知的问题，我们没有通过反射的方式创建委托实例，因此需要手动进行一些注册");
        // Debug.Log("如果没有注册委托适配器，运行时会报错并提示需要的注册代码，直接复制粘贴到ILRuntime初始化的地方");
        // _appDomain.Invoke("Hotfix.TestDelegate", "Initialize2", null, null);
        // _appDomain.Invoke("Hotfix.TestDelegate", "RunTest2", null, null);
        // Debug.Log("运行成功，我们可以看见，用Action或者Func当作委托类型的话，可以避免写转换器，所以项目中在不必要的情况下尽量只用Action和Func");
        // Debug.Log("另外应该尽量减少不必要的跨域委托调用，如果委托只在热更DLL中用，是不需要进行任何注册的");
        // Debug.Log("---------");
        // Debug.Log("我们再来在Unity主工程中调用一下刚刚的委托试试");
        // TestMethodDelegate(789);
        // var str = TestFunctionDelegate(098);
        // Debug.Log("!! OnHotFixLoaded str = " + str);
        // TestActionDelegate("Hello From Unity Main Project");
    }

    private void OnDestroy()
    {
        _stream?.Close();
        _symbol?.Close();
        _stream = null;
        _symbol = null;
    }
}