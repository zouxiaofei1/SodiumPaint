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

        // 1. 准备参数
        double r = ctx.PenThickness / 2.0;
        double rSq = r * r;
        int xmin = (int)(Math.Min(p1.X, p2.X) - r - 1);
        int ymin = (int)(Math.Min(p1.Y, p2.Y) - r - 1);
        int xmax = (int)(Math.Max(p1.X, p2.X) + r + 1);
        int ymax = (int)(Math.Max(p1.Y, p2.Y) + r + 1);

        _maskBitmap.Lock();
        unsafe
        {
            int w = _maskBitmap.PixelWidth;
            int h = _maskBitmap.PixelHeight;
            int stride = _maskBitmap.BackBufferStride;
            byte* basePtr = (byte*)_maskBitmap.BackBuffer;

            // 3. 边界安全裁剪，防止越界
            xmin = Math.Max(0, xmin);
            ymin = Math.Max(0, ymin);
            xmax = Math.Min(w - 1, xmax);
            ymax = Math.Min(h - 1, ymax);

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double lenSq = dx * dx + dy * dy;
            for (int y = ymin; y <= ymax; y++)
            {
                byte* rowPtr = basePtr + y * stride;
                for (int x = xmin; x <= xmax; x++)
                {
                    double t = 0;
                    if (lenSq > 0.001) // 避免除以0
                    {
                        t = ((x - p1.X) * dx + (y - p1.Y) * dy) / lenSq;
                        t = t < 0 ? 0 : (t > 1 ? 1 : t);
                    }
                    double closestX = p1.X + t * dx;
                    double closestY = p1.Y + t * dy;

                    double distSq = (x - closestX) * (x - closestX) + (y - closestY) * (y - closestY);
                    if (distSq <= rSq)
                    {
                        byte* p = rowPtr + x * 4;
                        if (p[2] != 255)
                        {
                            p[0] = 0;   // B
                            p[1] = 0;   // G
                            p[2] = 255; // R
                            p[3] = 255; // A
                        }
                    }
                }
            }
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
        var aiService = new AiService(mw._cacheDir);

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