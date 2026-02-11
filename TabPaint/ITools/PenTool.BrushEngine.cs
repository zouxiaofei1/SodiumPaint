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
                float op = (float)ctx.PenOpacity;
                int kRad = 1 + (int)(op * 49);
                return LineBounds(px, px, (int)ctx.PenThickness + kRad * 2 + 5);
        }
        return null;
    }
    private int[] _satBufferInt;      // 积分图改用 int（ROI 尺寸有限，不会溢出）
    private int _satIntCapacity;
    private byte[] _blurSourceSnapshot;// 模糊画笔专用：原始图像快照
    private int _blurSnapshotStride;




    private unsafe void DrawRoundStrokeUnsafe(ToolContext ctx, Point p1, float p1P,
     Point p2, float p2P, byte* basePtr, int stride, int w, int h)
    {
        float baseThickness = (float)ctx.PenThickness;
        float minScale = 0.2f;
        float r1 = MathF.Max(0.5f, (baseThickness * (minScale + (1.0f - minScale) * p1P)) * 0.5f);
        float r2 = MathF.Max(0.5f, (baseThickness * (minScale + (1.0f - minScale) * p2P)) * 0.5f);

        float globalOpacity = (float)ctx.PenOpacity;
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
        float globalOpacity = (float)ctx.PenOpacity;
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

        float globalOpacity = (float)ctx.PenOpacity;
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
        float globalOpacity = (float)ctx.PenOpacity;
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

      private static byte Div255(int v) { return (byte)((v * 257 + 257) >> 16); }

    private unsafe Int32Rect? DrawRoundStrokeUnsafe_Internal(
        ToolContext ctx, Point p1, float p1P,
        Point p2, float p2P, byte* basePtr, int stride, int w, int h)
    {
        float baseThickness = (float)ctx.PenThickness;
        float minScale = 0.12f;  // 最细时为最粗的 12%
        float r1 = MathF.Max(0.5f, (baseThickness * (minScale + (1.0f - minScale) * EaseInOutPressure(p1P))) * 0.5f);
        float r2 = MathF.Max(0.5f, (baseThickness * (minScale + (1.0f - minScale) * EaseInOutPressure(p2P))) * 0.5f);

        float globalOpacity = (float)ctx.PenOpacity;
        if (globalOpacity <= 0.005f) return null;

        Color targetColor = ctx.PenColor;
        float targetB = targetColor.B, targetG = targetColor.G;
        float targetR = targetColor.R, targetA = targetColor.A;

        // 书写笔不需要压力影响透明度，保持墨色浓郁
        float alpha1 = globalOpacity;
        float alpha2 = globalOpacity;

        float maxR = MathF.Max(r1, r2);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float EaseInOutPressure(float p)
    {
        p = Math.Clamp(p, 0f, 1f);
        return p * p * (3f - 2f * p);
    }


}
