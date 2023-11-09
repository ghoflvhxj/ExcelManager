using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Excel = Microsoft.Office.Interop.Excel;

namespace TestWPF
{
    /// <summary>
    /// EditorPanel.xaml에 대한 상호 작용 논리
    /// </summary>

    public partial class EditorPanel : System.Windows.Controls.UserControl
    {
        public List<EditorProcessInfo> EditorProcessList = new();
        public Dictionary<int, EditorProcessInfo> ExecutedProcesses = new();

        public List<EditorGameMode> GameModeList = new();

        public List<MatchingServerInfo> MatchingServerInfoList = new();

        public List<EditorMacro> EditorMacroList = new();

        public List<int> ClientCountList { get; set; }

        private bool bIsProgrammerUser = false;
        private int EditorPositionCounter;

        public bool bIsBinaryUpdated = false;
        public bool bIsSVNUpdated = false;
        public object updateLock = new();


#if DEBUG
        private string SettingTablePath = "C:\\Users\\mkh2022\\Desktop\\설정.xlsx";
#else
        private string SettingTablePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Data\\Table\\설정.xlsx");
#endif

        string[] EditorFileNames = {
                "UE4Editor",
                "UnrealEditor"
        };

        public enum ExecuteType {
            Editor,
            Game,
            Server,
            End
        }

        public EditorPanel()
        {
            InitializeComponent();

            ProcessInfoTextBlock.ItemsSource = EditorProcessList;

            ClientCountList = new();
            ClientCountList.Add(1);
            ClientCountList.Add(2);
            ClientCountList.Add(3);
            ClientCountComboBox.ItemsSource = ClientCountList;

            MExcel EditorPannelExcelLoader = new();
            Excel.Workbook workBook = null;
            Excel.Worksheet workSheet = null;

            // 매크로
            if (EditorPannelExcelLoader.GetWorkBookAndSheetFromTable(SettingTablePath, out workBook, out workSheet, "매크로", true))
            {
                Excel.Range range = workSheet.UsedRange;
                object[,] DataArray = (object[,])range.Value2;
                int lastRecordIndex = Convert.ToInt32(range.get_End(Excel.XlDirection.xlDown).Row);

                for(int i=2; i<=lastRecordIndex; ++i)
                {
                    EditorMacroList.Add(new EditorMacro() { Name = Convert.ToString(DataArray[i, 1]), Key = Convert.ToString(DataArray[i, 2]), Input = Convert.ToString(DataArray[i, 3]) });
                }
            }
            EditorMacroListBox.ItemsSource = EditorMacroList;

            // 게임모드
            if (EditorPannelExcelLoader.GetWorkBookAndSheetFromTable(SettingTablePath, out workBook, out workSheet, "게임모드", true))
            {
                Excel.Range range = workSheet.UsedRange;
                object[,] DataArray = (object[,])range.Value2;
                int lastRecordIndex = Convert.ToInt32(range.get_End(Excel.XlDirection.xlDown).Row);

                for (int i = 2; i <= lastRecordIndex; ++i)
                {
                    GameModeList.Add(new EditorGameMode() { Name = Convert.ToString(DataArray[i, 1]), gameModeIndex = Convert.ToString(DataArray[i, 2]), option = Convert.ToString(DataArray[i, 3]) });
                }
            }
            GameModeComboBox.ItemsSource = GameModeList;

            // 매칭서버
            if (EditorPannelExcelLoader.GetWorkBookAndSheetFromTable(SettingTablePath, out workBook, out workSheet, "매칭서버", true))
            {
                Excel.Range range = workSheet.UsedRange;
                object[,] DataArray = (object[,])range.Value2;
                int lastRecordIndex = Convert.ToInt32(range.get_End(Excel.XlDirection.xlDown).Row);

                for (int i = 2; i <= lastRecordIndex; ++i)
                {
                    MatchingServerInfo newMatchingServerInfo = new MatchingServerInfo();
                    newMatchingServerInfo.ipAddress = Convert.ToString(DataArray[i, 2]);
                    newMatchingServerInfo.InfoString = newMatchingServerInfo.ipAddress == "" ? Convert.ToString(DataArray[i, 1]) : Convert.ToString(DataArray[i, 1]) + " " + Convert.ToString(DataArray[i, 2]);
                    MatchingServerInfoList.Add(newMatchingServerInfo);
                }
            }

            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                MatchingServerInfoList[1].ipAddress = endPoint.Address.ToString();
                MatchingServerInfoList[1].InfoString += " " + MatchingServerInfoList[1].ipAddress;
            }

