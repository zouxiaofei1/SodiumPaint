using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
//
//画笔工具
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public class PenTool : ToolBase
        {
            public override string Name => "Pen";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Pen;

            private bool _drawing = false;
            private Point _lastPixel;

            // 荧光笔专用遮罩
            private bool[] _currentStrokeMask;
            private int _maskWidth;
            private int _maskHeight;

            // 喷枪缓存
            private static List<Point[]> _sprayPatterns;
            private static int _patternIndex = 0;
            private static Random _rnd = new Random();

            public override void Cleanup(ToolContext ctx)
            {
                base.Cleanup(ctx);
                _drawing = false;
                StopDrawing(ctx);
            }

            // 判断是否是“连续线段”类型的笔刷（不需要插值打点，而是直接画线）
            private bool IsLineBasedBrush(BrushStyle style)
            {
                return style == BrushStyle.Round ||
                       style == BrushStyle.Pencil ||
                       style == BrushStyle.Highlighter ||
                       style == BrushStyle.Watercolor ||
                       style == BrushStyle.Crayon;
            }

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                if (((MainWindow)System.Windows.Application.Current.MainWindow).IsViewMode) return;
                if (((MainWindow)System.Windows.Application.Current.MainWindow)._router.CurrentTool != ((MainWindow)System.Windows.Application.Current.MainWindow)._tools.Pen) return;

                // --- 荧光笔遮罩初始化 ---
                int totalPixels = ctx.Surface.Width * ctx.Surface.Height;
                if (_currentStrokeMask == null || _currentStrokeMask.Length != totalPixels || _maskWidth != ctx.Surface.Width)
                {
                    _currentStrokeMask = new bool[totalPixels];
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

                Int32Rect? dirty = null;

                // --- 修复：PointerDown 时根据笔刷类型分流 ---
                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* backBuffer = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;
                    int width = ctx.Surface.Bitmap.PixelWidth;
                    int height = ctx.Surface.Bitmap.PixelHeight;

                    if (IsLineBasedBrush(ctx.PenStyle))
                    {
                        // 线段型笔刷：原地画一条长度为0的线（即一个点）
                        dirty = DrawBrushLineUnsafe(ctx, px, px, backBuffer, stride, width, height);
                    }
                    else
                    {
                        // 印章型笔刷（方块、喷枪等）：直接盖一个章
                        dirty = DrawBrushAtUnsafe(ctx, px, backBuffer, stride, width, height);
                    }
                }
                if (dirty.HasValue)
                {
                    a.s("hasvalue");
                    var finalRect = ClampRect(dirty.Value, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                    if (finalRect.Width > 0 && finalRect.Height > 0)
                    {
                        ctx.Surface.Bitmap.AddDirtyRect(finalRect); // 更新屏幕
                        ctx.Undo.AddDirtyRect(finalRect);           // 修复：通知 Undo 系统
                    }
                }
                ctx.Surface.Bitmap.Unlock(); 
            }

            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                if (!_drawing) return;
                var px = ctx.ToPixel(viewPos);

                ctx.Surface.Bitmap.Lock();

                // 计算本次绘制的脏矩形范围
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                bool hasUpdate = false;

                unsafe
                {
                    byte* backBuffer = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;
                    int width = ctx.Surface.Bitmap.PixelWidth;
                    int height = ctx.Surface.Bitmap.PixelHeight;

                    // 传入指针进行极速绘制
                    var dirty = DrawContinuousStrokeUnsafe(ctx, _lastPixel, px, backBuffer, stride, width, height);

                    if (dirty.HasValue)
                    {
                        hasUpdate = true;
                        minX = dirty.Value.X;
                        minY = dirty.Value.Y;
                        maxX = dirty.Value.X + dirty.Value.Width;
                        maxY = dirty.Value.Y + dirty.Value.Height;
                    }
                }

                // 更新
                if (hasUpdate && maxX >= minX && maxY >= minY)
                {
                    var finalRect = ClampRect(new Int32Rect(minX, minY, maxX - minX, maxY - minY), ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                    if (finalRect.Width > 0 && finalRect.Height > 0)
                    {
                        ctx.Surface.Bitmap.AddDirtyRect(finalRect); // 1. 更新屏幕
                        ctx.Undo.AddDirtyRect(finalRect);           // 2. 修复：关键！必须通知 Undo 系统
                    }
                }

                ctx.Surface.Bitmap.Unlock();

                _lastPixel = px;
            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos)
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
            }

            // ---------------- 核心绘制逻辑 (Unsafe) ----------------

            private unsafe Int32Rect? DrawContinuousStrokeUnsafe(ToolContext ctx, Point from, Point to, byte* buffer, int stride, int w, int h)
            {
                // 1. 连续型笔刷：直接画线段，效率最高
                if (IsLineBasedBrush(ctx.PenStyle))
                {
                    return DrawBrushLineUnsafe(ctx, from, to, buffer, stride, w, h);
                }

                // 2. 间断型笔刷：需要插值“盖章”
                double dx = to.X - from.X;
                double dy = to.Y - from.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);

                if (length < 0.5) return DrawBrushAtUnsafe(ctx, to, buffer, stride, w, h);

                int steps = 1;
                switch (ctx.PenStyle)
                {
                    case BrushStyle.Square:
                    case BrushStyle.Eraser:
                        steps = (int)(length / (Math.Max(1, ctx.PenThickness / 2.0)));
                        break;
                    case BrushStyle.Brush:
                        steps = (int)(length / 2.0);
                        break;
                    case BrushStyle.Spray:
                        steps = (int)(length / (Math.Max(1, ctx.PenThickness)));
                        break;
                    case BrushStyle.Mosaic:
                        steps = (int)(length / 5.0);
                        break;
                }

                if (steps < 1) steps = 1;

                double xStep = dx / steps;
                double yStep = dy / steps;
                double x = from.X;
                double y = from.Y;

                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                bool hit = false;

                for (int i = 0; i <= steps; i++)
                {
                    var rect = DrawBrushAtUnsafe(ctx, new Point(x, y), buffer, stride, w, h);
                    if (rect.HasValue)
                    {
                        hit = true;
                        if (rect.Value.X < minX) minX = rect.Value.X;
                        if (rect.Value.Y < minY) minY = rect.Value.Y;
                        if (rect.Value.X + rect.Value.Width > maxX) maxX = rect.Value.X + rect.Value.Width;
                        if (rect.Value.Y + rect.Value.Height > maxY) maxY = rect.Value.Y + rect.Value.Height;
                    }
                    x += xStep;
                    y += yStep;
                }

                if (!hit) return null;
                return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
            }

            private unsafe Int32Rect? DrawBrushLineUnsafe(ToolContext ctx, Point p1, Point p2, byte* buffer, int stride, int w, int h)
            {
                switch (ctx.PenStyle)
                {
                    case BrushStyle.Round:
                        DrawRoundStrokeUnsafe(ctx, p1, p2, buffer, stride, w, h);
                        return LineBounds(p1, p2, (int)ctx.PenThickness);
                    case BrushStyle.Pencil:
                        DrawPencilLineUnsafe(ctx, p1, p2, buffer, stride, w, h);
                        return LineBounds(p1, p2, 1);
                    case BrushStyle.Highlighter:
                        DrawHighlighterLineUnsafe(ctx, p1, p2, buffer, stride, w, h);
                        return LineBounds(p1, p2, (int)ctx.PenThickness);
                    case BrushStyle.Watercolor:
                        // 简单起见，水彩和蜡笔在内部做局部插值
                        double dist = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                        int steps = (int)(dist / (ctx.PenThickness / 4));
                        if (steps == 0) steps = 1;
                        double sx = (p2.X - p1.X) / steps;
                        double sy = (p2.Y - p1.Y) / steps;
                        double cx = p1.X, cy = p1.Y;
                        for (int i = 0; i <= steps; i++)
                        {
                            DrawWatercolorStrokeUnsafe(ctx, new Point(cx, cy), buffer, stride, w, h);
                            cx += sx; cy += sy;
                        }
                        return LineBounds(p1, p2, (int)ctx.PenThickness + 5);
                    case BrushStyle.Crayon:
                        double dist2 = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                        int steps2 = (int)(dist2 / (ctx.PenThickness / 2));
                        if (steps2 == 0) steps2 = 1;
                        double sx2 = (p2.X - p1.X) / steps2;
                        double sy2 = (p2.Y - p1.Y) / steps2;
                        double cx2 = p1.X, cy2 = p1.Y;
                        for (int i = 0; i <= steps2; i++)
                        {
                            DrawOilPaintStrokeUnsafe(ctx, new Point(cx2, cy2), buffer, stride, w, h);
                            cx2 += sx2; cy2 += sy2;
                        }
                        return LineBounds(p1, p2, (int)ctx.PenThickness + 5);
                }
                return null;
            }

            private unsafe Int32Rect? DrawBrushAtUnsafe(ToolContext ctx, Point px, byte* buffer, int stride, int w, int h)
            {
                // 用于那些不支持线段绘制，只能打点的笔刷
                switch (ctx.PenStyle)
                {
                    case BrushStyle.Square:
                        DrawSquareStrokeUnsafe(ctx, px, buffer, stride, w, h, false);
                        return LineBounds(px, px, (int)ctx.PenThickness);
                    case BrushStyle.Eraser:
                        DrawSquareStrokeUnsafe(ctx, px, buffer, stride, w, h, true);
                        return LineBounds(px, px, (int)ctx.PenThickness);
                    case BrushStyle.Brush:
                        DrawBrushStrokeUnsafe(ctx, px, buffer, stride, w, h);
                        return LineBounds(px, px, 5);
                    case BrushStyle.Spray:
                        DrawSprayStrokeUnsafe(ctx, px, buffer, stride, w, h);
                        return LineBounds(px, px, (int)ctx.PenThickness * 2);
                    case BrushStyle.Mosaic:
                        DrawMosaicStrokeUnsafe(ctx, px, buffer, stride, w, h);
                        return LineBounds(px, px, (int)ctx.PenThickness + 5);
                }
                return null;
            }

            // ---------------- 具体笔刷实现 (Unsafe) ----------------

            private unsafe void DrawRoundStrokeUnsafe(ToolContext ctx, Point p1, Point p2, byte* basePtr, int stride, int w, int h)
            {
                int r = (int)(ctx.PenThickness / 2.0);
                if (r < 1) r = 1;

                Color targetColor = ctx.PenColor; // 目标颜色 (R,G,B,A)

                // 获取全局力度 (0.0 - 1.0)
                // 这里的 Opacity 不再直接乘到 Alpha 上，而是作为插值因子
                float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;

                // 如果力度为0，完全不画
                if (globalOpacity <= 0.005f) return;

                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double lenSq = dx * dx + dy * dy;

                int xmin = (int)Math.Min(p1.X, p2.X) - r;
                int ymin = (int)Math.Min(p1.Y, p2.Y) - r;
                int xmax = (int)Math.Max(p1.X, p2.X) + r;
                int ymax = (int)Math.Max(p1.Y, p2.Y) + r;

                xmin = Math.Max(0, xmin); ymin = Math.Max(0, ymin);
                xmax = Math.Min(w - 1, xmax); ymax = Math.Min(h - 1, ymax);

                int rSq = r * r;

                // 预计算目标分量，避免循环内重复转换
                float targetB = targetColor.B;
                float targetG = targetColor.G;
                float targetR = targetColor.R;
                float targetA = targetColor.A;

                for (int y = ymin; y <= ymax; y++)
                {
                    byte* rowPtr = basePtr + y * stride;
                    int rowIdx = y * w; // 用来计算Mask索引

                    for (int x = xmin; x <= xmax; x++)
                    {
                        // 1. 检查 Mask：防止同一次 Stroke 内重复叠加导致颜色过深
                        int pixelIndex = rowIdx + x;
                        if (_currentStrokeMask[pixelIndex]) continue;

                        // 2. 几何计算：点到线段的距离
                        double t = 0;
                        if (lenSq > 0)
                        {
                            t = ((x - p1.X) * dx + (y - p1.Y) * dy) / lenSq;
                            t = Math.Max(0, Math.Min(1, t));
                        }
                        double projx = p1.X + t * dx;
                        double projy = p1.Y + t * dy;
                        double distSq = (x - projx) * (x - projx) + (y - projy) * (y - projy);

                        if (distSq <= rSq)
                        {
                            // 标记该像素本次已绘制
                            _currentStrokeMask[pixelIndex] = true;

                            byte* p = rowPtr + x * 4;

                            // --- 核心修改：线性插值 (Lerp) 算法 ---

                            // 蓝色 B
                            // 公式：新值 = 旧值 + (目标值 - 旧值) * 力度
                            p[0] = (byte)(p[0] + (targetB - p[0]) * globalOpacity);

                            // 绿色 G
                            p[1] = (byte)(p[1] + (targetG - p[1]) * globalOpacity);

                            // 红色 R
                            p[2] = (byte)(p[2] + (targetR - p[2]) * globalOpacity);

                            // Alpha 通道 - 这里是实现“透明绘图”的关键
                            // 如果 targetA 是 0 (透明)，且 opacity 是 1，则 p[3] 变为 0
                            p[3] = (byte)(p[3] + (targetA - p[3]) * globalOpacity);
                        }
                    }
                }
            }



            // ---------------- 铅笔 (Bresenham + Lerp) ----------------
            private unsafe void DrawPencilLineUnsafe(ToolContext ctx, Point p1, Point p2, byte* basePtr, int stride, int w, int h)
            {
                int x0 = (int)p1.X; int y0 = (int)p1.Y;
                int x1 = (int)p2.X; int y1 = (int)p2.Y;

                Color targetColor = ctx.PenColor;
                float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;

                // 铅笔如果力度太小可能看不见，但为了统一逻辑还是允许微弱绘制
                if (globalOpacity <= 0.005f) return;

                float tB = targetColor.B;
                float tG = targetColor.G;
                float tR = targetColor.R;
                float tA = targetColor.A;

                int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
                int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
                int err = dx + dy, e2;

                while (true)
                {
                    if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h)
                    {
                        int pixelIndex = y0 * w + x0;
                        if (!_currentStrokeMask[pixelIndex])
                        {
                            _currentStrokeMask[pixelIndex] = true;
                            byte* p = basePtr + y0 * stride + x0 * 4;

                            // Lerp 插值
                            p[0] = (byte)(p[0] + (tB - p[0]) * globalOpacity);
                            p[1] = (byte)(p[1] + (tG - p[1]) * globalOpacity);
                            p[2] = (byte)(p[2] + (tR - p[2]) * globalOpacity);
                            p[3] = (byte)(p[3] + (tA - p[3]) * globalOpacity);
                        }
                    }

                    if (x0 == x1 && y0 == y1) break;
                    e2 = 2 * err;
                    if (e2 >= dy) { err += dy; x0 += sx; }
                    if (e2 <= dx) { err += dx; y0 += sy; }
                }
            }


            private unsafe void DrawSquareStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h, bool isEraser)
            {
                int size = (int)ctx.PenThickness;
                int half = size / 2;
                int x = (int)p.X - half;
                int y = (int)p.Y - half;

                // 核心逻辑变化：
                // 如果是橡皮擦，目标颜色是【全透明】。
                // 如果是普通方块笔，目标颜色是画笔颜色。
                Color targetColor = isEraser ? Color.FromArgb(255, 255,255, 255) : ctx.PenColor;

                float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
                if (globalOpacity <= 0.005f) return;

                float tB = targetColor.B;
                float tG = targetColor.G;
                float tR = targetColor.R;
                float tA = targetColor.A;

                int xend = Math.Min(w, x + size);
                int yend = Math.Min(h, y + size);
                int xstart = Math.Max(0, x);
                int ystart = Math.Max(0, y);

                for (int yy = ystart; yy < yend; yy++)
                {
                    byte* row = basePtr + yy * stride;
                    int rowIdx = yy * w;
                    for (int xx = xstart; xx < xend; xx++)
                    {
                        int pixelIndex = rowIdx + xx;
                        // 只有非橡皮擦才检查 Mask (橡皮擦通常允许重复擦除以加强效果，或者也为了均匀擦除检查Mask)
                        // 为了逻辑统一，建议这里也检查Mask，防止单次Draw内重叠导致的擦除不均匀
                        if (_currentStrokeMask[pixelIndex]) continue;
                        _currentStrokeMask[pixelIndex] = true;

                        byte* p2 = row + xx * 4;

                        p2[0] = (byte)(p2[0] + (tB - p2[0]) * globalOpacity);
                        p2[1] = (byte)(p2[1] + (tG - p2[1]) * globalOpacity);
                        p2[2] = (byte)(p2[2] + (tR - p2[2]) * globalOpacity);
                        p2[3] = (byte)(p2[3] + (tA - p2[3]) * globalOpacity);
                    }
                }
            }

            private unsafe void DrawBrushStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                Color c = ctx.PenColor;
                for (int i = 0; i < 20; i++)
                {
                    int dx = _rnd.Next(-2, 3);
                    int dy = _rnd.Next(-2, 3);
                    int xx = (int)p.X + dx;
                    int yy = (int)p.Y + dy;
                    if (xx >= 0 && xx < w && yy >= 0 && yy < h)
                    {
                        byte* ptr = basePtr + yy * stride + xx * 4;
                        ptr[0] = c.B; ptr[1] = c.G; ptr[2] = c.R; ptr[3] = c.A;
                    }
                }
            }

            private unsafe void DrawSprayStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                if (_sprayPatterns == null) InitializeSprayPatterns();
                int radius = (int)(ctx.PenThickness / 2.0);
                if (radius < 1) radius = 1;

                int count = 80;
                var pattern = _sprayPatterns[_patternIndex];
                _patternIndex = (_patternIndex + 1) % _sprayPatterns.Count;

                Color targetColor = ctx.PenColor;
                float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
                if (globalOpacity <= 0.005f) return;

                float tB = targetColor.B;
                float tG = targetColor.G;
                float tR = targetColor.R;
                float tA = targetColor.A;

                for (int i = 0; i < count && i < pattern.Length; i++)
                {
                    int xx = (int)(p.X + pattern[i].X * radius);
                    int yy = (int)(p.Y + pattern[i].Y * radius);

                    if (xx >= 0 && xx < w && yy >= 0 && yy < h)
                    {
                        byte* pPx = basePtr + yy * stride + xx * 4;
                        // 喷枪是随机打点，通常不使用 StrokeMask，允许粒子重叠以产生更浓密的效果
                        // 但这也意味着如果 Opacity 很高，重叠处会瞬间变成目标色

                        pPx[0] = (byte)(pPx[0] + (tB - pPx[0]) * globalOpacity);
                        pPx[1] = (byte)(pPx[1] + (tG - pPx[1]) * globalOpacity);
                        pPx[2] = (byte)(pPx[2] + (tR - pPx[2]) * globalOpacity);
                        pPx[3] = (byte)(pPx[3] + (tA - pPx[3]) * globalOpacity);
                    }
                }
            }


            private unsafe void DrawMosaicStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                int radius = (int)(ctx.PenThickness / 2.0);
                if (radius < 1) radius = 1;
                int blockSize = 12;

                int x_start = (int)Math.Max(0, p.X - radius);
                int x_end = (int)Math.Min(w, p.X + radius);
                int y_start = (int)Math.Max(0, p.Y - radius);
                int y_end = (int)Math.Min(h, p.Y + radius);

                for (int y = y_start; y < y_end; y++)
                {
                    for (int x = x_start; x < x_end; x++)
                    {
                        double dist = Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));
                        if (dist < radius)
                        {
                            int blockX = (x / blockSize) * blockSize;
                            int blockY = (y / blockSize) * blockSize;
                            blockX = Math.Clamp(blockX, 0, w - 1);
                            blockY = Math.Clamp(blockY, 0, h - 1);

                            byte* sourcePixel = basePtr + blockY * stride + blockX * 4;
                            byte* targetPixel = basePtr + y * stride + x * 4;

                            targetPixel[0] = sourcePixel[0];
                            targetPixel[1] = sourcePixel[1];
                            targetPixel[2] = sourcePixel[2];
                            targetPixel[3] = 255;
                        }
                    }
                }
            }

            private unsafe void DrawWatercolorStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                int radius = (int)(ctx.PenThickness / 2.0);
                if (radius < 1) radius = 1;
                Color targetColor = ctx.PenColor;
                float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;

                if (globalOpacity <= 0.005f) return;

                // 水彩通常比较淡，我们在全局力度基础上再乘一个系数，防止一下涂太死
                float baseOpacity = globalOpacity * 0.5f;

                float tB = targetColor.B;
                float tG = targetColor.G;
                float tR = targetColor.R;
                float tA = targetColor.A;

                double irregularRadius = radius * (0.9 + _rnd.NextDouble() * 0.2);
                int x_start = (int)Math.Max(0, p.X - radius);
                int x_end = (int)Math.Min(w, p.X + radius);
                int y_start = (int)Math.Max(0, p.Y - radius);
                int y_end = (int)Math.Min(h, p.Y + radius);

                for (int y = y_start; y < y_end; y++)
                {
                    byte* rowPtr = basePtr + y * stride;
                    for (int x = x_start; x < x_end; x++)
                    {
                        double dist = Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));
                        if (dist < irregularRadius)
                        {
                            // 边缘衰减
                            double falloff = 1.0 - (dist / irregularRadius);
                            // 核心修改：衰减值影响的是【混合力度 (Opacity)】，而不是目标颜色的 Alpha
                            float localOpacity = baseOpacity * (float)(falloff * falloff);

                            if (localOpacity > 0.001f)
                            {
                                byte* pPx = rowPtr + x * 4;
                                // 水彩也不使用 Mask，允许笔触内部叠加产生自然的深浅不一

                                pPx[0] = (byte)(pPx[0] + (tB - pPx[0]) * localOpacity);
                                pPx[1] = (byte)(pPx[1] + (tG - pPx[1]) * localOpacity);
                                pPx[2] = (byte)(pPx[2] + (tR - pPx[2]) * localOpacity);
                                pPx[3] = (byte)(pPx[3] + (tA - pPx[3]) * localOpacity);
                            }
                        }
                    }
                }
            }


            private unsafe void DrawOilPaintStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                double globalOpacity = TabPaint.SettingsManager.Instance.Current.PenOpacity;
                byte alpha = (byte)((0.2 * 255 / Math.Max(1, Math.Pow(ctx.PenThickness, 0.5))) * globalOpacity);

                if (alpha == 0) return;

                int radius = (int)(ctx.PenThickness / 2.0);
                if (radius < 1) radius = 1;
                Color baseColor = ctx.PenColor;
                int x_center = (int)p.X;
                int y_center = (int)p.Y;

                int numClumps = radius / 2 + 5;
                int brightnessVariation = 40;

                for (int i = 0; i < numClumps; i++)
                {
                    int brightnessOffset = _rnd.Next(-brightnessVariation, brightnessVariation + 1);
                    byte clumpR = ClampColor(baseColor.R + brightnessOffset);
                    byte clumpG = ClampColor(baseColor.G + brightnessOffset);
                    byte clumpB = ClampColor(baseColor.B + brightnessOffset);

                    double angle = _rnd.NextDouble() * 2 * Math.PI;
                    double distFromCenter = Math.Sqrt(_rnd.NextDouble()) * radius;
                    int clumpCenterX = x_center + (int)(distFromCenter * Math.Cos(angle));
                    int clumpCenterY = y_center + (int)(distFromCenter * Math.Sin(angle));

                    int clumpRadius = _rnd.Next(1, radius / 4 + 3);
                    int clumpRadiusSq = clumpRadius * clumpRadius;

                    int startX = Math.Max(0, clumpCenterX - clumpRadius);
                    int endX = Math.Min(w, clumpCenterX + clumpRadius);
                    int startY = Math.Max(0, clumpCenterY - clumpRadius);
                    int endY = Math.Min(h, clumpCenterY + clumpRadius);

                    for (int y = startY; y < endY; y++)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            int dx = x - clumpCenterX;
                            int dy = y - clumpCenterY;
                            if (dx * dx + dy * dy < clumpRadiusSq)
                            {
                                byte* pixelPtr = basePtr + y * stride + x * 4;
                                byte oldB = pixelPtr[0];
                                byte oldG = pixelPtr[1];
                                byte oldR = pixelPtr[2];
                                pixelPtr[0] = (byte)((clumpB * alpha + oldB * (255 - alpha)) / 255);
                                pixelPtr[1] = (byte)((clumpG * alpha + oldG * (255 - alpha)) / 255);
                                pixelPtr[2] = (byte)((clumpR * alpha + oldR * (255 - alpha)) / 255);
                            }
                        }
                    }
                }
            }

            private unsafe void DrawHighlighterLineUnsafe(ToolContext ctx, Point p1, Point p2, byte* basePtr, int stride, int w, int h)
            {
                int r = (int)(ctx.PenThickness / 2.0);
                if (r < 1) r = 1;
                double globalOpacity = TabPaint.SettingsManager.Instance.Current.PenOpacity;
                byte baseAlpha = 30;
                Color c = Color.FromArgb((byte)(baseAlpha * globalOpacity), 255, 255, 0);


                int xmin = (int)Math.Min(p1.X, p2.X) - r;
                int ymin = (int)Math.Min(p1.Y, p2.Y) - r;
                int xmax = (int)Math.Max(p1.X, p2.X) + r;
                int ymax = (int)Math.Max(p1.Y, p2.Y) + r;

                xmin = Math.Max(0, xmin); ymin = Math.Max(0, ymin);
                xmax = Math.Min(w - 1, xmax); ymax = Math.Min(h - 1, ymax);

                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double lenSq = dx * dx + dy * dy;

                int invSA = 255 - c.A;

                for (int y = ymin; y <= ymax; y++)
                {
                    int rowStartIndex = y * w;
                    byte* rowPtr = basePtr + y * stride;
                    for (int x = xmin; x <= xmax; x++)
                    {
                        int pixelIndex = rowStartIndex + x;
                        if (_currentStrokeMask[pixelIndex]) continue;

                        double t = 0;
                        if (lenSq > 0)
                        {
                            t = ((x - p1.X) * dx + (y - p1.Y) * dy) / lenSq;
                            t = Math.Max(0, Math.Min(1, t));
                        }
                        double closeX = p1.X + t * dx;
                        double closeY = p1.Y + t * dy;
                        double distSq = (x - closeX) * (x - closeX) + (y - closeY) * (y - closeY);

                        if (distSq <= r * r)
                        {
                            _currentStrokeMask[pixelIndex] = true;
                            byte* p = rowPtr + x * 4;

                            byte oldB = p[0];
                            byte oldG = p[1];
                            byte oldR = p[2];
                            byte oldA = p[3];

                            p[0] = (byte)((c.B * c.A + oldB * invSA) / 255);
                            p[1] = (byte)((c.G * c.A + oldG * invSA) / 255);
                            p[2] = (byte)((c.R * c.A + oldR * invSA) / 255);
                            p[3] = (byte)(c.A + (oldA * invSA) / 255);
                        }
                    }
                }
            }

            // --- 辅助方法 ---

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
                    ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelWidth,
                    ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelHeight);
            }
        }


    }
}