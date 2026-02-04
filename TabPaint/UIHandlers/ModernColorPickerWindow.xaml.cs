using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    public partial class ModernColorPickerWindow : Window
    {
        private static readonly Dictionary<Color, string> StandardColorNames = new Dictionary<Color, string>
        {
            { Color.FromRgb(255, 255, 255), "L_Color_White" },
            { Color.FromRgb(255, 204, 204), "L_Color_LightRed" },
            { Color.FromRgb(255, 229, 204), "L_Color_LightOrange" },
            { Color.FromRgb(255, 255, 204), "L_Color_LightYellow" },
            { Color.FromRgb(204, 255, 204), "L_Color_MintGreen" },
            { Color.FromRgb(204, 255, 255), "L_Color_LightCyan" },
            { Color.FromRgb(204, 229, 255), "L_Color_LightBlue" },
            { Color.FromRgb(229, 204, 255), "L_Color_Lavender" },
            { Color.FromRgb(255, 204, 255), "L_Color_LightPink" },
            { Color.FromRgb(245, 222, 179), "L_Color_Wheat" },
            { Color.FromRgb(192, 192, 192), "L_Color_Silver" },
            { Color.FromRgb(255, 0, 0), "L_Color_Red" },
            { Color.FromRgb(255, 128, 0), "L_Color_Orange" },
            { Color.FromRgb(255, 255, 0), "L_Color_Yellow" },
            { Color.FromRgb(0, 255, 0), "L_Color_Lime" },
            { Color.FromRgb(0, 255, 255), "L_Color_Cyan" },
            { Color.FromRgb(0, 128, 255), "L_Color_Azure" },
            { Color.FromRgb(128, 0, 255), "L_Color_Violet" },
            { Color.FromRgb(255, 0, 255), "L_Color_Magenta" },
            { Color.FromRgb(210, 105, 30), "L_Color_Chocolate" },
            { Color.FromRgb(128, 128, 128), "L_Color_Gray" },
            { Color.FromRgb(204, 0, 0), "L_Color_DeepRed" },
            { Color.FromRgb(204, 102, 0), "L_Color_DeepOrange" },
            { Color.FromRgb(255, 215, 0), "L_Color_Gold" },
            { Color.FromRgb(0, 128, 0), "L_Color_Green" },
            { Color.FromRgb(0, 128, 128), "L_Color_Teal" },
            { Color.FromRgb(0, 0, 255), "L_Color_Blue" },
            { Color.FromRgb(128, 0, 128), "L_Color_Purple" },
            { Color.FromRgb(199, 21, 133), "L_Color_DeepPink" },
            { Color.FromRgb(160, 82, 45), "L_Color_Ochre" },
            { Color.FromRgb(0, 0, 0), "L_Color_Black" },
            { Color.FromRgb(128, 0, 0), "L_Color_Maroon" },
            { Color.FromRgb(102, 51, 0), "L_Color_DeepBrown" },
            { Color.FromRgb(128, 128, 0), "L_Color_Olive" },
            { Color.FromRgb(0, 64, 0), "L_Color_ForestGreen" },
            { Color.FromRgb(0, 64, 64), "L_Color_RockCyan" },
            { Color.FromRgb(0, 0, 128), "L_Color_Navy" },
            { Color.FromRgb(51, 0, 102), "L_Color_Indigo" },
            { Color.FromRgb(75, 0, 130), "L_Color_DeepViolet" },
            { Color.FromRgb(62, 39, 35), "L_Color_Coffee" }
        };

        private string GetFriendlyColorName(Color target)
        {
            Color bestMatch = Color.FromRgb(0, 0, 0);
            double minDiff = double.MaxValue;

            foreach (var standard in StandardColorNames.Keys)
            {
                // 使用欧几里得距离计算颜色相似度
                double diff = Math.Pow(target.R - standard.R, 2) +
                              Math.Pow(target.G - standard.G, 2) +
                              Math.Pow(target.B - standard.B, 2);

                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestMatch = standard;
                }
            }

            string nameKey = StandardColorNames[bestMatch];
            string localizedName = LocalizationManager.GetString(nameKey);

            // 如果差异较大，则显示“接近 [颜色名]”
            if (minDiff > 500)
            {
                return $"{localizedName} ";
            }
            return localizedName;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            MicaAcrylicManager.ApplyEffect(this);
        }

        public Color SelectedColor { get; private set; }
        private enum ColorMode { RGB, HSV }
        private ColorMode _currentMode = ColorMode.RGB;
        private bool _isDraggingSpectrum = false;
        private bool _isDraggingAlpha = false; // 新增
        private bool _isDraggingHue = false;
        private bool _isUpdatingInputs = false;

        private double _currentHue = 0;
        private double _currentSat = 1;
        private double _currentVal = 1;
        private double _currentAlpha = 255; // 新增：0-255

        public ModernColorPickerWindow(Color initialColor)
        {
            InitializeComponent();
            SelectedColor = initialColor;

            OriginalColorRect.Fill = new SolidColorBrush(initialColor);
            RenderHueGradient();
            SetColorFromRgb(initialColor.R, initialColor.G, initialColor.B);
            LoadCustomColorsFromSettings();
            this.MouseDown += (s, e) => { Keyboard.ClearFocus(); };
            this.KeyDown += (s, e) => { if (e.Key == Key.Escape) { this.Close(); } };
            Loaded += (s, e) =>
            {
                UpdateUI();
                UpdateBasicColorTooltips();
            };
        }

        private void UpdateBasicColorTooltips()
        {
            foreach (var child in BasicColorsGrid.Children)
            {
                if (child is Button btn && btn.Background is SolidColorBrush brush)
                {
                    btn.ToolTip = GetFriendlyColorName(brush.Color);
                }
            }
        }

        private void LoadCustomColorsFromSettings()
        {
            var savedHexColors = SettingsManager.Instance.Current.CustomColors;

            _customColors.Clear();

            if (savedHexColors != null)
            {
                foreach (var hex in savedHexColors)
                {
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(hex);
                        _customColors.Add(color);
                    }
                    catch
                    {
                    }
                }
            }
            RenderCustomColors();
        }
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Background is SolidColorBrush brush)
            {
                var c = brush.Color;
                _currentAlpha = c.A; // 获取颜色的 Alpha
                SetColorFromRgb(c.R, c.G, c.B);
                UpdateUI();
            }
        }

        #region Core Logic: HSV <-> RGB
        private void UpdateHueColorVisual()
        {
            var hueColor = ColorFromHsv(_currentHue, 1, 1);
            SpectrumBaseColor.Fill = new SolidColorBrush(hueColor);
            var pureColor = GetRgbFromHsv(false); // false = 不带 alpha，纯色
            if (AlphaGradientStop != null)
                AlphaGradientStop.Color = pureColor;
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

        private Color GetRgbFromHsv(bool includeAlpha = true)
        {
            var c = ColorFromHsv(_currentHue, _currentSat, _currentVal);
            return includeAlpha ? Color.FromArgb((byte)_currentAlpha, c.R, c.G, c.B) : c;
        }

        private void UpdateUI()
        {
            if (_isUpdatingInputs) return;
            _isUpdatingInputs = true;

            try
            {
                var c = GetRgbFromHsv(true); // 获取带 Alpha 的颜色
                SelectedColor = c;

                if (NewColorRect != null)
                    NewColorRect.Fill = new SolidColorBrush(c);
                if (HexInput != null)
                    HexInput.Text = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                if (SpectrumLayerGroup != null) SpectrumLayerGroup.Opacity = _currentAlpha / 255.0;
                // 更新数值输入框
                if (Input1 != null && Input2 != null && Input3 != null && InputAlpha != null)
                {
                    InputAlpha.Text = ((int)_currentAlpha).ToString(); // Alpha 始终显示 0-255

                    if (_currentMode == ColorMode.RGB)
                    {
                        Input1.Text = c.R.ToString();
                        Input2.Text = c.G.ToString();
                        Input3.Text = c.B.ToString();
                    }
                    else
                    {
                        Input1.Text = Math.Round(_currentHue).ToString();
                        Input2.Text = Math.Round(_currentSat * 100).ToString();
                        Input3.Text = Math.Round(_currentVal * 100).ToString();
                    }
                }
                if (HueSliderGrid.ActualHeight > 0)
                {
                    double h = HueSliderGrid.ActualHeight;
                    double hueY = (1 - (_currentHue / 360.0)) * h;
                    Canvas.SetTop(HueCursor, Math.Clamp(hueY, 0, h));
                }
                if (SpectrumBaseColor.ActualWidth > 0)
                {
                    double w = SpectrumBaseColor.ActualWidth;
                    double h = SpectrumBaseColor.ActualHeight;
                    Canvas.SetLeft(CursorContainer, _currentSat * w);
                    Canvas.SetTop(CursorContainer, (1 - _currentVal) * h);
                }
                if (AlphaSliderGrid.ActualHeight > 0)
                {
                    double h = AlphaSliderGrid.ActualHeight;
                    // Alpha 255 在顶部(0)，0 在底部(h)
                    double alphaY = (1 - (_currentAlpha / 255.0)) * h;
                    Canvas.SetTop(AlphaCursor, Math.Clamp(alphaY, 0, h));
                    UpdateHueColorVisual(); // 动态更新滑块渐变色
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
            Keyboard.ClearFocus();
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
            var grid = sender as Grid;
            if (grid == null) return;

            if (_isDraggingSpectrum)
            {
                UpdateSpectrumFromMouse(e.GetPosition(grid), grid.ActualWidth, grid.ActualHeight);
            }
        }

        private void Spectrum_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSpectrum = false;
            if (sender is IInputElement el) el.ReleaseMouseCapture();

            if (ColorToolTipBorder != null)
            {
                ColorToolTipBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateSpectrumFromMouse(Point p, double w, double h)
        {
            double x = Math.Clamp(p.X, 0, w);
            double y = Math.Clamp(p.Y, 0, h);

            _currentSat = x / w;
            _currentVal = 1 - (y / h);

            UpdateUI();

            // 动态更新 Tooltip 内容
            if (ColorToolTipBorder != null)
            {
                var colorAtMouse = ColorFromHsv(_currentHue, _currentSat, _currentVal);
                ColorToolTipText.Text = GetFriendlyColorName(colorAtMouse);

                if (ColorToolTipBorder.Visibility != Visibility.Visible)
                    ColorToolTipBorder.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region Interaction - Hue Slider
        private void Hue_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
            _isDraggingHue = true;

            var grid = sender as Grid;
            if (grid != null)
            {
                grid.CaptureMouse();
                UpdateHueFromMouse(e.GetPosition(grid), grid.ActualHeight);
            }
        }
        private void UpdateHueFromMouse(Point p, double h)
        {
            if (h <= 0) return;
            double y = Math.Clamp(p.Y, 0, h);
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

        #region Interaction - Alpha Slider

        private void Alpha_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _isDraggingAlpha = true;

            AlphaSliderGrid.CaptureMouse();
            UpdateAlphaFromMouse(e.GetPosition(AlphaSliderGrid), AlphaSliderGrid.ActualHeight);
        }
        private void Alpha_Thumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            e.Handled = true; // 阻止事件冒泡，防止触发两次
            _isDraggingAlpha = true;
            AlphaSliderGrid.CaptureMouse();
            UpdateAlphaFromMouse(e.GetPosition(AlphaSliderGrid), AlphaSliderGrid.ActualHeight);
        }
        private void Hue_Thumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
            _isDraggingHue = true;
            HueSliderGrid.CaptureMouse();
            UpdateHueFromMouse(e.GetPosition(HueSliderGrid), HueSliderGrid.ActualHeight);
        }
        private void Alpha_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingAlpha)
            {
                var grid = sender as Grid;
                if (grid != null)
                    UpdateAlphaFromMouse(e.GetPosition(grid), grid.ActualHeight);
            }
        }

        private void Alpha_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingAlpha = false;
            var grid = sender as Grid;
            grid?.ReleaseMouseCapture();
        }
        private void UpdateAlphaFromMouse(Point p, double h)
        {
            if (h <= 0) return;
            double y = Math.Clamp(p.Y, 0, h);
            _currentAlpha = 255 - (y / h * 255);
            _currentAlpha = Math.Clamp(_currentAlpha, 0, 255);

            UpdateUI();
        }
        #endregion

        #region Text Input Handling
        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (HexInput == null || NewColorRect == null) return;
            if (_isUpdatingInputs) return;

            string hex = HexInput.Text.Trim('#');
            if (hex.Length == 6)
            {
                try
                {
                    _currentAlpha = 255; // 默认不透明
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    SetColorFromRgb(r, g, b);
                    var c = GetRgbFromHsv(true);
                    NewColorRect.Fill = new SolidColorBrush(c);
                    SelectedColor = c;
                    // 更新 Alpha 输入框
                    if (InputAlpha != null) InputAlpha.Text = "255";
                }
                catch { }
            }
            else if (hex.Length == 8)
            {
                try
                {
                    byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);

                    _currentAlpha = a;
                    SetColorFromRgb(r, g, b);

                    var c = GetRgbFromHsv(true);
                    NewColorRect.Fill = new SolidColorBrush(c);
                    SelectedColor = c;

                    if (InputAlpha != null) InputAlpha.Text = a.ToString();
                }
                catch { }
            }
        }
        private void NumericInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingInputs) return;
            if (Input1 == null || Input2 == null || Input3 == null || InputAlpha == null) return;

            if (double.TryParse(Input1.Text, out double v1) &&
                double.TryParse(Input2.Text, out double v2) &&
                double.TryParse(Input3.Text, out double v3) &&
                double.TryParse(InputAlpha.Text, out double vAlpha)) // 读取 Alpha
            {
                _currentAlpha = Math.Clamp(vAlpha, 0, 255);

                if (_currentMode == ColorMode.RGB)
                {
                    byte r = (byte)Math.Clamp(v1, 0, 255);
                    byte g = (byte)Math.Clamp(v2, 0, 255);
                    byte b = (byte)Math.Clamp(v3, 0, 255);
                    SetColorFromRgb(r, g, b);
                }
                else
                {
                    _currentHue = Math.Clamp(v1, 0, 360);
                    _currentSat = Math.Clamp(v2, 0, 100) / 100.0;
                    _currentVal = Math.Clamp(v3, 0, 100) / 100.0;
                    UpdateHueColorVisual();
                }

                // 刷新预览
                var c = GetRgbFromHsv(true);
                SelectedColor = c;
                NewColorRect.Fill = new SolidColorBrush(c);

                _isUpdatingInputs = true;
                HexInput.Text = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";


                if (AlphaSliderGrid.ActualHeight > 0)
                    Canvas.SetTop(AlphaCursor, (1 - (_currentAlpha / 255.0)) * AlphaSliderGrid.ActualHeight);

                if (SpectrumBaseColor.ActualWidth > 0)
                {
                    double w = SpectrumBaseColor.ActualWidth;
                    double h = SpectrumBaseColor.ActualHeight;
                    Canvas.SetLeft(CursorContainer, _currentSat * w);
                    Canvas.SetTop(CursorContainer, (1 - _currentVal) * h);
                }
                if (HueSliderGrid.ActualHeight > 0)
                {
                    double hGrid = HueSliderGrid.ActualHeight;
                    Canvas.SetTop(HueCursor, (1 - (_currentHue / 360.0)) * hGrid);
                }

                _isUpdatingInputs = false;
            }

        }
        private void ColorModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Label1 == null) return; // 防止初始化时崩溃

            if (ColorModeCombo.SelectedIndex == 0) // RGB
            {
                _currentMode = ColorMode.RGB;
                Label1.Text = LocalizationManager.GetString("L_ColorPicker_Red");
                Label2.Text = LocalizationManager.GetString("L_ColorPicker_Green");
                Label3.Text = LocalizationManager.GetString("L_ColorPicker_Blue");
            }
            else // HSV
            {
                _currentMode = ColorMode.HSV;
                Label1.Text = LocalizationManager.GetString("L_ColorPicker_Hue");
                Label2.Text = LocalizationManager.GetString("L_ColorPicker_Saturation");
                Label3.Text = LocalizationManager.GetString("L_ColorPicker_Brightness");
            }
            UpdateUI();
        }

        #endregion
        private List<Color> _customColors = new List<Color>();

        private void AddCustomColor_Click(object sender, RoutedEventArgs e)
        {
            var newColor = SelectedColor;

            if (_customColors.Contains(newColor)) return;
            _customColors.Insert(0, newColor); // 插入到最前面

            if (_customColors.Count > 18)
                _customColors.RemoveAt(18);

            // 刷新 UI
            RenderCustomColors();
            SaveCustomColorsToSettings();
        }
        private void SaveCustomColorsToSettings()
        {
            var hexList = new List<string>();

            foreach (var color in _customColors)
            {
                // Color.ToString() 默认返回 #AARRGGBB 格式
                hexList.Add(color.ToString());
            }

            // 更新设置对象
            SettingsManager.Instance.Current.CustomColors = hexList;
            // 写入文件
            SettingsManager.Instance.Save();
        }

        private void RenderCustomColors()
        {
            if (CustomColorsGrid == null) return;
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
                            Style = (Style)Resources["MiniColorSwatch"],
                            Background = new SolidColorBrush(c),
                            ToolTip = GetFriendlyColorName(c)
                        };
                        btn.Click += (s, e2) => {
                            _currentAlpha = c.A;
                            SetColorFromRgb(c.R, c.G, c.B);
                            UpdateUI();
                        };
                        slot.Children.Add(btn);
                    }
                    else
                    {
                        var dashedCircle = new System.Windows.Shapes.Ellipse
                        {
                            Stroke = (Brush)Application.Current.FindResource("BorderBrush"),
                            StrokeThickness = 1,
                            StrokeDashArray = new DoubleCollection { 3, 2 },
                            Opacity = 0.5
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
