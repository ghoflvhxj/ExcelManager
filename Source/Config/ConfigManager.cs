using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWPF
{
    public class ConfigManager
    {
        public ConfigUtility configUtility = null;

        public enum ESectionType
        {
            DefaultWorkspace,
            Count
        }

        public ConfigManager()
        {
            configUtility = new ConfigUtility();

            for(int i=0; i<(int)ESectionType.Count; ++i)
            { 
                configUtility.AddSectionElement(GetSectionString(i), "", false);
            }
        }

        public void AddSectionElement(ESectionType sectionType, string value, bool bOverlap = false)
        {
            configUtility.AddSectionElement(GetSectionString(sectionType), value, bOverlap);
        }

        public string GetSectionElementValue(ESectionType sectionString)
        {
            return configUtility.GetSectionElementValue(GetSectionString(sectionString));
        }

        private string GetSectionString(ESectionType sectionString)
        {
            return Enum.GetName(typeof(ESectionType), sectionString);
        }

        private string GetSectionString(int i)
        {
            return Enum.GetName(typeof(ESectionType), i);
        }
    }
}
