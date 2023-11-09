using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWPF
{
    class GlobalValue
    {
        public static string GamePath { get; set; }
        public static int InvalidIndex
        {
            get { return -1; }
        }
    }
}
