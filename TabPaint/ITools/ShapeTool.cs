using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TabPaint;
using static TabPaint.MainWindow;

public class ShapeTool : ToolBase
{
    public override string Name => "Shape";
    public override System.Windows.Input.Cursor Cursor => _isManipulating ? null : System.Windows.Input.Cursors.Cross;

    public enum ShapeType { Rectangle, Ellipse, Line, RoundedRectangle, Arrow }
    public ShapeType _currentShapeType = ShapeType.Rectangle;
    public ShapeType CurrentShapeType => _currentShapeType;
    private Point _startPoint;
    private bool _isDrawing;
    private bool _isManipulating = false;
    private System.Windows.Shapes.Shape _previewShape;
    private int lag = 0;
    public override void SetCursor(ToolContext ctx)
    {
        System.Windows.Input.Mouse.OverrideCursor = null;

        if (ctx.ViewElement != null)
        {
            ctx.ViewElement.Cursor = this.Cursor;
        }
    }

    public void SetShapeType(ShapeType type)
    {
        _currentShapeType = type;
    }

    private SelectTool GetSelectTool()
    {
        var mw = (MainWindow)Application.Current.MainWindow;
        return mw._tools.Select as SelectTool;
    }

    public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if (((MainWindow)System.Windows.Application.Current.MainWindow).IsViewMode) return;
        var selectTool = GetSelectTool();
        var px = ctx.ToPixel(viewPos);
        if (_isManipulating && selectTool != null)
        {
            if (!selectTool.HasActiveSelection)
            {
                _isManipulating = false;
                goto DrawingLogic; // 跳转到绘制逻辑
            }

            bool hitHandle = selectTool.HitTestHandle(px, selectTool._selectionRect) != SelectTool.ResizeAnchor.None;
            bool hitContent = selectTool.IsPointInSelection(px);

            if (hitHandle || hitContent)
            {
                // 转发事件给 SelectTool
                selectTool.OnPointerDown(ctx, viewPos);
                return;
            }
            else
            {
                selectTool.CommitSelection(ctx, true);
                selectTool.Cleanup(ctx);
                selectTool.lag = 0;
                _isManipulating = false;
                lag = 1;

            }
        }

    DrawingLogic:
      
        if(lag> 0)
        {
            lag--;
            return;
        }
        _startPoint = ctx.ToPixel(viewPos);
        _isDrawing = true;
        ctx.CapturePointer();

        // 初始化预览形状
        switch (_currentShapeType)
        {
            case ShapeType.Rectangle:
                _previewShape = new System.Windows.Shapes.Rectangle();
                break;
            case ShapeType.RoundedRectangle:
                _previewShape = new System.Windows.Shapes.Rectangle { RadiusX = 20, RadiusY = 20 };
                break;
            case ShapeType.Ellipse:
                _previewShape = new System.Windows.Shapes.Ellipse();
                break;
            case ShapeType.Line:
                _previewShape = new System.Windows.Shapes.Line();
                break;
            case ShapeType.Arrow:
                _previewShape = new System.Windows.Shapes.Path();
                break;
        }

        _previewShape.Stroke = new SolidColorBrush(ctx.PenColor);
        _previewShape.StrokeThickness = ctx.PenThickness;
        _previewShape.Fill = null;

        // 初始位置设置（线和箭头不需要设置 Left/Top）
        if (_currentShapeType != ShapeType.Line && _currentShapeType != ShapeType.Arrow)
        {
            Canvas.SetLeft(_previewShape, _startPoint.X);
            Canvas.SetTop(_previewShape, _startPoint.Y);
        }

