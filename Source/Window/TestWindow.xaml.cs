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
    public partial class TestWindow : Window
    {
        public List<TableUI> SelectedItems { get; set; } = new();
        List<TableUI> TableUIList = new();
        public TestWindow()
        {
            InitializeComponent();

            Random random = new();
            List<string> ep = MExcel.excelPaths.ToList();
            List<GameDataTable> temp = new();
            for(int i=0; i<3; ++i)
            {
                temp.Add(GameDataTable.GameDataTableMap[ep[random.Next() % ep.Count]]);
            }
            
            SetGameDataTables(temp);

            //TableUI a = new();
            //a.SetGameDataTable(GameDataTable.GetTableByPath(MExcel.excelPaths.First<string>()));

            //Canvas.SetLeft(a, 0);
            //Canvas.SetTop(a, 0);

            //CanvasPanel.Children.Add(a);
        }

        public void SetGameDataTables(List<GameDataTable> gameDataTables)
        {
            Random random = new();

            foreach (GameDataTable gameDataTable in gameDataTables)
            {
                TableUI a = new();
                a.SetGameDataTable(gameDataTable);

                Canvas.SetLeft(a, random.Next() % 500);
                Canvas.SetTop(a, random.Next() % 500);

                TableUIList.Add(a);

                CanvasPanel.Children.Add(a);
            }
        }

        private void CanvansPanel_MouseMove(object sender, MouseEventArgs e)
        {
            //Utility.Log("pos: " + e.GetPosition(CanvasPanel).X + ", " + e.GetPosition(CanvasPanel).Y);
            //Canvas.SetLeft(a, e.GetPosition(CanvasPanel).X);
            //Canvas.SetTop(a, e.GetPosition(CanvasPanel).Y);
        }
    }

    public class Base
    {
        public virtual UIElement GetDrawingData()
        {
            return null;
        }
    }

    public class TableUITemp : Base
    {
        public Grid grid = new();

        public TableUITemp(GameDataTable gameDataTable)
        {
            const int headerSize = 50; 
            grid.Width = 300;
            grid.Height = headerSize + gameDataTable.ColumnHeaders.Count * 20;

            RowDefinition rowDefinition = new();
            rowDefinition.Height = new GridLength(50, GridUnitType.Pixel);
            grid.RowDefinitions.Add(rowDefinition);

            for (int i = 0; i < gameDataTable.ColumnHeaders.Count; ++i)
            {
                rowDefinition = new();
                //rowDefinition.Height = new GridLength(1, GridUnitType.Star);
                grid.RowDefinitions.Add(rowDefinition);
            }

            TableUIElement tableName = new(gameDataTable);
            AddDrawingData(0, tableName.GetUIElements());

            for (int i = 0; i < gameDataTable.ColumnHeaders.Count; ++i)
            {
                TableUIElement tableColumn = new(gameDataTable.ColumnHeaders[i]);
                AddDrawingData(i + 1, tableColumn.GetUIElements());
            }
        }

        public void AddDrawingData(int row, List<UIElement> uIElements)
        {
            foreach(UIElement uIElement in uIElements)
            {
                grid.Children.Add(uIElement);
                Grid.SetRow(uIElement, row);
            }
        }
    }

    public class TableUIElement
    {
        TextBox textBox = new();
        Rectangle rect = new();

        public TableUIElement(GameDataTable gameDataTable)
        {
            textBox.Text = Utility.GetOnlyFileName(gameDataTable.FilePath);
        }

        public TableUIElement(BaseColumnHeader columnHeader)
        {
            textBox.Text = columnHeader.Name;
        }

        public List<UIElement> GetUIElements() 
        {
            textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            textBox.VerticalAlignment = VerticalAlignment.Stretch;
            textBox.HorizontalContentAlignment = HorizontalAlignment.Center;
            textBox.VerticalContentAlignment = VerticalAlignment.Center;

            List<UIElement> uIElements = new();
            uIElements.Add(textBox);

            rect.Stroke = Brushes.LightBlue;
            rect.StrokeThickness = 2;
            rect.Height = 200;
            rect.Width = 200;

            uIElements.Add(rect);

            return uIElements;
        }
    }
}
