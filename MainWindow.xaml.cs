﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
//using System.Windows.Shapes;
using System.Threading;
using Microsoft.Win32;

using System.Net;
using System.Net.Sockets;

namespace TestWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static ConfigManager configManager = new ConfigManager();
        public static ConcurrentDictionary<string, byte> allFileName = new();
        public static ConcurrentDictionary<string, string> allFileNameAsKey = new();
        public static ConcurrentDictionary<string, byte> allDirectoryName = new();
        public static ConcurrentDictionary<string, ConcurrentBag<string>> allDirectoryParentNames = new();
        public static ConcurrentDictionary<string, string> allDirectoryActualNames = new();

        private Thread travelThread = null;
        //private Thread resourceCheckThread = null;
        public Thread loadExcelThread = null;

        public static ConcurrentQueue<string> logQueue = new();

        public delegate void OnTraversalFinishedDelegate();
        public OnTraversalFinishedDelegate onTraversalFinished;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Label_MouseEnter(object sender, MouseEventArgs e)
        {
            dynamic control = sender as dynamic;
            if (control.GetType().GetProperty("Background") != null)
            {
                control.Background = new SolidColorBrush(Colors.Chocolate);
            }
        }

        private void Label_MouseLeave(object sender, MouseEventArgs e)
        {
            dynamic control = sender as dynamic;
            if (control.GetType().GetProperty("Background") != null)
            {
                control.Background = new SolidColorBrush(Colors.Gray);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogTextBox.AppendText(string.Join("\r\n", logQueue));

#if (!DEBUG)
            DevelopPanel.Visibility = Visibility.Collapsed;
#endif

            string gamePath = configManager.GetSectionElementValue(ConfigManager.ESectionType.GamePath);
            if (gamePath != "")
            {
                TravelContentDirectories(gamePath, false);

                MyEditorPannel.DelayCheckUpdate();
            }
            else
            {
                if (MessageBoxResult.OK == MessageBox.Show("처음 실행 시 프로젝트 경로를 설정해야 합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Exclamation, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly))
                {
                    SelectProjectFileAndTravel();
                }
            }
        }

        private void SelectProjectFileAndTravel()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "언리얼 프로젝트 파일 (*.uproject) | *.uproject";
            if (dlg.ShowDialog() == true)
            {
                string gamePath = System.IO.Path.GetDirectoryName(dlg.FileName);
                configManager.AddSectionElement(ConfigManager.ESectionType.ProjectName, Path.GetFileName(dlg.FileName), true);
                TravelContentDirectories(gamePath, true);
            }
            else
            {
                LogTextBox.AppendText("언리얼 프로젝트 파일(.uporject)을 찾을 수 없습니다.");
                SelectProjectFileAndTravel();
            }
        }

        private bool TravelContentDirectories(string gamePath, bool bSetConfig)
        {
            Dictionary<ConfigManager.ESectionType, string> ConfigPathMap = new();
            ConfigPathMap.Add(ConfigManager.ESectionType.GamePath, gamePath);
            ConfigPathMap.Add(ConfigManager.ESectionType.ContentPath, configManager.GetSectionElementValue(ConfigManager.ESectionType.ContentPath));
            ConfigPathMap.Add(ConfigManager.ESectionType.EnginePath, configManager.GetSectionElementValue(ConfigManager.ESectionType.EnginePath));

            if (bSetConfig)
            {
                ConfigPathMap[ConfigManager.ESectionType.ContentPath] = Path.Combine(gamePath, "Content");
                ConfigPathMap[ConfigManager.ESectionType.EnginePath] = Path.Combine(Directory.GetParent(gamePath).FullName, "Engine");
            }

            Dictionary<string, string> notExistPathList = new();
            string detail = "";
            foreach (var PathPair in ConfigPathMap)
            {
                if(Directory.Exists(PathPair.Value) == false)
                {
                    string enumString = Enum.GetName(typeof(ConfigManager.ESectionType), PathPair.Key);
                    string[] splitEnumString = enumString.Split("Path");
                    if(splitEnumString.Length > 0)
                    {
                        string folderName = splitEnumString[0];
                        notExistPathList.Add(folderName, PathPair.Value);
                        detail += folderName + ": " + PathPair.Value + "\r\n";
                    }
                }
            }

            if(notExistPathList.Count > 0)
            {
                MessageBox.Show(string.Join(", ", notExistPathList.Keys) + " 폴더를 찾을 수 없습니다.\r\n" + detail);
                LogTextBox.AppendText(string.Join(", ", notExistPathList.Keys) + " 폴더를 찾을 수 없습니다.");
                LogTextBox.AppendText(detail);
                return false;
            }

            GlobalValue.GamePath = gamePath;
            if (bSetConfig)
            {
                configManager.AddSectionElement(ConfigManager.ESectionType.GamePath, gamePath, true);
                configManager.AddSectionElement(ConfigManager.ESectionType.ContentPath, ConfigPathMap[ConfigManager.ESectionType.ContentPath], true);
                configManager.AddSectionElement(ConfigManager.ESectionType.EnginePath, ConfigPathMap[ConfigManager.ESectionType.EnginePath], true);
            }

            Utility.Log("경로 설정 완료.", Utility.LogType.Message);

            travelThread = new Thread(delegate ()
            {
                MExcel.LoadMetaData();

                allFileName = new();
                ConcurrentQueue<string> searchQueue = new ConcurrentQueue<string>();
                searchQueue.Enqueue(ConfigPathMap[ConfigManager.ESectionType.ContentPath]);
                while (searchQueue.Count != 0)
                {
                    string currentDirectory;
                    if (searchQueue.TryDequeue(out currentDirectory) == false)
                    {
                        break;
                    }

                    string[] subDirectories = System.IO.Directory.GetDirectories(currentDirectory);
                    Parallel.ForEach(subDirectories, subDirectory =>
                    {
                        searchQueue.Enqueue(subDirectory);

                        string subDirectoryName = System.IO.Path.GetFileName(subDirectory);
                        allDirectoryName.TryAdd(subDirectoryName, 0);

                        string subDirectoryNameAsKey = Utility.NameAsKey(subDirectoryName);
                        if (allDirectoryParentNames.ContainsKey(subDirectoryNameAsKey) == false)
                        {
                            allDirectoryParentNames.TryAdd(subDirectoryNameAsKey, new ConcurrentBag<string>());
                        }

                        string[] splitedDirectories = subDirectory.Split('\\');
                        bool bNotLeafDirectory = splitedDirectories.Length > 1;
                        if (bNotLeafDirectory)
                        {
                            string parnetsubDirectoryName = splitedDirectories[splitedDirectories.Length - 2];
                            allDirectoryParentNames[subDirectoryNameAsKey].Add(parnetsubDirectoryName.ToLower());
                            allDirectoryActualNames.TryAdd(subDirectoryNameAsKey, subDirectoryName);
                        }

                        // 엑셀 파일들 찾기
                        bool bTableDirectory = subDirectoryName == "Table";
                        if (bTableDirectory)
                        {
                            string[] pathsOfFileInTableDirectory = System.IO.Directory.GetFiles(subDirectory);
                            foreach (string filePath in pathsOfFileInTableDirectory)
                            {
                                bool bExcelFile = System.IO.Path.GetExtension(filePath) == @".xlsx";
                                if (bExcelFile == false)
                                {
                                    continue;
                                }

                                bool bTempFile = System.IO.Path.GetFileName(filePath)[0] == '~' || filePath.Any(char.IsDigit);
                                if (bTempFile)
                                {
                                    continue;
                                }

                                string fileName = Utility.GetOnlyFileName(filePath);

                                MExcel.excelPaths.Add(filePath);
                                MExcel.excelFileNames.Add(fileName);
                                MExcel.excelFileNameToPath.Add(fileName, filePath);
                            }
                        }
                    });

                    // 모든 파일을 캐시하는 작업
                    string[] pathFileNames = System.IO.Directory.GetFiles(currentDirectory);
                    Parallel.ForEach(pathFileNames, pathFileName =>
                    {
                        string fileNameOnly = Utility.GetOnlyFileName(pathFileName);

                        allFileName.TryAdd(fileNameOnly, 0);
                        allFileNameAsKey.TryAdd(Utility.NameAsKey(fileNameOnly), fileNameOnly);
                    });
                }

                Utility.Log("파일 탐색 완료.", Utility.LogType.Message);
            });
            travelThread.Start();
            travelThread.Join();

            onTraversalFinished();

            // 최신화가 안되있는 엑셀 파일들 갱신
            loadExcelThread = new Thread(delegate ()
            {
                foreach (var excelPath in MExcel.excelPaths)
                {
                    GameDataTable gameDataTable = MExcel.TableMap[excelPath];
                    gameDataTable.UpdateModifiedProperty(out _);
                }

                Dispatcher.BeginInvoke((Action)(() => 
                {
                    MyTablePanel.UpdateInfoUI();
                }));

                Utility.Log("엑셀 읽기 완료", Utility.LogType.Message);
            });
            loadExcelThread.Start();

            return true;
        }

        private void Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MExcel.SaveMetaData();
        }

        private void Button_MouseLeftButtonDown2(object sender, MouseButtonEventArgs e)
        {
            foreach (var excelPath in MExcel.excelPaths)
            {
                MExcel.TableMap[excelPath].LoadLatest(((App)Application.Current).ExcelLoader, excelPath, true);
            }

            //mExcel.DestroyExcelApp();
            //mExcel = null;

            foreach (var Item in MyTablePanel.AllTableViewer.ItemListWrapPanel.Children)
            {
                MyItem MyItemInstance = Item as MyItem;
                if (MyItemInstance != null)
                {
                    MyItemInstance.InitInfoUI();
                }
            }
        }

        private void Button_MouseLeftButtonDown3(object sender, MouseButtonEventArgs e)
        {
            MyTablePanel.AddBookmarkListTextBox("asd");
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            
#if (!DEBUG)
            MExcel.SaveMetaData();
#endif
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Log("DNS 이용 아이피", Utility.LogType.Message);
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    Log(ip.ToString());
                }
            }

            Log("소켓 이용 아이피", Utility.LogType.Message);
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                Log(endPoint.Address.ToString());
            }

            Log("스크린 사이즈", Utility.LogType.Message);
            foreach(System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                Log(screen.WorkingArea.Width + ", " + screen.WorkingArea.Height);
            }

            MyTablePanel.AllTableViewer.ResizeItem(50, 70);

            // 휴스톤이 켜져있는지 검사
            //string remoteSystem = "remoteSystemName";
            //string procSearch = "notepad";
            //Process[] proc = System.Diagnostics.Process.GetProcessesByName("houston", "192.168.2.16");
            //foreach(Process p in proc)
            //{
            //    Log(p.ProcessName);
            //}
        }

        public void Log(string log, Utility.LogType logType = Utility.LogType.Default)
        {
#if (!DEBUG)
            if(logType == Utility.LogType.Default)
            {
                return;
            }
#endif
            if (LogTextBox == null)
            {
                logQueue.Enqueue(log + "\r\n");
            }

            Brush brush = null;
            switch (logType)
            {
                case Utility.LogType.Default:
                    brush = Brushes.Black;
                    break;
                case Utility.LogType.Message:
                    brush = Brushes.Green;
                    break;
                case Utility.LogType.Warning:
                    brush = Brushes.Red;
                    break;
            }

            if (brush == null)
            {
                brush = Brushes.Black;
            }

            TextRange textRange = new TextRange(LogFlowDocument.ContentEnd, LogFlowDocument.ContentEnd);
            textRange.Text = "[" + DateTime.Now + "]" + log + "\r\n";
            textRange.ApplyPropertyValue(TextElement.ForegroundProperty, brush);

            LogTextBox.ScrollToEnd();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (MyEditorPannel == null)
            {
                return;
            }

            if (GlobalValue.GamePath == null)
            {
                return;
            }

            MyEditorPannel.DelayCheckUpdate();
        }
    }
}