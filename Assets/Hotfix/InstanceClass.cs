using System.Collections.Generic;

namespace Hotfix
{
    public class InstanceClass
    {
        private int id;

        private static List<int> numbers = new List<int>();

        public InstanceClass()
        {
            UnityEngine.Debug.Log("!!! InstanceClass::InstanceClass()");
            this.id = 0;
        }

        public InstanceClass(int id)
        {
            UnityEngine.Debug.Log("!!! InstanceClass::InstanceClass() id = " + id);
            this.id = id;
        }

        public int ID => id;

        // static method
        public static void StaticFunTest()
        {
            UnityEngine.Debug.Log("!!! InstanceClass.StaticFunTest()");
        }
        
        public static void StaticFunTest3()
        {
            for (int i = 0; i < 10000; i++)
                numbers.Add(i);
            
            UnityEngine.Debug.Log("!!! InstanceClass.StaticFunTest3()");
        }

        public static void StaticFunTest2(int a)
        {
            UnityEngine.Debug.Log("!!! InstanceClass.StaticFunTest2(), a=" + a);
        }

        public static void GenericMethod<T>(T a)
        {
            UnityEngine.Debug.Log("!!! InstanceClass.GenericMethod(), a=" + a);
        }

        public void RefOutMethod(int addition, out List<int> lst, ref int val)
        {
            val = val + addition + id;
            lst = new List<int> {id};
        }
    }
}