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
        private enum ColorMode { RGB, HSV }
        private ColorMode _currentMode = ColorMode.RGB;
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
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    this.Close();
                }
            };
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

                // 确保 NewColorRect 不为空 (防止初始化时的 null 引用)
                if (NewColorRect != null)
                    NewColorRect.Fill = new SolidColorBrush(c);

                if (HexInput != null)
                    HexInput.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";

                // --- 修改开始：根据模式更新输入框数值 ---
                if (Input1 != null && Input2 != null && Input3 != null)
                {
                    if (_currentMode == ColorMode.RGB)
                    {
                        Input1.Text = c.R.ToString();
                        Input2.Text = c.G.ToString();
                        Input3.Text = c.B.ToString();
                    }
                    else // HSV 模式
                    {
                        // Hue: 0-360, Sat: 0-100, Val: 0-100
                        Input1.Text = Math.Round(_currentHue).ToString();
                        Input2.Text = Math.Round(_currentSat * 100).ToString();
                        Input3.Text = Math.Round(_currentVal * 100).ToString();
                    }
                }
                // --- 修改结束 ---

                // ... 原有的 Hue 滑块和 Spectrum 光标更新代码保持不变 ...
                // 更新 Hue 滑块 (Vertical)
                if (HueSliderGrid.ActualHeight > 0)
                {
                    // ... (保持原样)
                    double h = HueSliderGrid.ActualHeight;
                    double hueY = (1 - (_currentHue / 360.0)) * h;
                    Canvas.SetTop(HueCursor, Math.Clamp(hueY, 0, h));
                    double centerX = (HueSliderGrid.ActualWidth - HueCursor.Width) / 2;
                    Canvas.SetLeft(HueCursor, centerX);
                }

                if (SpectrumBaseColor.ActualWidth > 0)
                {
                    // ... (保持原样)
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

        // 统一处理 RGB 或 HSV 的输入变化
        private void NumericInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingInputs) return;
            if (Input1 == null || Input2 == null || Input3 == null || NewColorRect == null) return;

            // 尝试解析三个输入框的值
            if (double.TryParse(Input1.Text, out double v1) &&
                double.TryParse(Input2.Text, out double v2) &&
                double.TryParse(Input3.Text, out double v3))
            {
                if (_currentMode == ColorMode.RGB)
                {
                    // RGB 模式：限制在 0-255
                    byte r = (byte)Math.Clamp(v1, 0, 255);
                    byte g = (byte)Math.Clamp(v2, 0, 255);
                    byte b = (byte)Math.Clamp(v3, 0, 255);

                    SetColorFromRgb(r, g, b); // 这会反算 H,S,V 并更新 UI (UpdateUI)
                }
                else
                {
                    // HSV 模式
                    // v1 (Hue): 0-360
                    // v2 (Sat): 0-100
                    // v3 (Val): 0-100

                    _currentHue = Math.Clamp(v1, 0, 360);
                    _currentSat = Math.Clamp(v2, 0, 100) / 100.0;
                    _currentVal = Math.Clamp(v3, 0, 100) / 100.0;

                    UpdateHueColorVisual(); // 更新色谱底色

                    // 手动触发 UI 更新 (但不更新输入框本身，以免打断输入，这里需要特殊处理)
                    // 简单起见，我们调用 UpdateUI，但为了防止光标跳动，可以在 UpdateUI 里加判断
                    // 或者直接在这里更新 SelectedColor 和 Hex

                    var c = GetRgbFromHsv();
                    SelectedColor = c;
                    NewColorRect.Fill = new SolidColorBrush(c);

                    // 只更新 Hex 和 Canvas 光标，不回写 Input 框，防止用户输到一半被重置
                    _isUpdatingInputs = true;
                    HexInput.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";

                    // 更新光标位置
                    if (SpectrumBaseColor.ActualWidth > 0)
                    {
                        double w = SpectrumBaseColor.ActualWidth;
                        double h = SpectrumBaseColor.ActualHeight;
                        Canvas.SetLeft(ColorCursor, _currentSat * w);
                        Canvas.SetTop(ColorCursor, (1 - _currentVal) * h);
                    }
                    if (HueSliderGrid.ActualHeight > 0)
                    {
                        double hGrid = HueSliderGrid.ActualHeight;
                        Canvas.SetTop(HueCursor, (1 - (_currentHue / 360.0)) * hGrid);
                    }

                    _isUpdatingInputs = false;
                }
            }
        }
        private void ColorModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Label1 == null) return; // 防止初始化时崩溃

            if (ColorModeCombo.SelectedIndex == 0)
            {
                _currentMode = ColorMode.RGB;
                Label1.Text = "红 (R)";
                Label2.Text = "绿 (G)";
                Label3.Text = "蓝 (B)";
            }
            else
            {
                _currentMode = ColorMode.HSV;
                Label1.Text = "色调";
                Label2.Text = "饱和";
                Label3.Text = "亮度"; // 或 "值"
            }

            // 切换后立即刷新输入框里的数值格式
            UpdateUI();
        }

        #endregion
        private List<Color> _customColors = new List<Color>();

        private void AddCustomColor_Click(object sender, RoutedEventArgs e)
        {
            // 获取当前选中的颜色
            var newColor = SelectedColor;

            // 如果已经存在，就不添加了(可选逻辑)
            if (_customColors.Contains(newColor)) return;

            // 添加到列表
            _customColors.Insert(0, newColor); // 插入到最前面

            // 限制最大数量为16
            if (_customColors.Count > 16)
                _customColors.RemoveAt(16);

            // 刷新 UI
            RenderCustomColors();
        }

        private void RenderCustomColors()
        {
            for (int i = 0; i < CustomColorsGrid.Children.Count; i++)
            {
                if (CustomColorsGrid.Children[i] is Grid slot)
                {
                    slot.Children.Clear(); // 清空内容

                    if (i < _customColors.Count)
                    {
                        // === 有颜色 ===
                        var c = _customColors[i];
                        var btn = new Button
                        {
                            // 【关键修改】这里改成引用新的 MiniColorSwatch 样式
                            Style = (Style)FindResource("MiniColorSwatch"),

                            Background = new SolidColorBrush(c),
                            ToolTip = $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                        };

                        slot.Children.Add(btn);
                    }
                    else
                    {
                        // === 空槽位 (虚线) ===
                        var dashedCircle = new System.Windows.Shapes.Ellipse
                        {
                            Stroke = new SolidColorBrush(Color.FromRgb(187, 187, 187)),
                            StrokeThickness = 1,
                            StrokeDashArray = new DoubleCollection { 3, 2 }
                        };
                        slot.Children.Add(dashedCircle);
                    }
                }
            }
        }




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
