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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using Excel = Microsoft.Office.Interop.Excel;

namespace TestWPF
{
    public partial class TablePanel : UserControl
    {
        static System.Text.RegularExpressions.Regex reg = new System.Text.RegularExpressions.Regex("[^0-9]");

        private Button currentBookmarkButton;
        private Brush currentBookmarkButtonBackGround;

        private List<MenuItem> contextMenuItems = new();

        class BookmarkData
        {
            public string TargetProjectName { get; set; }
            public Dictionary<string, List<string>> BookmarkMap { get; set; }
        }

        public TablePanel()
        {
            InitializeComponent();

            // 델리게이트 바인딩
            MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
            if(mainWindow != null)
            {
                mainWindow.onTraversalFinished += delegate() {
                    ResetPanel(true);
                };

                mainWindow.StateChanged += new EventHandler((object sender, EventArgs e) =>
                {
                    TableItemViewer.UpdateScrollViewerHeight();
                });

                mainWindow.SizeChanged += new SizeChangedEventHandler((object sender, SizeChangedEventArgs e) =>
                {
                    TableItemViewer.UpdateScrollViewerHeight();
                });
            }

            WorkSpace.onCurrentWorkspaceChanged += Initialize;

        }

        public void Initialize()
        {
            // 북마크 버튼 초기화
            BookmarkUpdate();

            // 컨텍스트 메뉴 북마크 초기화
            int bookmarkNum = Context_BookmarkMenuItem.Items.Count;
            for (int i = bookmarkNum - 1; i >= 1; --i)
            {
                Context_BookmarkMenuItem.Items.RemoveAt(i);
            }

            foreach (var bookmarkPair in WorkSpace.Current.BookmarkMap)
            {
                MenuItem newMenuItem = new();
                newMenuItem.Header = bookmarkPair.Key;
                newMenuItem.Click += delegate (object sender, RoutedEventArgs e)
                {
                    AddBookmark(bookmarkPair.Key);
                };

                Context_BookmarkMenuItem.Items.Add(newMenuItem);
            }

            // 컨텍스트 메뉴 커스텀 기능 초기화
            foreach (var oldMenuItem in contextMenuItems)
            {
                TablePanelContextMenu.Items.Remove(oldMenuItem);
            }
            contextMenuItems.Clear();

            if (WorkSpace.Current.FunctionMap != null)
            {
                foreach (var pair in WorkSpace.Current.FunctionMap) // DisplayName, FunctionName
                {
                    MenuItem newMenuItem = new();
                    newMenuItem.Header = pair.Key;
                    newMenuItem.Click += delegate (object sender, RoutedEventArgs e)
                    {
                        if (TableItemViewer.SelectedItemList == null || TableItemViewer.SelectedItemList.Count == 0)
                        {
                            return;
                        }

                        List<string> outList = new();
                        foreach (MyItem myItem in TableItemViewer.SelectedItemList)
                        {
                            outList.Add(myItem.Path);
                        }

                        Type tableType = GameDataTable.GetTableByPath(outList[0]).GetType();
                        if (tableType == null)
                        {
                            Utility.Log("컨텍스트 메뉴 기능실행 실패1.", LogType.Warning);
                            return;
                        }

                        MethodInfo methodInfo = null;
                        while (tableType != null)
                        {
                            methodInfo = tableType.GetMethod(pair.Value);
                            if (methodInfo != null)
                            {
                                methodInfo.Invoke(null, new object[] { outList });
                                return;
                            }

                            tableType = tableType.BaseType;
                        }

                        Utility.Log("컨텍스트 메뉴 기능실행 실패2.", LogType.Warning);
                    };

                    contextMenuItems.Add(newMenuItem);
                    TablePanelContextMenu.Items.Add(newMenuItem);
                }
            }
        }

        public void UpdateInfoUI()
        {
            foreach (var Item in TableItemViewer.ItemListWrapPanel.Children)
            {
                MyItem MyItemInstance = Item as MyItem;
                if (MyItemInstance != null)
                {
                    MyItemInstance.InitInfoUI();
                }
            }
        }

        public void ResetPanel(bool bUseCacheData)
        {
            if(GameDataTable.ResetGameDataTableMap() == false)
            {
                return;
            }

            if(bUseCacheData)
            {
                GameDataTable.LoadCacheData();
            }

            BookmarkUpdate();
            InitializeTableItems(MExcel.excelPaths.ToList());

            GameDataTable.LoadGameDataTables();
        }

