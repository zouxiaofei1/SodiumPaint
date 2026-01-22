
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static TabPaint.MainWindow;

//
//TabPaintCanvas画布事件处理cs
//

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
            catch
            {
                return true;
            }
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
            int borderSize = 2; // 边框厚度

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

                ApplyColorKey(targetColor, 45);
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_RemoveBgFailed_Prefix"), ex.Message));
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
                sourceToRecognize = selTool.GetSelectionCroppedBitmap();
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
        // 定义一个独立的方法，不依赖 RoutedEventArgs

        private async void OnRemoveBackgroundClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
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
                        FileName = "https://aka.ms/vs/17/release/vc_redist.x64.exe", // 微软官方直接下载链接
                        UseShellExecute = true
                    });
                }
                return;
            }
            var statusText = _imageSize; // 暂存状态栏
            _imageSize = LocalizationManager.GetString("L_AI_Status_Preparing");
            OnPropertyChanged(nameof(ImageSize));

            try
            {
                var aiService = new AiService(_cacheDir);

                // 2. 准备模型 (带进度)
                var progress = new Progress<double>(p =>
                {
                    _imageSize = string.Format(LocalizationManager.GetString("L_AI_Status_Downloading_Format"), p);
                    OnPropertyChanged(nameof(ImageSize));
                });

                string modelPath = await aiService.PrepareModelAsync(progress);

                _imageSize = LocalizationManager.GetString("L_AI_Status_Thinking");
                OnPropertyChanged(nameof(ImageSize));

                this.IsEnabled = false;

                var resultPixels = await aiService.RunInferenceAsync(modelPath, _surface.Bitmap);

                // 4. 应用结果并支持撤销
                ApplyAiResult(resultPixels);

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
                this.IsEnabled = true;
                _imageSize = statusText; // 恢复状态栏
                OnPropertyChanged(nameof(ImageSize));
                NotifyCanvasChanged(); this.Focus();
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
            var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
            if (sender is ToggleButton btn && btn.Tag is string align)
            {
                // 实现互斥
                mw.AlignLeftBtn.IsChecked = (align == "Left");
                mw.AlignCenterBtn.IsChecked = (align == "Center");
                mw.AlignRightBtn.IsChecked = (align == "Right");

                mw.FontSettingChanged(sender, null);
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