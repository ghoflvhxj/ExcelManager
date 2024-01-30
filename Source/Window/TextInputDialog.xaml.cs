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
using System.Windows.Shapes;

namespace TestWPF
{
    /// <summary>
    /// TextInputDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TextInputDialog : Window
    {
        public delegate void FOnClicked();
        public FOnClicked onClicked { get; set; }
        public string InputText { get { return InputTextBox.Text; } }

        public TextInputDialog(string newTitle)
        {
            InitializeComponent();

            Title = newTitle;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(onClicked != null)
            {
                onClicked();
            }

            Close();
        }
    }
}
