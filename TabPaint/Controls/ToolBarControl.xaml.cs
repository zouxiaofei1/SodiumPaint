using System.Windows;
using System.Windows.Controls;

namespace TabPaint.Controls
{
    public partial class ToolBarControl : UserControl
    {
        // ================= 定义路由事件 =================

        // 基础工具
        public static readonly RoutedEvent PenClickEvent = RegisterEvent("PenClick");
        public static readonly RoutedEvent PickColorClickEvent = RegisterEvent("PickColorClick");
        public static readonly RoutedEvent EraserClickEvent = RegisterEvent("EraserClick");
        public static readonly RoutedEvent SelectClickEvent = RegisterEvent("SelectClick");
        public static readonly RoutedEvent FillClickEvent = RegisterEvent("FillClick");
        public static readonly RoutedEvent TextClickEvent = RegisterEvent("TextClick");

        // 样式点击 (画刷/形状) - 需要传递 Tag 或 Source
        public static readonly RoutedEvent BrushStyleClickEvent = RegisterEvent("BrushStyleClick");
        public static readonly RoutedEvent ShapeStyleClickEvent = RegisterEvent("ShapeStyleClick");

        // 编辑操作
        public static readonly RoutedEvent CropClickEvent = RegisterEvent("CropClick");
        public static readonly RoutedEvent RotateLeftClickEvent = RegisterEvent("RotateLeftClick");
        public static readonly RoutedEvent RotateRightClickEvent = RegisterEvent("RotateRightClick");
        public static readonly RoutedEvent Rotate180ClickEvent = RegisterEvent("Rotate180Click");
        public static readonly RoutedEvent FlipVerticalClickEvent = RegisterEvent("FlipVerticalClick");
        public static readonly RoutedEvent FlipHorizontalClickEvent = RegisterEvent("FlipHorizontalClick");

        // 颜色操作
        public static readonly RoutedEvent CustomColorClickEvent = RegisterEvent("CustomColorClick");
        public static readonly RoutedEvent ColorOneClickEvent = RegisterEvent("ColorOneClick");
        public static readonly RoutedEvent ColorTwoClickEvent = RegisterEvent("ColorTwoClick");
        public static readonly RoutedEvent ColorButtonClickEvent = RegisterEvent("ColorButtonClick");

        // ================= 事件包装器 =================

        public event RoutedEventHandler PenClick { add => AddHandler(PenClickEvent, value); remove => RemoveHandler(PenClickEvent, value); }
        public event RoutedEventHandler PickColorClick { add => AddHandler(PickColorClickEvent, value); remove => RemoveHandler(PickColorClickEvent, value); }
        public event RoutedEventHandler EraserClick { add => AddHandler(EraserClickEvent, value); remove => RemoveHandler(EraserClickEvent, value); }
        public event RoutedEventHandler SelectClick { add => AddHandler(SelectClickEvent, value); remove => RemoveHandler(SelectClickEvent, value); }
        public event RoutedEventHandler FillClick { add => AddHandler(FillClickEvent, value); remove => RemoveHandler(FillClickEvent, value); }
        public event RoutedEventHandler TextClick { add => AddHandler(TextClickEvent, value); remove => RemoveHandler(TextClickEvent, value); }

        public event RoutedEventHandler BrushStyleClick { add => AddHandler(BrushStyleClickEvent, value); remove => RemoveHandler(BrushStyleClickEvent, value); }
        public event RoutedEventHandler ShapeStyleClick { add => AddHandler(ShapeStyleClickEvent, value); remove => RemoveHandler(ShapeStyleClickEvent, value); }

        public event RoutedEventHandler CropClick { add => AddHandler(CropClickEvent, value); remove => RemoveHandler(CropClickEvent, value); }
        public event RoutedEventHandler RotateLeftClick { add => AddHandler(RotateLeftClickEvent, value); remove => RemoveHandler(RotateLeftClickEvent, value); }
        public event RoutedEventHandler RotateRightClick { add => AddHandler(RotateRightClickEvent, value); remove => RemoveHandler(RotateRightClickEvent, value); }
        public event RoutedEventHandler Rotate180Click { add => AddHandler(Rotate180ClickEvent, value); remove => RemoveHandler(Rotate180ClickEvent, value); }
        public event RoutedEventHandler FlipVerticalClick { add => AddHandler(FlipVerticalClickEvent, value); remove => RemoveHandler(FlipVerticalClickEvent, value); }
        public event RoutedEventHandler FlipHorizontalClick { add => AddHandler(FlipHorizontalClickEvent, value); remove => RemoveHandler(FlipHorizontalClickEvent, value); }

        public event RoutedEventHandler CustomColorClick { add => AddHandler(CustomColorClickEvent, value); remove => RemoveHandler(CustomColorClickEvent, value); }
        public event RoutedEventHandler ColorOneClick { add => AddHandler(ColorOneClickEvent, value); remove => RemoveHandler(ColorOneClickEvent, value); }
        public event RoutedEventHandler ColorTwoClick { add => AddHandler(ColorTwoClickEvent, value); remove => RemoveHandler(ColorTwoClickEvent, value); }
        public event RoutedEventHandler ColorButtonClick { add => AddHandler(ColorButtonClickEvent, value); remove => RemoveHandler(ColorButtonClickEvent, value); }


