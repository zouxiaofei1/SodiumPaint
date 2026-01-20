using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    public partial class SettingsWindow : Window
    {
        public string ProgramVersion { get; set; } = "v0.9.2"; // 默认值，实际运行时由主程序传入

        public SettingsWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.SourceInitialized += SettingsWindow_SourceInitialized;
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) this.Close();
            };

            this.Loaded += (s, e) => SetHighResIcon();
        }
        private void SettingsWindow_SourceInitialized(object sender, EventArgs e)
        {
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;


            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);
        }
        private void SetHighResIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/Resources/TabPaint.ico");
                var decoder = BitmapDecoder.Create(iconUri, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                var bestFrame = decoder.Frames.OrderByDescending(f => f.Width).FirstOrDefault();

                if (bestFrame != null)
                {
                    AppIcon.Source = bestFrame;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Icon load failed: " + ex.Message);
            }
        }
        private void FactoryReset_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset_Confirm"),
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset"),
              MessageBoxButton.YesNo,
              MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // 2. 获取 AppData 路径
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");

                // 获取当前程序的可执行文件路径，用于重启
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string tempBatPath = Path.Combine(Path.GetTempPath(), "tabpaint_reset.bat");

                string batContent = $@"
@echo off
timeout /t 1 /nobreak > NUL
rd /s /q ""{appDataPath}""
start """" ""{currentExe}""
del ""%~f0""
";
                File.WriteAllText(tempBatPath, batContent);

                // 4. 启动批处理脚本
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = tempBatPath,
                    UseShellExecute = true,
                    CreateNoWindow = true, // 不显示黑框
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                // 5. 关闭当前程序
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show(
       string.Format(LocalizationManager.GetString("L_Msg_ResetFailed"), ex.Message),
       LocalizationManager.GetString("L_Common_Error"),
       MessageBoxButton.OK);
            }
        }
        private bool _isInternalChange = false; // 防止两个 ListBox 互相清空时触发死循环

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 1. 确保控件都已加载（避免初始化时的空引用）
            if (GeneralPanel == null || PaintPanel == null || ViewPanel == null ||
                ShortcutPanel == null || AdvancedPanel == null || AboutPanel == null) return;

            if (_isInternalChange) return;

            // 确定是哪个 ListBox 触发的
            System.Windows.Controls.ListBox source = sender as System.Windows.Controls.ListBox;
            if (source.SelectedIndex == -1) return; // 如果是由于代码清空导致的触发，不执行逻辑

            _isInternalChange = true;

            // 2. 互斥逻辑：点主菜单则清空底部，点底部则清空主菜单
            if (source == NavListBox)
            {
                BottomListBox.SelectedIndex = -1;
            }
            else
            {
                NavListBox.SelectedIndex = -1;
            }

            // 3. 先隐藏所有面板
            GeneralPanel.Visibility = Visibility.Collapsed;
            PaintPanel.Visibility = Visibility.Collapsed;
            ViewPanel.Visibility = Visibility.Collapsed;
            ShortcutPanel.Visibility = Visibility.Collapsed;
            AdvancedPanel.Visibility = Visibility.Collapsed;
            AboutPanel.Visibility = Visibility.Collapsed;

            // 4. 根据来源和索引显示面板
            if (source == NavListBox)
            {
                switch (NavListBox.SelectedIndex)
                {
                    case 0: GeneralPanel.Visibility = Visibility.Visible; break;  // 通用
                    case 1: PaintPanel.Visibility = Visibility.Visible; break;    // 画图设置
                    case 2: ViewPanel.Visibility = Visibility.Visible; break;     // 看图设置
                    case 3: ShortcutPanel.Visibility = Visibility.Visible; break; // 快捷键
                    case 4: AdvancedPanel.Visibility = Visibility.Visible; break; // 高级
                }
            }
            else if (source == BottomListBox)
            {
                // 底部列表只有一个“关于”项，索引永远是 0
                AboutPanel.Visibility = Visibility.Visible;
            }
            _isInternalChange = false;
        }
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (double.TryParse(textBox.Text, out double value))
                {
                    if (value < 0) value = 0;
                    if (value > 5000) value = 5000;
                    textBox.Text = value.ToString("0"); // 修正文本框显示

                    // 手动触发一次绑定更新，确保后端数据也被修正
                    var binding = textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                    binding?.UpdateSource();
                }
                else
                {
                    textBox.Text = "0"; // 如果输入非法（比如全是空格），重置为0
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // 正则表达式：只允许数字
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private void ResetShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var result = FluentMessageBox.Show("确定要将所有快捷键恢复为默认设置吗？此操作无法撤销。",
                             "确认重置",
                             MessageBoxButton.YesNo
                            );

            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.Instance.Current.ResetShortcutsToDefault();
            }
        }


        // --- 新增代码开始 ---

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                string url = e.Uri.AbsoluteUri;

                // 处理帮助文档（空链接或特殊标记）
                if (string.IsNullOrEmpty(url) || url.StartsWith("cmd://help"))
                {
                    FluentMessageBox.Show("帮助文档正在编写中，敬请期待！", "提示", MessageBoxButton.OK);
                    e.Handled = true;
                    return;
                }

                // 调用系统默认浏览器打开链接
                // .NET Core / .NET 5+ 需要设置 UseShellExecute = true 才能打开 URL
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show($"无法打开链接: {ex.Message}", "错误", MessageBoxButton.OK);
            }
        }

    }
}
