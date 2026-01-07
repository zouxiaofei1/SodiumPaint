
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TabPaint.Controls;
using static TabPaint.MainWindow;
using XamlAnimatedGif; // 添加这一行

//
//看图模式
//

namespace TabPaint
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                // 如果是 True，则隐藏 (Collapsed)；如果是 False，则显示 (Visible)
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                return v != Visibility.Visible;
            }
            return false;
        }
    }
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private long _lastModeSwitchTick = 0;
        private const long ModeSwitchCooldown = 200 * 10000;

        private void TriggerModeChange()
        {
            long currentTick = DateTime.Now.Ticks;
            if (currentTick - _lastModeSwitchTick < ModeSwitchCooldown)return;
            _lastModeSwitchTick = currentTick;
            IsViewMode = !IsViewMode;
            OnModeChanged(IsViewMode);
        }

        private void OnModeChanged(bool isView, bool isSilent = false)
        {
            if (!isSilent) ShowToast(isView ? "进入看图模式" : "进入画图模式");
            AutoSetFloatBarVisibility();
            CheckBirdEyeVisibility();
            if (isView)
            {
                if (AppTitleBar != null) AppTitleBar.IsLogoMenuEnabled = true;
                _router.CleanUpSelectionandShape();
                if (_router.CurrentTool is TextTool textTool) textTool.Cleanup(_ctx);
                if (_router.CurrentTool is PenTool penTool) penTool.StopDrawing(_ctx);
                MainImageBar.MainContainer.Height = 5;
                if (_isCurrentFileGif)
                {
                    BackgroundImage.Visibility = Visibility.Collapsed; // 隐藏静态图
                    GifPlayerImage.Visibility = Visibility.Visible;    // 显示动态图

                    if (!string.IsNullOrEmpty(_currentFilePath))
                    {
                        AnimationBehavior.SetSourceUri(GifPlayerImage, new Uri(_currentFilePath));
                    }

                    var controller = AnimationBehavior.GetAnimator(GifPlayerImage);
                    controller?.Play();
                }
                RootWindow.MinHeight= 150;
                RootWindow.MinWidth = 200;
                CanvasResizeOverlay.Visibility = Visibility.Collapsed;
               
            }
            else
            {
                if (AppTitleBar != null) AppTitleBar.IsLogoMenuEnabled = false;
                MainImageBar.MainContainer.Height = 100;
                if (_isCurrentFileGif)
                {
                    var controller = AnimationBehavior.GetAnimator(GifPlayerImage);
                    controller?.Pause();

                    GifPlayerImage.Visibility = Visibility.Collapsed; // 隐藏动态图
                    BackgroundImage.Visibility = Visibility.Visible;  // 显示静态图 (WriteableBitmap)
                }

                CanvasResizeOverlay.Visibility = Visibility.Visible;
                RootWindow.MinHeight = 400;
                RootWindow.MinWidth = 600;
            }
            UpdateCanvasVisuals();
            if (AppTitleBar != null) AppTitleBar.UpdateModeIcon(IsViewMode);
          
     
                if (_canvasResizer != null) _canvasResizer.UpdateUI();
            if (!_hasUserManuallyZoomed && _bitmap != null)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    FitToWindow();
                }, DispatcherPriority.Loaded);
            }

        }
        private void OnTitleBarModeSwitch(object sender, RoutedEventArgs e)
        {
            // 1. 切换布尔值
    TriggerModeChange();
        }

        private static void OnIsViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = (MainWindow)d;
            bool isView = (bool)e.NewValue;
            if (isView)
            {
                // 比如: window.CancelCurrentOperation();
            }
        }

    }
}
