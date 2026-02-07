
//
//EventHandler.Canvas.cs
//画布相关的事件处理逻辑，包括自动裁剪、OCR、AI背景移除、超分重建以及各种图像滤镜的触发。
//
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Ocr;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private bool IsOcrSupported()
        {
            // 1. 检查操作系统版本 (Windows 10 是 Major 10)
            var os = Environment.OSVersion;
            if (os.Version.Major < 10) return false;
            if (os.Version.Build < 17134) return false;

            return true;
        }
        private bool IsVcRedistInstalled()
        {
            try
            {
                string keyPath = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        int installed = (int)key.GetValue("Installed", 0);
                        int major = (int)key.GetValue("Major", 0);
                        // 14.0 以上通常代表 2015+ 版本
                        return installed == 1 && major >= 14;
                    }
                }
            }
            catch{ return true;}
            return false;
        }

        private void OnAddBorderClick(object sender, RoutedEventArgs e)
        {
            // 1. 检查画布是否存在
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            var bmp = _surface.Bitmap;
            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;
            int borderSize = AppConsts.DefaultBorderThickness; // 边框厚度

            // 如果图片太小，不足以画边框，直接返回
            if (w <= borderSize * 2 || h <= borderSize * 2)
            {
                ShowToast("L_Toast_SizeTooSmallForBorder");
                return;
            }
            _undo.BeginStroke();
            bmp.Lock();
            try
            {
                Color c = ForegroundColor;

                unsafe
                {
                    byte* basePtr = (byte*)bmp.BackBuffer;
                    int stride = bmp.BackBufferStride;
                    void FillRect(int rectX, int rectY, int rectW, int rectH)
                    {
                        for (int y = rectY; y < rectY + rectH; y++)
                        {
                            byte* rowPtr = basePtr + (y * stride) + (rectX * 4);
                            for (int x = 0; x < rectW; x++)
                            {
                                rowPtr[0] = c.B;
                                rowPtr[1] = c.G;
                                rowPtr[2] = c.R;
                                rowPtr[3] = c.A;
                                rowPtr += 4; // 移动到下一个像素
                            }
                        }
                    }
                    FillRect(0, 0, w, borderSize);
                    FillRect(0, h - borderSize, w, borderSize);
                    FillRect(0, borderSize, borderSize, h - 2 * borderSize);
                    FillRect(w - borderSize, borderSize, borderSize, h - 2 * borderSize);
                }
                var rTop = new Int32Rect(0, 0, w, borderSize);
                bmp.AddDirtyRect(rTop);
                _undo.AddDirtyRect(rTop);
                var rBottom = new Int32Rect(0, h - borderSize, w, borderSize);
                bmp.AddDirtyRect(rBottom);
                _undo.AddDirtyRect(rBottom);
                var rLeft = new Int32Rect(0, 0, borderSize, h);
                bmp.AddDirtyRect(rLeft);
                _undo.AddDirtyRect(rLeft);
                var rRight = new Int32Rect(w - borderSize, 0, borderSize, h);
                bmp.AddDirtyRect(rRight);
                _undo.AddDirtyRect(rRight);
            }
            finally
            {
                bmp.Unlock();
            }
            _undo.CommitStroke();
            _isEdited = true;
            _ctx.IsDirty = true;
            NotifyCanvasChanged();
            ShowToast("L_Toast_BorderAdded");
        }

        private Point _lastRightClickPosition; // 记录右键点击时的相对坐标
        private void OnAutoCropClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isLoadingImage) return;
                if (_router.CurrentTool is SelectTool st && st.HasActiveSelection)
                {
                    st.CommitSelection(_ctx);
                }

                AutoCrop();
            }
            catch (Exception ex)
            {
                // 简单的错误处理
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_CropFailed_Prefix"), ex.Message));
            }
        }
        private void OnCopyColorCodeClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;

            try
            {
                double scaleX = _bitmap.PixelWidth / BackgroundImage.ActualWidth;
                double scaleY = _bitmap.PixelHeight / BackgroundImage.ActualHeight;

                int x = (int)(_lastRightClickPosition.X * scaleX);
                int y = (int)(_lastRightClickPosition.Y * scaleY);

                if (x < 0 || x >= _bitmap.PixelWidth || y < 0 || y >= _bitmap.PixelHeight)
                {
                    ShowToast("L_Toast_NoSelection");
                    return;
                }
                Color color = GetPixelColor(x, y);
                string hexCode = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                System.Windows.Clipboard.SetText(hexCode);
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ColorCopied_Format"), hexCode));
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Common_Error") + ": {0}", ex.Message));
            }
        }
        private void OnScreenColorPickerClick(object sender, RoutedEventArgs e)
        {
            // 获取当前的 Dispatcher
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var picker = new ColorPickerWindow();

                // 2. 打开遮罩窗口 (此时菜单已经不可见)
                bool? result = picker.ShowDialog();

                if (result == true && picker.IsColorPicked)
                {
                    Color c = picker.PickedColor;
                    // 4. 应用颜色逻辑
                    ApplyPickedColor(c);
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }


        private void ApplyPickedColor(Color c)
        {
            if (!useSecondColor)
            {
                this.ForegroundColor = c;
                this.ForegroundBrush = new SolidColorBrush(c);
            }
            else
            {
                this.BackgroundColor = c;
                this.BackgroundBrush = new SolidColorBrush(c);
            }


            // 通知UI更新
            OnPropertyChanged(nameof(SelectedBrush));
            OnPropertyChanged(nameof(ForegroundBrush));
            OnPropertyChanged(nameof(BackgroundBrush));
            // 简单的提示
            ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ColorPicked_Format"), c.R, c.G, c.B));
        }
        private void OnScrollContainerContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (IsViewMode)
            {
                e.Handled = true;
                return;
            }
            _lastRightClickPosition = Mouse.GetPosition(BackgroundImage);
        }

        private void OnChromaKeyClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_bitmap == null) return;
                _router.CleanUpSelectionandShape();
                Point targetPoint = new Point(0, 0);

                if (sender is MainWindow || e is System.Windows.Input.KeyEventArgs) // 快捷键触发通常 sender 是 Window 或者 e 是 KeyEventArgs
                {
                    targetPoint = Mouse.GetPosition(CanvasWrapper);
                }
                else
                {
                    // 右键菜单触发：使用记录下的右键点击位置
                    targetPoint = _lastRightClickPosition;
                }

                int x = (int)targetPoint.X;
                int y = (int)targetPoint.Y;

                if (x < 0 || x >= _bitmap.PixelWidth || y < 0 || y >= _bitmap.PixelHeight)
                {
                    x = Math.Clamp(x, 0, _bitmap.PixelWidth - 1);
                    y = Math.Clamp(y, 0, _bitmap.PixelHeight - 1);
                }

                Color targetColor = GetPixelColor(x, y);

                ApplyColorKey(targetColor, AppConsts.DefaultChromaKeyTolerance);
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_RemoveBgFailed_Prefix"), ex.Message));
            }
        }
        private async void OnAiOcrClick(object sender, RoutedEventArgs e)
        {
            ShowToast("L_Toast_AiOcr_NotAvailable");
        }

        // 辅助方法：将 BitmapSource 转为 BMP 字节数组 (RapidOCR 接受)
        private byte[] BitmapSourceToBytes(BitmapSource source)
        {
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }

        private void ApplyColorKey(Color targetColor, int tolerance)
        {
            if (_surface?.Bitmap == null) return;

            _undo.BeginStroke();

            _bitmap.Lock();
            unsafe
            {
                byte* basePtr = (byte*)_bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                int width = _bitmap.PixelWidth;
                int height = _bitmap.PixelHeight;

                // 预计算目标颜色的分量
                int tR = targetColor.R;
                int tG = targetColor.G;
                int tB = targetColor.B;
                // 容差平方，避免开根号运算以提升性能
                int toleranceSq = tolerance * tolerance;

                // 2. 并行处理像素
                Parallel.For(0, height, y =>
                {
                    byte* row = basePtr + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        // 格式 BGRA
                        byte b = row[x * 4];
                        byte g = row[x * 4 + 1];
                        byte r = row[x * 4 + 2];
                        byte a = row[x * 4 + 3];

                        if (a == 0) continue; // 已经是透明的跳过

                        // 计算色差 (简单的欧几里得距离)
                        int diffR = r - tR;
                        int diffG = g - tG;
                        int diffB = b - tB;

                        int distSq = (diffR * diffR) + (diffG * diffG) + (diffB * diffB);

                        // 3 * toleranceSq 是因为我们累加了3个通道的误差平方
                        if (distSq <= 3 * toleranceSq)
                        {
                            row[x * 4 + 3] = 0; // Alpha 设为 0 (完全透明)
                        }
                    }
                });
            }
            var fullRect = new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight);
            _bitmap.AddDirtyRect(fullRect);
            _bitmap.Unlock();

            _undo.AddDirtyRect(fullRect);
            _undo.CommitStroke();

            NotifyCanvasChanged();
        }

        private async void OnOcrClick(object sender, RoutedEventArgs e)
        {
            // 1. 检查是否有图
            if (_surface?.Bitmap == null) return;
            if (!IsOcrSupported())
            {
                ShowToast("L_Toast_OCR_VersionError");
                return;
            }
            BitmapSource sourceToRecognize = _surface.Bitmap;

            if (_router.CurrentTool is SelectTool selTool && selTool.HasActiveSelection)
            {
                sourceToRecognize = selTool.GetSelectionCroppedBitmap(this);
            }

            try
            {
                // 3. UI 提示
                var oldStatus = _imageSize;
                _imageSize = LocalizationManager.GetString("L_OCR_Status_Processing");
                this.Cursor = System.Windows.Input.Cursors.Wait;

                // 4. 调用服务
                var ocrService = new OcrService(); // 也可以作为单例注入
                string text = await ocrService.RecognizeTextAsync(sourceToRecognize);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    System.Windows.Clipboard.SetText(text);
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_OCR_Success_Format"), text.Length));
                }
                else
                {
                    ShowToast("L_Toast_OCR_NoText");
                }

                _imageSize = oldStatus;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("0x80004005") || ex.Message.Contains("Language"))
                {
                    ShowToast("L_Toast_OCR_InitFailed");
                }
                else if (ex is PlatformNotSupportedException)
                {
                    ShowToast("L_Toast_OCR_NotSupported");
                }
                else
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_OCR_Error_Prefix"), ex.Message));
                }
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        private async void OnAiUpscaleClick(object sender, RoutedEventArgs e)
        {
            // 1. 基础检查
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();

            if (!IsVcRedistInstalled())
            {
                ShowToast("L_Error_MissingVCRedist");
                return;
            }

            bool ready = await EnsureAiModelReadyAsync(AiService.AiTaskType.SuperResolution);
            if (!ready) return;

            // 2. 状态保存与 UI 锁定
            var statusText = _imageSize;

            try
            {
                var aiService = new AiService(_cacheDir);
                string modelPath = Path.Combine(_cacheDir, AppConsts.Sr_ModelName);

                WriteableBitmap inputBmp = _surface.Bitmap;
                const int MaxLongSide = AppConsts.AiUpscaleMaxLongSide; // 限制长边最大 4096

                if (inputBmp.PixelWidth > MaxLongSide || inputBmp.PixelHeight > MaxLongSide)
                {
                    _imageSize = "图片过大，正在进行预缩小...";
                    OnPropertyChanged(nameof(ImageSize));

                    double scale = (double)MaxLongSide / Math.Max(inputBmp.PixelWidth, inputBmp.PixelHeight);
                    int targetW = (int)(inputBmp.PixelWidth * scale);
                    int targetH = (int)(inputBmp.PixelHeight * scale);

                    var resampledSource = ResampleBitmap(inputBmp, targetW, targetH);
                    inputBmp = new WriteableBitmap(resampledSource);
                }

                // 4. 执行推理
                _imageSize = LocalizationManager.GetString("L_AI_Status_Thinking");
                OnPropertyChanged(nameof(ImageSize));

                var inferProgress = new Progress<double>(p =>
                {
                    _imageSize = string.Format(LocalizationManager.GetString("L_AI_Status_Upscaling_Format"), p);
                    OnPropertyChanged(nameof(ImageSize));
                });

                // 注意这里传入的是处理后的 inputBmp，而不是直接传 _surface.Bitmap
                var resultBitmap = await aiService.RunSuperResolutionAsync(modelPath, inputBmp, inferProgress);

                // 5. 应用结果
                ApplyUpscaleResult(resultBitmap);
                GC.Collect(2, GCCollectionMode.Forced, true);
                ShowToast("L_Toast_Apply_Success");
            }
            catch (OperationCanceledException)
            {
               
                DownloadProgressPopup.Finish();
            }
            catch (Exception ex)
            {
                // 捕获溢出或其他异常
                string errorMsg = ex.Message;
                if (ex is OverflowException) errorMsg = "图片尺寸超出硬件/软件限制";
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_Upscale_Error_Prefix"), errorMsg));
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
                if (_surface?.Bitmap != null)
                    _imageSize = $"{_surface.Bitmap.PixelWidth}×{_surface.Bitmap.PixelHeight}" + LocalizationManager.GetString("L_Main_Unit_Pixel");

                OnPropertyChanged(nameof(ImageSize));
                NotifyCanvasChanged();
                this.Focus();
            }
        }
        private System.Threading.CancellationTokenSource _downloadCts;

        // 通用的下载取消处理方法
        private void OnDownloadCancelRequested(object sender, EventArgs e)
        {
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                _downloadCts.Cancel();
                ShowToast("L_Toast_DownloadCancelled"); 
            }
        }


        // 专门处理超分结果的应用逻辑 (因为画布尺寸变了)
        private void ApplyUpscaleResult(WriteableBitmap newBitmap)
        {
            var oldBitmap = _surface.Bitmap;
            int oldW = oldBitmap.PixelWidth;
            int oldH = oldBitmap.PixelHeight;

            // 1. 记录 Undo (变换前)
            var undoRect = new Int32Rect(0, 0, oldW, oldH);
            byte[] undoPixels = new byte[oldH * oldBitmap.BackBufferStride];
            oldBitmap.CopyPixels(undoRect, undoPixels, oldBitmap.BackBufferStride, 0);

            // 2. 替换 Bitmap
            _surface.ReplaceBitmap(newBitmap);
            _bitmap = newBitmap;
            BackgroundImage.Source = _bitmap; // 关键：更新显示源

            // 3. 记录 Redo (变换后)
            int newW = newBitmap.PixelWidth;
            int newH = newBitmap.PixelHeight;
            var redoRect = new Int32Rect(0, 0, newW, newH);
            byte[] redoPixels = new byte[newH * newBitmap.BackBufferStride];
            newBitmap.CopyPixels(redoRect, redoPixels, newBitmap.BackBufferStride, 0);

            // 4. 推入 Undo 栈 (利用你现有的 TransformAction)
            _undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);

            // 5. 更新状态
            NotifyCanvasSizeChanged(newW, newH);
            SetUndoRedoButtonState();

            // 自适应窗口
            if (_canvasResizer != null) _canvasResizer.UpdateUI();
        }

        private async void OnRemoveBackgroundClick(object sender, RoutedEventArgs e)
        {
            // 1. 基础检查
            if (_surface?.Bitmap == null) return;
           // _router.CleanUpSelectionandShape();
            var selectTool = _router.CurrentTool as SelectTool;
            bool isSelectionMode = selectTool != null && selectTool.HasActiveSelection;
            if (!isSelectionMode) _router.CleanUpSelectionandShape();
        
            if (!IsVcRedistInstalled())
            {
                var result = FluentMessageBox.Show(
                    LocalizationManager.GetString("L_AI_RMBG_MissingRuntime_Content"),
                    LocalizationManager.GetString("L_AI_RMBG_MissingRuntime_Title"),
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                        UseShellExecute = true
                    });
                }
                return;
            }

            bool ready = await EnsureAiModelReadyAsync(AiService.AiTaskType.RemoveBackground);
            if (!ready) return;

            var statusText = _imageSize; // 暂存状态栏文本

            try
            {
                var aiService = new AiService(_cacheDir);
                string modelPath = Path.Combine(_cacheDir, AppConsts.BgRem_ModelName);

                _imageSize = LocalizationManager.GetString("L_AI_Status_Thinking");
                OnPropertyChanged(nameof(ImageSize));

                byte[] resultPixels;
                if (isSelectionMode)
                {
                    // ========== 关键修改：区分规则选区和不规则选区 ==========
                    if (selectTool.IsIrregularSelection)
                    {
                        // --- 套索/魔棒模式：送完整包围盒像素给 AI ---
                        var boundingBoxBmp = selectTool.GetSelectionBoundingBoxBitmap(_ctx);
                        if (boundingBoxBmp == null)
                        {
                            // 回退：使用裁剪后的位图
                            boundingBoxBmp = selectTool.GetSelectionWriteableBitmap(this);
                        }
                        if (boundingBoxBmp == null) return;

                        int newW = boundingBoxBmp.PixelWidth;
                        int newH = boundingBoxBmp.PixelHeight;

                        // AI 推理：输入完整矩形区域
                        resultPixels = await aiService.RunInferenceAsync(modelPath, boundingBoxBmp);

                        // 将 AI 结果与套索 mask 做交集
                        selectTool.ReplaceSelectionDataWithMask(_ctx, resultPixels, newW, newH);
                    }
                    else
                    {
                        // --- 矩形选区模式：保持原有逻辑 ---
                        var cropBmp = selectTool.GetSelectionWriteableBitmap(this);
                        if (cropBmp == null) return;

                        int newW = cropBmp.PixelWidth;
                        int newH = cropBmp.PixelHeight;
                        resultPixels = await aiService.RunInferenceAsync(modelPath, cropBmp);
                        selectTool.ReplaceSelectionData(_ctx, resultPixels, newW, newH);
                    }
                }
                else
                {
                    resultPixels = await aiService.RunInferenceAsync(modelPath, _surface.Bitmap);

                    ApplyAiResult(resultPixels);
                }
                ShowToast("L_Toast_Apply_Success");
            }
            catch (OperationCanceledException)
            {
                // 处理用户主动取消
                DownloadProgressPopup.Finish();
            }
            catch (Exception ex)
            {
                if (ex is DllNotFoundException ||
                    ex.InnerException is DllNotFoundException ||
                    ex.Message.Contains("onnxruntime"))
                {
                    ShowToast(LocalizationManager.GetString("L_AI_Error_DllNotFound"));
                }
                else
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_RemoveBgFailed_Prefix"), ex.Message));
                }
            }
            finally
            {
                // --- 清理逻辑 ---
                _downloadCts?.Dispose();
                _downloadCts = null;
                _imageSize = statusText; // 恢复状态栏
                OnPropertyChanged(nameof(ImageSize));
                NotifyCanvasChanged();
                this.Focus();
            }
        }


        private void ApplyAiResult(byte[] newPixels)
        {
            _undo.PushFullImageUndo();

            // 更新 Bitmap
            _surface.Bitmap.Lock();
            _surface.Bitmap.WritePixels(
                new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight),
                newPixels,
                _surface.Bitmap.BackBufferStride,
                0
            );
            _surface.Bitmap.AddDirtyRect(new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight));
            _surface.Bitmap.Unlock();

            // 标记为脏，更新 UI
            _ctx.IsDirty = true;
            CheckDirtyState();
            SetUndoRedoButtonState();
        }


        private void TextAlign_Click(object sender, RoutedEventArgs e)
        {
            var mw = MainWindow.GetCurrentInstance();
            if (sender is ToggleButton btn && btn.Tag is string align)
            {
                // 实现互斥
                mw.TextMenu.AlignLeftBtn.IsChecked = (align == "Left");
                mw.TextMenu.AlignCenterBtn.IsChecked = (align == "Center");
                mw.TextMenu.AlignRightBtn.IsChecked = (align == "Right");

                mw.FontSettingChanged(sender, null);
            }
        }
        private void InsertTable_Click(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool is TextTool textTool && textTool._richTextBox != null)
            {
                textTool.InsertTableIntoCurrentBox();
            }
            else
            {
                ShowToast(LocalizationManager.GetString("L_Main_Toast_Info")); // "请先创建文本框"
            }
        }

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLoadingImage) return;
            if (e.ChangedButton != MouseButton.Left) return;
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseDown(pos, e);
        }
        private Thumb _opacitySliderThumb;

        private void OpacitySlider_Loaded(object sender, RoutedEventArgs e)
        {
            // 尝试在可视树中查找 Slider 内部的 Thumb
            if (OpacitySlider.Template != null)
            {
                _opacitySliderThumb = OpacitySlider.Template.FindName("Thumb", OpacitySlider) as Thumb;
            }
        }

        private void OpacitySlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            if (OpacitySlider.ToolTip is System.Windows.Controls.ToolTip toolTip)
            {
                toolTip.PlacementTarget = OpacitySlider;
                toolTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;

                // 打开时先更新一次位置
                UpdateToolTipOffset(toolTip);

                toolTip.IsOpen = true;
            }
        }

        private void OpacitySlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (OpacitySlider.ToolTip is System.Windows.Controls.ToolTip toolTip)
            {
                toolTip.IsOpen = false;
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacitySlider.ToolTip is System.Windows.Controls.ToolTip toolTip && toolTip.IsOpen)
            {
                UpdateToolTipOffset(toolTip);
            }
        }
        private void UpdateToolTipOffset(System.Windows.Controls.ToolTip toolTip)
        {
            // 1. 获取 Slider 的实际高度
            double sliderHeight = OpacitySlider.ActualHeight;

            double thumbSize = 20;

            // 3. 计算可滑动区域的有效高度
            double trackHeight = sliderHeight - thumbSize;

            double percent = (OpacitySlider.Value - OpacitySlider.Minimum) / (OpacitySlider.Maximum - OpacitySlider.Minimum);

            double offsetFromTop = (1.0 - percent) * trackHeight;

            toolTip.VerticalOffset = offsetFromTop;

        }


        private void OnCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isLoadingImage) return;
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseMove(pos, e);
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isLoadingImage) return;
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseUp(pos, e);
        }

        private void OnCanvasMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isLoadingImage) return;
            _router.CurrentTool?.StopAction(_ctx);
        }
        private void OnScrollContainerDoubleClick(object sender, MouseButtonEventArgs e)
        {

            if (!IsViewMode) { e.Handled = false; return; }
            if (e.ChangedButton != MouseButton.Left) return;
            if (_isPanning)
            {
                _isPanning = false;
                ScrollContainer.ReleaseMouseCapture();
                Mouse.OverrideCursor = null; // 恢复光标
            }
            MaximizeWindowHandler();
            e.Handled = true;
        }


        private void OnScrollContainerMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;
            if (e.ChangedButton != MouseButton.Left) return;
            if (Keyboard.IsKeyDown(Key.Space) && e.ChangedButton == MouseButton.Left || IsViewMode)
            {
                bool canScrollX = ScrollContainer.ScrollableWidth > 0.5;
                bool canScrollY = ScrollContainer.ScrollableHeight > 0.5;

                // 如果图片大于窗口，执行平移
                if (canScrollX || canScrollY)
                {
                    _isPanning = true;
                    _lastMousePosition = e.GetPosition(ScrollContainer);
                    ScrollContainer.CaptureMouse(); // 捕获鼠标，防止移出窗口失效

                    // 【修改点】设置抓紧光标
                    SetViewCursor(true);

                    e.Handled = true;
                    return;
                }
                else
                {
                    // 图片小于窗口，拖动窗口本身
                    if (e.ButtonState == MouseButtonState.Pressed)
                    {
                        try
                        {
                            this.DragMove();
                        }
                        catch { }
                        e.Handled = true;
                        return;
                    }
                }
            }
            if (IsViewMode) return;
            if (_router.CurrentTool is SelectTool selTool && selTool._selectionData != null)
            {
                // 检查点击的是否是左键
                if (e.ChangedButton != MouseButton.Left) return;

                if (IsVisualAncestorOf<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                Point ptInCanvas = e.GetPosition(CanvasWrapper);
                Point pixelPos = _ctx.ToPixel(ptInCanvas);

                bool hitHandle = selTool.HitTestHandle(pixelPos, selTool._selectionRect) != SelectTool.ResizeAnchor.None;
                bool hitInside = selTool.IsPointInSelection(pixelPos);

                if (hitHandle || hitInside)
                {
                    selTool.OnPointerDown(_ctx, ptInCanvas);

                    e.Handled = true;
                }
                else
                {
                    selTool.CommitSelection(_ctx);
                    selTool.ClearSelections(_ctx);
                    selTool.lag = 0;
                }
            }
        }

    }
}