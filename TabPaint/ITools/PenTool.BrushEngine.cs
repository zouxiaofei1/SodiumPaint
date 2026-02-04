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
    private unsafe Int32Rect? DrawContinuousStrokeUnsafe(ToolContext ctx, Point from, float fromP, Point to, float toP, byte* buffer, int stride, int w, int h)
    {
        if (IsLineBasedBrush(ctx.PenStyle))  return DrawBrushLineUnsafe(ctx, from, fromP, to, toP, buffer, stride, w, h);
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
    private unsafe Int32Rect? DrawBrushLineUnsafe(ToolContext ctx, Point p1, float p1Pressure, Point p2, float p2Pressure, byte* buffer, int stride, int w, int h)
    {
        switch (ctx.PenStyle)
        {
            case BrushStyle.Round:
            case BrushStyle.Calligraphy:
                DrawRoundStrokeUnsafe(ctx, p1, p1Pressure, p2, p2Pressure, buffer, stride, w, h);
                int maxPressureSize = (int)(ctx.PenThickness * Math.Max(p1Pressure, p2Pressure) + 2);
                return LineBounds(p1, p2, maxPressureSize);

            case BrushStyle.Pencil:
                DrawPencilLineUnsafe(ctx, p1, p1Pressure, p2, p2Pressure, buffer, stride, w, h);
                return LineBounds(p1, p2, 1);
            case BrushStyle.Highlighter:
                DrawHighlighterLineUnsafe(ctx, p1, p2, buffer, stride, w, h);
                return LineBounds(p1, p2, (int)ctx.PenThickness);
            case BrushStyle.Watercolor:
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
                return LineBounds(px, px, (int)ctx.PenThickness + 5);
            case BrushStyle.Spray:
                DrawSprayStrokeUnsafe(ctx, px, buffer, stride, w, h);
                return LineBounds(px, px, (int)ctx.PenThickness * 2);
            case BrushStyle.Mosaic:
                DrawMosaicStrokeUnsafe(ctx, px, buffer, stride, w, h);
                return LineBounds(px, px, (int)ctx.PenThickness + 5);
            case BrushStyle.GaussianBlur:
                DrawBlurStrokeUnsafe(ctx, px, buffer, stride, w, h);
                float op = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
                int kRad = 1 + (int)(op * 49);
                return LineBounds(px, px, (int)ctx.PenThickness + kRad * 2 + 5);
        }
        return null;
    }
    private unsafe void DrawBlurStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        int size = (int)ctx.PenThickness;
        int r = size / 2;
        if (r < 1) r = 1;
        float opacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        int kernelRadius = 1 + (int)(opacity * 49);

        int x = (int)p.X;
        int y = (int)p.Y;

        int sourceRectX = x - r - kernelRadius;
        int sourceRectY = y - r - kernelRadius;
        int sourceRectW = (r * 2) + (kernelRadius * 2);
        int sourceRectH = (r * 2) + (kernelRadius * 2);

        // 边界裁剪
        int roiX = Math.Max(0, sourceRectX);
        int roiY = Math.Max(0, sourceRectY);
        int roiR = Math.Min(w, sourceRectX + sourceRectW);
        int roiB = Math.Min(h, sourceRectY + sourceRectH);
        int roiW = roiR - roiX;
        int roiH = roiB - roiY;

        if (roiW <= 0 || roiH <= 0) return;
        long[,] satB = new long[roiH + 1, roiW + 1];
        long[,] satG = new long[roiH + 1, roiW + 1];
        long[,] satR = new long[roiH + 1, roiW + 1];
        long[,] satA = new long[roiH + 1, roiW + 1];

        // 填充积分图
        for (int iy = 0; iy < roiH; iy++)
        {
            byte* srcRow = basePtr + (roiY + iy) * stride;
            for (int ix = 0; ix < roiW; ix++)
            {
                byte* ptr = srcRow + (roiX + ix) * 4;

                satB[iy + 1, ix + 1] = ptr[0] + satB[iy, ix + 1] + satB[iy + 1, ix] - satB[iy, ix];
                satG[iy + 1, ix + 1] = ptr[1] + satG[iy, ix + 1] + satG[iy + 1, ix] - satG[iy, ix];
                satR[iy + 1, ix + 1] = ptr[2] + satR[iy, ix + 1] + satR[iy + 1, ix] - satR[iy, ix];
                satA[iy + 1, ix + 1] = ptr[3] + satA[iy, ix + 1] + satA[iy + 1, ix] - satA[iy, ix];
            }
        }
        int paintXMin = Math.Max(0, x - r);
        int paintYMin = Math.Max(0, y - r);
        int paintXMax = Math.Min(w, x + r);
        int paintYMax = Math.Min(h, y + r);

        int rSq = r * r;

        for (int py = paintYMin; py < paintYMax; py++)
        {
            byte* dstRow = basePtr + py * stride;
            int dy = py - y;
            int dySq = dy * dy;

            for (int px = paintXMin; px < paintXMax; px++)
            {
                int dx = px - x;
                if (dx * dx + dySq > rSq) continue;

                int pixelIndex = py * w + px;
                if (_currentStrokeMask[pixelIndex]) continue;
                _currentStrokeMask[pixelIndex] = true;
                int localX = px - roiX;
                int localY = py - roiY;
                int boxX1 = Math.Max(0, localX - kernelRadius);
                int boxY1 = Math.Max(0, localY - kernelRadius);
                int boxX2 = Math.Min(roiW, localX + kernelRadius + 1);
                int boxY2 = Math.Min(roiH, localY + kernelRadius + 1);

                int area = (boxX2 - boxX1) * (boxY2 - boxY1);
                if (area <= 0) continue;
                long sumB = satB[boxY2, boxX2] - satB[boxY1, boxX2] - satB[boxY2, boxX1] + satB[boxY1, boxX1];
                long sumG = satG[boxY2, boxX2] - satG[boxY1, boxX2] - satG[boxY2, boxX1] + satG[boxY1, boxX1];
                long sumR = satR[boxY2, boxX2] - satR[boxY1, boxX2] - satR[boxY2, boxX1] + satR[boxY1, boxX1];
                long sumA = satA[boxY2, boxX2] - satA[boxY1, boxX2] - satA[boxY2, boxX1] + satA[boxY1, boxX1];

                byte* dstPtr = dstRow + px * 4;
                dstPtr[0] = (byte)(sumB / area);
                dstPtr[1] = (byte)(sumG / area);
                dstPtr[2] = (byte)(sumR / area);
                dstPtr[3] = (byte)(sumA / area);
            }
        }
    }


    private unsafe void DrawRoundStrokeUnsafe(ToolContext ctx, Point p1, float p1P, Point p2, float p2P, byte* basePtr, int stride, int w, int h)
    {
        double baseThickness = ctx.PenThickness;
        double minScale = 0.2;
        double r1 = Math.Max(0.5, (baseThickness * (minScale + (1.0 - minScale) * p1P)) / 2.0);
        double r2 = Math.Max(0.5, (baseThickness * (minScale + (1.0 - minScale) * p2P)) / 2.0);

        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        if (globalOpacity <= 0.005f) return;

        Color targetColor = ctx.PenColor;
        float targetB = targetColor.B;
        float targetG = targetColor.G;
        float targetR = targetColor.R;
        float targetA = targetColor.A;

        double maxR = Math.Max(r1, r2);
        int xmin = (int)(Math.Min(p1.X, p2.X) - maxR - 1);
        int ymin = (int)(Math.Min(p1.Y, p2.Y) - maxR - 1);
        int xmax = (int)(Math.Max(p1.X, p2.X) + maxR + 1);
        int ymax = (int)(Math.Max(p1.Y, p2.Y) + maxR + 1);

        xmin = Math.Max(0, xmin); ymin = Math.Max(0, ymin);
        xmax = Math.Min(w - 1, xmax); ymax = Math.Min(h - 1, ymax);

        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double lenSq = dx * dx + dy * dy;

        for (int y = ymin; y <= ymax; y++)
        {
            byte* rowPtr = basePtr + y * stride;
            int rowIdx = y * w;

            for (int x = xmin; x <= xmax; x++)
            {
                int pixelIndex = rowIdx + x;
                if (_currentStrokeMask[pixelIndex]) continue;

                double t = 0;
                if (lenSq > 0)
                {
                    t = ((x - p1.X) * dx + (y - p1.Y) * dy) / lenSq;
                    t = Math.Max(0, Math.Min(1, t));
                }

                double currentR = r1 + (r2 - r1) * t;
                double rSq = currentR * currentR;

                double projx = p1.X + t * dx;
                double projy = p1.Y + t * dy;
                double distSq = (x - projx) * (x - projx) + (y - projy) * (y - projy);

                if (distSq <= rSq)
                {
                    _currentStrokeMask[pixelIndex] = true;
                    float currentPressure = p1P + (p2P - p1P) * (float)t;
                    float pressureAlpha = 0.2f + 0.8f * (float)Math.Pow(currentPressure, 0.5);
                    float dynamicOpacity = globalOpacity * pressureAlpha;

                    byte* p = rowPtr + x * 4;
                    p[0] = (byte)(p[0] + (targetB - p[0]) * dynamicOpacity);
                    p[1] = (byte)(p[1] + (targetG - p[1]) * dynamicOpacity);
                    p[2] = (byte)(p[2] + (targetR - p[2]) * dynamicOpacity);
                    p[3] = (byte)(p[3] + (targetA - p[3]) * dynamicOpacity);
                }
            }
        }
    }

    private unsafe void DrawPencilLineUnsafe(ToolContext ctx, Point p1, float p1P, Point p2, float p2P, byte* basePtr, int stride, int w, int h)
    {
        int x0 = (int)p1.X; int y0 = (int)p1.Y;
        int x1 = (int)p2.X; int y1 = (int)p2.Y;

        Color targetColor = ctx.PenColor;
        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        if (globalOpacity <= 0.005f) return;

        float tB = targetColor.B; float tG = targetColor.G;
        float tR = targetColor.R; float tA = targetColor.A;

        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy, e2;
        float totalDist = (float)Math.Sqrt(dx * dx + dy * dy);
        if (totalDist == 0) totalDist = 1;
        int startX = x0; int startY = y0;

        while (true)
        {
            if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h)
            {
                int pixelIndex = y0 * w + x0;
                if (!_currentStrokeMask[pixelIndex])
                {
                    _currentStrokeMask[pixelIndex] = true;
                    float currentDist = (float)Math.Sqrt(Math.Pow(x0 - startX, 2) + Math.Pow(y0 - startY, 2));
                    float t = currentDist / totalDist;
                    float currentPressure = p1P + (p2P - p1P) * t;
                    float localOpacity = globalOpacity * Math.Clamp(currentPressure, 0.1f, 1.0f);

                    byte* p = basePtr + y0 * stride + x0 * 4;
                    p[0] = (byte)(p[0] + (tB - p[0]) * localOpacity);
                    p[1] = (byte)(p[1] + (tG - p[1]) * localOpacity);
                    p[2] = (byte)(p[2] + (tR - p[2]) * localOpacity);
                    p[3] = (byte)(p[3] + (tA - p[3]) * localOpacity);
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
        Color targetColor = isEraser ? Color.FromArgb(255, 255, 255, 255) : ctx.PenColor;

        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        if (globalOpacity <= 0.005f) return;

        float tB = targetColor.B; float tG = targetColor.G;
        float tR = targetColor.R; float tA = targetColor.A;

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
        int r = (int)(ctx.PenThickness / 2.0);
        if (r < 1) r = 1;
        int count = r * r * 4;
        if (count < 20) count = 20;
        if (count > 2000) count = 2000;
        Color c = ctx.PenColor;
        for (int i = 0; i < count; i++)
        {
            int dx = _rnd.Next(-r, r + 1);
            int dy = _rnd.Next(-r, r + 1);
            if (dx * dx + dy * dy > r * r) continue;
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
        float tB = targetColor.B; float tG = targetColor.G;
        float tR = targetColor.R; float tA = targetColor.A;

        for (int i = 0; i < count && i < pattern.Length; i++)
        {
            int xx = (int)(p.X + pattern[i].X * radius);
            int yy = (int)(p.Y + pattern[i].Y * radius);
            if (xx >= 0 && xx < w && yy >= 0 && yy < h)
            {
                byte* pPx = basePtr + yy * stride + xx * 4;
                pPx[0] = (byte)(pPx[0] + (tB - pPx[0]) * globalOpacity);
                pPx[1] = (byte)(pPx[1] + (tG - pPx[1]) * globalOpacity);
                pPx[2] = (byte)(pPx[2] + (tR - pPx[2]) * globalOpacity);
                pPx[3] = (byte)(pPx[3] + (tA - pPx[3]) * globalOpacity);
            }
        }
    }

    private unsafe void DrawMosaicStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        int blockSize = Math.Max(4, (int)(ctx.PenThickness / 4.0));
        int radius = (int)(ctx.PenThickness / 2.0);
        if (radius < 1) radius = 1;
        long radiusSq = (long)radius * radius;
        int x_start = (int)Math.Max(0, p.X - radius);
        int x_end = (int)Math.Min(w, p.X + radius);
        int y_start = (int)Math.Max(0, p.Y - radius);
        int y_end = (int)Math.Min(h, p.Y + radius);
        int gridXStart = (x_start / blockSize) * blockSize;
        int gridYStart = (y_start / blockSize) * blockSize;

        for (int by = gridYStart; by < y_end; by += blockSize)
        {
            for (int bx = gridXStart; bx < x_end; bx += blockSize)
            {
                int sampleX = Math.Clamp(bx, 0, w - 1);
                int sampleY = Math.Clamp(by, 0, h - 1);
                byte* srcPixel = basePtr + sampleY * stride + sampleX * 4;
                byte B = srcPixel[0]; byte G = srcPixel[1]; byte R = srcPixel[2];
                int fillStartY = Math.Max(by, y_start);
                int fillEndY = Math.Min(by + blockSize, y_end);
                int fillStartX = Math.Max(bx, x_start);
                int fillEndX = Math.Min(bx + blockSize, x_end);
                for (int y = fillStartY; y < fillEndY; y++)
                {
                    long dy = y - (int)p.Y;
                    long dy2 = dy * dy;
                    byte* rowPtr = basePtr + y * stride;
                    for (int x = fillStartX; x < fillEndX; x++)
                    {
                        long dx = x - (int)p.X;
                        if (dx * dx + dy2 < radiusSq)
                        {
                            byte* destPixel = rowPtr + x * 4;
                            destPixel[0] = B; destPixel[1] = G; destPixel[2] = R;
                        }
                    }
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
        float baseOpacity = globalOpacity * 0.5f;
        float tB = targetColor.B; float tG = targetColor.G;
        float tR = targetColor.R; float tA = targetColor.A;
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
                    double falloff = 1.0 - (dist / irregularRadius);
                    float localOpacity = baseOpacity * (float)(falloff * falloff);
                    if (localOpacity > 0.001f)
                    {
                        byte* pPx = rowPtr + x * 4;
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
        int x_center = (int)p.X; int y_center = (int)p.Y;
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
                        byte oldB = pixelPtr[0]; byte oldG = pixelPtr[1]; byte oldR = pixelPtr[2];
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
                    byte oldB = p[0]; byte oldG = p[1]; byte oldR = p[2]; byte oldA = p[3];
                    p[0] = (byte)((c.B * c.A + oldB * invSA) / 255);
                    p[1] = (byte)((c.G * c.A + oldG * invSA) / 255);
                    p[2] = (byte)((c.R * c.A + oldR * invSA) / 255);
                    p[3] = (byte)(c.A + (oldA * invSA) / 255);
                }
            }
        }
    }
}