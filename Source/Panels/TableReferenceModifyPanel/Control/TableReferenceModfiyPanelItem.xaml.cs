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
    /// TableModfiyPanelItem.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TableReferenceModifyPanelItem : UserControl
    {
        public TableReferenceModifyPanelItem()
        {
            InitializeComponent();

            foreach (string excelFileName in MExcel.excelFileNames)
            {
                TableComboBox.Items.Add(excelFileName);
            }
            TableComboBox.Items.Add("");
        }

        private void TableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ColumnComboBox.Items.Clear();

            string tableName = TableComboBox.SelectedItem.ToString();
            if (MExcel.excelFileNameToPath.ContainsKey(tableName) == false)
            {
                return;
            }

            string tablePath = MExcel.excelFileNameToPath[tableName];
            foreach(var columnHeader in MExcel.TableMap[tablePath].ColumnHeaders)
            {
                ColumnComboBox.Items.Add(columnHeader.Name);
            }
        }
    }
}
