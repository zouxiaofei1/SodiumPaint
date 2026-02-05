//
//ImageBarControl.xaml.cs
//图片标签栏控件，负责显示已打开的图片缩略图、标签切换、关闭以及拖拽排序等交互。
//
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices; // 用于处理底层消息
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop; // 用于 HwndSource
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static TabPaint.MainWindow;

namespace TabPaint.Controls
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
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
    public partial class ImageBarControl : UserControl
    {
        private DispatcherTimer _closeTimer;
        private const int WM_MOUSEHWHEEL = AppConsts.WM_MOUSEHWHEEL;
        public ImageBarControl()
        {
            InitializeComponent();
            this.Loaded += ImageBarControl_Loaded;
            this.Unloaded += ImageBarControl_Unloaded;

            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromSeconds(0.2); // 设置为 0.5 秒
            _hoverTimer.Tick += HoverTimer_Tick;
            _closeTimer = new DispatcherTimer();
            _closeTimer.Interval = TimeSpan.FromMilliseconds(100); // 100ms 缓冲期
            _closeTimer.Tick += CloseTimer_Tick;

            _highResTimer = new DispatcherTimer();
            _highResTimer.Interval = TimeSpan.FromSeconds(0.3); // 悬浮显示后1秒触发
            _highResTimer.Tick += HighResTimer_Tick;
        }
        private void Internal_OnTabMouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsCompactMode) return;

            var element = sender as FrameworkElement;
            if (element == null) return;

            // 如果切换了Tab，先取消之前的任务和定时器
            if (_currentHoveredElement != element)
            {
                _highResTimer.Stop();
                _previewCts?.Cancel();
            }

            _currentHoveredElement = element;

            _closeTimer.Stop();
            if (LargePreviewPopup.IsOpen)
            {
                _hoverTimer.Stop();
                UpdatePreviewPopup(); // 立即更新内容
            }
            else
            {
                _hoverTimer.Stop();
                _hoverTimer.Start();
            }
        }


        // 4. 鼠标离开 Tab
        private void Internal_OnTabMouseLeave(object sender, MouseEventArgs e)
        {
            _hoverTimer.Stop(); // 还没显示的就别显示了
            _closeTimer.Start(); // 准备关闭
        }

        // 3. 关闭定时器触发
        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            _closeTimer.Stop();

            if (_currentHoveredElement == null)
            {
                ClosePopupAndReset();
                return;
            }

            // ... (保留原有的坐标判断逻辑) ...
            Point mousePos = Mouse.GetPosition(_currentHoveredElement);
            bool isStillOver = mousePos.X >= 0 &&
                               mousePos.X <= _currentHoveredElement.ActualWidth &&
                               mousePos.Y >= 0 &&
                               mousePos.Y <= _currentHoveredElement.ActualHeight;

            if (isStillOver)
            {
                if (!LargePreviewPopup.IsOpen) LargePreviewPopup.IsOpen = true;
                return;
            }

            // --- 修改: 关闭时彻底清理 ---
            ClosePopupAndReset();
        }
        private void ClosePopupAndReset()
        {
            LargePreviewPopup.IsOpen = false;
            _currentHoveredElement = null;
            _highResTimer.Stop();
            _previewCts?.Cancel(); // 取消正在进行的后台加载
            PopupPreviewImage.Source = null; // 释放内存引用
        }
        // 5. 定时器触发（0.5s 后）
        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (!IsCompactMode || _currentHoveredElement == null) return;

            // 开启前也要把关闭计时器停掉，防止意外
            _closeTimer.Stop();
            UpdatePreviewPopup();
        }

        private void UpdatePreviewPopup()
        {
            if (_currentHoveredElement == null) return;

            dynamic tabData = _currentHoveredElement.DataContext;
            if (tabData != null)
            {
                // 1. 先显示已有的缩略图 (模糊/小图)
                if (tabData.Thumbnail != null)
                {
                    PopupPreviewImage.Source = tabData.Thumbnail;
                }
                else
                {
                    // 如果连缩略图都没有，可以设置一个占位符或null
                    PopupPreviewImage.Source = null;
                }

                // 2. 获取文件基本信息 (同步快速操作)
                string filePath = tabData.FilePath;
                if (File.Exists(filePath))
                {
                    try
                    {
                        var fi = new FileInfo(filePath);
                        // 格式化文件大小
                        PopupFileSizeText.Text = FormatFileSize(fi.Length);
                        PopupDimensionsText.Text = "Loading..."; // 尺寸需要读取头部，稍后异步加载
                    }
                    catch
                    {
                        PopupFileSizeText.Text = "Unknown";
                    }
                }

                // 3. 设置位置并打开
                LargePreviewPopup.PlacementTarget = _currentHoveredElement;

                if (!LargePreviewPopup.IsOpen)
                {
                    LargePreviewPopup.IsOpen = true;
                }

                // 4. 启动高清图加载定时器 (1秒后触发)
                _highResTimer.Stop();
                _highResTimer.Start();

                // 5. 修复位置 (原代码逻辑)
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var method = typeof(Popup).GetMethod("Reposition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        method?.Invoke(LargePreviewPopup, null);
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }
        private async void HighResTimer_Tick(object sender, EventArgs e)
        {
            _highResTimer.Stop(); // 只触发一次

            if (_currentHoveredElement == null) return;
            dynamic tabData = _currentHoveredElement.DataContext;
            string filePath = tabData?.FilePath;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // 取消上一次可能的任务
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            try
            {
                // 在后台线程加载图片信息和高清预览
                var result = await Task.Run(() => LoadHighResPreviewInternal(filePath, token), token);

                if (token.IsCancellationRequested) return;

                // 回到UI线程更新界面
                if (result.Image != null)
                {
                    PopupPreviewImage.Source = result.Image;
                }
                if (result.Width > 0 && result.Height > 0)
                {
                    PopupDimensionsText.Text = $"{result.Width} × {result.Height} px";
                }
                else
                {
                    PopupDimensionsText.Text = "Unknown Size";
                }
            }
            catch (OperationCanceledException)
            {
                // 忽略取消
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"High res preview failed: {ex.Message}");
            }
        }

        // 内部结构，用于传递后台任务结果
        private struct PreviewResult
        {
            public BitmapSource Image;
            public int Width;
            public int Height;
        }

        // 后台加载逻辑
        private PreviewResult LoadHighResPreviewInternal(string filePath, CancellationToken token)
        {
            var res = new PreviewResult();
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // 1. 读取尺寸 (BitmapDecoder 仅仅读取头部，非常快)
                    var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                    if (decoder.Frames.Count > 0)
                    {
                        var frame = decoder.Frames[0];
                        res.Width = frame.PixelWidth;
                        res.Height = frame.PixelHeight;
                    }

                    // 2. 生成高清预览 (限制大小以优化性能，比如限制宽400)
                    if (token.IsCancellationRequested) return res;

                    // 重置流位置
                    fs.Position = 0;

                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad; // 必须OnLoad，因为流会关闭
                    img.StreamSource = fs;
                    // 重要：设置解码高度/宽度，避免加载4K/8K原图占用巨大内存
                    // 这里设置DecodePixelWidth=400，足够Popup清晰显示了
                    img.DecodePixelWidth = 400;
                    img.EndInit();
                    img.Freeze(); // 必须冻结以便跨线程传递

                    res.Image = img;
                }
            }
            catch
            {
                // 加载失败 (例如格式不支持)
            }
            return res;
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }


        private void ImageBarControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 获取当前窗口的句柄源
            var window = Window.GetWindow(this);
            if (window != null)
            {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                source?.AddHook(WndProc);
            }
        }

        private void ImageBarControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // 清理钩子，防止内存泄漏
            var window = Window.GetWindow(this);
            if (window != null)
            {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                source?.RemoveHook(WndProc);
            }
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL && IsMouseOverControl(FileTabsScroller))
            {
                int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                FileTabsScroller.ScrollToHorizontalOffset(FileTabsScroller.HorizontalOffset + delta);

                handled = true; // 标记消息已处理
            }

            return IntPtr.Zero;
        }
        private bool IsMouseOverControl(UIElement control)
        {
            if (control == null || !control.IsVisible) return false;

            var mousePos = Mouse.GetPosition(control);
            var bounds = new Rect(0, 0, control.RenderSize.Width, control.RenderSize.Height);
            return bounds.Contains(mousePos);
        }
        public ScrollViewer Scroller => FileTabsScroller;
        public ItemsControl TabList => FileTabList;
        public Slider Slider => PreviewSlider;
        public Button AddButton => LeftAddBtn; 

        public static readonly RoutedEvent SaveAllClickEvent = EventManager.RegisterRoutedEvent("SaveAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler SaveAllClick { add { AddHandler(SaveAllClickEvent, value); } remove { RemoveHandler(SaveAllClickEvent, value); } }
        private void Internal_OnSaveAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAllClickEvent, sender));
        public event RoutedEventHandler TabStickImageClick;
        private void Internal_OnTabStickImageClick(object sender, RoutedEventArgs e)
           => TabStickImageClick?.Invoke(sender, e);

        public static readonly RoutedEvent ClearUneditedClickEvent = EventManager.RegisterRoutedEvent("ClearUneditedClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler ClearUneditedClick { add { AddHandler(ClearUneditedClickEvent, value); } remove { RemoveHandler(ClearUneditedClickEvent, value); } }
        private void Internal_OnClearUneditedClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ClearUneditedClickEvent, sender));

        public static readonly RoutedEvent DiscardAllClickEvent = EventManager.RegisterRoutedEvent("DiscardAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler DiscardAllClick { add { AddHandler(DiscardAllClickEvent, value); } remove { RemoveHandler(DiscardAllClickEvent, value); } }
        private void Internal_OnDiscardAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(DiscardAllClickEvent, sender));

        public static readonly RoutedEvent PrependTabClickEvent = EventManager.RegisterRoutedEvent("PrependTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler PrependTabClick { add { AddHandler(PrependTabClickEvent, value); } remove { RemoveHandler(PrependTabClickEvent, value); } }
        private void Internal_OnPrependTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PrependTabClickEvent, sender));

        public static readonly RoutedEvent NewTabClickEvent = EventManager.RegisterRoutedEvent("NewTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler NewTabClick { add { AddHandler(NewTabClickEvent, value); } remove { RemoveHandler(NewTabClickEvent, value); } }
        private void Internal_OnNewTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NewTabClickEvent, sender));


        public static readonly RoutedEvent FileTabClickEvent = EventManager.RegisterRoutedEvent("FileTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler FileTabClick { add { AddHandler(FileTabClickEvent, value); } remove { RemoveHandler(FileTabClickEvent, value); } }
        private void Internal_OnFileTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FileTabClickEvent, e.OriginalSource)); // 保持 OriginalSource

        public static readonly RoutedEvent FileTabCloseClickEvent = EventManager.RegisterRoutedEvent("FileTabCloseClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler FileTabCloseClick { add { AddHandler(FileTabCloseClickEvent, value); } remove { RemoveHandler(FileTabCloseClickEvent, value); } }
        private void Internal_OnFileTabCloseClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FileTabCloseClickEvent, e.OriginalSource));

        // 右键菜单转发
        public event RoutedEventHandler TabCopyClick;
        public event RoutedEventHandler TabCutClick;
        public event RoutedEventHandler TabPasteClick;
        public event RoutedEventHandler TabOpenFolderClick;
        public event RoutedEventHandler TabDeleteClick;
        public event RoutedEventHandler TabFileDeleteClick;

        private void Internal_OnTabCopyClick(object sender, RoutedEventArgs e) => TabCopyClick?.Invoke(sender, e);
        private void Internal_OnTabCutClick(object sender, RoutedEventArgs e) => TabCutClick?.Invoke(sender, e);
        private void Internal_OnTabPasteClick(object sender, RoutedEventArgs e) => TabPasteClick?.Invoke(sender, e);
        private void Internal_OnTabOpenFolderClick(object sender, RoutedEventArgs e) => TabOpenFolderClick?.Invoke(sender, e);
        private void Internal_OnTabDeleteClick(object sender, RoutedEventArgs e) => TabDeleteClick?.Invoke(sender, e);
        private void Internal_OnTabFileDeleteClick(object sender, RoutedEventArgs e) => TabFileDeleteClick?.Invoke(sender, e);

        public event MouseButtonEventHandler FileTabPreviewMouseDown;
        private void Internal_OnFileTabPreviewMouseDown(object sender, MouseButtonEventArgs e) => FileTabPreviewMouseDown?.Invoke(sender, e);

        public event MouseEventHandler FileTabPreviewMouseMove;
        private void Internal_OnFileTabPreviewMouseMove(object sender, MouseEventArgs e) => FileTabPreviewMouseMove?.Invoke(sender, e);

        public event DragEventHandler FileTabDrop;
        private void Internal_OnFileTabDrop(object sender, DragEventArgs e) => FileTabDrop?.Invoke(sender, e);
        public event MouseWheelEventHandler FileTabsWheelScroll;
        public event DragEventHandler FileTabReorderDragOver;
        private void Internal_OnFileTabReorderDragOver(object sender, DragEventArgs e) => FileTabReorderDragOver?.Invoke(sender, e);

        // 滚动条与滑块
        private void Internal_OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)
        {
            var scroller = sender as ScrollViewer;
            if (scroller == null) return;
            if (e.Delta != 0)
            {
                scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }

        public event ScrollChangedEventHandler FileTabsScrollChanged;
        private void Internal_OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e) => FileTabsScrollChanged?.Invoke(sender, e);

        public event RoutedPropertyChangedEventHandler<double> PreviewSliderValueChanged;
        private void Internal_PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => PreviewSliderValueChanged?.Invoke(sender, e);

        public event MouseWheelEventHandler SliderPreviewMouseWheel;
        private void Internal_Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => SliderPreviewMouseWheel?.Invoke(sender, e);
        public static readonly RoutedEvent SaveAllDoubleClickEvent =
    EventManager.RegisterRoutedEvent("SaveAllDoubleClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));

        public event RoutedEventHandler SaveAllDoubleClick
        {
            add { AddHandler(SaveAllDoubleClickEvent, value); }
            remove { RemoveHandler(SaveAllDoubleClickEvent, value); }
        }

        private void Internal_OnSaveAllDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 标记事件已处理，防止其继续冒泡或触发其他的 Click 行为（视具体需求而定）
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(SaveAllDoubleClickEvent, sender));
        }
        private void Internal_ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }
        public FileTabItem GetTabFromPoint(Point pointRelativeToWindow)
        {
            Point pointInList = this.FileTabList.PointFromScreen(this.PointToScreen(new Point(0, 0)));
            Point mousePosInList = this.FileTabList.PointFromScreen(this.PointToScreen(pointRelativeToWindow));
            if (pointRelativeToWindow.Y > 220) return null;
            // 2. 遍历当前可见的 Tab 容器
            for (int i = 0; i < FileTabList.Items.Count; i++)
            {
                // 获取 UI 容器 (即 DataTemplate 里的那个 Grid/Button)
                var container = FileTabList.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;

                // 获取该 Tab 相对于 FileTabList 的位置
                Point relativePos = container.TranslatePoint(new Point(0, 0), FileTabList);
                Rect bounds = new Rect(relativePos.X, relativePos.Y+110, container.ActualWidth, container.ActualHeight);
                if (bounds.Contains(mousePosInList))
                {
                    return FileTabList.Items[i] as FileTabItem;
                }
            }

            return null;
        }

        private DispatcherTimer _highResTimer; // 用于1秒后加载大图
        private CancellationTokenSource _previewCts; // 用于取消正在进行的加载任务

        public static readonly DependencyProperty IsSingleTabModeProperty =
    DependencyProperty.Register("IsSingleTabMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsSingleTabMode
        {
            get { return (bool)GetValue(IsSingleTabModeProperty); }
            set { SetValue(IsSingleTabModeProperty, value); }
        }
        public event DragEventHandler FileTabLeave;
        // 1. DragOver: 计算位置并显示竖线
           private void Internal_OnFileTabDragLeave(object sender, DragEventArgs e) => FileTabLeave?.Invoke(sender, e);

        public static readonly DependencyProperty IsViewModeProperty =
                   DependencyProperty.Register("IsViewMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsViewMode
        {
            get { return (bool)GetValue(IsViewModeProperty); }
            set { SetValue(IsViewModeProperty, value); }
        }
        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register("IsPinned", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsPinned
        {
            get { return (bool)GetValue(IsPinnedProperty); }
            set { SetValue(IsPinnedProperty, value); }
        }
        public void TogglePin()
        {
            IsPinned = !IsPinned;
        }
        public static readonly DependencyProperty IsCompactModeProperty =
         DependencyProperty.Register("IsCompactMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false, OnCompactModeChanged));

        public bool IsCompactMode
        {
            get { return (bool)GetValue(IsCompactModeProperty); }
            set { SetValue(IsCompactModeProperty, value); }
        }

        // --- 新增：动态高度属性，用于动画绑定 ---
        public double DesiredHeight
        {
            get { return (double)GetValue(DesiredHeightProperty); }
            set { SetValue(DesiredHeightProperty, value); }
        }

        public static readonly DependencyProperty DesiredHeightProperty =
            DependencyProperty.Register("DesiredHeight", typeof(double), typeof(ImageBarControl), new PropertyMetadata(100.0));


        private static void OnCompactModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = d as ImageBarControl;
            if (ctrl != null)
            {
                // 切换模式时调整容器的预期高度，以便动画正常工作
                ctrl.DesiredHeight = (bool)e.NewValue ? 45.0 : 100.0;
                ctrl.InvalidateVisual();
            }
        }
        private void Internal_OnToggleViewModeClick(object sender, RoutedEventArgs e)
        {
            IsCompactMode = !IsCompactMode;
        }
        private void Internal_OnBackgroundMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;

            var dep = e.OriginalSource as DependencyObject;
            if (dep == null) return;
            if (HasDataContext<FileTabItem>(dep)) return;
            if (FindAncestor<ButtonBase>(dep) != null) return;
            if (FindAncestor<Slider>(dep) != null) return;
            if (FindAncestor<Thumb>(dep) != null) return;
            if (FindAncestor<ScrollBar>(dep) != null) return;

            IsCompactMode = !IsCompactMode;
            e.Handled = true;
        }
        private DispatcherTimer _hoverTimer;
        private FrameworkElement _currentHoveredElement;
        private static bool HasDataContext<T>(DependencyObject d)
        {
            while (d != null)
            {
                if (d is FrameworkElement fe && fe.DataContext is T) return true;
                d = VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        private static T? FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }
    }
}
