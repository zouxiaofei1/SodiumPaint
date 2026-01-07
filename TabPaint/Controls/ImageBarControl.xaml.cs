using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop; // 用于 HwndSource
using System.Runtime.InteropServices; // 用于处理底层消息
using static TabPaint.MainWindow;

namespace TabPaint.Controls
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
    public partial class ImageBarControl : UserControl
    {
        private const int WM_MOUSEHWHEEL = 0x020E;
        public ImageBarControl()
        {
            InitializeComponent();
            this.Loaded += ImageBarControl_Loaded;
            this.Unloaded += ImageBarControl_Unloaded;
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

        // 核心：消息处理函数
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 只有当鼠标指针在这个控件上方时才处理，避免干扰其他区域
            if (msg == WM_MOUSEHWHEEL && IsMouseOverControl(FileTabsScroller))
            {
                // 获取滚动的 Delta 值 (高位字)
                int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                FileTabsScroller.ScrollToHorizontalOffset(FileTabsScroller.HorizontalOffset + delta);

                handled = true; // 标记消息已处理
            }

            return IntPtr.Zero;
        }

        // 辅助方法：判断鼠标是否在 ScrollViewer 区域内
        private bool IsMouseOverControl(UIElement control)
        {
            if (control == null || !control.IsVisible) return false;

            var mousePos = Mouse.GetPosition(control);
            var bounds = new Rect(0, 0, control.RenderSize.Width, control.RenderSize.Height);

            // 稍微放宽一点判定，或者严格判定
            return bounds.Contains(mousePos);
        }
        // ==========================================
        // 1. 暴露内部控件给 MainWindow (修复 "当前上下文中不存在名称" 的错误)
        // ==========================================
        public ScrollViewer Scroller => FileTabsScroller;
        public ItemsControl TabList => FileTabList;
        public Slider Slider => PreviewSlider;
        public Button AddButton => LeftAddBtn; // 修复 LeftAddBtn 访问

        // ==========================================
        // 2. 简单的点击事件 (使用路由事件冒泡)
        // ==========================================
        public static readonly RoutedEvent SaveAllClickEvent = EventManager.RegisterRoutedEvent("SaveAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler SaveAllClick { add { AddHandler(SaveAllClickEvent, value); } remove { RemoveHandler(SaveAllClickEvent, value); } }
        private void Internal_OnSaveAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAllClickEvent, sender));

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

        // ==========================================
        // 4. 复杂事件 (直接使用 C# 事件，避开 DragEventArgs 构造函数问题)
        // ==========================================

        public event MouseButtonEventHandler FileTabPreviewMouseDown;
        private void Internal_OnFileTabPreviewMouseDown(object sender, MouseButtonEventArgs e) => FileTabPreviewMouseDown?.Invoke(sender, e);

        public event MouseEventHandler FileTabPreviewMouseMove;
        private void Internal_OnFileTabPreviewMouseMove(object sender, MouseEventArgs e) => FileTabPreviewMouseMove?.Invoke(sender, e);

        public event DragEventHandler FileTabDrop;
        private void Internal_OnFileTabDrop(object sender, DragEventArgs e) => FileTabDrop?.Invoke(sender, e);

        public event DragEventHandler FileTabReorderDragOver;
        private void Internal_OnFileTabReorderDragOver(object sender, DragEventArgs e) => FileTabReorderDragOver?.Invoke(sender, e);

        // 滚动条与滑块
        public event MouseWheelEventHandler FileTabsWheelScroll;
        private void Internal_OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)
        {
            var scroller = sender as ScrollViewer;
            if (scroller == null) return;
            if (e.Delta != 0)
            {
                scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta);

                // 标记事件已处理，防止事件冒泡导致父容器(比如画布)也跟着滚动或缩放
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
            // 1. 坐标系转换：获取相对于 FileTabList（ItemsControl）的坐标
            // 使用这种方式可以避开窗口装饰器导致的坐标偏移
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

                // 3. 判定鼠标坐标是否在这个 Tab 的矩形范围内
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

        // 2. IsPinned: 是否锁定展开 (可以通过快捷键或右键菜单触发)
        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register("IsPinned", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsPinned
        {
            get { return (bool)GetValue(IsPinnedProperty); }
            set { SetValue(IsPinnedProperty, value); }
        }

        // 公开方法供外部快捷键调用 (比如按下 Ctrl+T 锁定/解锁)
        public void TogglePin()
        {
            IsPinned = !IsPinned;
        }


    }
}
