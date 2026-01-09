
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
        private System.Windows.Input.Cursor _cachedCursoropen, _cachedCursorclose;
        public  System.Windows.Input.Cursor CursorOpenhand
        {
            get
            {
                if (_cachedCursoropen == null)
                {
                    // 仅在第一次访问时加载
                    var resourceInfo = System.Windows.Application.GetResourceStream(
                        new Uri("pack://application:,,,/Resources/Cursors/Openhand.cur"));

                    if (resourceInfo != null)
                    {
                        _cachedCursoropen = new System.Windows.Input.Cursor(resourceInfo.Stream);
                    }
                    else
                    {
                        // 防止资源没找到导致后续空指针，回退到默认
                        return System.Windows.Input.Cursors.Cross;
                    }
                }
                return _cachedCursoropen;
            }
        }
        public System.Windows.Input.Cursor CursorClosedhand
        {
            get
            {
                if (_cachedCursorclose == null)
                {
                    // 仅在第一次访问时加载
                    var resourceInfo = System.Windows.Application.GetResourceStream(
                        new Uri("pack://application:,,,/Resources/Cursors/Closedhand.cur"));

                    if (resourceInfo != null)
                    {
                        _cachedCursorclose = new System.Windows.Input.Cursor(resourceInfo.Stream);
                    }
                    else
                    {
                        // 防止资源没找到导致后续空指针，回退到默认
                        return System.Windows.Input.Cursors.Cross;
                    }
                }
                return _cachedCursorclose;
            }
        }
        private void SetViewCursor(bool isPressed = false)
        {
            if (isPressed)
            {
                this.Cursor = CursorClosedhand; System.Windows.Input.Mouse.OverrideCursor = CursorClosedhand;
            }
            else
            {
                this.Cursor = CursorOpenhand; System.Windows.Input.Mouse.OverrideCursor = CursorOpenhand;
            }
        }
        public void SetupMouseEvents()
        {
            // 这里以 MainCanvas 为例，也可以是 Window
            RootWindow.PreviewMouseDown += (s, e) => {
                if (IsViewMode) // 只有在看图模式或特定拖拽模式下执行
                {
                    SetViewCursor(true);
                }
            };

            RootWindow.PreviewMouseUp += (s, e) => {
                if (IsViewMode)
                {
                    SetViewCursor(false);
                }
            };

            // 额外处理：防止鼠标在按下状态移出窗口后在外部松开，导致回到窗口时光标还是闭合状态
            RootWindow.MouseEnter += (s, e) => {
                if (IsViewMode)
                {
                    SetViewCursor(System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed);
                }
            };

            // 离开区域恢复默认光标（可选）
            RootWindow.MouseLeave += (s, e) => {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            };
        }

        private void OnModeChanged(bool isView, bool isSilent = false)
        {
            var settings = SettingsManager.Instance.Current;

            if (!isSilent) ShowToast(isView ? "进入看图模式" : "进入画图模式");
            AutoSetFloatBarVisibility();
            CheckBirdEyeVisibility();
            if (isView)
            {
                SetupMouseEvents();
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
                if (settings.ViewUseDarkCanvasBackground)
                {
                    // 只有当前不是 Dark 时才切换，避免重复刷新
                    if (ThemeManager.CurrentAppliedTheme != AppTheme.Dark)
                    {
                        ThemeManager.ApplyTheme(AppTheme.Dark);
                    }
                }
                RootWindow.MinHeight= 100;
                RootWindow.MinWidth = 150;
                CanvasResizeOverlay.Visibility = Visibility.Collapsed;
                SetViewCursor();
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
                if (ThemeManager.CurrentAppliedTheme != settings.ThemeMode)
                {
                    // 如果 settings.ThemeMode 是 System，ThemeManager.ApplyTheme 内部会自动处理
                    ThemeManager.ApplyTheme(settings.ThemeMode);
                }
                CanvasResizeOverlay.Visibility = Visibility.Visible;
                RootWindow.MinHeight = 345;
                RootWindow.MinWidth = 430;
                _router.CurrentTool.SetCursor(_ctx);
            }
            UpdateCanvasVisuals();
            if (AppTitleBar != null) AppTitleBar.UpdateModeIcon(IsViewMode);
            AutoUpdateMaximizeIcon();
     
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
