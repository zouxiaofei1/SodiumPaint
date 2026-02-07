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
    { private Brush _checkerboardBrush;
        private DispatcherTimer _closeTimer;
        private const int WM_MOUSEHWHEEL = AppConsts.WM_MOUSEHWHEEL;
        private Brush GetCheckerboardBrush()
        {
            if (_checkerboardBrush != null) return _checkerboardBrush;

            var lightBrush = (Brush)FindResource("CheckerboardLightBrush");
            var darkBrush = (Brush)FindResource("CheckerboardDarkBrush");

            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(lightBrush, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            var darkGeometry = new GeometryGroup();
            darkGeometry.Children.Add(new RectangleGeometry(new Rect(0, 0, 8, 8)));
            darkGeometry.Children.Add(new RectangleGeometry(new Rect(8, 8, 8, 8)));
            drawing.Children.Add(new GeometryDrawing(darkBrush, null, darkGeometry));

            _checkerboardBrush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 16, 16),
                ViewportUnits = BrushMappingMode.Absolute
            };
            _checkerboardBrush.Freeze();
            return _checkerboardBrush;
        }

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
            ClosePopupAndReset();
        }
        private void UpdateCheckerboardVisibility(BitmapSource source)
        {
            if (source == null)
            {
                CheckerboardBorder.Background = Brushes.Transparent;
                return;
            }

            bool hasAlpha = false;
            try
            {
                var format = source.Format;
                hasAlpha = format == PixelFormats.Bgra32
                        || format == PixelFormats.Pbgra32
                        || format == PixelFormats.Rgba64
                        || format == PixelFormats.Rgba128Float
                        || format == PixelFormats.Prgba64
                        || format == PixelFormats.Prgba128Float
                        || (format.Masks.Count >= 4);
            }
            catch { hasAlpha = false;}
            CheckerboardBorder.Background = hasAlpha ? GetCheckerboardBrush() : Brushes.Transparent;
        }

        public void ClosePopupAndReset()
        {
            LargePreviewPopup.IsOpen = false;
            _currentHoveredElement = null;
            _highResTimer.Stop();
            _previewCts?.Cancel();
            PopupPreviewImage.Source = null;
            CheckerboardBorder.Background = Brushes.Transparent; // ★ 清理
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
                // 1. 先显示已有的缩略图
                if (tabData.Thumbnail != null)
                {
                    PopupPreviewImage.Source = tabData.Thumbnail;
                    UpdateCheckerboardVisibility(tabData.Thumbnail as BitmapSource);
                }
                else
                {
                    PopupPreviewImage.Source = null;
                    CheckerboardBorder.Background = Brushes.Transparent;
                }

                // 2. 获取文件信息
                string filePath = tabData.FilePath;
                bool isNewFile = string.IsNullOrEmpty(filePath) || !File.Exists(filePath);

                if (isNewFile)
                {
                    PopupFileSizeText.Text = "";
                    // ★ 新图片也尝试从缩略图获取尺寸
                    BitmapSource thumb = tabData.Thumbnail as BitmapSource;
                    if (thumb != null && thumb.PixelWidth > 0)
                    {
                        PopupDimensionsText.Text = $"{thumb.PixelWidth} × {thumb.PixelHeight} px";
                    }
                    else
                    {
                        PopupDimensionsText.Text = LocalizationManager.GetString("L_ImgBar_NewImage");
                    }
                    _highResTimer.Stop();
                }
                else
                {
                    try
                    {
                        var fi = new FileInfo(filePath);
                        PopupFileSizeText.Text = FormatFileSize(fi.Length);

                        // ★ 同步快速读取图片尺寸（只读文件头，不解码像素）
                        var dims = GetImageDimensionsFast(filePath);
                        if (dims.Width > 0 && dims.Height > 0)
                        {
                            PopupDimensionsText.Text = $"{dims.Width} × {dims.Height} px";
                        }
                        else
                        {
                            PopupDimensionsText.Text = "";
                        }
                    }
                    catch
                    {
                        PopupFileSizeText.Text = ""; PopupDimensionsText.Text = "";
                    }

                    _highResTimer.Stop();
                    _highResTimer.Start();
                }

                // 3. 设置位置并打开
                LargePreviewPopup.PlacementTarget = _currentHoveredElement;

                if (!LargePreviewPopup.IsOpen) LargePreviewPopup.IsOpen = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var method = typeof(Popup).GetMethod("Reposition",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        method?.Invoke(LargePreviewPopup, null);
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private (int Width, int Height) GetImageDimensionsFast(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder = BitmapDecoder.Create(fs,
                    BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                    BitmapCacheOption.None);

                if (decoder.Frames.Count > 0)
                {
                    var frame = decoder.Frames[0];
                    return (frame.PixelWidth, frame.PixelHeight);
                }
            }
            catch{}
            return (0, 0);
        }

        private async void HighResTimer_Tick(object sender, EventArgs e)
        {
            _highResTimer.Stop();

            if (_currentHoveredElement == null) return;
            dynamic tabData = _currentHoveredElement.DataContext;
            string filePath = tabData?.FilePath;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            try
            {
                var result = await Task.Run(() => LoadHighResPreviewInternal(filePath, token), token);

                if (token.IsCancellationRequested) return;

                if (result.Image != null)
                {
                    PopupPreviewImage.Source = result.Image;
                    UpdateCheckerboardVisibility(result.Image);
                }
                if (string.IsNullOrEmpty(PopupDimensionsText.Text) || PopupDimensionsText.Text == "")
                {
                    if (result.Width > 0 && result.Height > 0)
                    {
                        PopupDimensionsText.Text = $"{result.Width} × {result.Height} px";
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"High res preview failed: {ex.Message}");
            }
        }
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
                    if (token.IsCancellationRequested) return res;
                    fs.Position = 0;

                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad; // 必须OnLoad，因为流会关闭
                    img.StreamSource = fs;
                    img.DecodePixelWidth = 400;
                    img.EndInit();
                    img.Freeze(); // 必须冻结以便跨线程传递

                    res.Image = img;
                }
            }
            catch
            {
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
                var container = FileTabList.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;
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
