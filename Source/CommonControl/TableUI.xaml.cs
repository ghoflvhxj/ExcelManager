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
    /// Tableasd.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TableUI : UserControl
    {
        public TableUI()
        {
            InitializeComponent();
        }

        public void SetGameDataTable(GameDataTable gameDataTable)
        {
            Grid.RowDefinitions.RemoveRange(1, Grid.RowDefinitions.Count - 1);

            TableName.Text = Utility.GetOnlyFileName(gameDataTable.FilePath);

            // 칼럼들 추가하기~
            foreach (BaseColumnHeader columnHeader in gameDataTable.ColumnHeaders)
            {
                RowDefinition rowDefinition = new();
                Grid.RowDefinitions.Add(rowDefinition);

                TextBox textBox = new();
                textBox.Style = this.FindResource("ColumnTextBox") as Style;
                textBox.Text = columnHeader.Name;

                Grid.SetRow(textBox, Grid.RowDefinitions.Count - 1);
                Grid.Children.Add(textBox);
            }
        }
    }
}
