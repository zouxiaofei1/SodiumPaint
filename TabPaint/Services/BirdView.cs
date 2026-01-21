using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using XamlAnimatedGif; // 添加这一行

//
//鸟瞰图功能
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        private void UpdateBirdEyeView()
        {
            // 如果不在看图模式或面板不可见，直接返回
            if (BirdEyePanel.Visibility != Visibility.Visible || BackgroundImage.Source == null) return;

            double miniWidth = BirdEyeImage.ActualWidth;
            double miniHeight = BirdEyeImage.ActualHeight;

            if (miniWidth <= 0 || miniHeight <= 0) return;

            double ratioX = miniWidth / ScrollContainer.ExtentWidth;
            double ratioY = miniHeight / ScrollContainer.ExtentHeight;

            // 1. 计算视野框的大小
            double viewportW = ScrollContainer.ViewportWidth * ratioX;
            double viewportH = ScrollContainer.ViewportHeight * ratioY;

            // 限制大小不超过鸟瞰图本身
            if (viewportW > miniWidth) viewportW = miniWidth;
            if (viewportH > miniHeight) viewportH = miniHeight;

            BirdEyeViewport.Width = viewportW;
            BirdEyeViewport.Height = viewportH;

            // 2. 计算视野框的位置
            double left = ScrollContainer.HorizontalOffset * ratioX;
            double top = ScrollContainer.VerticalOffset * ratioY;

            Canvas.SetLeft(BirdEyeViewport, left);
            Canvas.SetTop(BirdEyeViewport, top);
        }
        private void BirdEyeCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingBirdEye = true;
            BirdEyeCanvas.CaptureMouse();
            MoveMainViewToPoint(e.GetPosition(BirdEyeCanvas));
        }

        private void BirdEyeCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingBirdEye)
            {
                MoveMainViewToPoint(e.GetPosition(BirdEyeCanvas));
            }
        }

        private void BirdEyeCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingBirdEye = false;
            BirdEyeCanvas.ReleaseMouseCapture();
        }
        private void MoveMainViewToPoint(Point p)
        {
            double miniWidth = BirdEyeImage.ActualWidth;
            double miniHeight = BirdEyeImage.ActualHeight;

            // 计算比例
            double ratioX = ScrollContainer.ExtentWidth / miniWidth;
            double ratioY = ScrollContainer.ExtentHeight / miniHeight;

            // 目标中心点在主图上的坐标
            double targetX = p.X * ratioX;
            double targetY = p.Y * ratioY;

            // 让目标点居中：Offset = Target - Viewport/2
            double offsetX = targetX - (ScrollContainer.ViewportWidth / 2);
            double offsetY = targetY - (ScrollContainer.ViewportHeight / 2);

            ScrollContainer.ScrollToHorizontalOffset(offsetX);
            ScrollContainer.ScrollToVerticalOffset(offsetY);
        }
        private void CheckBirdEyeVisibility()
        {
            if(RootWindow.Width<300||RootWindow.Height<200)
            {
                BirdEyePanel.Visibility = Visibility.Collapsed;
                return;
            }
            if (!IsViewMode)
            {
                BirdEyePanel.Visibility = Visibility.Collapsed;
                return;
            }

            // 判断逻辑：如果内容比视口大，说明有滚动条，则显示鸟瞰图
            bool needScroll = ScrollContainer.ExtentWidth > ScrollContainer.ViewportWidth ||
                              ScrollContainer.ExtentHeight > ScrollContainer.ViewportHeight;

            if (needScroll)
            {
                BirdEyePanel.Visibility = Visibility.Visible;
                Dispatcher.BeginInvoke(new Action(UpdateBirdEyeView), DispatcherPriority.Render);
            }
            else
            {
                BirdEyePanel.Visibility = Visibility.Collapsed;
            }
        }

    }
}