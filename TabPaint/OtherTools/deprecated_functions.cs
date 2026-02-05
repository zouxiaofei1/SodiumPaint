//using System.Diagnostics;
//using System.IO;
//using System.Windows.Media.Imaging;
//using System.Windows.Threading;
//using TabPaint;

//private async Task LoadImage(string filePath, string? sourcePath = null, bool lazyload = false)
//{
//    _isCurrentFileGif = false;
//    GifPlayerImage.Visibility = Visibility.Collapsed;
//    _loadImageCts?.Cancel();
//    _loadImageCts = new CancellationTokenSource();
//    var token = _loadImageCts.Token;
//    if (_progressCts != null)
//    {
//        _progressCts.Cancel();
//        _progressCts.Dispose();
//        _progressCts = null;
//    }
//    string fileToRead = sourcePath ?? filePath;
//    if (IsVirtualPath(filePath) && string.IsNullOrEmpty(sourcePath))
//    {
//        await LoadBlankCanvasAsync(filePath);
//        return;
//    }

//    if (!File.Exists(fileToRead))
//    {
//        ShowToast(string.Format(LocalizationManager.GetString("L_Toast_FileNotFound_Format"), fileToRead));
//        return;
//    }

//    try
//    {
//        var fileInfo = new FileInfo(fileToRead);
//        if (fileInfo.Length == 0)
//        {
//            await LoadBlankCanvasAsync(filePath, LocalizationManager.GetString("L_Load_Reason_Empty"));
//            return;
//        }
//    }
//    catch (Exception ex)
//    {
//        // 获取文件信息失败（可能是权限问题），记录日志并尝试继续，或者直接降级
//        Debug.WriteLine($"FileInfo check failed: {ex.Message}");
//    }

//    CancellationTokenSource progressCts = null;
//    Task progressTask = null;

//    try
//    {
//        // 步骤 1: 异步读取文件并快速获取最终尺寸
//        var imageBytes = await File.ReadAllBytesAsync(fileToRead, token);
//        if (token.IsCancellationRequested) return;
//        string sizeString = FormatFileSize(imageBytes.Length);
//        await Dispatcher.InvokeAsync(() => this.FileSize = sizeString);


//        var dimensions = await GetImageDimensionsAsync(imageBytes, filePath);
//        if (token.IsCancellationRequested) return;
//        if (dimensions == null)
//        {
//            await LoadBlankCanvasAsync(filePath, LocalizationManager.GetString("L_Load_Reason_Header"));
//            return;

//        }
//        var (originalWidth, originalHeight) = dimensions.Value;
//        long totalPixels = (long)originalWidth * originalHeight;
//        bool showProgress = totalPixels > AppConsts.PerformanceScorePixelThreshold * PerformanceScore;
//        if (showProgress)
//        {
//            // 创建新的进度条控制源
//            _progressCts = new CancellationTokenSource();
//            var progressToken = _progressCts.Token; // 捕获当前Token

//            // 启动模拟任务（不 await）
//            _ = SimulateProgressAsync(progressToken, totalPixels, (msg) =>
//            {
//                if (progressToken.IsCancellationRequested) return;

//                Dispatcher.Invoke(() =>
//                {
//                    if (!_isLoadingImage) return;

//                    _imageSize = msg;
//                    OnPropertyChanged(nameof(ImageSize));
//                });
//            });
//        }
//        else
//        {
//            // 小图直接显示尺寸
//            await Dispatcher.InvokeAsync(() =>
//            {
//                _imageSize = $"{originalWidth}×{originalHeight}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
//                OnPropertyChanged(nameof(ImageSize));
//            });
//        }
//        long _totalPixels = (long)originalWidth * originalHeight;
//        bool isHugeImage = _totalPixels > AppConsts.HugeImagePixelThreshold * PerformanceScore; // 阈值：5000万像素 (约8K分辨率以上)
//        if (isHugeImage)
//        {
//            try
//            {
//                await Task.Delay(AppConsts.ImageLoadDelayHugeMs, token);
//            }
//            catch (TaskCanceledException)
//            {
//                return;
//            }
//        }

//        // 步骤 2: 并行启动中等预览图和完整图的解码任务
//        string extension = System.IO.Path.GetExtension(filePath)?.ToLower();
//        Task<BitmapSource> previewTask;
//        Task<BitmapSource> fullResTask;

//        if (extension == ".svg")
//        {
//            previewTask = Task.Run<BitmapSource>(() => DecodeSvg(imageBytes, token), token);
//            fullResTask = previewTask.ContinueWith(t => t.Result, TaskContinuationOptions.OnlyOnRanToCompletion);
//        }
//        else
//        {
//            previewTask = Task.Run<BitmapSource>(() => DecodePreviewBitmap(imageBytes, token), token);
//            fullResTask = Task.Run<BitmapSource>(() => DecodeFullResBitmap(imageBytes, token), token);
//        }

//        // --- 阶段 0: (新增) 立即显示已缓存的缩略图 ---
//        bool isInitialLayoutSet = false;
//        // 查找与当前文件路径匹配的 Tab 项
//        var tabItem = FileTabs.FirstOrDefault(t => t.FilePath == filePath);

//        if (tabItem?.Thumbnail != null)
//        {
//            // 如果找到了并且它已经有缩略图，立即在UI线程上显示它
//            await Dispatcher.InvokeAsync(() =>
//            {
//                if (token.IsCancellationRequested) return;

//                RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);
//                BackgroundImage.Source = tabItem.Thumbnail;

