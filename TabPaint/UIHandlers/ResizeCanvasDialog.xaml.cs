using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TabPaint
{
    public partial class ResizeCanvasDialog : Window
    {
        private const int MaxPixelSize = (int)AppConsts.MaxCanvasSize;
        public int ImageWidth { get; private set; }
        public int ImageHeight { get; private set; }
        public bool IsCanvasResizeMode { get; private set; } // True=Canvas, False=Resample

        // 原始尺寸
        private readonly int _originalWidth;
        private readonly int _originalHeight;
        private readonly double _originalRatio;
        public bool ApplyToAll => ApplyToAllCheckBox.IsChecked == true;
        public bool IsAspectRatioLocked => AspectRatioToggle.IsChecked == true;

        // 防止事件递归标志
        private bool _isUpdating = false;

        public ResizeCanvasDialog(int currentWidth, int currentHeight)
        {
            InitializeComponent();
            _originalWidth = currentWidth;
            _originalHeight = currentHeight;
            _originalRatio = (double)currentWidth / currentHeight;

            ImageWidth = currentWidth;
            ImageHeight = currentHeight;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        { // 初始化 UI
            MicaAcrylicManager.ApplyEffect(this);
            _isUpdating = true;
            WidthTextBox.Text = _originalWidth.ToString();
            HeightTextBox.Text = _originalHeight.ToString();
            WidthSlider.Value = 0;     // Slider 默认在中间 (0 = 1.0x)
            HeightSlider.Value = 0;

            _isUpdating = false;

            WidthTextBox.Focus();
            WidthTextBox.SelectAll();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private double SliderToScale(double sliderValue)
        {
            return Math.Pow(10, sliderValue);
        }

        private double ScaleToSlider(double scale)
        {
            if (scale <= 0) return -1;
            return Math.Log10(scale);
        }

        private void UpdateInfoText()
        {
            if (ImageWidth == 0) return;
            double scale = (double)ImageWidth / _originalWidth;

            // 检查是否触顶
            bool isLimitReached = (ImageWidth >= MaxPixelSize || ImageHeight >= MaxPixelSize);

            if (isLimitReached)
            {
                InfoTextBlock.Text = string.Format(
            LocalizationManager.GetString("L_ResizeCanvas_LimitReached"),
            MaxPixelSize);
                InfoTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 50, 50)); // 红色警告
            }
            else
            {
                if (IsCanvasResizeMode)
                {
                    // 原始: "画布模式：{ImageWidth} x {ImageHeight}"
                    InfoTextBlock.Text = string.Format(
                        LocalizationManager.GetString("L_Info_CanvasMode_Format"),
                        ImageWidth,
                        ImageHeight);
                }
                else
                {
                    InfoTextBlock.Text = string.Format(
                        LocalizationManager.GetString("L_Info_ResampleMode_Format"),
                        scale,
                        _originalWidth,
                        _originalHeight);
                }
                InfoTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136)); // 恢复灰色 #888888
            }
        }
        private void OnWidthChanged(int newWidth, bool fromSlider)  // 统一处理宽度变更
        {
            if (_isUpdating) return;
            _isUpdating = true;

            if (newWidth > MaxPixelSize) newWidth = MaxPixelSize;

            if (AspectRatioToggle.IsChecked == true)
            {
                int calculatedHeight = (int)Math.Round(newWidth / _originalRatio);

                if (calculatedHeight > MaxPixelSize)
                {
                    calculatedHeight = MaxPixelSize;
                    newWidth = (int)Math.Round(calculatedHeight * _originalRatio);
                }

                ImageHeight = calculatedHeight;
                HeightTextBox.Text = calculatedHeight.ToString();
                HeightSlider.Value = ScaleToSlider((double)calculatedHeight / _originalHeight);
            }

            ImageWidth = newWidth;
            double scale = (double)newWidth / _originalWidth;

            if (!fromSlider) WidthSlider.Value = ScaleToSlider(scale);
            WidthTextBox.Text = newWidth.ToString();

            UpdateInfoText();
            _isUpdating = false;
        }
        private void OnHeightChanged(int newHeight, bool fromSlider)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            if (newHeight > MaxPixelSize) newHeight = MaxPixelSize;

            if (AspectRatioToggle.IsChecked == true)
            {
                int calculatedWidth = (int)Math.Round(newHeight * _originalRatio);
                if (calculatedWidth > MaxPixelSize)
                {
                    calculatedWidth = MaxPixelSize;
                    newHeight = (int)Math.Round(calculatedWidth / _originalRatio);
                }

                ImageWidth = calculatedWidth;
                WidthTextBox.Text = calculatedWidth.ToString();
                WidthSlider.Value = ScaleToSlider((double)calculatedWidth / _originalWidth);
            }
            ImageHeight = newHeight;   // 4. 更新高度相关 UI
            double scale = (double)newHeight / _originalHeight;

            if (!fromSlider)
            {
                HeightSlider.Value = ScaleToSlider(scale);
            }

            HeightTextBox.Text = newHeight.ToString();

            UpdateInfoText();
            _isUpdating = false;
        }
        private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            double scale = SliderToScale(WidthSlider.Value);
            int newWidth = (int)Math.Round(_originalWidth * scale);
            // 限制最小 1px
            newWidth = Math.Max(1, newWidth);
            OnWidthChanged(newWidth, true);
        }

        private void HeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            double scale = SliderToScale(HeightSlider.Value);
            int newHeight = (int)Math.Round(_originalHeight * scale);
            newHeight = Math.Max(1, newHeight);
            OnHeightChanged(newHeight, true);
        }

        private void WidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (int.TryParse(WidthTextBox.Text, out int w) && w > 0)
            {
                OnWidthChanged(w, false);
            }
        }

        private void HeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (int.TryParse(HeightTextBox.Text, out int h) && h > 0)
            {
                OnHeightChanged(h, false);
            }
        }

        private void AspectRatioToggle_Click(object sender, RoutedEventArgs e)
        {
            // 点击锁链时，立即根据当前宽度重新计算高度以对齐
            if (AspectRatioToggle.IsChecked == true)
            {
                if (int.TryParse(WidthTextBox.Text, out int w)) OnWidthChanged(w, false); // 强制触发一次同步
            }
        }

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return; // 防止初始化触发

            IsCanvasResizeMode = ModeComboBox.SelectedIndex == 1;

            if (IsCanvasResizeMode) InfoTextBlock.Text = LocalizationManager.GetString("L_ResizeCanvas_Desc_Canvas");
            else InfoTextBlock.Text = LocalizationManager.GetString("L_ResizeCanvas_Desc_Resample");
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 最终校验
            if (ImageWidth > MaxPixelSize || ImageHeight > MaxPixelSize)
            {
                FluentMessageBox.Show(
          string.Format(LocalizationManager.GetString("L_Msg_SizeTooLarge_Content"), MaxPixelSize),
          LocalizationManager.GetString("L_Msg_SizeTooLarge_Title"),
          MessageBoxButton.OK);

                // 强制修正回来
                if (ImageWidth > MaxPixelSize) OnWidthChanged(MaxPixelSize, false);
                else if (ImageHeight > MaxPixelSize) OnHeightChanged(MaxPixelSize, false);
                return;
            }

            if (ImageWidth > 0 && ImageHeight > 0)
            {
                DialogResult = true;
            }
            else
            {
                FluentMessageBox.Show(
          LocalizationManager.GetString("L_ResizeCanvas_Error_InvalidSize"),
          LocalizationManager.GetString("L_Common_Error"));
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
