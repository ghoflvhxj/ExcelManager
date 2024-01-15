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
        Complete
    }

    public class GameDataTable
    {
        [JsonIgnore]
        public static ConcurrentDictionary<string, GameDataTable> GameDataTableMap { get; set; }
        public static GameDataTable GetGameDataTableByPath(string inPath)
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

            Utility.Log(fileName + "로드 시작");
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

        // 엑셀에서 데이터를 복사해옵니다.
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

        protected void StringToColumnHeader(ref List<string> columnHeaderAsString, AnvilColumnHeader columnHeader, int col)
        {
            columnHeader.Name = Convert.ToString(columnHeaderAsString[(int)EColumnHeaderElement.Name]);
            columnHeader.MachineType = 0;
            columnHeader.DataType = 0;
            columnHeader.StructType = 0;
            columnHeader.ColumnIndex = col;

            //object a = new();
            //if((EMachineType)a  == EMachineType.None)
            //{

            //}

            string machineType = columnHeaderAsString[(int)EColumnHeaderElement.MachineType].ToLower();
            for (int i = 0; i < (int)EMachineType.Count; ++i)
            {
                if (Enum.GetName(typeof(EMachineType), i).ToLower() == machineType)
                {
                    columnHeader.MachineType = (EMachineType)i;
                    break;
                }
            }

            string dataType = Convert.ToString(columnHeaderAsString[(int)EColumnHeaderElement.DataType]).ToLower();
            for (int i = 0; i < (int)EDataType.Count; ++i)
            {
                if (Enum.GetName(typeof(EDataType), i).ToLower() == dataType)
                {
                    columnHeader.DataType = (EDataType)i;
                    break;
                }
            }

            string structType = Convert.ToString(columnHeaderAsString[(int)EColumnHeaderElement.StructType]).ToLower();
            for (int i = 0; i < (int)EStructType.Count; ++i)
            {
                if (Enum.GetName(typeof(EStructType), i).ToLower() == structType)
                {
                    columnHeader.StructType = (EStructType)i;
                    break;
                }
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

        static Dictionary<string, byte> enumMap = null;
        public static int rowReadCounter = 0;
        public static void MakeBinaryFiles(List<string> excelFilePath, Func<float, bool> OnLoadLatestCompleted, Func<float, bool> OnRowRead)
        {
            rowReadCounter = 0;

            if (excelFilePath.Count == 0)
            {
                return;
            }

            Thread t = new Thread(delegate ()
            {
                Utility.Log("바이너리 생성 시작", LogType.ProcessMessage);

                // 프리 프로세스
                {
                    GameDataTable enumTable = GameDataTable.GetTableByName("enum");
                    if (enumTable == null)
                    {
                        return;
                    }

                    // 데이터가 없으면 강제 로드, 있어도 최신이 아니면 로드 됨
                    if (enumTable.Load(((App)App.Current).ExcelLoader, enumTable.DataArray == null) == false)
                    {
                        Utility.Log(Utility.GetOnlyFileName(enumTable.FilePath) + " 로드에 실패해 바이너리 생성을 취소합니다", LogType.Warning);
                        return;
                    }

                    enumMap = new();
                    for (int i = (int)EColumnHeaderElement.StructType + 1; i <= enumTable.RowCount; ++i)
                    {
                        string enumName = Convert.ToString(enumTable.DataArray[i, 1]).ToLower();
                        byte enumValue = Convert.ToByte(enumTable.DataArray[i, 2]);

                        if (enumMap.ContainsKey(enumName) == false)
                        {
                            enumMap.Add(enumName, enumValue);
                        }
                    }
                }

                int progressCounter = 0;

                // 엑셀 파일을 읽는다
                List<GameDataTable> LoadedGameDataTables = new();
                for (int i=0; i<excelFilePath.Count; ++i) 
                {
                    progressCounter = i + 1;
                    if (OnLoadLatestCompleted != null)
                    {
                        App.Current.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            OnLoadLatestCompleted(progressCounter / (float)excelFilePath.Count / 2.0f);
                        }));
                    }

                    string path = excelFilePath[i];
                    GameDataTable table = GameDataTable.GetGameDataTableByPath(path);
                    if (table == null)
                    {
                        continue;
                    }

                    if(table.Load(((App)App.Current).ExcelLoader, table.DataArray == null))
                    {
                        LoadedGameDataTables.Add(table);
                    }

                    if (OnLoadLatestCompleted != null)
                    {
                        App.Current.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            OnLoadLatestCompleted(progressCounter / (float)excelFilePath.Count);
                        }));
                    }
                }

                // 바이너리로 만든다
                progressCounter = 0;
                string docPath = System.IO.Path.Combine(MainWindow.configManager.GetSectionElementValue(ConfigManager.ESectionType.ContentPath), "Doc");
                foreach (GameDataTable table in LoadedGameDataTables)
                {
                    if (table.MakeBinaryFile(docPath, enumMap))
                    {
                        if (OnRowRead != null)
                        {
                            App.Current.Dispatcher.BeginInvoke((Action)(() =>
                            {
                                OnRowRead(++progressCounter / LoadedGameDataTables.Count);
                            }));
                        }

                        Utility.Log(Utility.GetOnlyFileName(table.FilePath) + " 바이너리 생성완료", LogType.Message);
                    }
                }

                GameDataTable.SaveCacheData();

                Utility.Log("바이너리 생성 완료", LogType.ProcessMessage);
            });
            t.Start();
        }

        public bool MakeBinaryFile(string docPath, Dictionary<string, byte> enumMap)
        {
            string fileName = Utility.GetOnlyFileName(FilePath);
            string binPath = Path.Combine(docPath, "Client_" + fileName + "_Data.bin");
            string tempBinPath = Path.Combine(docPath, "Client_" + fileName + "_Data_Temp.bin");

            FileStream fs = new FileStream(tempBinPath, FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);

            // 총 칼럼 수
            UInt16 fieldCount = Convert.ToUInt16(ColumnHeaders.Count);
            bw.Write(fieldCount);

            // 각 칼럼의 정보
            foreach (var columnHeader in ColumnHeaders)
            {
                // 데이터 타입
                Byte fieldType = Convert.ToByte(columnHeader.DataType);
                bw.Write(fieldType);

                Byte[] stringBytes = System.Text.Encoding.Unicode.GetBytes(columnHeader.Name.Trim());
                bw.Write((UInt16)(stringBytes.Length));
                bw.Write(stringBytes);
            }

            // 데이터 수
            if(LastRecordIndex <= (int)EColumnHeaderElement.Count)
            {
                Utility.Log("데이터가 없는 것으로 간주되고 있습니다. " + fileName, LogType.Warning);
                return false;
            }

            UInt16 recordCount = Convert.ToUInt16(LastRecordIndex - (int)EColumnHeaderElement.Count);
            bw.Write(recordCount);

            // 데이터 검사
            string dataCheckMessage = "";
            Dictionary<int, int> IndicesMap = new Dictionary<int, int>();

            const int bufferSize = 4096;
            int seek = 0;
            Byte[] buffer = new byte[bufferSize];

            int start = (int)EColumnHeaderElement.Count + 1;
            for (int row = start; row <= LastRecordIndex; ++row)
            {
                foreach (var columnHeader in ColumnHeaders)
                {
                    object cellObject = DataArray[row, columnHeader.ColumnIndex];
                    string cellToString = Convert.ToString(cellObject).ToLower();
                    switch (columnHeader.DataType)
                    {
                        case EDataType.String:
                        case EDataType.Enum:
                            if (cellObject == null)
                            {
                                cellObject = "";
                            }
                            break;
                        default:
                            if (cellObject == null || Convert.ToString(cellObject) == "")
                            {
                                cellObject = "0";
                            }
                            break;
                    }

                    switch (columnHeader.DataType)
                    {
                        case EDataType.Int:
                        case EDataType.StringKey:
                            {
                                Int32 value = 0;

                                if (cellToString.Any(char.IsLetter))
                                {
                                    GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                }
                                else
                                {
                                    value = Convert.ToInt32(cellObject);
                                }

                                if (columnHeader.ColumnIndex == 1)
                                {
                                    if (IndicesMap.ContainsKey(value))
                                    {
                                        dataCheckMessage += IndicesMap[value] + " 행과 " + row + " 행의 인덱스가 중복되었습니다.\r\n";
                                    }
                                    else
                                    {
                                        IndicesMap.Add(value, row);
                                    }
                                }

                                byte[] bytes = BitConverter.GetBytes(value);
                                WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, ref cellObject, sizeof(Int32));
                            }
                            break;
                        case EDataType.Int64:
                            {
                                Int64 value = 0;

                                if (cellToString.Any(char.IsLetter))
                                {
                                    GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                }
                                else
                                {
                                    value = Convert.ToInt64(cellObject);
                                }

                                byte[] bytes = BitConverter.GetBytes(value);
                                WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, ref cellObject, sizeof(Int64));
                            }
                            break;
                        case EDataType.Bool:
                            {
                                bool value = false;

                                if (cellToString == "true")
                                {
                                    value = true;
                                }
                                else if (cellToString == "false")
                                {
                                    value = false;
                                }
                                else
                                {
                                    GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "false");
                                }

                                byte[] bytes = BitConverter.GetBytes(value);
                                WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, ref cellObject, sizeof(bool));
                            }
                            break;
                        case EDataType.Byte:
                            {
                                byte value = 0;

                                if (Convert.ToString(cellObject).Any(char.IsLetter))
                                {
                                    GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                }
                                else
                                {
                                    value = Convert.ToByte(cellObject);
                                }

                                byte[] bytes = BitConverter.GetBytes(value);
                                WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, ref cellObject, sizeof(byte));
                            }
                            break;
                        case EDataType.Short:
                            {
                                Int16 value = 0;

                                if (Convert.ToString(cellObject).Any(char.IsLetter))
                                {
                                    GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                }
                                else
                                {
                                    value = Convert.ToInt16(cellObject);
                                }

                                byte[] bytes = BitConverter.GetBytes(value);
                                WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, ref cellObject, sizeof(Int16));
                            }
                            break;
                        case EDataType.Float:
                            {
                                float value = 0;

                                if (value == 0 && Convert.ToString(cellObject).Any(IsLetterExclusiveDot))
                                {
                                    GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                }
                                else
                                {
                                    value = (float)Convert.ToDouble(cellObject);
                                }

                                byte[] bytes = BitConverter.GetBytes(value);
                                WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, ref cellObject, sizeof(float));
                            }
                            break;
                        case EDataType.Double:
                            {
                                double value = 0;

                                if (value == 0 && Convert.ToString(cellObject).Any(IsLetterExclusiveDot))
                                {
                                    GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                }
                                else
                                {
                                    value = Convert.ToDouble(cellObject);
                                }

                                byte[] bytes = BitConverter.GetBytes(value);
                                WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, ref cellObject, sizeof(double));
                            }
                            break;
                        case EDataType.Enum:
                            {
                                byte value = 0;
                                string enumName = cellToString;
                                if (enumName != "")
                                {
                                    string enumClass = Convert.ToString(DataArray[(int)EColumnHeaderElement.StructType + 1, columnHeader.ColumnIndex]).ToLower();
                                    string key = enumClass.Trim() + "_" + enumName.Trim();

                                    if (enumMap.ContainsKey(key))
                                    {
                                        value = enumMap[key];
                                    }
                                    else
                                    {
                                        dataCheckMessage += "[" + row + ", " + columnHeader.Name + "] 의 " + key + "는 없는 enum입니다.\r\n";
                                    }
                                }

                                byte[] bytes = BitConverter.GetBytes(value);
                                WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, ref cellObject, sizeof(byte));
                            }
                            break;
                        case EDataType.String:
                            {
                                Byte[] stringBytes = System.Text.Encoding.Unicode.GetBytes(Convert.ToString(cellObject));
                                Byte[] lengthBytes = BitConverter.GetBytes(Convert.ToUInt16(stringBytes.Length));

                                int nextSeek = seek + lengthBytes.Length + stringBytes.Length;
                                if (nextSeek < bufferSize)
                                {
                                    Buffer.BlockCopy(lengthBytes, 0, buffer, seek, lengthBytes.Length);
                                    Buffer.BlockCopy(stringBytes, 0, buffer, seek + 2, stringBytes.Length);

                                    seek = nextSeek;
                                }
                                else
                                {
                                    bw.Write(buffer, 0, seek);

                                    seek = 0;

                                    Buffer.BlockCopy(lengthBytes, 0, buffer, seek, lengthBytes.Length);
                                    Buffer.BlockCopy(stringBytes, 0, buffer, seek + 2, stringBytes.Length);

                                    seek = lengthBytes.Length + stringBytes.Length;
                                }
                            }
                            break;
                    }
                }
            }

            bool bMakeBinarySuccess = dataCheckMessage == "";
            if (bMakeBinarySuccess)
            {
                bw.Write(buffer, 0, seek);
                bw.Close();

                if (File.Exists(binPath))
                {
                    File.Delete(binPath);
                }
                File.Move(tempBinPath, binPath);
            }
            else
            {
                bw.Close();

                if (File.Exists(tempBinPath))
                {
                    File.Delete(tempBinPath);
                }

                Utility.Log(dataCheckMessage, LogType.Warning);
            }

            return bMakeBinarySuccess;
        }

        public bool IsTableChanged(DateTime LastWriteTime)
        {
            return LastWriteTime != CachedLastWriteTime;
        }

        private void GetDataCheckMessage(ref string data, ref string dataCheckMessage, AnvilColumnHeader columnHeader, int row, string defaultValue)
        {
            dataCheckMessage += "[" + row + ", " + columnHeader.Name + "] 의 " + " 데이터(" + data + ")와 타입(" + Enum.GetName(typeof(EDataType), columnHeader.DataType) + ")이 다릅니다.\r\n";
        }

        private void WriteBytes(ref BinaryWriter binaryWriter, ref byte[] buffer, ref byte[] data, ref int seek, int bufferSize, ref object cellObject, int sizeOfType)
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

        private bool IsLetterExclusiveDot(char c)
        {
            return c != '.' && char.IsLetter(c);
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

        protected bool IsContainForeignKeyToken(string str)
        {
            return str[0] == '@';
        }

        private string GetCommentColumnName()
        {
            return "comment";
        }

        public bool IsIndexColumn(string colunmName)
        {
            return ColumnNameToColumnHeader[colunmName] == IndexColumn; 
        }

        public void FixResourceData()
        {
            Load(((App)App.Current).ExcelLoader, DataArray == null);

            const int maxWorkers = 4;
            ThreadPool.SetMinThreads(1, 1);
            ThreadPool.SetMaxThreads(maxWorkers, maxWorkers);

            List<EventWaitHandle> threadEvents = new List<EventWaitHandle>(ResourceColums.Count);
            foreach (var pair in ResourceColums)
            {
                EResourcePathType resourcePathType = pair.Key;
                int col = pair.Value.ColumnIndex;

                // 멀티스레드지만 스레드가 동일한 원소에 접근하지 않아 내부에서 락을 안잡아도 됨
                ThreadPool.QueueUserWorkItem(CheckColumnData, new ResourceCheckInfo() { ColumnIndex = col, ColumnName = pair.Value.Name, ExcelPath = FilePath, resourcePathType = pair.Key, RowCount = RowCount });
            }
        }

        enum ECheckResult { InvalidDirectoryName, InvalidFileName, NotExistDirectory, NotExistFile, Count };
        public class ResourceCheckInfo
        { 
            public string ExcelPath { get; set; }
            public EResourcePathType resourcePathType { get; set; }
            public int ColumnIndex { get; set; }
            public string ColumnName { get; set; }
            public int RowCount { get; set; }
        }


        private void CheckColumnData(object obj)
        {
            ResourceCheckInfo resourceCheckInfo = obj as ResourceCheckInfo;
            if(resourceCheckInfo == null)
            {
                return;
            }

            EResourcePathType resourcePathType = resourceCheckInfo.resourcePathType;
            string colName = resourceCheckInfo.ColumnName;
            string excelPath = resourceCheckInfo.ExcelPath;
            int rowCount = resourceCheckInfo.RowCount;
            int columnIndex = resourceCheckInfo.ColumnIndex;

            for (int row = (int)EColumnHeaderElement.Count + 1; row <= rowCount; ++row)
            {
                object cellObject = GameDataTable.GameDataTableMap[excelPath].DataArray[row, columnIndex];
                if (cellObject == null)
                {
                    continue;
                }

                string originCellValue = cellObject.ToString();
                switch (resourcePathType)
                {
                    case EResourcePathType.FileName:
                        {
                            string fileName = originCellValue;
                            if (MainWindow.allFileName.ContainsKey(fileName) == false)
                            {
                                string message = GetRowColumnString(excelPath, row, colName, fileName);
                                string fileNameAsKey = Utility.NameAsKey(fileName);
                                if (MainWindow.allFileNameAsKey.ContainsKey(fileNameAsKey))
                                {
                                    message += GetCheckResultAsMessage(ECheckResult.InvalidFileName, fileName, fileNameAsKey);
                                    GameDataTable.GameDataTableMap[excelPath].DataArray[row, columnIndex] = MainWindow.allFileNameAsKey[fileNameAsKey];
                                }
                                else
                                {
                                    message += GetCheckResultAsMessage(ECheckResult.NotExistFile, fileName);
                                }
                            }
                        }
                        break;
                    case EResourcePathType.Path:
                        {
                            string resourcePath = originCellValue;
                            string fileName = Path.GetFileName(resourcePath);
                            if (resourcePath[0] == '/')
                            {
                                resourcePath = resourcePath.Substring(1);
                            }

                            string[] directoryNames = Path.GetDirectoryName(resourcePath).Split('\\');
                            if (directoryNames.Length > 0)
                            {
                                int offset = 0;
                                if (directoryNames[0] == "Game")
                                {
                                    ++offset;
                                }

                                string message = "";
                                // 폴더 이름 검사
                                for (int i = directoryNames.Length - 1; i >= offset; --i)
                                {
                                    if (MainWindow.allDirectoryName.ContainsKey(directoryNames[i]))
                                    {
                                        continue;
                                    }

                                    message = (message.Length != 0) ? message : GetRowColumnString(excelPath, row, colName, resourcePath);

                                    string directoryNameAsKey = Utility.NameAsKey(directoryNames[i]);
                                    if (MainWindow.allDirectoryActualNames.ContainsKey(directoryNameAsKey))
                                    {
                                        if (i > 1 && MainWindow.allDirectoryParentNames[directoryNameAsKey].Contains(Utility.NameAsKey(directoryNames[i - 1])))
                                        {
                                            message += GetCheckResultAsMessage(ECheckResult.InvalidDirectoryName, directoryNames[i], directoryNameAsKey);
                                            string onlyDirectroy = originCellValue.Substring(0, originCellValue.LastIndexOf('/') + 1);
                                            GameDataTable.GameDataTableMap[excelPath].DataArray[row, columnIndex] = originCellValue = onlyDirectroy.Replace(directoryNames[i], MainWindow.allDirectoryActualNames[directoryNameAsKey]) + fileName;
                                        }
                                        else
                                        {
                                            message += GetCheckResultAsMessage(ECheckResult.NotExistDirectory, directoryNames[i]);
                                        }
                                    }
                                    else
                                    {
                                        message += GetCheckResultAsMessage(ECheckResult.NotExistDirectory, directoryNames[i]);
                                    }
                                }
                                // 파일 이름 검사
                                string fileNameAsKey = Utility.NameAsKey(fileName);
                                bool bWrongLetter = MainWindow.allFileName.ContainsKey(fileName) == false;
                                bool bExist = MainWindow.allFileNameAsKey.ContainsKey(fileNameAsKey);
                                if (bWrongLetter && bExist)
                                {
                                    message = (message.Length != 0) ? message : GetRowColumnString(excelPath, row, colName, resourcePath);
                                    message += GetCheckResultAsMessage(ECheckResult.InvalidFileName, fileName, fileNameAsKey);

                                    string onlyDirectroy = originCellValue.Substring(0, originCellValue.LastIndexOf('/') + 1);
                                    GameDataTable.GameDataTableMap[excelPath].DataArray[row, columnIndex] = onlyDirectroy + MainWindow.allFileNameAsKey[fileNameAsKey];
                                }
                                else if (bExist == false)
                                {
                                    message = (message.Length != 0) ? message : GetRowColumnString(excelPath, row, colName, resourcePath);
                                    message += GetCheckResultAsMessage(ECheckResult.NotExistFile, fileName);
                                }

                                if (message.Length > 0)
                                {
                                    Utility.Log(message, LogType.Warning);
                                }
                            }
                        }
                        break;
                }
            }
        }

        private string GetRowColumnString(string excelPathFileName, int row, int col, string data)
        {
            return Path.GetFileName(excelPathFileName) + "[" + (row + (int)EColumnHeaderElement.Count) + "," + col + "]: " + data + "\r\n";
        }

        private string GetRowColumnString(string excelPathFileName, int row, string colName, string data)
        {
            return Path.GetFileName(excelPathFileName) + "[" + row + "," + colName + "]: " + data + "\r\n";
        }

        private string GetModifedMessage()
        {
            return "└ 수정되었습니다. \r\n";
        }

        private string GetCheckResultAsMessage(ECheckResult result, string str, string strAsKey = "")
        {
            string message = "";
            switch (result)
            {
                case ECheckResult.InvalidDirectoryName:
                    message = " 제안사항: 폴더의 대소문자 점검, " + "엑셀에 적힌 폴더이름:" + str + " ≠ " + "실제 폴더이름:" + MainWindow.allDirectoryActualNames[strAsKey];
                    break;
                case ECheckResult.InvalidFileName:
                    message = " 제안사항: 파일의 대소문자 점검, " + "엑셀에 적힌 파일이름:" + str + " ≠ " + "실제 파일이름:" + MainWindow.allFileNameAsKey[strAsKey];
                    break;
                case ECheckResult.NotExistDirectory:
                    message = " 존재하지 않는 디렉토리 입니다 " + str;
                    break;
                case ECheckResult.NotExistFile:
                    message = " 존재하지 않는 파일입니다 " + str;
                    break;
            }

            return message;
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
            });
            loadExcelThread.Start();
        }

        public static void ResetGameDataTableMap<T>()
            where T : GameDataTable, new()
        {
            GameDataTable.GameDataTableMap = new();
            foreach (var excelPath in MExcel.excelPaths)
            {
                T newTable = new();
                GameDataTableMap.TryAdd(excelPath, newTable);
            }
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

        public static async void SaveCacheData()
        {
            FileStream fileStream = File.Create(ConfigUtility.CachedDataPath);
            await JsonSerializer.SerializeAsync(fileStream, GameDataTableMap);
            await fileStream.DisposeAsync();
        }

        public static void LoadCachedData<T>()
            where T : GameDataTable, new()
        {
            string filePath = ConfigUtility.CachedDataPath;
            if (File.Exists(filePath))
            {
                string jsonString = File.ReadAllText(filePath);
                if (jsonString == "")
                {
                    Utility.Log(filePath + " 데이터를 읽지 못했습니다.", LogType.Warning);
                    return;
                }

                Utility.Log("파일을 읽습니다 경로: " + Path.GetFullPath(filePath));
                var CachedGameDataTableMap = JsonSerializer.Deserialize<ConcurrentDictionary<string, T>>(jsonString);
                foreach(var pair in CachedGameDataTableMap)
                {
                    if (GameDataTableMap.ContainsKey(pair.Key) == false)
                    {
                        Utility.Log("캐시 데이터에 저장된 테이블이 현재 존재하지 않습니다.\n" + pair.Key, LogType.Warning);
                    }
                    GameDataTableMap[pair.Key] = pair.Value;
                }
            }
        }

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
