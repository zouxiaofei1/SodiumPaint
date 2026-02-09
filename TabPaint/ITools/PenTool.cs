using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TabPaint;
using static TabPaint.MainWindow;

public partial class PenTool : ToolBase
{
    public override string Name => "Pen";

    // --- 修改 1：优化光标对象定义 ---
    private System.Windows.Shapes.Path _brushCursor;
    private TranslateTransform _cursorTransform; // 用于高性能移动
    private EllipseGeometry _circleGeometry;     // 缓存圆形
    private RectangleGeometry _squareGeometry;   // 缓存方形
    // 缓存的画刷，减少GC压力
    private SolidColorBrush _cachedFillBrush;
    private Color _lastColor;
    private double _lastOpacity = -1;

    private bool _drawing = false;
    private Point _lastPixel;
    private float _lastPressure = 1.0f;
    private byte[] _currentStrokeMask;
    private int _maskWidth;
    private int _maskHeight;
    private WriteableBitmap _maskBitmap; // 专门用于存储 mask 数据的位图
    private Image _maskImageOverlay;     // 用于显示红色遮罩的控件
    private static List<Point[]> _sprayPatterns;
    private static int _patternIndex = 0;
    private static Random _rnd = new Random();

    public override void Cleanup(ToolContext ctx)
    {
        base.Cleanup(ctx); CleanupMask(ctx);
        _drawing = false;
        StopDrawing(ctx);

        // 清理自定义光标
        if (_brushCursor != null && ctx.EditorOverlay.Children.Contains(_brushCursor))
        {
            ctx.EditorOverlay.Children.Remove(_brushCursor);
            _brushCursor = null;
            _cursorTransform = null;
            _circleGeometry = null;
            _squareGeometry = null;
        }

        // 恢复系统光标
        if (ctx.ViewElement != null)
        {
            ctx.ViewElement.Cursor = System.Windows.Input.Cursors.Arrow;
        }
        System.Windows.Input.Mouse.OverrideCursor = null;
    }
    public override void OnMouseLeave(ToolContext ctx)
    {
        if (_brushCursor != null)
        {
            _brushCursor.Visibility = Visibility.Collapsed;
        }
    }
    public override void SetCursor(ToolContext ctx)
    {
        if ((MainWindow.GetCurrentInstance()).IsViewMode) return;
        if (ctx.EditorOverlay != null)
        {
            ctx.EditorOverlay.ClipToBounds = false;
        }

        if (_brushCursor == null)
        {
            _brushCursor = new System.Windows.Shapes.Path
            {
                IsHitTestVisible = false,
                SnapsToDevicePixels = false,
                UseLayoutRounding = false
            };
            _cursorTransform = new TranslateTransform();
            _brushCursor.RenderTransform = _cursorTransform;
            _circleGeometry = new EllipseGeometry();
            _squareGeometry = new RectangleGeometry();
            Panel.SetZIndex(_brushCursor, AppConsts.PenCursorZIndex);
        }

        // 2. 将光标添加到图层（如果尚未添加）
        if (!ctx.EditorOverlay.Children.Contains(_brushCursor))
        {
            ctx.EditorOverlay.Children.Add(_brushCursor);
        }

        Point currentPos = System.Windows.Input.Mouse.GetPosition(ctx.ViewElement);
        UpdateCursorVisual(ctx, currentPos);
    }


