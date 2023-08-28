using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

using System.Text.Json;
using System.Text.Json.Serialization;
using Excel = Microsoft.Office.Interop.Excel;

namespace TestWPF
{
    public partial class MExcel : IDisposable
    {
        public Excel.Application ExcelApplication { get { return excelApp; } }
        private Excel.Application excelApp = null;

        private bool IsOwnedByProcess { get; }

        public bool Disposed { get; set; }

        [DllImport("Oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(int hwnd, uint dwObjectID, byte[] riid, out Microsoft.Office.Interop.Excel.Application ptr);

        public MExcel(string excelFileName)
        {
            FindOrCreateAppication(excelFileName);
        }

        public MExcel(bool bOptimiaztion = true)
        {
            excelApp = new Excel.Application();
            ChangeOptimization(bOptimiaztion);
        }

        public void ChangeOptimization(bool bOptimiaztion)
        {
            if (bOptimiaztion)
            {
                excelApp.Visible = false;
                excelApp.ScreenUpdating = false;
                excelApp.DisplayStatusBar = false;
                excelApp.UserControl = false;
                excelApp.DisplayAlerts = false;
            }
            else
            {
                excelApp.Visible = true;
                excelApp.ScreenUpdating = true;
                excelApp.DisplayStatusBar = true;
                excelApp.UserControl = false;
                excelApp.DisplayAlerts = true;
            }
        }

        ~MExcel()
        {
            DestroyExcelApp();
        }

        void FindOrCreateAppication(string findExcelFileName)
        {
            //excelApp = (Excel.Application)System.Runtime.InteropServices.Marshal.A("Excel.Application");
            //if(excelApp != null)
            //{
            //    IsOwnedByProcess = true;
            //}

            Process[] processes = Process.GetProcessesByName("EXCEL");
            foreach (Process proc in processes)
            {
                if (proc.MainWindowTitle != MExcel.GetProcessMainTitle(findExcelFileName))
                {
                    continue;
                }

                const uint OBJID_NATIVEOM = 0xFFFFFFF0;
                Guid IID_IDispatch = new Guid("{00020400-0000-0000-C000-000000000046}");
                Excel.Window excelWindow = null;
                Microsoft.Office.Interop.Excel.Application app = null;
                int hr = AccessibleObjectFromWindow((int)proc.MainWindowHandle, OBJID_NATIVEOM, IID_IDispatch.ToByteArray(), out app);

                if(hr >= 0)
                {
                    System.Windows.MessageBox.Show("앱을 염");
                    //Excel.Window excelWindow = window as Excel.Window;
                    if (excelWindow != null)
                    {
                        excelApp = excelWindow.Application;
                    }
                }

                break;
            }

            if(excelApp == null)
            {
                //excelApp = new Excel.Application();
            }
        }

        public void Show()
        {
            if (excelApp.Visible == false)
            {
                excelApp.Visible = true;
            }
        }

        public void DestroyExcelApp()
        {
            if(excelApp != null)
            {
                var workbooks = excelApp.Workbooks;
                workbooks.Close();
                excelApp.Quit();

                Marshal.ReleaseComObject(workbooks);
                Marshal.ReleaseComObject(excelApp);
                excelApp = null;
            }
        }

        public bool GetWorkBookAndSheetFromTable(string excelPathFileName, out Excel.Workbook outWorkBook, out Excel.Worksheet outWorkSheet, string sheetName, bool bReadOnly)
        {
            bool bResult = false;

            outWorkBook = null;
            outWorkSheet = null;

            var workBooks = excelApp.Workbooks;
            outWorkBook = workBooks.Open(excelPathFileName, 0, bReadOnly);
            if (outWorkBook != null)
            {
                var workSheets = outWorkBook.Worksheets;
                if (workSheets.Count > 0)
                {
                    foreach (Excel.Worksheet workSheet in workSheets)
                    {
                        // 시트 이름이 없으면 첫번째 시트
                        if (workSheet.Name.ToLower().Contains(sheetName) || sheetName == "")
                        {
                            outWorkSheet = workSheet;
                            bResult = true;
                            break;
                        }
                    }

                    if (bResult == false)
                    {
                        System.Windows.MessageBox.Show("이름이 " + sheetName + "과 일치한 시트가 없습니다.");
                    }
                }
            }

            Marshal.ReleaseComObject(workBooks);

            return bResult;
        }

        public bool GetWorkBookAndSheetFromGameDataTable(string excelPathFileName, out Excel.Workbook outWorkBook, out Excel.Worksheet outWorkSheet, bool bReadOnly)
        {
            return GetWorkBookAndSheetFromTable(excelPathFileName, out outWorkBook, out outWorkSheet, "data", bReadOnly);
        }

        public void Dispose()
        {
            DestroyExcelApp();
            Disposed = true;
        }
    }
}