        ctx.EditorOverlay.Children.Add(_previewShape);

    }

    public void GiveUpSelection(ToolContext ctx)
    {
        if (ctx == null) return;
    
        GetSelectTool()?.CommitSelection(ctx,true);
        GetSelectTool()?.Cleanup(ctx);
       // ctx.Undo.Undo();

    }

    public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if (_isManipulating)
        {
            GetSelectTool()?.OnPointerMove(ctx, viewPos);
            return;
        }

        // 2. 如果正在画新图形
        if (!_isDrawing || _previewShape == null) return;

        var current = ctx.ToPixel(viewPos);
        UpdatePreviewShape(_startPoint, current, ctx.PenThickness);

        int w = (int)Math.Abs(current.X - _startPoint.X);
        int h = (int)Math.Abs(current.Y - _startPoint.Y);

        var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
        mw.SelectionSize = string.Format(LocalizationManager.GetString("L_Selection_Size_Format"), w, h);
    }

    private BitmapSource RenderShapeToBitmapClipped(Point globalStart, Point globalEnd, Rect validBounds, Color color, double thickness, double dpiX, double dpiY)
    {
        // 位图的尺寸 = 交集矩形的尺寸 (保证不超过画布)
        int pixelWidth = (int)Math.Ceiling(validBounds.Width);
        int pixelHeight = (int)Math.Ceiling(validBounds.Height);

        if (pixelWidth <= 0 || pixelHeight <= 0) return null;

        DrawingVisual drawingVisual = new DrawingVisual();
        using (DrawingContext dc = drawingVisual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(-validBounds.X, -validBounds.Y));

            Pen pen = new Pen(new SolidColorBrush(color), thickness);

            // 线帽设置 (保持原逻辑)
            if (_currentShapeType == ShapeType.Rectangle)
            {
                pen.LineJoin = PenLineJoin.Miter;
                pen.StartLineCap = PenLineCap.Square;
                pen.EndLineCap = PenLineCap.Square;
            }
            else
            {
                pen.LineJoin = PenLineJoin.Round;
                pen.StartLineCap = PenLineCap.Round;
                pen.EndLineCap = PenLineCap.Round;
            }
            pen.Freeze();
            Point p1 = globalStart;
            Point p2 = globalEnd;
            Rect logicalRect = new Rect(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p1.X - p2.X),
                Math.Abs(p1.Y - p2.Y)
            );

            switch (_currentShapeType)
            {
                case ShapeType.Rectangle:
                    dc.DrawRectangle(null, pen, logicalRect);
                    break;
                case ShapeType.RoundedRectangle:
                    dc.DrawRoundedRectangle(null, pen, logicalRect, 20, 20);
                    break;
                case ShapeType.Ellipse:
                    // 椭圆圆心
                    dc.DrawEllipse(null, pen,
                        new Point(logicalRect.X + logicalRect.Width / 2.0, logicalRect.Y + logicalRect.Height / 2.0),
                        logicalRect.Width / 2.0, logicalRect.Height / 2.0);
                    break;
                case ShapeType.Line:
                    dc.DrawLine(pen, globalStart, globalEnd);
                    break;
                case ShapeType.Arrow:
                    var arrowGeo = BuildArrowGeometry(globalStart, globalEnd, Gethandlength(globalStart, globalEnd));
                    dc.DrawGeometry(null, pen, arrowGeo);
                    break;
            }

            dc.Pop(); // 弹出 Transform
        }

        RenderTargetBitmap bmp = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        bmp.Render(drawingVisual);
        bmp.Freeze();
        return bmp;
    }
    public override void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if (_isManipulating)
        {
            GetSelectTool()?.OnPointerUp(ctx, viewPos);
            return;
        }

        if (!_isDrawing) return;

        var endPoint = ctx.ToPixel(viewPos);
        _isDrawing = false;
        ctx.ReleasePointerCapture();

        if (_previewShape != null)
        {
            ctx.EditorOverlay.Children.Remove(_previewShape);
            _previewShape = null;
        }

        // 1. 计算逻辑包围盒 (鼠标拖拽的起止点)
        var rawRect = MakeRect(_startPoint, endPoint);
        if (rawRect.Width <= 1 || rawRect.Height <= 1) return;

        double arrowScale = Gethandlength(_startPoint, endPoint);
        double padding = ctx.PenThickness / 2.0 + 2;
        if (_currentShapeType == ShapeType.Arrow)
        {
            padding = (ctx.PenThickness/  2.0 + arrowScale/2.0) + 2;
        }
        // 形状在全局坐标系下的完整矩形 (可能包含负坐标或超出画布)
        Rect shapeGlobalBounds = new Rect(
            rawRect.X - padding,
            rawRect.Y - padding,
            rawRect.Width + padding * 2,
            rawRect.Height + padding * 2
        );
        Rect canvasBounds = new Rect(0, 0, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);

        Rect validBounds = Rect.Intersect(shapeGlobalBounds, canvasBounds);

        // 如果完全在画布外，直接不画
        if (validBounds == Rect.Empty || validBounds.Width <= 0 || validBounds.Height <= 0)
            return;

        var shapeBitmap = RenderShapeToBitmapClipped(_startPoint, endPoint, validBounds, ctx.PenColor, ctx.PenThickness, ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY);

        var selectTool = GetSelectTool();
        if (selectTool != null && shapeBitmap != null)
        {
            // 插入图片，不扩充画布 (因为我们已经裁剪到画布内了)
            selectTool.InsertImageAsSelection(ctx, shapeBitmap, false);
            int finalX = (int)validBounds.X;
            int finalY = (int)validBounds.Y;

            selectTool._selectionRect = new Int32Rect(finalX, finalY, shapeBitmap.PixelWidth, shapeBitmap.PixelHeight);
            selectTool._originalRect = selectTool._selectionRect;

            double uiScaleX = ctx.ViewElement.ActualWidth / ctx.Surface.Bitmap.PixelWidth;
            double uiScaleY = ctx.ViewElement.ActualHeight / ctx.Surface.Bitmap.PixelHeight;

            ctx.SelectionPreview.Width = selectTool._selectionRect.Width * uiScaleX;
            ctx.SelectionPreview.Height = selectTool._selectionRect.Height * uiScaleY;

            // 设置变换：将选区移动到计算出的裁剪位置
            var tg = new TransformGroup();
            tg.Children.Add(new ScaleTransform(1, 1)); // 初始比例 1:1
            tg.Children.Add(new TranslateTransform(finalX, finalY));
            ctx.SelectionPreview.RenderTransform = tg;

            // 设置裁剪 (防止渲染溢出)
            ctx.SelectionPreview.Clip = new RectangleGeometry(new Rect(0, 0, shapeBitmap.PixelWidth, shapeBitmap.PixelHeight));

            selectTool.RefreshOverlay(ctx);

            // 进入操控模式
            _isManipulating = true; selectTool.UpdateStatusBarSelectionSize();
        }
    }

    public override void OnKeyDown(ToolContext ctx, KeyEventArgs e)
    {
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            // 只有在“操控模式”（形状刚画完，处于浮动选中状态）下才生效
            if (_isManipulating)
            {
                var st = GetSelectTool();
                if (st != null && st.HasActiveSelection)
                {
                    st.Cleanup(ctx);
                    ctx.Undo.Redo(); 
                    _isManipulating = false;
                    ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                     e.Handled = true;
                    return;
                }
            }
        }
        if (_isManipulating)
        {
            var st = GetSelectTool();
            if (st != null)
            {
                st.OnKeyDown(ctx, e);
                if (!st.HasActiveSelection)
                {
                    _isManipulating = false;
                }
                return;
            }
        }
        base.OnKeyDown(ctx, e);
    }

    private double Gethandlength(Point start, Point end)
    {
        return Math.Pow(Math.Pow(start.X - end.X, 2) + Math.Pow(start.Y - end.Y, 2),0.5)*0.2;
    }
    private void UpdatePreviewShape(Point start, Point end, double thickness)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double w = Math.Abs(end.X - start.X);
        double h = Math.Abs(end.Y - start.Y);

        if (_currentShapeType == ShapeType.Line)
        {
            var line = (System.Windows.Shapes.Line)_previewShape;
            line.X1 = start.X; line.Y1 = start.Y;
            line.X2 = end.X; line.Y2 = end.Y;
        }
        else if (_currentShapeType == ShapeType.Arrow)
        {
            var path = (System.Windows.Shapes.Path)_previewShape;
            path.Data = BuildArrowGeometry(start, end, Gethandlength(start,end));
        }
        else
        {
            Canvas.SetLeft(_previewShape, x);
            Canvas.SetTop(_previewShape, y);
            _previewShape.Width = w;
            _previewShape.Height = h;
        }
    }
    private Geometry BuildArrowGeometry(Point start, Point end, double headLength)
    {
        Vector vec = end - start;
        // 防止长度为0崩溃
        if (vec.LengthSquared < 0.001) vec = new Vector(0, 1);

        vec.Normalize();
        if (Double.IsNaN(vec.X)) vec = new Vector(1, 0);

        Vector backVec = -vec;

        double angle = 35;
        Matrix m1 = Matrix.Identity; m1.Rotate(angle);
        Matrix m2 = Matrix.Identity; m2.Rotate(-angle);

        // 计算翅膀向量
        Vector wing1 = m1.Transform(backVec) * headLength;
        Vector wing2 = m2.Transform(backVec) * headLength;

        // 翅膀的端点
        Point p1 = end + wing1;
        Point p2 = end + wing2;


        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            // 画线身
            ctx.BeginFigure(start, false, false);
            ctx.LineTo(end, true, false); // 线身连到尖端

            // 画箭头翅膀 (这里采用开放式箭头，如果想要闭合三角形，需要改逻辑)
            ctx.LineTo(p1, true, false); // 连到一侧翅膀

            ctx.BeginFigure(end, false, false); // 回到尖端
            ctx.LineTo(p2, true, false); // 连到另一侧翅膀
        }
        geometry.Freeze();
        return geometry;
    }

    private static Int32Rect MakeRect(Point p1, Point p2)
    {
        int x = (int)Math.Min(p1.X, p2.X);
        int y = (int)Math.Min(p1.Y, p2.Y);
        int w = Math.Abs((int)p1.X - (int)p2.X);
        int h = Math.Abs((int)p1.Y - (int)p2.Y);
        return new Int32Rect(x, y, w, h);
    }
}
