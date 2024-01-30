using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace TestWPF
{
    public class ColumnDescsription
    {
        public List<string> TargetProjectName { get; set; }
        public List<ColumnElementDescription> Elements { get; set; }
    }

    class WorkSpace : ICloneable
    {
        public enum ELoadResult
        {
            Success,
            Failed,
            InvalidData,
            Count
        }

        public delegate void OnTraversalFinishedDelegate();
        public static OnTraversalFinishedDelegate onCurrentWorkspaceChanged;

        private static WorkSpace current;
        public static WorkSpace Current { get { return current; } set { current = value; if(onCurrentWorkspaceChanged != null) onCurrentWorkspaceChanged(); } }
        public static Type CurrentTableType { get { return Type.GetType("TestWPF." + WorkSpace.Current.TableType); } }

        public string ProjectName   { get; set; }
        public string GamePath      { get; set; }
        public string ContentPath   { get; set; }
        public string EnginePath { get; set; }

        public List<string> ExcludeTables { get; set; }
        public Dictionary<string, HashSet<string>> BookmarkMap { get; set; }

        public string TableType { get; set; }
        public List<ColumnDescsription> Elements { get; set; }

        public Dictionary<string, string> FunctionMap { get; set; }

        public bool IsValid()
        {
            return ProjectName != "" && Directory.Exists(GamePath) && Directory.Exists(ContentPath) && Directory.Exists(EnginePath);
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