        public void AddBookmark(string bookrmarkName)
        {
            WorkSpace newWorkSapce = (WorkSpace)WorkSpace.Current.Clone();
            newWorkSapce.BookmarkMap.TryAdd(bookrmarkName, new());

            foreach (MyItem selectedItem in TableItemViewer.SelectedItemList)
            {
                newWorkSapce.BookmarkMap[bookrmarkName].Add(selectedItem.FileName);
            }
            WorkSpace.Current = newWorkSapce;
        }

        public void BookmarkUpdate()
        {
            CustomPanel.Children.Clear();
            foreach (var pair in WorkSpace.Current.BookmarkMap)
            {
                Button bookmark = new();
                bookmark.Style = this.FindResource("RoundButton") as Style;
                bookmark.Content = pair.Key;

                CustomPanel.Children.Add(bookmark);

                bookmark.ContextMenu = FindResource("RoundButtonContextMenu") as ContextMenu;
                bookmark.ContextMenu.PlacementTarget = bookmark;

                bookmark.Click += delegate (object sender, RoutedEventArgs e)
                {
                    TableItemViewer.ClearItems();

                    List<string> excelPathList = new();
                    foreach (string tableName in pair.Value)
                    {
                        string excelPath = MExcel.GetExcelPathByTableName(tableName);
                        if(excelPath.Equals(string.Empty) == false)
                        {
                            excelPathList.Add(excelPath);
                        }
                    }

                    InitializeTableItems(excelPathList);
                    OnButtonClicked(sender, e);
                };
            }
        }

        public void InitializeTableItems(List<string> excelPathList)
        {
            TableItemViewer.ClearItems();

            foreach (string excelPath in excelPathList)
            {
                MyItem myItem = new MyItem();
                myItem.BindGameDataTable(excelPath);

                if (AddItem(myItem) == false)
                {
                    Utility.Log("TablePanel에 아이템 추가를 실패했습니다. " + myItem.FileName, LogType.Warning);
                    continue;
                }

                Utility.Log("TablePanel에 아이템 추가" + excelPath);
            }

            // 스크롤 뷰 높이 업데이트
            TableItemViewer.UpdateScrollViewerHeight();
        }

        public bool AddItem(MyItem myItem)
        {
            if (TableItemViewer.AddItem(myItem, out _))
            {
                return true;
            }

            return false;
        }

        public void SelectBookmarkList(string bookmarkName)
        {
            if(MExcel.BookMarkMap.ContainsKey(bookmarkName) == false)
            {
                return;
            }

            MExcel.SelectedBookmarkListName = bookmarkName;
            foreach (var myItemElement in TableItemViewer.ItemListWrapPanel.Children)
            {
                MyItem myItem = myItemElement as MyItem;
                if(myItem == null)
                {
                    continue;
                }

                myItem.SetBookmark(MExcel.BookMarkMap[bookmarkName].Contains(myItem.Path));
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            TextInputDialog dlg = new("새로운 북마크 생성");

            dlg.onClicked += delegate ()
            {
                AddBookmark(dlg.InputText);
            };

            dlg.Show();
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            //TableViewer t = new TableViewer();
            //t.Show();

            //List<string> selectedTablePathList = new();
            //foreach(MyItem myItem in TableItemViewer.SelectedItemList)
            //{
            //    selectedTablePathList.Add(myItem.Path);
            //}

            //t.Init(selectedTablePathList);

            GameDataTable table = GameDataTable.GetTableByPath(TableItemViewer.SelectedItemList.First().Path);
            if(table != null)
            {
                foreach(var t in table.ResourceColums)
                {
                    Utility.Log(t.Value.Name);
                }
            }
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            TableReferenceModifier t = new TableReferenceModifier();
            t.Show();

            List<string> selectedTablePathList = new();
            foreach(MyItem tableItem in TableItemViewer.SelectedItemList)
            {
                selectedTablePathList.Add(tableItem.Path);
            }

            if(selectedTablePathList.Count > 0)
                t.Init(GameDataTable.GameDataTableMap[selectedTablePathList[0]]);
        }

        private void BookMarkedTableListViewer_GotFocus(object sender, RoutedEventArgs e)
        {
            //TableItemViewer.ClearSelectedItems();
            //focusedItemViewr = BookMarkedTableListViewer;
        }

        private void TableItemViewer_GotFocus(object sender, RoutedEventArgs e)
        {
            //BookMarkedTableListViewer.ClearSelectedItems();
            //focusedItemViewr = TableItemViewer;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if(TableItemViewer != null)
            {
                TableItemViewer.UpdateScrollViewerHeight();
            }
        }

        private void MenuItem_Click_3(object sender, RoutedEventArgs e)
        {
            string docPath = System.IO.Path.Combine(WorkSpace.Current.ContentPath, "Doc");

            if (System.IO.Directory.Exists(docPath) == false)
            {
                return;
            }

            if (TableItemViewer == null)
            {
                return;
            }

            List<string> outList = new();
            foreach (MyItem item in TableItemViewer.SelectedItemList)
            {
                outList.Add(item.Path);
            }

            AnvilTeamTable.MakeBinaryFiles(outList);
        }

        private void BookMarkedTableListViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            //focusedItemViewr = BookMarkedTableListViewer;
        }

