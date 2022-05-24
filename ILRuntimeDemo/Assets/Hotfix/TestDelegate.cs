using System;

namespace Hotfix
{
    public class TestDelegate
    {
        private static MainDelegateMethod delegateMethod;
        private static MainDelegateFunction delegateFunc;
        private static Action<string> delegateAction;

        public static void Initialize()
        {
            delegateMethod = OnMethod;
            delegateFunc = OnFunction;
            delegateAction = OnAction;
        }

        public static void RunTest()
        {
            delegateMethod(123);
            var res = delegateFunc(456);
            UnityEngine.Debug.Log("!! TestDelegate.RunTest res = " + res);
            delegateAction("rrr");
        }

        public static void Initialize2()
        {
            DelegateDemo.MainMethodDelegate = OnMethod;
            DelegateDemo.MainFunctionDelegate = OnFunction;
            DelegateDemo.MainActionDelegate = OnAction;
        }

        public static void RunTest2()
        {
            DelegateDemo.MainMethodDelegate(123);
            var res = DelegateDemo.MainFunctionDelegate(456);
            UnityEngine.Debug.Log("!!主工程调用热更工程中的委托：TestDelegate.RunTest2 res = " + res);
            DelegateDemo.MainActionDelegate("rrr");
        }

        private static void OnMethod(int a)
        {
            UnityEngine.Debug.Log("!! TestDelegate.Method, a = " + a);
        }

        private static string OnFunction(int a)
        {
            return a.ToString();
        }

        private static void OnAction(string a)
        {
            UnityEngine.Debug.Log("!! TestDelegate.Action, a = " + a);
        }
    }
}
