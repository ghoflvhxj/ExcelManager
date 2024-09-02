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
using System.Threading;
using Excel = Microsoft.Office.Interop.Excel;

namespace TestWPF
{
    public enum EGameDataTableLoadState
    {
        Wait,
        Loading,
        Failed,
        Complete
    }

    public class GameDataTable
    {
        [JsonIgnore]
        public static ConcurrentDictionary<string, GameDataTable> GameDataTableMap { get; set; }
        public static GameDataTable GetTableByPath(string inPath)
        {
            return GameDataTableMap.ContainsKey(inPath) ? GameDataTableMap[inPath] : null;
        }

        [JsonIgnore]
        private const int Index = 1;

        // 파일 정보
        public DateTime CachedLastWriteTime { get; set; }
        public string FilePath { get; set; }

        // 테이블 데이터
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public int LastRecordIndex { get; set; }
        [JsonIgnore]
        public object[,] DataArray { get; set; }

        // 테이블 정보
        public List<AnvilColumnHeader> ColumnHeaders { get; set; }
        public List<KeyValuePair<EResourcePathType, AnvilColumnHeader>> ResourceColums { get; set; }
        public Dictionary<int, int> RecordIndexToDataArrayIndex { get; set; }
        public Dictionary<string, ForeignKeyInfo> ForeignKeyInfoMap { get; set; }
        public List<AnvilColumnHeader> CommentColumns { get; set; }
        public AnvilColumnHeader IndexColumn { get; set; }

        // 메타
        [JsonIgnore]
        public HashSet<string> ReferencedTableNames { get; set; }
        [JsonIgnore]
        public Dictionary<string, AnvilColumnHeader> ColumnNameToColumnHeader;

        [JsonIgnore]
        public bool Modified { get; set; }

        [JsonIgnore]
        public EGameDataTableLoadState LoadState { get; set; }

        public delegate void FOnLoadStateChangedDelegate(EGameDataTableLoadState NewState);
        public FOnLoadStateChangedDelegate OnLoadStateChanged;

        public GameDataTable()
        {
            Reset();
            ChangeLoadState(EGameDataTableLoadState.Wait);
        }

        public void Reset()
        {
            ColumnHeaders = new();
            ResourceColums = new();
            CommentColumns = new();
            IndexColumn = new();
            ForeignKeyInfoMap = new();
            ResetMetaInfo();

            LoadState = EGameDataTableLoadState.Wait;
        }
        
        public void ResetMetaInfo()
        {
            ReferencedTableNames = new HashSet<string>();
            ColumnNameToColumnHeader = new Dictionary<string, AnvilColumnHeader>(StringComparer.OrdinalIgnoreCase);
        }

        public void GetLastWriteTime(out DateTime outLastWriteTime)
        {
            outLastWriteTime = DateTime.MinValue;

            if (File.Exists(FilePath) == false)
            {
                Utility.Log(FilePath + " 존재하지 않는 파일입니다", LogType.Warning);
                return;
            }

            FileInfo fileInfo = new FileInfo(FilePath);
            outLastWriteTime = fileInfo.LastWriteTime;
        }

        //public bool Load(MExcel mExcel, string excelFilePath, bool bForce = false)
        //{
        //    Utility.Log(excelFilePath + "로드 시작");
        //    if (File.Exists(excelFilePath) == false)
        //    {
        //        Utility.Log(excelFilePath + " 로드 실패", LogType.Warning);
        //        return false;
        //    }

        //    FilePath = excelFilePath;
        //    DateTime lastWriteTime = DateTime.MinValue;
        //    GetLastWriteTime(out lastWriteTime);
        //    if (lastWriteTime != CachedLastWriteTime || bForce)
        //    {
        //        if (LoadGameDataTable(mExcel))
        //        {
        //            Utility.Log(excelFilePath + " 데이터 읽음"); 
        //            CachedLastWriteTime = lastWriteTime;
        //        }
        //    }

        //    Utility.Log(excelFilePath + " 로드 완료", LogType.Message);
        //    return true;
        //}

        public bool Load(MExcel mExcel, bool bForce = false)
        {
            string fileName = Utility.GetOnlyFileName(FilePath);

            Utility.Log(fileName + " 로드 시작");
            if (File.Exists(FilePath) == false)
            {
                Utility.Log(fileName + " 로드 실패", LogType.Warning);
                return false;
            }

            DateTime lastWriteTime = DateTime.MinValue;
            GetLastWriteTime(out lastWriteTime);

            if (lastWriteTime != CachedLastWriteTime || bForce)
            {
                ChangeLoadState(EGameDataTableLoadState.Loading);
                if (LoadGameDataTable(mExcel))
                {
                    Utility.Log(fileName + " 데이터 읽음");
                    CachedLastWriteTime = lastWriteTime;
                }
                else
                {
                    Utility.Log(fileName + " 열기 실패", LogType.Warning);
                    ChangeLoadState(EGameDataTableLoadState.Failed);
                    return false;
                }
            }
            else
            {
                Utility.Log(fileName + " 캐시 로드 완료");
            }

            ChangeLoadState(EGameDataTableLoadState.Complete);
            Utility.Log(fileName + " 로드 완료", LogType.Message);
            return true;
        }

