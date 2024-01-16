using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWPF
{
    public class ColumnElementDescription
    {
        public string Name { get; set; }
        public int Index { get; set; }
    }

    public enum EDataType
    {
        None,
        String,
        Int,
        StringKey,
        Enum,
        Int64,
        Byte,
        Enum_Byte,
        Bool,
        Short,
        Float,
        Double,
        Count
    }
    public enum EMachineType
    {
        None,
        Client,
        Server,
        All,
        Count
    }
    public enum EStructType
    {
        None,
        Array,
        Count
    }
    public enum EResourcePathType 
    { 
        FileName, 
        Path 
    };
    enum EColumnHeaderElement
    {
        Name,
        MachineType,
        DataType,
        StructType,
        Count
    };

    public class BaseColumnHeader
    {
        public int ColumnIndex { set; get; }
        public string Name { set; get; }
    }

    public class AnvilColumnHeader : BaseColumnHeader
    {
        public string EnumName { set; get; }

        public EDataType DataType { set; get; }
        public EMachineType MachineType { set; get; }
        public EStructType StructType { set; get; }

        public AnvilColumnHeader()
        {

        }

        public override string ToString()
        {
            string str = "";

            //str += Enum.GetName(typeof(EColumnHeaderElement), (int)EColumnHeaderElement.Name) + ": " + Name + ", ";
            //str += Enum.GetName(typeof(EColumnHeaderElement), (int)EColumnHeaderElement.MachineType) + ": " + Enum.GetName(typeof(EMachineType), (int)MachineType) + ", ";
            //str += Enum.GetName(typeof(EColumnHeaderElement), (int)EColumnHeaderElement.DataType) + ": " + Enum.GetName(typeof(EDataType), (int)DataType) + ", ";
            //str += Enum.GetName(typeof(EColumnHeaderElement), (int)EColumnHeaderElement.StructType) + ": " + Enum.GetName(typeof(EStructType), (int)StructType) + ", ";

            return str;
        }
    }

    // 참조 정보
    public class ForeignKeyInfo
    {
        public string ReferencedTableName { get; set; }
        public string ForeignKeyName { set; get; }
    }
}
