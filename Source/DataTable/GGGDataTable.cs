using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWPF
{
    class GGGDataTable : AnvilTeamTable
    {
        protected override bool IsContainForeignKeyToken(List<string> columnHeaderString)
        {
            string foreignKey = columnHeaderString[(int)EColumnHeaderElement.Name];
            return foreignKey.Length > 0 && foreignKey[0] == '@';
        }
    }
}