        public ToolBarControl()
        {
            InitializeComponent();
        }

        // 辅助注册方法
        private static RoutedEvent RegisterEvent(string name)
        {
            return EventManager.RegisterRoutedEvent(name, RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolBarControl));
        }

        // ================= 内部转发方法 (XAML Click 指向这里) =================

        private void OnPenClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PenClickEvent));
        private void OnPickColorClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PickColorClickEvent));
        private void OnEraserClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(EraserClickEvent));
        private void OnSelectClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SelectClickEvent));
        private void OnFillClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FillClickEvent));
        private void OnTextClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(TextClickEvent));

        // 注意：MenuItem 的 Tag 和 Source 需要保留，所以直接传 e，或者把 Source 设为触发的 MenuItem
        private void OnBrushStyleClick_Forward(object sender, RoutedEventArgs e)
        {
            // 关闭 Popup
            BrushToggle.IsChecked = false;
            // 转发事件，保持 Source 为被点击的 MenuItem，这样 MainWindow 可以读取 Tag
            RaiseEvent(new RoutedEventArgs(BrushStyleClickEvent, sender));
        }

        private void OnShapeStyleClick_Forward(object sender, RoutedEventArgs e)
        {
            ShapeToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(ShapeStyleClickEvent, sender));
        }

        private void CropMenuItem_Click_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CropClickEvent));
        private void OnRotateLeftClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RotateLeftClickEvent));
        private void OnRotateRightClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RotateRightClickEvent));

        private void OnRotate180Click_Forward(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(Rotate180ClickEvent));
        }
        private void OnFlipVerticalClick_Forward(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(FlipVerticalClickEvent));
        }
        private void OnFlipHorizontalClick_Forward(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(FlipHorizontalClickEvent));
        }

        private void OnCustomColorClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CustomColorClickEvent));
        private void OnColorOneClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ColorOneClickEvent, sender)); // 需要 sender 获取 Tag
        private void OnColorTwoClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ColorTwoClickEvent, sender));

        private void OnColorButtonClick_Forward(object sender, RoutedEventArgs e)
        {
            // 列表中的颜色块，sender是Button，DataContext是颜色Brush
            RaiseEvent(new RoutedEventArgs(ColorButtonClickEvent, sender));
        }
        private void OnToolBarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double collapseThreshold = 580;

            if (e.NewSize.Width < collapseThreshold)
            {
                // 窗口变窄：隐藏展开面板，显示折叠菜单
                if (ExpandedToolsPanel.Visibility == Visibility.Visible)
                {
                    ExpandedToolsPanel.Visibility = Visibility.Collapsed;
                    CollapsedToolsPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // 窗口够宽：显示展开面板，隐藏折叠菜单
                if (ExpandedToolsPanel.Visibility == Visibility.Collapsed)
                {
                    ExpandedToolsPanel.Visibility = Visibility.Visible;
                    CollapsedToolsPanel.Visibility = Visibility.Collapsed;

                    // 也可以在这里顺便关闭 Popup，防止菜单悬浮在空中
                    ToolsMenuToggle.IsChecked = false;
                }
            }
            UpdateColorPaletteVisibility();
        }
        private void UpdateColorPaletteVisibility()
        {
            if (ColorPaletteItems == null || ColorPaletteItems.Items.Count == 0) return;

            double itemWidth = 28.0;

            double leftUsedWidth = 620; // 保底默认值

            try
            {
                // 获取 ColorPaletteItems 在 ToolBarControl 中的相对位置
                Point relativePoint = ColorPaletteItems.TranslatePoint(new Point(0, 0), this);
                // 如果 X > 0 说明布局已完成，使用真实坐标作为左侧已占用宽度
                if (relativePoint.X > 0)
                {
                    leftUsedWidth = relativePoint.X;
                }
            }
            catch { }

            // 3. 计算留给颜色列表的剩余空间
            // 总宽度 - 左侧占用 - 右侧留白(比如20px)
            double availableWidth = this.ActualWidth - leftUsedWidth - 20;

            if (availableWidth < 0) availableWidth = 0;

            // 4. 计算能放下多少个按钮
            int visibleCount = (int)(availableWidth / itemWidth);

            // 5. 遍历容器设置可见性
            var generator = ColorPaletteItems.ItemContainerGenerator;
            for (int i = 0; i < ColorPaletteItems.Items.Count; i++)
            {
                // 获取第 i 个数据对应的 UI 元素 (ContentPresenter)
                var container = generator.ContainerFromIndex(i) as UIElement;
                if (container != null)
                {
                    // 如果索引小于允许显示的个数，则显示，否则折叠
                    Visibility targetVisibility = (i < visibleCount) ? Visibility.Visible : Visibility.Collapsed;

                    // 只有状态改变时才赋值，减少重绘开销
                    if (container.Visibility != targetVisibility)
                    {
                        container.Visibility = targetVisibility;
                    }
                }
            }
        }
    }
}
