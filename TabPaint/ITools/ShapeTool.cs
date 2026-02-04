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

    // 1. 更新枚举，添加新形状
    public enum ShapeType { Rectangle, Ellipse, Line, RoundedRectangle, Arrow, Triangle, Diamond, Pentagon, Star, Bubble }

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

    public void GiveUpSelection(ToolContext ctx)
    {
        if (ctx == null) return;
        GetSelectTool()?.CommitSelection(ctx, true);
        GetSelectTool()?.Cleanup(ctx);
    }

    public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if (((MainWindow)System.Windows.Application.Current.MainWindow).IsViewMode) return;
        var selectTool = GetSelectTool();
        var px = ctx.ToPixel(viewPos);

        // ... (保持原有的操控模式判断逻辑) ...
        if (_isManipulating && selectTool != null)
        {
            if (!selectTool.HasActiveSelection)
            {
                _isManipulating = false;
                goto DrawingLogic;
            }
            bool hitHandle = selectTool.HitTestHandle(px, selectTool._selectionRect) != SelectTool.ResizeAnchor.None;
            bool hitContent = selectTool.IsPointInSelection(px);

            if (hitHandle || hitContent)
            {
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
        if (lag > 0) { lag--; return; }

        _startPoint = ctx.ToPixel(viewPos);
        _isDrawing = true;
        ctx.CapturePointer();

        // 2. 初始化预览形状
        switch (_currentShapeType)
        {
            case ShapeType.Rectangle:
                _previewShape = new System.Windows.Shapes.Rectangle();
                break;
            case ShapeType.RoundedRectangle:
                _previewShape = new System.Windows.Shapes.Rectangle { RadiusX = AppConsts.ShapeToolRoundedRectRadius, RadiusY = AppConsts.ShapeToolRoundedRectRadius };
                break;
            case ShapeType.Ellipse:
                _previewShape = new System.Windows.Shapes.Ellipse();
                break;
            case ShapeType.Line:
                _previewShape = new System.Windows.Shapes.Line();
                break;
            // 所有复杂形状都使用 Path
            case ShapeType.Arrow:
            case ShapeType.Triangle:
            case ShapeType.Diamond:
            case ShapeType.Pentagon:
            case ShapeType.Star:
            case ShapeType.Bubble:
                _previewShape = new System.Windows.Shapes.Path();
                break;
        }

        _previewShape.Stroke = new SolidColorBrush(ctx.PenColor);
        _previewShape.StrokeThickness = ctx.PenThickness;
        _previewShape.Fill = null;

        // Line, Arrow 以及新的复杂形状是基于 Path Data 的，不需要设置 Canvas Left/Top，或者在 Update 时处理
        if (_currentShapeType == ShapeType.Rectangle ||
            _currentShapeType == ShapeType.RoundedRectangle ||
            _currentShapeType == ShapeType.Ellipse)
        {
            Canvas.SetLeft(_previewShape, _startPoint.X);
            Canvas.SetTop(_previewShape, _startPoint.Y);
        }

        ctx.EditorOverlay.Children.Add(_previewShape);
    }

    public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if (_isManipulating) { GetSelectTool()?.OnPointerMove(ctx, viewPos); return; }
        if (!_isDrawing || _previewShape == null) return;

        var current = ctx.ToPixel(viewPos);
        UpdatePreviewShape(_startPoint, current, ctx.PenThickness);

        int w = (int)Math.Abs(current.X - _startPoint.X);
        int h = (int)Math.Abs(current.Y - _startPoint.Y);
        var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
        mw.SelectionSize = string.Format(LocalizationManager.GetString("L_Selection_Size_Format"), w, h);
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

        // 移除正在绘制的预览线框
        if (_previewShape != null)
        {
            ctx.EditorOverlay.Children.Remove(_previewShape);
            _previewShape = null;
        }

        // 计算图形区域
        var rawRect = MakeRect(_startPoint, endPoint);
        if (rawRect.Width <= 1 || rawRect.Height <= 1) return;

        // 计算 Padding (箭头需要额外空间)
        double arrowScale = 0;
        if (_currentShapeType == ShapeType.Arrow)
        {
            arrowScale = Gethandlength(_startPoint, endPoint);
        }
        double padding = ctx.PenThickness / 2.0 + 2 + arrowScale;

        // 计算有效绘制区域
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

        // 生成位图
        var shapeBitmap = RenderShapeToBitmapClipped(_startPoint, endPoint, validBounds, ctx.PenColor, ctx.PenThickness, ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY);

        var selectTool = GetSelectTool();
        if (selectTool != null && shapeBitmap != null)
        {
            bool isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            // 1. 先将生成的图片加载到 SelectTool 中 (这是通用步骤)
            selectTool.InsertImageAsSelection(ctx, shapeBitmap, false);

            int finalX = (int)validBounds.X;
            int finalY = (int)validBounds.Y;
            selectTool._selectionRect = new Int32Rect(finalX, finalY, shapeBitmap.PixelWidth, shapeBitmap.PixelHeight);
            selectTool._originalRect = selectTool._selectionRect;

            if (isCtrlPressed)
            {
                double uiScaleX = ctx.ViewElement.ActualWidth / ctx.Surface.Bitmap.PixelWidth;
                double uiScaleY = ctx.ViewElement.ActualHeight / ctx.Surface.Bitmap.PixelHeight;
                ctx.SelectionPreview.Width = selectTool._selectionRect.Width * uiScaleX;  // 设置预览层的大小
                ctx.SelectionPreview.Height = selectTool._selectionRect.Height * uiScaleY;
                var tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(1, 1)); // 初始比例 1:1
                tg.Children.Add(new TranslateTransform(finalX, finalY));
                ctx.SelectionPreview.RenderTransform = tg;
                ctx.SelectionPreview.Clip = new RectangleGeometry(new Rect(0, 0, shapeBitmap.PixelWidth, shapeBitmap.PixelHeight));
                selectTool.RefreshOverlay(ctx);
                selectTool.UpdateStatusBarSelectionSize();
                _isManipulating = true;
            }
            else
            {
                selectTool.CommitSelection(ctx, true);
                selectTool.Cleanup(ctx);
                _isManipulating = false;
                selectTool.lag = 0;
            }
        }
    }

    public override void OnKeyDown(ToolContext ctx, KeyEventArgs e)
    {
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
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
                if (!st.HasActiveSelection) _isManipulating = false;
                return;
            }
        }
        base.OnKeyDown(ctx, e);
    }
    private BitmapSource RenderShapeToBitmapClipped(Point globalStart, Point globalEnd, Rect validBounds, Color color, double thickness, double dpiX, double dpiY)
    {
        int pixelWidth = (int)Math.Ceiling(validBounds.Width);
        int pixelHeight = (int)Math.Ceiling(validBounds.Height);
        if (pixelWidth <= 0 || pixelHeight <= 0) return null;

        DrawingVisual drawingVisual = new DrawingVisual();
        using (DrawingContext dc = drawingVisual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(-validBounds.X, -validBounds.Y));
            Pen pen = new Pen(new SolidColorBrush(color), thickness);

            if (_currentShapeType == ShapeType.Rectangle || _currentShapeType == ShapeType.Diamond || _currentShapeType == ShapeType.Triangle)
            {
                pen.LineJoin = PenLineJoin.Miter;
                pen.StartLineCap = PenLineCap.Square;
                pen.EndLineCap = PenLineCap.Square;
            }
            else
            {
                pen.LineJoin = PenLineJoin.Round; pen.StartLineCap = PenLineCap.Round; pen.EndLineCap = PenLineCap.Round;
            }
            pen.Freeze();

            Rect logicalRect = new Rect(
                Math.Min(globalStart.X, globalEnd.X),
                Math.Min(globalStart.Y, globalEnd.Y),
                Math.Abs(globalStart.X - globalEnd.X),
                Math.Abs(globalStart.Y - globalEnd.Y)
            );

            switch (_currentShapeType)
            {
                case ShapeType.Rectangle:
                    dc.DrawRectangle(null, pen, logicalRect);
                    break;
                case ShapeType.RoundedRectangle:
                    dc.DrawRoundedRectangle(null, pen, logicalRect, AppConsts.ShapeToolRoundedRectRadius, AppConsts.ShapeToolRoundedRectRadius);
                    break;
                case ShapeType.Ellipse:
                    dc.DrawEllipse(null, pen,
                        new Point(logicalRect.X + logicalRect.Width / 2.0, logicalRect.Y + logicalRect.Height / 2.0),
                        logicalRect.Width / 2.0, logicalRect.Height / 2.0);
                    break;
                case ShapeType.Line:
                    dc.DrawLine(pen, globalStart, globalEnd);
                    break;
                case ShapeType.Arrow:
                    dc.DrawGeometry(null, pen, BuildArrowGeometry(globalStart, globalEnd, Gethandlength(globalStart, globalEnd)));
                    break;

                // 新增形状渲染
                case ShapeType.Triangle:
                    dc.DrawGeometry(null, pen, BuildRegularPolygon(logicalRect, 3, -Math.PI / 2));
                    break;
                case ShapeType.Diamond:
                    dc.DrawGeometry(null, pen, BuildRegularPolygon(logicalRect, 4, 0)); // 菱形即旋转的矩形
                    break;
                case ShapeType.Pentagon:
                    dc.DrawGeometry(null, pen, BuildRegularPolygon(logicalRect, 5, -Math.PI / 2));
                    break;
                case ShapeType.Star:
                    dc.DrawGeometry(null, pen, BuildStarGeometry(logicalRect));
                    break;
                case ShapeType.Bubble:
                    dc.DrawGeometry(null, pen, BuildBubbleGeometry(logicalRect));
                    break;
            }
            dc.Pop();
        }

        RenderTargetBitmap bmp = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        bmp.Render(drawingVisual);
        bmp.Freeze();
        return bmp;
    }

    // 4. 预览更新
    private void UpdatePreviewShape(Point start, Point end, double thickness)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double w = Math.Abs(end.X - start.X);
        double h = Math.Abs(end.Y - start.Y);

        if (_currentShapeType == ShapeType.Line)
        {
            var line = (System.Windows.Shapes.Line)_previewShape;
            line.X1 = start.X; line.Y1 = start.Y; line.X2 = end.X; line.Y2 = end.Y;
        }
        else
        {
            // 对于 Path 类型的形状，我们需要重新生成 Data
            if (_previewShape is System.Windows.Shapes.Path path)
            {
                Rect r = new Rect(x, y, w, h);
                switch (_currentShapeType)
                {
                    case ShapeType.Arrow:
                        path.Data = BuildArrowGeometry(start, end, Gethandlength(start, end));
                        break;
                    case ShapeType.Triangle:
                        path.Data = BuildRegularPolygon(r, 3, -Math.PI / 2);
                        break;
                    case ShapeType.Diamond:
                        path.Data = BuildRegularPolygon(r, 4, 0);
                        break;
                    case ShapeType.Pentagon:
                        path.Data = BuildRegularPolygon(r, 5, -Math.PI / 2);
                        break;
                    case ShapeType.Star:
                        path.Data = BuildStarGeometry(r);
                        break;
                    case ShapeType.Bubble:
                        path.Data = BuildBubbleGeometry(r);
                        break;
                }
            }
            else
            {
                // 基础形状 (Rect, Ellipse)
                Canvas.SetLeft(_previewShape, x);
                Canvas.SetTop(_previewShape, y);
                _previewShape.Width = w;
                _previewShape.Height = h;
            }
        }
    }
    private double Gethandlength(Point start, Point end)
    {
        return Math.Pow(Math.Pow(start.X - end.X, 2) + Math.Pow(start.Y - end.Y, 2), 0.5) * 0.2;
    }
    private Geometry BuildArrowGeometry(Point start, Point end, double headLength)
    {
        Vector vec = end - start;
        if (vec.LengthSquared < 0.001) vec = new Vector(0, 1);
        vec.Normalize();
        if (Double.IsNaN(vec.X)) vec = new Vector(1, 0);

        Vector backVec = -vec;
        double angle = 35;
        Matrix m1 = Matrix.Identity; m1.Rotate(angle);
        Matrix m2 = Matrix.Identity; m2.Rotate(-angle);

        Vector wing1 = m1.Transform(backVec) * headLength;
        Vector wing2 = m2.Transform(backVec) * headLength;
        Point p1 = end + wing1;
        Point p2 = end + wing2;

        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.LineTo(end, true, false);
            ctx.LineTo(p1, true, false);
            ctx.BeginFigure(end, false, false);
            ctx.LineTo(p2, true, false);
        }
        geometry.Freeze();
        return geometry;
    }
    private Geometry BuildRegularPolygon(Rect rect, int sides, double startAngle) // 构造正多边形 
    {
        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            double centerX = rect.X + rect.Width / 2.0;
            double centerY = rect.Y + rect.Height / 2.0;
            double radiusX = rect.Width / 2.0;
            double radiusY = rect.Height / 2.0;

            for (int i = 0; i < sides; i++)
            {
                double angle = startAngle + 2 * Math.PI * i / sides;
                double x = centerX + radiusX * Math.Cos(angle);
                double y = centerY + radiusY * Math.Sin(angle);
                Point pt = new Point(x, y);

                if (i == 0) ctx.BeginFigure(pt, true, true);
                else ctx.LineTo(pt, true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }
    private Geometry BuildStarGeometry(Rect rect) // 构造五角星
    {
        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            double centerX = rect.X + rect.Width / 2.0;
            double centerY = rect.Y + rect.Height / 2.0;
            double radiusX = rect.Width / 2.0;
            double radiusY = rect.Height / 2.0;
            double innerRadiusRatio = AppConsts.ShapeToolStarInnerRadiusRatio; // 内圆比例

            int numPoints = 5;
            double angleStep = Math.PI / numPoints;
            double currentAngle = -Math.PI / 2; // 顶点朝上

            for (int i = 0; i < numPoints * 2; i++)
            {
                double rX = (i % 2 == 0) ? radiusX : radiusX * innerRadiusRatio;
                double rY = (i % 2 == 0) ? radiusY : radiusY * innerRadiusRatio;

                double x = centerX + rX * Math.Cos(currentAngle);
                double y = centerY + rY * Math.Sin(currentAngle);
                Point pt = new Point(x, y);

                if (i == 0) ctx.BeginFigure(pt, true, true);
                else ctx.LineTo(pt, true, false);

                currentAngle += angleStep;
            }
        }
        geometry.Freeze();
        return geometry;
    }
    private Geometry BuildBubbleGeometry(Rect rect)   // 构造对话气泡
    {
        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            double radius = Math.Min(rect.Width, rect.Height) * AppConsts.ShapeToolBubbleRadiusRatio;
            double tailHeight = rect.Height * AppConsts.ShapeToolBubbleTailHeightRatio;
            double bodyHeight = rect.Height - tailHeight;
            Rect bodyRect = new Rect(rect.X, rect.Y, rect.Width, bodyHeight);
            Point tailStart = new Point(rect.X + rect.Width * AppConsts.ShapeToolBubbleTailStartRatio, rect.Y + bodyHeight);
            Point tailTip = new Point(rect.X + rect.Width * AppConsts.ShapeToolBubbleTailStartRatio, rect.Y + rect.Height);
            Point tailEnd = new Point(rect.X + rect.Width * AppConsts.ShapeToolBubbleTailEndRatio, rect.Y + bodyHeight);
            ctx.BeginFigure(new Point(bodyRect.X + radius, bodyRect.Y), true, true);
            ctx.LineTo(new Point(bodyRect.Right - radius, bodyRect.Top), true, true);
            ctx.ArcTo(new Point(bodyRect.Right, bodyRect.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
            ctx.LineTo(new Point(bodyRect.Right, bodyRect.Bottom - radius), true, true);
            ctx.ArcTo(new Point(bodyRect.Right - radius, bodyRect.Bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
            ctx.LineTo(tailStart, true, true);
            ctx.LineTo(tailTip, true, true);
            ctx.LineTo(tailEnd, true, true);

            ctx.LineTo(new Point(bodyRect.Left + radius, bodyRect.Bottom), true, true);
            ctx.ArcTo(new Point(bodyRect.Left, bodyRect.Bottom - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
            ctx.LineTo(new Point(bodyRect.Left, bodyRect.Top + radius), true, true);
            ctx.ArcTo(new Point(bodyRect.Left + radius, bodyRect.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
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
