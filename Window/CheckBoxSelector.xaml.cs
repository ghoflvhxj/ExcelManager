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
using System.Windows.Media.Animation;

namespace TestWPF
{
    /// <summary>
    /// CheckBoxSelector.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CheckBoxSelector : Window
    {
        public delegate void OnButtonClickedDelegate(List<CheckBox> checkedList);
        public OnButtonClickedDelegate OnButtonClicked;

        public int TargetValue { get; set; }

        public CheckBoxSelector()
        {
            InitializeComponent();
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(progressBar.Value == progressBar.Maximum)
            {
                System.Timers.Timer timer = new();
                timer.Interval = 100;
                timer.Elapsed += new System.Timers.ElapsedEventHandler((object sender, System.Timers.ElapsedEventArgs args) => {
                    timer.Stop();
                    if (MessageBox.Show("완료되었습니다.") == MessageBoxResult.OK)
                    {
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            Close();
                        }));
                    }
                });
                timer.Start();
            }
        }

        HashSet<string> checkedList = new();
        public void InitializeItemList(List<string> items)
        {
            foreach(string item in items)
            {
                CheckBox checkBox = new();
                checkBox.Content = item;
                checkBox.IsChecked = true;

                checkBox.Checked += CheckBox_Checked;
                checkBox.Unchecked += CheckBox_Unchecked;

                CheckBoxWrapPanel.Children.Add(checkBox);
            }

            checkedList = items.ToHashSet();
            UpdateCheckedListString();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                checkedList.Remove(checkBox.Content.ToString());
            }
            UpdateCheckedListString();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            if(checkBox != null)
            {
                checkedList.Add(checkBox.Content.ToString());
            }
            UpdateCheckedListString();
        }

        private void UpdateCheckedListString()
        {
            SelectedListTextBlock.Text = string.Join(", ", checkedList.ToArray());
        }

        public void UpdateTest(int Value)
        {
            DoubleAnimation animation = new DoubleAnimation(Value, TimeSpan.FromSeconds(0.1));
            progressBar.BeginAnimation(ProgressBar.ValueProperty, animation);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if(OnButtonClicked != null)
            {
                List<CheckBox> checkedList = new();
                foreach (UIElement element in CheckBoxWrapPanel.Children)
                {
                    CheckBox checkBox = element as CheckBox;
                    if(checkBox == null)
                    {
                        continue;
                    }

                    if(checkBox.IsChecked == false)
                    {
                        continue;
                    }

                    checkedList.Add(checkBox);
                }

                OnButtonClicked(checkedList);
            }
        }
    }
}
