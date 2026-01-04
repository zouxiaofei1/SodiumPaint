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
            // 失去焦点时恢复边框颜色
            this.LostFocus += (s, e) => MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            this.GotFocus += (s, e) => MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0)); // Win11 蓝色
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

        // 核心逻辑：监听按键
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 忽略单独按下的控制键（如只按了Ctrl）
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin ||
                e.Key == Key.System) // Alt sometimes shows as System
            {
                return;
            }

            e.Handled = true; // 阻止事件冒泡，防止触发外层快捷键

            // 获取按下的实际按键 (处理 System Key 如 Alt组合)
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // 获取修饰键
            ModifierKeys modifiers = Keyboard.Modifiers;

            // 更新数据对象
            if (CurrentItem == null) CurrentItem = new ShortcutItem();

            CurrentItem.Key = key;
            CurrentItem.Modifiers = modifiers;

            UpdateDisplay();

            // 自动移除焦点，表示录入完成
            Keyboard.ClearFocus();

            // 触发保存（可选，取决于你何时保存 Settings）
            // SettingsManager.Instance.Save(); 
        }
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
         
            this.Focus();

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
