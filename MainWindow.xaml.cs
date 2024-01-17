using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    public partial class MainWindow : Window
    {
        public static ConfigManager configManager = new();
        public static ConcurrentDictionary<string, byte> allFileName = new();
        public static ConcurrentDictionary<string, string> allFileNameAsKey = new();
        public static ConcurrentDictionary<string, byte> allDirectoryName = new();
        public static ConcurrentDictionary<string, ConcurrentBag<string>> allDirectoryParentNames = new();
        public static ConcurrentDictionary<string, string> allDirectoryActualNames = new();

        private Thread travelThread = null;
        //private Thread resourceCheckThread = null;
        
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
            LoadWorkspace(GetWorkspacePath(true));

            LogTextBox.AppendText(string.Join("\r\n", logQueue));
#if (!DEBUG)
            DevelopPanel.Visibility = Visibility.Collapsed;
#endif
        }

        private void SelectProjectFileAndTravel()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "언리얼 프로젝트 파일 (*.uproject) | *.uproject";
            if (dlg.ShowDialog() == true)
            {
                if(SetWorkspace(dlg.FileName))
                {
                    TravelContentDirectories();
                    return;
                }

            }
            else
            {
                LogTextBox.AppendText("언리얼 프로젝트 파일(.uporject)을 찾을 수 없습니다.");
            }

            SelectProjectFileAndTravel();
        }

        private string GetWorkspacePath(bool bDefault)
        {
            return Path.Combine(GlobalValue.currentDirectory, configManager.GetSectionElementValue(ConfigManager.ESectionType.DefaultWorkspace));
        }

        private void LoadWorkspace(string path)
        {
            WorkSpace loadedWorkSpace;
            if (IsValidWorkspace(path, out loadedWorkSpace))
            {
                WorkSpace.Current = loadedWorkSpace;
                Utility.Log("워크스페이스 불러오기 완료.", LogType.Message);

                TravelContentDirectories();
                //MyEditorPannel.DelayCheckUpdate();
            }
            else
            {
                if (MessageBoxResult.OK == MessageBox.Show("프로젝트 경로를 설정해야 합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Exclamation, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly))
                {
                    SelectProjectFileAndTravel();
                }
            }
        }

        private bool IsValidWorkspace(string path, out WorkSpace loadedWorkSpace)
        {
            loadedWorkSpace = default;
            if (Utility.JsonDeserialize<WorkSpace>(path, out loadedWorkSpace) == false)
            {
                Utility.Log("불러올 수 없는 워크스페이스 입니다.", LogType.Warning);
                return false;
            }

            if (loadedWorkSpace.IsValid() == false)
            {
                Utility.Log("유효하지 않은 워크스페이스 입니다.", LogType.Warning);
                return false;
            }

            return true;
        }

        public bool SetWorkspace(string ueProjectFilePath)
        {
            WorkSpace newWorkSpace;
            if (Utility.JsonDeserialize<WorkSpace>(GetWorkspacePath(true), out newWorkSpace) == false)
            {
                return false;
            }

            newWorkSpace.ProjectName = Utility.GetOnlyFileName(ueProjectFilePath);
            newWorkSpace.GamePath = Directory.GetParent(ueProjectFilePath).FullName;
            newWorkSpace.ContentPath = Path.Combine(newWorkSpace.GamePath, "Content");
            newWorkSpace.EnginePath = Path.Combine(Directory.GetParent(newWorkSpace.GamePath).FullName, "Engine");

            Utility.AsyncJsonSerialize(Path.Combine(GlobalValue.currentDirectory, Utility.GetOnlyFileName(newWorkSpace.ProjectName) + ".json"), newWorkSpace);

            WorkSpace.Current = newWorkSpace;

            return true;
        }

        private bool TravelContentDirectories()
        {
            if (travelThread != null && travelThread.IsAlive)
            {
                travelThread.Interrupt();
            }

            MExcel.excelPaths.Clear();
            MExcel.excelFileNames.Clear();
            MExcel.excelFileNameToPath.Clear();

            travelThread = new Thread(delegate ()
            {
                allFileName = new();
                ConcurrentQueue<string> searchQueue = new ConcurrentQueue<string>();
                searchQueue.Enqueue(WorkSpace.Current.ContentPath);

                if(Directory.Exists(searchQueue.Last()) == false)
                {
                    Utility.Log("존재하지 않는 디렉토리를 탐색하려 합니다.", LogType.Warning);
                    return;
                }

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

                Utility.Log("파일 탐색 완료.", LogType.Message);
            });
            travelThread.Start();
            travelThread.Join();

            onTraversalFinished();

            return true;
        }

        private void Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            GameDataTable.SaveCacheData();
        }

        private void Button_MouseLeftButtonDown2(object sender, MouseButtonEventArgs e)
        {
            MyTablePanel.ResetItemViewer(false);
            //foreach (var excelFileName in MExcel.excelFileNames)
            //{
            //    GameDataTable.GetTableByName(excelFileName).Load(((App)Application.Current).ExcelLoader, true);
            //}

            //foreach (var Item in MyTablePanel.TableItemViewer.ItemListWrapPanel.Children)
            //{
            //    MyItem MyItemInstance = Item as MyItem;
            //    if (MyItemInstance != null)
            //    {
            //        MyItemInstance.InitInfoUI();
            //    }
            //}
        }

        private void Button_MouseLeftButtonDown3(object sender, MouseButtonEventArgs e)
        {
           //MyTablePanel.AddBookmarkListTextBox("asd");
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            
#if (!DEBUG)
            //MExcel.SaveMetaData();
#endif
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Log("DNS 이용 아이피", LogType.Message);
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    Log(ip.ToString());
                }
            }

            Log("소켓 이용 아이피", LogType.Message);
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                Log(endPoint.Address.ToString());
            }

            MyTablePanel.TableItemViewer.ResizeItem(50, 70);

            // 휴스톤이 켜져있는지 검사
            //string remoteSystem = "remoteSystemName";
            //string procSearch = "notepad";
            //Process[] proc = System.Diagnostics.Process.GetProcessesByName("houston", "192.168.2.16");
            //foreach(Process p in proc)
            //{
            //    Log(p.ProcessName);
            //}
        }

        public void Log(string log, LogType logType = LogType.Default)
        {
#if (!DEBUG)
            if(logType == LogType.Default)
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
                case LogType.Default:
                    brush = Brushes.Black;
                    break;
                case LogType.Message:
                    brush = Brushes.Green;
                    break;
                case LogType.Warning:
                    brush = Brushes.Red;
                    break;
            }

            if (brush == null)
            {
                brush = Brushes.Black;
            }

            TextRange textRange = new TextRange(LogFlowDocument.ContentEnd, LogFlowDocument.ContentEnd);
            textRange.Text = "[" + DateTime.Now + "]" + "\r" + log + "\r\n";
            textRange.ApplyPropertyValue(TextElement.ForegroundProperty, brush);

            LogTextBox.ScrollToEnd();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (MyEditorPannel == null)
            {
                return;
            }

            if (WorkSpace.Current == null)
            {
                return;
            }

            MyEditorPannel.DelayCheckUpdate();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectProjectFileAndTravel();
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "워크스페이스 파일 (*.json) | *.json";
            if (dlg.ShowDialog() == true)
            {
                LoadWorkspace(dlg.FileName);
            }
        }
    }
}
