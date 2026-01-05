
using System.ComponentModel;
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
        private void OnAddBorderClick(object sender, RoutedEventArgs e)
        {
            // 1. 检查画布是否存在
            if (_surface?.Bitmap == null) return;

            var bmp = _surface.Bitmap;
            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;
            int borderSize = 2; // 边框厚度

            // 如果图片太小，不足以画边框，直接返回
            if (w <= borderSize * 2 || h <= borderSize * 2)
            {
                ShowToast("图片尺寸过小，无法添加边框");
                return;
            }

            // 2. 准备撤销 (记录当前状态)
            _undo.BeginStroke();

            // 3. 锁定位图进行绘制
            bmp.Lock();
            try
            {
                // 获取当前前景色 (B, G, R, A)
                Color c = ForegroundColor;

                unsafe
                {
                    byte* basePtr = (byte*)bmp.BackBuffer;
                    int stride = bmp.BackBufferStride;

                    // 定义一个局部函数来快速填充矩形区域
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

                    // --- 绘制四条边 ---

                    // 1. 上边框 (全宽)
                    FillRect(0, 0, w, borderSize);

                    // 2. 下边框 (全宽)
                    FillRect(0, h - borderSize, w, borderSize);

                    // 3. 左边框 (中间部分，避免与上下重叠绘制，虽然重叠也没事)
                    FillRect(0, borderSize, borderSize, h - 2 * borderSize);

                    // 4. 右边框 (中间部分)
                    FillRect(w - borderSize, borderSize, borderSize, h - 2 * borderSize);
                }

                // 上
                var rTop = new Int32Rect(0, 0, w, borderSize);
                bmp.AddDirtyRect(rTop);
                _undo.AddDirtyRect(rTop);

                // 下
                var rBottom = new Int32Rect(0, h - borderSize, w, borderSize);
                bmp.AddDirtyRect(rBottom);
                _undo.AddDirtyRect(rBottom);

                // 左
                var rLeft = new Int32Rect(0, 0, borderSize, h);
                bmp.AddDirtyRect(rLeft);
                _undo.AddDirtyRect(rLeft);

                // 右
                var rRight = new Int32Rect(w - borderSize, 0, borderSize, h);
                bmp.AddDirtyRect(rRight);
                _undo.AddDirtyRect(rRight);
            }
            finally
            {
                bmp.Unlock();
            }

            // 5. 提交撤销 (计算差异并推入栈)
            _undo.CommitStroke();

            // 6. 标记文件已修改
            _isEdited = true;
            _ctx.IsDirty = true;

            // 7. 刷新界面状态
            NotifyCanvasChanged();
            ShowToast($"已添加 2px 边框");
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
               ShowToast($"裁切失败: {ex.Message}");
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
                    ShowToast("未选中图片区域"); // 假设你有ShowToast方法
                    return;
                }
                Color color = GetPixelColor(x, y);
                string hexCode = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                // 5. 复制到剪贴板
                System.Windows.Clipboard.SetText(hexCode);

                // 6. 提示用户
                ShowToast($"已复制颜色: {hexCode}");
            }
            catch (Exception ex)
            {
                // 容错处理
                System.Diagnostics.Debug.WriteLine(ex.Message);
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
            ShowToast($"已吸取颜色: #{c.R:X2}{c.G:X2}{c.B:X2}");
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
            if (_bitmap == null) return;

            // 1. 验证坐标是否在图片范围内
            int x = (int)_lastRightClickPosition.X;
            int y = (int)_lastRightClickPosition.Y;

            if (x < 0 || x >= _bitmap.PixelWidth || y < 0 || y >= _bitmap.PixelHeight)
            {
                // 如果点到了图片外面（灰白格子区域），默认取 (0,0) 或者不执行
                x = Math.Clamp(x, 0, _bitmap.PixelWidth - 1);
                y = Math.Clamp(y, 0, _bitmap.PixelHeight - 1);
            }

            // 2. 获取该点的颜色作为目标色
            Color targetColor = GetPixelColor(x, y);
            // 3. 执行抠图（容差 45 左右通常效果较好）
            ApplyColorKey(targetColor, 45);
        }
        private void ApplyColorKey(Color targetColor, int tolerance)
        {
            if (_surface?.Bitmap == null) return;

            // --- 关键：接入撤销系统 ---
            // 1. 告诉撤销管理器准备开始一次“操作”，它会拍摄快照
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

            // 3. 标记整个区域为 Dirty，以便 UI 刷新
            var fullRect = new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight);
            _bitmap.AddDirtyRect(fullRect);
            _bitmap.Unlock();

            // 4. 提交到撤销栈
            // 告诉管理器哪个区域变了。因为是全图滤镜，我们传入全图区域。
            _undo.AddDirtyRect(fullRect);
            _undo.CommitStroke();

            NotifyCanvasChanged();
        }

        private async void OnOcrClick(object sender, RoutedEventArgs e)
        {
            // 1. 检查是否有图
            if (_surface?.Bitmap == null) return;

            // 2. 确定要识别的区域
            // 如果有选区（Selection），则只识别选区；否则识别全图
            BitmapSource sourceToRecognize = _surface.Bitmap;
           
            if (_router.CurrentTool is SelectTool selTool && selTool.HasActiveSelection)
            {
                // 假设你有个方法能拿到选区的 CroppedBitmap
                sourceToRecognize = selTool.GetSelectionCroppedBitmap();
            }

            try
            {
                // 3. UI 提示
                var oldStatus = _imageSize;
                _imageSize = "正在提取文字...";
                this.Cursor = System.Windows.Input.Cursors.Wait;

                // 4. 调用服务
                var ocrService = new OcrService(); // 也可以作为单例注入
                string text = await ocrService.RecognizeTextAsync(sourceToRecognize);

                // 5. 结果处理
                if (!string.IsNullOrWhiteSpace(text))
                {
                    System.Windows.Clipboard.SetText(text);
                    ShowToast($"成功提取 {text.Length} 个字符到剪切板！");
                }
                else
                {
                    ShowToast("未识别到文字");
                }

                _imageSize = oldStatus;
            }
            catch (Exception ex)
            {
                ShowToast($"OCR 错误: {ex.Message}");
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

            // 1. 简单的加载状态提示 (可以用你的 imagebar 进度条或者状态栏)
            var statusText = _imageSize; // 暂存状态栏
            _imageSize = "正在准备 AI 模型...";
            OnPropertyChanged(nameof(ImageSize));

            try
            {
                var aiService = new AiService(_cacheDir);

                // 2. 准备模型 (带进度)
                var progress = new Progress<double>(p =>
                {
                    _imageSize = $"下载模型中: {p:F1}%";
                    OnPropertyChanged(nameof(ImageSize));
                });

                string modelPath = await aiService.PrepareModelAsync(progress);

                _imageSize = "AI 正在思考...";
                OnPropertyChanged(nameof(ImageSize));

                // 3. 执行推理 (后台线程)
                // 此时锁定UI防止用户乱动
                this.IsEnabled = false;

                var resultPixels = await aiService.RunInferenceAsync(modelPath, _surface.Bitmap);

                // 4. 应用结果并支持撤销
                ApplyAiResult(resultPixels);

            }
            catch (Exception ex)
            {
                ShowToast($"抠图失败: {ex.Message}");
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
            // 利用 UndoRedoManager 的全图撤销机制
            // 先把当前状态压入 Undo 栈
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
        // "Thumb" 是 WPF 默认 Slider 模板中滑块部件的标准名称
        // 如果你的 Win11VerticalSliderStyle 修改了模板，请确认名称是否为 "Thumb"
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

        // 核心计算逻辑
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
            if (!IsViewMode) return;
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
                bool canScrollX = ScrollContainer.ScrollableWidth > 0.5; // 用 0.5 容错
                bool canScrollY = ScrollContainer.ScrollableHeight > 0.5;

                // 情况 A: 图片比窗口大 -> 执行平移 (Pan)
                if (canScrollX || canScrollY)
                {
                    _isPanning = true;
                    _lastMousePosition = e.GetPosition(ScrollContainer);
                    ScrollContainer.CaptureMouse();

                    // 改变光标：抓手
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.ScrollAll;
                    e.Handled = true;
                    return;
                }
                else
                {
                    if (e.ButtonState == MouseButtonState.Pressed)
                    {
                        try
                        {
                            this.DragMove();
                        }
                        catch {  }
                        e.Handled = true;
                        return;
                    }
                }
            }
            if (IsViewMode) return;
            if (_router.CurrentTool is SelectTool selTool && selTool._selectionData != null)
            {
                // 1. 检查点击的是否是左键（通常右键用于弹出菜单，不应触发提交）
                if (e.ChangedButton != MouseButton.Left) return;

                // 2. 深度判定：点击来源是否属于滚动条的任何组成部分
                if (IsVisualAncestorOf<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject))
                {
                    return; // 点击在滚动条上（轨道、滑块、箭头等），不执行提交
                }

                // 获取逻辑坐标
                Point pt = e.GetPosition(CanvasWrapper);

                // 3. 判定：点击是否不在选区内，且不在缩放句柄上
                bool hitHandle = selTool.HitTestHandle(pt, selTool._selectionRect) != SelectTool.ResizeAnchor.None;
                bool hitInside = selTool.IsPointInSelection(pt);

                if (!hitHandle && !hitInside)
                {
                   // s(1);
                    selTool.CommitSelection(_ctx);
                    selTool.ClearSelections(_ctx);
                    selTool.lag = 0;
                }
            }
        }

    }
}