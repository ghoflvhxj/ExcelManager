using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;

namespace TestWPF
{
    public class ConfigUtility
    {
        private Configuration Config = null;

        static public string ProjectName { get { return System.Reflection.Assembly.GetEntryAssembly().GetName().Name; } }

        // This will get the current WORKING directory (i.e. \bin\Debug)
        static string workingDirectory = Environment.CurrentDirectory;

        // This will get the current PROJECT bin directory (ie ../bin/)
        static string binaryDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;

        // This will get the current PROJECT directory
        static string projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.Parent.FullName;

        static public string DataPath { get { return Path.Combine(projectDirectory, "Data"); }  }
        static public string ConfigPath { get { return Path.Combine(DataPath, "ExcelManager.config"); } }
        static public string CachedDataPath { get { return Path.Combine(DataPath, "CachedData.json"); } }
        static public string BookmarkFileName { get { return "Bookmark.json"; } }

        public ConfigUtility()
        {
            Initilaize();
        }

        public void Initilaize()
        {;
            Utility.Log("설정 파일을 엽니다. 경로: " + Path.GetFullPath(ConfigPath));

            ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
            fileMap.ExeConfigFilename = ConfigPath;
            Config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);

            Config.Save();
        }

        public bool AddSectionElement(string key, string value, bool bOverlap = false)
        {
            if (Config.AppSettings.Settings.AllKeys.Contains(key) == true)
            {
                if (bOverlap == true)
                {
                    Config.AppSettings.Settings[key].Value = value;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                Config.AppSettings.Settings.Add(key, value);
            }

            Config.Save();

            return true;
        }

        public string GetSectionElementValue(string key)
        {
            if (Config.AppSettings.Settings.AllKeys.Contains(key) == true)
            {
                return Config.AppSettings.Settings[key].Value;
            }

            return "";
        }

    }
}
