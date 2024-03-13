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
    }
}
