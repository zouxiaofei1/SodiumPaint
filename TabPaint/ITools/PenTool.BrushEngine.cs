using System.Collections.Concurrent;

using System.IO;
using System.Runtime.CompilerServices;
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
            case BrushStyle.Mosaic:
                DrawMosaicLineUnsafe(ctx, p1, p2, buffer, stride, w, h);
                return LineBounds(p1, p2, (int)ctx.PenThickness + 5);
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
                break;
            case BrushStyle.Brush:
                DrawBrushStrokeLineUnsafe(ctx, p1, p1Pressure, p2, p2Pressure, buffer, stride, w, h);
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
            case BrushStyle.Spray:
                DrawSprayStrokeUnsafe(ctx, px, buffer, stride, w, h);
                return LineBounds(px, px, (int)ctx.PenThickness * 2);
            case BrushStyle.GaussianBlur:
                DrawBlurStrokeUnsafeParallel(ctx, px, buffer, stride, w, h);
                float op = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
                int kRad = 1 + (int)(op * 49);
                return LineBounds(px, px, (int)ctx.PenThickness + kRad * 2 + 5);
        }
        return null;
    }
    private int[] _satBufferInt;      // 积分图改用 int（ROI 尺寸有限，不会溢出）
    private int _satIntCapacity;
    private unsafe void DrawBlurStrokeUnsafeParallel(
        ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        int size = (int)ctx.PenThickness;
        int r = size / 2;
        if (r < 1) r = 1;

        float opacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        int kernelRadius = 1 + (int)(opacity * 49);

        int cx = (int)p.X;
        int cy = (int)p.Y;
        int roiX = Math.Max(0, cx - r - kernelRadius);
        int roiY = Math.Max(0, cy - r - kernelRadius);
        int roiR = Math.Min(w, cx + r + kernelRadius);
        int roiB = Math.Min(h, cy + r + kernelRadius);
        int roiW = roiR - roiX;
        int roiH = roiB - roiY;
        if (roiW <= 0 || roiH <= 0) return;
        int satW = roiW + 1;
        int satH = roiH + 1;
        int satStride4 = satW * 4;// 每行元素数（含4通道）
        int totalNeeded = satH * satStride4;

        if (_satBufferInt == null || _satIntCapacity < totalNeeded)
        {
            _satIntCapacity = totalNeeded;
            _satBufferInt = new int[_satIntCapacity];
        }
        else Array.Clear(_satBufferInt, 0, totalNeeded);

        fixed (int* sat = _satBufferInt)
        {
            for (int iy = 0; iy < roiH; iy++)
            {
                byte* srcRow = basePtr + (long)(roiY + iy) * stride + roiX * 4;
                int* curRow = sat + (iy + 1) * satStride4;
                int* prevRow = sat + iy * satStride4;

                // 行内展开：利用局部变量做行前缀和，减少随机内存访问
                int prefB = 0, prefG = 0, prefR = 0, prefA = 0;

                for (int ix = 0; ix < roiW; ix++)
                {
                    byte* px = srcRow + ix * 4;
                    prefB += px[0];
                    prefG += px[1];
                    prefR += px[2];
                    prefA += px[3];

                    int idx = (ix + 1) * 4;
                    curRow[idx + 0] = prefB + prevRow[idx + 0];
                    curRow[idx + 1] = prefG + prevRow[idx + 1];
                    curRow[idx + 2] = prefR + prevRow[idx + 2];
                    curRow[idx + 3] = prefA + prevRow[idx + 3];
                }
            }
            int paintXMin = Math.Max(0, cx - r);
            int paintYMin = Math.Max(0, cy - r);
            int paintXMax = Math.Min(w, cx + r);
            int paintYMax = Math.Min(h, cy + r);
            int rSq = r * r;

            // 捕获到局部变量，供 lambda 使用
            int localRoiX = roiX;
            int localRoiY = roiY;
            int localRoiW = roiW;
            int localRoiH = roiH;
            int localKernelRadius = kernelRadius;
            int localW = w;
            int localStride = stride;
            int localSatStride4 = satStride4;
            int localCx = cx;
            int localCy = cy;
            int localRSq = rSq;
            int* satPtr = sat;
            byte* bufPtr = basePtr;
            byte[] maskRef = _currentStrokeMask;

            int rowCount = paintYMax - paintYMin;
            if (rowCount <= 0) return;

            const int ParallelThreshold = 16; // 少于16行不值得并行

            if (rowCount >= ParallelThreshold)
            {
                // ── 并行版本 ──
                Parallel.For(paintYMin, paintYMax, new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                },
                (int py) =>
                {
                    BlurApplyRow(
                        satPtr, localSatStride4,
                        bufPtr, localStride, localW,
                        maskRef,
                        py, paintXMin, paintXMax,
                        localCx, localCy, localRSq,
                        localRoiX, localRoiY, localRoiW, localRoiH,
                        localKernelRadius);
                });
            }
            else
            {
                // ── 串行版本（小区域） ──
                for (int py = paintYMin; py < paintYMax; py++)
                {
                    BlurApplyRow(
                        satPtr, localSatStride4,
                        bufPtr, localStride, localW,
                        maskRef,
                        py, paintXMin, paintXMax,
                        localCx, localCy, localRSq,
                        localRoiX, localRoiY, localRoiW, localRoiH,
                        localKernelRadius);
                }
            }
        } // fixed
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void BlurApplyRow(
        int* sat, int satStride4,
        byte* basePtr, int stride, int imgW,
        byte[] strokeMask,
        int py, int paintXMin, int paintXMax,
        int cx, int cy, int rSq,
        int roiX, int roiY, int roiW, int roiH,
        int kernelRadius)
    {
        int dy = py - cy;
        int dySq = dy * dy;
        if (dySq > rSq) return; // 整行在圆外

        int maxDx = (int)Math.Sqrt(rSq - dySq);
        int rowXMin = Math.Max(paintXMin, cx - maxDx);
        int rowXMax = Math.Min(paintXMax, cx + maxDx + 1);

        byte* dstRow = basePtr + (long)py * stride;
        int rowIdx = py * imgW;

        int localY = py - roiY;

        for (int px = rowXMin; px < rowXMax; px++)
        {
            int dx = px - cx;
            // 精确圆形检测（上面已粗筛，这里做精确判断）
            if (dx * dx + dySq > rSq) continue;

            int pixelIndex = rowIdx + px;
            if (strokeMask[pixelIndex] >= 255) continue;
            strokeMask[pixelIndex] = 255;

            // 积分图查询：box [boxY1..boxY2) × [boxX1..boxX2)
            int localX = px - roiX;
            int boxX1 = localX - kernelRadius;
            int boxY1 = localY - kernelRadius;
            int boxX2 = localX + kernelRadius + 1;
            int boxY2 = localY + kernelRadius + 1;

            // Clamp to ROI bounds
            if (boxX1 < 0) boxX1 = 0;
            if (boxY1 < 0) boxY1 = 0;
            if (boxX2 > roiW) boxX2 = roiW;
            if (boxY2 > roiH) boxY2 = roiH;

            int area = (boxX2 - boxX1) * (boxY2 - boxY1);
            if (area <= 0) continue;

            // 积分图四角索引
            int i22 = boxY2 * satStride4 + boxX2 * 4;
            int i12 = boxY1 * satStride4 + boxX2 * 4;
            int i21 = boxY2 * satStride4 + boxX1 * 4;
            int i11 = boxY1 * satStride4 + boxX1 * 4;
            byte* dstPtr = dstRow + px * 4;
            dstPtr[0] = (byte)((sat[i22 + 0] - sat[i12 + 0] - sat[i21 + 0] + sat[i11 + 0]) / area);
            dstPtr[1] = (byte)((sat[i22 + 1] - sat[i12 + 1] - sat[i21 + 1] + sat[i11 + 1]) / area);
            dstPtr[2] = (byte)((sat[i22 + 2] - sat[i12 + 2] - sat[i21 + 2] + sat[i11 + 2]) / area);
            dstPtr[3] = (byte)((sat[i22 + 3] - sat[i12 + 3] - sat[i21 + 3] + sat[i11 + 3]) / area);
        }
    }

    private unsafe void DrawRoundStrokeUnsafe(ToolContext ctx, Point p1, float p1P,
     Point p2, float p2P, byte* basePtr, int stride, int w, int h)
    {
      
        double baseThickness = ctx.PenThickness;
        double minScale = 0.2;
        double r1 = Math.Max(0.5, (baseThickness * (minScale + (1.0 - minScale) * p1P)) / 2.0);
        double r2 = Math.Max(0.5, (baseThickness * (minScale + (1.0 - minScale) * p2P)) / 2.0);

        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        if (globalOpacity <= 0.005f) return;

        Color targetColor = ctx.PenColor;
        float targetB = targetColor.B, targetG = targetColor.G;
        float targetR = targetColor.R, targetA = targetColor.A;
        float alpha1 = globalOpacity * (0.2f + 0.8f * MathF.Sqrt(p1P));
        float alpha2 = globalOpacity * (0.2f + 0.8f * MathF.Sqrt(p2P));

        double maxR = Math.Max(r1, r2);
        double feather = Math.Max(1.0, maxR * 0.08);

        int xmin = Math.Max(0, (int)(Math.Min(p1.X, p2.X) - maxR - feather - 1));
        int ymin = Math.Max(0, (int)(Math.Min(p1.Y, p2.Y) - maxR - feather - 1));
        int xmax = Math.Min(w - 1, (int)(Math.Max(p1.X, p2.X) + maxR + feather + 1));
        int ymax = Math.Min(h - 1, (int)(Math.Max(p1.Y, p2.Y) + maxR + feather + 1));

        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double lenSq = dx * dx + dy * dy;
        double invLenSq = lenSq > 0 ? 1.0 / lenSq : 0;

        int totalRows = ymax - ymin + 1;

        Parallel.For(ymin, ymax + 1, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        },
        (y) =>
        {
            DrawRoundSingleRow(y, xmin, xmax,
                basePtr, stride, w, h,
                p1, p2, r1, r2, dx, dy, lenSq, invLenSq,
                feather, alpha1, alpha2,
                targetB, targetG, targetR, targetA);
        });
     
    }

    private unsafe void DrawRoundSingleRow(
      int y, int xmin, int xmax,
      byte* basePtr, int stride, int w, int h,
      Point p1, Point p2, double r1, double r2,
      double dx, double dy, double lenSq, double invLenSq,
      double feather, float alpha1, float alpha2,
      float targetB, float targetG, float targetR, float targetA)
    {
      
        byte* rowPtr = basePtr + y * stride;
        int rowIdx = y * w;
        double pyDy = y - p1.Y;

        for (int x = xmin; x <= xmax; x++)
        {
            double pxDx = x - p1.X;
            double t = lenSq > 0
                ? Math.Clamp((pxDx * dx + pyDy * dy) * invLenSq, 0, 1)
                : 0;

            double currentR = r1 + (r2 - r1) * t;
            double projx = p1.X + t * dx;
            double projy = p1.Y + t * dy;
            double distSq = (x - projx) * (x - projx) + (y - projy) * (y - projy);

            double outerR = currentR + feather;
            if (distSq > outerR * outerR) continue;

            double innerR = currentR - feather;
            if (innerR < 0) innerR = 0;

            float edgeFactor;
            if (distSq <= innerR * innerR)
            {
                edgeFactor = 1.0f;
            }
            else
            {
                double dist = Math.Sqrt(distSq);
                edgeFactor = (float)((outerR - dist) / (outerR - innerR));
                edgeFactor = edgeFactor * edgeFactor * (3.0f - 2.0f * edgeFactor);
            }

            if (edgeFactor <= 0.002f) continue;

            float dynamicOpacity = alpha1 + (alpha2 - alpha1) * (float)t;
            float desiredOpacity = dynamicOpacity * edgeFactor;
            byte desiredLevel = (byte)(desiredOpacity * 255.0f);
            if (desiredLevel == 0) continue;

            int pixelIndex = rowIdx + x;
            byte currentLevel = _currentStrokeMask[pixelIndex];
            if (currentLevel >= desiredLevel) continue;

            float currentOpacity = currentLevel / 255.0f;
            float delta = desiredOpacity - currentOpacity;
            _currentStrokeMask[pixelIndex] = desiredLevel;

            if (delta <= 0.001f) continue;
            float remaining = 1.0f - currentOpacity;
            float blend = remaining <= 0.001f ? 1.0f
                : Math.Min(delta / remaining, 1.0f);

            byte* bp = rowPtr + x * 4;
            bp[0] = (byte)(bp[0] + (targetB - bp[0]) * blend);
            bp[1] = (byte)(bp[1] + (targetG - bp[1]) * blend);
            bp[2] = (byte)(bp[2] + (targetR - bp[2]) * blend);
            bp[3] = (byte)(bp[3] + (targetA - bp[3]) * blend);
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
                if (!(_currentStrokeMask[pixelIndex] >= 255))
                {
                    _currentStrokeMask[pixelIndex] = 255;
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
                if (_currentStrokeMask[pixelIndex] >= 255) continue;
                _currentStrokeMask[pixelIndex] = 255;

                byte* p2 = row + xx * 4;
                p2[0] = (byte)(p2[0] + (tB - p2[0]) * globalOpacity);
                p2[1] = (byte)(p2[1] + (tG - p2[1]) * globalOpacity);
                p2[2] = (byte)(p2[2] + (tR - p2[2]) * globalOpacity);
                p2[3] = (byte)(p2[3] + (tA - p2[3]) * globalOpacity);
            }
        }
    }


    private unsafe void DrawBrushStrokeLineUnsafe(ToolContext ctx, Point p1, float p1P, Point p2, float p2P,
    byte* basePtr, int stride, int w, int h)
    {
        int r = (int)(ctx.PenThickness / 2.0);
        if (r < 1) r = 1;

        Color c = ctx.PenColor;
        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        if (globalOpacity <= 0.005f) return;

        float tB = c.B, tG = c.G, tR = c.R, tA = c.A;

        double maxR = r;
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double lenSq = dx * dx + dy * dy;
        double invLenSq = lenSq > 0 ? 1.0 / lenSq : 0;

        int xmin = Math.Max(0, (int)(Math.Min(p1.X, p2.X) - maxR - 1));
        int ymin = Math.Max(0, (int)(Math.Min(p1.Y, p2.Y) - maxR - 1));
        int xmax = Math.Min(w - 1, (int)(Math.Max(p1.X, p2.X) + maxR + 1));
        int ymax = Math.Min(h - 1, (int)(Math.Max(p1.Y, p2.Y) + maxR + 1));

        int rSq = r * r;
        for (int y = ymin; y <= ymax; y++)
        {
            byte* rowPtr = basePtr + y * stride;
            int rowIdx = y * w;

            for (int x = xmin; x <= xmax; x++)
            {
                // 计算到线段的最近距离
                double t = 0;
                if (lenSq > 0)
                {
                    t = ((x - p1.X) * dx + (y - p1.Y) * dy) * invLenSq;
                    if (t < 0) t = 0;
                    else if (t > 1) t = 1;
                }

                double projX = p1.X + t * dx;
                double projY = p1.Y + t * dy;
                double distX = x - projX;
                double distY = y - projY;
                double distSq = distX * distX + distY * distY;

                if (distSq > rSq) continue;

                int pixelIndex = rowIdx + x;
                if (_currentStrokeMask[pixelIndex] >= 255) continue;
                uint hash = (uint)(x * 73856093 ^ y * 19349663);
                hash = (hash ^ (hash >> 16)) * 0x45d9f3b;
                hash = hash ^ (hash >> 16);
                float noise = (hash & 0xFF) / 255.0f;
                double normalizedDist = distSq / (double)rSq; // 0(中心)~1(边缘)
                float density = (float)(1.0 - normalizedDist * 0.7); // 中心密，边缘疏
                if (noise > density) continue;

                _currentStrokeMask[pixelIndex] = 255;

                byte* bp = rowPtr + x * 4;
                bp[0] = (byte)(bp[0] + (tB - bp[0]) * globalOpacity);
                bp[1] = (byte)(bp[1] + (tG - bp[1]) * globalOpacity);
                bp[2] = (byte)(bp[2] + (tR - bp[2]) * globalOpacity);
                bp[3] = (byte)(bp[3] + (tA - bp[3]) * globalOpacity);
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

    private unsafe void DrawMosaicLineUnsafe(ToolContext ctx, Point p1, Point p2,
     byte* basePtr, int stride, int w, int h)
    {
        var xyy = new TimeRecorder(); xyy.Toggle();
        int radius = (int)(ctx.PenThickness / 2.0);
        if (radius < 1) radius = 1;
        long radiusSq = (long)radius * radius;
        int blockSize = Math.Max(4, (int)(ctx.PenThickness / 4.0));

        // 1) 计算线段扫过区域的总包围盒（胶囊体的AABB）
        int aabbLeft = (int)(Math.Min(p1.X, p2.X) - radius);
        int aabbTop = (int)(Math.Min(p1.Y, p2.Y) - radius);
        int aabbRight = (int)(Math.Max(p1.X, p2.X) + radius);
        int aabbBottom = (int)(Math.Max(p1.Y, p2.Y) + radius);

        aabbLeft = Math.Max(0, aabbLeft);
        aabbTop = Math.Max(0, aabbTop);
        aabbRight = Math.Min(w, aabbRight);
        aabbBottom = Math.Min(h, aabbBottom);

        if (aabbLeft >= aabbRight || aabbTop >= aabbBottom) return;

        // 2) 线段参数（用于点到线段距离计算）
        double segDx = p2.X - p1.X;
        double segDy = p2.Y - p1.Y;
        double segLenSq = segDx * segDx + segDy * segDy;
        double invSegLenSq = segLenSq > 0 ? 1.0 / segLenSq : 0;
        int gridXStart = (aabbLeft / blockSize) * blockSize;
        int gridYStart = (aabbTop / blockSize) * blockSize;

        for (int by = gridYStart; by < aabbBottom; by += blockSize)
        {
            for (int bx = gridXStart; bx < aabbRight; bx += blockSize)
            {
                double blockCenterX = bx + blockSize * 0.5;
                double blockCenterY = by + blockSize * 0.5;
                double t = 0;
                if (segLenSq > 0)
                {
                    t = ((blockCenterX - p1.X) * segDx + (blockCenterY - p1.Y) * segDy) * invSegLenSq; t = Math.Clamp(t, 0, 1);
                }
                double closestX = p1.X + t * segDx;
                double closestY = p1.Y + t * segDy;
                double dcx = blockCenterX - closestX;
                double dcy = blockCenterY - closestY;
                double centerDistSq = dcx * dcx + dcy * dcy;
                double blockHalfDiag = blockSize * 0.7071; // sqrt(2)/2
                double threshold = radius + blockHalfDiag;
                if (centerDistSq > threshold * threshold) continue;
                int sampleX = Math.Clamp(bx, 0, w - 1);
                int sampleY = Math.Clamp(by, 0, h - 1);
                byte* srcPixel = basePtr + sampleY * stride + sampleX * 4;
                byte mB = srcPixel[0];
                byte mG = srcPixel[1];
                byte mR = srcPixel[2];
                int fillStartX = Math.Max(bx, aabbLeft);
                int fillEndX = Math.Min(bx + blockSize, aabbRight);
                int fillStartY = Math.Max(by, aabbTop);
                int fillEndY = Math.Min(by + blockSize, aabbBottom);

                for (int y = fillStartY; y < fillEndY; y++)
                {
                    byte* rowPtr = basePtr + y * stride;
                    int rowIdx = y * w;

                    for (int x = fillStartX; x < fillEndX; x++)
                    {
                        int pixelIndex = rowIdx + x;
                        if (_currentStrokeMask[pixelIndex] >= 255) continue;
                        double pt = 0;
                        if (segLenSq > 0)
                        {
                            pt = ((x - p1.X) * segDx + (y - p1.Y) * segDy) * invSegLenSq;
                            pt = Math.Clamp(pt, 0, 1);
                        }
                        double projX = p1.X + pt * segDx;
                        double projY = p1.Y + pt * segDy;
                        double pdx = x - projX;
                        double pdy = y - projY;

                        if (pdx * pdx + pdy * pdy <= radiusSq)
                        {
                            _currentStrokeMask[pixelIndex] = 255;
                            byte* destPixel = rowPtr + x * 4;
                            destPixel[0] = mB;
                            destPixel[1] = mG;
                            destPixel[2] = mR;
                        }
                    }
                }
            }
        }
        xyy.Toggle(slient: true);
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
        double irregularRadiusSq = irregularRadius * irregularRadius;
        double invIrregularRadius = 1.0 / irregularRadius;

        int x_start = (int)Math.Max(0, p.X - radius);
        int x_end = (int)Math.Min(w, p.X + radius);
        int y_start = (int)Math.Max(0, p.Y - radius);
        int y_end = (int)Math.Min(h, p.Y + radius);

        double pX = p.X, pY = p.Y;

        for (int y = y_start; y < y_end; y++)
        {
            byte* rowPtr = basePtr + y * stride;
            double dyVal = y - pY;
            double dySq = dyVal * dyVal;

            for (int x = x_start; x < x_end; x++)
            {
                double dxVal = x - pX;
                double distSq = dxVal * dxVal + dySq;
                if (distSq >= irregularRadiusSq) continue;
                double normalizedDistSq = distSq / irregularRadiusSq; // 0~1
                double falloffSq = (1.0 - normalizedDistSq); // 近似 (1-d/R)²当d/R较小时
                float localOpacity = baseOpacity * (float)(falloffSq * falloffSq);

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Div255(int v) { return (byte)((v * 257 + 257) >> 16); }
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
                if (_currentStrokeMask[pixelIndex] >= 255) continue;
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
                    _currentStrokeMask[pixelIndex] = 255;
                    byte* p = rowPtr + x * 4;
                    byte oldB = p[0]; byte oldG = p[1]; byte oldR = p[2]; byte oldA = p[3];
                    p[0] = Div255((c.B * c.A + oldB * invSA));
                    p[1] = Div255((c.G * c.A + oldG * invSA));
                    p[2] = Div255((c.R * c.A + oldR * invSA));
                }
            }
        }
    }
}