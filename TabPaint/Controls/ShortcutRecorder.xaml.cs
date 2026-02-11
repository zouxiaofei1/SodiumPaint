//
//ShortcutRecorder.xaml.cs
//快捷键录制控件，用于在设置界面中捕获用户按下的键盘组合键并实时显示。
//
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabPaint.Services;

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

        private static void OnCurrentItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {  }
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
            ModifierKeys modifiers = Keyboard.Modifiers;

            // 冲突检查逻辑
            var settings = SettingsManager.Instance.Current;
            if (settings != null && settings.Shortcuts != null)
            {
                var conflict = settings.Shortcuts.FirstOrDefault(kvp => 
                    kvp.Value != CurrentItem && // 排除自己
                    key != Key.None && // 忽略 None 键的冲突
                    kvp.Value.Key == key && 
                    kvp.Value.Modifiers == modifiers);

                if (conflict.Value != null)
                {
                    conflict.Value.Key = Key.None;
                    conflict.Value.Modifiers = ModifierKeys.None;
                    var window = Window.GetWindow(this) as SettingsWindow;
                    if (window != null)
                    {
                        string featureName = GetFriendlyName(conflict.Key);
                        window.ShowConflictToast(featureName);
                    }
                }
            }
            if (CurrentItem == null) CurrentItem = new ShortcutItem();

            CurrentItem.Key = key;
            CurrentItem.Modifiers = modifiers;

            Keyboard.ClearFocus();
        }
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Focus();
            if (e.OriginalSource is DependencyObject source)
            {
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
            }
        }

        private string GetFriendlyName(string shortcutId)
        {
            string resourceKey = "L_Settings_Shortcuts_" + shortcutId.Replace(".", "_");
            string name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;

            resourceKey = "L_Settings_Shortcuts_" + (shortcutId.Contains(".") ? shortcutId.Split('.')[1] : shortcutId);
            name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;
            resourceKey = "L_ToolBar_" + shortcutId.Replace("Tool.SwitchTo", "");
            name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;
            resourceKey = "L_Menu_" + shortcutId.Replace(".", "_");
            name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;

            return shortcutId;
        }
    }
}
