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

        private static void OnCurrentItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Binding is used in XAML now
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

            // 冲突检查逻辑
            var settings = SettingsManager.Instance.Current;
            if (settings != null && settings.Shortcuts != null)
            {
                // 查找除了当前项以外，是否已经有功能占用了这个组合键
                var conflict = settings.Shortcuts.FirstOrDefault(kvp => 
                    kvp.Value != CurrentItem && // 排除自己
                    key != Key.None && // 忽略 None 键的冲突
                    kvp.Value.Key == key && 
                    kvp.Value.Modifiers == modifiers);

                if (conflict.Value != null)
                {
                    // 发现冲突：清除旧的
                    conflict.Value.Key = Key.None;
                    conflict.Value.Modifiers = ModifierKeys.None;
                    
                    // 尝试通知 SettingsWindow 显示 Toast
                    var window = Window.GetWindow(this) as SettingsWindow;
                    if (window != null)
                    {
                        // 尝试获取冲突功能的名称
                        string featureName = GetFriendlyName(conflict.Key);
                        window.ShowConflictToast(featureName);
                    }
                }
            }

            // 更新数据对象
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
            }
        }

        private string GetFriendlyName(string shortcutId)
        {
            // 1. 尝试直接拼接 L_Settings_Shortcuts_ 为前缀的资源 Key (例如 L_Settings_Shortcuts_File_Open)
            string resourceKey = "L_Settings_Shortcuts_" + shortcutId.Replace(".", "_");
            string name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;

            // 2. 尝试去掉前缀后的名称 (例如 L_Settings_Shortcuts_ToggleMode)
            resourceKey = "L_Settings_Shortcuts_" + (shortcutId.Contains(".") ? shortcutId.Split('.')[1] : shortcutId);
            name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;

            // 3. 尝试通用工具前缀 (L_ToolBar_...)
            resourceKey = "L_ToolBar_" + shortcutId.Replace("Tool.SwitchTo", "");
            name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;

            // 4. 尝试菜单前缀 (L_Menu_...)
            resourceKey = "L_Menu_" + shortcutId.Replace(".", "_");
            name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;

            return shortcutId;
        }
    }
}
