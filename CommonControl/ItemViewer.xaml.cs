using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace TestWPF
{
    /// <summary>
    /// ItemViewer.xaml에 대한 상호 작용 논리
    /// </summary>
    [ContentProperty("Children")]
    public partial class ItemViewer : UserControl
    {
        public UIElementCollection Children
        {
            get { return MenuPanel.Children; }
        }

        public string Title { get; set; }

        public bool ClickToggler { get; set; }
        public bool MouseEnteredByChild;

        // 아이템
        public HashSet<MyItem> SelectedItemList { get; set; } = new();
        public MyItem MouseEnteredItem { get; set; }

        // 드래그 선택
        public Point DragStart { get; set; }
        public Rect dragRect;
        private Rectangle rectangle;
        public delegate void FOnDragSelectionDelegate(Rect dragRect);
        FOnDragSelectionDelegate OnDragSelectionDelegate;
        public HashSet<MyItem> DragItemList { get; set; } = new();

        public Color MyColor = Colors.Gray;
        public bool IsHighlighted { get { return MyColor == Colors.Chocolate; } }
        private Storyboard myStoryboard;

        // 더블클릭 델리게이트
        public delegate void OnItemDoubleClickedDelegate(MyItem doubleClickedItem);
        public OnItemDoubleClickedDelegate onItemDoubleClicked;

        public ItemViewer()
        {
            InitializeComponent();

            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1));
            myDoubleAnimation.FillBehavior = FillBehavior.HoldEnd;
            myDoubleAnimation.Completed += MyDoubleAnimation_Completed;

            myStoryboard = new Storyboard();
            myStoryboard.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, ScrollViewer);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath("RenderTransform.ScaleY"));
            ClickToggler = true;
        }

        public bool AddItem(MyItem newItem, out int outIndex)
        {
            if (newItem == null)
            {
                outIndex = GlobalValue.InvalidIndex;
                return false;
            }

            // 드래그 처리 델리게이트
            OnDragSelectionDelegate += delegate (Rect dragRect)
            {
                Rect itemRect;
                itemRect.Location = newItem.TranslatePoint(new Point(0, 0), ItemListWrapPanel);
                itemRect.Size = new Size(newItem.ActualWidth, newItem.ActualHeight);

                if (itemRect.IntersectsWith(dragRect))
                {
                    if(DragItemList.Contains(newItem) == false)
                    {
                        newItem.ToggleSelect();
                        DragItemList.Add(newItem);
                    }
                }
                else
                {
                    if(DragItemList.Contains(newItem))
                    {
                        newItem.ToggleSelect();
                        DragItemList.Remove(newItem);
                    }
                }

                if (newItem.Selected)
                {
                    SelectedItemList.Add(newItem);
                }
                else
                {
                    SelectedItemList.Remove(newItem);
                }
            };

            newItem.OnMouseHoverChanged += delegate (bool bEnetered)
            {
                if (bEnetered)
                {
                    MouseEnteredItem = newItem;
                }
                else
                {
                    MouseEnteredItem = null;
                }
            };

            outIndex = ItemListWrapPanel.Children.Add(newItem);
            return true;
        }

        public void ResizeItem(int newWidth, int newHeight)
        {
            ItemListWrapPanel.ItemWidth = newWidth;
            ItemListWrapPanel.ItemHeight = newHeight;
        }

        public void ToggleHighlight()
        {
            MyColor = MyColor == Colors.Gray ? Colors.Chocolate : Colors.Gray;
            ItemViewerHead.Background = new SolidColorBrush(MyColor);
        }

        public void UpdateScrollViewerHeight(bool bMaxmized = false)
        {
            if (ScrollViewer != null && Application.Current.MainWindow != null)
            {
                Point ScrollViewerPoint = ScrollViewer.TranslatePoint(new Point(0, 0), Application.Current.MainWindow);
                ScrollViewer.Height = Application.Current.MainWindow.ActualHeight - ScrollViewerPoint.Y - System.Windows.SystemParameters.WindowCaptionHeight - 20;
                Utility.Log("스크롤 뷰어 새높이:" + ScrollViewer.Height + ", 메인윈도우 높이:" + Application.Current.MainWindow.Height + ", 스크롤위치:" + ScrollViewerPoint.Y);
            }
        }

        public int GetItemWidth()
        {
            return (int)ItemListWrapPanel.ItemWidth;
        }

        public int GetItemHeight()
        {
            return (int)ItemListWrapPanel.ItemHeight;
        }

        public int GetInterval()
        {
            return (int)ItemListWrapPanel.ActualWidth / GetItemWidth();
        }

        public void ClearSelectedItems()
        {
            foreach(MyItem myItem in SelectedItemList)
            {
                Utility.Log(myItem.FileName + "언셀렉트");
                myItem.UnSelect();
            }

            SelectedItemList.Clear();
        }

        public void ToggleSelection()
        {
            foreach (MyItem myItem in SelectedItemList)
            {
                myItem.SelectedRectangle.Visibility = myItem.SelectedRectangle.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            }
        }

        bool GetItem(out MyItem outItem)
        {
            outItem = null;
            int interval = GetInterval();
            int x = (int)DragStart.X / GetItemWidth();
            int y = (int)DragStart.Y / GetItemHeight();
            int index = x + (y * interval);

            bool invalidSelectPosition = false;
            invalidSelectPosition |= DragStart.X > GetItemWidth() * interval;
            invalidSelectPosition |= ItemListWrapPanel.Children.Count < index + 1;
            invalidSelectPosition |= (int)DragStart.X - (x * GetItemWidth()) > GetItemWidth();
            if(invalidSelectPosition == false)
            {
                outItem = ItemListWrapPanel.Children[index] as MyItem;
            }

            return !invalidSelectPosition;
        }

        private void MyDoubleAnimation_Completed(object sender, EventArgs e)
        {
            if (ClickToggler == false)
            {
                ScrollViewer.Visibility = Visibility.Collapsed;
            }
        }

        public void Label_MouseLeave(object sender, MouseEventArgs e)
        {
            dynamic control = sender as dynamic;
            if (control.GetType().GetProperty("Background") != null)
            {
                MyColor = Colors.Gray;
                ItemViewerHead.Background = new SolidColorBrush(MyColor);
            }

            MouseEnteredByChild = false;
        }

        private void Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ClickToggler = !ClickToggler;
            if (ClickToggler)
            {
                myStoryboard.AutoReverse = false;
                myStoryboard.Begin();
            }
            else
            {
                myStoryboard.AutoReverse = true;
                myStoryboard.Begin();

                // 열리는 애니메이션으로 Seek하고 Reverse되면 닫히게 된다.
                myStoryboard.Seek(TimeSpan.FromSeconds(0.1));
            }

            if (ScrollViewer.Visibility == Visibility.Collapsed)
            {
                ScrollViewer.Visibility = Visibility.Visible;
            }
        }

        private void ItemViewerHead_MouseMove(object sender, MouseEventArgs e)
        {
            dynamic control = sender as dynamic;
            if (control.GetType().GetProperty("Background") != null && MouseEnteredByChild == false && MyColor != Colors.Chocolate)
            {
                MyColor = Colors.Chocolate;
                ItemViewerHead.Background = new SolidColorBrush(MyColor);
            }
        }

        private void DragSelectionCanvnas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released || rectangle == null)
            {
                return;
            }

            Point currentMousePosition = Mouse.GetPosition(ItemListWrapPanel);

            double left = Math.Min(currentMousePosition.X, DragStart.X);
            double top = Math.Min(currentMousePosition.Y, DragStart.Y);
            rectangle.Width = Math.Max(currentMousePosition.X, DragStart.X) - left;
            rectangle.Height = Math.Max(currentMousePosition.Y, DragStart.Y) - top;
            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);

            dragRect.Location = new Point(left, top);
            dragRect.Size = new Size(rectangle.Width, rectangle.Height);

            OnDragSelectionDelegate(dragRect);
        }

        private void DragSelectionCanvnas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Utility.Log("캔바스 눌림");

            DragItemList.Clear();

            if (MouseEnteredItem == null)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) == false)
                {
                    ClearSelectedItems();
                }

                rectangle = new();
                rectangle.Stroke = Brushes.LightBlue;
                rectangle.StrokeThickness = 2;
                DragSelectionCanvnas.Children.Add(rectangle);

                DragStart = Mouse.GetPosition(ItemListWrapPanel);
                Canvas.SetLeft(rectangle, DragStart.X);
                Canvas.SetTop(rectangle, DragStart.Y);
            }
        }

        private void DragSelectionCanvnas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            bool IsDraging = rectangle != null;
            if (IsDraging)
            {
                DragSelectionCanvnas.Children.Clear();
            }

            rectangle = null;
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragStart = Mouse.GetPosition(ItemListWrapPanel);

            if (Keyboard.IsKeyDown(Key.LeftCtrl) == false)
            {
                ClearSelectedItems();
            }

            if (MouseEnteredItem != null)
            {
                if(SelectedItemList.Contains(MouseEnteredItem))
                {
                    MouseEnteredItem.UnSelect();
                    SelectedItemList.Remove(MouseEnteredItem);
                    Utility.Log(MouseEnteredItem.FileName + "제거");
                }
                else
                {
                    MouseEnteredItem.Select();
                    SelectedItemList.Add(MouseEnteredItem);
                    Utility.Log(MouseEnteredItem.FileName + "추가");
                }
            }

            if (e.ClickCount >= 2 && MouseEnteredItem != null)
            {
                Utility.ExecuteProcess(MouseEnteredItem.Path);
            }

            Utility.Log("그리드 눌림");
        }

        private void Grid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DragSelectionCanvnas_MouseLeftButtonUp(sender, e);
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            DragSelectionCanvnas_MouseMove(sender, e);
        }

        private void Grid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(MouseEnteredItem == null)
            {
                return;
            }

            if(SelectedItemList.Contains(MouseEnteredItem) == false)
            {
                ClearSelectedItems();
            }

            SelectedItemList.Add(MouseEnteredItem);
            MouseEnteredItem.Select();
        }

        char searchKey = ' ';
        int searchIndex = 0;
        int workCounter = 0; 
        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            Utility.Log("키 입력");
            if(e.Key < Key.A || Key.Z < e.Key)
            {
                return;
            }

            Dictionary<char, List<MyItem>> test = new();
            foreach(UIElement uiElement in ItemListWrapPanel.Children)
            {
                MyItem myItem = uiElement as MyItem;
                if(myItem == null)
                {
                    continue;
                }

                Utility.FindOrAdd<char, List<MyItem>>(test, myItem.FileName[0]).Add(myItem);
            }

            if(test.ContainsKey(e.Key.ToString()[0]) == false)
            {
                return;
            }

            if (searchKey != e.Key.ToString()[0])
            {
                searchKey = e.Key.ToString()[0];
                searchIndex = 0;
            }
            else
            {
                ++searchIndex;
                if (test[searchKey].Count <= searchIndex)
                {
                    searchIndex = 0;
                }
            }

            MyItem item = test[searchKey][searchIndex];

            ClearSelectedItems();
            item.Select();
            SelectedItemList.Add(item);

            Point itemPoint = item.TranslatePoint(new Point(0, item.ActualHeight), ItemListWrapPanel);
            if (itemPoint.Y > ScrollViewer.VerticalOffset + ScrollViewer.Height)
            {
                ScrollViewer.ScrollToVerticalOffset(itemPoint.Y);
            }
            else
            {
                ScrollViewer.ScrollToVerticalOffset(itemPoint.Y - item.ActualHeight);
            }
        }
    }
}
