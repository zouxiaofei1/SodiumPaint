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
        if (IsLineBasedBrush(ctx.PenStyle)) return DrawBrushLineUnsafe(ctx, from, fromP, to, toP, buffer, stride, w, h);
        float dx = (float)(to.X - from.X);
        float dy = (float)(to.Y - from.Y);
        float length = MathF.Sqrt(dx * dx + dy * dy);

        if (length < 0.5f) return DrawBrushAtUnsafe(ctx, to, buffer, stride, w, h);

        int steps = 1;
        float thickness = (float)ctx.PenThickness;
        switch (ctx.PenStyle)
        {
            case BrushStyle.Square:
            case BrushStyle.Eraser:
                steps = (int)(length / MathF.Max(1f, thickness * 0.5f));
                break;
            case BrushStyle.Brush:
                steps = (int)(length * 0.5f);
                break;
            case BrushStyle.Spray:
                steps = (int)(length / MathF.Max(1f, thickness));
                break;
        }

        if (steps < 1) steps = 1;

        float xStep = dx / steps;
        float yStep = dy / steps;
        float x = (float)from.X;
        float y = (float)from.Y;

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
                float wdx = (float)(p2.X - p1.X);
                float wdy = (float)(p2.Y - p1.Y);
                float dist = MathF.Sqrt(wdx * wdx + wdy * wdy);
                int steps = (int)(dist / MathF.Max(1f, (float)ctx.PenThickness * 0.25f));
                if (steps == 0) steps = 1;
                float sx = wdx / steps;
                float sy = wdy / steps;
                float cx = (float)p1.X, cy = (float)p1.Y;
                for (int i = 0; i <= steps; i++)
                {
                    DrawWatercolorStrokeUnsafe(ctx, new Point(cx, cy), buffer, stride, w, h);
                    cx += sx; cy += sy;
                }
                return LineBounds(p1, p2, (int)ctx.PenThickness + 5);
            case BrushStyle.Crayon:
                float cdx = (float)(p2.X - p1.X);
                float cdy = (float)(p2.Y - p1.Y);
                float dist2 = MathF.Sqrt(cdx * cdx + cdy * cdy);
                int steps2 = (int)(dist2 / MathF.Max(1f, (float)ctx.PenThickness * 0.5f));
                if (steps2 == 0) steps2 = 1;
                float sx2 = cdx / steps2;
                float sy2 = cdy / steps2;
                float cx2 = (float)p1.X, cy2 = (float)p1.Y;
                for (int i = 0; i <= steps2; i++)
                {
                    DrawOilPaintStrokeUnsafe(ctx, new Point(cx2, cy2), buffer, stride, w, h);
                    cx2 += sx2; cy2 += sy2;
                }
                return LineBounds(p1, p2, (int)ctx.PenThickness + 5);
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
    private byte[] _blurSourceSnapshot;// 模糊画笔专用：原始图像快照
    private int _blurSnapshotStride;

    private unsafe void DrawBlurStrokeUnsafeParallel(
        ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        int size = (int)ctx.PenThickness;
        int r = size / 2;
        if (r < 1) r = 1;

        float opacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        int kernelRadius = 1 + (int)(opacity * r*0.2);

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
        int satStride4 = satW * 4;
        int totalNeeded = satH * satStride4;

        if (_satBufferInt == null || _satIntCapacity < totalNeeded)
        {
            _satIntCapacity = totalNeeded;
            _satBufferInt = new int[_satIntCapacity];
        }
        else Array.Clear(_satBufferInt, 0, totalNeeded);

        int[] localSatBuffer = _satBufferInt;

        // ★ 关键：用 GCHandle 固定快照数组，获取裸指针，避免 fixed + lambda 冲突
        var snapHandle = System.Runtime.InteropServices.GCHandle.Alloc(
            _blurSourceSnapshot, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            byte* snapBase = (byte*)snapHandle.AddrOfPinnedObject();
            int snapStride = _blurSnapshotStride;

            // 第一步：并行计算行前缀和（从快照读取）
            Parallel.For(0, roiH, (iy) =>
            {
                byte* srcRow = snapBase + (long)(roiY + iy) * snapStride + roiX * 4;
                fixed (int* sat = localSatBuffer)
                {
                    int* curRow = sat + (iy + 1) * satStride4;
                    int prefB = 0, prefG = 0, prefR = 0, prefA = 0;

                    for (int ix = 0; ix < roiW; ix++)
                    {
                        byte* px = srcRow + ix * 4;
                        prefB += px[0]; prefG += px[1]; prefR += px[2]; prefA += px[3];
                        int idx = (ix + 1) * 4;
                        curRow[idx + 0] = prefB;
                        curRow[idx + 1] = prefG;
                        curRow[idx + 2] = prefR;
                        curRow[idx + 3] = prefA;
                    }
                }
            });

            // 第二步：并行计算列前缀和
            Parallel.For(1, roiW + 1, (ix) =>
            {
                int idx4 = ix * 4;
                fixed (int* sat = localSatBuffer)
                {
                    for (int iy = 1; iy < roiH; iy++)
                    {
                        int* curRow = sat + (iy + 1) * satStride4 + idx4;
                        int* prevRow = sat + iy * satStride4 + idx4;
                        curRow[0] += prevRow[0];
                        curRow[1] += prevRow[1];
                        curRow[2] += prevRow[2];
                        curRow[3] += prevRow[3];
                    }
                }
            });
        }
        finally
        {
            snapHandle.Free();
        }

        // 第三步：应用模糊（写入 basePtr，与之前相同）
        fixed (int* sat = localSatBuffer)
        {
            int paintXMin = Math.Max(0, cx - r);
            int paintYMin = Math.Max(0, cy - r);
            int paintXMax = Math.Min(w, cx + r);
            int paintYMax = Math.Min(h, cy + r);
            int rSq = r * r;

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

        if (rowCount >= AppConsts.BlurParallelThreshold)
            {
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
        }
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

        int maxDx = (int)Math.Sqrt(rSq - dySq);
        int rowXMin = Math.Max(paintXMin, cx - maxDx);
        int rowXMax = Math.Min(paintXMax, cx + maxDx + 1);

        byte* dstRow = basePtr + (long)py * stride;
        int rowIdx = py * imgW;
        int localY = py - roiY;

        for (int px = rowXMin; px < rowXMax; px++)
        {
            int pixelIndex = rowIdx + px;
            if (strokeMask[pixelIndex] >= 255) continue;

            int dx = px - cx;
            if (dx * dx + dySq > rSq) continue;

            strokeMask[pixelIndex] = 255;

            int localX = px - roiX;
            int boxX1 = Math.Max(0, localX - kernelRadius);
            int boxY1 = Math.Max(0, localY - kernelRadius);
            int boxX2 = Math.Min(roiW, localX + kernelRadius + 1);
            int boxY2 = Math.Min(roiH, localY + kernelRadius + 1);

            int area = (boxX2 - boxX1) * (boxY2 - boxY1);
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
        float baseThickness = (float)ctx.PenThickness;
        float minScale = 0.2f;
        float r1 = MathF.Max(0.5f, (baseThickness * (minScale + (1.0f - minScale) * p1P)) * 0.5f);
        float r2 = MathF.Max(0.5f, (baseThickness * (minScale + (1.0f - minScale) * p2P)) * 0.5f);

        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        if (globalOpacity <= 0.005f) return;

        Color targetColor = ctx.PenColor;
        float targetB = targetColor.B, targetG = targetColor.G, targetR = targetColor.R, targetA = targetColor.A;
        float alpha1 = globalOpacity * (0.2f + 0.8f * MathF.Sqrt(p1P));
        float alpha2 = globalOpacity * (0.2f + 0.8f * MathF.Sqrt(p2P));

        float maxR = MathF.Max(r1, r2);
        float feather = MathF.Max(1.0f, maxR * 0.08f);

        int xmin = Math.Max(0, (int)(MathF.Min((float)p1.X, (float)p2.X) - maxR - feather - 1));
        int ymin = Math.Max(0, (int)(MathF.Min((float)p1.Y, (float)p2.Y) - maxR - feather - 1));
        int xmax = Math.Min(w - 1, (int)(MathF.Max((float)p1.X, (float)p2.X) + maxR + feather + 1));
        int ymax = Math.Min(h - 1, (int)(MathF.Max((float)p1.Y, (float)p2.Y) + maxR + feather + 1));

        float dx = (float)(p2.X - p1.X), dy = (float)(p2.Y - p1.Y);
        float lenSq = dx * dx + dy * dy;
        float invLenSq = lenSq > 1e-6f ? 1.0f / lenSq : 0;

        Parallel.For(ymin, ymax + 1, (y) =>
        {
            byte* rowPtr = basePtr + (long)y * stride;
            int rowIdx = y * w;
            float pyDy = (float)(y - p1.Y);

            for (int x = xmin; x <= xmax; x++)
            {
                float pxDx = (float)(x - p1.X);
                float t = lenSq > 1e-6f ? Math.Clamp((pxDx * dx + pyDy * dy) * invLenSq, 0, 1) : 0;

                float currentR = r1 + (r2 - r1) * t;
                float projx = (float)p1.X + t * dx, projy = (float)p1.Y + t * dy;
                float dX = x - projx, dY = y - projy;
                float distSq = dX * dX + dY * dY;

                float outerR = currentR + feather;
                float outerRSq = outerR * outerR;
                if (distSq > outerRSq) continue;

                float innerR = MathF.Max(0, currentR - feather);
                float innerRSq = innerR * innerR;
                float edgeFactor;
                if (distSq <= innerRSq)
                {
                    edgeFactor = 1.0f;
                }
                else
                {
                    float dist = MathF.Sqrt(distSq);
                    edgeFactor = (outerR - dist) / (outerR - innerR);
                    edgeFactor = edgeFactor * edgeFactor * (3.0f - 2.0f * edgeFactor);
                }

                if (edgeFactor <= 0.002f) continue;

                float dynamicOpacity = alpha1 + (alpha2 - alpha1) * t;
                float desiredOpacity = dynamicOpacity * edgeFactor;
                byte desiredLevel = (byte)(desiredOpacity * 255.0f);
                if (desiredLevel == 0) continue;

                int pixelIndex = rowIdx + x;
                byte currentLevel = _currentStrokeMask[pixelIndex];
                if (currentLevel >= desiredLevel) continue;

                float currentOpacity = currentLevel * 0.00392157f; // 1/255
                float delta = desiredOpacity - currentOpacity;
                _currentStrokeMask[pixelIndex] = desiredLevel;

                if (delta <= 0.001f) continue;
                float invOneMinusCurrent = 1.0f / (MathF.Max(0.001f, 1.0f - currentOpacity));
                float blend = MathF.Min(delta * invOneMinusCurrent, 1.0f);

                byte* bp = rowPtr + (long)x * 4;
                bp[0] = (byte)(bp[0] + (targetB - bp[0]) * blend);
                bp[1] = (byte)(bp[1] + (targetG - bp[1]) * blend);
                bp[2] = (byte)(bp[2] + (targetR - bp[2]) * blend);
                bp[3] = (byte)(bp[3] + (targetA - bp[3]) * blend);
            }
        });
    }

    private unsafe void DrawPencilLineUnsafe(ToolContext ctx, Point p1, float p1P, Point p2, float p2P, byte* basePtr, int stride, int w, int h)
    {
        int x0 = (int)p1.X; int y0 = (int)p1.Y;
        int x1 = (int)p2.X; int y1 = (int)p2.Y;

        Color targetColor = ctx.PenColor;
        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        if (globalOpacity <= 0.005f) return;

        float tB = targetColor.B, tG = targetColor.G, tR = targetColor.R, tA = targetColor.A;

        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy, e2;
        float totalDist = MathF.Sqrt((float)dx * dx + (float)dy * dy);
        if (totalDist == 0) totalDist = 1;
        int startX = x0, startY = y0;

        while (true)
        {
            if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h)
            {
                int pixelIndex = y0 * w + x0;
                if (_currentStrokeMask[pixelIndex] < 255)
                {
                    _currentStrokeMask[pixelIndex] = 255;
                    float dX = x0 - startX, dY = y0 - startY;
                    float currentDist = MathF.Sqrt(dX * dX + dY * dY);
                    float t = currentDist / totalDist;
                    float currentPressure = p1P + (p2P - p1P) * t;
                    float localOpacity = globalOpacity * Math.Clamp(currentPressure, 0.1f, 1.0f);

                    byte* p = basePtr + (long)y0 * stride + (long)x0 * 4;
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
        float dx = (float)(p2.X - p1.X);
        float dy = (float)(p2.Y - p1.Y);
        float lenSq = dx * dx + dy * dy;
        float invLenSq = lenSq > 0 ? 1.0f / lenSq : 0;

        int xmin = Math.Max(0, (int)(Math.Min(p1.X, p2.X) - r - 1));
        int ymin = Math.Max(0, (int)(Math.Min(p1.Y, p2.Y) - r - 1));
        int xmax = Math.Min(w - 1, (int)(Math.Max(p1.X, p2.X) + r + 1));
        int ymax = Math.Min(h - 1, (int)(Math.Max(p1.Y, p2.Y) + r + 1));

        float rSq = r * r;
        Parallel.For(ymin, ymax + 1, (y) =>
        {
            byte* rowPtr = basePtr + (long)y * stride;
            int rowIdx = y * w;
            float dyVal = (float)(y - p1.Y);

            for (int x = xmin; x <= xmax; x++)
            {
                float t = 0;
                if (lenSq > 0)
                {
                    t = Math.Clamp(((float)(x - p1.X) * dx + dyVal * dy) * invLenSq, 0, 1);
                }

                float projX = (float)p1.X + t * dx;
                float projY = (float)p1.Y + t * dy;
                float distX = x - projX;
                float distY = y - projY;
                float distSq = distX * distX + distY * distY;

                if (distSq > rSq) continue;

                int pixelIndex = rowIdx + x;
                if (_currentStrokeMask[pixelIndex] >= 255) continue;
                
                uint hash = (uint)(x * 73856093 ^ y * 19349663);
                hash = (hash ^ (hash >> 16)) * 0x45d9f3b;
                hash = hash ^ (hash >> 16);
                float noise = (hash & 0xFF) / 255.0f;
                float density = 1.0f - (distSq / rSq) * 0.7f; 
                if (noise > density) continue;

                _currentStrokeMask[pixelIndex] = 255;
                byte* bp = rowPtr + x * 4;
                bp[0] = (byte)(bp[0] + (tB - bp[0]) * globalOpacity);
                bp[1] = (byte)(bp[1] + (tG - bp[1]) * globalOpacity);
                bp[2] = (byte)(bp[2] + (tR - bp[2]) * globalOpacity);
                bp[3] = (byte)(bp[3] + (tA - bp[3]) * globalOpacity);
            }
        });
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
        int radius = (int)(ctx.PenThickness / 2.0);
        if (radius < 1) radius = 1;
        float radiusSq = radius * radius;
        int blockSize = Math.Max(4, (int)(ctx.PenThickness / 4.0));

        int aabbLeft = Math.Max(0, (int)(Math.Min(p1.X, p2.X) - radius));
        int aabbTop = Math.Max(0, (int)(Math.Min(p1.Y, p2.Y) - radius));
        int aabbRight = Math.Min(w, (int)(Math.Max(p1.X, p2.X) + radius));
        int aabbBottom = Math.Min(h, (int)(Math.Max(p1.Y, p2.Y) + radius));

        if (aabbLeft >= aabbRight || aabbTop >= aabbBottom) return;

        float segDx = (float)(p2.X - p1.X);
        float segDy = (float)(p2.Y - p1.Y);
        float segLenSq = segDx * segDx + segDy * segDy;
        float invSegLenSq = segLenSq > 0 ? 1.0f / segLenSq : 0;
        int gridXStart = (aabbLeft / blockSize) * blockSize;
        int gridYStart = (aabbTop / blockSize) * blockSize;

        // 使用 Parallel 优化块循环
        Parallel.For(0, (aabbBottom - gridYStart + blockSize - 1) / blockSize, (i) =>
        {
            int by = gridYStart + i * blockSize;
            for (int bx = gridXStart; bx < aabbRight; bx += blockSize)
            {
                float blockCenterX = bx + blockSize * 0.5f;
                float blockCenterY = by + blockSize * 0.5f;
                float t = 0;
                if (segLenSq > 0)
                {
                    t = Math.Clamp(((blockCenterX - (float)p1.X) * segDx + (blockCenterY - (float)p1.Y) * segDy) * invSegLenSq, 0, 1);
                }
                float closestX = (float)p1.X + t * segDx;
                float closestY = (float)p1.Y + t * segDy;
                float dcx = blockCenterX - closestX;
                float dcy = blockCenterY - closestY;
                float centerDistSq = dcx * dcx + dcy * dcy;

                float blockHalfDiag = blockSize * 0.7071f;
                float thresholdOuter = radius + blockHalfDiag;
                if (centerDistSq > thresholdOuter * thresholdOuter) continue;

                float thresholdInner = Math.Max(0, radius - blockHalfDiag);
                bool isFullyInside = centerDistSq <= thresholdInner * thresholdInner;

                int sampleX = Math.Clamp(bx, 0, w - 1);
                int sampleY = Math.Clamp(by, 0, h - 1);
                byte* srcPixel = basePtr + (long)sampleY * stride + sampleX * 4;
                byte mB = srcPixel[0], mG = srcPixel[1], mR = srcPixel[2];

                int fillStartX = Math.Max(bx, aabbLeft);
                int fillEndX = Math.Min(bx + blockSize, aabbRight);
                int fillStartY = Math.Max(by, aabbTop);
                int fillEndY = Math.Min(by + blockSize, aabbBottom);

                for (int y = fillStartY; y < fillEndY; y++)
                {
                    byte* rowPtr = basePtr + (long)y * stride;
                    int rowIdx = y * w;
                    float dyVal = (float)(y - p1.Y);

                    for (int x = fillStartX; x < fillEndX; x++)
                    {
                        int pixelIndex = rowIdx + x;
                        if (_currentStrokeMask[pixelIndex] >= 255) continue;

                        if (isFullyInside)
                        {
                            _currentStrokeMask[pixelIndex] = 255;
                            byte* destPixel = rowPtr + x * 4;
                            destPixel[0] = mB; destPixel[1] = mG; destPixel[2] = mR;
                            continue;
                        }

                        float pt = 0;
                        if (segLenSq > 0)
                        {
                            pt = Math.Clamp(((x - (float)p1.X) * segDx + dyVal * segDy) * invSegLenSq, 0, 1);
                        }
                        float pdx = x - ((float)p1.X + pt * segDx);
                        float pdy = y - ((float)p1.Y + pt * segDy);

                        if (pdx * pdx + pdy * pdy <= radiusSq)
                        {
                            _currentStrokeMask[pixelIndex] = 255;
                            byte* destPixel = rowPtr + x * 4;
                            destPixel[0] = mB; destPixel[1] = mG; destPixel[2] = mR;
                        }
                    }
                }
            }
        });
    }


    private unsafe void DrawWatercolorStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        float radius = (float)ctx.PenThickness * 0.5f;
        if (radius < 0.5f) radius = 0.5f;
        Color targetColor = ctx.PenColor;
        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        if (globalOpacity <= 0.005f) return;
        float baseOpacity = globalOpacity * 0.5f;
        float tB = targetColor.B, tG = targetColor.G, tR = targetColor.R, tA = targetColor.A;

        float irregularRadius = radius * (0.9f + (float)_rnd.Value.NextDouble() * 0.2f);
        float irregularRadiusSq = irregularRadius * irregularRadius;

        int x_start = Math.Max(0, (int)(p.X - irregularRadius - 1));
        int x_end = Math.Min(w, (int)(p.X + irregularRadius + 1));
        int y_start = Math.Max(0, (int)(p.Y - irregularRadius - 1));
        int y_end = Math.Min(h, (int)(p.Y + irregularRadius + 1));

        float pX = (float)p.X, pY = (float)p.Y;
        float invIrregularRadiusSq = 1.0f / irregularRadiusSq;

        // 对水彩画笔应用并行化
        Parallel.For(y_start, y_end, (y) =>
        {
            byte* rowPtr = basePtr + (long)y * stride;
            float dyVal = y - pY;
            float dySq = dyVal * dyVal;

            for (int x = x_start; x < x_end; x++)
            {
                float dxVal = x - pX;
                float distSq = dxVal * dxVal + dySq;
                if (distSq >= irregularRadiusSq) continue;

                float normalizedDistSq = distSq * invIrregularRadiusSq;
                float falloff = 1.0f - normalizedDistSq;
                float localOpacity = baseOpacity * falloff * falloff;

                if (localOpacity > 0.001f)
                {
                    byte* pPx = rowPtr + (long)x * 4;
                    pPx[0] = (byte)(pPx[0] + (tB - pPx[0]) * localOpacity);
                    pPx[1] = (byte)(pPx[1] + (tG - pPx[1]) * localOpacity);
                    pPx[2] = (byte)(pPx[2] + (tR - pPx[2]) * localOpacity);
                    pPx[3] = (byte)(pPx[3] + (tA - pPx[3]) * localOpacity);
                }
            }
        });
    }
    private unsafe void DrawOilPaintStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        double globalOpacity = TabPaint.SettingsManager.Instance.Current.PenOpacity;
        float baseAlpha = (float)(0.2f * 255.0f / MathF.Max(1.0f, MathF.Sqrt((float)ctx.PenThickness))) * (float)globalOpacity;
        int alpha = (int)baseAlpha;
        if (alpha == 0) return;

        int radius = (int)(ctx.PenThickness * 0.5);
        if (radius < 1) radius = 1;
        Color baseColor = ctx.PenColor;
        int x_center = (int)p.X; int y_center = (int)p.Y;
        int numClumps = radius / 2 + 5;

        // 对油画笔应用并行化处理每一个 Clump (团块)
        Parallel.For(0, numClumps, (i) =>
        {
            var r = _rnd.Value;
            int brightnessOffset = r.Next(-AppConsts.OilPaintBrightnessVariation, AppConsts.OilPaintBrightnessVariation + 1);
            byte clumpR = ClampColor(baseColor.R + brightnessOffset);
            byte clumpG = ClampColor(baseColor.G + brightnessOffset);
            byte clumpB = ClampColor(baseColor.B + brightnessOffset);

            double angle = r.NextDouble() * 2 * Math.PI;
            double distFromCenter = MathF.Sqrt((float)r.NextDouble()) * radius;
            int clumpCenterX = x_center + (int)(distFromCenter * Math.Cos(angle));
            int clumpCenterY = y_center + (int)(distFromCenter * Math.Sin(angle));
            int clumpRadius = r.Next(1, radius / 4 + 3);
            int clumpRadiusSq = clumpRadius * clumpRadius;

            int startX = Math.Max(0, clumpCenterX - clumpRadius);
            int endX = Math.Min(w, clumpCenterX + clumpRadius);
            int startY = Math.Max(0, clumpCenterY - clumpRadius);
            int endY = Math.Min(h, clumpCenterY + clumpRadius);

            int invAlpha = 255 - alpha;

            for (int y = startY; y < endY; y++)
            {
                byte* rowPtr = basePtr + (long)y * stride;
                int dySq = (y - clumpCenterY) * (y - clumpCenterY);
                for (int x = startX; x < endX; x++)
                {
                    int dx = x - clumpCenterX;
                    if (dx * dx + dySq < clumpRadiusSq)
                    {
                        byte* pixelPtr = rowPtr + (long)x * 4;
                        // 快速混合 (Div255 优化)
                        pixelPtr[0] = Div255(clumpB * alpha + pixelPtr[0] * invAlpha);
                        pixelPtr[1] = Div255(clumpG * alpha + pixelPtr[1] * invAlpha);
                        pixelPtr[2] = Div255(clumpR * alpha + pixelPtr[2] * invAlpha);
                    }
                }
            }
        });
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Div255(int v) { return (byte)((v * 257 + 257) >> 16); }
    private unsafe void DrawHighlighterLineUnsafe(ToolContext ctx, Point p1, Point p2, byte* basePtr, int stride, int w, int h)
    {
        int r = (int)(ctx.PenThickness / 2.0);
        if (r < 1) r = 1;
        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        byte baseAlpha = 30;
        Color c = Color.FromArgb((byte)(baseAlpha * globalOpacity), 255, 255, 0);
        int xmin = Math.Max(0, (int)Math.Min(p1.X, p2.X) - r);
        int ymin = Math.Max(0, (int)Math.Min(p1.Y, p2.Y) - r);
        int xmax = Math.Min(w - 1, (int)Math.Max(p1.X, p2.X) + r);
        int ymax = Math.Min(h - 1, (int)Math.Max(p1.Y, p2.Y) + r);
        
        float dx = (float)(p2.X - p1.X), dy = (float)(p2.Y - p1.Y);
        float lenSq = dx * dx + dy * dy;
        float invLenSq = lenSq > 0 ? 1.0f / lenSq : 0;
        int invSA = 255 - c.A, cA = c.A, cB = c.B, cG = c.G, cR = c.R;
        float rSq = r * r;

        Parallel.For(ymin, ymax + 1, (y) =>
        {
            int rowStartIndex = y * w;
            byte* rowPtr = basePtr + (long)y * stride;
            float dyVal = (float)(y - p1.Y);

            for (int x = xmin; x <= xmax; x++)
            {
                int pixelIndex = rowStartIndex + x;
                if (_currentStrokeMask[pixelIndex] >= 255) continue;

                float t = 0;
                if (lenSq > 0)
                {
                    t = Math.Clamp(((float)(x - p1.X) * dx + dyVal * dy) * invLenSq, 0, 1);
                }
                float closeX = (float)p1.X + t * dx, closeY = (float)p1.Y + t * dy;
                float dX = x - closeX, dY = y - closeY;
                if (dX * dX + dY * dY <= rSq)
                {
                    _currentStrokeMask[pixelIndex] = 255;
                    byte* p = rowPtr + (long)x * 4;
                    p[0] = Div255(cB * cA + p[0] * invSA);
                    p[1] = Div255(cG * cA + p[1] * invSA);
                    p[2] = Div255(cR * cA + p[2] * invSA);
                }
            }
        });
    }

    /// <summary>
    /// 将一段线段细分为多个子段，每段的压力线性插值，
    /// 确保粗细过渡平滑，不会出现突变。
    /// </summary>
    private unsafe Int32Rect? DrawCalligraphySegmentSubdivided(
        ToolContext ctx, Point from, float fromP, Point to, float toP,
        byte* buffer, int stride, int w, int h)
    {
        float dx = (float)(to.X - from.X);
        float dy = (float)(to.Y - from.Y);
        float length = MathF.Sqrt(dx * dx + dy * dy);

        if (length < 0.5f)
        {
            return DrawBrushLineUnsafe(ctx, from, fromP, to, toP, buffer, stride, w, h);
        }

        // 细分步长：每 2~3 像素一个子段，确保粗细变化平滑
        float baseThickness = (float)ctx.PenThickness;
        float maxRadiusChange = baseThickness * 0.15f; // 每子段最大半径变化

        // 根据压力差计算需要的最小细分数
        float radiusDiff = MathF.Abs(toP - fromP) * baseThickness * 0.5f;
        int pressureSteps = radiusDiff > 0.1f ? (int)MathF.Ceiling(radiusDiff / maxRadiusChange) : 1;

        // 根据距离计算细分数（每 2 像素一段）
        int distanceSteps = (int)MathF.Ceiling(length / 2.0f);

        int steps = Math.Max(1, Math.Max(pressureSteps, distanceSteps));
        // 上限防止极端情况
        steps = Math.Min(steps, 500);

        float stepX = dx / steps;
        float stepY = dy / steps;
        float stepP = (toP - fromP) / steps;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        bool hit = false;

        float curX = (float)from.X;
        float curY = (float)from.Y;
        float curP = fromP;

        for (int i = 0; i < steps; i++)
        {
            float nextX = curX + stepX;
            float nextY = curY + stepY;
            float nextP = curP + stepP;

            var rect = DrawRoundStrokeUnsafe_Internal(
                ctx,
                new Point(curX, curY), curP,
                new Point(nextX, nextY), nextP,
                buffer, stride, w, h);

            if (rect.HasValue)
            {
                hit = true;
                if (rect.Value.X < minX) minX = rect.Value.X;
                if (rect.Value.Y < minY) minY = rect.Value.Y;
                int rx = rect.Value.X + rect.Value.Width;
                int ry = rect.Value.Y + rect.Value.Height;
                if (rx > maxX) maxX = rx;
                if (ry > maxY) maxY = ry;
            }

            curX = nextX;
            curY = nextY;
            curP = nextP;
        }

        if (!hit) return null;
        return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// 内部版本，返回 Int32Rect 用于脏矩形合并。
    /// 针对书写笔优化了边缘抗锯齿和压力响应曲线。
    /// </summary>
    private unsafe Int32Rect? DrawRoundStrokeUnsafe_Internal(
        ToolContext ctx, Point p1, float p1P,
        Point p2, float p2P, byte* basePtr, int stride, int w, int h)
    {
        float baseThickness = (float)ctx.PenThickness;

        // ★ 书写笔的压力→半径映射：使用更好的曲线
        // 让细笔画不会太细（保持可读性），粗笔画有明显对比
        float minScale = 0.12f;  // 最细时为最粗的 12%
        float r1 = MathF.Max(0.5f, (baseThickness * (minScale + (1.0f - minScale) * EaseInOutPressure(p1P))) * 0.5f);
        float r2 = MathF.Max(0.5f, (baseThickness * (minScale + (1.0f - minScale) * EaseInOutPressure(p2P))) * 0.5f);

        float globalOpacity = (float)TabPaint.SettingsManager.Instance.Current.PenOpacity;
        if (globalOpacity <= 0.005f) return null;

        Color targetColor = ctx.PenColor;
        float targetB = targetColor.B, targetG = targetColor.G;
        float targetR = targetColor.R, targetA = targetColor.A;

        // 书写笔不需要压力影响透明度，保持墨色浓郁
        float alpha1 = globalOpacity;
        float alpha2 = globalOpacity;

        float maxR = MathF.Max(r1, r2);
        // ★ 更好的抗锯齿羽化：至少 1px，但不超过半径的 15%
        float feather = MathF.Max(1.0f, MathF.Min(maxR * 0.15f, 2.0f));

        int xmin = Math.Max(0, (int)(MathF.Min((float)p1.X, (float)p2.X) - maxR - feather - 1));
        int ymin = Math.Max(0, (int)(MathF.Min((float)p1.Y, (float)p2.Y) - maxR - feather - 1));
        int xmax = Math.Min(w - 1, (int)(MathF.Max((float)p1.X, (float)p2.X) + maxR + feather + 1));
        int ymax = Math.Min(h - 1, (int)(MathF.Max((float)p1.Y, (float)p2.Y) + maxR + feather + 1));

        float dx = (float)(p2.X - p1.X), dy = (float)(p2.Y - p1.Y);
        float lenSq = dx * dx + dy * dy;
        float invLenSq = lenSq > 1e-6f ? 1.0f / lenSq : 0;

        Parallel.For(ymin, ymax + 1, (y) =>
        {
            byte* rowPtr = basePtr + (long)y * stride;
            int rowIdx = y * w;
            float pyDy = (float)(y - p1.Y);

            for (int x = xmin; x <= xmax; x++)
            {
                float pxDx = (float)(x - p1.X);
                float t = lenSq > 1e-6f
                    ? Math.Clamp((pxDx * dx + pyDy * dy) * invLenSq, 0, 1) : 0;

                // ★ 半径沿线段平滑插值
                float currentR = r1 + (r2 - r1) * t;
                float projx = (float)p1.X + t * dx;
                float projy = (float)p1.Y + t * dy;
                float dX = x - projx, dY = y - projy;
                float distSq = dX * dX + dY * dY;

                float outerR = currentR + feather;
                if (distSq > outerR * outerR) continue;

                float innerR = MathF.Max(0, currentR - feather);
                float edgeFactor;
                if (distSq <= innerR * innerR)
                {
                    edgeFactor = 1.0f;
                }
                else
                {
                    float dist = MathF.Sqrt(distSq);
                    edgeFactor = (outerR - dist) / (outerR - innerR);
                    // Smoothstep 抗锯齿
                    edgeFactor = edgeFactor * edgeFactor * (3.0f - 2.0f * edgeFactor);
                }

                if (edgeFactor <= 0.002f) continue;

                float desiredOpacity = globalOpacity * edgeFactor;
                byte desiredLevel = (byte)(desiredOpacity * 255.0f);
                if (desiredLevel == 0) continue;

                int pixelIndex = rowIdx + x;
                byte currentLevel = _currentStrokeMask[pixelIndex];
                if (currentLevel >= desiredLevel) continue;

                float currentOpacity = currentLevel * (1f / 255f);
                float delta = desiredOpacity - currentOpacity;
                _currentStrokeMask[pixelIndex] = desiredLevel;

                if (delta <= 0.001f) continue;
                float blend = MathF.Min(delta / MathF.Max(0.001f, 1.0f - currentOpacity), 1.0f);

                byte* bp = rowPtr + (long)x * 4;
                bp[0] = (byte)(bp[0] + (targetB - bp[0]) * blend);
                bp[1] = (byte)(bp[1] + (targetG - bp[1]) * blend);
                bp[2] = (byte)(bp[2] + (targetR - bp[2]) * blend);
                bp[3] = (byte)(bp[3] + (targetA - bp[3]) * blend);
            }
        });

        return new Int32Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1);
    }

    /// <summary>
    /// 压力的 ease-in-out 曲线，让粗细变化更有"弹性"
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float EaseInOutPressure(float p)
    {
        // Hermite 插值：在 0 和 1 附近变化缓慢，中间变化快
        // 这让细笔画和粗笔画都更稳定，中间过渡更明显
        p = Math.Clamp(p, 0f, 1f);
        return p * p * (3f - 2f * p);
    }

    private unsafe Int32Rect? DrawCatmullRomSegment(
        ToolContext ctx,
        CalligraphyPoint p0, CalligraphyPoint p1,
        CalligraphyPoint p2, CalligraphyPoint p3,
        byte* buffer, int stride, int w, int h)
    {
        // p1→p2 之间的距离决定细分数
        float segDx = p2.X - p1.X;
        float segDy = p2.Y - p1.Y;
        float segLen = MathF.Sqrt(segDx * segDx + segDy * segDy);
        int steps = Math.Max(1, (int)MathF.Ceiling(segLen / 1.5f));
        steps = Math.Min(steps, 300);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        bool hit = false;

        float prevX = p1.X, prevY = p1.Y, prevP = p1.Pressure;

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            float t2 = t * t;
            float t3 = t2 * t;

            // Catmull-Rom 系数
            float c0 = -0.5f * t3 + t2 - 0.5f * t;
            float c1 = 1.5f * t3 - 2.5f * t2 + 1.0f;
            float c2 = -1.5f * t3 + 2.0f * t2 + 0.5f * t;
            float c3 = 0.5f * t3 - 0.5f * t2;

            float curX = c0 * p0.X + c1 * p1.X + c2 * p2.X + c3 * p3.X;
            float curY = c0 * p0.Y + c1 * p1.Y + c2 * p2.Y + c3 * p3.Y;
            float curP = c0 * p0.Pressure + c1 * p1.Pressure + c2 * p2.Pressure + c3 * p3.Pressure;
            curP = Math.Clamp(curP, 0.1f, 1.0f);

            var rect = DrawRoundStrokeUnsafe_Internal(
                ctx,
                new Point(prevX, prevY), prevP,
                new Point(curX, curY), curP,
                buffer, stride, w, h);

            if (rect.HasValue)
            {
                hit = true;
                if (rect.Value.X < minX) minX = rect.Value.X;
                if (rect.Value.Y < minY) minY = rect.Value.Y;
                int rx = rect.Value.X + rect.Value.Width;
                int ry = rect.Value.Y + rect.Value.Height;
                if (rx > maxX) maxX = rx;
                if (ry > maxY) maxY = ry;
            }

            prevX = curX; prevY = curY; prevP = curP;
        }

        if (!hit) return null;
        return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
    }
}
