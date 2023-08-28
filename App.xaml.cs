using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace TestWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public MExcel ExcelLoader { get; set; }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (ExcelLoader != null)
            {
                ExcelLoader.DestroyExcelApp();
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ExcelLoader = new MExcel(true);
        }
    }
}
