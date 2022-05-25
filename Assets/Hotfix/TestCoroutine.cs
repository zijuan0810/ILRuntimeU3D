using UnityEngine;

namespace Hotfix
{
    public class TestCoroutine
    {
        public static void RunTest()
        {
            CoroutineDemo.Instance.DoCoroutine(Coroutine());
        }

        private static System.Collections.IEnumerator Coroutine()
        {
            Debug.Log("开始协程,t=" + Time.time);
            yield return new WaitForSeconds(3);
            Debug.Log("等待了3秒,t=" + Time.time);
        }
    }
}