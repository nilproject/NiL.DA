using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicAssmsBuilder
{
    internal class TestClass
    {
        private static object zero = 0;

        public TestClass()
        {
            Console.WriteLine(new object());
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var assm = new Assembly(new StreamReader(new FileStream("source.js", FileMode.Open, FileAccess.Read)).ReadToEnd())
                .Save("testAsm.dll", "testAsm");
            var types = assm.GetTypes();
            var field = types[0].GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var ctor = field[0].GetValue(null);
            var dinstance = Activator.CreateInstance(types[1]);
            types[1].GetProperty("Name").GetSetMethod().Invoke(dinstance, new[] { "Habrahabr" });
            var value = types[1].GetMethod("GetMessage").Invoke(dinstance, null);
            Console.WriteLine(value);
        }
    }
}
