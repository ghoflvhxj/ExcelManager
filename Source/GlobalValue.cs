using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace TestWPF
{
    class GlobalValue
    {
        // This will get the current WORKING directory (i.e. \bin\Debug)
        public static string currentDirectory = Environment.CurrentDirectory;

        // This will get the current PROJECT bin directory (ie ../bin/)
        public static string binaryDirectory = Directory.GetParent(currentDirectory).Parent.Parent.FullName;

        // This will get the current PROJECT directory
        public static string projectDirectory = Directory.GetParent(currentDirectory).Parent.Parent.Parent.FullName;

        static public string dataDirectory = Path.Combine(GlobalValue.currentDirectory, "Data");

        public static string GamePath { get; set; }
        public static int InvalidIndex
        {
            get { return -1; }
        }
    }
}
