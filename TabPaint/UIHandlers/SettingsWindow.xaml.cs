using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    public partial class SettingsWindow : Window
    {
        public string ProgramVersion { get; set; } 
        public SettingsWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    this.Close();
                }
            };
            this.Loaded += (s, e) => SetHighResIcon();
        }
        private void SetHighResIcon()
        {
            try
            {
                // 1. 指定你的 ICO 路径
                var iconUri = new Uri("pack://application:,,,/Resources/TabPaint.ico");

                // 2. 使用解码器读取 ICO 文件中的所有帧
                var decoder = BitmapDecoder.Create(iconUri, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);

                // 3. 筛选出宽度最大的一帧 (通常是 256x256)
                // 如果你想保险一点，也可以找最接近 64px 且大于 64px 的帧
                var bestFrame = decoder.Frames
                    .OrderByDescending(f => f.Width)
                    .FirstOrDefault();

                // 4. 赋值给 Image 控件
                if (bestFrame != null)
                {
                    AppIcon.Source = bestFrame;
                }
            }
            catch (Exception ex)
            {
                // 如果提取失败，保持 XAML 里的默认引用，或者记录日志
                System.Diagnostics.Debug.WriteLine("Icon load failed: " + ex.Message);
            }
        }
        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsPanel == null || AboutPanel == null) return;

            int index = NavListBox.SelectedIndex;
            if (index == 0) // 常规设置
            {
                SettingsPanel.Visibility = Visibility.Visible;
                AboutPanel.Visibility = Visibility.Collapsed;
            }
            else if (index == 1) // 关于
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
                AboutPanel.Visibility = Visibility.Visible;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 这里以后可以添加保存设置的逻辑
            this.DialogResult = true;
            this.Close();
        }
    }
}
