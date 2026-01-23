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
using System.Windows.Threading;

namespace TabPaint
{
    public partial class AdjustBCEWindow : System.Windows.Window
    {
        private WriteableBitmap _originalFullBitmap; // 原始大图 (只读备份)
        private WriteableBitmap _thumbnailBitmap;    // 用于预览的小图 (读写)
        private WriteableBitmap _thumbnailSource;    // 小图的纯净备份 (只读)
        private readonly int[] _redCounts = new int[256];
        private readonly int[] _greenCounts = new int[256];
        private readonly int[] _blueCounts = new int[256];

        private Image _previewLayer; // 主窗口的预览控件

        private bool _isDragging = false;
        private bool _isUpdatingFromTextBox = false;
        private DispatcherTimer _updateTimer;

        // 保存最终参数，供外部使用
        public double FinalBrightness { get; private set; }
        public double FinalContrast { get; private set; }
        public double FinalExposure { get; private set; }

        public AdjustBCEWindow(WriteableBitmap fullBitmap, Image previewLayer)
        {
            InitializeComponent();
            _originalFullBitmap = fullBitmap;
            _previewLayer = previewLayer;

            CreateThumbnail(fullBitmap);

            // 2. 初始化预览层
            if (_previewLayer != null)
            {
                _previewLayer.Source = _thumbnailBitmap;
                _previewLayer.Width = fullBitmap.PixelWidth;  // 强制拉伸到原图大小
                _previewLayer.Height = fullBitmap.PixelHeight;
                _previewLayer.Visibility = Visibility.Visible;
            }

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) }; // 30ms 足够快
            _updateTimer.Tick += (s, e) => { _updateTimer.Stop(); ApplyPreview(); };

