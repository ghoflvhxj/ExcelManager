using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWPF
{
    class AnvilDataTable : GameDataTable
    {
        public override void MakeColumnHeaders()
        {
            // 칼럼 분석
            List<AnvilColumnHeader> newColumnHeaders = new List<AnvilColumnHeader>();
            for (int col = 1; col <= ColumnCount; ++col)
            {
                List<string> columnHeaderAsString = new List<string>((int)EColumnHeaderElement.Count);
                for (int row = 0; row < (int)EColumnHeaderElement.Count; ++row)
                {
                    object cellObject = DataArray[row + 1, col];
                    columnHeaderAsString.Add(cellObject != null ? cellObject.ToString() : "");
                }

                if (IsInvalidColumn(columnHeaderAsString))
                {
                    break;
                }

                AnvilColumnHeader newColumnHeader = new AnvilColumnHeader();
                StringToColumnHeader(ref columnHeaderAsString, newColumnHeader, col);

                bool bColumnUsedInGame = newColumnHeader.MachineType != EMachineType.None;
                if (bColumnUsedInGame)
                {
                    if (IsContainForeignKeyToken(columnHeaderAsString[(int)EColumnHeaderElement.Name]))
                    {
                        string columnName = columnHeaderAsString[(int)EColumnHeaderElement.Name].Remove(0, 1);
                        int underbarIndex = columnName.LastIndexOf('_');
                        string tableName = columnName;
                        if (underbarIndex != -1)
                        {
                            tableName = columnName.Substring(0, underbarIndex);
                        }

                        bool bFoundRealTable = false;
                        string realTableName = tableName;
                        foreach (string excelFileName in MExcel.excelFileNames)
                        {
                            if ((tableName == excelFileName) || (tableName.Contains(excelFileName) && !bFoundRealTable) || (excelFileName.Contains(tableName) && !bFoundRealTable))
                            {
                                bFoundRealTable = true;
                                realTableName = excelFileName;
                                break;
                            }
                        }

                        if (ForeignKeyInfoMap.ContainsKey(newColumnHeader.Name) == false)
                        {
                            ForeignKeyInfoMap.Add(newColumnHeader.Name, new ForeignKeyInfo()
                            {
                                ReferencedTableName = realTableName,
                                ForeignKeyName = "index"
                            });
                        }
                        //newColumnHeader.ReferencedTableName = realTableName;
                        //newColumnHeader.ForeignKeyName = "index";
                    }
                    else if (newColumnHeader.DataType == EDataType.Enum)
                    {
                        newColumnHeader.EnumName = columnHeaderAsString[(int)EColumnHeaderElement.StructType];
                    }

                    newColumnHeaders.Add(newColumnHeader);

                    if (newColumnHeader.Name.ToLower() == "index")
                    {
                        IndexColumn = newColumnHeader;
                    }
                }
                else
                {
                    if (newColumnHeader.Name.ToLower().Contains("comment"))
                    {
                        CommentColumns.Add(newColumnHeader);
                    }
                }
            }

            // 레코드 인덱스와 배열 인덱스 바인딩
            Dictionary<int, int> newIndexToDataArrayRow = new Dictionary<int, int>();
            for (int row = (int)EColumnHeaderElement.Count + 1; row <= RowCount; ++row)
            {
                if (IndexColumn.ColumnIndex == 0)
                {
                    continue;
                }

                if (DataArray[row, IndexColumn.ColumnIndex] == null)
                {
                    continue;
                }

                newIndexToDataArrayRow.TryAdd(Convert.ToInt32(DataArray[row, IndexColumn.ColumnIndex]), row);
            }
            RecordIndexToDataArrayIndex = newIndexToDataArrayRow;

            ColumnHeaders = newColumnHeaders;
        }
    }
}
