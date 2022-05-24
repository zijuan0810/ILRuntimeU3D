using UnityEngine;
using System.IO;
using UnityEngine.Profiling;

public class HelloWorld : MonoBehaviour
{
    private MemoryStream _stream;
    private MemoryStream _symbol;
    private ILRuntime.Runtime.Enviorment.AppDomain _appDomain;

    private void Start()
    {
    }

    private void LoadHotFixAssembly()
    {
        //首先实例化ILRuntime的AppDomain，AppDomain是一个应用程序域，每个AppDomain都是一个独立的沙盒
        _appDomain = new ILRuntime.Runtime.Enviorment.AppDomain {Name = "HellWorld"};
        //正常项目中应该是自行从其他地方下载dll，或者打包在AssetBundle中读取，平时开发以及为了演示方便直接从StreammingAssets中读取，
        //正式发布的时候需要大家自行从其他地方读取dll

#if UNITY_EDITOR
        _stream = new MemoryStream(File.ReadAllBytes("Library/ScriptAssemblies/Hotfix.dll"));
        _symbol = new MemoryStream(File.ReadAllBytes("Library/ScriptAssemblies/Hotfix.pdb"));
        _appDomain.LoadAssembly(_stream, _symbol, new ILRuntime.Mono.Cecil.Pdb.PdbReaderProvider());
#else
        _stream = new MemoryStream(File.ReadAllBytes(Application.streamingAssetsPath + "/Hotfix.dll"));
        _appDomain.LoadAssembly(_stream, null, null);
#endif

        InitializeILRuntime();
        // OnHotFixLoaded();
    }

    private void InitializeILRuntime()
    {
#if DEBUG && (UNITY_EDITOR || UNITY_ANDROID || UNITY_IPHONE)
        //由于Unity的Profiler接口只允许在主线程使用，为了避免出异常，需要告诉ILRuntime主线程的线程ID才能正确将函数运行耗时报告给Profiler
        _appDomain.UnityMainThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
        //这里做一些ILRuntime的注册，HelloWorld示例暂时没有需要注册的
    }

    private void Test1()
    {
        _appDomain.Invoke("Hotfix.InstanceClass", "StaticFunTest", null, null);
    }
    
    private void Test2()
    {
        _appDomain.Invoke("Hotfix.InstanceClass", "StaticFunTest3", null, null);
    }


    private void OnDestroy()
    {
        _appDomain?.Dispose();
        _stream?.Close();
        _symbol?.Close();
        _stream = null;
        _symbol = null;
    }

    private void OnGUI()
    {
        var usedSize = Profiler.GetMonoUsedSizeLong();
        var heapSize = Profiler.GetMonoHeapSizeLong();
        var totalSize = Profiler.GetTotalAllocatedMemoryLong();
        
        GUI.Label(new Rect(20, 10, 300, 30), $"used: {usedSize / 1024f / 1024f}m");
        GUI.Label(new Rect(20, 40, 300, 30), $"heap: {heapSize / 1024f / 1024f}m");
        GUI.Label(new Rect(20, 70, 300, 30), $"totalSize: {heapSize / 1024f / 1024f}m");
        
        if (GUI.Button(new Rect(20f, 110f, 120f, 30f), "Load Hotfix"))
            LoadHotFixAssembly();
        if (GUI.Button(new Rect(20f, 150f, 120f, 30f), "Test 1"))
            Test1();
        if (GUI.Button(new Rect(20f, 190f, 120f, 30f), "Test 2"))
            Test2();
    }
}