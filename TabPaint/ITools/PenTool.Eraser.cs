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
    private void DrawMaskLine(ToolContext ctx, Point p1, Point p2, float pressure)
    {
        if (_maskBitmap == null) return;

        // 1. 准备参数 (使用 float 加速计算)
        float r = (float)ctx.PenThickness * 0.5f;
        float rSq = r * r;
        int xmin = (int)(MathF.Min((float)p1.X, (float)p2.X) - r - 1);
        int ymin = (int)(MathF.Min((float)p1.Y, (float)p2.Y) - r - 1);
        int xmax = (int)(MathF.Max((float)p1.X, (float)p2.X) + r + 1);
        int ymax = (int)(MathF.Max((float)p1.Y, (float)p2.Y) + r + 1);

        _maskBitmap.Lock();
        unsafe
        {
            int w = _maskBitmap.PixelWidth;
            int h = _maskBitmap.PixelHeight;
            int stride = _maskBitmap.BackBufferStride;
            byte* basePtr = (byte*)_maskBitmap.BackBuffer;

            // 3. 边界安全裁剪
            xmin = Math.Max(0, xmin);
            ymin = Math.Max(0, ymin);
            xmax = Math.Min(w - 1, xmax);
            ymax = Math.Min(h - 1, ymax);

            float dx = (float)(p2.X - p1.X);
            float dy = (float)(p2.Y - p1.Y);
            float lenSq = dx * dx + dy * dy;
            float invLenSq = lenSq > 1e-6f ? 1.0f / lenSq : 0;

            // 使用 Parallel.For 并行化
            Parallel.For(ymin, ymax + 1, (y) =>
            {
                byte* rowPtr = basePtr + (long)y * stride;
                float pyDy = (float)(y - p1.Y);
                
                for (int x = xmin; x <= xmax; x++)
                {
                    float pxDx = (float)(x - p1.X);
                    float t = lenSq > 1e-6f ? Math.Clamp((pxDx * dx + pyDy * dy) * invLenSq, 0, 1) : 0;

                    float closestX = (float)p1.X + t * dx;
                    float closestY = (float)p1.Y + t * dy;

                    float dX = x - closestX;
                    float dY = y - closestY;
                    float distSq = dX * dX + dY * dY;

                    if (distSq <= rSq)
                    {
                        byte* p = rowPtr + (long)x * 4;
                        // 直接写入 BGRA (0, 0, 255, 255)
                        *((uint*)p) = 0xFFFF0000; 
                    }
                }
            });
        }
        if (xmax >= xmin && ymax >= ymin)
        {
            _maskBitmap.AddDirtyRect(new Int32Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1));
        }
        _maskBitmap.Unlock();
    }

    private async void ApplyAiEraser(ToolContext ctx)
    {
        var mw = MainWindow.GetCurrentInstance();
        var aiService = AiService.Instance;

        try
        {
            string modelPath = System.IO.Path.Combine(mw._cacheDir, AppConsts.Inpaint_ModelName);
            string oldStatus = mw.ImageSize;

            // 开始推理阶段
            mw.ImageSize = LocalizationManager.GetString("L_AI_Eraser_Processing");
            mw.ShowToast("L_AI_Eraser_Processing");
            mw.IsEnabled = false; // 锁定 UI

            // --- 原有的推理逻辑 ---
            var oldBmp = ctx.Surface.Bitmap;
            int origW = oldBmp.PixelWidth;
            int origH = oldBmp.PixelHeight;
            int targetW = AppConsts.AiInpaintSize;
            int targetH = AppConsts.AiInpaintSize;

            var scaledImg = new TransformedBitmap(oldBmp, new ScaleTransform((double)targetW / origW, (double)targetH / origH));
            var wbImg = new WriteableBitmap(scaledImg);
            byte[] imgBytes = new byte[targetH * wbImg.BackBufferStride];
            wbImg.CopyPixels(imgBytes, wbImg.BackBufferStride, 0);

            var scaledMask = new TransformedBitmap(_maskBitmap, new ScaleTransform((double)targetW / origW, (double)targetH / origH));
            var wbMask = new WriteableBitmap(scaledMask);
            byte[] maskBytes = new byte[targetH * wbMask.BackBufferStride];
            wbMask.CopyPixels(maskBytes, wbMask.BackBufferStride, 0);

            // 执行推理
            byte[] rawResultPixels = await aiService.RunInpaintingAsync(modelPath, imgBytes, maskBytes, origW, origH);

            // 处理并应用结果
            var result512 = new WriteableBitmap(targetW, targetH, AppConsts.StandardDpi, AppConsts.StandardDpi, PixelFormats.Bgra32, null);
            result512.WritePixels(new Int32Rect(0, 0, targetW, targetH), rawResultPixels, targetW * 4, 0);

            var finalScaled = new TransformedBitmap(result512, new ScaleTransform((double)origW / targetW, (double)origH / targetH));
            var finalWb = new WriteableBitmap(finalScaled);

            // 撤销重做逻辑
            var undoRect = new Int32Rect(0, 0, origW, origH);
            byte[] undoPixels = new byte[origH * oldBmp.BackBufferStride];
            oldBmp.CopyPixels(undoPixels, oldBmp.BackBufferStride, 0);
            ctx.Surface.ReplaceBitmap(finalWb);
            byte[] redoPixels = new byte[origH * finalWb.BackBufferStride];
            finalWb.CopyPixels(redoPixels, finalWb.BackBufferStride, 0);
            ctx.Undo.PushTransformAction(undoRect, undoPixels, undoRect, redoPixels);

            mw.NotifyCanvasChanged();
            mw.ShowToast("L_AI_Eraser_Success");
            mw.ImageSize = oldStatus;
        }
        catch (Exception ex)
        {
            string errorFormat = LocalizationManager.GetString("L_AI_Eraser_Error_Prefix");
            mw.ShowToast(string.Format(errorFormat, ex.Message));
        }
        finally
        {
            mw.IsEnabled = true;
            CleanupMask(ctx);
            mw.Focus();
        }
    }


    private void CleanupMask(ToolContext ctx)  // 清理遮罩层
    {
        if (_maskBitmap != null)
        {
            _maskBitmap.Clear(); // 扩展方法: 清空像素
            _maskBitmap = null;
        }
        if (_maskImageOverlay != null)
        {
            ctx.EditorOverlay.Children.Remove(_maskImageOverlay);
            _maskImageOverlay = null;
        }
    }

}