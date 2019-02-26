using System;
using System.Collections.Generic;
using System.Threading;
using DictionaryMini;

namespace Example
{
    internal class Program
    {
        private static ConcurrentDictionaryMini<int, DateTime> concurrentDictionaryMini = new ConcurrentDictionaryMini<int, DateTime>();

        private static void Main()
        {
            Console.WriteLine("Demo of diction mini");
            DemoDictionMini();

            Console.WriteLine("Demo of ConcurrentDictionaryMini");
        }

        private static void DemoDictionMini()
        {
            var dicMini = new DictionaryMini<int, string>(5);
            dicMini.Add(1, "liu ZhenYu");
            dicMini.Add(new KeyValuePair<int, string>(2, "coder ayu"));

            Console.WriteLine(dicMini[1]);
            Console.WriteLine(dicMini[2]);

            dicMini.Remove(1);
            Console.WriteLine(dicMini.ContainsKey(1));

            dicMini.Clear();
            Console.WriteLine(dicMini.ContainsKey(2));
        }

        private static void DemoConcurrentDictionaryMini()
        {
            var writeThread = new Thread(WriteMethod);
            var readThread = new Thread(ReadMethod);
            var removeThread = new Thread(RemoveMethod);
        }

        private static void WriteMethod()
        {
        }

        private static void ReadMethod()
        {
        }

        private static void RemoveMethod()
        {
        }
    }
}