            UpdateTextBoxesFromSliders();
            ApplyPreview(); // 初始应用一次
        }

        private void CreateThumbnail(WriteableBitmap source)
        {
            double maxDim = 1920;
            double scale = 1.0;
            if (source.PixelWidth > maxDim || source.PixelHeight > maxDim)
            {
                scale = Math.Min(maxDim / source.PixelWidth, maxDim / source.PixelHeight);
            }

            int w = (int)(source.PixelWidth * scale);
            int h = (int)(source.PixelHeight * scale);
            if (w < 1) w = 1; if (h < 1) h = 1;

            // 创建缩略图
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, w, h));
            }
            rtb.Render(visual);

            // 转换为 WriteableBitmap 以便后续快速像素操作
            _thumbnailSource = new WriteableBitmap(rtb);
            _thumbnailBitmap = _thumbnailSource.Clone(); // 用于实时修改
        }

        protected override void OnClosed(EventArgs e)
        {
            // 清理预览层
            if (_previewLayer != null)
            {
                _previewLayer.Source = null;
                _previewLayer.Visibility = Visibility.Collapsed;
            }
            base.OnClosed(e);
        }

        // --- 事件处理 (保持 UI 逻辑不变) ---
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
        private void Slider_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (sender is Slider s) s.Value = 0; }
        private void Reset_Click(object sender, RoutedEventArgs e) { BrightnessSlider.Value = 0; ContrastSlider.Value = 0; ExposureSlider.Value = 0; }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e) => e.Handled = new Regex("[^0-9.-]+").IsMatch(e.Text);

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            if (!_isUpdatingFromTextBox) UpdateTextBoxesFromSliders();
            ThrottlePreview();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (sender is TextBox tb && double.TryParse(tb.Text, out double val))
            {
                _isUpdatingFromTextBox = true;
                if (tb == BrightnessBox) BrightnessSlider.Value = val;
                else if (tb == ContrastBox) ContrastSlider.Value = val;
                else if (tb == ExposureBox) ExposureSlider.Value = val;
                _isUpdatingFromTextBox = false;
            }
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e) => _isDragging = true;
        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e) { _isDragging = false; ThrottlePreview(); }

        private void UpdateTextBoxesFromSliders()
        {
            BrightnessBox.Text = BrightnessSlider.Value.ToString("F0");
            ContrastBox.Text = ContrastSlider.Value.ToString("F0");
            ExposureBox.Text = ExposureSlider.Value.ToString("F1");
        }

        private void ThrottlePreview()
        {
            if (!_updateTimer.IsEnabled) _updateTimer.Start();
        }

        // --- 核心算法 ---

        private void ApplyPreview()
        {
            int stride = _thumbnailSource.BackBufferStride;
            int byteCount = _thumbnailSource.PixelHeight * stride;

            _thumbnailBitmap.Lock();
            // 1. 还原像素
            _thumbnailSource.CopyPixels(new Int32Rect(0, 0, _thumbnailSource.PixelWidth, _thumbnailSource.PixelHeight), _thumbnailBitmap.BackBuffer, byteCount, stride);

            // 2. 应用调整 (注意：ProcessBitmapUnsafe 现在需要返回 void 或处理后的状态，这里依然是原地修改)
            ProcessBitmapUnsafe(_thumbnailBitmap, BrightnessSlider.Value, ContrastSlider.Value, ExposureSlider.Value);

            // 3. 计算并更新直方图 (在 Lock 期间直接读取内存，最快)
            UpdateHistogramUnsafe(_thumbnailBitmap);

            _thumbnailBitmap.AddDirtyRect(new Int32Rect(0, 0, _thumbnailBitmap.PixelWidth, _thumbnailBitmap.PixelHeight));
            _thumbnailBitmap.Unlock();
        }

        private unsafe void UpdateHistogramUnsafe(WriteableBitmap bmp)
        {
            // 清空计数器
            Array.Clear(_redCounts, 0, 256);
            Array.Clear(_greenCounts, 0, 256);
            Array.Clear(_blueCounts, 0, 256);

            byte* ptr = (byte*)bmp.BackBuffer;
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = bmp.BackBufferStride;

            for (int y = 0; y < height; y++)
            {
                byte* row = ptr + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    _blueCounts[row[0]]++;
                    _greenCounts[row[1]]++;
                    _redCounts[row[2]]++;
                    row += 4;
                }
            }

            int max = 1;
            for (int i = 0; i < 256; i++)
            {
                if (_redCounts[i] > max) max = _redCounts[i];
                if (_greenCounts[i] > max) max = _greenCounts[i];
                if (_blueCounts[i] > max) max = _blueCounts[i];
            }


            UpdatePolyline(HistoRed, _redCounts, max);
            UpdatePolyline(HistoGreen, _greenCounts, max);
            UpdatePolyline(HistoBlue, _blueCounts, max);
        }

        private void UpdatePolyline(Polyline polyline, int[] counts, int maxVal)
        {
            PointCollection points = new PointCollection(258); // 256 + 2 for closing
            double height = HistogramGrid.Height; // 60
            double width = HistogramGrid.ActualWidth; // 窗口宽度减去边距

            // 如果窗口刚启动还未渲染，ActualWidth可能为0，给个默认值
            if (width <= 0) width = 332; // 380 - 48 (Padding)

            double stepX = width / 255.0;

            // 起始点 (左下角)
            points.Add(new Point(0, height));

            for (int i = 0; i < 256; i++)
            {
                // Y轴归一化: 0 count -> height (bottom), max count -> 0 (top)
                double normalizedY = height - ((double)counts[i] / maxVal * height);
                points.Add(new Point(i * stepX, normalizedY));
            }

            // 结束点 (右下角)
            points.Add(new Point(width, height));

            polyline.Points = points;
        }

        private static unsafe void ProcessBitmapUnsafe(WriteableBitmap bmp, double brightness, double contrast, double exposure)
        {
            // 预计算 LUT (Look-Up Table)
            // 避免在循环中进行复杂的 Math.Pow 计算
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
                // 钳制范围
                if (val > 255) val = 255;
                else if (val < 0) val = 0;
                lut[i] = (byte)val;
            }

            // 像素操作
            byte* basePtr = (byte*)bmp.BackBuffer;
            int stride = bmp.BackBufferStride;
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;

            // Parallel 处理小图可能优势不明显，但在大图上必备。
            // 为了通用性，这里保留 Parallel。
            Parallel.For(0, height, y =>
            {
                byte* row = basePtr + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    // BGRA 顺序
                    // 注意：通常 Alpha 通道不需要调整亮度对比度，这里只动 RGB
                    row[x * 4] = lut[row[x * 4]];     // B
                    row[x * 4 + 1] = lut[row[x * 4 + 1]]; // G
                    row[x * 4 + 2] = lut[row[x * 4 + 2]]; // R
                    // row[x * 4 + 3] is Alpha, keep as is
                }
            });
        }

        // 供外部调用的核心方法：应用到全分辨率大图
        public void ApplyToFullImage(WriteableBitmap targetBitmap)
        {
            // 确保在 UI 线程或正确上下文中 Lock
            targetBitmap.Lock();
            ProcessBitmapUnsafe(targetBitmap, BrightnessSlider.Value, ContrastSlider.Value, ExposureSlider.Value);
            targetBitmap.AddDirtyRect(new Int32Rect(0, 0, targetBitmap.PixelWidth, targetBitmap.PixelHeight));
            targetBitmap.Unlock();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 记录最终值
            FinalBrightness = BrightnessSlider.Value;
            FinalContrast = ContrastSlider.Value;
            FinalExposure = ExposureSlider.Value;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
