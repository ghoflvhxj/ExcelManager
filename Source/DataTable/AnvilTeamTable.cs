using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Excel = Microsoft.Office.Interop.Excel;

namespace TestWPF
{
    class AnvilTeamTable : GameDataTable
    {
        virtual protected bool IsContainForeignKeyToken(List<string> columnHeaderString)
        {
            return false;
        }

        private string GetCommentColumnName()
        {
            return "comment";
        }

        public string CommentColumnName { get { return "comment"; }  }
        public string IndexColumnName { get { return "index";  } }

        public override void MakeColumnHeaders()
        {
            // 칼럼 분석
            List<AnvilColumnHeader> newColumnHeaders = new();
            for (int col = 1; col <= ColumnCount; ++col)
            {
                List<string> columnHeaderAsString = new((int)EColumnHeaderElement.Count);
                for (int row = 0; row < (int)EColumnHeaderElement.Count; ++row)
                {
                    object cellObject = DataArray[row + 1, col];
                    columnHeaderAsString.Add(cellObject != null ? cellObject.ToString() : "");
                }

                if (IsInvalidColumn(columnHeaderAsString))
                {
                    break;
                }

                AnvilColumnHeader newColumnHeader = new();
                StringToColumnHeader(columnHeaderAsString, newColumnHeader, col);

                bool bUsedColumn = newColumnHeader.MachineType != EMachineType.None;
                if (bUsedColumn)
                {
                    if (IsContainForeignKeyToken(columnHeaderAsString))
                    {
                        //string columnName = columnHeaderAsString[(int)EColumnHeaderElement.Name].Remove(0, 1);
                        //int underbarIndex = columnName.LastIndexOf('_');
                        //string tableName = columnName;
                        //if (underbarIndex != -1)
                        //{
                        //    tableName = columnName.Substring(0, underbarIndex);
                        //}

                        //bool bFoundRealTable = false;
                        //string realTableName = tableName;
                        //foreach (string excelFileName in MExcel.excelFileNames)
                        //{
                        //    if ((tableName == excelFileName) || (tableName.Contains(excelFileName) && !bFoundRealTable) || (excelFileName.Contains(tableName) && !bFoundRealTable))
                        //    {
                        //        bFoundRealTable = true;
                        //        realTableName = excelFileName;
                        //        break;
                        //    }
                        //}

                        //if (ForeignKeyInfoMap.ContainsKey(newColumnHeader.Name) == false)
                        //{
                        //    ForeignKeyInfoMap.Add(newColumnHeader.Name, new ForeignKeyInfo()
                        //    {
                        //        ReferencedTableName = realTableName,
                        //        ForeignKeyName = "index"
                        //    });
                        //}

                        //newColumnHeader.ReferencedTableName = realTableName;
                        //newColumnHeader.ForeignKeyName = "index";
                    }

                    newColumnHeaders.Add(newColumnHeader);

                    if (newColumnHeader.Name.ToLower() == IndexColumnName)
                    {
                        IndexColumn = newColumnHeader;
                    }
                }
                else
                {
                    if (newColumnHeader.Name.ToLower().Contains(CommentColumnName))
                    {
                        CommentColumns.Add(newColumnHeader);
                    }
                }
            }

            //// 레코드 인덱스와 배열 인덱스 바인딩
            //Dictionary<int, int> newIndexToDataArrayRow = new Dictionary<int, int>();
            //for (int row = (int)EColumnHeaderElement.Count + 1; row <= RowCount; ++row)
            //{
            //    if (IndexColumn.ColumnIndex == 0)
            //    {
            //        continue;
            //    }

            //    if (DataArray[row, IndexColumn.ColumnIndex] == null)
            //    {
            //        continue;
            //    }

            //    newIndexToDataArrayRow.TryAdd(Convert.ToInt32(DataArray[row, IndexColumn.ColumnIndex]), row);
            //}
            //RecordIndexToDataArrayIndex = newIndexToDataArrayRow;

            ColumnHeaders = newColumnHeaders;
        }

        public override void CopyDataFromWorkSheet(Excel.Range range)
        {
            base.CopyDataFromWorkSheet(range);

            object o = DataArray[(int)EColumnHeaderElement.StructType + 1, 1];
            if (o == null || Convert.ToString(o) == "")
            {
                DataArray[(int)EColumnHeaderElement.StructType + 1, 1] = range.Cells[(int)EColumnHeaderElement.StructType + 1, 1] = "none";
            }
        }

