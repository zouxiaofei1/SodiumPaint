using System.ComponentModel;
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
            public bool HasActiveSelection => _selectionData != null;

            // 添加：执行删除选区的具体逻辑
            public void DeleteSelection(ToolContext ctx)
            {
                if (_selectionData == null) return;

                if (!_hasLifted)
                {
                    ctx.Undo.BeginStroke();
                    ctx.Undo.AddDirtyRect(_selectionRect);

                    if (SelectionType == SelectionType.Lasso && _selectionAlphaMap != null)
                    {
                        ClearLassoRegion(ctx, _selectionRect, ctx.EraserColor);
                    }
                    else
                    {
                        ClearRect(ctx, _selectionRect, ctx.EraserColor);
                    }
                    // 提交到 Undo 栈
                    ctx.Undo.CommitStroke();
                    ctx.IsDirty = true;
                }
                HidePreview(ctx);
                if (ctx.SelectionOverlay != null)
                {
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                }
                _selectionData = null;
                _selectionRect = new Int32Rect(0, 0, 0, 0);
                _originalRect = new Int32Rect(0, 0, 0, 0);
                _hasLifted = false;
                _transformStep = 0;
                _draggingSelection = false;
                _resizing = false;
                Mouse.OverrideCursor = null;
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                mw.SetCropButtonState();
                mw.SelectionSize = string.Format(LocalizationManager.GetString("L_Selection_Size_Format"), 0, 0);
                mw.SetUndoRedoButtonState();
                LastSelectionDeleteTime = DateTime.Now;
            }
            public void ResetLastDeleteTime()
            {
                LastSelectionDeleteTime = DateTime.MinValue;
            }

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
                    var bitmapToCopy = BitmapSource.Create(  // 从原始字节数据创建 BitmapSource
                        width,
                        height,
                        ctx.Surface.Bitmap.DpiX,
                        ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32,
                        null,
                        data,
                        stride
                    );
                    DataObject dataObj = new DataObject();
                    dataObj.SetImage(bitmapToCopy);
                    dataObj.SetData(MainWindow.InternalClipboardFormat, "TabPaintInternal");

                    System.Windows.Clipboard.SetDataObject(dataObj, true);
                }
                catch (Exception)
                {
                }
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
            private DateTime _hoverStartTime;
            private bool _isHoveringForSwitch = false;
            public void ForceDragState()
            {
                if (_selectionData != null)
                {
                    _hasLifted = true; // 视为已经浮起
                    _draggingSelection = true;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                        _clickOffset = new Point(_selectionRect.Width / 2, _selectionRect.Height / 2);
                        Mouse.Capture(mw.CanvasWrapper);
                    });
                }
            }
            public void InsertImageAsSelection(ToolContext ctx, BitmapSource sourceBitmap, bool expandCanvas = true)
            {

                // 1. 提交当前的选区（如果有）
                if (_selectionData != null) CommitSelection(ctx);

                if (sourceBitmap == null) return;
                IsPasted = true;
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

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

                    // mw._canvasResizer.UpdateUI();
                    mw.OnPropertyChanged("CanvasWidth");
                    mw.OnPropertyChanged("CanvasHeight");
                }


                int strideFinal = imgW * 4;
                var newData = new byte[imgH * strideFinal];
                sourceBitmap.CopyPixels(newData, strideFinal, 0);

                _selectionData = newData;
                _selectionRect = new Int32Rect(0, 0, imgW, imgH);
                _originalRect = _selectionRect;

                // 这里直接使用 WriteableBitmap 包装归一化后的 bitmap，DPI 已经是正确的了
                ctx.SelectionPreview.Source = new WriteableBitmap(sourceBitmap);

                // 默认放在左上角 (0,0)
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
                ctx.SelectionPreview.Visibility = Visibility.Visible;
                ctx.SelectionPreview.Width = imgW;
                ctx.SelectionPreview.Height = imgH;

                // 绘制 8 个句柄和虚线框
                DrawOverlay(ctx, _selectionRect);
                _transformStep = 0;
                _hasLifted = true;
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();
                mw._canvasResizer.UpdateUI();
            }

            public void PasteSelection(ToolContext ctx, bool ins)
            {

                if (_selectionData != null) CommitSelection(ctx);

                BitmapSource? sourceBitmap = null;
                if (System.Windows.Clipboard.ContainsImage())
                {
                    sourceBitmap = System.Windows.Clipboard.GetImage();
                }
                else if (System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.FileDrop))
                {
                    var fileList = System.Windows.Clipboard.GetData(System.Windows.DataFormats.FileDrop) as string[];
                    if (fileList != null && fileList.Length > 0)
                    {
                        string filePath = fileList[0]; // 取第一个文件
                        sourceBitmap = LoadImageFromFile(filePath);
                    }
                }
                else if (_clipboardData != null)
                {
                    sourceBitmap = BitmapSource.Create(_clipboardWidth, _clipboardHeight,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _clipboardData, _clipboardWidth * 4);
                }

                // 统一处理获取到的位图
                if (sourceBitmap != null)
                {
                    // 调用上一步建议中提取的统一插入逻辑
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

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path);
                    // 必须使用 OnLoad，否则粘贴后如果删除/移动原文件，程序会崩溃或锁定文件
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
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


            // 替换 SelectTool 类中的 SelectAll 方法
            public void SelectAll(ToolContext ctx, bool cut = true)
            {
                if (ctx.Surface?.Bitmap == null) return;
                IsPasted = false;
                ctx.SelectionPreview.Clip = null;             // 清除残留的裁剪
                ctx.SelectionPreview.RenderTransform = null;  // 清除残留的位移/缩放
                Canvas.SetLeft(ctx.SelectionPreview, 0);      // 归位布局坐标
                Canvas.SetTop(ctx.SelectionPreview, 0);       // 归位布局坐标

                _selectionRect = new Int32Rect(0, 0,
                    ctx.Surface.Bitmap.PixelWidth,
                    ctx.Surface.Bitmap.PixelHeight);
                _originalRect = _selectionRect;

                // 提取整幅像素
                _selectionData = ctx.Surface.ExtractRegion(_selectionRect);

                // 如果提取失败或为空，直接返回
                if (_selectionData == null || _selectionData.Length < _selectionRect.Width * _selectionRect.Height * 4)
                    return;

                // Undo 记录逻辑
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_selectionRect);
                ctx.Undo.CommitStroke();
                if (cut)
                {
                    ClearRect(ctx, _selectionRect, ctx.EraserColor);
                    _hasLifted = true; // 标记已经提起来了
                }
                else
                {
                    _hasLifted = false;
                }

                // 创建预览位图
                var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                previewBmp.WritePixels(
                    new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                    _selectionData, _selectionRect.Width * 4, 0);

                ctx.SelectionPreview.Source = previewBmp;
                ctx.SelectionPreview.Visibility = Visibility.Visible;

                double dpiScaleX = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiX;
                double dpiScaleY = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiY;

                ctx.SelectionPreview.Width = _selectionRect.Width * dpiScaleX;
                ctx.SelectionPreview.Height = _selectionRect.Height * dpiScaleY;

                // 确保它填充空间
                ctx.SelectionPreview.Stretch = System.Windows.Media.Stretch.Fill;
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
                _transformStep = 0;

                // 绘制虚线框
                DrawOverlay(ctx, _selectionRect);
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();
            }


            public void CropToSelection(ToolContext ctx)
            {
                if (_selectionData == null || _selectionRect.Width <= 0 || _selectionRect.Height <= 0) return;

                var undoRect = new Int32Rect(0, 0, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                var undoPixels = ctx.Surface.ExtractRegion(undoRect);
                var wb = ctx.Surface.Bitmap;
                int stride = wb.PixelWidth * (wb.Format.BitsPerPixel / 8);
                byte[] pixels = new byte[wb.PixelHeight * stride];

                wb.CopyPixels(pixels, stride, 0);
                byte[] finalSelectionData;
                int finalWidth = _selectionRect.Width;
                int finalHeight = _selectionRect.Height;
                int finalStride;

                // 检查是否进行过缩放
                if (_originalRect.Width > 0 && _originalRect.Height > 0 &&
      (_originalRect.Width != _selectionRect.Width || _originalRect.Height != _selectionRect.Height))
                {
                    var src = BitmapSource.Create(
                        _originalRect.Width, _originalRect.Height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _selectionData, _originalRect.Width * 4);

                    // 【替换旧逻辑】
                    var resizedBitmap = ((MainWindow)System.Windows.Application.Current.MainWindow).ResampleBitmap(src, finalWidth, finalHeight);

                    finalStride = resizedBitmap.PixelWidth * 4;
                    finalSelectionData = new byte[finalHeight * finalStride];
                    resizedBitmap.CopyPixels(finalSelectionData, finalStride, 0);
                }
                else
                {
                    // 没有缩放，直接使用原始数据
                    finalSelectionData = _selectionData;
                    finalStride = finalWidth * 4;
                }

                var newBitmap = new WriteableBitmap(
                    finalWidth,
                    finalHeight,
                    ctx.Surface.Bitmap.DpiX,
                    ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Bgra32,
                    null
                );

                newBitmap.WritePixels(
                    new Int32Rect(0, 0, finalWidth, finalHeight),
                    finalSelectionData,
                    finalStride,
                    0
                );

                var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);
                int redoStride = newBitmap.BackBufferStride;
                var redoPixels = new byte[redoStride * redoRect.Height];
                newBitmap.CopyPixels(redoPixels, redoStride, 0);


                ctx.Surface.ReplaceBitmap(newBitmap);
                Cleanup(ctx);
                ctx.Undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);
                ctx.IsDirty = true;
                ((MainWindow)System.Windows.Application.Current.MainWindow).NotifyCanvasSizeChanged(finalWidth, finalHeight);
                // 更新UI（例如Undo/Redo按钮的状态）
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
            }
            public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f) ////////////////////////////////////////////////////////////////////////////// 后面是鼠标键盘事件处理
            {

                if (((MainWindow)System.Windows.Application.Current.MainWindow).IsViewMode) return;
                if (lag > 0) { lag--; return; }
                if (ctx.Surface.Bitmap == null) return;
                var px = ctx.ToPixel(viewPos);
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                if (_selectionData != null && !isShift)
                {
                    // 判定点击位置是句柄还是框内
                    _currentAnchor = HitTestHandle(px, _selectionRect);
                    if (_currentAnchor != ResizeAnchor.None)
                    {
                        if (_transformStep == 0) _originalRect = _selectionRect;
                        _transformStep++;
                        _resizing = true;
                        _startMouse = px;
                        _startW = _selectionRect.Width;
                        _startH = _selectionRect.Height;
                        _startX = _selectionRect.X;
                        _startY = _selectionRect.Y;
                        ctx.ViewElement.CaptureMouse();
                        return;
                    }
                    else if (IsPointInSelection(px))
                    {
                        if (_transformStep == 0)  _originalRect = _selectionRect;
                        _transformStep++;
                        _draggingSelection = true;
                        _clickOffset = new Point(px.X - _selectionRect.X, px.Y - _selectionRect.Y);
                        ctx.ViewElement.CaptureMouse();
                        return;
                    }
                }
                if (SelectionType == SelectionType.MagicWand)
                {
                    _selecting = true;
                    _isWandAdjusting = true; // 标记开始调整容差
                    _wandStartPoint = px;
                    _startPixel = px; // 借用这个记录一下，方便计算距离
                    _wandTolerance = 0; // 初始点击容差为 0

                    // 记录点击点的颜色
                    _wandStartColor = ((MainWindow)System.Windows.Application.Current.MainWindow).GetPixelColor((int)px.X, (int)px.Y);

                    // 如果没有按 Shift，先清除旧选区 (视觉上)
                    if (!isShift)
                    {
                        HidePreview(ctx);
                        if (ctx.SelectionOverlay != null) ctx.SelectionOverlay.Children.Clear();
                        _selectionData = null; // 逻辑清除
                    }
                    else
                    {
                    }

                    // 立即执行一次容差为0的计算
                    RunMagicWand(ctx, _wandStartPoint, _wandTolerance, isShift);

                    ctx.ViewElement.CaptureMouse();
                    ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();
                    return;
                }

                if (SelectionType == SelectionType.Lasso)
                {
                    _lassoPoints = new List<Point>();
                    _lassoPoints.Add(px);
                }

                _selecting = true;
                _startPixel = px;
                _selectionRect = new Int32Rect((int)px.X, (int)px.Y, 0, 0);
                HidePreview(ctx);
                ctx.ViewElement.CaptureMouse();
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();
            }

            public bool _hasLifted = false;
            public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                ctxForTimer = ctx; // 缓存 Context 供 Timer 使用
                EnsureTimer();
                if ((_selecting || _draggingSelection || _resizing) &&
        Mouse.LeftButton == MouseButtonState.Released)
                {
                    ResetSwitchTimer();
                    OnPointerUp(ctx, viewPos);
                    return;
                }
                var px = ctx.ToPixel(viewPos);
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (_draggingSelection && _selectionData != null)
                {
                    // 1. 坐标转换
                    Point windowPos = ctx.ViewElement.TranslatePoint(viewPos, mw);

                    // 2. 获取 Tab
                    var targetTab = mw.MainImageBar.GetTabFromPoint(windowPos);

                    if (targetTab != null && targetTab != mw._currentTabItem)
                    {
                        // 如果鼠标移动到了一个新的 Tab 上
                        if (_pendingTab != targetTab)
                        {
                            _pendingTab = targetTab;
                            _hoverStartTime = DateTime.Now;
                            _tabSwitchTimer.Start(); // 启动定时器！
                        }
                    }
                    else
                    {
                        if (_pendingTab != null)
                        {
                            ResetSwitchTimer();
                        }
                    }
                }
                else
                {
                    // 如果没在拖拽，确保 Timer 是关掉的
                    if (_pendingTab != null) ResetSwitchTimer();
                }
                // 光标样式
                if (_selectionData != null)
                {
                    var anchor = HitTestHandle(px, _selectionRect);
                    switch (anchor)
                    {
                        case ResizeAnchor.TopLeft:
                        case ResizeAnchor.BottomRight:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNWSE;
                            break;
                        case ResizeAnchor.TopRight:
                        case ResizeAnchor.BottomLeft:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNESW;
                            break;
                        case ResizeAnchor.LeftMiddle:
                        case ResizeAnchor.RightMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeWE;
                            break;
                        case ResizeAnchor.TopMiddle:
                        case ResizeAnchor.BottomMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNS;
                            break;
                        default:
                            Mouse.OverrideCursor = null;
                            break;
                    }
                }

                // 缩放逻辑
                // 缩放逻辑
                if (_resizing)
                {
                    if (!_hasLifted) LiftSelectionFromCanvas(ctx);

                    double fixedRight = _startX + _startW;
                    double fixedBottom = _startY + _startH;

                    bool isShiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    double dx = px.X - _startMouse.X;
                    double dy = px.Y - _startMouse.Y;

                    double proposedW = _startW;
                    double proposedH = _startH;

                    switch (_currentAnchor)
                    {
                        case ResizeAnchor.TopLeft:
                            proposedW = _startW - dx;
                            proposedH = _startH - dy;
                            break;
                        case ResizeAnchor.TopMiddle:
                            proposedH = _startH - dy;
                            break;
                        case ResizeAnchor.TopRight:
                            proposedW = _startW + dx;
                            proposedH = _startH - dy;
                            break;
                        case ResizeAnchor.LeftMiddle:
                            proposedW = _startW - dx;
                            break;
                        case ResizeAnchor.RightMiddle:
                            proposedW = _startW + dx;
                            break;
                        case ResizeAnchor.BottomLeft:
                            proposedW = _startW - dx;
                            proposedH = _startH + dy;
                            break;
                        case ResizeAnchor.BottomMiddle:
                            proposedH = _startH + dy;
                            break;
                        case ResizeAnchor.BottomRight:
                            proposedW = _startW + dx;
                            proposedH = _startH + dy;
                            break;
                    }

                    // 3. Shift 等比例修正 (仅针对四个角，边中点缩放通常不支持Shift或者是中心缩放逻辑较复杂)
                    if (isShiftDown && (_startH != 0))
                    {
                        double aspectRatio = (double)_startW / _startH;

                        if (_currentAnchor == ResizeAnchor.TopLeft || _currentAnchor == ResizeAnchor.TopRight ||
                            _currentAnchor == ResizeAnchor.BottomLeft || _currentAnchor == ResizeAnchor.BottomRight)
                        {
                            // 取变化幅度较大的一边作为主导
                            if (Math.Abs(proposedW / _startW) > Math.Abs(proposedH / _startH))
                            {
                                proposedH = proposedW / aspectRatio;
                            }
                            else
                            {
                                proposedW = proposedH * aspectRatio;
                            }
                        }
                    }

                    if (proposedW < 1) proposedW = 1;
                    if (proposedH < 1) proposedH = 1;

                    double finalX = _startX;
                    double finalY = _startY;

                    // 如果锚点在左侧，X 必须由 右边界 - 宽度 算出
                    if (_currentAnchor == ResizeAnchor.TopLeft ||
                        _currentAnchor == ResizeAnchor.LeftMiddle ||
                        _currentAnchor == ResizeAnchor.BottomLeft)
                    {
                        finalX = fixedRight - proposedW;
                    }
                    else
                    {
                        finalX = _startX;
                    }

                    // 如果锚点在上方，Y 必须由 下边界 - 高度 算出
                    if (_currentAnchor == ResizeAnchor.TopLeft ||
                        _currentAnchor == ResizeAnchor.TopMiddle ||
                        _currentAnchor == ResizeAnchor.TopRight)
                    {
                        finalY = fixedBottom - proposedH;
                    }
                    else
                    {
                        finalY = _startY;
                    }

                    // 6. 应用结果
                    _selectionRect.Width = (int)proposedW;
                    _selectionRect.Height = (int)proposedH;
                    _selectionRect.X = (int)finalX;
                    _selectionRect.Y = (int)finalY;


                    // ---------------- 渲染部分 (Transform逻辑保持不变) ----------------
                    if (_originalRect.Width > 0 && _originalRect.Height > 0)
                    {
                        double scaleX = (double)_selectionRect.Width / _originalRect.Width;
                        double scaleY = (double)_selectionRect.Height / _originalRect.Height;

                        var tg = ctx.SelectionPreview.RenderTransform as TransformGroup;
                        if (tg == null)
                        {
                            tg = new TransformGroup();
                            tg.Children.Add(new ScaleTransform(scaleX, scaleY));
                            tg.Children.Add(new TranslateTransform(_selectionRect.X, _selectionRect.Y));
                            ctx.SelectionPreview.RenderTransform = tg;
                        }
                        else
                        {
                            var s = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                            if (s == null)
                            {
                                s = new ScaleTransform(1, 1);
                                tg.Children.Insert(0, s);
                            }
                            s.ScaleX = scaleX;
                            s.ScaleY = scaleY;

                            var t = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                            if (t == null)
                            {
                                t = new TranslateTransform(0, 0);
                                tg.Children.Add(t);
                            }
                            t.X = _selectionRect.X;
                            t.Y = _selectionRect.Y;
                        }
                        ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }
                    UpdateStatusBarSelectionSize();
                    DrawOverlay(ctx, _selectionRect);
                    return;
                }

                if (_selecting && SelectionType == SelectionType.MagicWand && _isWandAdjusting)
                {
                    // 计算鼠标拖拽距离，映射为容差 (例如 1px = 0.5 容差，最大 255)
                    double dist = Math.Sqrt(Math.Pow(px.X - _startPixel.X, 2) + Math.Pow(px.Y - _startPixel.Y, 2));
                    int newTolerance = (int)(dist / 2.0); // 灵敏度调节
                    if (newTolerance > 255) newTolerance = 255;

                    // 只有容差变化了才重算，节省性能
                    if (newTolerance != _wandTolerance)
                    {
                        _wandTolerance = newTolerance;
                        bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                        RunMagicWand(ctx, _wandStartPoint, _wandTolerance, isShift);

                        // 状态栏提示当前容差
                    //    var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                        mw.SelectionSize = $"{LocalizationManager.GetString("L_Tool_MagicWand")}: {_wandTolerance}";
                    }
                    return;
                }
                if (_selecting)// 框选逻辑
                {
                    _hasLifted = false;
                    if (SelectionType == SelectionType.Lasso)
                    {
                        var pxs = ctx.ToPixel(viewPos);
                        // 简单过滤一下距离，避免点太密集
                        if (_lassoPoints.Count > 0)
                        {
                            var last = _lassoPoints[_lassoPoints.Count - 1];
                            if (Math.Abs(pxs.X - last.X) > 2 || Math.Abs(pxs.Y - last.Y) > 2)
                            {
                                _lassoPoints.Add(pxs);
                                DrawLassoTrace(ctx); // 专门绘制轨迹的方法
                            }
                        }
                    }
                    else // 矩形逻辑 (保持原有)
                    {
                        var pxs = ctx.ToPixel(viewPos);
                        _selectionRect = MakeRect(_startPixel, pxs);
                        if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                            DrawOverlay(ctx, _selectionRect);
                    }
                }

                else if (_draggingSelection) // 拖动逻辑
                {
                    if (!_hasLifted)
                    {
                        LiftSelectionFromCanvas(ctx);
                    }
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        Point posInWindow = ctx.ViewElement.TranslatePoint(viewPos, mainWindow);
                        double margin = -5;
                        bool isOutside = posInWindow.X < margin ||
                                         posInWindow.Y < margin ||
                                         posInWindow.X > mainWindow.ActualWidth - margin ||
                                         posInWindow.Y > mainWindow.ActualHeight - margin;
                        if (isOutside)
                        {
                            ctx.ViewElement.ReleaseMouseCapture();
                            StartDragDropOperation(ctx);
                            _draggingSelection = false;
                            return;
                        }
                    }
                    int newX = (int)(px.X - _clickOffset.X);
                    int newY = (int)(px.Y - _clickOffset.Y);

                    // 更新 TransformGroup 中的 TranslateTransform
                    var tg = ctx.SelectionPreview.RenderTransform as TransformGroup;
                    if (tg != null)
                    {
                        var t = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (t != null)
                        {
                            t.X = newX;
                            t.Y = newY;
                        }
                    }
                    else if (ctx.SelectionPreview.RenderTransform is TranslateTransform singleT)
                    {
                        singleT.X = newX;
                        singleT.Y = newY;
                    }


                    ctx.SelectionPreview.Clip = null;

                    Int32Rect tmprc = new Int32Rect(newX, newY, _selectionRect.Width, _selectionRect.Height);

                    double canvasW = ctx.Surface.Bitmap.PixelWidth;
                    double canvasH = ctx.Surface.Bitmap.PixelHeight;

                    // 选区左上角相对于画布的偏移
                    double offsetX = tmprc.X;
                    double offsetY = tmprc.Y;

                    double ratioX = (double)_selectionRect.Width / (double)_originalRect.Width;
                    double ratioY = (double)_selectionRect.Height / (double)_originalRect.Height;

                    // 计算在预览自身坐标系中的有效显示范围
                    double visibleX = (int)Math.Max(0, -offsetX / ratioX);
                    double visibleY = (int)Math.Max(0, -offsetY / ratioY);


                    double visibleW = Math.Max(0, Math.Min(tmprc.Width / ratioX, (canvasW - offsetX) / ratioX));
                    double visibleH = Math.Max(0, Math.Min(tmprc.Height / ratioY, (canvasH - offsetY) / ratioY));

                    Int32Rect intRect = ClampRect(new Int32Rect((int)visibleX, (int)visibleY, (int)visibleW, (int)visibleH), ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                    Rect rect = new Rect(intRect.X, intRect.Y, intRect.Width, intRect.Height);
                    Geometry visibleRect = new RectangleGeometry(rect);
                    if (visibleW > 0 && visibleH > 0)
                    {
                        // 确保 X 和 Y 也是合法的（虽然通常 offsetX 逻辑不会错，但为了保险）
                        double validX = Math.Max(0, visibleX);
                        double validY = Math.Max(0, visibleY);

                        ctx.SelectionPreview.Clip = new RectangleGeometry(new Rect(validX, validY, visibleW, visibleH));

                        // 确保可见
                        if (ctx.SelectionPreview.Visibility != Visibility.Visible)
                            ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ctx.SelectionPreview.Clip = Geometry.Empty;
                    }

                    DrawOverlay(ctx, tmprc);// 画布的尺寸

                }

                UpdateStatusBarSelectionSize();
            }
            private void RunMagicWand(ToolContext ctx, Point startPt, int tolerance, bool union)
            {
                int w = ctx.Surface.Bitmap.PixelWidth;
                int h = ctx.Surface.Bitmap.PixelHeight;
                int startX = (int)startPt.X;
                int startY = (int)startPt.Y;

                if (startX < 0 || startX >= w || startY < 0 || startY >= h) return;

                // 1. 准备全图 Mask (bool array)
                // 如果是 Shift 追加模式，我们需要保留之前的选中状态
                bool[] mask = new bool[w * h];

                if (union && _selectionData != null && _selectionAlphaMap != null)
                {
                    // 将旧选区恢复到 mask 中
                    int oldX = _selectionRect.X;
                    int oldY = _selectionRect.Y;
                    int oldW = _selectionRect.Width;
                    int oldH = _selectionRect.Height;
                    int oldStride = oldW * 4;
                    for (int y = 0; y < oldH; y++)
                    {
                        for (int x = 0; x < oldW; x++)
                        {
                            int alphaIndex = y * oldStride + x * 4 + 3;
                            if (alphaIndex < _selectionAlphaMap.Length && _selectionAlphaMap[alphaIndex] > 128)
                            {
                                int globalX = oldX + x;
                                int globalY = oldY + y;
                                if (globalX >= 0 && globalX < w && globalY >= 0 && globalY < h)
                                {
                                    mask[globalY * w + globalX] = true;
                                }
                            }
                        }
                    }
                }

                // 2. 执行泛洪填充 (BFS)
                ctx.Surface.Bitmap.Lock();
                try
                {
                    unsafe
                    {
                        byte* ptr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                        int stride = ctx.Surface.Bitmap.BackBufferStride;

                        // 获取目标颜色 (B, G, R, A)
                        byte* startPx = ptr + startY * stride + startX * 4;
                        byte targetB = startPx[0];
                        byte targetG = startPx[1];
                        byte targetR = startPx[2];
                        byte targetA = startPx[3];

                        Queue<int> q = new Queue<int>();
                        q.Enqueue(startX + startY * w);

                        // 如果起始点还没被选中，才开始Fill
                        if (!mask[startX + startY * w])
                        {
                            mask[startX + startY * w] = true; // 标记访问

                            // 4-邻域偏移
                            int[] dx = { 0, 0, 1, -1 };
                            int[] dy = { 1, -1, 0, 0 };

                            while (q.Count > 0)
                            {
                                int curr = q.Dequeue();
                                int cx = curr % w;
                                int cy = curr / w;

                                for (int i = 0; i < 4; i++)
                                {
                                    int nx = cx + dx[i];
                                    int ny = cy + dy[i];

                                    if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                                    {
                                        int nIdx = nx + ny * w;
                                        if (!mask[nIdx]) // 未访问过
                                        {
                                            byte* currPtr = ptr + ny * stride + nx * 4;
                                            byte b = currPtr[0];
                                            byte g = currPtr[1];
                                            byte r = currPtr[2];
                                            byte a = currPtr[3];

                                            // 颜色距离计算 (简单的曼哈顿距离或最大分量差)
                                            // 这里使用各通道绝对差值均小于 tolerance (Box模型)，这在PS里比较常用
                                            bool match = (Math.Abs(b - targetB) <= tolerance) &&
                                                         (Math.Abs(g - targetG) <= tolerance) &&
                                                         (Math.Abs(r - targetR) <= tolerance) &&
                                                         (Math.Abs(a - targetA) <= tolerance);

                                            if (match)
                                            {
                                                mask[nIdx] = true;
                                                q.Enqueue(nIdx);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    ctx.Surface.Bitmap.Unlock();
                }

                // 3. 计算新的包围盒
                int minX = w, maxX = 0, minY = h, maxY = 0;
                bool hasSelection = false;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (mask[y * w + x])
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            hasSelection = true;
                        }
                    }
                }

                if (!hasSelection)
                {
                    Cleanup(ctx);
                    return;
                }

                // 4. 生成 _selectionData 和 _selectionAlphaMap
                int newW = maxX - minX + 1;
                int newH = maxY - minY + 1;
                _selectionRect = new Int32Rect(minX, minY, newW, newH);
                _originalRect = _selectionRect;

                // 提取原始像素数据
                byte[] rawData = ctx.Surface.ExtractRegion(_selectionRect);

                // 生成 AlphaMap (BGRA格式)
                int mapStride = newW * 4;
                _selectionAlphaMap = new byte[newH * mapStride];

                for (int y = 0; y < newH; y++)
                {
                    for (int x = 0; x < newW; x++)
                    {
                        int globalX = minX + x;
                        int globalY = minY + y;
                        bool selected = mask[globalY * w + globalX];

                        int pixelIdx = y * mapStride + x * 4;

                        // 设置 AlphaMap: 选中则 Alpha=255, 否则 0
                        _selectionAlphaMap[pixelIdx + 0] = 0; // B
                        _selectionAlphaMap[pixelIdx + 1] = 0; // G
                        _selectionAlphaMap[pixelIdx + 2] = 0; // R
                        _selectionAlphaMap[pixelIdx + 3] = selected ? (byte)255 : (byte)0; // A

                        // 同时处理 _selectionData：未选中区域设为透明
                        if (!selected)
                        {
                            rawData[pixelIdx + 0] = 0;
                            rawData[pixelIdx + 1] = 0;
                            rawData[pixelIdx + 2] = 0;
                            rawData[pixelIdx + 3] = 0;
                        }
                    }
                }

                _selectionData = rawData;
                _hasLifted = false; // 魔棒刚选完，并未真正“提起来”（原图还在），直到用户拖动

                // 5. 更新预览
                CreatePreviewFromSelectionData(ctx);
            }

            public void UpdateStatusBarSelectionSize()
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {// 状态栏更新
                    ((MainWindow)System.Windows.Application.Current.MainWindow).SelectionSize =
                        $"{_selectionRect.Width}×{_selectionRect.Height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
                });
            }
            private void LiftSelectionFromCanvas(ToolContext ctx)
            {
                if (_hasLifted) return;

                // 1. Undo 记录
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_originalRect);
                ctx.Undo.CommitStroke();

                // 2. 执行清除 (区分矩形模式和套索模式)
                if (SelectionType == SelectionType.Lasso && _selectionAlphaMap != null)
                {
                    // 套索模式：精确清除
                    ClearLassoRegion(ctx, ClampRect(_originalRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight), ctx.EraserColor);
                }
                else
                {
                    // 矩形模式：原有逻辑
                    ClearRect(ctx, ClampRect(_originalRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight), ctx.EraserColor);
                }

                _hasLifted = true;
            }
            private void ClearLassoRegion(ToolContext ctx, Int32Rect rect, Color color)
            {
                var clearMode = SettingsManager.Instance.Current.SelectionClearMode;
                ctx.Surface.Bitmap.Lock();
                try
                {
                    unsafe
                    {
                        byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                        int stride = ctx.Surface.Bitmap.BackBufferStride;
                        int maskStride = _selectionRect.Width * 4; // 遮罩的 stride

                        // 预计算填充色
                        byte tB = 0, tG = 0, tR = 0, tA = 0;
                        bool writeAlpha = true;
                        // ... (这里复用你 ClearRect 里关于 ClearMode 的 switch 判断逻辑) ...
                        // 假设是 Transparent 模式:
                        if (clearMode == SelectionClearMode.Transparent) { tB = 0; tG = 0; tR = 0; tA = 0; writeAlpha = true; }
                        else if (clearMode == SelectionClearMode.White) { tB = 255; tG = 255; tR = 255; tA = 255; writeAlpha = true; }

                        // 遍历区域
                        for (int y = 0; y < rect.Height; y++)
                        {
                            // 注意边界检查，防止遮罩和实际rect尺寸微小差异导致越界
                            if (y * maskStride >= _selectionAlphaMap.Length) break;

                            byte* rowPtr = basePtr + (rect.Y + y) * stride + rect.X * 4;

                            for (int x = 0; x < rect.Width; x++)
                            {
                                int maskIndex = y * maskStride + x * 4 + 3; // Alpha通道
                                if (maskIndex < _selectionAlphaMap.Length)
                                {
                                    // 只有当遮罩显示“此处被选中”（Alpha > 128）时，才清除画布上的像素
                                    if (_selectionAlphaMap[maskIndex] > 128)
                                    {
                                        rowPtr[0] = tB;
                                        rowPtr[1] = tG;
                                        rowPtr[2] = tR;
                                        if (writeAlpha) rowPtr[3] = tA;
                                    }
                                }
                                rowPtr += 4;
                            }
                        }
                    }
                    ctx.Surface.Bitmap.AddDirtyRect(rect);
                }
                finally
                {
                    ctx.Surface.Bitmap.Unlock();
                }
            }
            public override void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e)
            {
                if (((MainWindow)System.Windows.Application.Current.MainWindow).IsViewMode) return;
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {
                        case Key.X:
                            //public ITool CurrentTool => Select;
                            CutSelection(ctx, true);
                            e.Handled = true;
                            break;
                        case Key.C:
                            e.Handled = true;
                            CopySelection(ctx);
                            break;
                            //case Key.V:
                            //    PasteSelection(ctx, false);
                            //    e.Handled = true;
                            //    break;
                    }
                }
                else
                {
                    switch (e.Key)
                    {
                        case Key.Delete:
                            DeleteSelection(ctx);
                            e.Handled = true;
                            break;
                    }
                }
            }

            public void RotateSelection(ToolContext ctx, int angle)
            {
                if (_selectionData == null || _originalRect.Width == 0 || _originalRect.Height == 0) return;

                int oldW = _originalRect.Width;
                int oldH = _originalRect.Height;
                int stride = oldW * 4;

                var srcBmp = BitmapSource.Create(
                    oldW, oldH,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Bgra32, null,
                    _selectionData, stride);

                var transform = new TransformedBitmap(srcBmp, new RotateTransform(angle));

                int newOriginalW = transform.PixelWidth;
                int newOriginalH = transform.PixelHeight;
                int newStride = newOriginalW * 4;

                byte[] newPixels = new byte[newOriginalH * newStride];
                transform.CopyPixels(newPixels, newStride, 0);

                _selectionData = newPixels;
                _originalRect.Width = newOriginalW;
                _originalRect.Height = newOriginalH;

                // 计算旋转后的中心点位置，保持中心不变
                double centerX = _selectionRect.X + _selectionRect.Width / 2.0;
                double centerY = _selectionRect.Y + _selectionRect.Height / 2.0;

                int newSelectionW = _selectionRect.Width;
                int newSelectionH = _selectionRect.Height;

                // 如果旋转90或270度，交换选区的宽高
                if (angle % 180 != 0)
                {
                    newSelectionW = _selectionRect.Height;
                    newSelectionH = _selectionRect.Width;
                }

                int newX = (int)Math.Round(centerX - newSelectionW / 2.0);
                int newY = (int)Math.Round(centerY - newSelectionH / 2.0);

                _selectionRect = new Int32Rect(newX, newY, newSelectionW, newSelectionH);
                // 更新源图片
                var previewBmp = new WriteableBitmap(transform);
                ctx.SelectionPreview.Source = previewBmp;

                double dpiScaleX = 96.0 / ctx.Surface.Bitmap.DpiX;
                double dpiScaleY = 96.0 / ctx.Surface.Bitmap.DpiY;

                ctx.SelectionPreview.Width = newOriginalW * dpiScaleX;
                ctx.SelectionPreview.Height = newOriginalH * dpiScaleY;

                double scaleX = (double)newSelectionW / newOriginalW;
                double scaleY = (double)newSelectionH / newOriginalH;

                // 应用变换：缩放 + 位移
                var tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(scaleX, scaleY));
                tg.Children.Add(new TranslateTransform(newX * dpiScaleX, newY * dpiScaleY));

                ctx.SelectionPreview.RenderTransform = tg;

                // 确保对齐
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);
                ctx.SelectionPreview.Visibility = Visibility.Visible;

                // 刷新虚线框
                DrawOverlay(ctx, _selectionRect);
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                mw.SelectionSize = $"{_selectionRect.Width}×{_selectionRect.Height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
                mw.SetCropButtonState();
            }
            public BitmapSource GetSelectionCroppedBitmap()
            {
                if (_selectionData == null || _originalRect.Width <= 0 || _originalRect.Height <= 0)
                    return null;

                try
                {
                    int stride = _originalRect.Width * 4;

                    // 获取当前画布的 DPI，确保 OCR 识别精度一致
                    var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                    double dpiX = mw._surface?.Bitmap.DpiX ?? AppConsts.StandardDpi;
                    double dpiY = mw._surface?.Bitmap.DpiY ?? AppConsts.StandardDpi;

                    BitmapSource result = BitmapSource.Create( _originalRect.Width,  _originalRect.Height, dpiX, dpiY,
                        PixelFormats.Bgra32,  null,   _selectionData,stride  );

                    if (_selectionRect.Width != _originalRect.Width || _selectionRect.Height != _originalRect.Height)
                    {
                        double scaleX = (double)_selectionRect.Width / _originalRect.Width;
                        double scaleY = (double)_selectionRect.Height / _originalRect.Height;

                        // 使用 TransformedBitmap 进行高质量缩放
                        result = new TransformedBitmap(result, new ScaleTransform(scaleX, scaleY));
                    }
                    result.Freeze();
                    return result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("OCR 裁剪失败: " + ex.Message);
                    return null;
                }
            }
            private void DrawLassoTrace(ToolContext ctx)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                double invScale = 1 / mw.zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.Children.Clear();

                if (_lassoPoints == null || _lassoPoints.Count < 2) return;

                // 构建路径几何
                StreamGeometry geom = new StreamGeometry();
                using (StreamGeometryContext gc = geom.Open())
                {
                    gc.BeginFigure(_lassoPoints[0], false, false);
                    gc.PolyLineTo(_lassoPoints.Skip(1).ToList(), true, false);
                }

                var path = new System.Windows.Shapes.Path
                {
                    Stroke = Brushes.White,
                    StrokeThickness = 2 * invScale,
                    Data = geom,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                };
                var pathBlack = new System.Windows.Shapes.Path
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2 * invScale,
                    Data = geom,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    StrokeDashOffset = 4
                };

                overlay.Children.Add(path);
                overlay.Children.Add(pathBlack);
                overlay.Visibility = Visibility.Visible;
            }

            private void ProcessLassoSelection(ToolContext ctx)
            {
                if (_lassoPoints == null || _lassoPoints.Count < 3) { Cleanup(ctx); return; }

                // 1. 计算包围盒
                double minX = _lassoPoints.Min(p => p.X);
                double minY = _lassoPoints.Min(p => p.Y);
                double maxX = _lassoPoints.Max(p => p.X);
                double maxY = _lassoPoints.Max(p => p.Y);

                var rawRect = new Int32Rect((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
                _selectionRect = ClampRect(rawRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);

                if (_selectionRect.Width <= 0 || _selectionRect.Height <= 0) { Cleanup(ctx); return; }

                // 2. 提取原始矩形像素
                byte[] rawData = ctx.Surface.ExtractRegion(_selectionRect);
                if (rawData == null) return;

                var localPoints = _lassoPoints.Select(p => new Point(p.X - _selectionRect.X, p.Y - _selectionRect.Y)).ToList();
                StreamGeometry geom = new StreamGeometry();
                using (StreamGeometryContext gc = geom.Open())
                {
                    gc.BeginFigure(localPoints[0], true, true);
                    gc.PolyLineTo(localPoints.Skip(1).ToList(), true, true);
                }
                geom.Freeze();
                _selectionGeometry = geom; // 保存下来用于 DrawOverlay

                // 4. 生成遮罩 (用于数据处理和画布清除)
                var visual = new DrawingVisual();
                using (DrawingContext dc = visual.RenderOpen())
                {
                    // 绘制白色形状，背景透明
                    dc.DrawGeometry(Brushes.White, null, geom);
                }
                var maskBmp = new RenderTargetBitmap(_selectionRect.Width, _selectionRect.Height, 96, 96, PixelFormats.Pbgra32);
                maskBmp.Render(visual);

                int stride = _selectionRect.Width * 4;
                _selectionAlphaMap = new byte[_selectionRect.Height * stride];
                maskBmp.CopyPixels(_selectionAlphaMap, stride, 0);

                for (int i = 0; i < rawData.Length; i += 4)
                {
                    // 遮罩Alpha通道在 i+3
                    if (_selectionAlphaMap[i + 3] < 128)
                    {
                        rawData[i + 0] = 0;
                        rawData[i + 1] = 0;
                        rawData[i + 2] = 0;
                        rawData[i + 3] = 0;
                    }
                }

                _selectionData = rawData;
                _originalRect = _selectionRect;

                CreatePreviewFromSelectionData(ctx);
            }

            private void CreatePreviewFromSelectionData(ToolContext ctx)
            {
                var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                previewBmp.WritePixels(new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                                       _selectionData, _selectionRect.Width * 4, 0);

                ctx.SelectionPreview.Source = previewBmp;
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);

                SetPreviewPosition(ctx, _selectionRect.X, _selectionRect.Y);
                ((MainWindow)Application.Current.MainWindow).UpdateSelectionScalingMode();
                ctx.SelectionPreview.Visibility = Visibility.Visible;
                UpdateStatusBarSelectionSize();

                DrawOverlay(ctx, _selectionRect);
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();
            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                if (lag > 0) { lag--; return; }
                ctx.ViewElement.ReleaseMouseCapture();
                var px = ctx.ToPixel(viewPos);
                if (_selecting && SelectionType == SelectionType.MagicWand)
                {
                    _selecting = false;
                    _isWandAdjusting = false;

                    if (_selectionAlphaMap != null && _selectionRect.Width > 0 && _selectionRect.Height > 0)
                    {
                        _originalRect = _selectionRect;

                        // 生成精确轮廓 Geometry（在后台或直接执行）
                        // 对于小选区直接执行，大选区可考虑异步
                        if (_selectionRect.Width * _selectionRect.Height < 500000) // 约 700x700
                        {
                            _selectionGeometry = GeneratePixelEdgeGeometry(
                                _selectionAlphaMap,
                                _selectionRect.Width,
                                _selectionRect.Height,
                                _selectionRect.X,
                                _selectionRect.Y);
                        }

                        UpdateStatusBarSelectionSize();
                        DrawOverlay(ctx, _selectionRect);
                    }
                    else
                    {
                        Cleanup(ctx);
                    }
                    ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();
                    return;
                }
                if (_selecting)
                {
                    _selecting = false;
                    if (SelectionType == SelectionType.Lasso) ProcessLassoSelection(ctx);
                    else
                    {
                        var rawRect = MakeRect(_startPixel, px);
                        _selectionRect = ClampRect(rawRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);

                        if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
                        {
                            // 3. 提取数据 (因为上面已经Clamp过了，这里提取的数据量就是精确匹配 _selectionRect 的)
                            _selectionData = ctx.Surface.ExtractRegion(_selectionRect);

                            // 4. 记录原始尺寸
                            _originalRect = _selectionRect;
                            CreatePreviewFromSelectionData(ctx);
                        }
                        else
                        {
                            Cleanup(ctx);
                        }
                    }
                }
                else if (_draggingSelection)
                {
                    _draggingSelection = false;

                    Point relativePoint = ctx.SelectionPreview.TranslatePoint(new Point(0, 0), ctx.ViewElement);
                    Point pixelPoint = ctx.ToPixel(relativePoint);
                    _selectionRect.X = (int)Math.Round(pixelPoint.X);
                    _selectionRect.Y = (int)Math.Round(pixelPoint.Y);
                }

                if (_resizing)
                {
                    _resizing = false;
                    _currentAnchor = ResizeAnchor.None;
                    return;
                }
                if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                {
                    DrawOverlay(ctx, _selectionRect);
                }
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();

            }

        }
    }
}