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
                    AllTableViewer.UpdateScrollViewerHeight();
                };

                mainWindow.StateChanged += new EventHandler((object sender, EventArgs e) =>
                {
                    AllTableViewer.UpdateScrollViewerHeight();
                });

                mainWindow.SizeChanged += new SizeChangedEventHandler((object sender, SizeChangedEventArgs e) =>
                {
                    AllTableViewer.UpdateScrollViewerHeight();
                });
            }

            focusedItemViewr = AllTableViewer;
        }

        public void UpdateInfoUI()
        {
            foreach (var Item in AllTableViewer.ItemListWrapPanel.Children)
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

            // 북마크 버튼
            string NewBookmarklistName = "새로운 즐겨찾기";
            int NewBookmarkNum = 1;
            Button btn = new Button();
            btn.Content = "+";
            btn.Click += new RoutedEventHandler((object sender, RoutedEventArgs e) =>
            {
                while(true)
                {
                    if(MExcel.BookMarkMap.ContainsKey(NewBookmarklistName) == false)
                    {
                        break;
                    }

                    NewBookmarklistName = "새로운 즐겨찾기" + NewBookmarkNum++;
                }

                AddBookmarkListTextBox(NewBookmarklistName);
            });
            BookMarkedTableListViewer.Children.Add(btn);

            // 기본 북마크 버튼
            foreach(var pair in MExcel.BookMarkMap)
            {
                AddBookmarkListTextBox(pair.Key);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //FindingIndexTextBox.Visibility = FindingIndexTextBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            //if (FindingIndexTextBox.Visibility == Visibility.Collapsed)
            //{
            //    foreach (var children in AllTableViewer.Children)
            //    {
            //        MyItem myItem = children as MyItem;
            //        if (myItem == null)
            //        {
            //            continue;
            //        }

            //        myItem.Visibility = Visibility.Visible;
            //    }
            //}
            //else
            //{
            //    FilteringItem();
            //}
        }

        public bool AddItem(MyItem myItem)
        {
            if(MyItemMap.ContainsKey(myItem.Path))
            {
                return false;
            }

            myItem.OnBookMarkChangedDelegate += delegate ()
            {
                if (myItem.BookMarked)  
                {
                    if (BookMarkedTableListViewer.ItemListWrapPanel.Children.Contains(myItem) == false)
                    {
                        BookMarkedTableListViewer.ItemListWrapPanel.Children.Add(new MyItem(myItem));
                    }
                }
            };

            //myItem.OnRightClicked += delegate ()
            //{
            //    if(SelectedItems.Contains(myItem))
            //    {

            //    }
            //    else
            //    {
            //        myItem.Select();
            //        myItem.OnSelectionChangedDelegate();
            //    }
            //};

            if (AllTableViewer.AddItem(myItem, out _))
            {
                MyItemMap.Add(myItem.Path, myItem);
                return true;
            }

            return false;
        }

        public void AddBookmarkListTextBox(string bookmarkListName)
        {
            MExcel.BookMarkMap.TryAdd(bookmarkListName, new());

            TextBox btn = new TextBox();
            btn.Text = bookmarkListName;
            btn.VerticalAlignment = VerticalAlignment.Stretch;
            btn.IsReadOnly = true;
            btn.Background = new SolidColorBrush(Colors.Transparent);

            btn.AddHandler(TextBox.MouseLeftButtonDownEvent, new MouseButtonEventHandler((object sender, MouseButtonEventArgs e) => {
                SelectBookmarkList(Convert.ToString(btn.Text));
                e.Handled = true;
            }), true);
            btn.LostFocus += new RoutedEventHandler((object sender, RoutedEventArgs e) =>{
                Keyboard.ClearFocus();
                btn.IsReadOnly = true;
                if(MExcel.BookMarkMap.ContainsKey(bookmarkListName))
                {
                    if(MExcel.BookMarkMap.TryAdd(btn.Text, MExcel.BookMarkMap[bookmarkListName]))
                    {
                        MExcel.BookMarkMap.Remove(bookmarkListName);
                        SelectBookmarkList(btn.Text);
                    }
                }
            });
            btn.MouseEnter += new MouseEventHandler((object sender, MouseEventArgs e) =>
            {
                BookMarkedTableListViewer.MouseEnteredByChild = true;
                if(BookMarkedTableListViewer.IsHighlighted)
                {
                    BookMarkedTableListViewer.ToggleHighlight();
                }
                btn.Background = new SolidColorBrush(Colors.Chocolate);
            });
            btn.MouseLeave += new MouseEventHandler((object sender, MouseEventArgs e) =>
            {
                BookMarkedTableListViewer.MouseEnteredByChild = false;
                btn.Background = new SolidColorBrush(Colors.Transparent);
            });
            btn.KeyDown += new KeyEventHandler((object sender, KeyEventArgs e)=>{
                if(e.Key == Key.Enter)
                {
                    btn.RaiseEvent(new RoutedEventArgs(LostFocusEvent, btn));
                }
            });

            ContextMenu contextMenu = new();
            Label label1 = new();
            label1.Content = "이름 변경";
            label1.MouseLeftButtonDown += new MouseButtonEventHandler((object sender, MouseButtonEventArgs e) =>
            {
                btn.IsReadOnly = false;
            });
            Label label2 = new();
            label2.Content = "삭제";
            label2.MouseLeftButtonDown += new MouseButtonEventHandler((object sender, MouseButtonEventArgs e) =>
            {
                MExcel.BookMarkMap.Remove(bookmarkListName);
                BookMarkedTableListViewer.Children.Remove(btn);
            });

            contextMenu.Items.Add(label1);
            contextMenu.Items.Add(label2);
            btn.ContextMenu = contextMenu;

            BookMarkedTableListViewer.Children.Add(btn);

            SelectBookmarkList(bookmarkListName);
        }

        public void SelectBookmarkList(string bookmarkName)
        {
            if(MExcel.BookMarkMap.ContainsKey(bookmarkName) == false)
            {
                return;
            }

            MExcel.SelectedBookmarkListName = bookmarkName;
            foreach (var myItemElement in AllTableViewer.ItemListWrapPanel.Children)
            {
                MyItem myItem = myItemElement as MyItem;
                if(myItem == null)
                {
                    continue;
                }

                myItem.SetBookmark(MExcel.BookMarkMap[bookmarkName].Contains(myItem.Path));
            }
        }

        private void FindingIndexTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            //e.Handled = reg.IsMatch(e.Text);
        }

        private void FindingIndexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //FilteringItem();
        }

        private void FilteringItem()
        {
            //if (FindingIndexTextBox.Text == null || FindingIndexTextBox.Text.Length == 0)
            //{
            //    return;
            //}

            //int findingIndex = Convert.ToInt32(FindingIndexTextBox.Text);

            //foreach (var children in AllTableViewer.ItemListPanel.Children)
            //{
            //    MyItem myItem = children as MyItem;
            //    if (myItem == null)
            //    {
            //        continue;
            //    }

            //    Table table = MExcel.GetTableByPath(myItem.ExcelPath);
            //    if (table != null)
            //    {
            //        if(table.RecordIndexToDataArrayIndex == null)
            //        {
            //            myItem.Visibility = Visibility.Collapsed;
            //            continue;
            //        }

            //        if (table.RecordIndexToDataArrayIndex.ContainsKey(findingIndex))
            //        {
            //            myItem.Visibility = Visibility.Visible;
            //            continue;
            //        }
            //    }

            //    myItem.Visibility = Visibility.Collapsed;
            //}
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool bIsAllSelectedItemsBookmarekd = true;
            foreach (MyItem selectedItem in AllTableViewer.SelectedItemList)
            {
                if (selectedItem.BookMarked == false)
                {
                    bIsAllSelectedItemsBookmarekd = false;
                    break;
                }
            }

            foreach (MyItem selectedItem in AllTableViewer.SelectedItemList)
            {
                selectedItem.SetBookmark(!bIsAllSelectedItemsBookmarekd);
            }
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            TableViewer t = new TableViewer();
            t.Show();

            List<string> selectedTablePathList = new();
            foreach(MyItem myItem in AllTableViewer.SelectedItemList)
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
            //AllTableViewer.ClearSelectedItems();
            //focusedItemViewr = BookMarkedTableListViewer;
        }

        private void AllTableViewer_GotFocus(object sender, RoutedEventArgs e)
        {
            //BookMarkedTableListViewer.ClearSelectedItems();
            //focusedItemViewr = AllTableViewer;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if(AllTableViewer != null)
            {
                AllTableViewer.UpdateScrollViewerHeight();
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
            focusedItemViewr = BookMarkedTableListViewer;
        }

        private void AllTableViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            focusedItemViewr = AllTableViewer;
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

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            List<string> dirtyTableNames = new();

            foreach (var pathToTablePair in MExcel.TableMap)
            {
                GameDataTable gameDataTable = pathToTablePair.Value;
                gameDataTable.UpdateModifiedProperty(out _);
                if (gameDataTable.bIsModified == true)
                {
                    dirtyTableNames.Add(Utility.GetOnlyFileName(pathToTablePair.Key));
                }
            }

            if (dirtyTableNames.Count == 0)
            {
                return;
            }

            int allProgress = 0;
            
            CheckBoxSelector checkBoxSelector = new();
            checkBoxSelector.OnButtonClicked += delegate (List<CheckBox> checkedList)
            {
                List<string> binaryMakingList = new();
                foreach(CheckBox checkBox in checkedList)
                {
                    binaryMakingList.Add(MExcel.excelFileNameToPath[checkBox.Content as string]);
                }

                int loadedRatio = 80;
                GameDataTable.MakeBinaryFiles(binaryMakingList, 
                    (float prgressRatio) => {
                        checkBoxSelector.UpdateTest((int)(prgressRatio * loadedRatio));

                        Utility.Log("대기 작업:" + allProgress + " Ratio:" + prgressRatio + ", " + loadedRatio);
                    return true;
                    }, 
                    (float prgressRatio) =>
                    {
                        int newValue = loadedRatio + (int)(prgressRatio * (100 - loadedRatio));
                        checkBoxSelector.UpdateTest(newValue);

                        Utility.Log("진행도:" + newValue);
                        return true;
                    }
                );
            };

            checkBoxSelector.InitializeItemList(dirtyTableNames);
            checkBoxSelector.Show();
        }
    }
}
