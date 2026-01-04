using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint
{
    public partial class ColorPickerWindow : Window
    {
        public System.Windows.Media.Color PickedColor { get; private set; }
        public bool IsColorPicked { get; private set; } = false;

        private double _dpiX = 1.0;
        private double _dpiY = 1.0;

        // 设置放大镜里显示多少个像素宽度的内容
        // 比如 15x15 的区域，放到 120x120 的圆里，每个像素会被放大 8 倍
        private const int ZoomPixelSize = 15;

        public ColorPickerWindow()
        {
            InitializeComponent();
            // 注意：事件绑定已在 XAML 中定义，或在此处 += 
        }

        private void ColorPickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ScreenColorPickerHelper.GetDpiScale(this, out _dpiX, out _dpiY);

            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            var screenshot = ScreenColorPickerHelper.CaptureScreen(_dpiX, _dpiY);
            ScreenShotImage.Source = screenshot;
            // 确保图片填满逻辑窗口，坐标系对齐
            ScreenShotImage.Stretch = Stretch.Fill;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateMagnifier(e.GetPosition(this));
        }

        private void UpdateMagnifier(System.Windows.Point logicalPos)
        {
            Magnifier.Visibility = Visibility.Visible;

            // --- 1. 解决遮挡问题 (智能跟随) ---
            double offset = 20; // 距离鼠标的距离
            double magSize = Magnifier.Width;

            double targetLeft = logicalPos.X + offset;
            double targetTop = logicalPos.Y + offset;

            // 如果靠右边缘，就往左显示
            if (targetLeft + magSize > this.ActualWidth)
            {
                targetLeft = logicalPos.X - magSize - offset;
            }
            // 如果靠下边缘，就往上显示
            if (targetTop + magSize > this.ActualHeight)
            {
                targetTop = logicalPos.Y - magSize - offset;
            }

            Canvas.SetLeft(Magnifier, targetLeft);
            Canvas.SetTop(Magnifier, targetTop);

            // --- 2. 实现像素放大预览 (VisualBrush Viewbox) ---

            // 我们需要确定 Viewbox 在 Image 控件上的逻辑坐标区域
            // 因为 ScreenShotImage 是 Stretch="Fill" 铺满窗口的，
            // 所以 Image 的逻辑坐标 == 窗口的逻辑坐标 (logicalPos)

            // 计算 Viewbox 的矩形 (以鼠标为中心，向四周扩散 ZoomPixelSize 的一半)
            double halfSize = ZoomPixelSize / 2.0;

            // Viewbox 是逻辑坐标系下的矩形
            Rect viewboxRect = new Rect(
                logicalPos.X - halfSize,
                logicalPos.Y - halfSize,
                ZoomPixelSize,
                ZoomPixelSize);

            ZoomBrush.Viewbox = viewboxRect;

            // --- 3. 获取精确颜色 (逻辑不变) ---
            // 仍然需要转回物理坐标去 Bitmap 里取色，这样最准
            int physicalX = (int)((SystemParameters.VirtualScreenLeft + logicalPos.X) * _dpiX);
            int physicalY = (int)((SystemParameters.VirtualScreenTop + logicalPos.Y) * _dpiY);

            // 边界检查，防止 GetPixel 越界崩掉
            if (physicalX >= 0 && physicalY >= 0)
            {
                try
                {
                    var c = ScreenColorPickerHelper.GetColorAtPhysical(physicalX, physicalY);
                    ColorText.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}\nRGB:{c.R},{c.G},{c.B}";
                    // 可以动态改变文字背景色来增强对比度，或者保持半透明黑底
                }
                catch { }
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var logicalPos = e.GetPosition(this);
                int physicalX = (int)((SystemParameters.VirtualScreenLeft + logicalPos.X) * _dpiX);
                int physicalY = (int)((SystemParameters.VirtualScreenTop + logicalPos.Y) * _dpiY);

                try
                {
                  
                    PickedColor = ScreenColorPickerHelper.GetColorAtPhysical(physicalX, physicalY);
                    IsColorPicked = true;
                    this.DialogResult = true;
                    this.Close();
                    return;
                }
                catch
                {
                    // 忽略越界点击
                }
            }
            // 右键退出
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                this.DialogResult = false;
                this.Close();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
                this.Close();
            }
        }
    }
}
