using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//SelectTool类键鼠操作相关方法
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class SelectTool : ToolBase
        {

            private void CopyToSystemClipboard(ToolContext ctx)
            {
                if (_selectionData == null) return;
                int width = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                int height = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                byte[] data = _selectionData;

                if (width == 0 || height == 0) return;
                int stride = width * 4;
                try
                {
                    var bitmapToCopy = BitmapSource.Create(
                        width, height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, data, stride);

                    DataObject dataObj = new DataObject();
                    dataObj.SetImage(bitmapToCopy);
                    using (var pngStream = new System.IO.MemoryStream())
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapToCopy));
                        encoder.Save(pngStream);
                        var pngData = pngStream.ToArray();
                        var clipStream = new System.IO.MemoryStream(pngData);
                        dataObj.SetData("PNG", clipStream, false);
                    }

                    // 3. 内部标记
                    dataObj.SetData(MainWindow.InternalClipboardFormat, "TabPaintInternal");

                    System.Windows.Clipboard.SetDataObject(dataObj, true);
                }
                catch (Exception) { }
            }

            public void CutSelection(ToolContext ctx, bool paste)
            {//paste = false ->delete , true->cut
                if (_selectionData == null) SelectAll(ctx, true);

                if (_selectionData == null) return;
                int Clipwidth, Clipheight;
                if (_originalRect.Width == 0 || _originalRect.Height == 0)
                {
                    Clipwidth = _selectionRect.Width;
                    Clipheight = _selectionRect.Height;
                }
                else
                {
                    Clipwidth = _originalRect.Width;
                    Clipheight = _originalRect.Height;
                }
                // 复制到剪贴板
                if (paste)
                {
                    CopyToSystemClipboard(ctx);
                    _clipboardWidth = Clipwidth;
                    _clipboardHeight = Clipheight;

                    _clipboardData = new byte[_selectionData.Length];
                    Array.Copy(_selectionData, _clipboardData, _selectionData.Length);
                }
                else
                {
                    _clipboardData = null; _clipboardWidth = _clipboardHeight = 0;
                }
                DeleteSelection(ctx);

            }

            public void PasteSelection(ToolContext ctx, bool ins)
            {
                if (_selectionData != null) CommitSelection(ctx);

                BitmapSource? sourceBitmap = null;
                bool isInternalCopy = false;
                try
                {
                    var dataObj = System.Windows.Clipboard.GetDataObject();
                    if (dataObj != null && dataObj.GetDataPresent(MainWindow.InternalClipboardFormat))
                    {
                        isInternalCopy = true;
                    }
                }
                catch { }

                if (isInternalCopy && _clipboardData != null && _clipboardWidth > 0 && _clipboardHeight > 0)
                {
                    // 直接使用内部缓存，完整保留 Alpha
                    sourceBitmap = BitmapSource.Create(
                        _clipboardWidth, _clipboardHeight,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _clipboardData, _clipboardWidth * 4);
                }
                else
                {
                    // ★ 优先级2：尝试从剪贴板读取 PNG 格式（保留透明度）
                    try
                    {
                        var dataObj = System.Windows.Clipboard.GetDataObject();
                        if (dataObj != null && dataObj.GetDataPresent("PNG"))
                        {
                            var pngStream = dataObj.GetData("PNG") as System.IO.Stream;
                            if (pngStream != null)
                            {
                                pngStream.Position = 0;
                                var decoder = new PngBitmapDecoder(pngStream,
                                    BitmapCreateOptions.PreservePixelFormat,
                                    BitmapCacheOption.OnLoad);
                                if (decoder.Frames.Count > 0)
                                {
                                    sourceBitmap = decoder.Frames[0];
                                }
                            }
                        }
                    }
                    catch { }

                    // ★ 优先级3：标准图像格式（会丢失透明度，但兼容外部程序）
                    if (sourceBitmap == null && System.Windows.Clipboard.ContainsImage())
                    {
                        sourceBitmap = System.Windows.Clipboard.GetImage();
                    }

                    // ★ 优先级4：文件拖放
                    if (sourceBitmap == null && System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.FileDrop))
                    {
                        var fileList = System.Windows.Clipboard.GetData(System.Windows.DataFormats.FileDrop) as string[];
                        if (fileList != null && fileList.Length > 0)
                        {
                            string filePath = fileList[0];
                            sourceBitmap = LoadImageFromFile(filePath);
                        }
                    }

                    // ★ 优先级5：内部缓存兜底（没有内部标记但有缓存数据）
                    if (sourceBitmap == null && _clipboardData != null && _clipboardWidth > 0 && _clipboardHeight > 0)
                    {
                        sourceBitmap = BitmapSource.Create(
                            _clipboardWidth, _clipboardHeight,
                            ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                            PixelFormats.Bgra32, null, _clipboardData, _clipboardWidth * 4);
                    }
                }

                if (sourceBitmap != null)
                {
                    InsertImageAsSelection(ctx, sourceBitmap);
                }
            }
            private BitmapSource? LoadImageFromFile(string path)
            {
                try
                {
                    // 检查扩展名过滤非图片文件
                    string ext = System.IO.Path.GetExtension(path).ToLower();
                    string[] allowed = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };
                    if (!allowed.Contains(ext)) return null;

                    // 获取原始尺寸
                    int originalWidth = 0;
                    int originalHeight = 0;
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                        originalWidth = decoder.Frames[0].PixelWidth;
                        originalHeight = decoder.Frames[0].PixelHeight;
                    }

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;

                    // 检查尺寸限制
                    const int maxSize = (int)AppConsts.MaxCanvasSize;
                    if (originalWidth > maxSize || originalHeight > maxSize)
                    {
                        if (originalWidth >= originalHeight)
                            bitmap.DecodePixelWidth = maxSize;
                        else
                            bitmap.DecodePixelHeight = maxSize;

                        ctxForTimer?.ParentWindow?.ShowToast("L_Toast_ImageTooLarge");
                    }

                    bitmap.EndInit();
                    bitmap.Freeze(); // 跨线程安全
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Load file from clipboard failed: " + ex.Message);
                    return null;
                }
            }
            public void CopySelection(ToolContext ctx)
            {
                if (_selectionData == null) SelectAll(ctx, false);

                if (_selectionData != null)
                {
                    CopyToSystemClipboard(ctx);
                    _clipboardWidth = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                    _clipboardHeight = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                    _clipboardData = new byte[_selectionData.Length];
                    Array.Copy(_selectionData, _clipboardData, _selectionData.Length);
                }
            }

            public void InsertImageAsSelection(ToolContext ctx, BitmapSource sourceBitmap, bool expandCanvas = true, Point? dropPos = null)
            {

                // 1. 提交当前的选区（如果有）
                if (_selectionData != null) CommitSelection(ctx);

                if (sourceBitmap == null) return;

                // 尺寸限制检查
                const int maxSize = (int)AppConsts.MaxCanvasSize;
                if (sourceBitmap.PixelWidth > maxSize || sourceBitmap.PixelHeight > maxSize)
                {
                    double scale = Math.Min((double)maxSize / sourceBitmap.PixelWidth, (double)maxSize / sourceBitmap.PixelHeight);
                    sourceBitmap = new TransformedBitmap(sourceBitmap, new ScaleTransform(scale, scale));
                    ctx.ParentWindow?.ShowToast("L_Toast_ImageTooLarge");
                }

                IsPasted = true;
                var mw = ctx.ParentWindow;

                if (sourceBitmap.Format != PixelFormats.Bgra32)
                {
                    sourceBitmap = new FormatConvertedBitmap(sourceBitmap, PixelFormats.Bgra32, null, 0);
                }

                double canvasDpiX = ctx.Surface.Bitmap.DpiX;
                double canvasDpiY = ctx.Surface.Bitmap.DpiY;

                // 允许一点点浮点误差
                if (Math.Abs(sourceBitmap.DpiX - canvasDpiX) > 1.0 || Math.Abs(sourceBitmap.DpiY - canvasDpiY) > 1.0)
                {
                    int w = sourceBitmap.PixelWidth;
                    int h = sourceBitmap.PixelHeight;
                    int stride = w * 4;
                    byte[] rawPixels = new byte[h * stride];

                    // 提取原始像素
                    sourceBitmap.CopyPixels(rawPixels, stride, 0);

                    // 使用画布的 DPI 重新创建 BitmapSource
                    sourceBitmap = BitmapSource.Create(
                        w, h,
                        canvasDpiX, canvasDpiY, // 强行使用画布 DPI
                        PixelFormats.Bgra32,
                        null,
                        rawPixels,
                        stride);
                }
                int imgW = sourceBitmap.PixelWidth;
                int imgH = sourceBitmap.PixelHeight;
                int canvasW = ctx.Surface.Bitmap.PixelWidth;
                int canvasH = ctx.Surface.Bitmap.PixelHeight;

                bool _canvasChanged = false;

                if (expandCanvas && (imgW > canvasW || imgH > canvasH))
                {
                    _canvasChanged = true;
                    int newW = Math.Max(imgW, canvasW);
                    int newH = Math.Max(imgH, canvasH);

                    Int32Rect oldRect = new Int32Rect(0, 0, canvasW, canvasH);
                    byte[] oldPixels = ctx.Surface.ExtractRegion(oldRect);

                    var newBmp = new WriteableBitmap(newW, newH, ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);
                    newBmp.Lock();
                    unsafe
                    {
                        byte* p = (byte*)newBmp.BackBuffer;
                        int totalBytes = newBmp.BackBufferStride * newBmp.PixelHeight;
                        for (int i = 0; i < totalBytes; i++) p[i] = 255;
                        newBmp.AddDirtyRect(new Int32Rect(0, 0, newW, newH));
                    }
                    newBmp.Unlock();

                    newBmp.WritePixels(oldRect, oldPixels, canvasW * 4, 0);
                    ctx.Surface.ReplaceBitmap(newBmp);
                    Int32Rect redoRect = new Int32Rect(0, 0, newW, newH);
                    byte[] redoPixels = ctx.Surface.ExtractRegion(redoRect);
                    mw.UpdateSelectionScalingMode();
                    ctx.Undo.PushTransformAction(oldRect, oldPixels, redoRect, redoPixels);
                    mw.NotifyCanvasSizeChanged(newW, newH);
                    mw.OnPropertyChanged("CanvasWidth");
                    mw.OnPropertyChanged("CanvasHeight");
                }


                int strideFinal = imgW * 4;
                var newData = new byte[imgH * strideFinal];
                sourceBitmap.CopyPixels(newData, strideFinal, 0);

                _selectionData = newData;
                int startX = 0;
                int startY = 0;

                if (dropPos.HasValue)
                {
                    Point px = ctx.ToPixel(dropPos.Value);
                    startX = (int)(px.X - imgW / 2.0);
                    startY = (int)(px.Y - imgH / 2.0);
                }

                _selectionRect = new Int32Rect(startX, startY, imgW, imgH);
                _originalRect = _selectionRect;
                ctx.SelectionPreview.Source = new WriteableBitmap(sourceBitmap);
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(startX, startY);
                ctx.SelectionPreview.Visibility = Visibility.Visible;
                ctx.SelectionPreview.Width = imgW;
                ctx.SelectionPreview.Height = imgH;

                // 绘制 8 个句柄和虚线框
                DrawOverlay(ctx, _selectionRect);
                _transformStep = 0;
                _hasLifted = true;

                mw.UpdateSelectionToolBarPosition();
                mw.SetCropButtonState();
                mw._canvasResizer.UpdateUI(); lag = 0;
            }


        }
    }
}
