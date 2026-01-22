using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Controls
{
    public partial class ShortcutRecorder : UserControl
    {
        // 定义依赖属性，以便在 XAML 中绑定
        public static readonly DependencyProperty CurrentItemProperty =
            DependencyProperty.Register("CurrentItem", typeof(ShortcutItem), typeof(ShortcutRecorder),
                new PropertyMetadata(null, OnCurrentItemChanged));

        public ShortcutItem CurrentItem
        {
            get { return (ShortcutItem)GetValue(CurrentItemProperty); }
            set { SetValue(CurrentItemProperty, value); }
        }

        public ShortcutRecorder()
        {
            InitializeComponent();
        }

        private static void OnCurrentItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ShortcutRecorder;
            control?.UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (CurrentItem != null)
                KeyDisplay.Text = CurrentItem.DisplayText;
            else
                KeyDisplay.Text = "无";
        }
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin ||
                e.Key == Key.System) // Alt sometimes shows as System
            {
                return;
            }
            if (e.Key == Key.ImeProcessed)
            {
                e.Handled = true;
                return;
            }
            e.Handled = true; 
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // 获取修饰键
            ModifierKeys modifiers = Keyboard.Modifiers;

            // 更新数据对象
            if (CurrentItem == null) CurrentItem = new ShortcutItem();

            CurrentItem.Key = key;
            CurrentItem.Modifiers = modifiers;

            UpdateDisplay();
            Keyboard.ClearFocus();
        }
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Focus();
            if (e.OriginalSource is DependencyObject source)
            {
                // 向上查找看是不是点到了 Button
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(source);
                while (parent != null && parent != this)
                {
                    if (parent is Button) return; // 如果是按钮，直接返回，不做额外处理
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            }

            e.Handled = true;

        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentItem != null)
            {
                CurrentItem.Key = Key.None;
                CurrentItem.Modifiers = ModifierKeys.None;
                UpdateDisplay();
            }
        }
    }
}
