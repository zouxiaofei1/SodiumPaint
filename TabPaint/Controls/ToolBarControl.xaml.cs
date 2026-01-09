using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            RightColorSeperator.Visibility = e.NewSize.Width>435 ? Visibility.Visible : Visibility.Collapsed;
            UpdateColorPaletteVisibility();
        }
        private void UpdateColorPaletteVisibility()
        {
            if (BasicColorsGrid == null) return;

            // 1. 基础配置
            double buttonWidth = 19.0; // 16px宽度 + 3px Margin(左右各1.5)
            double rightPadding = 45.0; //右侧预留给其他控件(如Tab切换、关闭按钮等)的空间，根据实际情况调整

            // 2. 获取 BasicColorsGrid 在窗口中的 X 坐标 (即左侧已占用的宽度)
            double leftUsedWidth = 0;
            try
            {
                Point p = BasicColorsGrid.TranslatePoint(new Point(0, 0), this);
                leftUsedWidth = p.X;
            }
            catch
            {
                // 窗口刚初始化可能获取不到，给个默认值
                leftUsedWidth = 600;
            }

            // 3. 计算颜色栏可用的总宽度
            double availableWidth = this.ActualWidth - leftUsedWidth - rightPadding;
            if (availableWidth < 0) availableWidth = 0;

            // 4. 计算能放下多少列 (每列宽 buttonWidth)
            int visibleColumns = (int)(availableWidth / buttonWidth);

            // 限制最大列数为10 (你的XML里原本是10列)
            if (visibleColumns > 10) visibleColumns = 10;
            if (visibleColumns < 1) visibleColumns = 1; // 至少显示1列

            // 5. 核心逻辑：控制显示
            // 假设你的颜色共有20个，分两行，每行10个。
            // 索引 0-9 是第一行，10-19 是第二行。
            // 如果 visibleColumns = 8，我们需要显示 0-7 和 10-17，隐藏 8,9 和 18,19。

            // 更新 Grid 的列数定义，这样布局更紧凑
            BasicColorsGrid.Columns = visibleColumns;

            int totalItems = BasicColorsGrid.Children.Count;
            // 假设是标准的2行布局 (根据你的XML: 鲜艳色系一行，深色系一行)
            int originalColumns = 10;

            for (int i = 0; i < totalItems; i++)
            {
                UIElement child = BasicColorsGrid.Children[i];

                // 计算该元素原本所在的 列索引 (0-9)
                // 第一行是 0-9, 第二行是 10-19
                // 这是一个简单的取模运算，假设原本设计是10列
                int originalColIndex = i % originalColumns;

                // 如果该元素原本的列索引 小于 我们现在允许的列数，则显示
                if (originalColIndex < visibleColumns)
                {
                    if (child.Visibility != Visibility.Visible)
                        child.Visibility = Visibility.Visible;
                }
                else
                {
                    if (child.Visibility != Visibility.Collapsed)
                        child.Visibility = Visibility.Collapsed;
                }
            }
        }


        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            // 1. 确保点击源是按钮且有背景色
            if (sender is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                Color selectedColor = brush.Color;

                mw.SelectedBrush = new SolidColorBrush(selectedColor);

                // 4. 更新核心绘图上下文 (_ctx)
                mw._ctx.PenColor = selectedColor;

                mw.UpdateCurrentColor(selectedColor, mw.useSecondColor);

                mw.UpdateColorHighlight(); 
            }
        }

    }
}
