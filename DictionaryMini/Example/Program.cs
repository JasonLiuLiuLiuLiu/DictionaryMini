using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DictionaryMini;

namespace Example
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var dicMini = new DictionaryMini<int, string>(5);
            dicMini.Add(1, "liu ZhenYu");
            dicMini.Add(new KeyValuePair<int, string>(2, "coder ayu"));

            Console.WriteLine(dicMini[1]);
            Console.WriteLine(dicMini[2]);

            dicMini.Remove(1);
            // Console.WriteLine(dicMini[1]);

            dicMini.Clear();
            // Console.WriteLine(dicMini[2]);
        }
    }
}