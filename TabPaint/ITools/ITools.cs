
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static TabPaint.MainWindow;

//
//工具管理器
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void UpdateToolSelectionHighlight()
        {
            if (_router == null) return;

            var accentBrush = Application.Current.FindResource("ToolAccentBrush") as Brush;
            var accentSubtleBrush = Application.Current.FindResource("ToolAccentSubtleSelectedBrush") as Brush;

            // 1. 获取当前工具类型字符串
            string currentToolTag = _router.CurrentTool switch
            {
                SelectTool => "SelectTool",
                PenTool when _ctx.PenStyle == BrushStyle.Eraser => "EraserTool",
                PenTool when _ctx.PenStyle == BrushStyle.Pencil => "PenTool",
                EyedropperTool => "EyedropperTool",
                FillTool => "FillTool",
                TextTool => "TextTool",
                _ => ""
            };

            // 2. 检查是否处于折叠模式
            bool isCollapsedMode = MainToolBar.CollapsedToolsPanel.Visibility == Visibility.Visible;

            MainToolBar.ToolsMenuToggle.ClearValue(Control.BackgroundProperty);
            MainToolBar.ToolsMenuToggle.ClearValue(Control.BorderBrushProperty);

            // 获取所有常规工具按钮
            var toolControls = new Control[] {
        MainToolBar.PickColorButton, MainToolBar.EraserButton, MainToolBar.SelectButton,
        MainToolBar.FillButton, MainToolBar.TextButton, MainToolBar.PenButton
    };

            // 清除所有展开按钮及特殊切换按钮的高亮
            foreach (var ctrl in toolControls)
            {
                ctrl.ClearValue(Control.BorderBrushProperty);
                ctrl.ClearValue(Control.BackgroundProperty);
            }
            MainToolBar.BrushToggle.ClearValue(Control.BorderBrushProperty);
            MainToolBar.BrushToggle.ClearValue(Control.BackgroundProperty);
            MainToolBar.ShapeToggle.ClearValue(Control.BorderBrushProperty);
            MainToolBar.ShapeToggle.ClearValue(Control.BackgroundProperty);
            bool isBasicTool = !string.IsNullOrEmpty(currentToolTag);

            if (isBasicTool)
            {
                if (isCollapsedMode)
                {
                    // --- 折叠模式逻辑 ---
                    MainToolBar.ToolsMenuToggle.BorderBrush = accentBrush;
                    MainToolBar.ToolsMenuToggle.Background = accentSubtleBrush;

                    UpdateCollapsedButtonIcon(currentToolTag);
                    UpdateCollapsedMenuHighlight(currentToolTag);
                }
                else
                {
                    // --- 展开模式逻辑 ---
                    Control target = _router.CurrentTool switch
                    {
                        EyedropperTool => MainToolBar.PickColorButton,
                        FillTool => MainToolBar.FillButton,
                        SelectTool => MainToolBar.SelectButton,
                        TextTool => MainToolBar.TextButton,
                        PenTool when _ctx.PenStyle == BrushStyle.Eraser => MainToolBar.EraserButton,
                        PenTool when _ctx.PenStyle == BrushStyle.Pencil => MainToolBar.PenButton,
                        _ => null
                    };

                    if (target != null)
                    {
                        target.BorderBrush = accentBrush;
                        target.Background = accentSubtleBrush;
                    }
                }
            }
            else
            {
                if (_router.CurrentTool is PenTool && _ctx.PenStyle != BrushStyle.Eraser && _ctx.PenStyle != BrushStyle.Pencil)
                {
                    // 1. 高亮主按钮
                    MainToolBar.BrushToggle.BorderBrush = accentBrush;
                    MainToolBar.BrushToggle.Background = accentSubtleBrush;

                    string brushTag = _ctx.PenStyle.ToString();

                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupBrush, brushTag);
                }
                else
                {
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupBrush, null);
                }
                if (_router.CurrentTool is ShapeTool shapeTool)
                {
                    MainToolBar.ShapeToggle.BorderBrush = accentBrush;
                    MainToolBar.ShapeToggle.Background = accentSubtleBrush;

                    string shapeTag = shapeTool.CurrentShapeType.ToString();

                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupShape, shapeTag);
                }
                else
                {
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupShape, null);
                }
            }
        }
        private void UpdateSubMenuHighlight(System.Windows.Controls.Primitives.Popup popup, string targetTag)
        {
            if (popup?.Child is Border border && border.Child is StackPanel panel)
            {
                foreach (var item in panel.Children)
                {
                    if (item is MenuItem menuItem)
                    {
                        if (targetTag != null && menuItem.Tag?.ToString() == targetTag)
                        {
                            // 选中样式
                            menuItem.Background = Application.Current.FindResource("ToolAccentSubtleHoverBrush") as Brush;


                            menuItem.FontWeight = FontWeights.Bold;
                        }
                        else
                        {
                            // 恢复默认
                            menuItem.ClearValue(Control.BackgroundProperty);
                            menuItem.ClearValue(Control.BorderBrushProperty);
                            menuItem.ClearValue(Control.BorderThicknessProperty);
                            menuItem.ClearValue(Control.FontWeightProperty);
                        }
                    }
                }
            }
        }
        private void UpdateCollapsedButtonIcon(string toolTag)
        {
            MainToolBar.CurrentToolIcon.Visibility = Visibility.Collapsed;

            object resource = null;
            string toolName = "";

            try
            {
                switch (toolTag)
                {
                    case "SelectTool":
                        resource = FindResource("Select_Image"); // 它是 Geometry
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Select");
                        break;

                    case "PenTool":
                        resource = FindResource("Pencil_Image"); // 它是 ImageSource
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Pen");
                        break;

                    case "EyedropperTool":
                        resource = FindResource("Pick_Colour_Image");
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Eyedropper");
                        break;

                    case "EraserTool":
                        resource = FindResource("Eraser_Image");
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Eraser");
                        break;

                    case "FillTool":
                        resource = FindResource("Fill_Bucket_Image");
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Fill");
                        break;

                    case "TextTool":
                        resource = FindResource("Text_Image"); // 它是 Geometry
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Text");
                        break;
                }

                // 4. 应用资源到 UI
                if (resource is ImageSource imgSrc)
                {
                    // 如果是图片资源
                    MainToolBar.CurrentToolIcon.Source = imgSrc;
                    MainToolBar.CurrentToolIcon.Visibility = Visibility.Visible;
                }

                // 5. 更新提示文字
                if (!string.IsNullOrEmpty(toolName))
                {
                    MainToolBar.ToolsMenuToggle.ToolTip = toolName;
                }
            }
            catch (Exception ex)
            {
                // 防御性编程：如果资源找不到，避免崩溃
                Console.WriteLine($"Error loading icon resource: {ex.Message}");
            
        }

        }
        private void UpdateCollapsedMenuHighlight(string toolTag)
        {
            foreach (var item in MainToolBar.CollapsedMenuItems.Children)
            {
                if (item is MenuItem menuItem)
                {
                    if (menuItem.Tag?.ToString() == toolTag)
                    {
                        menuItem.Background = Application.Current.FindResource("ListItemSelectedBackgroundBrush") as Brush  ; // 浅蓝色高亮
                        menuItem.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        menuItem.ClearValue(Control.BackgroundProperty);
                        menuItem.ClearValue(Control.FontWeightProperty);
                    }
                }
            }
        }







        public interface ITool
        {
            string Name { get; }
            System.Windows.Input.Cursor Cursor { get; }
            void Cleanup(ToolContext ctx);
            void StopAction(ToolContext ctx);
            void OnPointerDown(ToolContext ctx, Point viewPos);
            void OnPointerMove(ToolContext ctx, Point viewPos);
            void OnPointerUp(ToolContext ctx, Point viewPos);
            void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e);
            void SetCursor(ToolContext ctx);
        }

        public abstract class ToolBase : ITool
        {
            public abstract string Name { get; }
            public virtual System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Arrow;
            public virtual void OnPointerDown(ToolContext ctx, Point viewPos) { }
            public virtual void OnPointerMove(ToolContext ctx, Point viewPos) { }
            public virtual void OnPointerUp(ToolContext ctx, Point viewPos) { }
            public virtual void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e) { }
            public virtual void Cleanup(ToolContext ctx) { }
            public virtual void StopAction(ToolContext ctx) { }
            public virtual void SetCursor(ToolContext ctx) { }
        }

        public class ToolContext
        {
            public CanvasSurface Surface { get; }
            public UndoRedoManager Undo { get; }
            public Color PenColor { get; set; } = Colors.Black;
            public Color EraserColor { get; set; } = Colors.White;
            public double PenThickness { get; set; } = 5.0;
            public Image ViewElement { get; } // 例如 DrawImage
            public WriteableBitmap Bitmap => Surface.Bitmap;
            public Image SelectionPreview { get; } // 预览层
            public Canvas SelectionOverlay { get; }
            public Canvas EditorOverlay { get; }
            public BrushStyle PenStyle { get; set; } = BrushStyle.Pencil;

            // 文档状态
            // public string CurrentFilePath { get; set; } = string.Empty;
            public bool IsDirty { get; set; } = false;
            private readonly IInputElement _captureElement;
            public ToolContext(CanvasSurface surface, UndoRedoManager undo, Image viewElement, Image previewElement, Canvas overlayElement, Canvas EditorElement, IInputElement captureElement)
            {
                Surface = surface;
                Undo = undo;
                ViewElement = viewElement;
                SelectionPreview = previewElement;
                SelectionOverlay = overlayElement; // ← 保存引用
                EditorOverlay = EditorElement;
                _captureElement = captureElement;
            }

            // 视图坐标 -> 像素坐标
            public Point ToPixel(Point viewPos)
            {
                var bmp = Surface.Bitmap;
                if(bmp==null)return new Point(0,0);
                double sx = bmp.PixelWidth / ViewElement.ActualWidth;
                double sy = bmp.PixelHeight / ViewElement.ActualHeight;
                return new Point(viewPos.X * sx, viewPos.Y * sy);
            }

            public void CapturePointer() { _captureElement?.CaptureMouse(); }

            public void ReleasePointerCapture() { _captureElement?.ReleaseMouseCapture(); }
        }


        public class ToolRegistry//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        {
            public ITool Pen { get; } = new PenTool();
            //public ITool Eraser { get; } = new EraserTool();
            public ITool Eyedropper { get; } = new EyedropperTool();
            public ITool Fill { get; } = new FillTool();
            public ITool Select { get; } = new SelectTool();//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            public ITool Text { get; } = new TextTool();
            public ITool Shape { get; } = new ShapeTool();
        }

        public class InputRouter /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        {
            private readonly ToolContext _ctx;
            public ITool CurrentTool { get; private set; }

            public InputRouter(ToolContext ctx, ITool defaultTool)
            {
                _ctx = ctx;
                CurrentTool = defaultTool;

            }

            public void ViewElement_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
            {


                var position = e.GetPosition(_ctx.ViewElement);
                if (_ctx.Surface.Bitmap != null)
                {

                    Point px = _ctx.ToPixel(position);
                    ((MainWindow)System.Windows.Application.Current.MainWindow).MousePosition = $"{(int)px.X}, {(int)px.Y}"+ LocalizationManager.GetString("L_Main_Unit_Pixel");
                }
                CurrentTool.OnPointerMove(_ctx, position);
            }// 定义高亮颜色

            public SelectTool GetSelectTool()
            {
                var mw = (MainWindow)Application.Current.MainWindow;
                return mw._tools.Select as SelectTool;
            }

            public void CleanUpSelectionandShape()
            {
                var mw = (MainWindow)Application.Current.MainWindow;
                if (CurrentTool is SelectTool selTool)
                {
                    if (selTool._selectionData != null)
                        selTool.GiveUpSelection(_ctx);
                    selTool.Cleanup(_ctx);

                }
                if (mw._router.CurrentTool is ShapeTool shapetool)
                {
                    shapetool.GiveUpSelection(_ctx);
                    GetSelectTool()?.Cleanup(_ctx);
                }
                if (mw._router.CurrentTool is TextTool textTool)
                {
                    textTool.Cleanup(_ctx);
                }

            }

            public void SetTool(ITool tool)
            {

                if (CurrentTool == tool) return; // Optional: Don't do work if it's the same tool.
                CleanUpSelectionandShape();
                CurrentTool?.Cleanup(_ctx);
                CurrentTool = tool;
                tool.SetCursor(_ctx);
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                mw.AutoSetFloatBarVisibility();
                mw._router.GetSelectTool().UpdateStatusBarSelectionSize();
                mw.UpdateToolSelectionHighlight();
                mw.UpdateGlobalToolSettingsKey();
            }
            public void ViewElement_MouseDown(object sender, MouseButtonEventArgs e)
    => CurrentTool?.OnPointerDown(_ctx, e.GetPosition(_ctx.ViewElement));

            public void ViewElement_MouseUp(object sender, MouseButtonEventArgs e)
                => CurrentTool?.OnPointerUp(_ctx, e.GetPosition(_ctx.ViewElement));
            public void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            {//快捷键
                CurrentTool.OnKeyDown(_ctx, e);
            }
        }


    }
}