using System;
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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Excel = Microsoft.Office.Interop.Excel;

namespace TestWPF
{
    public partial class TablePanel : UserControl
    {
        static System.Text.RegularExpressions.Regex reg = new System.Text.RegularExpressions.Regex("[^0-9]");

        public Dictionary<string, MyItem> MyItemMap = new();
        public HashSet<MyItem> SelectedItems = new();

        private ItemViewer focusedItemViewr = null;

        public TablePanel()
        {
            InitializeComponent();

            // 델리게이트 바인딩
            MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
            if(mainWindow != null)
            {
                mainWindow.onTraversalFinished += delegate() {
                    // 테이블 리스트 뷰 패널에 아이템 추가
                    InitializePanels();

                    // 테이블 정보 표시
                    UpdateInfoUI();

                    // 북마크 선택
                    SelectBookmarkList("Default");

                    // 스크롤 뷰 높이 업데이트
                    TableItemViewer.UpdateScrollViewerHeight();
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

            focusedItemViewr = TableItemViewer;
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

        public void InitializePanels()
        {
            foreach (string excelPath in MExcel.excelPaths)
            {
                MyItem myItem = new MyItem();
                myItem.BindExcelPath(excelPath);
                if (AddItem(myItem))
                {
                    MExcel.TableMap.TryAdd(excelPath, new GameDataTable());
                }
            }
        }

        public bool AddItem(MyItem myItem)
        {
            if(MyItemMap.ContainsKey(myItem.Path))
            {
                return false;
            }

            if (TableItemViewer.AddItem(myItem, out _))
            {
                MyItemMap.Add(myItem.Path, myItem);
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
            bool bIsAllSelectedItemsBookmarekd = true;
            foreach (MyItem selectedItem in TableItemViewer.SelectedItemList)
            {
                if (selectedItem.BookMarked == false)
                {
                    bIsAllSelectedItemsBookmarekd = false;
                    break;
                }
            }

            foreach (MyItem selectedItem in TableItemViewer.SelectedItemList)
            {
                selectedItem.SetBookmark(!bIsAllSelectedItemsBookmarekd);
            }
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            TableViewer t = new TableViewer();
            t.Show();

            List<string> selectedTablePathList = new();
            foreach(MyItem myItem in TableItemViewer.SelectedItemList)
            {
                selectedTablePathList.Add(myItem.Path);
            }

            t.Init(selectedTablePathList);
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            TableReferenceModifier t = new TableReferenceModifier();
            t.Show();
            //t.Init(MExcel.TableMap[ExcelPath]);
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

            if (focusedItemViewr == null)
            {
                return;
            }

            List<string> temp = new();
            foreach (MyItem item in focusedItemViewr.SelectedItemList)
            {
                temp.Add(item.Path);
            }

            GameDataTable.MakeBinaryFiles(temp, null, null);
        }

        private void BookMarkedTableListViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            //focusedItemViewr = BookMarkedTableListViewer;
        }

        private void TableItemViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            focusedItemViewr = TableItemViewer;
        }

        private void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {
            foreach(MyItem item in focusedItemViewr.SelectedItemList)
            {
                Utility.ExecuteProcess(item.Path);
            }
        }

        private void MenuItem_Click_5(object sender, RoutedEventArgs e)
        {
            foreach (MyItem item in focusedItemViewr.SelectedItemList)
            {
                MExcel.GetTableByPath(item.Path).FixResourceData();
            }
        }

        private void MenuItem_Click_6(object sender, RoutedEventArgs e)
        {

        }

        public void Button_Click_1(object sender, RoutedEventArgs e)
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
