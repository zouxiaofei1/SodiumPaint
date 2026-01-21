using System;
using System.Text.RegularExpressions; // 引入正则
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TabPaint
{
    public partial class AdjustBCEWindow : System.Windows.Window
    {
        private WriteableBitmap _originalBitmap;
        private WriteableBitmap _previewBitmap;
        private Image _targetImage;

        private bool _isDragging = false;
        private bool _isUpdatingFromTextBox = false;

        private DispatcherTimer _updateTimer;

        public WriteableBitmap FinalBitmap { get; private set; }
        public double Brightness => BrightnessSlider.Value;
        public double Contrast => ContrastSlider.Value;
        public double Exposure => ExposureSlider.Value;

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        public AdjustBCEWindow(WriteableBitmap bitmapForPreview, Image targetImage)
        {
            InitializeComponent();

            _originalBitmap = bitmapForPreview.Clone();
            _targetImage = targetImage;
            _previewBitmap = bitmapForPreview;

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 稍微加快响应速度适应打字
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            // 初始化 TextBox 的值（防止初始为空）
            UpdateTextBoxesFromSliders();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            _updateTimer.Stop();
            ApplyPreview();
        }
        private void Slider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                slider.Value = 0;
            }
        }
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded) return;

            if (!_isUpdatingFromTextBox)
            {
                UpdateTextBoxesFromSliders();
            }
            ApplyPreviewWithThrottle();
        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!this.IsLoaded) return;

            TextBox tb = sender as TextBox;
            if (tb == null) return;

            // 标记开始从 TextBox 更新 Slider
            _isUpdatingFromTextBox = true;

            if (double.TryParse(tb.Text, out double val))
            {
                if (tb == BrightnessBox) BrightnessSlider.Value = val;
                else if (tb == ContrastBox) ContrastSlider.Value = val;
                else if (tb == ExposureBox) ExposureSlider.Value = val;
            }
            // 解析失败（比如空字符串或只有负号）时不更新 Slider，保持原值

            _isUpdatingFromTextBox = false;
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.-]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            BrightnessSlider.Value = 0;
            ContrastSlider.Value = 0;
            ExposureSlider.Value = 0;
        }
        private void UpdateTextBoxesFromSliders()
        {
            // 亮度对比度取整显示，曝光保留一位小数
            BrightnessBox.Text = BrightnessSlider.Value.ToString("F0");
            ContrastBox.Text = ContrastSlider.Value.ToString("F0");
            ExposureBox.Text = ExposureSlider.Value.ToString("F1");
        }

        // 拖拽相关
        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
            _updateTimer.Stop();
            ApplyPreview(); // 拖拽结束强制刷新一次
        }

        private void ApplyPreviewWithThrottle()
        {
            // 只有当定时器没在跑的时候才启动它
            if (!_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
            }
        }

        private void ApplyPreview()
        {
            // 这里逻辑不变
            int stride = _originalBitmap.BackBufferStride;
            int byteCount = _originalBitmap.PixelHeight * stride;

            byte[] pixelData = new byte[byteCount];

            _originalBitmap.CopyPixels(pixelData, stride, 0);
            _previewBitmap.WritePixels(new Int32Rect(0, 0, _originalBitmap.PixelWidth, _originalBitmap.PixelHeight), pixelData, stride, 0);

            AdjustImage(_previewBitmap, BrightnessSlider.Value, ContrastSlider.Value, ExposureSlider.Value);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            FinalBitmap = _previewBitmap;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _targetImage.Source = _originalBitmap;
            DialogResult = false;
            Close();
        }

        // 保持之前的 Parallel 算法不变
        private void AdjustImage(WriteableBitmap bmp, double brightness, double contrast, double exposure)
        {
            double brAdj = brightness;
            double ctAdj = (100.0 + contrast) / 100.0;
            ctAdj *= ctAdj;
            double expAdj = Math.Pow(2, exposure);

            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                double val = i + brAdj;
                val = ((((val / 255.0) - 0.5) * ctAdj) + 0.5) * 255.0;
                val *= expAdj;
                if (val > 255) val = 255;
                if (val < 0) val = 0;
                lut[i] = (byte)val;
            }

            bmp.Lock();
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;
                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;

                Parallel.For(0, height, y =>
                {
                    byte* row = basePtr + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        row[x * 4] = lut[row[x * 4]];
                        row[x * 4 + 1] = lut[row[x * 4 + 1]];
                        row[x * 4 + 2] = lut[row[x * 4 + 2]];
                    }
                });
            }
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
        }
    }
}
