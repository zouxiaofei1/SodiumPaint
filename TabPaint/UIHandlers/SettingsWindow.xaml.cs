using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;

namespace TabPaint
{
    public partial class SettingsWindow : Window
    {
        public string ProgramVersion { get; set; } = "v0.8.2"; // 默认值，实际运行时由主程序传入

        public SettingsWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) this.Close();
            };

            this.Loaded += (s, e) => SetHighResIcon();
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
            // 1. 弹出确认警告
            var result = System.Windows.MessageBox.Show(
                "此操作将删除所有缓存、临时文件和用户配置，并将软件恢复到初始状态。\n\n确定要继续吗？",
                "恢复出厂设置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // 2. 获取 AppData 路径
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");

                // 获取当前程序的可执行文件路径，用于重启
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;

                // 3. 构建一个临时的批处理命令
                // 逻辑：
                // (1) 等待 1 秒让主程序完全退出
                // (2) 强制删除 TabPaint 文件夹
                // (3) 重新启动主程序
                // (4) 删除脚本自己
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
                System.Windows.MessageBox.Show($"重置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 确保控件都已加载（避免初始化时的空引用）
            if (GeneralPanel == null || PaintPanel == null || ViewPanel == null ||
                ShortcutPanel == null || AdvancedPanel == null || AboutPanel == null) return;

            // 1. 先隐藏所有面板
            GeneralPanel.Visibility = Visibility.Collapsed;
            PaintPanel.Visibility = Visibility.Collapsed;
            ViewPanel.Visibility = Visibility.Collapsed;
            ShortcutPanel.Visibility = Visibility.Collapsed;
            AdvancedPanel.Visibility = Visibility.Collapsed;
            AboutPanel.Visibility = Visibility.Collapsed;

            // 2. 根据索引显示对应面板
            int index = NavListBox.SelectedIndex;
            switch (index)
            {
                case 0: GeneralPanel.Visibility = Visibility.Visible; break;  // 通用
                case 1: PaintPanel.Visibility = Visibility.Visible; break;    // 画图设置
                case 2: ViewPanel.Visibility = Visibility.Visible; break;     // 看图设置
                case 3: ShortcutPanel.Visibility = Visibility.Visible; break; // 快捷键
                case 4: AdvancedPanel.Visibility = Visibility.Visible; break; // 高级
                case 5: AboutPanel.Visibility = Visibility.Visible; break;    // 关于
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void ResetShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show("确定要将所有快捷键恢复为默认设置吗？此操作无法撤销。",
                             "确认重置",
                             MessageBoxButton.YesNo,
                             MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.Instance.Current.ResetShortcutsToDefault();

                // 强制刷新 UI (如果 Binding 没有自动更新，可能需要手动刷新一下 ShortcutPanel)
                // 通常如果 AppSettings 实现了 INotifyPropertyChanged，UI 会自动变。
                // 如果 UI 没变，可以尝试重新设置 DataContext 或让 ScrollViewer 重新布局。
            }
        }
    }
}
