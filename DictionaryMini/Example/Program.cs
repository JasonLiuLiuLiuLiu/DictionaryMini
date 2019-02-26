using System;
using System.Collections.Generic;
using System.Threading;
using DictionaryMini;

namespace Example
{
    internal class Program
    {
        private static ConcurrentDictionaryMini<int, DateTime> _concurrentDictionaryMini = new ConcurrentDictionaryMini<int, DateTime>();
        private static Random _rd = new Random();

        private static void Main()
        {
            Console.WriteLine("Demo of diction mini");
            DemoDictionMini();

            Console.WriteLine("Demo of ConcurrentDictionaryMini");
            DemoConcurrentDictionaryMini();
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
            var threadList = new List<Thread>();
            for (int i = 0; i < 4; i++)
            {
                threadList.Add(new Thread(WriteMethod));
                threadList.Add(new Thread(ReadMethod));
                threadList.Add(new Thread(RemoveMethod));
            }
            threadList.ForEach(u => u.Start());

            Thread.Sleep(1000);

            threadList.ForEach(u => u.Abort());

            Console.ReadLine();
        }

        private static void WriteMethod()
        {
            while (true)
            {
                var index = GetRandomIndex();
                var value = DateTime.Now;
                if (_concurrentDictionaryMini.TryAdd(index, value))
                {
                    Console.WriteLine($"Success Add,key:{index},value:{value}");
                }
                else
                {
                    Console.WriteLine($"Fail Add,key:{index},value:{value}");
                }
            }
        }

        private static void ReadMethod()
        {
            while (true)
            {
                var index = GetRandomIndex();
                if (_concurrentDictionaryMini.TryGetValue(index, out DateTime value))
                {
                    Console.WriteLine($"Success Get,key:{index},value:{value}");
                }
                else
                {
                    Console.WriteLine($"Fail Get,key:{index}");
                }
            }
        }

        private static void RemoveMethod()
        {
            while (true)
            {
                var index = GetRandomIndex();
                if (_concurrentDictionaryMini.TryRemove(index, out DateTime value))
                {
                    Console.WriteLine($"Success Remove,key:{index},value:{value}");
                }
                else
                {
                    Console.WriteLine($"Fail Remove,key:{index}");
                }
            }
        }

        private static int GetRandomIndex()
        {
            return _rd.Next(0, 10);
        }
    }
}