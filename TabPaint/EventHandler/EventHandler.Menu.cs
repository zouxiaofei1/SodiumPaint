
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TabPaint事件处理cs
//menu及位于那一行的所有东西
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnAppTitleBarLogoMiddleClick(object sender, RoutedEventArgs e)
        {
            if (_currentTabItem != null)
            {
                CloseTab(_currentTabItem);
            }
        }

        private void OnNewWindowClick(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow newWindow = new MainWindow(string.Empty);

                newWindow.Left = this.Left + 20;
                newWindow.Top = this.Top + 20;

                newWindow.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"{LocalizationManager.GetString("L_Common_Error")}: {ex.Message}");
            }
        }

        private void OnInvertColorsClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            _undo.PushFullImageUndo();

            var bmp = _surface.Bitmap;
            bmp.Lock();
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)bmp.BackBuffer;
                    int stride = bmp.BackBufferStride;
                    int height = bmp.PixelHeight;
                    int width = bmp.PixelWidth;

                    // 并行处理反色
                    Parallel.For(0, height, y =>
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            row[x * 4] = (byte)(255 - row[x * 4]);     // B
                            row[x * 4 + 1] = (byte)(255 - row[x * 4 + 1]); // G
                            row[x * 4 + 2] = (byte)(255 - row[x * 4 + 2]); // R
                        }
                    });
                }
                bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            }
            finally
            {
                bmp.Unlock();
            }
            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            ShowToast("L_Toast_Effect_Inverted");
        }

        private void OnAutoLevelsClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            // 1. 记录 Undo
            _undo.PushFullImageUndo();

            var bmp = _surface.Bitmap;
            bmp.Lock();
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)bmp.BackBuffer;
                    int stride = bmp.BackBufferStride;
                    int height = bmp.PixelHeight;
                    int width = bmp.PixelWidth;
                    long totalPixels = width * height;

                    int[] histR = new int[256];
                    int[] histG = new int[256];
                    int[] histB = new int[256];

                    // 采样统计 (为了性能，如果是超大图可以考虑跳跃采样，这里做全采样)
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            histB[row[x * 4]]++;
                            histG[row[x * 4 + 1]]++;
                            histR[row[x * 4 + 2]]++;
                        }
                    }
                    float clipPercent = 0.005f;
                    int threshold = (int)(totalPixels * clipPercent);

                    void GetMinMax(int[] hist, out byte min, out byte max)
                    {
                        min = 0; max = 255;
                        int count = 0;
                        // 找 min
                        for (int i = 0; i < 256; i++)
                        {
                            count += hist[i];
                            if (count > threshold) { min = (byte)i; break; }
                        }
                        // 找 max
                        count = 0;
                        for (int i = 255; i >= 0; i--)
                        {
                            count += hist[i];
                            if (count > threshold) { max = (byte)i; break; }
                        }
                    }

                    GetMinMax(histB, out byte minB, out byte maxB);
                    GetMinMax(histG, out byte minG, out byte maxG);
                    GetMinMax(histR, out byte minR, out byte maxR);

                    // --- 第三步：生成查找表 (LUT) 优化性能 ---
                    byte[] lutR = BuildLevelLut(minR, maxR);
                    byte[] lutG = BuildLevelLut(minG, maxG);
                    byte[] lutB = BuildLevelLut(minB, maxB);

                    // --- 第四步：应用映射 ---
                    Parallel.For(0, height, y =>
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            row[x * 4] = lutB[row[x * 4]];     // B
                            row[x * 4 + 1] = lutG[row[x * 4 + 1]]; // G
                            row[x * 4 + 2] = lutR[row[x * 4 + 2]]; // R
                        }
                    });
                }
                bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            }
            finally
            {
                bmp.Unlock();
            }

            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            ShowToast("L_Toast_Effect_AutoLevels");
        }
        private byte[] BuildLevelLut(byte min, byte max)
        {
            byte[] lut = new byte[256];
            if (max <= min) // 避免除以零或异常情况
            {
                for (int i = 0; i < 256; i++) lut[i] = (byte)i;
                return lut;
            }

            float scale = 255.0f / (max - min);
            for (int i = 0; i < 256; i++)
            {
                if (i <= min) lut[i] = 0;
                else if (i >= max) lut[i] = 255;
                else
                {
                    lut[i] = (byte)((i - min) * scale);
                }
            }
            return lut;
        }
        private void OnRecentFileClick(object sender, string filePath)
        {
            if (File.Exists(filePath))
            {
                string[] files = [filePath];
                OpenFilesAsNewTabs(files);

                UpdateImageBarSliderState();
            }
            else
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_FileNotFound_Format"), filePath));
            }
        }

        private void OnClearRecentFilesClick(object sender, EventArgs e)
        {
            SettingsManager.Instance.ClearRecentFiles();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)// 打开设置窗口
        {
            var settingsWindow = new SettingsWindow();
            TabPaint.SettingsManager.Instance.Current.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "ViewInterpolationThreshold" ||
                    e.PropertyName == "PaintInterpolationThreshold")
                {
                    this.Dispatcher.Invoke(() =>{ RefreshBitmapScalingMode(); });
                }
            };

            settingsWindow.ProgramVersion = this.ProgramVersion;
            settingsWindow.Owner = this; // 设置主窗口为父窗口，实现模态
            settingsWindow.ShowDialog();
        }



        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            // 如果是空路径 OR 是虚拟路径，都视为"从未保存过"，走另存为
            if (string.IsNullOrEmpty(_currentFilePath) || IsVirtualPath(_currentFilePath)) OnSaveAsClick(sender, e);
            else SaveBitmap(_currentFilePath);
        }


        private void OnSaveAsClick(object sender, RoutedEventArgs e)
        {
            // 1. 准备默认文件名
            // 如果是新建的，DisplayName 会返回 "未命名 1"，如果是已有的，会返回原文件名
            string defaultName = _currentTabItem?.DisplayName ?? "image";
            if (!defaultName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                defaultName += ".png";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = PicFilterString,
                FileName = defaultName
            };

            // 2. 需求2：默认位置为打开的文件夹 (即 _currentFilePath 所在目录)
            string initialDir = "";
            if (!string.IsNullOrEmpty(_currentFilePath))
                initialDir = System.IO.Path.GetDirectoryName(_currentFilePath);
            else if (_imageFiles != null && _imageFiles.Count > 0)
                initialDir = System.IO.Path.GetDirectoryName(_imageFiles[0]);

            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                dlg.InitialDirectory = initialDir;

            if (dlg.ShowDialog() == true)
            {
                string newPath = dlg.FileName;
                SaveBitmap(newPath); // 实际保存文件

                // 3. 更新状态
                _currentFilePath = newPath;
                _currentFileName = System.IO.Path.GetFileName(newPath);

                if (_currentTabItem != null)
                {
                    // 这里会触发 FilePath 的 setter，进而自动触发 DisplayName 的通知
                    _currentTabItem.FilePath = newPath;

                    if (_currentTabItem.IsNew)
                    {
                        _currentTabItem.IsNew = false; // 也会触发 DisplayName 更新通知
                        if (!_imageFiles.Contains(newPath)) _imageFiles.Add(newPath);
                    }
                    else if (!_imageFiles.Contains(newPath))
                    {
                        _imageFiles.Add(newPath);
                    }

                    _currentImageIndex = _imageFiles.IndexOf(newPath);
                   // s(_currentImageIndex);
                }

                _isFileSaved = true;
                UpdateWindowTitle();
            }
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            // 确保 SelectTool 是当前工具
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CopySelection(_ctx);
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CutSelection(_ctx, true);
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.PasteSelection(_ctx, false);

        }

        private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();
        private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();
        private async void OnBrightnessContrastExposureClick(object sender, RoutedEventArgs e)
        {
            if (_surface.Bitmap == null) return;

            _router.CleanUpSelectionandShape();

            var dialog = new AdjustBCEWindow(_surface.Bitmap, WatermarkPreviewLayer)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                _undo.PushFullImageUndo();

                try
                {
                    dialog.ApplyToFullImage(_surface.Bitmap);

                    // 6. 标记为脏并刷新状态
                    CheckDirtyState();
                    SetUndoRedoButtonState();
                }
                finally
                {
                }
            }
            else
            {
            }
        }


        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if(!_programClosed)OnClosing();
        }
        private void CropMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool is SelectTool selectTool)
            {
                selectTool.CropToSelection(_ctx);
                SetCropButtonState();
                NotifyCanvasChanged();
                _canvasResizer.UpdateUI();
            }
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            MaximizeWindowHandler();
        }
        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ThicknessSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Visible;
            UpdateThicknessPreviewPosition(); // 初始定位

            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(ThicknessSlider.Value);
        }

        private void ThicknessSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Collapsed;

            ThicknessTip.Visibility = Visibility.Collapsed;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialLayoutComplete) return;
            if (ThicknessTip == null || ThicknessTipText == null || ThicknessSlider == null||ThicknessSlider.Visibility!=Visibility.Visible)
                return;
            if (_isUpdatingToolSettings)
            {
                ThicknessTip.Visibility = Visibility.Collapsed;
                return;
            }
            double realSize = SettingsManager.Instance.Current.PenThickness;

            SetThicknessSlider_Pos(e.NewValue);
            UpdateThicknessPreviewPosition();

            ThicknessTipText.Text = $"{(int)Math.Round(realSize)}"+ LocalizationManager.GetString("L_Main_Unit_Pixel");

            ThicknessTip.Visibility = Visibility.Visible;
        }
      
        private async void OnOpenWorkspaceClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择图片以建立新工作区",
                Filter = PicFilterString,
                Multiselect = false     
            };

            if (dlg.ShowDialog() == true)
            {
                string file = dlg.FileName;
                SettingsManager.Instance.AddRecentFile(file);

                await SwitchWorkspaceToNewFile(file);
                UpdateImageBarSliderState();
            }
        }


        private void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = PicFilterString,
                 Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                string[] files= dlg.FileNames;
                OpenFilesAsNewTabs(files);
                foreach (var file in files)
                    SettingsManager.Instance.AddRecentFile(file);
                UpdateImageBarSliderState();
            }
        }
        private void OnColorTempTintSaturationClick(object sender, RoutedEventArgs e)
        {
            if (_surface.Bitmap == null) return;

            _router.CleanUpSelectionandShape();

            var oldBitmap = _surface.Bitmap;
            var undoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);
            byte[] undoPixels = new byte[undoRect.Height * oldBitmap.BackBufferStride];
            oldBitmap.CopyPixels(undoRect, undoPixels, oldBitmap.BackBufferStride, 0);

            var dialog = new AdjustTTSWindow(_surface.Bitmap, WatermarkPreviewLayer)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                var newBitmap = _surface.Bitmap;
                var redoPixels = new byte[undoRect.Height * newBitmap.BackBufferStride];
                newBitmap.CopyPixels(undoRect, redoPixels, newBitmap.BackBufferStride, 0);

                _undo.PushTransformAction(undoRect, undoPixels, undoRect, redoPixels);

                NotifyCanvasChanged();
                SetUndoRedoButtonState();
                CheckDirtyState();
            }
            else
            {
            }
        }

        private void OnConvertToBlackAndWhiteClick(object sender, RoutedEventArgs e)
        {

            if (_bitmap == null) return;  // 1. 检查图像是否存在
            _router.CleanUpSelectionandShape();
            _undo.PushFullImageUndo();
            ConvertToBlackAndWhite(_bitmap);
            CheckDirtyState();
            SetUndoRedoButtonState();
            ShowToast("L_Toast_Effect_BW");

        }
        private async void OnResizeCanvasClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape(); // 清理选区

            // 获取当前原始尺寸
            int originalW = _surface.Bitmap.PixelWidth;
            int originalH = _surface.Bitmap.PixelHeight;

            var dialog = new ResizeCanvasDialog(originalW, originalH);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                int targetWidth = dialog.ImageWidth;
                int targetHeight = dialog.ImageHeight;
                bool isCanvasMode = dialog.IsCanvasResizeMode;
                bool keepRatio = dialog.IsAspectRatioLocked;

                // 计算缩放比例 (仅用于 Resample 模式)
                double scaleX = (double)targetWidth / originalW;
                double scaleY = (double)targetHeight / originalH;

                // 1. 处理当前图片 (保持原有逻辑，立即响应)
                if (isCanvasMode) ResizeCanvasDimensions(targetWidth, targetHeight);
                else ResizeCanvas(targetWidth, targetHeight);

                CheckDirtyState();
                if (_canvasResizer != null) _canvasResizer.UpdateUI();

                // 2. 如果勾选了"应用到所有"，则启动后台批量任务
                if (dialog.ApplyToAll)
                {
                    // 传递必要的参数：目标尺寸、缩放比例、模式、是否保持比例
                    await BatchResizeImages(targetWidth, targetHeight, scaleX, scaleY, isCanvasMode, keepRatio);
                }
            }
        }
        private async Task BatchResizeImages(int targetW, int targetH, double refScaleX, double refScaleY, bool isCanvasMode, bool keepRatio)
        {
            // 1. 获取需要处理的 Tab 数据 (排除当前正在编辑的 Tab)
            string currentTabId = _currentTabItem?.Id;

            var tasksInfo = FileTabs
                .Where(t => t.Id != currentTabId)
                .Select(t => new
                {
                    Tab = t,
                    // 优先使用备份文件(未保存的修改)，否则使用原文件
                    SourcePath = (!string.IsNullOrEmpty(t.BackupPath) && File.Exists(t.BackupPath)) ? t.BackupPath : t.FilePath
                })
                .Where(x => !string.IsNullOrEmpty(x.SourcePath) && File.Exists(x.SourcePath))
                .ToList();

            if (tasksInfo.Count == 0) return;

            ShowToast(LocalizationManager.GetString("L_Toast_BatchResizeStart") ?? $"Resizing {tasksInfo.Count} images...");

            // 2. 并发控制
            int maxDegreeOfParallelism = Environment.ProcessorCount;
            using (var semaphore = new SemaphoreSlim(maxDegreeOfParallelism))
            {
                var tasks = new List<Task>();

                foreach (var info in tasksInfo)
                {
                    info.Tab.IsLoading = true;

                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            string newCachePath = null;
                            BitmapSource thumbnailResult = null;

                            // 独立线程进行渲染和IO操作
                            Thread renderThread = new Thread(() =>
                            {
                                try
                                {
                                    // A. 加载图片
                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.UriSource = new Uri(info.SourcePath);
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.EndInit();
                                    bmp.Freeze();

                                    BitmapSource resultBmp = null;

                                    // B. 计算新尺寸
                                    if (isCanvasMode)
                                    {
                                        resultBmp = ResizeBitmapCanvas(bmp, targetW, targetH);
                                    }
                                    else
                                    {
                                        // 采样模式 (Resample)
                                        int finalW, finalH;

                                        if (keepRatio)
                                        {
                                            // 核心逻辑：按比例 -> 使用参考图片的缩放倍率
                                            // 例如：第一张图从 1000->500 (0.5倍)，那么这张 2000 的图就变成 1000
                                            finalW = (int)Math.Round(bmp.PixelWidth * refScaleX);
                                            finalH = (int)Math.Round(bmp.PixelHeight * refScaleY); // 如果锁链开启，ScaleX应该等于ScaleY

                                            // 防止无效尺寸
                                            finalW = Math.Max(1, finalW);
                                            finalH = Math.Max(1, finalH);
                                        }
                                        else
                                        {
                                            // 未锁定比例 -> 强制拉伸到指定像素
                                            finalW = targetW;
                                            finalH = targetH;
                                        }

                                        // 执行缩放
                                        resultBmp = new TransformedBitmap(bmp, new ScaleTransform(
                                            (double)finalW / bmp.PixelWidth,
                                            (double)finalH / bmp.PixelHeight));
                                    }

                                    resultBmp.Freeze();

                                    // C. 保存结果
                                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                                    string fileName = $"{info.Tab.Id}_resize_{DateTime.Now.Ticks}.png";
                                    string fullPath = Path.Combine(_cacheDir, fileName);

                                    using (var fs = new FileStream(fullPath, FileMode.Create))
                                    {
                                        BitmapEncoder encoder = new PngBitmapEncoder();
                                        encoder.Frames.Add(BitmapFrame.Create(resultBmp));
                                        encoder.Save(fs);
                                    }

                                    newCachePath = fullPath;

                                    // D. 生成缩略图
                                    if (resultBmp.PixelWidth > 200)
                                    {
                                        var scale = 200.0 / resultBmp.PixelWidth;
                                        var thumb = new TransformedBitmap(resultBmp, new ScaleTransform(scale, scale));
                                        thumb.Freeze();
                                        thumbnailResult = thumb;
                                    }
                                    else
                                    {
                                        thumbnailResult = resultBmp;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Batch Resize Error: {ex.Message}");
                                }
                            });

                            renderThread.SetApartmentState(ApartmentState.STA); // WPF 图形处理必须 STA
                            renderThread.IsBackground = true;
                            renderThread.Start();
                            renderThread.Join();

                            // E. UI更新
                            if (!string.IsNullOrEmpty(newCachePath))
                            {
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    var tab = info.Tab;
                                    tab.BackupPath = newCachePath;
                                    tab.IsDirty = true; // 标记为未保存
                                    tab.LastBackupTime = DateTime.Now;
                                    if (thumbnailResult != null) tab.Thumbnail = thumbnailResult;
                                    tab.IsLoading = false;
                                });
                            }
                            else
                            {
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => info.Tab.IsLoading = false);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
            }

            SaveSession();
            ShowToast(LocalizationManager.GetString("L_Toast_BatchResizeComplete") ?? "Batch resize complete.");
        }

        private BitmapSource ResizeBitmapCanvas(BitmapSource source, int targetW, int targetH)
        {
            var rtb = new RenderTargetBitmap(targetW, targetH, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                // 绘制背景 (透明或白色，取决于需求，这里留空即透明)

                // 计算居中位置
                double x = (targetW - source.PixelWidth) / 2.0;
                double y = (targetH - source.PixelHeight) / 2.0;

                // 绘制原图
                ctx.DrawImage(source, new Rect(x, y, source.PixelWidth, source.PixelHeight));
            }
            rtb.Render(dv);
            return rtb;
        }

        private async void OnWatermarkClick(object sender, RoutedEventArgs e)
        {
            var oldBitmap = _surface.Bitmap;
            var undoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);

            byte[] undoPixels = new byte[undoRect.Height * oldBitmap.BackBufferStride];
            oldBitmap.CopyPixels(undoRect, undoPixels, oldBitmap.BackBufferStride, 0);

            // 2. 打开窗口
            var dlg = new WatermarkWindow(_surface.Bitmap, WatermarkPreviewLayer)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                var newBitmap = _surface.Bitmap;
                var redoPixels = new byte[undoRect.Height * newBitmap.BackBufferStride];
                newBitmap.CopyPixels(undoRect, redoPixels, newBitmap.BackBufferStride, 0);

                // 推送 Undo
                _undo.PushTransformAction(undoRect, undoPixels, undoRect, redoPixels);

                NotifyCanvasChanged();
                SetUndoRedoButtonState();

                if (dlg.ApplyToAll)
                {
                    await ApplyWatermarkToAllTabs(dlg.CurrentSettings);
                }
            }
            else
            {
                NotifyCanvasChanged();
            }
        }
        // 批量应用水印逻辑
        private async Task ApplyWatermarkToAllTabs(WatermarkSettings settings)
        {
            if (settings == null) return;

            // 1. 获取需要处理的 Tab 数据
            // 注意：不能在后台线程直接访问 FileTabs (ObservableCollection)，需要先在 UI 线程提取出数据副本
            string currentTabId = _currentTabItem?.Id;

            // 提取纯数据对象(DTO)以传入后台，避免跨线程访问 UI 对象
            var tasksInfo = FileTabs
                .Where(t => t.Id != currentTabId)
                .Select(t => new
                {
                    Tab = t,
                    // 优先使用备份文件(未保存的修改)，否则使用原文件
                    SourcePath = (!string.IsNullOrEmpty(t.BackupPath) && File.Exists(t.BackupPath)) ? t.BackupPath : t.FilePath
                })
                .Where(x => !string.IsNullOrEmpty(x.SourcePath) && File.Exists(x.SourcePath))
                .ToList();

            if (tasksInfo.Count == 0) return;

            ShowToast(LocalizationManager.GetString("L_Toast_BatchStart") ?? $"Processing {tasksInfo.Count} images...");

            // 2. 准备并发控制
            // 限制并发数为 CPU 核心数，避免创建过多 RenderTargetBitmap 耗尽显存/内存
            int maxDegreeOfParallelism = Environment.ProcessorCount;
            using (var semaphore = new SemaphoreSlim(maxDegreeOfParallelism))
            {
                var tasks = new List<Task>();

                foreach (var info in tasksInfo)
                {
                    // 更新 Tab 状态为加载中
                    info.Tab.IsLoading = true;

                    // 创建异步任务
                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync(); // 等待信号量
                        try
                        {
                            string newCachePath = null;
                            BitmapSource thumbnailResult = null;

                            // 定义一个线程任务来执行渲染
                            Thread renderThread = new Thread(() =>
                            {
                                try
                                {
                                    // A. 加载图片 (需要在新线程重新加载，不能复用 UI 线程的 Bitmap)
                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.UriSource = new Uri(info.SourcePath);
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.EndInit();
                                    bmp.Freeze(); // 冻结以便处理

                                    // B. 应用水印 (这是静态纯计算方法)
                                    var renderedBmp = WatermarkWindow.ApplyWatermarkToBitmap(bmp, settings);

                                    // C. 保存到缓存
                                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                                    string fileName = $"{info.Tab.Id}_{DateTime.Now.Ticks}.png"; // 加时间戳防止重名冲突
                                    string fullPath = Path.Combine(_cacheDir, fileName);

                                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                                    {
                                        BitmapEncoder encoder = new PngBitmapEncoder();
                                        encoder.Frames.Add(BitmapFrame.Create(renderedBmp));
                                        encoder.Save(fileStream);
                                    }

                                    newCachePath = fullPath;

                                    if (renderedBmp.PixelWidth > 200)
                                    {
                                        var scale = 200.0 / renderedBmp.PixelWidth;
                                        var thumb = new TransformedBitmap(renderedBmp, new ScaleTransform(scale, scale));
                                        thumb.Freeze();
                                        thumbnailResult = thumb;
                                    }
                                    else
                                    {
                                        thumbnailResult = renderedBmp; // 已经 Freeze 过了
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Thread Render Error: {ex.Message}");
                                }
                            });

                            // 设置为 STA 模式，这是 WPF 渲染必须的
                            renderThread.SetApartmentState(ApartmentState.STA);
                            renderThread.IsBackground = true;
                            renderThread.Start();
                            renderThread.Join(); // 等待线程结束

                            // === 回到 UI 线程更新 ===
                            if (!string.IsNullOrEmpty(newCachePath))
                            {
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    var tab = info.Tab;
                                    tab.BackupPath = newCachePath;
                                    tab.IsDirty = true;
                                    tab.LastBackupTime = DateTime.Now;

                                    // 更新缩略图
                                    if (thumbnailResult != null)
                                    {
                                        tab.Thumbnail = thumbnailResult; // 需要你的 ViewModel 支持
                                    }

                                    tab.IsLoading = false;
                                });
                            }
                            else
                            {
                                // 失败处理
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => info.Tab.IsLoading = false);
                            }
                        }
                        finally
                        {
                            semaphore.Release(); // 释放信号量
                        }
                    });

                    tasks.Add(task);
                }

                // 等待所有任务完成
                await Task.WhenAll(tasks);
            }

            SaveSession(); // 保存会话状态
            ShowToast(LocalizationManager.GetString("L_Toast_BatchComplete") ?? "Batch watermark applied.");
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            CreateNewTab(TabInsertPosition.AfterCurrent,true);
        }
    }
}