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

        // 2. 计算受影响的矩形范围 (Bounding Box)
        // 这样我们只需要遍历这个矩形内的像素，而不是全图
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

            // 5. 遍历包围盒内的像素
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

                    // 找到线段上距离当前点最近的点
                    double closestX = p1.X + t * dx;
                    double closestY = p1.Y + t * dy;

                    // 计算距离的平方
                    double distSq = (x - closestX) * (x - closestX) + (y - closestY) * (y - closestY);

                    // 6. 如果在半径范围内，填充红色
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

        // 7. 仅更新脏矩形区域，而不是全图
        if (xmax >= xmin && ymax >= ymin)
        {
            _maskBitmap.AddDirtyRect(new Int32Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1));
        }
        _maskBitmap.Unlock();
    }

    private async void ApplyAiEraser(ToolContext ctx)
    {
        var mw = (MainWindow)Application.Current.MainWindow;
        var aiService = new AiService(mw._cacheDir);

        try
        {
            if (!aiService.IsModelReady(AiService.AiTaskType.Inpainting))
            {
                // 弹出确认对话框 (类似 MainWindow 中的逻辑)
                var result = FluentMessageBox.Show(
                    LocalizationManager.GetString("L_AI_Download_Inpaint_Content"), // 需在资源文件添加: "即将下载 AI 修复模型 (约 200MB)，是否继续？"
                    LocalizationManager.GetString("L_AI_Download_Title"),
                    MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    CleanupMask(ctx); // 用户取消，清理遮罩
                    return;
                }
            }

            // 2. 准备下载进度条回调
            // 需确保 mw.ImageSize 的 set 是 public 的，或者 mw 有 UpdateStatus 方法
            string oldStatus = mw.ImageSize;
            mw.ImageSize = LocalizationManager.GetString("L_AI_Status_Preparing");

            var dlProgress = new Progress<double>(p =>
            {
                // 更新主窗口状态栏
                mw.ImageSize = string.Format(LocalizationManager.GetString("L_AI_Status_Downloading_Format"), p);
            });

            // 3. 下载/准备模型 (统一使用 PrepareModelAsync)
            string modelPath = await aiService.PrepareModelAsync(AiService.AiTaskType.Inpainting, dlProgress);

            // 4. 开始推理提示
            mw.ImageSize = LocalizationManager.GetString("L_AI_Eraser_Processing");
            mw.ShowToast(LocalizationManager.GetString("L_AI_Eraser_Processing"));

            // --- 1. 准备数据 (UI线程) ---
            var oldBmp = ctx.Surface.Bitmap;
            int origW = oldBmp.PixelWidth;
            int origH = oldBmp.PixelHeight;
            int targetW = 512;
            int targetH = 512;

            // 缩放原图到 512
            var scaledImg = new TransformedBitmap(oldBmp, new ScaleTransform((double)targetW / origW, (double)targetH / origH));
            var wbImg = new WriteableBitmap(scaledImg);
            byte[] imgBytes = new byte[targetH * wbImg.BackBufferStride];
            wbImg.CopyPixels(imgBytes, wbImg.BackBufferStride, 0);

            // 缩放 Mask 到 512
            var scaledMask = new TransformedBitmap(_maskBitmap, new ScaleTransform((double)targetW / origW, (double)targetH / origH));
            var wbMask = new WriteableBitmap(scaledMask);
            byte[] maskBytes = new byte[targetH * wbMask.BackBufferStride];
            wbMask.CopyPixels(maskBytes, wbMask.BackBufferStride, 0);

           
            byte[] rawResultPixels = await aiService.RunInpaintingAsync(modelPath, imgBytes, maskBytes, origW, origH);

            var result512 = new WriteableBitmap(targetW, targetH, 96, 96, PixelFormats.Bgra32, null);
            result512.WritePixels(new Int32Rect(0, 0, targetW, targetH), rawResultPixels, targetW * 4, 0);

            var finalScaled = new TransformedBitmap(result512, new ScaleTransform((double)origW / targetW, (double)origH / targetH));
            var finalWb = new WriteableBitmap(finalScaled);

            var undoRect = new Int32Rect(0, 0, origW, origH);
            byte[] undoPixels = new byte[origH * oldBmp.BackBufferStride];
            oldBmp.CopyPixels(undoPixels, oldBmp.BackBufferStride, 0);

            ctx.Surface.ReplaceBitmap(finalWb);

            // 记录 Redo
            byte[] redoPixels = new byte[origH * finalWb.BackBufferStride];
            finalWb.CopyPixels(redoPixels, finalWb.BackBufferStride, 0);
            ctx.Undo.PushTransformAction(undoRect, undoPixels, undoRect, redoPixels);

            mw.NotifyCanvasChanged();
            mw.ShowToast(LocalizationManager.GetString("L_AI_Eraser_Success"));
            mw.ImageSize = oldStatus;
        }
        catch (Exception ex)
        {
            string errorFormat = LocalizationManager.GetString("L_AI_Eraser_Error_Prefix");
            mw.ShowToast(string.Format(errorFormat, ex.Message));
        }
        finally
        {
            CleanupMask(ctx);
        }
    }


    // 清理遮罩层
    private void CleanupMask(ToolContext ctx)
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