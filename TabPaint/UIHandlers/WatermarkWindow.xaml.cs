using Microsoft.Win32;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    public class WatermarkSettings
    {
        public bool IsText { get; set; }
        public string Text { get; set; }
        public BitmapImage ImageSource { get; set; }
        public double FontSize { get; set; }
        public FontFamily FontFamily { get; set; }
        public Color Color { get; set; }
        public double Opacity { get; set; }
        public double Angle { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public double ImageScale { get; set; }
        public bool UseRandom { get; set; }
    }

    public partial class WatermarkWindow : Window
    {
        public bool ApplyToAll => ChkApplyToAll.IsChecked == true; 
        public WatermarkSettings CurrentSettings { get; private set; }
        private WriteableBitmap _originalBitmap; // 备份，用于重置和底图
        private WriteableBitmap _targetBitmap;   // 引用 MainWindow 的图，用于实时显示
        private BitmapImage _watermarkImageSource;
        private class ColorItem
        {
            public string Name { get; set; }
            public Color Color { get; set; }
            public bool IsCustom { get; set; }
        }

        private bool _isUpdatingColor = false; // 防止事件循环触发
        public WriteableBitmap FinalBitmap { get; private set; } // 返回结果

        private bool _isInitialized = false;
        private Color _selectedColor = Colors.White;
        public WatermarkWindow(WriteableBitmap bitmapForPreview)
        {
            InitializeComponent();
            _targetBitmap = bitmapForPreview;
            _originalBitmap = bitmapForPreview.Clone(); // 深度复制一份原始图作为底图
            InitializeFonts(); InitializeCommonColors(); // <--- 新增
            _isInitialized = true;
            UpdatePreview(); // 初始渲染
        }
        private void InitializeCommonColors()
        {
            var colors = new List<ColorItem>
    {
     new ColorItem { Name = LocalizationManager.GetString("L_Color_White"), Color = Colors.White },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Black"), Color = Colors.Black },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Red"), Color = Colors.Red },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Green"), Color = Colors.Lime }, // 纯绿
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Blue"), Color = Colors.Blue },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Yellow"), Color = Colors.Yellow },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Gray"), Color = Colors.Gray },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Orange"), Color = Colors.Orange },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Purple"), Color = Colors.Purple },
                
                // 添加“自定义”项，颜色值不重要，主要是作为占位符
                new ColorItem { Name = LocalizationManager.GetString("L_ToolBar_CustomColor"), Color = Colors.Transparent, IsCustom = true }
    };

            ComboCommonColors.ItemsSource = colors;
            ComboCommonColors.DisplayMemberPath = "Name";
            ComboCommonColors.SelectedValuePath = "Color";

            // 默认选中白色
            ComboCommonColors.SelectedIndex = 0;
            UpdateColorInternal(Colors.White, false);
        }
        private void InitializeFonts()
        {
            // 获取系统字体并排序
            var fonts = Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();
            ComboFontFamily.ItemsSource = fonts;

            // 尝试选中 Microsoft YaHei 或 Arial，否则选中第一个
            var defaultFont = fonts.FirstOrDefault(f => f.Source.Contains("Microsoft YaHei"))
                           ?? fonts.FirstOrDefault(f => f.Source.Contains("Arial"))
                           ?? fonts.FirstOrDefault();

            if (defaultFont != null)
            {
                ComboFontFamily.SelectedItem = defaultFont;
            }
        }
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _isInitialized = false;

            // 1. 重置滑块
            SliderOpacity.Value = 0.5;
            SliderAngle.Value = -45;
            SliderRows.Value = 3;
            SliderCols.Value = 3;
            SliderImgScale.Value = 0.8;

            // 2. 重置文字内容和大小
            TxtContent.Text = "TabPaint";
            TxtFontSize.Text = "40";

            // 3. 重置随机偏移
            ChkRandom.IsChecked = false;
            InitializeFonts();
            ComboCommonColors.SelectedIndex = 0;
            UpdateColorInternal(Colors.White, false);

            // 恢复监听并刷新一次
            _isInitialized = true;
            UpdatePreview();
        }
        private void ColorPreview_Click(object sender, MouseButtonEventArgs e)
        {
            var dlg = new ModernColorPickerWindow(_selectedColor);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                // 强制 Alpha 为 255 (既然 Hex只允许RGB，我们这里的画笔就应保持不透明，透明度由滑块控制)
                Color c = dlg.SelectedColor;
                c.A = 255;

                UpdateColorInternal(c, true); // true 表示这是自定义颜色，可能不在下拉列表中
                UpdatePreview();
            }
        }
        private void CommonColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingColor) return;

            if (ComboCommonColors.SelectedItem is ColorItem item)
            {
                if (item.IsCustom)
                {
                    return;
                }
                UpdateColorInternal(item.Color, false);
                UpdatePreview();
            }
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            FinalBitmap = _targetBitmap;

            // 保存最后一次确认的设置，供主窗口批量处理使用
            CurrentSettings = new WatermarkSettings
            {
                IsText = RadioText.IsChecked == true,
                Text = TxtContent.Text,
                ImageSource = _watermarkImageSource,
                FontSize = double.TryParse(TxtFontSize.Text, out double fs) ? fs : 40,
                FontFamily = ComboFontFamily.SelectedItem as FontFamily ?? new FontFamily("Microsoft YaHei"),
                Color = _selectedColor,
                Opacity = SliderOpacity.Value,
                Angle = SliderAngle.Value,
                Rows = (int)SliderRows.Value,
                Cols = (int)SliderCols.Value,
                ImageScale = SliderImgScale.Value,
                UseRandom = ChkRandom.IsChecked == true
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 还原
            RestoreOriginal();
            DialogResult = false;
            Close();
        }

        private void RestoreOriginal()
        {
            if (_originalBitmap != null && _targetBitmap != null)
            {
                int stride = _originalBitmap.BackBufferStride;
                int h = _originalBitmap.PixelHeight;
                int w = _originalBitmap.PixelWidth;
                byte[] data = new byte[h * stride];
                _originalBitmap.CopyPixels(data, stride, 0);
                _targetBitmap.WritePixels(new Int32Rect(0, 0, w, h), data, stride, 0);
            }
        }
        private void Param_Changed_Combo(object sender, SelectionChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }
        // --- 控件事件 ---
        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            PanelText.Visibility = RadioText.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelImage.Visibility = RadioImage.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreview();
        }

        private void Param_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isInitialized) UpdatePreview(); }
        private void Input_TextChanged(object sender, TextChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }
        private void ComboColor_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9]+").IsMatch(e.Text);
        }

        // --- 图片加载 ---
        private void Image_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) LoadWatermarkImage(files[0]);
            }
        }
        private void ImageSelect_Click(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true) LoadWatermarkImage(dlg.FileName);
        }
        private void LoadWatermarkImage(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                _watermarkImageSource = bmp;
                ImgPreview.Source = bmp;
                TxtImageName.Text = System.IO.Path.GetFileName(path);
                UpdatePreview();
            }
            catch { }
        }
        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ModernColorPickerWindow(_selectedColor);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                UpdateColorInternal(dlg.SelectedColor, true);
                UpdatePreview();
            }
        }

        // 2. HEX 输入框变化
        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingColor) return;

            string hex = TxtHexColor.Text.Trim();
            try
            {
                Color? c = null;
                // 确保以 # 开头
                if (!hex.StartsWith("#")) hex = "#" + hex;

                // 只处理 #RRGGBB (7位) 或 #RGB (4位)
                // 即使输入了8位或9位，ColorConverter 也能解，但我们要强制 A=255
                try
                {
                    Color temp = (Color)ColorConverter.ConvertFromString(hex);
                    c = Color.FromRgb(temp.R, temp.G, temp.B); // 忽略解析出来的A，强制不透明
                }
                catch { }

                if (c.HasValue)
                {
                    _selectedColor = c.Value;
                    RectColorPreview.Fill = new SolidColorBrush(_selectedColor);

                    // 如果这个颜色不在常用列表中，将下拉框设为 -1 (或者你可以显示"自定义")
                    _isUpdatingColor = true;
                    ComboCommonColors.SelectedIndex = -1; // 取消选中常用颜色
                    _isUpdatingColor = false;

                    UpdatePreview();
                }
            }
            catch
            {
                // 解析失败忽略
            }
        }

        // 内部更新颜色方法 (同时更新Hex文本和预览)
        private void UpdateColorInternal(Color color, bool isCustom)
        {
            _isUpdatingColor = true;

            _selectedColor = color;
            // 格式化为 #RRGGBB (不带Alpha)
            TxtHexColor.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            RectColorPreview.Fill = new SolidColorBrush(color);

            if (isCustom)
            {
                ComboCommonColors.SelectedIndex = -1;
            }
            // 如果不是自定义调用（例如来自下拉框），则不需要去动 ComboBox 的 Index

            _isUpdatingColor = false;
        }

        // 新增这个，给 CheckBox 用 (因为 CheckBox 的事件签名是 RoutedEventArgs)
        private void Generic_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) UpdatePreview();
        }

        // --- 核心渲染 ---
        private void UpdatePreview()
        {
            if (!_isInitialized || _originalBitmap == null) return;

            // 收集当前设置
            var settings = new WatermarkSettings
            {
                IsText = RadioText.IsChecked == true,
                Text = TxtContent.Text,
                ImageSource = _watermarkImageSource,
                // 解析字体大小，带默认值防护
                FontSize = double.TryParse(TxtFontSize.Text, out double fs) ? fs : 40,
                FontFamily = ComboFontFamily.SelectedItem as FontFamily ?? new FontFamily("Microsoft YaHei"),
                Color = _selectedColor,
                Opacity = SliderOpacity.Value,
                Angle = SliderAngle.Value,
                Rows = (int)SliderRows.Value,
                Cols = (int)SliderCols.Value,
                ImageScale = SliderImgScale.Value,
                UseRandom = ChkRandom.IsChecked == true
            };

            // 使用通用方法生成新的图像
            // 注意：这里我们只生成用于显示的 WriteableBitmap 内容，
            // 实际上 UpdatePreview 是把结果写回 _targetBitmap (即 MainWindow 的当前画布)

            // 为了复用代码，我们将核心绘图逻辑提取为静态方法 RenderWatermarkToBitmap
            // 但因为 UpdatePreview 需要直接操作 _targetBitmap 的像素内存以保持高性能预览，
            // 我们稍微调整一下策略：提取 DrawingVisual 的构建逻辑。

            var visual = CreateWatermarkVisual(_originalBitmap, settings);

            int w = _originalBitmap.PixelWidth;
            int h = _originalBitmap.PixelHeight;

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var converted = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);

            _targetBitmap.Lock();
            converted.CopyPixels(new Int32Rect(0, 0, w, h), _targetBitmap.BackBuffer, _targetBitmap.BackBufferStride * h, _targetBitmap.BackBufferStride);
            _targetBitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
            _targetBitmap.Unlock();
        }
      

        // 4. 核心渲染逻辑提取 (静态方法，供 ApplyToAll 使用)
        // 该方法根据源图和设置，返回一个 DrawingVisual
        private static DrawingVisual CreateWatermarkVisual(BitmapSource source, WatermarkSettings settings)
        {
            int w = source.PixelWidth;
            int h = source.PixelHeight;
            if (settings.Rows < 1) settings.Rows = 1;
            if (settings.Cols < 1) settings.Cols = 1;

            Random rnd = new Random(12345);
            var visual = new DrawingVisual();

            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, w, h));

                if (settings.Opacity > 0)
                {
                    dc.PushOpacity(settings.Opacity);
                    double cellW = (double)w / settings.Cols;
                    double cellH = (double)h / settings.Rows;

                    Brush textBrush = new SolidColorBrush(settings.Color);

                    for (int r = 0; r < settings.Rows; r++)
                    {
                        for (int c = 0; c < settings.Cols; c++)
                        {
                            double cx = c * cellW + cellW / 2;
                            double cy = r * cellH + cellH / 2;

                            if (settings.UseRandom)
                            {
                                double offsetX = (rnd.NextDouble() - 0.5) * (cellW * 0.5);
                                double offsetY = (rnd.NextDouble() - 0.5) * (cellH * 0.5);
                                cx += offsetX;
                                cy += offsetY;
                            }

                            dc.PushTransform(new RotateTransform(settings.Angle, cx, cy));

                            if (settings.IsText && !string.IsNullOrEmpty(settings.Text))
                            {
                                var ft = new FormattedText(settings.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                    new Typeface(settings.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                                    settings.FontSize, textBrush, 96); // 这里的DPI假设为96，或者传入参数
                                dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
                            }
                            else if (!settings.IsText && settings.ImageSource != null)
                            {
                                double iw = settings.ImageSource.PixelWidth;
                                double ih = settings.ImageSource.PixelHeight;
                                double scaleX = (cellW * 0.8) / iw;
                                double scaleY = (cellH * 0.8) / ih;
                                double finalScale = Math.Min(scaleX, scaleY) * settings.ImageScale;
                                double fw = iw * finalScale;
                                double fh = ih * finalScale;
                                dc.DrawImage(settings.ImageSource, new Rect(cx - fw / 2, cy - fh / 2, fw, fh));
                            }
                            dc.Pop(); // Rotate
                        }
                    }
                    dc.Pop(); // Opacity
                }
            }
            return visual;
        }

        // 5. 提供一个公开的静态辅助方法，生成最终图片
        public static BitmapSource ApplyWatermarkToBitmap(BitmapSource original, WatermarkSettings settings)
        {
            var visual = CreateWatermarkVisual(original, settings);
            var rtb = new RenderTargetBitmap(original.PixelWidth, original.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }
    }
}
