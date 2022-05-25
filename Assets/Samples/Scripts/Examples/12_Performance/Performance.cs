//#define XLUA_INSTALLED

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using ILRuntime.Mono.Cecil.Pdb;
using ILRuntime.Runtime;
using ILRuntime.Runtime.CLRBinding;
using ILRuntime.Runtime.Enviorment;
using UnityEngine;
using UnityEngine.UI;

#if XLUA_INSTALLED
using XLua;
//下面这行为了取消使用WWW的警告，Unity2018以后推荐使用UnityWebRequest，处于兼容性考虑Demo依然使用WWW
#pragma warning disable CS0618
[LuaCallCSharp]
#else
//下面这行为了取消使用WWW的警告，Unity2018以后推荐使用UnityWebRequest，处于兼容性考虑Demo依然使用WWW
#pragma warning disable CS0618
#endif

public class Performance : MonoBehaviour
{
    public Button btnLoadStack;
    public Button btnLoadRegister;
    public Button btnUnload;
    public CanvasGroup panelTest;
    public RectTransform panelButton;

    public Text lbResult;

    //AppDomain是ILRuntime的入口，最好是在一个单例类中保存，整个游戏全局就一个，这里为了示例方便，每个例子里面都单独做了一个
    //大家在正式项目中请全局只创建一个AppDomain
    private AppDomain _appDomain;

    private MemoryStream _stream;
    private MemoryStream _symbol;

#if XLUA_INSTALLED
    LuaEnv luaenv = null;
    [XLua.CSharpCallLua]
    public delegate void LuaCallPerfCase(StringBuilder sb);
#endif
    private List<string> tests = new List<string>();

    private void Awake()
    {
        tests.Add("TestMandelbrot");
        tests.Add("Test0");
        tests.Add("Test1");
        tests.Add("Test2");
        tests.Add("Test3");
        tests.Add("Test4");
        tests.Add("Test5");
        tests.Add("Test6");
        tests.Add("Test7");
        tests.Add("Test8");
        tests.Add("Test9");
        tests.Add("Test10");
        tests.Add("Test11");
        var go = panelButton.GetChild(0).gameObject;
        go.SetActive(false);

        foreach (var i in tests)
        {
            var child = Instantiate(go, panelButton, true);
            CreateTestButton(i, child);
            child.SetActive(true);
        }
    }

    private void CreateTestButton(string testName, GameObject go)
    {
        Button btn = go.GetComponent<Button>();
        Text txt = go.GetComponentInChildren<Text>();
        txt.text = testName;
        btn.onClick.AddListener(() =>
        {
            StringBuilder sb = new StringBuilder();
#if UNITY_EDITOR || DEBUG
            sb.AppendLine("请打包工程至非Development Build，并安装到真机再测试，编辑器中性能差异巨大，当前测试结果不具备测试意义");
#endif
#if XLUA_INSTALLED
            if (luaenv != null)
            {
                var perf = luaenv.Global.GetInPath<LuaCallPerfCase>(testName);
                perf(sb);
            }
            else
#endif
            _appDomain.Invoke("Hotfix.TestPerformance", testName, null, sb);
            lbResult.text = sb.ToString();
        });
    }

    public void LoadHotFixAssemblyStack()
    {
        //首先实例化ILRuntime的AppDomain，AppDomain是一个应用程序域，每个AppDomain都是一个独立的沙盒
        _appDomain = new AppDomain();
        LoadHotFixAssembly();
    }

    public void LoadHotFixAssemblyRegister()
    {
        //首先实例化ILRuntime的AppDomain，AppDomain是一个应用程序域，每个AppDomain都是一个独立的沙盒
        //ILRuntimeJITFlags.JITImmediately表示默认使用寄存器VM执行所有方法
        _appDomain = new AppDomain(ILRuntimeJITFlags.JITImmediately);
        LoadHotFixAssembly();
    }

    public void LoadLua()
    {
#if XLUA_INSTALLED
        string luaStr = @"require 'performance'";
        luaenv = new LuaEnv();
        luaenv.DoString(luaStr);
#else
        lbResult.text = "请自行安装XLua并生成xlua绑定代码，将performance.lua复制到StreamingAssets后，解除Performace.cs第一行注释";
        Debug.LogError("请自行安装XLua并生成xlua绑定代码后，将performance.lua复制到StreamingAssets后，解除Performace.cs第一行注释");
#endif
        OnHotFixLoaded();
    }

    private void LoadHotFixAssembly()
    {
        btnLoadRegister.interactable = false;
        btnLoadStack.interactable = false;
        _appDomain = new AppDomain() {Name = "LitJsonDemo"};
        _stream = new MemoryStream(File.ReadAllBytes("Library/ScriptAssemblies/Hotfix.dll"));
        _symbol = new MemoryStream(File.ReadAllBytes("Library/ScriptAssemblies/Hotfix.pdb"));
        try
        {
            _appDomain.LoadAssembly(_stream, _symbol, new PdbReaderProvider());
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
        _appDomain.RegisterValueTypeBinder(typeof(Vector3), new Vector3Binder());
        _appDomain.RegisterValueTypeBinder(typeof(Quaternion), new QuaternionBinder());
        _appDomain.RegisterValueTypeBinder(typeof(Vector2), new Vector2Binder());
        CLRBindingUtils.Initialize(_appDomain);
    }

    private void OnHotFixLoaded()
    {
        btnUnload.interactable = true;
        panelTest.interactable = true;
    }

    public void Unload()
    {
        _stream?.Close();
        _symbol?.Close();
        _stream = null;
        _symbol = null;
        _appDomain = null;

#if XLUA_INSTALLED
        if (luaenv != null)
            luaenv.Dispose();
        luaenv = null;
#endif
        btnUnload.interactable = false;
        btnLoadRegister.interactable = true;
        btnLoadStack.interactable = true;
        panelTest.interactable = false;
    }

    private void OnDestroy()
    {
        _stream?.Close();
        _symbol?.Close();
        _stream = null;
        _symbol = null;
    }

    public static bool MandelbrotCheck(float workX, float workY)
    {
        return ((workX * workX) + (workY * workY)) < 4.0f;
    }

    public static void TestFunc1(int a, string b, Transform d)
    {
    }
}