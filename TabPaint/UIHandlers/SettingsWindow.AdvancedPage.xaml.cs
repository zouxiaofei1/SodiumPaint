using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TabPaint.Pages
{
    public partial class AdvancedPage : UserControl
    {
        public AdvancedPage()
        {
            InitializeComponent();
        }
        private void OpenPlugins_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this) as SettingsWindow;
            if (win != null)
            {
                win.NavListBox.SelectedIndex = 5; // Plugins item index
            }
        }
        // 打开缓存文件夹
        private void OpenCacheFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 定位到 %LOCALAPPDATA%\TabPaint
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string cachePath = Path.Combine(localAppData, "TabPaint");

                // 如果目录不存在，先创建它（防止打开报错）
                if (!Directory.Exists(cachePath))
                {
                    Directory.CreateDirectory(cachePath);
                }

                // 使用资源管理器打开
                Process.Start(new ProcessStartInfo
                {
                    FileName = cachePath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception)
            {
                // 忽略错误，或添加简单的日志
            }
        }

        // 恢复出厂设置
        private void FactoryReset_Click(object sender, RoutedEventArgs e)
        {
            var result = FluentMessageBox.Show(
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset_Confirm"),
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset"),
              MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string tempBatPath = Path.Combine(Path.GetTempPath(), "tabpaint_reset.bat");

                // 创建一个批处理文件来删除数据并重启应用
                string batContent = $@"
                        @echo off
                        timeout /t 1 /nobreak > NUL
                        rd /s /q ""{appDataPath}""
                        start """" ""{currentExe}""
                        del ""%~f0""
                        ";
                File.WriteAllText(tempBatPath, batContent);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = tempBatPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                // 关闭当前应用
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show(
                   string.Format(LocalizationManager.GetString("L_Msg_ResetFailed"), ex.Message),
                   LocalizationManager.GetString("L_Common_Error"),
                   MessageBoxButton.OK);
            }
        }

        // 输入框失去焦点时进行数值校验
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (double.TryParse(textBox.Text, out double value))
                {
                    // 限制范围
                    if (value < 0) value = 0;
                    if (value > 5000) value = 5000;

                    textBox.Text = value.ToString("0");

                    // 显式更新绑定源
                    var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                    binding?.UpdateSource(); MainWindow.UndoRedoManager.CheckGlobalUndoMemory();
                }
                else
                {
                    textBox.Text = "0";
                }
            }
        }

        // 限制输入只能是数字
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