        public void ChangeLoadState(EGameDataTableLoadState NewLoadState)
        {
            LoadState = NewLoadState;
            if (OnLoadStateChanged != null)
            {
                OnLoadStateChanged(NewLoadState);
            }
        }

        public bool LoadGameDataTable(MExcel mExcel)
        {
            Excel.Workbook workBook = null;
            Excel.Worksheet workSheet = null;

            if (mExcel.GetWorkBookAndSheetFromGameDataTable(FilePath, out workBook, out workSheet, true))
            {
                Excel.Range range = workSheet.UsedRange;

                CopyDataFromWorkSheet(range);
                MakeInfo(range);
                workBook.Close();
                Marshal.ReleaseComObject(workBook);
                Marshal.ReleaseComObject(workSheet);
                return true;
            }

            return false;
        }

        public void SaveGameDataTable(MExcel mExcel)
        {
            Excel.Workbook workBook = null;
            Excel.Worksheet workSheet = null;

            if (mExcel.GetWorkBookAndSheetFromGameDataTable(FilePath, out workBook, out workSheet, false))
            {
                workSheet.Range["A1", Utility.ConvetToExcelColumn(ColumnCount) + RowCount].Value2 = DataArray;
                workBook.Save();
                workBook.Close();

                Modified = false;
            }
        }

        public virtual void CopyDataFromWorkSheet(Excel.Range range)
        {
            DataArray = (object[,])range.Value2;
        }

        public virtual void MakeInfo(Excel.Range range)
        {
            if (DataArray == null)
            {
                return;
            }

            RowCount = range.Rows.Count;
            ColumnCount = range.Columns.Count;
            LastRecordIndex = Convert.ToInt32(range.get_End(Excel.XlDirection.xlDown).Row);

            MakeColumnHeaders();
            ExtractResourceColum();
            PostInitInfo();
        }

        // 칼럼을 분석해 정보를 만듭니다.
        public virtual void MakeColumnHeaders()
        {

        }

        public void PostInitInfo()
        {
            ResetMetaInfo();

            foreach (var columnHeader in ColumnHeaders)
            {
                if (ForeignKeyInfoMap.ContainsKey(columnHeader.Name))
                {
                    string referencedTableName = ForeignKeyInfoMap[columnHeader.Name].ReferencedTableName;
                    //Utility.FindOrAdd(ForeignColumnMap, referencedTableName).Add(columnHeader);
                    ReferencedTableNames.Add(referencedTableName);
                }

                ColumnNameToColumnHeader.Add(columnHeader.Name, columnHeader);
            }
        }

        public void ExtractResourceColum()
        {
            ResourceColums.Clear();

            string[] Head = { "", "client", "string" };
            foreach (var columnHeader in ColumnHeaders)
            {
                int col = columnHeader.ColumnIndex;

                // 헤더 검사
                if (columnHeader.MachineType != EMachineType.Server &&
                    columnHeader.DataType == EDataType.String &&
                    columnHeader.StructType == EStructType.None)
                {
                    // 데이터 검사, 첫번째 데이터가 /, _를 가지고 있는지 확인함
                    bool bResourceString = true;
                    EResourcePathType resourcePathType = EResourcePathType.Path;

                    for (int row = 5; row < RowCount; ++row)
                    {
                        if (DataArray[row, col] == null)
                        {
                            continue;
                        }

                        string cellValue = DataArray[row, col].ToString();
                        if(cellValue == "")
                        {
                            continue;
                        }

                        bool bHasSlash = cellValue[0] == '/';
                        if (bHasSlash == false)
                        {
                            resourcePathType = EResourcePathType.FileName;
                            bool bHasUnderbar = cellValue.IndexOf('_') != -1;
                            if (bHasUnderbar == false)
                            {
                                bResourceString = false;
                            }
                        }

                        break;
                    }

                    if (bResourceString)
                    {
                        ResourceColums.Add(new KeyValuePair<EResourcePathType, AnvilColumnHeader>(resourcePathType, columnHeader));
                    }
                }
                else
                {
                    continue;
                }
            }
        }

        public bool IsTableChanged(DateTime LastWriteTime)
        {
            return LastWriteTime != CachedLastWriteTime;
        }

        
        protected void WriteBytes(ref BinaryWriter binaryWriter, ref byte[] buffer, ref byte[] data, ref int seek, int bufferSize, int sizeOfType)
        {
            int nextSeek = seek + sizeOfType;
            if (nextSeek < bufferSize)
            {
                Buffer.BlockCopy(data, 0, buffer, seek, sizeOfType);

                seek = nextSeek;
            }
            else
            {
                binaryWriter.Write(buffer, 0, seek);

                seek = 0;

                Buffer.BlockCopy(data, 0, buffer, seek, sizeOfType);

                seek = sizeOfType;
            }
        }

        protected bool IsInValidInteger(char c)
        {
            return !char.IsDigit(c);
        }

