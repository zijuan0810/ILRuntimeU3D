using UnityEngine;
using System.IO;
using ILRuntime.Runtime.Enviorment;


public class LitJsonDemo : MonoBehaviour
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
        _appDomain = new AppDomain() {Name = "LitJsonDemo"};
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
        //这里做一些ILRuntime的注册，这里我们对LitJson进行注册
        LitJson.JsonMapper.RegisterILRuntimeCLRRedirection(_appDomain);
    }

    private void OnHotFixLoaded()
    {
        Debug.Log("LitJson在使用前需要初始化，请看InitliazeILRuntime方法中的初始化");
        Debug.Log("LitJson的使用很简单，JsonMapper类里面提供了对象到Json以及Json到对象的转换方法");
        Debug.Log("具体使用方法请看热更项目中的代码");
        //调用无参数静态方法，appdomain.Invoke("类名", "方法名", 对象引用, 参数列表);
        _appDomain.Invoke("Hotfix.TestJson", "RunTest", null, null);
    }

    private void OnDestroy()
    {
        _stream?.Close();
        _symbol?.Close();
        _stream = null;
        _symbol = null;
    }
}