    private void UpdateCursorVisual(ToolContext ctx, Point viewPos)
    {
        if (_brushCursor == null || _cursorTransform == null) return;

        double size = ctx.PenThickness;
        const double MinCustomCursorSize = 4.0;

        if (ctx.PenStyle == BrushStyle.Pencil || size < MinCustomCursorSize)
        {
            _brushCursor.Visibility = Visibility.Collapsed;
            if (ctx.ViewElement != null && ctx.ViewElement.Cursor != System.Windows.Input.Cursors.Cross)
            {
                ctx.ViewElement.Cursor = System.Windows.Input.Cursors.Cross;
            }
            return; 
        }
        else
        {
            if (_brushCursor.Visibility != Visibility.Visible)
            {
                _brushCursor.Visibility = Visibility.Visible;
            }

            if (ctx.ViewElement != null && ctx.ViewElement.Cursor != System.Windows.Input.Cursors.None)
            {
                ctx.ViewElement.Cursor = System.Windows.Input.Cursors.None;
            }
        }

        double halfSize = size / 2.0;

        _cursorTransform.X = viewPos.X - halfSize;
        _cursorTransform.Y = viewPos.Y - halfSize;

        bool isSquare = ctx.PenStyle == BrushStyle.Square ||
                        ctx.PenStyle == BrushStyle.Eraser ||
                        ctx.PenStyle == BrushStyle.Mosaic;

        if (isSquare)
        {
            // 更新方形尺寸
            if (_brushCursor.Data != _squareGeometry) _brushCursor.Data = _squareGeometry;

            if (_squareGeometry.Rect.Width != size)
            {
                _squareGeometry.Rect = new Rect(0, 0, size, size);
            }
        }
        else
        {
            // 更新圆形尺寸
            if (_brushCursor.Data != _circleGeometry) _brushCursor.Data = _circleGeometry;

            // EllipseGeometry 的 Center 设置为半径位置，Radius 设置为半径
            if (_circleGeometry.RadiusX != halfSize)
            {
                _circleGeometry.Center = new Point(halfSize, halfSize);
                _circleGeometry.RadiusX = halfSize;
                _circleGeometry.RadiusY = halfSize;
            }
        }
        if (ctx.PenStyle == BrushStyle.Highlighter)
        {
            _brushCursor.Fill = new SolidColorBrush(Color.FromArgb(AppConsts.HighlighterAlpha, 255, 255, 0));
            _brushCursor.Stroke = Brushes.Yellow;
            _brushCursor.StrokeThickness = AppConsts.PenDefaultStrokeThickness;
        }else 
        if (ctx.PenStyle == BrushStyle.AiEraser)
        {
            _brushCursor.Fill = new SolidColorBrush(Color.FromArgb(AppConsts.AiEraserCursorAlpha, 255, 0, 0));
            _brushCursor.Stroke = Brushes.Red;
            _brushCursor.StrokeThickness = AppConsts.PenDefaultStrokeThickness;
        }
        // 3. 颜色更新 (保持原有的缓存逻辑)
        else if(ctx.PenStyle == BrushStyle.Eraser)
        {
            _brushCursor.Fill = Brushes.Transparent;
            _brushCursor.Stroke = Brushes.Black;
            _brushCursor.StrokeThickness = AppConsts.PenDefaultStrokeThickness;
        }
            else if (ctx.PenStyle == BrushStyle.GaussianBlur)
            {
                // 青色半透明，代表水滴/模糊
                _brushCursor.Fill = new SolidColorBrush(Color.FromArgb(AppConsts.GaussianBlurCursorAlpha, 0, 255, 255));
                _brushCursor.Stroke = Brushes.Cyan;
                _brushCursor.StrokeThickness = AppConsts.PenDefaultStrokeThickness;
            }

            else
            {
            var appSettings = TabPaint.SettingsManager.Instance.Current;
            double globalOpacity = appSettings.PenOpacity;
            Color penColor = ctx.PenColor;

            if (_cachedFillBrush == null || _lastColor != penColor || Math.Abs(_lastOpacity - globalOpacity) > 0.001)
            {
                byte a = (byte)(penColor.A * globalOpacity);
                Color displayColor = Color.FromArgb(a, penColor.R, penColor.G, penColor.B);

                _cachedFillBrush = new SolidColorBrush(displayColor);
                if (_cachedFillBrush.CanFreeze) _cachedFillBrush.Freeze();

                _lastColor = penColor;
                _lastOpacity = globalOpacity;
            }

            _brushCursor.Fill = _cachedFillBrush;

            if (globalOpacity < AppConsts.PenLowOpacityThreshold)
            {
                _brushCursor.Stroke = new SolidColorBrush(Color.FromArgb(AppConsts.PenLowOpacityStrokeAlpha, 128, 128, 128));
                _brushCursor.StrokeThickness = AppConsts.PenLowOpacityStrokeThickness;
            }
            else
            {
                _brushCursor.Stroke = null;
            }
        }
    }
    public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if ((MainWindow.GetCurrentInstance()).IsViewMode) return;
        if ((MainWindow.GetCurrentInstance())._router.CurrentTool != (MainWindow.GetCurrentInstance())._tools.Pen) return;

