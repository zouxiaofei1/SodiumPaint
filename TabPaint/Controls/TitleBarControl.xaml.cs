//
//TitleBarControl.xaml.cs
//自定义标题栏控件，提供窗口最小化、最大化、关闭功能，以及模式切换和Logo菜单交互。
//
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Controls
{
  
    public partial class TitleBarControl : UserControl
    {
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


        private void OnMinimizeClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MinimizeClickEvent));
        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MaximizeRestoreClickEvent));
        private void OnCloseClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CloseClickEvent));

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Button ||
                (e.OriginalSource is FrameworkElement fe && fe.TemplatedParent is System.Windows.Controls.Button))
            {
                return;
            }
            if (e.OriginalSource == AppIcon || e.Source == AppIcon)
            {
                return;
            }
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
                var newImage = Application.Current.TryFindResource(resourceKey) as Geometry;

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
        public event RoutedEventHandler LogoMiddleClick;
        public bool IsLogoMenuEnabled { get; set; } = false;
        private void OnAppIconMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                LogoMiddleClick?.Invoke(this, e);
                e.Handled = true;
                return;
            }

            // 原有逻辑：处理左键菜单 (增加 Left 按钮判断)
            if (e.ChangedButton == MouseButton.Left && IsLogoMenuEnabled)
            {
                if (AppIcon.ContextMenu != null)
                {
                    AppIcon.ContextMenu.PlacementTarget = AppIcon;
                    AppIcon.ContextMenu.IsOpen = true;
                }
                e.Handled = true;
            }
        }
        // 定义事件
        public static readonly RoutedEvent HelpClickEvent = EventManager.RegisterRoutedEvent(
            "HelpClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));

        public event RoutedEventHandler HelpClick
        {
            add => AddHandler(HelpClickEvent, value);
            remove => RemoveHandler(HelpClickEvent, value);
        }

        // 触发事件
        private void OnHelpClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(HelpClickEvent));
        }

        private void OnNewClick(object sender, RoutedEventArgs e) => NewClick?.Invoke(this, e);
        private void OnOpenClick(object sender, RoutedEventArgs e) => OpenClick?.Invoke(this, e);
        private void OnOpenWorkspaceClick(object sender, RoutedEventArgs e) => OpenWorkspaceClick?.Invoke(this, e);
        private void OnSaveClick(object sender, RoutedEventArgs e) => SaveClick?.Invoke(this, e);
        private void OnSaveAsClick(object sender, RoutedEventArgs e) => SaveAsClick?.Invoke(this, e);
        private void OnExitClick(object sender, RoutedEventArgs e) => ExitClick?.Invoke(this, e);

        public event EventHandler<MouseButtonEventArgs> IconDragRequest;
        private Point _dragStartPoint;

        private void OnAppIconPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
        }
        private void OnAppIconPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(this);
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
            if (relativePoint.Y < 60 && relativePoint.X > 20 && !((MainWindow)System.Windows.Application.Current.MainWindow).IsViewMode)
            {
                ((MainWindow)System.Windows.Application.Current.MainWindow).MaximizeWindowHandler();

                e.Handled = true;
            }
        }
    }
}