        protected bool IsInValidFloat(char c)
        {
            return !(c == '.' || char.IsDigit(c));
        }

        public bool IsValidColumnName(string columnName)
        {
            return ColumnNameToColumnHeader.ContainsKey(columnName);
        }

        public bool IsValidForeignColumnName(string columnName)
        {
            return IsValidColumnName(columnName) && ForeignKeyInfoMap.ContainsKey(columnName);
        }

        protected bool IsInvalidColumn(List<string> columnHeaderAsString)
        {
            if(columnHeaderAsString[(int)EColumnHeaderElement.Name] == "")
            {
                return true;
            }

            return false;
        }

        public bool IsIndexColumn(string colunmName)
        {
            return ColumnNameToColumnHeader[colunmName] == IndexColumn; 
        }

        public enum ECheckResult { InvalidDirectoryName, InvalidFileName, NotExistDirectory, NotExistFile, Count };
        public class ResourceCheckInfo
        { 
            public string ExcelPath { get; set; }
            public EResourcePathType resourcePathType { get; set; }
            public int ColumnIndex { get; set; }
            public string ColumnName { get; set; }
            public int RowCount { get; set; }
        }

        public static Thread loadExcelThread;
        public static void LoadGameDataTables()
        {
            loadExcelThread = new Thread(delegate ()
            {
                Utility.Log("테이블 데이터 불러오기 시작", LogType.ProcessMessage);
                foreach (var excelPath in MExcel.excelPaths)
                {
                    GameDataTable.LoadGameDataTable(excelPath);
                }

                Utility.Log("테이블 테이블 불러오기 완료", LogType.ProcessMessage);
                SaveCacheData();
            });
            loadExcelThread.Start();
        }

        public static bool ResetGameDataTableMap()
        {
            if(WorkSpace.CurrentTableType == null)
            {
                Utility.Log("데이터 테이블 타입이 잘못되었습니다", LogType.Warning);
                return false;
            }

            GameDataTable.GameDataTableMap = new();
            foreach (var excelPath in MExcel.excelPaths)
            {
                GameDataTable newTable = (GameDataTable)Activator.CreateInstance(WorkSpace.CurrentTableType);
                GameDataTableMap.TryAdd(excelPath, newTable);
            }

            return true;
        }

        public static bool LoadGameDataTable(string path)
        {
            if(GameDataTableMap.ContainsKey(path))
            {
                GameDataTable gameDataTable = GameDataTableMap[path];
                gameDataTable.FilePath = path;
                gameDataTable.Load(((App)App.Current).ExcelLoader);
                return true;
            }

            return false;
        }

        [JsonIgnore]
        public static string CacheDataPath { get { return Path.Combine(GlobalValue.dataDirectory, "CacheData", WorkSpace.Current.ProjectName + ".json"); } }

        public static void SaveCacheData()
        {
            Type t = GetDictionaryType();
            var a = Activator.CreateInstance(t);
            var addMethod = t.GetMethod("TryAdd", new[] { typeof(string), GameDataTableMap.Last().Value.GetType() });
            foreach (var pair in GameDataTableMap)
            {
                //GameDataTableMap[pair.Key] = pair.Value;
                addMethod.Invoke(a, new object[] { pair.Key, pair.Value });
            }

            Utility.AsyncJsonSerialize(CacheDataPath, a);
            Utility.Log("캐시 데이터 저장 완료");
        }

        public static void LoadCacheData()
        {
            if (File.Exists(CacheDataPath))
            {
                string jsonString = File.ReadAllText(CacheDataPath);
                if (jsonString == "")
                {
                    Utility.Log("캐시 데이터를 불러오지 못했습니다", LogType.Warning);
                    return;
                }

                Utility.Log("캐시 데이터를 불러옵니다\n경로: "+ Path.GetFullPath(CacheDataPath));

                dynamic cachedDataTableMap = JsonSerializer.Deserialize(jsonString, GetDictionaryType());

                foreach (var pair in cachedDataTableMap)
                {
                    if (GameDataTableMap.ContainsKey(pair.Key) == false)
                    {
                        Utility.Log("캐시된 테이블이 현재 존재하지 않습니다.\n" + pair.Key, LogType.Warning);
                        continue;
                    }

                    Func<string, GameDataTable, GameDataTable> p = delegate (string k, GameDataTable v)
                    {
                        return pair.Value;
                    };

                    GameDataTableMap.AddOrUpdate(pair.Key, pair.Value, p);
                }
            }
        }

        public static Type GetDictionaryType()
        {
            Type t = typeof(ConcurrentDictionary<,>);
            Type genericType = t.MakeGenericType(new Type[] { typeof(string), GameDataTableMap.Last().Value.GetType() });

            return genericType;
        }

        //public static ConcurrentDictionary<string, T> CreateDictionary(T value)
        //{
        //    return new();
        //}

        public static GameDataTable GetTableByName(string excelFileName)
        {
            if (MExcel.excelFileNameToPath.ContainsKey(excelFileName))
            {
                return GameDataTableMap[MExcel.excelFileNameToPath[excelFileName]];
            }

            return null;
        }
    }
}
