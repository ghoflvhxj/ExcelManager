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
    /// TableReferenceModifyPanel.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TableReferenceModifyPanel : UserControl
    {
        GameDataTable table;

        public TableReferenceModifyPanel()
        {
            InitializeComponent();
        }

        public void BindTable(GameDataTable newTable)
        {
            table = newTable;
            foreach(AnvilColumnHeader columnHeader in table.ColumnHeaders)
            {
                if(columnHeader == table.IndexColumn)
                {
                    continue;
                }

                TableReferenceModifyPanelItem item = new TableReferenceModifyPanelItem();
                item.ColumnName.Content = columnHeader.Name;

                // 테이블, 칼럼 선택
                if(table.ForeignKeyInfoMap.ContainsKey(columnHeader.Name))
                {
                    ForeignKeyInfo foreignKeyInfo = table.ForeignKeyInfoMap[columnHeader.Name];

                    for(int i= 0 ; i < item.TableComboBox.Items.Count; ++i)
                    {
                        if(item.TableComboBox.Items[i].ToString().ToLower() == foreignKeyInfo.ReferencedTableName.ToLower())
                        {
                            item.TableComboBox.SelectedItem = item.TableComboBox.Items[i];
                            break;
                        }
                    }

                    for (int i = 0; i < item.ColumnComboBox.Items.Count; ++i)
                    {
                        if (item.ColumnComboBox.Items[i].ToString().ToLower() == foreignKeyInfo.ForeignKeyName.ToLower())
                        {
                            item.ColumnComboBox.SelectedItem = item.ColumnComboBox.Items[i];
                            break;
                        }
                    }
                }

                ItemStackPanel.Children.Add(item);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            int itemCount = ItemStackPanel.Children.Count;
            for(int i=0; i<itemCount; ++i)
            {
                TableReferenceModifyPanelItem myItem = ItemStackPanel.Children[i] as TableReferenceModifyPanelItem;
                if(myItem == null)
                {
                    continue;
                }

                string columnName = Convert.ToString(myItem.ColumnName.Content);
                if (table.IsValidColumnName(columnName) == false)
                {
                    continue;
                }

                if(myItem.TableComboBox.SelectedItem != null && myItem.ColumnComboBox.SelectedItem != null)
                {
                    ForeignKeyInfo foreignKeyInfo = new();
                    foreignKeyInfo.ReferencedTableName = myItem.TableComboBox.SelectedItem.ToString();
                    foreignKeyInfo.ForeignKeyName = myItem.ColumnComboBox.SelectedItem.ToString();

                    table.ForeignKeyInfoMap[columnName] = foreignKeyInfo;
                }
                else
                {
                    table.ForeignKeyInfoMap.Remove(columnName);
                }
            }

            table.PostInitInfo();
        }
    }
}