        UpdateCursorVisual(ctx, viewPos); // 更新光标
        if (ctx.PenStyle == BrushStyle.AiEraser)
        {
            // 确保遮罩层存在
            if (_maskImageOverlay == null)
            {
                _maskImageOverlay = new Image
                {
                    IsHitTestVisible = false,
                    Opacity = AppConsts.AiEraserMaskOpacity // 半透明显示
                };
                // 插入到 EditorOverlay 中，但在 Cursor 之下
                ctx.EditorOverlay.Children.Insert(0, _maskImageOverlay);
            }

            // 如果 Bitmap 大小变了或为空，重新创建
            int w = ctx.Surface.Bitmap.PixelWidth;
            int h = ctx.Surface.Bitmap.PixelHeight;
            if (_maskBitmap == null || _maskBitmap.PixelWidth != w || _maskBitmap.PixelHeight != h)
            {
                _maskBitmap = new WriteableBitmap(w, h, AppConsts.StandardDpi, AppConsts.StandardDpi, PixelFormats.Bgra32, null);
                _maskImageOverlay.Source = _maskBitmap;// 确保 Image 控件填满画布
                Canvas.SetLeft(_maskImageOverlay, 0);
                Canvas.SetTop(_maskImageOverlay, 0);
                _maskImageOverlay.Width = ctx.ViewElement.ActualWidth;
                _maskImageOverlay.Height = ctx.ViewElement.ActualHeight;
            }
            ctx.CapturePointer();
            _drawing = true;
            _lastPixel = ctx.ToPixel(viewPos);
            _lastPressure = pressure;
            DrawMaskLine(ctx, _lastPixel, _lastPixel, pressure); // 绘制第一笔到 Mask 上
            return; 
        }


        if (ctx.PenStyle == BrushStyle.Calligraphy) pressure = 1.0f;
        int totalPixels = ctx.Surface.Width * ctx.Surface.Height;
        if (_currentStrokeMask == null || _currentStrokeMask.Length != totalPixels || _maskWidth != ctx.Surface.Width)
        {
            _currentStrokeMask = new byte[totalPixels];
            _maskWidth = ctx.Surface.Width;
            _maskHeight = ctx.Surface.Height;
        }
        else
        {
            Array.Clear(_currentStrokeMask, 0, _currentStrokeMask.Length);
        }

        ctx.CapturePointer();
        var px = ctx.ToPixel(viewPos);
        ctx.Undo.BeginStroke();
        _drawing = true;
        _lastPixel = px;
        _lastPressure = pressure;
        Int32Rect? dirty = null;
        ctx.Surface.Bitmap.Lock();
        unsafe
        {
            byte* backBuffer = (byte*)ctx.Surface.Bitmap.BackBuffer;
            int stride = ctx.Surface.Bitmap.BackBufferStride;
            int width = ctx.Surface.Bitmap.PixelWidth;
            int height = ctx.Surface.Bitmap.PixelHeight;

            if (IsLineBasedBrush(ctx.PenStyle)) dirty = DrawBrushLineUnsafe(ctx, px, pressure, px, pressure, backBuffer, stride, width, height);
            else  dirty = DrawBrushAtUnsafe(ctx, px, backBuffer, stride, width, height);
        }
        if (dirty.HasValue)
        {
            var finalRect = ClampRect(dirty.Value, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
            if (finalRect.Width > 0 && finalRect.Height > 0)
            {
                ctx.Surface.Bitmap.AddDirtyRect(finalRect);
                ctx.Undo.AddDirtyRect(finalRect);
            }
        }
        ctx.Surface.Bitmap.Unlock();
    }

    public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if ((MainWindow.GetCurrentInstance()).IsViewMode)
        {
            if (_brushCursor != null)
            {
                _brushCursor.Visibility = Visibility.Collapsed;
            }
            return;
        }

        UpdateCursorVisual(ctx, viewPos);
        
        if (!_drawing) return;
        var px = ctx.ToPixel(viewPos);

        if (ctx.PenStyle == BrushStyle.AiEraser)
        {
            DrawMaskLine(ctx, _lastPixel, px, pressure);
            _lastPixel = px;
            _lastPressure = pressure;
            return;
        }


        if (ctx.PenStyle == BrushStyle.Calligraphy)
        {
            double distance = Math.Sqrt(Math.Pow(px.X - _lastPixel.X, 2) + Math.Pow(px.Y - _lastPixel.Y, 2));
            double maxSpeed = AppConsts.CalligraphyMaxSpeed;
            float speedPressure = (float)Math.Clamp(1.0 - (distance / maxSpeed), AppConsts.CalligraphyMinPressure, 1.0);
            pressure = speedPressure;
        }
       ctx.Surface.Bitmap.Lock();
        try
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            bool hasUpdate = false;

