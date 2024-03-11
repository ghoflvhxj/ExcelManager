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
    class AnvilDataTable : AnvilTeamTable
    {
        protected override bool IsContainForeignKeyToken(List<string> columnHeaderString) 
        {
            return columnHeaderString[(int)EColumnHeaderElement.Name][0] == '@';
        }

        public void FixResource()
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
                ThreadPool.QueueUserWorkItem(CheckResourceData, new ResourceCheckInfo() { ColumnIndex = col, ColumnName = pair.Value.Name, ExcelPath = FilePath, resourcePathType = pair.Key, RowCount = RowCount });
            }
        }

        private void CheckResourceData(object obj)
        {
            ResourceCheckInfo resourceCheckInfo = obj as ResourceCheckInfo;
            if (resourceCheckInfo == null)
            {
                return;
            }

            string colName = resourceCheckInfo.ColumnName;
            string excelPath = resourceCheckInfo.ExcelPath;
            int columnIndex = resourceCheckInfo.ColumnIndex;

            for (int row = (int)EColumnHeaderElement.Count + 1; row <= resourceCheckInfo.RowCount; ++row)
            {
                object cellObject = GameDataTable.GameDataTableMap[excelPath].DataArray[row, columnIndex];
                if (cellObject == null)
                {
                    continue;
                }

                string originCellValue = cellObject.ToString();
                switch (resourceCheckInfo.resourcePathType)
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

    }
}
