using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TabPaint
{
    public partial class AdjustTTSWindow : Window
    {
        private WriteableBitmap _fullSizeSource;

        private WriteableBitmap _smallSource;
        private WriteableBitmap _smallTarget;

        private Image _previewLayer; // 主窗口的预览控件
        private readonly int[] _redCounts = new int[256];
        private readonly int[] _greenCounts = new int[256];
        private readonly int[] _blueCounts = new int[256];
        public double Temperature => TemperatureSlider.Value;
        public double Tint => TintSlider.Value;
        public double Saturation => SaturationSlider.Value;

        // 构造函数接收预览层控件
        public AdjustTTSWindow(WriteableBitmap sourceBitmap, Image previewLayer)
        {
            InitializeComponent();

            _fullSizeSource = sourceBitmap;
            _previewLayer = previewLayer;

            InitSmallBitmaps();

            // 2. 设置预览层
            if (_previewLayer != null)
            {
                _previewLayer.Source = _smallTarget; // 绑定到计算结果
                _previewLayer.Width = _fullSizeSource.PixelWidth; // 强制拉伸到原图大小
                _previewLayer.Height = _fullSizeSource.PixelHeight;
                _previewLayer.Visibility = Visibility.Visible;
            }

            // 3. 初始应用一次
            ApplyPreview();
        }

        private void InitSmallBitmaps()
        {
            double maxDim = 1920;
            double scale = 1.0;
            if (_fullSizeSource.PixelWidth > maxDim || _fullSizeSource.PixelHeight > maxDim)
            {
                scale = Math.Min(maxDim / _fullSizeSource.PixelWidth, maxDim / _fullSizeSource.PixelHeight);
            }

            int w = (int)(_fullSizeSource.PixelWidth * scale);
            int h = (int)(_fullSizeSource.PixelHeight * scale);
            if (w < 1) w = 1;
            if (h < 1) h = 1;

            var scaledSource = new TransformedBitmap(_fullSizeSource, new ScaleTransform(scale, scale));

            _smallSource = new WriteableBitmap(scaledSource);
            _smallTarget = _smallSource.Clone();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        // 窗口关闭/取消时清理
        protected override void OnClosed(EventArgs e)
        {
            if (_previewLayer != null)
            {
                _previewLayer.Source = null;
                _previewLayer.Visibility = Visibility.Collapsed;
            }
            base.OnClosed(e);
        }

        // --- 滑块事件 ---
        private void Slider_DragStarted(object sender, DragStartedEventArgs e) { }

        // 拖动过程中如果性能允许也可以实时刷新，这里还是用 ValueChanged
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded) ApplyPreview();
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ApplyPreview();
        }

        private void Slider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider) slider.Value = 0;
        }

        // --- 核心预览逻辑 ---
        private void ApplyPreview()
        {
            if (_smallSource == null || _smallTarget == null) return;

            // 1. 每次都从干净的 _smallSource 复制到 _smallTarget
            // 这样避免误差累积，且逻辑简单
            int stride = _smallSource.BackBufferStride;
            int byteCount = _smallSource.PixelHeight * stride;

            byte[] pixelData = new byte[byteCount];
            _smallSource.CopyPixels(pixelData, stride, 0);
            _smallTarget.WritePixels(new Int32Rect(0, 0, _smallSource.PixelWidth, _smallSource.PixelHeight), pixelData, stride, 0);

            // 2. 在 _smallTarget 上执行算法
            AdjustImage(_smallTarget, Temperature, Tint, Saturation); UpdateHistogram(_smallTarget);
        }
        private void UpdateHistogram(WriteableBitmap bmp)
        {
            // 清空计数
            Array.Clear(_redCounts, 0, 256);
            Array.Clear(_greenCounts, 0, 256);
            Array.Clear(_blueCounts, 0, 256);

            // 锁定读取像素统计
            bmp.Lock();
            unsafe
            {
                byte* ptr = (byte*)bmp.BackBuffer;
                int width = bmp.PixelWidth;
                int height = bmp.PixelHeight;
                int stride = bmp.BackBufferStride;

                // 简单采样遍历 (对于缩略图非常快)
                for (int y = 0; y < height; y++)
                {
                    byte* row = ptr + (y * stride);
                    for (int x = 0; x < width; x++)
                    {
                        // BGRA
                        _blueCounts[row[0]]++;
                        _greenCounts[row[1]]++;
                        _redCounts[row[2]]++;
                        row += 4;
                    }
                }
            }
            bmp.Unlock();

            // 寻找最大值 (归一化用)
            int max = 1;
            for (int i = 0; i < 256; i++)
            {
                if (_redCounts[i] > max) max = _redCounts[i];
                if (_greenCounts[i] > max) max = _greenCounts[i];
                if (_blueCounts[i] > max) max = _blueCounts[i];
            }

            // 更新 UI (Polyline)
            UpdatePolyline(HistoRed, _redCounts, max);
            UpdatePolyline(HistoGreen, _greenCounts, max);
            UpdatePolyline(HistoBlue, _blueCounts, max);
        }

        private void UpdatePolyline(Polyline polyline, int[] counts, int maxVal)
        {
            PointCollection points = new PointCollection(258);
            double height = HistogramGrid.Height;
            double width = HistogramGrid.ActualWidth;
            if (width <= 0) width = 332; // 默认宽度

            double stepX = width / 255.0;

            points.Add(new Point(0, height)); // 左下

            for (int i = 0; i < 256; i++)
            {
                double normalizedY = height - ((double)counts[i] / maxVal * height);
                points.Add(new Point(i * stepX, normalizedY));
            }

            points.Add(new Point(width, height)); // 右下

            polyline.Points = points;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            AdjustImage(_fullSizeSource, Temperature, Tint, Saturation);

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 取消：什么都不做，OnClosed 会隐藏预览层
            // 原图从未被修改
            DialogResult = false;
            Close();
        }
        private void AdjustImage(WriteableBitmap bmp, double temperature, double tint, double saturation)
        {
            // 参数预计算
            double tempAdj = temperature / 2.0;
            double tintAdj = tint / 2.0;
            double satAdj = (saturation + 100.0) / 100.0;

            bmp.Lock();
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;
                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;

                // 并行处理
                Parallel.For(0, height, y =>
                {
                    byte* row = basePtr + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        // 注意像素顺序通常是 BGRA
                        byte b = row[x * 4];
                        byte g = row[x * 4 + 1];
                        byte r = row[x * 4 + 2];
                        // alpha = row[x * 4 + 3];

                        double nr = r + tempAdj;
                        double ng = g + tintAdj;
                        double nb = b - tempAdj;

                        if (saturation != 0)
                        {
                            // 亮度加权 (Luma)
                            double luminance = 0.299 * nr + 0.587 * ng + 0.114 * nb;
                            nr = luminance + satAdj * (nr - luminance);
                            ng = luminance + satAdj * (ng - luminance);
                            nb = luminance + satAdj * (nb - luminance);
                        }

                        // 钳位
                        row[x * 4 + 2] = (byte)(nr < 0 ? 0 : (nr > 255 ? 255 : nr));
                        row[x * 4 + 1] = (byte)(ng < 0 ? 0 : (ng > 255 ? 255 : ng));
                        row[x * 4] = (byte)(nb < 0 ? 0 : (nb > 255 ? 255 : nb));
                    }
                });
            }
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9-]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null) return;

            // 利用 Tag 属性获取对应的 Slider 控件
            Slider targetSlider = textBox.Tag as Slider;
            if (targetSlider == null) return;

            string input = textBox.Text;

            if (string.IsNullOrEmpty(input) || input == "-")
                return;
            if (double.TryParse(input, out double result))
            {
                if (result > targetSlider.Maximum) result = targetSlider.Maximum;
                if (result < targetSlider.Minimum) result = targetSlider.Minimum;

                if (Math.Abs(targetSlider.Value - result) > 0.01)
                {
                    targetSlider.Value = result;
                }
            }
        }
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            TemperatureSlider.Value = 0;
            TintSlider.Value = 0;
            SaturationSlider.Value = 0;
        }
    }
}
