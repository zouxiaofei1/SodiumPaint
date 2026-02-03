//
//ToolBarControl.xaml.cs
//工具栏控件，包含画笔、选区、填充、文字等工具的选择按钮，以及颜色板和旋转镜像功能。
//
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TabPaint.Controls
{
    public partial class ToolBarControl : UserControl
    {
        public static readonly RoutedEvent PenClickEvent = RegisterEvent("PenClick");
        public static readonly RoutedEvent PickColorClickEvent = RegisterEvent("PickColorClick");
        public static readonly RoutedEvent EraserClickEvent = RegisterEvent("EraserClick");
        public static readonly RoutedEvent SelectClickEvent = RegisterEvent("SelectClick");
        public static readonly RoutedEvent FillClickEvent = RegisterEvent("FillClick");
        public static readonly RoutedEvent TextClickEvent = RegisterEvent("TextClick");
        public static readonly RoutedEvent BrushStyleClickEvent = RegisterEvent("BrushStyleClick");
        public static readonly RoutedEvent ShapeStyleClickEvent = RegisterEvent("ShapeStyleClick");
        public static readonly RoutedEvent CropClickEvent = RegisterEvent("CropClick");
        public static readonly RoutedEvent RotateLeftClickEvent = RegisterEvent("RotateLeftClick");
        public static readonly RoutedEvent RotateRightClickEvent = RegisterEvent("RotateRightClick");
        public static readonly RoutedEvent Rotate180ClickEvent = RegisterEvent("Rotate180Click");
        public static readonly RoutedEvent FlipVerticalClickEvent = RegisterEvent("FlipVerticalClick");
        public static readonly RoutedEvent FlipHorizontalClickEvent = RegisterEvent("FlipHorizontalClick");
        public static readonly RoutedEvent CustomColorClickEvent = RegisterEvent("CustomColorClick");
        public static readonly RoutedEvent ColorOneClickEvent = RegisterEvent("ColorOneClick");
        public static readonly RoutedEvent ColorTwoClickEvent = RegisterEvent("ColorTwoClick");
        public static readonly RoutedEvent ColorButtonClickEvent = RegisterEvent("ColorButtonClick");
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
        public static readonly RoutedEvent BrushMainClickEvent = RegisterEvent("BrushMainClick");
        public event RoutedEventHandler BrushMainClick { add => AddHandler(BrushMainClickEvent, value); remove => RemoveHandler(BrushMainClickEvent, value); }

        // 新增：当点击形状主按钮（左侧）时触发
        public static readonly RoutedEvent ShapeMainClickEvent = RegisterEvent("ShapeMainClick");
        public event RoutedEventHandler ShapeMainClick { add => AddHandler(ShapeMainClickEvent, value); remove => RemoveHandler(ShapeMainClickEvent, value); }
        private void OnBrushMainButtonClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BrushMainClickEvent));
        private void OnShapeMainButtonClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ShapeMainClickEvent));
        public ToolBarControl()
        {
            InitializeComponent();
        }
        public void UpdateBrushIcon(string resourceKey, bool isPathData)
        {
            if (isPathData)
            {
                var pathData = TryFindResource(resourceKey) as Geometry;
                if (pathData != null)
                {
                    var path = new System.Windows.Shapes.Path
                    {
                        Data = pathData,
                        Stretch = Stretch.Uniform,
                        Fill = (Brush)TryFindResource("IconFillBrush"), // 确保你有这个Brush资源
                        Width = 16,
                        Height = 16
                    };
                    CurrentBrushIconHost.Content = path;
                }
            }
            else
            {
                // 如果是 ImageSource (如 png/ico)
                var imgSrc = TryFindResource(resourceKey) as ImageSource;
                if (imgSrc != null)
                {
                    var img = new Image { Source = imgSrc, Width = 16, Height = 16 };
                    CurrentBrushIconHost.Content = img;
                }
            }
        }

        public event RoutedEventHandler SelectMainClick;   // 对应主按钮点击
        public event RoutedEventHandler SelectStyleClick;  // 对应下拉菜单点击
        private void OnSelectMainButtonClick(object sender, RoutedEventArgs e)
        {
            SelectMainClick?.Invoke(this, e);
        }

        // 2. 下拉菜单项点击转发
        private void OnSelectStyleClick_Forward(object sender, RoutedEventArgs e)
        {
            // 关闭 Popup (可选，根据你的交互需求)
            SubMenuPopupSelect.IsOpen = false;

            // 转发事件给 MainWindow
            SelectStyleClick?.Invoke(sender, e);
        }
        public void UpdateShapeIcon(string resourceKey)
        {
            var pathData = TryFindResource(resourceKey) as Geometry;
            if (pathData != null)
            {
                var path = new System.Windows.Shapes.Path
                {
                    Data = pathData,
                    Stretch = Stretch.Uniform,
                    Fill = Brushes.Transparent,
                    Stroke = (Brush)TryFindResource("IconFillBrush"),
                    StrokeThickness = 1.5,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    Width = 16,
                    Height = 16
                };
                CurrentShapeIconHost.Content = path;
            }
        }
   

        private static RoutedEvent RegisterEvent(string name)
        {
            return EventManager.RegisterRoutedEvent(name, RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolBarControl));
        }
        private void OnPenClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PenClickEvent));
        private void OnPickColorClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PickColorClickEvent));
        private void OnEraserClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(EraserClickEvent));
        private void OnSelectClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SelectClickEvent));
        private void OnFillClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FillClickEvent));
        private void OnTextClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(TextClickEvent));
        private void OnBrushStyleClick_Forward(object sender, RoutedEventArgs e)
        {
            BrushToggle.IsChecked = false;
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
                if (ExpandedToolsPanel.Visibility == Visibility.Visible)
                {
                    ExpandedToolsPanel.Visibility = Visibility.Collapsed;
                    CollapsedToolsPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
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
            double rightPadding = 45.0;

            double leftUsedWidth = 0;
            try
            {
                Point p = BasicColorsGrid.TranslatePoint(new Point(0, 0), this);
                leftUsedWidth = p.X;
            }
            catch
            {
                leftUsedWidth = 600;
            }

            double availableWidth = this.ActualWidth - leftUsedWidth - rightPadding;
            if (availableWidth < 0) availableWidth = 0;

            // 4. 计算能放下多少列 (每列宽 buttonWidth)
            int visibleColumns = (int)(availableWidth / buttonWidth);

            if (visibleColumns > 11) visibleColumns = 11;
            if (visibleColumns < 1) visibleColumns = 1; // 至少显示1列

            BasicColorsGrid.Columns = visibleColumns;

            int totalItems = BasicColorsGrid.Children.Count;
            int originalColumns = 11;

            for (int i = 0; i < totalItems; i++)
            {
                UIElement child = BasicColorsGrid.Children[i];

                int originalColIndex = i % originalColumns;

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
            if (sender is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                Color selectedColor = brush.Color;

                mw.SelectedBrush = new SolidColorBrush(selectedColor);

                mw._ctx.PenColor = selectedColor;

                mw.UpdateCurrentColor(selectedColor, mw.useSecondColor);

                mw.UpdateColorHighlight(); 
            }
        }

    }
}