        private void TableItemViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            //focusedItemViewr = TableItemViewer;
        }

        private void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {
            foreach(MyItem item in TableItemViewer.SelectedItemList)
            {
                Utility.ExecuteProcess(item.Path);
            }
        }

        private void MenuItem_Click_5(object sender, RoutedEventArgs e)
        {
            foreach (MyItem tableItem in TableItemViewer.SelectedItemList)
            {
                //GameDataTable.GetTableByName(tableItem.FileName).FixResourceData();
            }
        }

        public void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string docPath = System.IO.Path.Combine(WorkSpace.Current.ContentPath, "Doc");
            if (System.IO.Directory.Exists(docPath) == false)
            {
                return;
            }

            Process.Start("explorer.exe", docPath);
        }

        public void OnButtonClicked(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if(button == null)
            {
                return;
            }

            if(currentBookmarkButton != null)
            {
                currentBookmarkButton.Style = currentBookmarkButton.Parent == CustomPanel ? this.FindResource("RoundButton") as Style : this.FindResource("RoundSystemButton") as Style;
            }

            currentBookmarkButton = button;
            currentBookmarkButton.Style = this.FindResource("CurrentButton") as Style;
        }

        private void Label_MouseEnter(object sender, MouseEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            InitializeTableItems(MExcel.excelPaths.ToList());
            OnButtonClicked(sender, e);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            TestWindow a = new();
            a.Show();

            
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {

        }

        private void MenuItem_ChangeBookmarkName(object sender, RoutedEventArgs e)
        {
            TextInputDialog a = new("북마크 이름 변경");
            a.Show();
            a.onClicked += delegate ()
            {
                MenuItem mnu = sender as MenuItem;
                Button button = null;

                if (mnu == null)
                {
                    return;
                }

                button = ((ContextMenu)mnu.Parent).PlacementTarget as Button;
                //Button button = sender as Button;
                WorkSpace newWorkSpace = WorkSpace.Current.Clone() as WorkSpace;
                string oldBookmarkName = button.Content as string;
                WorkSpace.Current.BookmarkMap[a.InputText] = WorkSpace.Current.BookmarkMap[oldBookmarkName];
                WorkSpace.Current.BookmarkMap.Remove(oldBookmarkName);

                WorkSpace.Current = newWorkSpace;
            };
        }

        private void MenuItem_RemoveBookmark(object sender, RoutedEventArgs e)
        {
            MenuItem mnu = sender as MenuItem;
            Button button = null;

            if (mnu == null)
            {
                return;
            }

            button = ((ContextMenu)mnu.Parent).PlacementTarget as Button;
            string oldBookmarkName = button.Content as string;
            WorkSpace newWorkSpace = WorkSpace.Current.Clone() as WorkSpace;
            WorkSpace.Current.BookmarkMap.Remove(oldBookmarkName);

            WorkSpace.Current = newWorkSpace;
        }

        //private void Button_Click_1(object sender, RoutedEventArgs e)
        //{
        //    if (TableItemViewer.SelectedItemList.Count == 0)
        //    {
        //        return;
        //    }

        //    int allProgress = 0;

        //    CheckBoxSelector checkBoxSelector = new();
        //    checkBoxSelector.OnButtonClicked += delegate (List<CheckBox> checkedList)
        //    {
        //        List<string> binaryMakingList = new();
        //        foreach(CheckBox checkBox in checkedList)
        //        {
        //            binaryMakingList.Add(MExcel.excelFileNameToPath[checkBox.Content as string]);
        //        }

        //        int loadedRatio = 80;
        //        GameDataTable.MakeBinaryFiles(binaryMakingList, 
        //            (float prgressRatio) => {
        //                checkBoxSelector.UpdateTest((int)(prgressRatio * loadedRatio));

        //                Utility.Log("대기 작업:" + allProgress + " Ratio:" + prgressRatio + ", " + loadedRatio);
        //            return true;
        //            }, 
        //            (float prgressRatio) =>
        //            {
        //                int newValue = loadedRatio + (int)(prgressRatio * (100 - loadedRatio));
        //                checkBoxSelector.UpdateTest(newValue);

        //                Utility.Log("진행도:" + newValue);
        //                return true;
        //            }
        //        );
        //    };

        //    //checkBoxSelector.InitializeItemList(dirtyTableNames);
        //    checkBoxSelector.Show();
        //}
        }
}
