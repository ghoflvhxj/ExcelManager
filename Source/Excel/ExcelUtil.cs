using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Excel = Microsoft.Office.Interop.Excel;

namespace TestWPF
{
    public partial class MExcel : IDisposable
    {
        static public HashSet<string> excelPaths = new();
        static public HashSet<string> excelFileNames = new();
        static public Dictionary<string, string> excelFileNameToPath = new(StringComparer.OrdinalIgnoreCase);

        static public string ProcessName { get { return "EXCEL"; } }
        static public string StringTableName { get { return "string"; } }

        // 북마크
        static public Dictionary<string, HashSet<string>> BookMarkMap = new();
        static public string SelectedBookmarkListName = "Default";

        //public static void PostInit()
        //{
        //    foreach (var pair in TableMap)
        //    {
        //        Table table = pair.Value;
        //        table.PostInitInfo();
        //    }
        //}

        //public static void BookmarkLoadFromSaveFile(string filePath)
        //{
        //    if (File.Exists(filePath))
        //    {
        //        string jsonString = File.ReadAllText(filePath);
        //        MExcel.BookMarkMap = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(jsonString);
        //    }
        //}

        public static async void SaveBookmarkFile(string filePath)
        {
            FileStream fileStream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, MExcel.BookMarkMap);
            await fileStream.DisposeAsync();
        }

        public static void AddBookmark(string excelPath)
        {
            Utility.FindOrAdd(BookMarkMap, MExcel.SelectedBookmarkListName).Add(excelPath);
        }

        public static void RemoveBookmark(string excelPath)
        {
            if(BookMarkMap.ContainsKey(MExcel.SelectedBookmarkListName))
            {
                BookMarkMap[MExcel.SelectedBookmarkListName].Remove(excelPath);
            }
        }

        public static string GetProcessMainTitle(string excelFileName)
        {
            return excelFileName + ".xlsx - Excel";
        }

        public static string GetExcelNameFromProcess(Process process)
        {
            return process.MainWindowTitle.Split('.')[0];
        }

        public static string GetExcelPathByTableName(string tableName)
        {
            return excelFileNameToPath.ContainsKey(tableName) ? excelFileNameToPath[tableName] : null;
        }

        public static GameDataTable GetTableByPath(string excelFilePath)
        {
            //if (GameDataTableMap.ContainsKey(excelFilePath))
            //{
            //    return GameDataTableMap[excelFilePath];
            //}

            return null;
        }

        public static bool IsStringTable(GameDataTable table)
        {
            if(GameDataTable.GameDataTableMap.ContainsKey(StringTableName))
            {
                return GameDataTable.GameDataTableMap[StringTableName] == table;
            }

            return false;
        }

        public static void FindExcelByPredicate(Action<Process> Predicate)
        {
            Process[] processes = Process.GetProcessesByName("EXCEL");
            foreach (Process proc in processes)
            {
                Predicate(proc);
                //if (proc.MainWindowTitle.Contains(excelFileName))
                //{
                //    proc.Kill();
                //    break;
                //}
            }
        }
    }
}