            unsafe
            {
                byte* backBuffer = (byte*)ctx.Surface.Bitmap.BackBuffer;
                int stride = ctx.Surface.Bitmap.BackBufferStride;
                int width = ctx.Surface.Bitmap.PixelWidth;
                int height = ctx.Surface.Bitmap.PixelHeight;

                var dirty = DrawContinuousStrokeUnsafe(ctx, _lastPixel, _lastPressure, px, pressure, backBuffer, stride, width, height);

                if (dirty.HasValue)
                {
                    hasUpdate = true;
                    minX = dirty.Value.X;
                    minY = dirty.Value.Y;
                    maxX = dirty.Value.X + dirty.Value.Width;
                    maxY = dirty.Value.Y + dirty.Value.Height;
                }
            }

            if (hasUpdate && maxX >= minX && maxY >= minY)
            {
                var finalRect = ClampRect(new Int32Rect(minX, minY, maxX - minX, maxY - minY), ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                if (finalRect.Width > 0 && finalRect.Height > 0)
                {
                    ctx.Surface.Bitmap.AddDirtyRect(finalRect);
                    ctx.Undo.AddDirtyRect(finalRect);
                }
            }
        }
        finally
        {
            ctx.Surface.Bitmap.Unlock();
        }

        _lastPixel = px;
        _lastPressure = pressure; 
    }

    private bool IsLineBasedBrush(BrushStyle style)
    {
        return style == BrushStyle.Round ||
               style == BrushStyle.Pencil ||
               style == BrushStyle.Highlighter ||
               style == BrushStyle.Watercolor ||
               style == BrushStyle.Crayon ||
               style == BrushStyle.Calligraphy  ||style == BrushStyle.Brush ||
           style == BrushStyle.Mosaic;  
    }

    public override void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        StopDrawing(ctx);
    }

    public override void StopAction(ToolContext ctx)
    {
        StopDrawing(ctx);
    }

    public void StopDrawing(ToolContext ctx)
    {
        if (!_drawing) return;
        _drawing = false;
        ctx.Undo.CommitStroke();
        ctx.IsDirty = true;
        ctx.ReleasePointerCapture();

        if (ctx.PenStyle == BrushStyle.AiEraser)
        {
            ApplyAiEraser(ctx);
            return;
        }
    }
    private static byte ClampColor(int value)
    {
        if (value < 0) return 0;
        if (value > 255) return 255;
        return (byte)value;
    }

    private static void InitializeSprayPatterns()
    {
        if (_sprayPatterns != null) return;
        _sprayPatterns = new List<Point[]>();
        for (int i = 0; i < 5; i++)
            _sprayPatterns.Add(GenerateSprayPattern(200));
    }

    private static Point[] GenerateSprayPattern(int count)
    {
        Random r = new Random();
        Point[] pts = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double a = r.NextDouble() * 2 * Math.PI;
            double d = Math.Sqrt(r.NextDouble());
            pts[i] = new Point(d * Math.Cos(a), d * Math.Sin(a));
        }
        return pts;
    }

    private static Int32Rect ClampRect(Int32Rect rect, int maxWidth, int maxHeight)
    {
        int left = Math.Max(0, rect.X);
        int top = Math.Max(0, rect.Y);
        int right = Math.Min(maxWidth, rect.X + rect.Width);
        int bottom = Math.Min(maxHeight, rect.Y + rect.Height);
        int width = Math.Max(0, right - left);
        int height = Math.Max(0, bottom - top);
        return new Int32Rect(left, top, width, height);
    }
    private static Int32Rect LineBounds(Point p1, Point p2, int penRadius)
    {
        int expand = penRadius + 2;
        int x = (int)Math.Min(p1.X, p2.X) - expand;
        int y = (int)Math.Min(p1.Y, p2.Y) - expand;
        int w = (int)Math.Abs(p1.X - p2.X) + expand * 2;
        int h = (int)Math.Abs(p1.Y - p2.Y) + expand * 2;
        return ClampRect(new Int32Rect(x, y, w, h),
            (MainWindow.GetCurrentInstance())._ctx.Bitmap.PixelWidth,
            (MainWindow.GetCurrentInstance())._ctx.Bitmap.PixelHeight);
    }
}
