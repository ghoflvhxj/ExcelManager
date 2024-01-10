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
        static public ConcurrentDictionary<string, GameDataTable> TableMap = new ConcurrentDictionary<string, GameDataTable>();

        static public HashSet<string> excelPaths = new HashSet<string>();
        static public HashSet<string> excelFileNames = new HashSet<string>();
        static public Dictionary<string, string> excelFileNameToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static public Dictionary<string, Tuple<Excel.Workbook, Excel.Worksheet>> WorkbookSheetMap = new();

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

        public static void LoadCachedData()
        {
            LoadCachedData(ConfigUtility.CachedDataPath);
            BookmarkLoadFromSaveFile(ConfigUtility.BookmarkFileName);
        }

        public static void LoadCachedData(string filePath)
        {
            if (File.Exists(filePath))
            {
                string jsonString = File.ReadAllText(filePath);
                if(jsonString == "")
                {
                    Utility.Log(filePath + " 데이터를 읽지 못했습니다.", Utility.LogType.Warning);
                    return;
                }

                Utility.Log("파일을 읽습니다 경로: " + Path.GetFullPath(filePath));
                MExcel.TableMap = JsonSerializer.Deserialize<ConcurrentDictionary<string, GameDataTable>>(jsonString);
            }
        }

        public static void BookmarkLoadFromSaveFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                string jsonString = File.ReadAllText(filePath);
                MExcel.BookMarkMap = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(jsonString);
            }
        }

        public static void SaveMetaData()
        {
            SaveMetaData(ConfigUtility.CachedDataPath);
        }

        public static async void SaveMetaData(string filePath)
        {
            FileStream fileStream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, MExcel.TableMap);
            await fileStream.DisposeAsync();
        }

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

        public static GameDataTable GetTableByName(string excelFileName)
        {
            if (excelFileNameToPath.ContainsKey(excelFileName))
            {
                return TableMap[excelFileNameToPath[excelFileName]];
            }

            return null;
        }

        public static GameDataTable GetTableByPath(string excelFilePath)
        {
            if (TableMap.ContainsKey(excelFilePath))
            {
                return TableMap[excelFilePath];
            }

            return null;
        }

        public static bool IsStringTable(GameDataTable table)
        {
            GameDataTable stringTable = GetTableByName(StringTableName);
            if(stringTable != null)
            {
                return stringTable == table;
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