//                // 更新窗口标题等基本信息
//                _currentFileName = IsVirtualPath(filePath)
//   ? (FileTabs.FirstOrDefault(t => t.FilePath == filePath)?.FileName ?? "未命名")
//   : System.IO.Path.GetFileName(filePath);
//                _currentFilePath = filePath;
//                UpdateWindowTitle();
//                FitToWindow(1);
//                CenterImage();
//                BackgroundImage.InvalidateVisual();
//                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
//                isInitialLayoutSet = true; // 标记初始布局已完成
//                _canvasResizer.UpdateUI();

//            });
//        }
//        // --- 阶段 1: 等待 480p 预览图并更新 ---
//        var previewBitmap = await previewTask;
//        if (token.IsCancellationRequested || previewBitmap == null) return;

//        await Dispatcher.InvokeAsync(() =>
//        {
//            if (token.IsCancellationRequested) return;
//            RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);

//            {
//                BackgroundImage.Source = previewBitmap;
//                _currentFileName = System.IO.Path.GetFileName(filePath);
//                _currentFilePath = filePath;
//                UpdateWindowTitle();
//                FitToWindow();
//                CenterImage();
//                BackgroundImage.InvalidateVisual();
//                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
//                _canvasResizer.UpdateUI();
//            }
//        });

//        // --- 阶段 2: 等待完整图并最终更新 ---
//        var fullResBitmap = await fullResTask;
//        if (_progressCts != null)
//        {
//            _progressCts.Cancel(); // 停止循环
//            _progressCts.Dispose();
//            _progressCts = null;
//        }
//        if (lazyload)
//        {
//            try
//            {
//                await Task.Delay(AppConsts.ImageLoadDelayLazyMs, token);
//            }
//            catch (TaskCanceledException)
//            {
//                return;
//            }
//        }
//        if (token.IsCancellationRequested || fullResBitmap == null) return;

//        // 获取元数据 (保持不变)
//        string metadataString = await GetImageMetadataInfoAsync(imageBytes, filePath, fullResBitmap);

//        await Dispatcher.InvokeAsync(() =>
//        {
//            if (token.IsCancellationRequested) return;
//            long currentMem = Process.GetCurrentProcess().PrivateMemorySize64;
//            if (currentMem > AppConsts.MemoryLimitForAggressiveRelease)
//            {
//                GC.Collect(2, GCCollectionMode.Forced, true);
//            }
//            // 1. 记录原始 DPI
//            _originalDpiX = fullResBitmap.DpiX;
//            _originalDpiY = fullResBitmap.DpiY;

//            // 计算统一后的尺寸
//            int width = fullResBitmap.PixelWidth;
//            int height = fullResBitmap.PixelHeight;

//            BitmapSource source = fullResBitmap;
//            if (source.Format != PixelFormats.Bgra32)
//            {
//                var formatted = new FormatConvertedBitmap();
//                formatted.BeginInit();
//                formatted.Source = fullResBitmap;
//                formatted.DestinationFormat = PixelFormats.Bgra32;
//                formatted.EndInit();
//                source = formatted;
//            }

//            _bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);

//            _bitmap.Lock();
//            try
//            {
//                source.CopyPixels(
//                    new Int32Rect(0, 0, width, height),
//                    _bitmap.BackBuffer, // 目标指针
//                    _bitmap.BackBufferStride * height, // 缓冲区总大小
//                    _bitmap.BackBufferStride // 步长
//                );

//                // 标记脏区以更新画面
//                _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
//            }
//            catch (Exception copyEx)
//            {
//                Debug.WriteLine("内存拷贝失败: " + copyEx.Message);
//            }
//            finally
//            {
//                _bitmap.Unlock();
//            }

//            source = null;
//            fullResBitmap = null;
//            RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.NearestNeighbor);

//            BackgroundImage.Source = _bitmap;
//            this.CurrentImageFullInfo = metadataString;

//            if (_surface == null) _surface = new CanvasSurface(_bitmap);
//            else _surface.Attach(_bitmap);

//            _undo?.ClearUndo();
//            _undo?.ClearRedo();
//            _isEdited = false;
//            SetPreviewSlider();
//            _imageSize = $"{_surface.Width}×{_surface.Height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
//            OnPropertyChanged(nameof(ImageSize));
//            _hasUserManuallyZoomed = false;
//            FitToWindow();
//            CenterImage();
//            _canvasResizer.UpdateUI();

//            string ext = System.IO.Path.GetExtension(fileToRead)?.ToLower();
//            _isCurrentFileGif = (ext == ".gif");

//            if (_isCurrentFileGif && IsViewMode)
//            {
//                AnimationBehavior.SetSourceUri(GifPlayerImage, new Uri(fileToRead));
//                GifPlayerImage.Visibility = Visibility.Visible;   //if (SettingsManager.Instance.Current.StartInViewMode)
//                var controller = AnimationBehavior.GetAnimator(GifPlayerImage);
//                if (controller != null)
//                {
//                    controller.Play();
//                }
//            }
//            else
//            {
//                // 如果不是 GIF，清空播放器资源
//                AnimationBehavior.SetSourceUri(GifPlayerImage, null);
//                GifPlayerImage.Visibility = Visibility.Collapsed;
//                BackgroundImage.Visibility = Visibility.Visible;
//            }


//        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); // 稍微降低优先级，确保UI先响应
//    }
//    catch (OperationCanceledException)
//    {
//        Debug.WriteLine($"Image load for {filePath} was canceled.");
//    }
//    catch (Exception ex)
//    {
//        Debug.WriteLine($"Error loading image {filePath}: {ex.Message}");

//        // 取消可能的进度条
//        _progressCts?.Cancel();
//        await LoadBlankCanvasAsync(filePath, LocalizationManager.GetString("L_Load_Reason_Corrupt"));
//    }
//    finally
//    {
//        // 清理进度条资源
//        progressCts?.Dispose();
//    }
//}