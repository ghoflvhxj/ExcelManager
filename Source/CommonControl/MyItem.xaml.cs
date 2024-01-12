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
using System.IO;
using System.Diagnostics;
using System.Drawing;
using Excel = Microsoft.Office.Interop.Excel;

namespace TestWPF
{
    /// <summary>
    /// MyItem.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MyItem : UserControl
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public bool BookMarkable { get; set; }
        public bool BookMarked { get; set; }
        private MyItem CopyItem { get; set; }
        public bool Selected { get; set; }

        public delegate void FOnBookMarkChangedDelegate();
        public FOnBookMarkChangedDelegate OnBookMarkChangedDelegate;
        public delegate void FOnSelectionChangedDelegate();
        public FOnSelectionChangedDelegate OnSelectionChangedDelegate;
        public delegate void FOnRightClicked();
        public FOnRightClicked OnRightClicked;
        public delegate void FOnMouseHoverChanged(bool bEntered);
        public FOnMouseHoverChanged OnMouseHoverChanged;

        private static SolidColorBrush GreenBrsuh = new SolidColorBrush(Colors.Green);
        private static SolidColorBrush BlackBrsuh = new SolidColorBrush(Colors.Black);

        public MyItem(MyItem Rhs)
        {
            InitializeComponent();
            ExcelIcon.Source = Rhs.ExcelIcon.Source;
            FileNameTextBlock.Text = Rhs.FileNameTextBlock.Text;

            OnBookMarkChangedDelegate += OnBookMarkChanged;

            Path = Rhs.Path;
            FileName = Rhs.FileName;

            //InfoColumnCount.Content = Rhs.InfoColumnCount.Content;
            //InfoRowCount.Content = Rhs.InfoColumnCount.Content;
            //InfoReferencedTables.Content = Rhs.InfoReferencedTables.Content;
            //InfoUnknonwReferencedTables.Content = Rhs.InfoUnknonwReferencedTables.Content;

            Rhs.CopyItem = this;
        }

        public MyItem()
        {
            InitializeComponent();

            BookMarkable = true;
            BookMarkIcon.Visibility = Visibility.Collapsed;
            OnBookMarkChangedDelegate += OnBookMarkChanged;
        }

        public void BindExcelPath(string newExcelPath)
        {
            FileName = Utility.GetOnlyFileName(newExcelPath);
            Path = newExcelPath;

            FileNameTextBlock.Text = FileName;
        }

        public void InitInfoUI()
        {
            //if(MExcel.TableMap.ContainsKey(ExcelPath) == false)
            //{
            //    return;
            //}

            //GameDataTable table = MExcel.TableMap[ExcelPath];
            //table.MakeInfo();
            //InfoColumnCount.Content = table.ColumnCount;
            //InfoRowCount.Content = table.LastRecordIndex;

            //List<string> referencedTableName = new List<string>();
            //List<string> unknownReferencedTableName = new List<string>();
            //foreach(var tableName in table.ReferencedTableNames)
            //{
            //    if(MExcel.excelFileNames.Contains(tableName))
            //    {
            //        referencedTableName.Add(tableName);
            //    }
            //    else
            //    {
            //        unknownReferencedTableName.Add(tableName);
            //    }
            //}    

            //if (referencedTableName.Count > 0)
            //{
            //    InfoReferencedTables.Content = string.Join(", ", referencedTableName);
            //}

            //if (unknownReferencedTableName.Count > 0)
            //{
            //    InfoUnknonwReferencedTables.Content = string.Join(", ", unknownReferencedTableName);
            //}
        }

        public void SetBookmark(bool bNewbookmark)
        {
            if (BookMarkable == false)
            {
                return;
            }

            if(BookMarked != bNewbookmark)
            {
                BookMarked = bNewbookmark;
                OnBookMarkChangedDelegate();
            }
        }

        //private void Button_MouseDown(object sender, MouseButtonEventArgs e)
        //{
        //    Utility.Log("버튼 눌림");

        //    if (Selected == false)
        //    {
        //        Select();
        //    }
        //}

        public void Select()
        {
            Selected = true;
            SelectedRectangle.Visibility = Visibility.Visible;
            MouseEnterBorder.BorderBrush = GreenBrsuh;
        }

        public void UnSelect()
        {
            Selected = false;
            SelectedRectangle.Visibility = Visibility.Hidden;
            MouseEnterBorder.BorderBrush = BlackBrsuh;
        }

        public void ToggleSelect()
        {
            if(Selected)
            {
                UnSelect();
            }
            else
            { 
                Select();
            }
        }

        private void OnBookMarkChanged()
        {
            if(BookMarked)
            {
                BookMarkIcon.Visibility = Visibility.Visible;

                MExcel.AddBookmark(Path);
            }
            else
            {
                BookMarkIcon.Visibility = Visibility.Collapsed;

                bool bCopyFile = CopyItem == null;
                if (bCopyFile)
                {
                    WrapPanel panel = Parent as WrapPanel;
                    panel.Children.Remove(this);
                }
                else
                {
                    WrapPanel panel = CopyItem.Parent as WrapPanel;
                    panel.Children.Remove(CopyItem);
                }

                MExcel.RemoveBookmark(Path);
            }
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            if (OnMouseHoverChanged != null)
            {
                OnMouseHoverChanged(true);
            }
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            if (OnMouseHoverChanged != null)
            {
                OnMouseHoverChanged(false);
            }
        }
    }
}
