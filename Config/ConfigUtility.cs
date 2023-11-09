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
        static public string ConfigPathName { get; set; }
        static public string ProjectName { get { return System.Reflection.Assembly.GetEntryAssembly().GetName().Name; } }
        static public string ConfigPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProjectName); } }
        static public string SavedFileName { get { return "WeatherForecast.json"; } }
        static public string SavedFilePath { get { return Path.Combine(ConfigUtility.ConfigPath, SavedFileName); } }
        static public string BookmarkFileName { get { return "Bookmark.json"; } }

        public ConfigUtility()
        {
            Initilaize();
        }

        public void Initilaize()
        {;
            ConfigPathName = Path.Combine(ConfigPath, "Setting.config");

            Utility.Log("설정 파일을 엽니다. 경로: " + Path.GetFullPath(ConfigPathName));

            ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
            fileMap.ExeConfigFilename = ConfigPathName;
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
