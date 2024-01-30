using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestWPF
{
    /// <summary>
    /// BookmarkPanel.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class BookmarkPanel : UserControl
    {
        public Dictionary<string, HashSet<string>> Test { get; set; }
        public HashSet<string> Test2 { get; set; }

        public BookmarkPanel()
        {
            InitializeComponent();

            Test2 = new();
            Test2.Add("a");
            Test2.Add("b");
            Test2.Add("c");
            TV.ItemsSource = Test2;
        }


    }
}
