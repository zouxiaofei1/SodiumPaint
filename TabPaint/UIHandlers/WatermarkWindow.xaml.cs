using Microsoft.Win32;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading; // 需要引用
using TabPaint.Services;


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
        public bool _isWindowLoaded = false;
        public bool ApplyToAll => ChkApplyToAll.IsChecked == true;
        private Image _previewLayer;

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
        public WatermarkWindow(WriteableBitmap bitmapForPreview, Image previewLayer)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            _targetBitmap = bitmapForPreview;
            _originalBitmap = bitmapForPreview.Clone();
            _previewLayer = previewLayer;
            if (_previewLayer != null)
            {
                _previewLayer.Source = null;
                _previewLayer.Visibility = Visibility.Visible;
                _previewLayer.Width = _originalBitmap.PixelWidth;
                _previewLayer.Height = _originalBitmap.PixelHeight;
            }

            InitializeFonts();
            InitializeCommonColors();
            this.Loaded += (s, e) =>
            {
                _isWindowLoaded = true;
                _isInitialized = true;
                UpdatePreview();
            };
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            MicaAcrylicManager.ApplyEffect(this);
            bool isDark = (ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);
            var src = (HwndSource)PresentationSource.FromVisual(this);
            if (src != null)
            {
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            if (_previewLayer != null)
            {
                _previewLayer.Source = null;
                _previewLayer.Visibility = Visibility.Collapsed;
            }
            base.OnClosed(e);
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
                new ColorItem { Name = LocalizationManager.GetString("L_ToolBar_CustomColor"), Color = Colors.Transparent, IsCustom = true }
    };

            ComboCommonColors.ItemsSource = colors;
            ComboCommonColors.DisplayMemberPath = "Name";
            ComboCommonColors.SelectedValuePath = "Color";

            ComboCommonColors.SelectedIndex = 0;
            UpdateColorInternal(Colors.Black, false);
        }
        private void InitializeFonts()
        {
            var fonts = FontService.GetSystemFonts();

            ComboFontFamily.ItemsSource = fonts;
            ComboFontFamily.SelectedValuePath = "FontFamily";  // 选中后获取哪个属性

            var defaultFontItem = FontService.GetDefaultFont(fonts);

            if (defaultFontItem != null)
            {
                ComboFontFamily.SelectedItem = defaultFontItem;
            }
        }
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _isInitialized = false;
            SliderOpacity.Value = 0.5;
            SliderAngle.Value = -45;
            SliderRows.Value = 3;
            SliderCols.Value = 3;
            SliderImgScale.Value = 0.8;
            TxtContent.Text = "TabPaint";
            TxtFontSize.Text = "40";
            ChkRandom.IsChecked = false;
            InitializeFonts();
            ComboCommonColors.SelectedIndex = 0;
            UpdateColorInternal(Colors.White, false);
            _isInitialized = true;
            UpdatePreview();
        }
        private void ColorPreview_Click(object sender, MouseButtonEventArgs e)
        {
            var dlg = new ModernColorPickerWindow(_selectedColor);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
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
                if (item.IsCustom)return;
                UpdateColorInternal(item.Color, false);
                UpdatePreview();
            }
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var finalBmp = ApplyWatermarkToBitmap(_originalBitmap, GetCurrentSettings());

            // 将结果写回 MainWindow 的 WriteableBitmap
            int w = finalBmp.PixelWidth;
            int h = finalBmp.PixelHeight;
            int stride = w * 4;
            byte[] data = new byte[h * stride];
            finalBmp.CopyPixels(data, stride, 0);

            _targetBitmap.WritePixels(new Int32Rect(0, 0, w, h), data, stride, 0);

            FinalBitmap = _targetBitmap;

            // 保存设置供批量处理
            CurrentSettings = GetCurrentSettings();

            this.SetDialogResultSafe(true);
            Close();
        }
        private WatermarkSettings GetCurrentSettings()
        {
            return new WatermarkSettings
            {
                IsText = RadioText.IsChecked == true,
                Text = TxtContent.Text,
                ImageSource = _watermarkImageSource,
                FontSize = double.TryParse(TxtFontSize.Text, out double fs) ? fs : 40,
                 FontFamily = ComboFontFamily.SelectedValue as FontFamily ?? new FontFamily("Microsoft YaHei"),
                Color = _selectedColor,
                Opacity = SliderOpacity.Value,
                Angle = SliderAngle.Value,
                Rows = (int)SliderRows.Value,
                Cols = (int)SliderCols.Value,
                ImageScale = SliderImgScale.Value,
                UseRandom = ChkRandom.IsChecked == true
            };
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.SetDialogResultSafe(false);
            Close();
        }
        private void Param_Changed_Combo(object sender, SelectionChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }
        // --- 控件事件 ---
        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            bool isText = RadioText.IsChecked == true;
            PanelText.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
            PanelImage.Visibility = !isText ? Visibility.Visible : Visibility.Collapsed;

            // 播放切换动画
            var sb = this.Resources["FadeInAnimation"] as System.Windows.Media.Animation.Storyboard;
            if (sb != null)
            {
                if (isText) sb.Begin(PanelText);
                else sb.Begin(PanelImage);
            }

            UpdatePreview();
        }

        private void Param_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isInitialized) UpdatePreview(); }
        private void Input_TextChanged(object sender, TextChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }
        private void ComboColor_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9]+").IsMatch(e.Text);
        }
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
        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingColor) return;

            string hex = TxtHexColor.Text.Trim();
            try
            {
                Color? c = null;
                if (!hex.StartsWith("#")) hex = "#" + hex;
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
                    _isUpdatingColor = true;    // 如果这个颜色不在常用列表中，将下拉框设为 -1 (或者你可以显示"自定义")
                    ComboCommonColors.SelectedIndex = -1; // 取消选中常用颜色
                    _isUpdatingColor = false;
                    UpdatePreview();
                }
            }
            catch { }
        }
        private void UpdateColorInternal(Color color, bool isCustom)   // 内部更新颜色方法 (同时更新Hex文本和预览)
        {
            _isUpdatingColor = true;
            _selectedColor = color;
            // 格式化为 #RRGGBB (不带Alpha)
            TxtHexColor.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            RectColorPreview.Fill = new SolidColorBrush(color);

            if (isCustom) ComboCommonColors.SelectedIndex = -1;
            _isUpdatingColor = false;
        }
        private void Generic_Changed(object sender, RoutedEventArgs e){ if (_isInitialized) UpdatePreview(); }
        private void UpdatePreview()  // --- 核心渲染 ---
        {
            if (!_isInitialized || _originalBitmap == null) return;

            var settings = GetCurrentSettings();
            if (_previewLayer != null)
            {
                double maxOverlayDim = 1920;
                double origW = _originalBitmap.PixelWidth;
                double origH = _originalBitmap.PixelHeight;

                double overlayScale = 1.0;
                if (origW > maxOverlayDim || origH > maxOverlayDim)  overlayScale = Math.Min(maxOverlayDim / origW, maxOverlayDim / origH);
                var visualOverlay = CreateWatermarkVisual(_originalBitmap, settings, true, overlayScale);

                int overlayW = (int)(origW * overlayScale);
                int overlayH = (int)(origH * overlayScale);
                if (overlayW < 1) overlayW = 1;
                if (overlayH < 1) overlayH = 1;

                var rtbOverlay = new RenderTargetBitmap(overlayW, overlayH, 96, 96, PixelFormats.Pbgra32);
                rtbOverlay.Render(visualOverlay);
                rtbOverlay.Freeze();
                _previewLayer.Source = rtbOverlay;
            }
            if (_isWindowLoaded)
            {
                double maxPreviewDim = 1200;

                double w = _originalBitmap.PixelWidth;
                double h = _originalBitmap.PixelHeight;

                // 计算缩放比例，让原图适应预览框
                double scale = Math.Min(maxPreviewDim / w, maxPreviewDim / h);

                // 生成包含底图的预览 (onlyWatermark = false)
                var visualInternal = CreateWatermarkVisual(_originalBitmap, settings, false, scale);

                int scaledW = (int)(w * scale);
                int scaledH = (int)(h * scale);
                if (scaledW < 1) scaledW = 1;
                if (scaledH < 1) scaledH = 1;

                var rtbInternal = new RenderTargetBitmap(scaledW, scaledH, 96, 96, PixelFormats.Pbgra32);
                rtbInternal.Render(visualInternal);
                rtbInternal.Freeze();
                ImgWindowPreview.Source = rtbInternal;
            }
        }
        private static DrawingVisual CreateWatermarkVisual(BitmapSource source, WatermarkSettings settings, bool onlyWatermark, double renderScale = 1.0)
        {
            int w = source.PixelWidth;
            int h = source.PixelHeight;

            if (settings.Rows < 1) settings.Rows = 1;
            if (settings.Cols < 1) settings.Cols = 1;

            Random rnd = new Random(12345);
            var visual = new DrawingVisual();

            using (var dc = visual.RenderOpen())
            {
                if (renderScale != 1.0) dc.PushTransform(new ScaleTransform(renderScale, renderScale));

                if (!onlyWatermark) dc.DrawImage(source, new Rect(0, 0, w, h));
                if (settings.Opacity > 0)
                {
                    dc.PushOpacity(settings.Opacity);
                    double cellW = (double)w / settings.Cols;
                    double cellH = (double)h / settings.Rows;

                    Brush textBrush = new SolidColorBrush(settings.Color);
                    if (textBrush.CanFreeze) textBrush.Freeze();

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
                                var tf = new Typeface(settings.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

                                var ft = new FormattedText(
                                    settings.Text,
                                    CultureInfo.CurrentCulture, // 确保这里跟系统的语言一致
                                    FlowDirection.LeftToRight,
                                    tf,
                                    settings.FontSize,
                                    textBrush,
                                    VisualTreeHelper.GetDpi(visual).PixelsPerDip // 使用系统的实际 DPI
                                );
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
                if (renderScale != 1.0) dc.Pop();

            }
            return visual;
        }
        public static BitmapSource ApplyWatermarkToBitmap(BitmapSource original, WatermarkSettings settings)
        {
            if (!original.IsFrozen && original.CheckAccess() == false)
                throw new InvalidOperationException("Source bitmap must be frozen.");
            var visual = CreateWatermarkVisual(original, settings, onlyWatermark: false, renderScale: 1.0);

            var rtb = new RenderTargetBitmap(original.PixelWidth, original.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }
        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.SetDialogResultSafe(false);
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
