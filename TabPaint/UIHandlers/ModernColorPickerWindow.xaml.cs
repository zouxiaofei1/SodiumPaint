using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    public partial class ModernColorPickerWindow : Window
    {
        public Color SelectedColor { get; private set; }

        private bool _isDraggingSpectrum = false;
        private bool _isDraggingHue = false;
        private bool _isUpdatingInputs = false;

        private double _currentHue = 0;
        private double _currentSat = 1;
        private double _currentVal = 1;

        public ModernColorPickerWindow(Color initialColor)
        {
            InitializeComponent();
            SelectedColor = initialColor;

            // UI初始化
            OriginalColorRect.Fill = new SolidColorBrush(initialColor);
            RenderHueGradient();
            SetColorFromRgb(initialColor.R, initialColor.G, initialColor.B);

            // 确保第一次显示时UI位置正确
            Loaded += (s, e) => UpdateUI();
        }

        // 新增：无边框窗口拖动支持
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // 新增：基本颜色块点击处理
        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Background is SolidColorBrush brush)
            {
                var c = brush.Color;
                SetColorFromRgb(c.R, c.G, c.B);
                UpdateUI();
            }
        }

        #region Core Logic: HSV <-> RGB
        private void UpdateHueColorVisual()
        {
            var hueColor = ColorFromHsv(_currentHue, 1, 1);
            SpectrumBaseColor.Fill = new SolidColorBrush(hueColor);
        }

        private void SetColorFromRgb(byte r, byte g, byte b)
        {
            var c = System.Drawing.Color.FromArgb(r, g, b);
            _currentHue = c.GetHue();

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            _currentVal = max / 255.0;

            if (max == 0) _currentSat = 0;
            else _currentSat = (max - min) / max;

            UpdateHueColorVisual();
        }

        private Color GetRgbFromHsv()
        {
            return ColorFromHsv(_currentHue, _currentSat, _currentVal);
        }

        private void UpdateUI()
        {
            if (_isUpdatingInputs) return;
            _isUpdatingInputs = true;

            try
            {
                var c = GetRgbFromHsv();
                SelectedColor = c;
                NewColorRect.Fill = new SolidColorBrush(c);

                HexInput.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                RInput.Text = c.R.ToString();
                GInput.Text = c.G.ToString();
                BInput.Text = c.B.ToString();

                // 更新 Hue 滑块 (Vertical)
                if (HueSliderGrid.ActualHeight > 0)
                {
                    double h = HueSliderGrid.ActualHeight;
                    double hueY = (1 - (_currentHue / 360.0)) * h;

                    // 设置垂直位置
                    Canvas.SetTop(HueCursor, Math.Clamp(hueY, 0, h));

                    // 修复：设置水平居中 (Grid宽度 - 光标宽度) / 2
                    double centerX = (HueSliderGrid.ActualWidth - HueCursor.Width) / 2;
                    Canvas.SetLeft(HueCursor, centerX);
                }

                // 更新 Spectrum 光标
                if (SpectrumBaseColor.ActualWidth > 0)
                {
                    double w = SpectrumBaseColor.ActualWidth;
                    double h = SpectrumBaseColor.ActualHeight;

                    double specX = _currentSat * w;
                    double specY = (1 - _currentVal) * h;

                    Canvas.SetLeft(ColorCursor, specX);
                    Canvas.SetTop(ColorCursor, specY);
                }
            }
            finally
            {
                _isUpdatingInputs = false;
            }
        }
        #endregion

        #region UI Rendering
        private void RenderHueGradient()
        {
            // 注意：因为XAML里Image被放到了Grid里自适应，这里的bitmap大小可能需要稍大一点保证清晰度
            int w = 20;
            int h = 360;
            var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            int[] pixels = new int[w * h];

            for (int y = 0; y < h; y++)
            {
                double hue = 360 - (y * 360.0 / h);
                var color = ColorFromHsv(hue, 1, 1);
                int colorData = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;

                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = colorData;
                }
            }
            bitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
            HueSliderImage.Source = bitmap;
        }

        private static Color ColorFromHsv(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1 - saturation));
            byte q = Convert.ToByte(value * (1 - f * saturation));
            byte t = Convert.ToByte(value * (1 - (1 - f) * saturation));

            if (hi == 0) return Color.FromRgb(v, t, p);
            else if (hi == 1) return Color.FromRgb(q, v, p);
            else if (hi == 2) return Color.FromRgb(p, v, t);
            else if (hi == 3) return Color.FromRgb(p, q, v);
            else if (hi == 4) return Color.FromRgb(t, p, v);
            else return Color.FromRgb(v, p, q);
        }
        #endregion

        #region Interaction - Spectrum
        private void Spectrum_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 关键：阻止事件冒泡，防止被父级捕获
            e.Handled = true;
            _isDraggingSpectrum = true;

            var grid = sender as Grid;
            if (grid != null)
            {
                grid.CaptureMouse();
                UpdateSpectrumFromMouse(e.GetPosition(grid), grid.ActualWidth, grid.ActualHeight);
            }
        }

        private void Spectrum_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSpectrum)
            {
                var grid = sender as Grid;
                UpdateSpectrumFromMouse(e.GetPosition(grid), grid.ActualWidth, grid.ActualHeight);
            }
        }

        private void Spectrum_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSpectrum = false;
            if (sender is IInputElement el) el.ReleaseMouseCapture();
        }

        private void UpdateSpectrumFromMouse(Point p, double w, double h)
        {
            double x = Math.Clamp(p.X, 0, w);
            double y = Math.Clamp(p.Y, 0, h);

            _currentSat = x / w;
            _currentVal = 1 - (y / h);

            UpdateUI();
        }
        #endregion

        #region Interaction - Hue Slider
        private void Hue_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _isDraggingHue = true;

            var grid = sender as Grid;
            if (grid != null)
            {
                grid.CaptureMouse();
                // 修复：传递 Grid 的 ActualHeight
                UpdateHueFromMouse(e.GetPosition(grid), grid.ActualHeight);
            }
        }
        private void UpdateHueFromMouse(Point p, double h)
        {
            if (h <= 0) return;
            double y = Math.Clamp(p.Y, 0, h);

            // 计算 Hue (0在底部，360在顶部)
            _currentHue = 360 - (y / h * 360);
            _currentHue = Math.Clamp(_currentHue, 0, 360);

            UpdateHueColorVisual();
            UpdateUI();
        }

        private void Hue_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingHue)
            {
                var grid = sender as Grid;
                if (grid != null)
                    UpdateHueFromMouse(e.GetPosition(grid), grid.ActualHeight);
            }
        }


        private void Hue_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingHue = false;
            var grid = sender as Grid;
            grid?.ReleaseMouseCapture();
        }


        #endregion

        #region Text Input Handling
        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 修复：确保所有相关控件都已初始化，防止空引用崩溃
            if (HexInput == null || NewColorRect == null) return;
            if (_isUpdatingInputs) return;

            string hex = HexInput.Text.Trim('#');
            if (hex.Length == 6)
            {
                try
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    SetColorFromRgb(r, g, b);

                    var c = GetRgbFromHsv();
                    NewColorRect.Fill = new SolidColorBrush(c);
                    SelectedColor = c;
                }
                catch { }
            }
        }

        private void RGBInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 修复：必须检查所有涉及到的控件是否为 null
            // 在 InitializeComponent 过程中，Text="0" 会触发此事件，此时其他控件可能还没生成
            if (RInput == null || GInput == null || BInput == null || NewColorRect == null) return;

            if (_isUpdatingInputs) return;

            if (byte.TryParse(RInput.Text, out byte r) &&
                byte.TryParse(GInput.Text, out byte g) &&
                byte.TryParse(BInput.Text, out byte b))
            {
                SetColorFromRgb(r, g, b);
                var c = GetRgbFromHsv();
                NewColorRect.Fill = new SolidColorBrush(c);
                SelectedColor = c;
            }
        }
        #endregion

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
