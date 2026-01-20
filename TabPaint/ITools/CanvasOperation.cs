using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TabPaint.MainWindow;

//
//关于图片的一些操作方法，
//原来被大量放在Mainwindow.cs里
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void ResizeCanvasDimensions(int newWidth, int newHeight)
        {
            var oldBitmap = _surface.Bitmap;
            if (oldBitmap == null) return;
            if (oldBitmap.PixelWidth == newWidth && oldBitmap.PixelHeight == newHeight) return;

            int oldW = oldBitmap.PixelWidth;
            int oldH = oldBitmap.PixelHeight;

            // --- 1. 捕获 Undo 数据 (旧图全貌) ---
            var undoRect = new Int32Rect(0, 0, oldW, oldH);
            // 使用你现有的 ExtractRegion 方法或 CopyPixels
            byte[] undoPixels = new byte[oldH * oldBitmap.BackBufferStride];
            oldBitmap.CopyPixels(undoRect, undoPixels, oldBitmap.BackBufferStride, 0);

            // --- 2. 创建新位图 ---
            var newBitmap = new WriteableBitmap(newWidth, newHeight, oldBitmap.DpiX, oldBitmap.DpiY, PixelFormats.Bgra32, null);

            // --- 3. 填充背景色 (白色) ---
            // 如果不填充，WriteableBitmap 默认为透明。根据你的应用习惯填充白色。
            int newStride = newBitmap.BackBufferStride;
            byte[] whiteBg = new byte[newHeight * newStride];
            for (int i = 0; i < whiteBg.Length; i++) whiteBg[i] = 255; // 简单的全白填充
            newBitmap.WritePixels(new Int32Rect(0, 0, newWidth, newHeight), whiteBg, newStride, 0);

            // --- 4. 计算居中位置 ---
            // 计算旧图在新图中的左上角坐标
            int destX = (newWidth - oldW) / 2;
            int destY = (newHeight - oldH) / 2;

            // --- 5. 计算有效的复制区域 (Intersection) ---
            // 只有当旧图和新图重叠的部分才需要复制
            int srcX = 0;
            int srcY = 0;
            int copyW = oldW;
            int copyH = oldH;

            // 如果新图比旧图小（裁剪），需要调整源起始点和复制大小
            if (destX < 0)
            {
                srcX = -destX;      // 源图左边被裁掉的部分
                copyW = newWidth;   // 复制宽度等于新图宽度
                destX = 0;          // 在新图中从 0 开始贴
            }
            if (destY < 0)
            {
                srcY = -destY;
                copyH = newHeight;
                destY = 0;
            }

            // 确保不越界
            copyW = Math.Min(copyW, oldW - srcX);
            copyH = Math.Min(copyH, oldH - srcY);

            if (copyW > 0 && copyH > 0)
            {
                // 提取旧图中需要保留的部分
                var srcRect = new Int32Rect(srcX, srcY, copyW, copyH);
                byte[] sourcePixels = new byte[copyH * oldBitmap.BackBufferStride]; // 这里的 Stride 还是旧图的
                oldBitmap.CopyPixels(srcRect, sourcePixels, oldBitmap.BackBufferStride, 0);

                // 写入新图的指定位置
                var destRect = new Int32Rect(destX, destY, copyW, copyH);
                newBitmap.WritePixels(destRect, sourcePixels, oldBitmap.BackBufferStride, 0);
            }

            // --- 6. 捕获 Redo 数据 (新图全貌) ---
            var redoRect = new Int32Rect(0, 0, newWidth, newHeight);
            byte[] redoPixels = new byte[newHeight * newBitmap.BackBufferStride];
            newBitmap.CopyPixels(redoRect, redoPixels, newBitmap.BackBufferStride, 0);

            // --- 7. 更新 UI 和状态 ---
            _surface.ReplaceBitmap(newBitmap);
            _bitmap = newBitmap;

            // 记录到撤销栈
            _undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);

            NotifyCanvasSizeChanged(newWidth, newHeight);
            NotifyCanvasChanged();
            SetUndoRedoButtonState();

            // 自动适应窗口或更新滚动条位置
            // EnsureEdgeVisible(new Rect(0, 0, newWidth, newHeight)); // 可选
        }

        private void ApplyTransform(System.Windows.Media.Transform transform)
        {
            if (BackgroundImage.Source is not BitmapSource src || _surface?.Bitmap == null)
                return;

            var undoRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight); // --- 1. 捕获变换前的状态 (for UNDO) ---
            var undoPixels = _surface.ExtractRegion(undoRect);
            if (undoPixels == null) return; // 如果提取失败则中止
            var transformedBmp = new TransformedBitmap(src, transform); // --- 2. 计算并生成变换后的新位图 (这是 REDO 的目标状态) ---
            var newBitmap = new WriteableBitmap(transformedBmp);

            var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);  // --- 3. 捕获变换后的状态 (for REDO) ---
            int redoStride = newBitmap.BackBufferStride;
            var redoPixels = new byte[redoStride * redoRect.Height];
            newBitmap.CopyPixels(redoPixels, redoStride, 0);

            _bitmap = newBitmap;
            BackgroundImage.Source = _bitmap;
            _surface.Attach(_bitmap);
            _surface.ReplaceBitmap(_bitmap);

            _undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);   // --- 5. newBitmap.PixelWidth Undo 栈 ---
            ((MainWindow)System.Windows.Application.Current.MainWindow).NotifyCanvasSizeChanged(newBitmap.PixelWidth, newBitmap.PixelHeight);
            SetUndoRedoButtonState();
        }

        private void RotateBitmap(int angle)
        {
            var mw = (MainWindow)Application.Current.MainWindow;
            // 1. 检查当前工具是否为 SelectTool 且有活动选区
            if (_tools.Select is SelectTool st && st.HasActiveSelection)
            {
                // 调用选区旋转
                st.RotateSelection(_ctx, angle);
                    return; // 结束，不旋转画布
            }
            if (mw._router.CurrentTool is ShapeTool shapetool && mw._router.GetSelectTool()?._selectionData != null)
            {
                mw._router.GetSelectTool()?.RotateSelection(_ctx, angle);
                return; // 结束，不旋转画布
            }

            // 2. 原有的画布旋转逻辑
            ApplyTransform(new RotateTransform(angle));
            NotifyCanvasChanged();
            _canvasResizer.UpdateUI(); 
        }


        private Color GetPixelColor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _bitmap.PixelWidth || y >= _bitmap.PixelHeight) return Colors.Transparent;
            _bitmap.Lock();
            try
            {
                unsafe
                {
                    byte* ptr = (byte*)_bitmap.BackBuffer;
                    int stride = _bitmap.BackBufferStride;
                    byte* pixel = ptr + y * stride + x * 4;
                    // BGRA 格式
                    return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
                }
            }
            finally
            {
                _bitmap.Unlock();
            }
        }

        private void DrawPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= _bitmap.PixelWidth || y >= _bitmap.PixelHeight) return;

            _bitmap.Lock();
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                byte* p = (byte*)pBackBuffer + y * stride + x * 4;
                p[0] = color.B;
                p[1] = color.G;
                p[2] = color.R;
                p[3] = color.A;
            }
            _bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            _bitmap.Unlock();
        }
        private void FlipBitmap(bool flipVertical)
        {
            double cx = _bitmap.PixelWidth / 2.0;
            double cy = _bitmap.PixelHeight / 2.0;
            ApplyTransform(flipVertical ? new ScaleTransform(1, -1, cx, cy) : new ScaleTransform(-1, 1, cx, cy));
        }
        private BitmapSource CreateWhiteThumbnail()  // 辅助方法：生成纯白缩略图
        {
            int w = 100; int h = 60;
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
                // 可以在中间画个加号或者 "New" 字样
            }
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }

        private byte[] ExtractRegionFromSnapshot(byte[] fullData, Int32Rect rect, int stride)
        {
            int bytesPerPixel = 4;
            byte[] region = new byte[rect.Height * rect.Width * bytesPerPixel];

            for (int row = 0; row < rect.Height; row++)
            {
                int srcOffset = (rect.Y + row) * stride + rect.X * bytesPerPixel;
                int dstOffset = row * rect.Width * bytesPerPixel;
                Buffer.BlockCopy(fullData, srcOffset, region, dstOffset, rect.Width * bytesPerPixel);
            }
            return region;
        }
        private static Int32Rect ClampRect(Int32Rect rect, int maxWidth, int maxHeight)
        {
            // 1. 计算左上角和右下角的边界坐标
            int left = Math.Max(0, rect.X);
            int top = Math.Max(0, rect.Y);

            // 2. 计算右边界和下边界（不能超过最大宽高）
            int right = Math.Min(maxWidth, rect.X + rect.Width);
            int bottom = Math.Min(maxHeight, rect.Y + rect.Height);

            // 3. 计算新的宽高。确保结果不为负数
            int width = Math.Max(0, right - left);
            int height = Math.Max(0, bottom - top);

            return new Int32Rect(left, top, width, height);
        }

        private void ClearRect(ToolContext ctx, Int32Rect rect, Color color)
        {
            ctx.Surface.Bitmap.Lock();
            unsafe
            {
                byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                int stride = ctx.Surface.Bitmap.BackBufferStride;
                for (int y = rect.Y; y < rect.Y + rect.Height; y++)
                {
                    byte* rowPtr = basePtr + y * stride + rect.X * 4;
                    for (int x = 0; x < rect.Width; x++)
                    {
                        rowPtr[0] = color.B;
                        rowPtr[1] = color.G;
                        rowPtr[2] = color.R;
                        rowPtr[3] = color.A;
                        rowPtr += 4;
                    }
                }
            }
            ctx.Surface.Bitmap.AddDirtyRect(rect);
            ctx.Surface.Bitmap.Unlock();
        }
        private void Clean_bitmap(int _bmpWidth, int _bmpHeight)
        {
            _bitmap = new WriteableBitmap(_bmpWidth, _bmpHeight, 96, 96, PixelFormats.Bgra32, null);
            BackgroundImage.Source = _bitmap;

            // 填充白色背景
            _bitmap.Lock();

            if (_undo != null)
            {
                _undo.ClearUndo();
                _undo.ClearRedo();
            }

            if (_surface == null)
                _surface = new CanvasSurface(_bitmap);
            else
                _surface.Attach(_bitmap);
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                for (int y = 0; y < _bmpHeight; y++)
                {
                    byte* row = (byte*)pBackBuffer + y * stride;
                    for (int x = 0; x < _bmpWidth; x++)
                    {
                        row[x * 4 + 0] = 255; // B
                        row[x * 4 + 1] = 255; // G
                        row[x * 4 + 2] = 255; // R
                        row[x * 4 + 3] = 255; // A
                    }
                }
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bmpWidth, _bmpHeight));
            _bitmap.Unlock();

            // 调整窗口和画布大小
            double imgWidth = _bitmap.Width;
            double imgHeight = _bitmap.Height;

            NotifyCanvasSizeChanged(imgWidth, imgHeight);
            UpdateWindowTitle();

            FitToWindow();
            SetBrushStyle(BrushStyle.Round);
        }

        public void NotifyCanvasSizeChanged(double pixwidth, double pixheight)
        {
            BackgroundImage.Width = pixwidth;
            BackgroundImage.Height = pixheight;
            _imageSize = $"{pixwidth}×{pixheight}"+ LocalizationManager.GetString("L_Main_Unit_Pixel");
            OnPropertyChanged(nameof(ImageSize));
            UpdateWindowTitle();
        }
        private void ResizeCanvas(int newWidth, int newHeight)
        {
            var oldBitmap = _surface.Bitmap;
            if (oldBitmap == null) return;
            if (oldBitmap.PixelWidth == newWidth && oldBitmap.PixelHeight == newHeight) return;

            // --- 1. 捕获变换前的完整状态 (Undo) ---
            var undoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);
            var undoPixels = new byte[oldBitmap.PixelHeight * oldBitmap.BackBufferStride];
            oldBitmap.CopyPixels(undoRect, undoPixels, oldBitmap.BackBufferStride, 0);

            // --- 2. 准备变换 ---
            var transform = new ScaleTransform(
                (double)newWidth / oldBitmap.PixelWidth,
                (double)newHeight / oldBitmap.PixelHeight
            );

            var transformedBitmap = new TransformedBitmap(oldBitmap, transform);

            System.Windows.Media.BitmapScalingMode wpfScalingMode;

            // 获取当前设置
            var appMode = SettingsManager.Instance.Current.ResamplingMode;

            switch (appMode)
            {
                case AppResamplingMode.Bilinear:
                    wpfScalingMode = BitmapScalingMode.Linear; // WPF中Linear即双线性
                    break;
                case AppResamplingMode.Fant:
                    wpfScalingMode = BitmapScalingMode.Fant;   // 高质量幻像插值
                    break;
                case AppResamplingMode.HighQuality:
                    wpfScalingMode = BitmapScalingMode.HighQuality; // 一般高质量
                    break;
                case AppResamplingMode.Auto:
                default:
                    wpfScalingMode = BitmapScalingMode.HighQuality;
                    break;
            }

            // 将计算出的模式应用到 transformedBitmap 上
            RenderOptions.SetBitmapScalingMode(transformedBitmap, wpfScalingMode);
            var newFormatedBitmap = new FormatConvertedBitmap(transformedBitmap, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            var newBitmap = new WriteableBitmap(newFormatedBitmap);

            // --- 3. 捕获变换后的完整状态 (Redo) ---
            var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);
            var redoPixels = new byte[newBitmap.PixelHeight * newBitmap.BackBufferStride];
            newBitmap.CopyPixels(redoRect, redoPixels, newBitmap.BackBufferStride, 0);

            // --- 4. 替换画布 ---
            _surface.ReplaceBitmap(newBitmap);

            // --- 5. 记录 Undo ---
            _ctx.Undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);

            NotifyCanvasSizeChanged(newWidth, newHeight);
            NotifyCanvasChanged();
            _bitmap = newBitmap;
            SetUndoRedoButtonState();

            // 如果有 canvasResizer 控件，也更新它
            if (_canvasResizer != null)
                _canvasResizer.UpdateUI();
        }

        private void ConvertToBlackAndWhite(WriteableBitmap bmp)
        {
            bmp.Lock();
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;
                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;
                Parallel.For(0, height, y =>
                {
                    byte* row = basePtr + y * stride;
                    // 像素格式为 BGRA (4 bytes per pixel)
                    for (int x = 0; x < width; x++)
                    {
                        // 获取当前像素的 B, G, R 值
                        byte b = row[x * 4];
                        byte g = row[x * 4 + 1];
                        byte r = row[x * 4 + 2];
                        byte gray = (byte)(r * 0.2126 + g * 0.7152 + b * 0.0722); 
                        row[x * 4] = gray; // Blue
                        row[x * 4 + 1] = gray; // Green
                        row[x * 4 + 2] = gray; 
                    }
                });
            }
            // 标记整个图像区域已更新
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
        }

        private void AutoCrop()
        {
            if (_surface?.Bitmap == null) return;

            var bmp = _surface.Bitmap;
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;

            bmp.Lock();
            Int32Rect cropRect = new Int32Rect(0, 0, width, height);
            bool contentFound = false;

            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;

                // 定义什么是"空白"。通常是全透明 (0) 或全白 (255,255,255)
                // 这里我们可以稍微宽松一点，或者只认定完全透明/白色
                // 为了演示，这里检查 Alpha=0 或者 (R=255,G=255,B=255)
                bool IsEmpty(byte* pixel)
                {
                    byte b = pixel[0];
                    byte g = pixel[1];
                    byte r = pixel[2];
                    byte a = pixel[3];

                    // 判定条件：完全透明 或 完全白色
                    // 你也可以扩展判定，比如跟随当前背景色 BackgroundColor
                    return a == 0 || (r == 255 && g == 255 && b == 255);
                }

                int top = 0, bottom = height - 1, left = 0, right = width - 1;

                // 1. 扫描 Top
                for (; top < height; top++)
                {
                    bool rowHasContent = false;
                    byte* rowPtr = basePtr + top * stride;
                    for (int x = 0; x < width; x++)
                    {
                        if (!IsEmpty(rowPtr + x * 4)) { rowHasContent = true; break; }
                    }
                    if (rowHasContent) break;
                }

                // 如果 top 到底都没找到内容，说明全是空白
                if (top == height)
                {
                    bmp.Unlock();
                    ShowToast("L_Toast_NoContentToCrop");
                    return;
                }

                // 2. 扫描 Bottom
                for (; bottom >= top; bottom--)
                {
                    bool rowHasContent = false;
                    byte* rowPtr = basePtr + bottom * stride;
                    for (int x = 0; x < width; x++)
                    {
                        if (!IsEmpty(rowPtr + x * 4)) { rowHasContent = true; break; }
                    }
                    if (rowHasContent) break;
                }

                // 3. 扫描 Left (只扫描 Top 到 Bottom 之间的行)
                for (; left < width; left++)
                {
                    bool colHasContent = false;
                    for (int y = top; y <= bottom; y++)
                    {
                        byte* pixel = basePtr + y * stride + left * 4;
                        if (!IsEmpty(pixel)) { colHasContent = true; break; }
                    }
                    if (colHasContent) break;
                }

                // 4. 扫描 Right
                for (; right >= left; right--)
                {
                    bool colHasContent = false;
                    for (int y = top; y <= bottom; y++)
                    {
                        byte* pixel = basePtr + y * stride + right * 4;
                        if (!IsEmpty(pixel)) { colHasContent = true; break; }
                    }
                    if (colHasContent) break;
                }

                // 计算裁剪区域
                // +1 是因为坐标是 0-based，宽度长度需要包含当前像素
                cropRect = new Int32Rect(left, top, right - left + 1, bottom - top + 1);
            }
            bmp.Unlock();

            // 检查是否需要裁剪
            if (cropRect.Width == width && cropRect.Height == height)
            {
                ShowToast("L_Toast_MinSize");
                return;
            }

            // 执行裁剪 (复用 ResizeCanvas 的逻辑或者创建新逻辑)
            ApplyAutoCrop(cropRect);
        }

        // 辅助方法：将带透明度的图片合成到白色背景上
        private BitmapSource ConvertToWhiteBackground(BitmapSource source)
        {
            if (source == null) return null;

            // 1. 创建视觉对象
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                // 2. 填充白色背景
                var rect = new Rect(0, 0, source.PixelWidth, source.PixelHeight);
                context.DrawRectangle(Brushes.White, null, rect);

                // 3. 在上层绘制原图
                context.DrawImage(source, rect);
            }

            // 4. 渲染为新的位图
            var rtb = new RenderTargetBitmap(
                source.PixelWidth,
                source.PixelHeight,
                source.DpiX,
                source.DpiY,
                PixelFormats.Pbgra32);

            rtb.Render(drawingVisual);
            rtb.Freeze(); // 冻结以提升性能

            return rtb;
        }
        private BitmapScalingMode GetWpfScalingMode()
        {
            var appMode = SettingsManager.Instance.Current.ResamplingMode;
            switch (appMode)
            {
                case AppResamplingMode.Bilinear:
                    return BitmapScalingMode.Linear;
                case AppResamplingMode.Fant:
                case AppResamplingMode.HighQuality:
                    return BitmapScalingMode.HighQuality; // Fant 其实就是 HighQuality
                case AppResamplingMode.Auto:
                default:
                    return BitmapScalingMode.Unspecified;
            }
        }

        // 核心重采样方法
        private BitmapSource ResampleBitmap(BitmapSource source, int width, int height)
        {
            // 1. 设置绘图视觉对象
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // 关键：应用设置中的插值算法
                RenderOptions.SetBitmapScalingMode(visual, GetWpfScalingMode());

                // 绘制图像到新的尺寸
                dc.DrawImage(source, new Rect(0, 0, width, height));
            }

            double dpiX = source.DpiX;
            double dpiY = source.DpiY;

            var rtb = new RenderTargetBitmap(width, height, dpiX, dpiY, PixelFormats.Pbgra32);
            rtb.Render(visual);

            // 3. 格式转换 (RenderTargetBitmap 是 Pbgra32，我们需要 Bgra32 以便后续字节处理)
            if (rtb.Format != PixelFormats.Bgra32)
            {
                return new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
            }
            return rtb;
        }

        private void ApplyAutoCrop(Int32Rect cropRect)
        {
            var oldBitmap = _surface.Bitmap;
            var undoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);
            var undoPixels = _surface.ExtractRegion(undoRect);
            var newPixels = _surface.ExtractRegion(cropRect);

            // 创建新位图
            var newBitmap = new WriteableBitmap(cropRect.Width, cropRect.Height, oldBitmap.DpiX, oldBitmap.DpiY, PixelFormats.Bgra32, null);
            newBitmap.WritePixels(new Int32Rect(0, 0, cropRect.Width, cropRect.Height), newPixels, newBitmap.BackBufferStride, 0);
            var redoRect = new Int32Rect(0, 0, cropRect.Width, cropRect.Height);
            _surface.ReplaceBitmap(newBitmap);
            _bitmap = newBitmap;
            BackgroundImage.Source = _bitmap;

            _undo.PushTransformAction(undoRect, undoPixels, redoRect, newPixels);
            NotifyCanvasSizeChanged(newBitmap.PixelWidth, newBitmap.PixelHeight);
            NotifyCanvasChanged();
            _canvasResizer.UpdateUI();
            ShowToast(string.Format("Cropped: {0}x{1}", cropRect.Width, cropRect.Height));
        }
    }
}
   