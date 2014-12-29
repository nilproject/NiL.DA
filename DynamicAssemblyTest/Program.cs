using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicAssemblyTest
{
    class Program
    {
        public class FirstJSClassDeriver : FirstJSClass
        {
            public override object Message
            {
                get
                {
                    return "Other message for " + Name;
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine(new FirstJSClassDeriver().Message);
            Console.WriteLine(new FirstJSClassDeriver().GetMessage());
        }
    }
}
