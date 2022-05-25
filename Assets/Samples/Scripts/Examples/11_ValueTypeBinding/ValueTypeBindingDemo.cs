using UnityEngine;
using System.Collections;
using System.IO;
using ILRuntime.Runtime.Enviorment;

public class ValueTypeBindingDemo : MonoBehaviour
{
    //AppDomain是ILRuntime的入口，最好是在一个单例类中保存，整个游戏全局就一个，这里为了示例方便，每个例子里面都单独做了一个
    //大家在正式项目中请全局只创建一个AppDomain
    private AppDomain _appDomain;
    private MemoryStream _stream;
    private MemoryStream _symbol;

    private void Start()
    {
        StartCoroutine(LoadHotFixAssembly());
    }

    private IEnumerator LoadHotFixAssembly()
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
        yield return new WaitForSeconds(0.5f);
        RunTest();
        yield return new WaitForSeconds(0.5f);
        RunTest2();
        yield return new WaitForSeconds(0.5f);
        RunTest3();
    }

    private void InitializeILRuntime()
    {
#if DEBUG && (UNITY_EDITOR || UNITY_ANDROID || UNITY_IPHONE)
        //由于Unity的Profiler接口只允许在主线程使用，为了避免出异常，需要告诉ILRuntime主线程的线程ID才能正确将函数运行耗时报告给Profiler
        _appDomain.UnityMainThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
        //这里做一些ILRuntime的注册，这里我们注册值类型Binder，注释和解注下面的代码来对比性能差别
        _appDomain.RegisterValueTypeBinder(typeof(Vector2), new Vector2Binder());
        _appDomain.RegisterValueTypeBinder(typeof(Vector3), new Vector3Binder());
        _appDomain.RegisterValueTypeBinder(typeof(Quaternion), new QuaternionBinder());
    }

    private void RunTest()
    {
        Debug.Log("Vector3等Unity常用值类型如果不做任何处理，在ILRuntime中使用会产生较多额外的CPU开销和GC Alloc");
        Debug.Log("我们通过值类型绑定可以解决这个问题，只有Unity主工程的值类型才需要此处理，热更DLL内定义的值类型不需要任何处理");
        Debug.Log("请注释或者解注InitializeILRuntime里的代码来对比进行值类型绑定前后的性能差别");
        //调用无参数静态方法，appdomain.Invoke("类名", "方法名", 对象引用, 参数列表);
        _appDomain.Invoke("Hotfix.TestValueType", "RunTest", null, null);
    }

    private void RunTest2()
    {
        Debug.Log("=======================================");
        Debug.Log("Quaternion测试");
        //调用无参数静态方法，appdomain.Invoke("类名", "方法名", 对象引用, 参数列表);
        _appDomain.Invoke("Hotfix.TestValueType", "RunTest2", null, null);
    }

    private void RunTest3()
    {
        Debug.Log("=======================================");
        Debug.Log("Vector2测试");
        //调用无参数静态方法，appdomain.Invoke("类名", "方法名", 对象引用, 参数列表);
        _appDomain.Invoke("Hotfix.TestValueType", "RunTest3", null, null);
    }

    private void OnDestroy()
    {
        _stream?.Close();
        _symbol?.Close();
        _stream = null;
        _symbol = null;
    }
}