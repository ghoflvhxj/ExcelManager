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
using Excel = Microsoft.Office.Interop.Excel;
using System.IO;

namespace TestWPF
{
    public partial class TablePanel : UserControl
    {
        static System.Text.RegularExpressions.Regex reg = new System.Text.RegularExpressions.Regex("[^0-9]");

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
                    ResetItemViewer<AnvilDataTable>(true);
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

            string jsonDataString = File.ReadAllText(@"C:\Users\mkh2022\Desktop\TestJsonData2.json");
            Utility.Log(jsonDataString);
            BookmarkData bookmarkData = JsonSerializer.Deserialize<BookmarkData>(jsonDataString);

            foreach(var pair in bookmarkData.BookmarkMap)
            {
                Button bookmark = new();
                bookmark.Style = this.FindResource("RoundButton") as Style;
                bookmark.Content = pair.Key;

                CustomPanel.Children.Add(bookmark);

                bookmark.Click += delegate (object sender, RoutedEventArgs e)
                {
                    TableItemViewer.ClearItems();

                    List<string> excelPathList = new();
                    foreach(string tableName in pair.Value)
                    {
                        excelPathList.Add(MExcel.GetExcelPathByTableName(tableName));
                    }

                    InitializeTableItems(excelPathList);
                };
            }

            int a = 0;
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

        public void ResetItemViewer<T>(bool bUseCacheData)
            where T : GameDataTable, new()
        {
            GameDataTable.ResetGameDataTableMap<T>();

            if(bUseCacheData)
            {
                GameDataTable.LoadCachedData<T>();
            }

            InitializeTableItems(MExcel.excelPaths.ToList());

            GameDataTable.LoadGameDataTables();
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

                Utility.Log("TablePanel에 아이템 추가" + excelPath, LogType.Warning);
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
            //bool bIsAllSelectedItemsBookmarekd = true;
            //foreach (MyItem selectedItem in TableItemViewer.SelectedItemList)
            //{
            //    if (selectedItem.BookMarked == false)
            //    {
            //        bIsAllSelectedItemsBookmarekd = false;
            //        break;
            //    }
            //}

            //foreach (MyItem selectedItem in TableItemViewer.SelectedItemList)
            //{
            //    selectedItem.SetBookmark(!bIsAllSelectedItemsBookmarekd);
            //}
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
            string docPath = MainWindow.configManager.GetSectionElementValue(ConfigManager.ESectionType.ContentPath);
            docPath = System.IO.Path.Combine(docPath, "Doc");

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

            GameDataTable.MakeBinaryFiles(outList, null, null);
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
                GameDataTable.GetTableByName(tableItem.FileName).FixResourceData();
            }
        }

        public void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void Label_MouseEnter(object sender, MouseEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // 리로드 테스트
            InitializeTableItems(MExcel.excelPaths.ToList());
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            
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
