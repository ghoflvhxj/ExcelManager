using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace TestWPF
{
    public class Utility
    {
        public static Value FindOrAdd<Key, Value>(Dictionary<Key, Value> Map, Key key) where Value : class, new()
        {
            if(Map.ContainsKey(key) == false)
            {
                Map.Add(key, new Value());
            }

            return Map[key];
        }

        public static string NameAsKey(string name)
        {
            return name.ToLower();
        }

        public static void SetTextThreadSafe(TextBox textBox, string str)
        {
            //textBox.BeginInvoke(new Action(delegate ()
            //{
            //    textBox.Text = str;
            //}));
        }
        
        public static string GetTextThreadSafe(TextBox textBox)
        {
            string text = "";
            //textBox.BeginInvoke(new Action(delegate ()
            //{
            //    text = textBox.Text;
            //}));

            return text;
        }

        public static void AppendTextThreadSafe(TextBox textBox, string str)
        {
            //textBox.BeginInvoke(new Action(delegate ()
            //{
            //    textBox.AppendText(str);
            //}));
        }

        public static string GetOnlyFileName(string str)
        {
            string fileName = Path.GetFileName(str);
            if (Path.HasExtension(fileName))
            {
                fileName = Path.ChangeExtension(fileName, null);
            }

            return fileName;
        }

        public static string ConvetToExcelColumn(int column)
        {
            string columnName = "";

            while (column > 0)
            {
                int modulo = (column - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                column = (column - modulo) / 26;
            }

            return columnName;
        }

        public static void Log(string log, LogType logType = LogType.Default)
        {
#if (!DEBUG)
            if(logType == LogType.Default)
            {
                return;
            }
#endif

            if(Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    MainWindow mainWindow = App.Current.MainWindow as MainWindow;
                    if(mainWindow != null)
                    {
                        mainWindow.Log(log, logType);
                    }
                }));
            }
        }

        public static string GetIPAddress()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        }

        public static Process ExecuteProcess(string path)
        {
            if (System.IO.File.Exists(path) == false)
            {
                Utility.Log(path + " 존재하지 않는 파일입니다.", LogType.Warning);
                return null;
            }

            ProcessStartInfo processStartInfo = new();
            processStartInfo.FileName = "excel";
            processStartInfo.Arguments = path;
            processStartInfo.UseShellExecute = true;

            Process process = Process.Start(processStartInfo);
            return process;
        }

        public enum LogType
        { 
            Default,
            Message,
            Warning,
            Count
        }


        public const uint WM_KEYDOWN = 0x100;
        public const uint WM_CHAR = 0x0102;
        public const uint WM_SYSCOMMAND = 0x018;
        public const uint SC_CLOSE = 0x053;

        [DllImport("user32.dll")]
        internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow); //ShowWindow needs an IntPtr/// 

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
