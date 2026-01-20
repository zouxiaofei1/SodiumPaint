using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Controls
{
  
    public partial class TitleBarControl : UserControl
    {
        // --- 1. 新增：暴露内部控件给 MainWindow 访问 ---
        public TextBlock TitleTextControl => TitleTextBlock;
        public Button MaxBtn => MaxRestoreButton;

        // --- 原有的路由事件定义 ---
        public static readonly RoutedEvent MinimizeClickEvent = EventManager.RegisterRoutedEvent(
            "MinimizeClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent MaximizeRestoreClickEvent = EventManager.RegisterRoutedEvent(
            "MaximizeRestoreClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent CloseClickEvent = EventManager.RegisterRoutedEvent(
            "CloseClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));

        public event RoutedEventHandler MinimizeClick { add => AddHandler(MinimizeClickEvent, value); remove => RemoveHandler(MinimizeClickEvent, value); }
        public event RoutedEventHandler MaximizeRestoreClick { add => AddHandler(MaximizeRestoreClickEvent, value); remove => RemoveHandler(MaximizeRestoreClickEvent, value); }
        public event RoutedEventHandler CloseClick { add => AddHandler(CloseClickEvent, value); remove => RemoveHandler(CloseClickEvent, value); }

        public TitleBarControl()
        {
            InitializeComponent(); 
            UpdateModeIcon(false);
        }
        public event MouseButtonEventHandler TitleBarMouseDown;

        // 2. 内部 Border 的点击事件处理器

        private void OnMinimizeClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MinimizeClickEvent));
        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MaximizeRestoreClickEvent));
        private void OnCloseClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CloseClickEvent));

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击的是按钮（最大化/关闭等），通常按钮会拦截事件，但为了保险可以判断 Source
            if (e.OriginalSource is System.Windows.Controls.Button ||
                (e.OriginalSource is FrameworkElement fe && fe.TemplatedParent is System.Windows.Controls.Button))
            {
                return;
            }
            if (e.OriginalSource == AppIcon || e.Source == AppIcon)
            {
                return;
            }
            // 3. 将事件转发给外部 (即 MainWindow)
            TitleBarMouseDown?.Invoke(this, e);
        }
        public static readonly RoutedEvent ModeSwitchClickEvent = EventManager.RegisterRoutedEvent(
    "ModeSwitchClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));

        public event RoutedEventHandler ModeSwitchClick
        {
            add => AddHandler(ModeSwitchClickEvent, value);
            remove => RemoveHandler(ModeSwitchClickEvent, value);
        }
        private void OnModeSwitchClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ModeSwitchClickEvent));
        }
        public void UpdateModeIcon(bool isViewMode)
        {
            string resourceKey = isViewMode ? "Paint_Mode_Image" : "View_Mode_Image";

            if (ModeIconImage != null)
            {
                // 从资源字典中查找 DrawingImage
                var newImage = Application.Current.TryFindResource(resourceKey) as Geometry;

                // 如果在当前 Control 资源里找不到，就去全局找
                if (newImage == null)
                    newImage = this.TryFindResource(resourceKey) as Geometry;

                if (newImage != null)
                {
                    ModeIconImage.Data = newImage;
                }

                // 更新提示文字
                ModeSwitchButton.ToolTip = isViewMode
             ? LocalizationManager.GetString("L_Mode_Switch_ToPaint")
             : LocalizationManager.GetString("L_Mode_Switch_ToView");
            }
        }
        public event RoutedEventHandler NewClick;
        public event RoutedEventHandler OpenClick;
        public event RoutedEventHandler OpenWorkspaceClick;
        public event RoutedEventHandler SaveClick;
        public event RoutedEventHandler SaveAsClick;
        public event RoutedEventHandler ExitClick;

        // === 新增：是否允许点击Logo打开菜单 (由 MainWindow 控制) ===
        public bool IsLogoMenuEnabled { get; set; } = false;

        // === 新增：Logo 点击处理 ===
        private void OnAppIconMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果启用了Logo菜单（看图模式），则打开菜单并阻止窗口拖动
            if (IsLogoMenuEnabled)
            {
                if (AppIcon.ContextMenu != null)
                {
                    AppIcon.ContextMenu.PlacementTarget = AppIcon;
                    AppIcon.ContextMenu.IsOpen = true;
                }
                e.Handled = true; // 重要：阻止事件冒泡到 TitleBar 的拖拽逻辑
            }
            // 否则，什么都不做，让事件冒泡，允许拖拽窗口
        }

        private void OnNewClick(object sender, RoutedEventArgs e) => NewClick?.Invoke(this, e);
        private void OnOpenClick(object sender, RoutedEventArgs e) => OpenClick?.Invoke(this, e);
        private void OnOpenWorkspaceClick(object sender, RoutedEventArgs e) => OpenWorkspaceClick?.Invoke(this, e);
        private void OnSaveClick(object sender, RoutedEventArgs e) => SaveClick?.Invoke(this, e);
        private void OnSaveAsClick(object sender, RoutedEventArgs e) => SaveAsClick?.Invoke(this, e);
        private void OnExitClick(object sender, RoutedEventArgs e) => ExitClick?.Invoke(this, e);

        public event EventHandler<MouseButtonEventArgs> IconDragRequest;

        // 2. 用于记录鼠标按下时的坐标
        private Point _dragStartPoint;

        // 3. 鼠标按下预览：记录起点
        private void OnAppIconPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
        }

        // 4. 鼠标移动预览：判断是否构成拖拽
        private void OnAppIconPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(this);

                // 计算移动距离是否超过系统阈值 (防止手抖误触)
                if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    IconDragRequest?.Invoke(this, null);
                }
            }
        }

        private void Window_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            Point relativePoint = e.GetPosition(this);
            // 只有在特定区域且非看图模式下才触发
            if (relativePoint.Y < 60 && relativePoint.X > 20 && !((MainWindow)System.Windows.Application.Current.MainWindow).IsViewMode)
            {
                ((MainWindow)System.Windows.Application.Current.MainWindow).MaximizeWindowHandler();

                e.Handled = true;
            }
        }
    }
}