        protected void StringToColumnHeader(List<string> columnHeaderAsString, AnvilColumnHeader columnHeader, int col)
        {
            columnHeader.Name = Convert.ToString(columnHeaderAsString[(int)EColumnHeaderElement.Name]);
            columnHeader.MachineType = default;
            columnHeader.DataType = default;
            columnHeader.StructType = default;
            columnHeader.ColumnIndex = col;

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

            // 배열이라면 string으로 오버라이딩
            if (columnHeader.StructType == EStructType.Array)
            {
                columnHeader.DataType = EDataType.String;
            }
        }

        private static Dictionary<string, byte> enumMap;
        private static int rowReadCounter;
        public static void MakeBinaryFiles(List<string> excelFilePath/*, Func<float, bool> OnLoadLatestCompleted, Func<float, bool> OnRowRead*/)
        {
            rowReadCounter = 0;

            if (excelFilePath.Count == 0)
            {
                return;
            }

            string docPath = System.IO.Path.Combine(WorkSpace.Current.ContentPath, "Doc");
            if (System.IO.Directory.Exists(docPath) == false)
            {
                return;
            }


            Thread t = new Thread(delegate ()
            {
                Utility.Log("바이너리 생성 시작", LogType.ProcessMessage);

                // 이넘 읽기
                {
                    AnvilTeamTable enumTable = (AnvilTeamTable)GameDataTable.GetTableByName("enum");

                    // 데이터가 없으면 강제 로드, 있어도 최신이 아니면 로드 됨
                    if (enumTable == null || enumTable.Load(((App)App.Current).ExcelLoader, enumTable.DataArray == null) == false)
                    {
                        Utility.Log("Enum 테이블 로드에 실패해 바이너리 생성을 취소합니다", LogType.Warning);
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
                List<AnvilTeamTable> LoadedGameDataTables = new();
                for (int i = 0; i < excelFilePath.Count; ++i)
                {
                    progressCounter = i + 1;
                    //if (OnLoadLatestCompleted != null)
                    //{
                    //    App.Current.Dispatcher.BeginInvoke((Action)(() =>
                    //    {
                    //        OnLoadLatestCompleted(progressCounter / (float)excelFilePath.Count / 2.0f);
                    //    }));
                    //}

                    string path = excelFilePath[i];
                    AnvilTeamTable table = (AnvilTeamTable)GameDataTable.GetTableByPath(path);
                    if (table == null)
                    {
                        continue;
                    }

                    if (table.Load(((App)App.Current).ExcelLoader, table.DataArray == null))
                    {
                        LoadedGameDataTables.Add(table);
                    }

                    //if (OnLoadLatestCompleted != null)
                    //{
                    //    App.Current.Dispatcher.BeginInvoke((Action)(() =>
                    //    {
                    //        OnLoadLatestCompleted(progressCounter / (float)excelFilePath.Count);
                    //    }));
                    //}
                }

                // 바이너리로 만든다
                progressCounter = 0;
                foreach (AnvilTeamTable table in LoadedGameDataTables)
                {
                    if (table.MakeBinary(docPath, enumMap))
                    {
                        //if (OnRowRead != null)
                        //{
                        //    App.Current.Dispatcher.BeginInvoke((Action)(() =>
                        //    {
                        //        OnRowRead(++progressCounter / LoadedGameDataTables.Count);
                        //    }));
                        //}

                        Utility.Log(Utility.GetOnlyFileName(table.FilePath) + " 바이너리 생성완료", LogType.Message);
                    }
                }

                GameDataTable.SaveCacheData();

                Utility.Log("바이너리 생성 완료", LogType.ProcessMessage);
            });
            t.Start();
        }

        public bool MakeBinary(string docPath, Dictionary<string, byte> enumMap)
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
            if (LastRecordIndex <= (int)EColumnHeaderElement.Count)
            {
                Utility.Log("데이터가 없는 것으로 간주되고 있습니다. " + fileName, LogType.Warning);
                bw.Close();
                return false;
            }

            UInt16 recordCount = Convert.ToUInt16(LastRecordIndex - (int)EColumnHeaderElement.Count);
            bw.Write(recordCount);

            // 데이터 검사
            string dataCheckMessage = "";
            Dictionary<string, int> IndicesMap = new();

            const int bufferSize = 4096;
            int seek = 0;
            Byte[] buffer = new byte[bufferSize];

            int start = (int)EColumnHeaderElement.Count + 1;
            for (int row = start; row <= LastRecordIndex; ++row)
            {
                foreach (var columnHeader in ColumnHeaders)
                {
                    object cellObject = DataArray[row, columnHeader.ColumnIndex];
                    
                    // 비어있는 경우 디폴트 값 세팅
                    switch (columnHeader.DataType)
                    {
                        case EDataType.String:
                        case EDataType.Enum:
                            cellObject = cellObject == null ? "" : cellObject;
                            break;
                        default:
                            cellObject = cellObject == null || Convert.ToString(cellObject) == "" ? "0" : cellObject;
                            break;
                    }

                    List<string> actualCellObjectList = new();
                    actualCellObjectList.Add(Convert.ToString(cellObject));

                    for (int i=0; i< actualCellObjectList.Count; ++i)
                    {
                        string cellToString = actualCellObjectList[i];

                        switch (columnHeader.DataType)
                        {
                            case EDataType.Int:
                            case EDataType.StringKey:
                                {
                                    Int32 value = 0;

                                    if (cellToString.Any(IsInValidInteger))
                                    {
                                        GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                    }
                                    else
                                    {
                                        value = Convert.ToInt32(cellToString);
                                    }

                                    bool bIsIndexColumn = columnHeader.ColumnIndex == IndexColumn.ColumnIndex;
                                    if (bIsIndexColumn)
                                    {
                                        if (IndicesMap.ContainsKey(cellToString))
                                        {
                                            dataCheckMessage += IndicesMap[cellToString] + " 행과 " + row + " 행의 인덱스가 중복되었습니다.\r\n";
                                        }
                                        else
                                        {
                                            IndicesMap.Add(cellToString, row);
                                        }
                                    }

                                    byte[] bytes = BitConverter.GetBytes(value);
                                    WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, sizeof(Int32));
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
                                        value = Convert.ToInt64(cellToString);
                                    }

                                    byte[] bytes = BitConverter.GetBytes(value);
                                    WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, sizeof(Int64));
                                }
                                break;
                            case EDataType.Bool:
                                {
                                    cellToString = cellToString.ToLower();

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
                                    WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, sizeof(bool));
                                }
                                break;
                            case EDataType.Byte:
                                {
                                    byte value = 0;

                                    if (cellToString.Any(char.IsLetter))
                                    {
                                        GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                    }
                                    else
                                    {
                                        value = Convert.ToByte(cellToString);
                                    }

                                    byte[] bytes = BitConverter.GetBytes(value);
                                    WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, sizeof(byte));
                                }
                                break;
                            case EDataType.Short:
                                {
                                    Int16 value = 0;

                                    if (cellToString.Any(char.IsLetter))
                                    {
                                        GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                    }
                                    else
                                    {
                                        value = Convert.ToInt16(cellToString);
                                    }

                                    byte[] bytes = BitConverter.GetBytes(value);
                                    WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, sizeof(Int16));
                                }
                                break;
                            case EDataType.Float:
                                {
                                    float value = 0;

                                    if (cellToString.Any(IsInValidFloat))
                                    {
                                        GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                    }
                                    else
                                    {
                                        value = (float)Convert.ToDouble(cellToString);
                                    }

                                    byte[] bytes = BitConverter.GetBytes(value);
                                    WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, sizeof(float));
                                }
                                break;
                            case EDataType.Double:
                                {
                                    double value = 0;

                                    if (cellToString.Any(IsInValidFloat))
                                    {
                                        GetDataCheckMessage(ref cellToString, ref dataCheckMessage, columnHeader, row, "0");
                                    }
                                    else
                                    {
                                        value = Convert.ToDouble(cellToString);
                                    }

                                    byte[] bytes = BitConverter.GetBytes(value);
                                    WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, sizeof(double));
                                }
                                break;
                            case EDataType.Enum:
                                {
                                    cellToString = cellToString.ToLower();

                                    byte value = 0;
                                    if (cellToString != "")
                                    {
                                        string enumType = Convert.ToString(DataArray[(int)EColumnHeaderElement.StructType + 1, columnHeader.ColumnIndex]).ToLower();
                                        string key = enumType.Trim() + "_" + cellToString.Trim();

                                        if (enumMap.ContainsKey(key))
                                        {
                                            value = enumMap[key];
                                        }
                                        else
                                        {
                                            dataCheckMessage += "[" + row + ", " + columnHeader.Name + "] 의 " + key + "는 없는 enum입니다.\r\n";
                                        }
                                    }

                                    byte[] bytes = { value };
                                    WriteBytes(ref bw, ref buffer, ref bytes, ref seek, bufferSize, sizeof(byte));
                                }
                                break;
                            case EDataType.String:
                                {
                                    Byte[] stringBytes = System.Text.Encoding.Unicode.GetBytes(cellToString);
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

        private void GetDataCheckMessage(ref string data, ref string dataCheckMessage, AnvilColumnHeader columnHeader, int row, string defaultValue)
        {
            dataCheckMessage += "[" + row + ", " + columnHeader.Name + "] 의 " + " 데이터(" + data + ")와 타입(" + Enum.GetName(typeof(EDataType), columnHeader.DataType) + ")이 다릅니다.\r\n";
        }
    }
}