            MatchingServerComboBox.ItemsSource = MatchingServerInfoList;

            Marshal.ReleaseComObject(workBook);
            Marshal.ReleaseComObject(workSheet);
            EditorPannelExcelLoader.DestroyExcelApp();
        }

        public void CheckUpdate(object sender, System.EventArgs e)
        {
            // 업데이트 중에는 버전 체크를 하지 않음
            if (bIsSvnUpdating || bIsBinaryUpdating)
            {
                return;
            }

            Thread t = new Thread(delegate ()
            {
                string updateMessage = "";

                DateTime editorLastWriteTime = new();
                bool bNewBinaryUpdated = IsBinaryUpdated(out bIsProgrammerUser, ref editorLastWriteTime);
                lock (updateLock)
                {
                    bIsBinaryUpdated = bNewBinaryUpdated;
                    if (bIsBinaryUpdated == false && bIsProgrammerUser == false)
                    {
                        updateMessage += "에디터 실행파일 업데이트가 필요합니다. 최신: " + editorLastWriteTime.ToString() + "\r\n";
                    }
                }

                long Start = 0, End = 0, LatestRevision = 0;
                bool bNewSVNUpdated = false;
                SharpSvn.SvnClient svnClient = sender as SharpSvn.SvnClient;
                SVNResult svnResult = svnClient == null ? IsSVNUpdated(ref Start, ref End, ref LatestRevision, ref bNewSVNUpdated) : IsSVNUpdated(svnClient, ref Start, ref End, ref LatestRevision, ref bNewSVNUpdated);

                if(svnResult == SVNResult.Failed)
                {
                    Utility.Log("SVN 에러가 발생했습니다. 수동으로 클린 업을 해보세요.", Utility.LogType.Warning);
                    updateMessage = "SVN 에러가 발생. 로그를 확인해주세요.";
                }

                lock (updateLock)
                {
                    bIsSVNUpdated = bNewSVNUpdated;
                    if (svnResult == SVNResult.Success && bIsSVNUpdated == false)
                    {
                        updateMessage += "SVN 업데이트가 필요합니다. Revision 최신: " + LatestRevision + ", 현재: " + Start;
                    }
                }

                if (bIsSVNUpdated && bIsBinaryUpdated)
                {
                    updateMessage = "현재 최신 버전입니다.";
                }

                if(System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        UpdateAndExecuteEditorButtonMessage.Content = updateMessage;
                        if (bIsBinaryUpdated && bIsSVNUpdated)
                        {
                            UpdateAndExecuteEditorButton.IsEnabled = false;
                            UpdateAndExecuteEditorButtonMessage.Foreground = new SolidColorBrush(Colors.LightGreen);
                            UpdateAndExecuteEditorButton.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            UpdateAndExecuteEditorButton.IsEnabled = true;
                            UpdateAndExecuteEditorButtonMessage.Foreground = new SolidColorBrush(Colors.Red);
                            UpdateAndExecuteEditorButton.Visibility = Visibility.Visible;
                        }


                    }));
                }
            });

            t.Start();
        }


        enum SVNResult
        { 
            Success,
            Failed
        }


        private SVNResult IsSVNUpdated(ref long Start, ref long End, ref long LatestRevision, ref bool Result)
        {
            using (var svnClient = new SharpSvn.SvnClient())
            {
                return IsSVNUpdated(svnClient, ref Start, ref End, ref LatestRevision, ref Result);
            }
        }

        private SVNResult IsSVNUpdated(SharpSvn.SvnClient svnClient, ref long Start, ref long End, ref long LatestRevision, ref bool bIsUpdated)
        {
            bIsUpdated = false;

            var workingCopyClient = new SharpSvn.SvnWorkingCopyClient();
            SharpSvn.SvnWorkingCopyVersion version;
            SharpSvn.SvnInfoEventArgs info;
            Uri repos = new Uri("http://repositories.actionsquare.corp/svn/GR/trunk/Game/");

            SVNResult svnResult = SVNResult.Failed;
            bool bCleanUpSuccess = false;

            try
            {
                workingCopyClient.GetVersion(GlobalValue.GamePath, out version);
                svnClient.GetInfo(repos, out info);

                Start = version.Start;
                End = version.End;
                LatestRevision = info.Revision;

                bIsUpdated = version.Start == info.Revision;

                svnResult = SVNResult.Success;

                return svnResult;
            }
            catch (Exception e)
            {
                Utility.Log(e.Message, Utility.LogType.Warning);
                Utility.Log("클린 업을 시도 합니다.", Utility.LogType.Warning);

                bIsMessageBoxShow = true;
                if (ExitAllEditorProcess("SVN 버전 체크 중 에러가 발생해 클린 업이 필요합니다. 언리얼 프로세스는 종료되지만 계속할까요?") == true)
                {
                    svnClient.CleanUp(GlobalValue.GamePath);
                    bCleanUpSuccess = true;
                }
                updateCheckTime = DateTime.Now;
                bIsMessageBoxShow = false;

                if (svnResult == SVNResult.Failed && bCleanUpSuccess)
                {
                    // 재시도
                    try
                    {
                        workingCopyClient.GetVersion(GlobalValue.GamePath, out version);
                        svnClient.GetInfo(repos, out info);

                        Start = version.Start;
                        End = version.End;
                        LatestRevision = info.Revision;

                        bIsUpdated = version.Start == info.Revision;

                        svnResult = SVNResult.Success;
                    }
                    catch (Exception e2)
                    {
                        Utility.Log(e2.Message, Utility.LogType.Warning);
                        Utility.Log("SVN 업데이트 체크 실패", Utility.LogType.Warning);
                        svnResult = SVNResult.Failed;
                    }
                }

                return svnResult;
            }
        }

        private bool IsBinaryUpdated(out bool bIsProgrammerUser, ref DateTime latestWriteDateTime)
        {
            string gitPath = System.IO.Path.Combine(Directory.GetParent(GlobalValue.GamePath).FullName, ".git");
            bIsProgrammerUser = Directory.Exists(gitPath);

            string gamePath = System.IO.Path.Combine(GlobalValue.GamePath, "Binaries", "Win64");
            string[] myBinaryFiles = Directory.GetFiles(gamePath);
            Dictionary<string, DateTime> fileToDateTimeMap = new();
            foreach (string mybinaryFile in myBinaryFiles)
            {
                FileInfo fileInfo = new FileInfo(mybinaryFile);
                fileToDateTimeMap.TryAdd(Utility.GetOnlyFileName(mybinaryFile), fileInfo.LastWriteTime);

                if (bIsProgrammerUser)
                {
                    return true;
                }
            }

            bool bResult = true;
            string serverBinaryPath = @"\\build-gr-master\UE4\DevelopmentEditor\Game\Binaries\Win64";
            string[] serverBinaryFiles = Directory.GetFiles(serverBinaryPath);
            foreach (string serverBinaryFile in serverBinaryFiles)
            {
                FileInfo serverFileInfo = new FileInfo(serverBinaryFile);
                latestWriteDateTime = latestWriteDateTime < serverFileInfo.LastWriteTime ? serverFileInfo.LastWriteTime : latestWriteDateTime;

                string serverFileName = Utility.GetOnlyFileName(serverBinaryFile);
                if(fileToDateTimeMap.ContainsKey(serverFileName) == false)
                {
                    bResult = false;
                    continue;
                }

                DateTime myLastWriteTime = fileToDateTimeMap[serverFileName];
                if(serverFileInfo.LastWriteTime > myLastWriteTime)
                {
                    Utility.Log(serverFileInfo.LastWriteTime.ToString() + " > " + myLastWriteTime.ToString());
                    bResult = false;
                }
            }

            return bResult;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ExecuteEditor(ExecuteType.Editor);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            EditorPositionCounter = 0;

            // 서버
            ExecuteEditor(ExecuteType.Server);

            // 클라 
            System.Windows.Forms.Timer t = new();
            t.Interval = 5000;
            t.Tick += delegate (System.Object o, System.EventArgs e)
            {
                int clientCount = GetClientCountToExectue();
                for (int i = 0; i < clientCount; ++i)
                {
                    ExecuteEditor(ExecuteType.Game);
                }
                t.Stop();
            };
            t.Start();
        }

        private int GetClientCountToExectue()
        {
            return ClientCountList[ClientCountComboBox.SelectedIndex];
        }

        private bool ExecuteEditor(ExecuteType executeType)
        {
            List<string> options = new();
            List<string> urlOptions = new();

            Screen firstScreen = System.Windows.Forms.Screen.AllScreens[0];
            if (firstScreen == null)
            {
                Utility.Log("스크린 정보가 없음", Utility.LogType.Warning);
                return false;
            }
            
            switch (executeType)
            {
                case ExecuteType.Game:
                    if(MatchingServerComboBox.SelectedIndex != 0)
                    {
                        MatchingServerInfo mathcingServerInfo = MatchingServerInfoList[MatchingServerComboBox.SelectedIndex];
                        options.Add("-WarehouseURL=\"http://" + mathcingServerInfo.ipAddress + "/warehouse\" -AlexandriaURL=\"http://" + mathcingServerInfo.ipAddress + "/alexandria\"");
                    }
                    else
                    {
                        options.Add("-ForceLogin");
                    }
                    if(ConnectToLocalServerCheckBox.IsEnabled && ConnectToLocalServerCheckBox.IsChecked == true)
                    {
                        options.Clear();
                        options.Add(Utility.GetIPAddress());
                    }
                    AppendPositionOption(ref options);
                    options.Add("-game");
                    options.Add("-log");
                    return ExecuteProcess(options, urlOptions, executeType);
                case ExecuteType.Server:
                    if (MatchingServerComboBox.SelectedIndex != 0)
                    {
                        MatchingServerInfo mathcingServerInfo = MatchingServerInfoList[MatchingServerComboBox.SelectedIndex];
                        options.Add("-PromoterURL=\"http://" + mathcingServerInfo.ipAddress + "/promoter\" -PublicIP=\"" + MatchingServerInfoList[1].ipAddress + "\"");
                    }
                    else
                    {
                        EditorGameMode gameMode = GameModeList.ElementAtOrDefault(GameModeComboBox.SelectedIndex);
                        urlOptions.Add("NoMatching");
                        urlOptions.Add("Difficulty=" + gameMode.gameModeIndex);
                        if(gameMode.option.Length > 0)
                        {
                            urlOptions.Add(gameMode.option);
                        }
                    }
                    //AppendPositionOption(ref options);
                    options.Add("-server");
                    options.Add("-log");
                    return ExecuteProcess(options, urlOptions, executeType);
                default:
                    return ExecuteProcess(options, urlOptions, executeType);
            }
        }

        public void AppendPositionOption(ref List<string> options)
        {
            Point consolePosition, windowPosition;
            Point workingScreenSize;
            Screen firstScreen = System.Windows.Forms.Screen.AllScreens[0];
            if (firstScreen == null)
            {
                Utility.Log("스크린 정보가 없음", Utility.LogType.Warning);
                return;
            }

            workingScreenSize.X = firstScreen.WorkingArea.Width;
            workingScreenSize.Y = firstScreen.WorkingArea.Height;

            //windowPosition.X = (EditorPositionCounter % 2) * (workingScreenSize.X / 2.0);
            //windowPosition.Y = (EditorPositionCounter / 2) * (workingScreenSize.Y / 2.0);

            int clientCount = GetClientCountToExectue();

            windowPosition.X = 0.0;
            windowPosition.Y = EditorPositionCounter * (workingScreenSize.Y / (double)clientCount);

            // 윈도우 위치
            options.Add("-WinX=" + (int)windowPosition.X);
            options.Add("-WinY=" + (int)windowPosition.Y);

            // 콘솔 위치
            options.Add("-ConsoleX=" + (int)windowPosition.X  + (int)(workingScreenSize.X / 2.0));
            options.Add("-ConsoleY=" + (int)windowPosition.Y);

            // 해상도
            options.Add("-ResX=" + (int)(workingScreenSize.X / (double)clientCount));
            options.Add("-ResY=" + (int)(workingScreenSize.Y / (double)clientCount));

            options.Add("-Windowed");

            ++EditorPositionCounter;
        }

        private bool ExecuteProcess(List<string> additionalOptions, List<string>urlOptions, ExecuteType executeType)
        {
            string gamePath = MainWindow.configManager.GetSectionElementValue(ConfigManager.ESectionType.GamePath);
            string projectName = MainWindow.configManager.GetSectionElementValue(ConfigManager.ESectionType.ProjectName);
            string url = System.IO.Path.Combine(gamePath, projectName);
            if (File.Exists(url) == false)
            {
                return false;
            }

            List<string> arguments = new();
            arguments.Add(url);
            if(urlOptions.Count > 0)
            {
                arguments.Add("?" + string.Join('?', urlOptions));
            }
            arguments.Add(string.Join(' ', additionalOptions));

            string EnginePath = MainWindow.configManager.GetSectionElementValue(ConfigManager.ESectionType.EnginePath);
            string EditorDirectory = System.IO.Path.Combine(EnginePath, "Binaries", "Win64");
            foreach (string EditorFileName in EditorFileNames)
            {
                string EditorPath = System.IO.Path.Combine(EditorDirectory, EditorFileName);
                EditorPath = System.IO.Path.ChangeExtension(EditorPath, ".exe");
                if (File.Exists(EditorPath))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = EditorPath;
                    startInfo.Arguments = string.Join(' ', arguments);

                    Utility.Log(startInfo.FileName + " " + startInfo.Arguments);

                    Process process = Process.Start(startInfo);
                    process.EnableRaisingEvents = true;
                    process.Exited += new EventHandler(Editor_Exited);

                    // UI 바인딩
                    EditorProcessInfo newEditorProcess = new();
                    newEditorProcess.StrPID = Convert.ToString(process.Id);
                    newEditorProcess.executeType = executeType;
                    switch (executeType)
                    { 
                        case ExecuteType.Game:
                            newEditorProcess.StrType = "클라이언트";
                            break;
                        case ExecuteType.Server:
                            newEditorProcess.StrType = "서버";
                            break;
                        case ExecuteType.Editor:
                            newEditorProcess.StrType = "에디터";
                            break;
                    }
                    EditorProcessList.Add(newEditorProcess);
                    ExecutedProcesses.Add(process.Id, newEditorProcess);

                    ProcessInfoTextBlock.Items.Refresh();

                    return true;
                }
            }

            return false;
        }

        private void Editor_Exited(object sender, System.EventArgs e)
        {
            Process exitedProcess = sender as Process;  
            if(exitedProcess == null)
            {
                return;
            }

            if (ExecutedProcesses.ContainsKey(exitedProcess.Id))
            {
                if (EditorProcessList.Contains(ExecutedProcesses[exitedProcess.Id]))
                {
                    EditorProcessList.Remove(ExecutedProcesses[exitedProcess.Id]);
                }
                ExecutedProcesses.Remove(exitedProcess.Id);
            }

            OnExecutedProcessCountChanged();
        }

        private void OnExecutedProcessCountChanged()
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                ProcessInfoTextBlock.Items.Refresh();
            }));
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            EditorProcessInfo executedEditorProcess = EditorProcessList.ElementAtOrDefault(ProcessInfoTextBlock.SelectedIndex);
            System.Windows.Clipboard.SetText(executedEditorProcess.StrPID);
        }

        public class EditorProcessInfo
        {
            //public Process process;
            public string StrType { get; set; }
            public string StrPID { get; set; }
            public ExecuteType executeType;
        }

        public class MatchingServerInfo
        {
            public string InfoString { get; set; }
            public string ipAddress;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //if(pipe != null)
            //{
            //    pipe.Close();
            //}
            //pipe = new("testpipe", PipeDirection.Out, 3, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
            
            //pipe.WaitForConnection();
            //byte[] buff = Encoding.UTF8.GetBytes("Test message");
            //pipe.WriteAsync(buff, 0, buff.Length);

            //pipe.Close();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            EditorProcessList.Clear();
            ProcessInfoTextBlock.Items.Refresh();
        }

        private void ProcessInfoTextBlock_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditorProcessInfo executedEditorProcess = EditorProcessList.ElementAtOrDefault(ProcessInfoTextBlock.SelectedIndex);
            Process processRunning = Process.GetProcessById(Convert.ToInt32(executedEditorProcess.StrPID));
            Utility.SetForegroundWindow(processRunning.MainWindowHandle);
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            EditorMacro macro = EditorMacroList.ElementAtOrDefault(EditorMacroListBox.SelectedIndex);

            foreach (EditorProcessInfo process in EditorProcessList)
            {
                if(process.StrType == "서버")
                {
                    continue;
                }

                Process processRunning = Process.GetProcessById(Convert.ToInt32(process.StrPID));
                //Utility.SetForegroundWindow(processRunning.MainWindowHandle);
                SendKeys.SendWait(macro.Key);
            }
        }

        public class EditorMacro
        {
            public string Name { get; set; }
            public string Key { get; set; }
            public string Input { get; set; }
        }

        public class EditorGameMode
        {
            public string Name { get; set; }
            public string gameModeIndex;
            public string option;
        }

        private List<Thread> t = new();
        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            if (t.Count == 0)
            {
                for (int i = 0; i < 10; ++i)
                {
                    Thread ts = new Thread(delegate ()
                    {
                        int i = 0;
                        while (true)
                        {
                            ++i;
                            if (i > 10)
                            {
                                i = 0;
                            }
                        }
                    });
                    ts.Start();
                    t.Add(ts);
                }
            }
            else
            {
                foreach (var th in t)
                {
                    try
                    {
                        th.Interrupt();
                    }
                    catch (ThreadInterruptedException exception)
                    {
                        Environment.Exit(0);
                    }
                }
                t.Clear();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            // 게임으로 열린 에디터 프로세스 종료
            foreach (var oldEdtiorProcessInfo in EditorProcessList)
            {
                if (oldEdtiorProcessInfo.executeType == ExecuteType.Editor)
                {
                    continue;
                }

                Process editorProcess = Process.GetProcessById(Convert.ToInt32(oldEdtiorProcessInfo.StrPID));
                if (editorProcess == null)
                {
                    // 이미 없는 프로세스를 종료하려고 하고 있다.
                }

                editorProcess.Kill();
            }
        }

        public bool bNeedSvnUpdated = false;
        public bool bNeedBinaryUpdated = false;
        public bool bIsSvnUpdating = false;
        public bool bIsBinaryUpdating = false;
        private void UpdateAndExecuteEditorButton_Click(object sender, RoutedEventArgs e)
        {
            if (ExitAllEditorProcess("업데이트를 진행하기 위해 모든 에디터가 종료됩니다. 계속 진행할까요?") == false)
            {
                return;
            }

            // SVN 최신화
            if (bIsSVNUpdated == false)
            {
                Utility.Log("SVN 업데이트 시작");
                Utility.Log("경로: " + GlobalValue.GamePath);

                bIsSvnUpdating = true;

                SharpSvn.SvnClient svnClient = new SharpSvn.SvnClient();
                Thread t = new Thread(delegate () {
                    SharpSvn.SvnUpdateResult svnUpdateResult;
                    svnClient.Update(GlobalValue.GamePath, out svnUpdateResult);
                    bIsSvnUpdating = false;
                    CheckUpdate(svnClient, null);
                    Utility.Log("SVN 업데이트 완료", Utility.LogType.Message);
                });

                t.Start();
            }

            // 바이너리 최신화
            if (bIsProgrammerUser == false && bIsBinaryUpdated == false)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/C " + System.IO.Path.Combine(Directory.GetParent(GlobalValue.GamePath).FullName, "UpdateEditorBinaries.bat");
                startInfo.WorkingDirectory = Directory.GetParent(GlobalValue.GamePath).FullName;

                Process process = Process.Start(startInfo);
                process.EnableRaisingEvents = true;
                process.Exited += new EventHandler(OnBinaryUpdated);
                process.Exited += new EventHandler(CheckUpdate);
                bIsBinaryUpdating = true;
            }

            UpdateAndExecuteEditorButton.IsEnabled = false;
            UpdateAndExecuteEditorButton.Visibility = Visibility.Collapsed;
            UpdateAndExecuteEditorButtonMessage.Content = "업데이트 중 입니다. 진행 상황은 로그에 출력됩니다.";
        }

        public void OnBinaryUpdated(object sender, System.EventArgs e)
        {
            Process pro = sender as Process;
            if (pro != null)
            {
                if (pro.ExitCode == 0)
                {
                    Utility.Log("에디터 업데이트 완료", Utility.LogType.Message);
                }
                else
                {
                    Utility.Log("에디터 업데이트 실패", Utility.LogType.Warning);
                }
            }

            bIsBinaryUpdating = false;
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            ExecuteEditor(ExecuteType.Editor);
        }

        public void Test()
        {

        }

        private void MatchingServerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool bIsNotUseMatchingServer = MatchingServerComboBox.SelectedIndex == 0;
            GameModeComboBox.IsEnabled = bIsNotUseMatchingServer;
            GameModeComboBox.Foreground = bIsNotUseMatchingServer ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.Gray);
            if(bIsNotUseMatchingServer == false)
            {
                ConnectToLocalServerCheckBox.IsChecked = false;
            }
            ConnectToLocalServerCheckBox.IsEnabled = bIsNotUseMatchingServer;
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            string saveGamesPath = System.IO.Path.Combine(GlobalValue.GamePath, "Saved", "SaveGames");

            if (Directory.Exists(saveGamesPath) == false)
            {
                Utility.Log(saveGamesPath + " 가 존재하지 않습니다.", Utility.LogType.Warning);
                return;
            }

            Utility.Log("세이브 파일이 삭제하기 전에 PIE를 종료해야합니다.", Utility.LogType.Message);
            Directory.Delete(saveGamesPath, true);
        }

        private bool ExitAllEditorProcess(string message)
        {
            // 프로세스가 있는지 검사
            Process[] processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                //Utility.Log(process.ProcessName);

                if (EditorFileNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
                {
                    EditorProcessList.Add(new EditorProcessInfo() { executeType = ExecuteType.End, StrPID = Convert.ToString(process.Id), StrType = "" });
                }
            }

            if (EditorProcessList.Count == 0)
            {
                return true;
            }


            // 취소하면 실패
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(message, "경고", MessageBoxButton.OKCancel);
            if (messageBoxResult == MessageBoxResult.OK)
            {
                foreach (EditorProcessInfo editorProcessInfo in EditorProcessList)
                {
                    Process editorProcess = Process.GetProcessById(Convert.ToInt32(editorProcessInfo.StrPID));
                    if (editorProcess == null)
                    {
                        continue;
                    }

                    editorProcess.Kill();
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        DateTime updateCheckTime;
        float updateCheckDelay = 3.0f;
        bool bIsMessageBoxShow = false;
        public void DelayCheckUpdate()
        {
            if (bIsSvnUpdating || bIsMessageBoxShow)
            {
                return;
            }

            if (EditorTabControl.SelectedIndex == 0)
            {
                if ((DateTime.Now - updateCheckTime).TotalSeconds < updateCheckDelay)
                {
                    Utility.Log("업데이트 체크 기다리기");
                }
                else
                {
                    Utility.Log("업데이트 체크");
                    updateCheckTime = DateTime.Now;
                    CheckUpdate(null, null);
                }
            }
        }

        private void Grid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DelayCheckUpdate();
        }

    }
}