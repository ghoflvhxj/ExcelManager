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

    class WorkSpace
    {
        public static WorkSpace Current { get; set; }

        public string ProjectName   { get; set; }
        public string GamePath      { get; set; }
        public string ContentPath   { get; set; }
        public string EnginePath { get; set; }

        public List<string> ExcludeTables { get; set; }
        public Dictionary<string, List<string>> BookmarkMap { get; set; }
        public List<ColumnDescsription> Elements { get; set; }

        public bool IsValid()
        {
            return ProjectName != "" && Directory.Exists(GamePath) && Directory.Exists(ContentPath) && Directory.Exists(EnginePath);
        }
    }
}
