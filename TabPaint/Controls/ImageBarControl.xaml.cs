//
//ImageBarControl.xaml.cs
//图片标签栏控件，负责显示已打开的图片缩略图、标签切换、关闭以及拖拽排序等交互。
//
using System.Globalization;
using System.Runtime.InteropServices; // 用于处理底层消息
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop; // 用于 HwndSource
using System.Windows.Media;
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
        private const int WM_MOUSEHWHEEL = 0x020E;
        public ImageBarControl()
        {
            InitializeComponent();
            this.Loaded += ImageBarControl_Loaded;
            this.Unloaded += ImageBarControl_Unloaded;

            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromSeconds(0.5); // 设置为 0.5 秒
            _hoverTimer.Tick += HoverTimer_Tick;
            _closeTimer = new DispatcherTimer();
            _closeTimer.Interval = TimeSpan.FromMilliseconds(100); // 100ms 缓冲期
            _closeTimer.Tick += CloseTimer_Tick;
        }
        private void Internal_OnTabMouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsCompactMode) return;

            var element = sender as FrameworkElement;
            if (element == null) return;

            _currentHoveredElement = element;

            _closeTimer.Stop();
            if (LargePreviewPopup.IsOpen)
            {
                _hoverTimer.Stop();
                UpdatePreviewPopup();
            }
            else
            {
                // 只有当完全没开的时候，才开始 0.5s 计时
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
            // 先停止计时器
            _closeTimer.Stop();

            // 如果当前没有记录的悬停元素，直接关闭
            if (_currentHoveredElement == null)
            {
                LargePreviewPopup.IsOpen = false;
                return;
            }
            Point mousePos = Mouse.GetPosition(_currentHoveredElement);
            bool isStillOver = mousePos.X >= 0 &&
                               mousePos.X <= _currentHoveredElement.ActualWidth &&
                               mousePos.Y >= 0 &&
                               mousePos.Y <= _currentHoveredElement.ActualHeight;

            if (isStillOver)
            {
                if (!LargePreviewPopup.IsOpen)
                {
                    // 极少数情况下如果被关了，这里可以救回来，但通常不需要
                    LargePreviewPopup.IsOpen = true;
                }
                return;
            }

            // --- 只有当鼠标坐标真的跑出去了，才执行关闭 ---
            LargePreviewPopup.IsOpen = false;
            _currentHoveredElement = null;
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
            if (tabData != null && tabData.Thumbnail != null)
            {
                PopupPreviewImage.Source = tabData.Thumbnail;
                LargePreviewPopup.PlacementTarget = _currentHoveredElement;

                if (!LargePreviewPopup.IsOpen)
                {
                    LargePreviewPopup.IsOpen = true;
                }
                else
                {
                    // 修复：使用 Dispatcher 延后执行位置重算
                    // 这确保了 PlacementTarget 属性在底层已经完全更新后，再命令 Popup 移动
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var method = typeof(Popup).GetMethod("Reposition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (method != null)
                            {
                                method.Invoke(LargePreviewPopup, null);
                            }
                            else
                            {
                                // 备用 Hack：微动 Offset 触发重绘
                                var currentOffset = LargePreviewPopup.HorizontalOffset;
                                LargePreviewPopup.HorizontalOffset = currentOffset + 0.1;
                                LargePreviewPopup.HorizontalOffset = currentOffset;
                            }
                        }
                        catch
                        {
                            // 忽略潜在的反射异常
                        }
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            }
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
