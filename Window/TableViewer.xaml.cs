using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Data;
using System.Reflection;
using System.Runtime.InteropServices;
using Excel = Microsoft.Office.Interop.Excel;

namespace TestWPF
{
    /// <summary>
    /// TableViewer.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TableViewer : Window
    {
        public string TableName { get; set; }
        public DataTable MyDataTable { get; set; }
        public object[,] CopiedDataArray { get; set; }

        struct GridPosition
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        private List<GridPosition> wrongBindPosition;

        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(int hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public TableViewer()
        {
            InitializeComponent();
        }

        public void Init(List<string> tableNames)
        {
            if(tableNames.Count == 0)
            {
                Utility.Log("tableNames.Count == 0", Utility.LogType.Warning);
                return;
            }

            TableName = tableNames[0];

            GameDataTable table = GetTable();
            if (table == null)
            {
                return;
            }

            MExcel mExcel = new MExcel(true);
            table.LoadLatest(mExcel);
            if (table.DataArray == null)
            {
                table.LoadGameDataTable(mExcel);
                table.MakeInfo();
            }
            CopiedDataArray = table.DataArray;

            foreach (string refTableName in table.ReferencedTableNames)
            {
                GameDataTable referencedTable = MExcel.GetTableByName(refTableName);
                if(referencedTable == null)
                {
                    continue;
                }

                referencedTable.LoadLatest(mExcel);
                if (referencedTable.DataArray == null)
                {
                    referencedTable.LoadGameDataTable(mExcel);
                }

                referencedTable.MakeInfo();
            }
            //mExcel.Dispose();

            // 코멘트 목록
            CommentComboBox.Items.Clear();
            int commentCount = table.CommentColumns.Count;
            for(int i=0; i<commentCount; ++i)
            {
                CommentComboBox.Items.Add(i);
            }
            CommentComboBox.SelectedIndex = 0;

            // 데이터그리드
            LoadDataGrid();

            int MaxFrozneCount = 5/*table.ColumnHeaders.Count*/;
            for (int i = 0; i <= MaxFrozneCount; ++i)
            {
                FrozenColumCountComboBox.Items.Add(i);
            }
            FrozenColumCountComboBox.SelectedItem = FrozenColumCountComboBox.Items.IndexOf(2) ;
        }

        private void LoadDataGrid()
        {
            GameDataTable table = GetTable();
            if (table == null)
            {
                return;
            }

            List<ColumnHeader> IndexExclusiveColumnHeaders = table.ColumnHeaders.ToList();
            IndexExclusiveColumnHeaders.Remove(table.IndexColumn);

            MyDataGrid.ItemsSource = null;
            wrongBindPosition = new();

            ColumnHeader CommentColumn = table.CommentColumns.ElementAtOrDefault(CommentComboBox.SelectedIndex);

            // 칼럼 추가
            MyDataTable = new DataTable();
            if (table.IndexColumn.ColumnIndex != 0)
            {
                MyDataTable.Columns.Add("인덱스");
            }
            if (CommentColumn.ColumnIndex != 0)
            {
                MyDataTable.Columns.Add("코멘트");
            }

            foreach (var columnHeader in IndexExclusiveColumnHeaders)
            {
                MyDataTable.Columns.Add(columnHeader.Name);
            }

            // 로우 추가
            for (int i = (int)EColumnHeaderElement.Count + 1; i < table.RowCount; ++i)
            {
                DataRow dataRow = MyDataTable.NewRow();

                // 인덱스,코멘트 칼럼 데이터를 맨 앞에
                int j = 0;
                if (table.IndexColumn.ColumnIndex != 0)
                {
                    dataRow[j++] = table.DataArray[i, table.IndexColumn.ColumnIndex];
                }

                if (CommentColumn.ColumnIndex != 0)
                {
                    dataRow[j++] = table.DataArray[i, CommentColumn.ColumnIndex];
                }

                // 나머지 칼럼을 순회하며 데이터 추가
                foreach (var columnHeader in IndexExclusiveColumnHeaders)
                {
                    object data = table.DataArray[i, columnHeader.ColumnIndex];
                    dataRow[j++] = data;
                    if (data == null)
                    {
                        continue;
                    }

                    if (table.IsValidForeignColumnName(columnHeader.Name) == false)
                    {
                        continue;
                    }

                    ForeignKeyInfo foreignKeyInfo = table.ForeignKeyInfoMap[columnHeader.Name];
                    GameDataTable referencedTable = MExcel.GetTableByName(foreignKeyInfo.ReferencedTableName);
                    if (referencedTable == null)
                    {
                        continue;
                    }

                    if (referencedTable.IsValidColumnName(foreignKeyInfo.ForeignKeyName) == false)
                    {
                        continue;
                    }

                    ColumnHeader referencedTableCommentColumn = referencedTable.CommentColumns.ElementAtOrDefault(0);
                    if (foreignKeyInfo.ReferencedTableName.ToLower() == MExcel.StringTableName)
                    {
                        referencedTableCommentColumn = referencedTable.ColumnHeaders[1];
                    }
                    else
                    {
                        if (referencedTableCommentColumn == null || referencedTableCommentColumn.ColumnIndex == 0)
                        {
                            continue;
                        }
                    }

                    // 외부 테이블의 코멘트로 변경 시키기
                    string[] foreignIndexStrings = Convert.ToString(data).Split(",");
                    List<string> replacedData = new List<string>();
                    foreach (string foreignIndexString in foreignIndexStrings)
                    {
                        string commentString = null;

                        if (foreignIndexString.Length > 0 && foreignIndexString.All(char.IsDigit))
                        {
                            int foreignIndex = Convert.ToInt32(foreignIndexString);
                            if (referencedTable.IsIndexColumn(foreignKeyInfo.ForeignKeyName))
                            {
                                if (foreignIndex != 0 && referencedTable.RecordIndexToDataArrayIndex.ContainsKey(foreignIndex))
                                {
                                    commentString = Convert.ToString(referencedTable.DataArray[referencedTable.RecordIndexToDataArrayIndex[foreignIndex], referencedTableCommentColumn.ColumnIndex]);
                                }
                            }
                            else
                            {
                                if (referencedTable.ColumnNameToColumnHeader.ContainsKey(foreignKeyInfo.ForeignKeyName))
                                {
                                    int columnIndex = referencedTable.ColumnNameToColumnHeader[foreignKeyInfo.ForeignKeyName].ColumnIndex;
                                    for (int k = (int)EColumnHeaderElement.Count + 1; k < referencedTable.RowCount; ++k)
                                    {
                                        if (referencedTable.DataArray[k, columnIndex] != null)
                                        {
                                            string str = Convert.ToString(referencedTable.DataArray[k, columnIndex]);
                                            string[] splitedStr = str.Split(',');
                                            if (splitedStr.Contains(foreignIndexString))
                                            {
                                                commentString = Convert.ToString(referencedTable.DataArray[k, referencedTableCommentColumn.ColumnIndex]);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            if (commentString == null)
                            {
                                commentString = GetErrorMessage(Convert.ToString(foreignIndex));
                                GridPosition a = new GridPosition();
                                a.X = i - (int)EColumnHeaderElement.Count;
                                a.Y = j;
                                wrongBindPosition.Add(a);
                            }
                        }
                        else
                        {
                            commentString = GetErrorMessage(foreignIndexString);
                        }
                        replacedData.Add(Convert.ToString(commentString));
                    }

                    if (replacedData.Count > 0)
                    {
                        dataRow[j - 1] = string.Join(',', replacedData);
                    }
                }

                MyDataTable.Rows.Add(dataRow);
            }

            MyDataGrid.ItemsSource = MyDataTable.DefaultView;
            MyDataGrid.FrozenColumnCount = 2;

            foreach (var girdPosition in wrongBindPosition)
            {
                DataGridRow row = MyDataGrid.ItemContainerGenerator.ContainerFromIndex(girdPosition.X) as DataGridRow;
                if (row != null)
                {
                    row.Background = new SolidColorBrush(Colors.Red);
                }
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ChangeForeignColumnVisibility(false);
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ChangeForeignColumnVisibility(true);
        }

        private void ChangeForeignColumnVisibility(bool bVisible)
        {
            GameDataTable table = GetTable();
            if (table == null)
            {
                return;
            }

            int columnCount = MyDataGrid.Columns.Count;
            for (int i = 0; i < columnCount; ++i)
            {
                string columnName = Convert.ToString(MyDataGrid.Columns[i].Header);
                if (table.ColumnNameToColumnHeader.ContainsKey(columnName) == false)
                {
                    continue;
                }

                if (table.ForeignKeyInfoMap.ContainsKey(columnName) == false)
                {
                    MyDataGrid.Columns[i].Visibility = bVisible == true ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private GameDataTable GetTable()
        {
            if(MExcel.TableMap.ContainsKey(TableName))
            {
                return MExcel.TableMap[TableName];
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            GameDataTable table = GetTable();
            if (table == null)
            {
                return;
            }

            int columnCount = MyDataGrid.Columns.Count;
            for (int i = 0; i < columnCount; ++i)
            {
                MyDataGrid.Columns[i].Visibility = Visibility.Visible;
            }

            NotForeignColumnHideCheckBox.IsChecked = false;
        }

        private void MyDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            GameDataTable table = GetTable();
            if (table == null)
            {
                return;
            }

            foreach(string referecnedTableName in table.ReferencedTableNames)
            {
                //MExcel newExcel = new MExcel(referecnedTableName);
            }

            
            foreach (DataGridCellInfo cellInfo in MyDataGrid.SelectedCells)
            {
                //string columnName = Convert.ToString(cellInfo.Column.Header);
                //if(table.ColumnNameToColumnHeader.ContainsKey(columnName) == false)
                //{
                //    continue;
                //}

                //MExcel mExcel = new MExcel(false);
                //Process[] processes = Process.GetProcessesByName("EXCEL");
                //foreach (Process proc in processes)
                //{
                //    foreach (string excelFileName in MExcel.excelFileNames)
                //    {
                //        if (proc.MainWindowTitle == Path.GetFileName(excelFileName) + " - " + "Excel")
                //        {
                //            //proc.CloseMainWindow();
                //            //proc.WaitForExit();
                //            proc.Kill();
                //        }
                //    }
                //}
            }
        }

        private MExcel mExcel = null;
        private Dictionary<string, Tuple<Excel.Workbook, Excel.Worksheet>> OpenedExcelByTableViewer = new();
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            HashSet<string> referencedRanges = new();
            Excel.Worksheet referencedWorksheet = null;
            GetReferencedRagne(ref referencedRanges, ref referencedWorksheet, true);

            // 참조된 범위가 있다면 선택
            if (referencedWorksheet != null && referencedRanges.Count > 0)
            {
                string first = referencedRanges.First();
                Excel.Range unionRange = referencedWorksheet.get_Range(first);
                referencedRanges.Remove(first);

                string temp = string.Join(",", referencedRanges);
                foreach (var ran in referencedRanges)
                {
                    unionRange = mExcel.ExcelApplication.Union(unionRange, referencedWorksheet.get_Range(ran));
                }

                try
                {
                    unionRange.Select();
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                SetForegroundWindow((IntPtr)mExcel.ExcelApplication.Hwnd);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if(mExcel != null)
            {
                //mExcel.Dispose();
                mExcel = null;
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(FrozenColumCountComboBox.SelectedItem == null)
            {
                return;
            }

            MyDataGrid.FrozenColumnCount = Convert.ToInt32(FrozenColumCountComboBox.SelectedItem);
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            HashSet<DataGridColumn> selectedColumns = GetSelectedDataGridColumns();
            if (selectedColumns == null)
            {
                return;
            }

            int columnCount = MyDataGrid.Columns.Count;
            for (int i = 2; i < columnCount; ++i)
            {
                MyDataGrid.Columns[i].Visibility = selectedColumns.Contains(MyDataGrid.Columns[i]) == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            HashSet<DataGridColumn> selectedColumns = GetSelectedDataGridColumns();
            if(selectedColumns == null)
            {
                return;
            }

            int columnCount = MyDataGrid.Columns.Count;
            for (int i = 2; i < columnCount; ++i)
            {
                if (selectedColumns.Contains(MyDataGrid.Columns[i]))
                {
                    MyDataGrid.Columns[i].Visibility = Visibility.Collapsed;
                }
            }
        }

        private HashSet<DataGridColumn> GetSelectedDataGridColumns()
        {
            HashSet<DataGridColumn> selectedColumns = new();
            foreach (var selectedCell in MyDataGrid.SelectedCells)
            {
                selectedColumns.Add(selectedCell.Column);
            }

            return selectedColumns;
        }

        private void MenuItem_Click_3(object sender, RoutedEventArgs e)
        {
            foreach (var selectedCell in MyDataGrid.SelectedCells)
            {
                DataGridRow row = selectedCell.Item as DataGridRow;
                if(row != null)
                {
                    row.Visibility = Visibility.Hidden;
                }
            }
        }

        private void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {

        }

        private void CommentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadDataGrid();
        }

        private void MenuItem_Click_5(object sender, RoutedEventArgs e)
        {
            HashSet<string> referencedRanges = new();
            Excel.Worksheet referencedWorksheet = null;
            GetReferencedRagne(ref referencedRanges, ref referencedWorksheet, false);

            Excel.Workbook newWorkbook = mExcel.ExcelApplication.Workbooks.Add();
            Excel.Worksheet newWorksheet = (Excel.Worksheet)newWorkbook.Worksheets.Add();

            // 참조된 범위가 있다면 선택
            if (referencedWorksheet != null && referencedRanges.Count > 0)
            {
                GameDataTable table = GetTable();
                if(table == null)
                {
                    return;
                }

                // 칼럼의 이름을 얻기
                DataGridCellInfo cellInfo = MyDataGrid.SelectedCells[0];
                string columnName = Convert.ToString(cellInfo.Column.Header);

                // 유효한 칼럼인지 검사
                if (table.IsValidForeignColumnName(columnName) == false)
                {
                    return;
                }

                // 참조된 테이블이 불러와져 있는지
                ForeignKeyInfo foreignKeyInfo = table.ForeignKeyInfoMap[columnName];
                if (OpenedExcelByTableViewer.ContainsKey(foreignKeyInfo.ReferencedTableName) == false)
                {
                    return;
                }

                GameDataTable referencedTable = MExcel.GetTableByName(foreignKeyInfo.ReferencedTableName);
                if(referencedTable == null)
                {
                    return;
                }

                string columNameRange = "A1:" + Utility.ConvetToExcelColumn(referencedTable.ColumnCount) + 1;
                referencedWorksheet.get_Range(columNameRange).Copy();
                newWorksheet.get_Range("A1").PasteSpecial(Excel.XlPasteType.xlPasteColumnWidths);
                newWorksheet.get_Range("A1").PasteSpecial(Excel.XlPasteType.xlPasteAll);

                var ranges = referencedRanges.ToList();
                for (int i = 0; i < ranges.Count; ++i)
                {
                    referencedWorksheet.get_Range(ranges[i]).Copy();
                    newWorksheet.get_Range("A" + (2 + i)).PasteSpecial(Excel.XlPasteType.xlPasteColumnWidths);
                    newWorksheet.get_Range("A" + (2 + i)).PasteSpecial(Excel.XlPasteType.xlPasteAll);
                }

                OnWorkedInExcel();
            }
        }

        private void GetReferencedRagne(ref HashSet<string> ReferencedRanges, ref Excel.Worksheet referencedWorkSheet, bool bOnlyIndexColumn)
        {
            GameDataTable table = GetTable();
            if (table == null)
            {
                return;
            }

            // 열어야 할 엑셀 파일들을 찾는다
            List<string> excelFileNamesToOpen = new();
            foreach (DataGridCellInfo cellInfo in MyDataGrid.SelectedCells)
            {
                string columnName = Convert.ToString(cellInfo.Column.Header);
                if (table.ForeignKeyInfoMap.ContainsKey(columnName) == false)
                {
                    continue;
                }

                string referencedTableName = table.ForeignKeyInfoMap[columnName].ReferencedTableName;
                if (MExcel.excelFileNames.Contains(referencedTableName) == false)
                {
                    continue;
                }

                excelFileNamesToOpen.Add(referencedTableName);
            }

            // 다른 곳에 열려있는 것들을 찾는다.
            HashSet<string> excelFileNamesToClose = new(StringComparer.OrdinalIgnoreCase);
            MExcel.FindExcelByPredicate(new Action<Process>((Process proc) => {
                string excelFileName = MExcel.GetExcelNameFromProcess(proc);

                if (OpenedExcelByTableViewer.ContainsKey(excelFileName))
                {
                    return;
                }

                if (excelFileNamesToOpen.Contains(excelFileName) == false)
                {
                    return;
                }

                excelFileNamesToClose.Add(excelFileName);
            }));
        
            // 물어보고 다른 곳에 열린 것들을 닫는다
            if(excelFileNamesToClose.Count > 0)
            {
                if (MessageBox.Show("다른 곳에서 열린 엑셀파일들을 닫아야 합니다.\r\n닫을까요?", "알림", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    MExcel.FindExcelByPredicate(new Action<Process>((Process proc) =>
                    {
                        string excelFileName = MExcel.GetExcelNameFromProcess(proc);
                        if (excelFileNamesToClose.Contains(excelFileName))
                        {
                            proc.Kill();
                        }
                    }));
                }
                else
                {
                    return;
                }
            }

            // 엑셀 파일들을 연다.
            if (mExcel == null)
            {
                mExcel = new MExcel(false);
            }

            mExcel.Show();
            foreach (string excelFileName in excelFileNamesToOpen)
            {
                if (OpenedExcelByTableViewer.ContainsKey(excelFileName))
                {
                    continue;
                }
                Excel.Workbook workBook = null;
                Excel.Worksheet workSheet = null;

                if (mExcel.GetWorkBookAndSheetFromGameDataTable(MExcel.excelFileNameToPath[excelFileName], out workBook, out workSheet, false))
                {
                    workBook.BeforeClose += new Excel.WorkbookEvents_BeforeCloseEventHandler((ref bool cancel) =>
                    {
                        OpenedExcelByTableViewer.Remove(excelFileName);
                    });

                    workBook.AfterSave += new Excel.WorkbookEvents_AfterSaveEventHandler((bool target) =>
                    {
                        Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                        {
                            List<string> a = new();
                            a.Add(TableName);
                            Init(a);
                        }));
                    });
                }
                OpenedExcelByTableViewer.Add(excelFileName, new Tuple<Excel.Workbook, Excel.Worksheet>(workBook, workSheet));
            }

            if (MyDataGrid.SelectedCells.Count > 0)
            {
                // 칼럼의 이름을 얻기
                DataGridCellInfo cellInfo = MyDataGrid.SelectedCells[0];
                string columnName = Convert.ToString(cellInfo.Column.Header);

                // 유효한 칼럼인지 검사
                if (table.IsValidForeignColumnName(columnName) == false)
                {
                    return;
                }

                // 참조된 테이블이 불러와져 있는지
                ForeignKeyInfo foreignKeyInfo = table.ForeignKeyInfoMap[columnName];
                if (OpenedExcelByTableViewer.ContainsKey(foreignKeyInfo.ReferencedTableName) == false)
                {
                    return;
                }

                // 레퍼런스 테이블 확인
                GameDataTable referencedTable = MExcel.GetTableByName(foreignKeyInfo.ReferencedTableName);
                if (referencedTable == null)
                {
                    return;
                }

                // 참조된 키를 구하기
                foreach(var selectedCell in MyDataGrid.SelectedCells)
                {
                    int dataGridRowIndex = MyDataGrid.Items.IndexOf(selectedCell.Item);
                    ColumnHeader columnHeader = table.ColumnNameToColumnHeader[columnName];

                    object selectedData = table.DataArray[(int)EColumnHeaderElement.Count + 1 + dataGridRowIndex, columnHeader.ColumnIndex];
                    if (selectedData == null)
                    {
                        return;
                    }

                    HashSet<string> selectedDataStrings = Convert.ToString(selectedData).Split(',').ToHashSet();
                    referencedWorkSheet = OpenedExcelByTableViewer[foreignKeyInfo.ReferencedTableName].Item2;
                    referencedWorkSheet.Activate();

                    // 참조된 키가 존재하는 범위를 구하기
                    if (foreignKeyInfo.ForeignKeyName.ToLower() == "index")
                    {
                        foreach (string selectedDataString in selectedDataStrings)
                        {
                            int referencedIndex = Convert.ToInt32(selectedDataString);
                            if (referencedTable.RecordIndexToDataArrayIndex.ContainsKey(referencedIndex))
                            {
                                int dataArrayIndex = referencedTable.RecordIndexToDataArrayIndex[referencedIndex];
                                ReferencedRanges.Add("A" + dataArrayIndex + ":" + (bOnlyIndexColumn ? "A" : Utility.ConvetToExcelColumn(referencedTable.ColumnCount)) + dataArrayIndex);
                            }
                        }
                    }
                    else
                    {
                        if (referencedTable.IsValidColumnName(foreignKeyInfo.ForeignKeyName) == false)
                        {
                            return;
                        }

                        ColumnHeader referencedColumnHeader = referencedTable.ColumnNameToColumnHeader[foreignKeyInfo.ForeignKeyName];
                        for (int i = (int)EColumnHeaderElement.Count + 1; i < referencedTable.RowCount; ++i)
                        {
                            object referencedTableData = referencedTable.DataArray[i, referencedColumnHeader.ColumnIndex];
                            if (referencedTableData == null)
                            {
                                continue;
                            }

                            HashSet<string> referencedDataStrings = Convert.ToString(referencedTableData).Split(',').ToHashSet();

                            if (selectedDataStrings.Count > 0)
                            {
                                foreach (string selectedDataString in selectedDataStrings)
                                {
                                    if (referencedDataStrings.Contains(selectedDataString))
                                    {
                                        ReferencedRanges.Add("A" + i + ":" + (bOnlyIndexColumn ? "A" : Utility.ConvetToExcelColumn(referencedTable.ColumnCount)) + i);
                                    }
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void MyDataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            var textBox = e.EditingElement as TextBox;
            if(textBox == null)
            {
                return;
            }

            GameDataTable table = GetTable();
            if (table == null)
            {
                return;
            }

            string columnName = Convert.ToString(e.Column.Header);
            if (table.IsValidForeignColumnName(columnName) == false)
            {
                return;
            }

            int rowIndex = 0, columnIndex = 0;
            GetCellToDataArrayIndex(table, e.Row.GetIndex(), columnName, ref rowIndex, ref columnIndex);
            textBox.Text = Convert.ToString(CopiedDataArray[rowIndex, columnIndex]);

            ForeignKeyInfo foreignKeyInfo = table.ForeignKeyInfoMap[columnName];
            GameDataTable referencedTable = MExcel.GetTableByName(foreignKeyInfo.ReferencedTableName);

            // 텍스트가 편집될 때 마다 갱신
            if (MExcel.IsStringTable(referencedTable) || referencedTable.IsIndexColumn(foreignKeyInfo.ForeignKeyName))
            {
                int referencedTableCommentColunm = MExcel.IsStringTable(referencedTable) ? 2 : referencedTable.CommentColumns[0].ColumnIndex;
                textBox.TextChanged += new TextChangedEventHandler(delegate (object sender, TextChangedEventArgs e)
                {
                    string LastTextBoxString = textBox.Text.Split(',').Last();

                    // 팝업
                    if(LastTextBoxString == "")
                    {
                        IndexHelperPopup.IsOpen = false;
                    }
                    else
                    {
                        IndexHelperPopup.IsOpen = true;
                    }

                    IndexHelperListBox.Items.Clear();
                    for(int i= (int)EColumnHeaderElement.Count + 1; i<referencedTable.RowCount; ++i)
                    {
                        object indexData = referencedTable.DataArray[i, referencedTable.IndexColumn.ColumnIndex];
                        if (indexData == null)
                        {
                            continue;
                        }

                        string indexDataString = Convert.ToString(indexData);
                        if(indexDataString.Contains(LastTextBoxString))
                        {
                            string presentString = indexDataString;
                            if(referencedTable.DataArray[i, referencedTableCommentColunm] != null)
                            {
                                indexDataString += " = " + Convert.ToString(referencedTable.DataArray[i, referencedTableCommentColunm]);
                            }
                            IndexHelperListBox.Items.Add(indexDataString);
                        }
                    }
                });
            }
            else
            {

            }
        }

        private void MyDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            var textBox = e.EditingElement as TextBox;
            if (textBox == null)
            {
                return;
            }

            GameDataTable table = GetTable();
            if (table == null)
            {
                return;
            }

            string columnName = Convert.ToString(e.Column.Header);
            if (table.IsValidForeignColumnName(columnName) == false)
            {
                return;
            }

            ForeignKeyInfo foreignKeyInfo = table.ForeignKeyInfoMap[columnName];
            GameDataTable referencedTable = MExcel.GetTableByName(foreignKeyInfo.ReferencedTableName);

            string[] indexStrings = textBox.Text.Split(',');
            if (referencedTable.IsValidColumnName(foreignKeyInfo.ForeignKeyName) == false)
            {
                return;
            }

            List<string> IndexToCommentStrings = new();

            // 변경된 데이터에 맞게 코멘트를 찾는다
            if(referencedTable.IsIndexColumn(foreignKeyInfo.ForeignKeyName))
            {
                int commentColumn = MExcel.IsStringTable(referencedTable) ? 2 : referencedTable.CommentColumns[0].ColumnIndex;
                foreach (string indexString in indexStrings)
                {
                    if(indexString.Length == 0)
                    {
                        continue;
                    }

                    bool bInteger = indexString.All(char.IsDigit);
                    if (bInteger == false)
                    {
                        IndexToCommentStrings.Add(indexString);
                        continue;
                    }

                    int index = Convert.ToInt32(indexString);

                    if(referencedTable.RecordIndexToDataArrayIndex.ContainsKey(index) == false)
                    {
                        IndexToCommentStrings.Add(GetErrorMessage(Convert.ToString(index)));
                        continue;
                    }

                    int refTableRowIndex = referencedTable.RecordIndexToDataArrayIndex[index];
                    int refTableColIndex = commentColumn;
                    IndexToCommentStrings.Add(Convert.ToString(referencedTable.DataArray[refTableRowIndex, refTableColIndex]));
                }
            }
            else
            {

            }

            int rowIndex = 0, columnIndex = 0;
            GetCellToDataArrayIndex(table, e.Row.GetIndex(), columnName, ref rowIndex, ref columnIndex);
            CopiedDataArray[rowIndex, columnIndex] = textBox.Text;
            textBox.Text = string.Join(',', IndexToCommentStrings);

            IndexHelperPopup.IsOpen = false;
        }

        private void GetCellToDataArrayIndex(GameDataTable table, int cellRowIndex, string columnName, ref int row, ref int col)
        {
            row = (int)EColumnHeaderElement.Count + 1 + cellRowIndex;
            col = table.ColumnNameToColumnHeader[columnName].ColumnIndex;
        }

        private string GetErrorMessage(string value)
        {
            return "알 수 없는 데이터 바인딩(" + value + ")";
        }

        private void MenuItem_Click_6(object sender, RoutedEventArgs e)
        {
            GameDataTable table = GetTable();
            if (table == null)
            {
                return;
            }

            //foreach (var selectedCell in MyDataGrid.SelectedCells)
            //{
            //    selectedCell.
            //}
            //var textBox = e.EditingElement as TextBox;
            //if (textBox == null)
            //{
            //    return;
            //}

            //int rowIndex = 0, columnIndex = 0;
            //GetCellToDataArrayIndex(table, e.Row.GetIndex(), columnName, ref rowIndex, ref columnIndex);
            //CopiedDataArray[rowIndex, columnIndex] = textBox.Text;
            //textBox.Text = string.Join(',', IndexToCommentStrings);
        }

        private void OnWorkedInExcel()
        {
            SetForegroundWindow((IntPtr)mExcel.ExcelApplication.Hwnd);
        }

        private void MenuItem_Click_7(object sender, RoutedEventArgs e)
        {

        }
    }
}
