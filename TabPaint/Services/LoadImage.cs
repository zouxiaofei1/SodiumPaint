using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using XamlAnimatedGif; // 添加这一行

//
//图片加载队列机制
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private readonly object _queueLock = new object();

        // “待办事项”：只存放最新的一个图片加载请求
        private string _pendingFilePath = null;

        // 标志位：表示图像加载“引擎”是否正在工作中
        private bool _isProcessingQueue = false;
        private CancellationTokenSource _loadImageCts;
        private CancellationTokenSource _progressCts;
        public async Task OpenImageAndTabs(string filePath, bool refresh = false, bool lazyload = false, bool forceFolderScan = false, bool nobackup = false)
        {
           
            _isLoadingImage = true;
            OnPropertyChanged("IsLoadingImage");
            try
            {
                bool autoLoad = SettingsManager.Instance.Current.AutoLoadFolderImages || forceFolderScan;
                if (autoLoad && _currentImageIndex == -1 && _currentFileExists)
                {
                    await ScanFolderImagesAsync(filePath);
                }
                else if (!autoLoad && _currentImageIndex == -1 && !IsVirtualPath(filePath))
                {
                    _imageFiles = new List<string> { filePath };
                }

                if (!nobackup) TriggerBackgroundBackup();

                if (IsVirtualPath(filePath) && !_imageFiles.Contains(filePath))
                {
                    _imageFiles.Add(filePath);
                }
                if (File.Exists(filePath))
                {
                    SettingsManager.Instance.AddRecentFile(filePath);
                }
                int newIndex = _imageFiles.IndexOf(filePath);
                _currentImageIndex = newIndex;
                RefreshTabPageAsync(_currentImageIndex, refresh);

                var current = FileTabs.FirstOrDefault(t => t.FilePath == filePath);

                if (current != null)
                {
                    foreach (var tab in FileTabs) tab.IsSelected = false;
                    current.IsSelected = true;
                    _currentTabItem = current;
                }
                UpdateImageBarVisibilityState();
                // --- 智能加载逻辑 ---
                string fileToLoad = filePath;
                bool isFileLoadedFromCache = false;
                string actualSourcePath = null; // 新增变量
                                                // 检查是否有缓存
                if (current != null)                              // 对于虚拟文件，如果它有 BackupPath (例如从 Session 恢复的)，必须读 BackupPath
                    if (_activeSaveTasks.TryGetValue(current.Id, out Task? pendingSave))
                    {
                        // 显示一个小的加载状态或日志
                        // Debug.WriteLine("Waiting for background save to finish...");
                        try
                        {
                            await pendingSave;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Wait save failed: " + ex.Message);
                        }
                    }

                // 等待结束后，重新检查 BackupPath，因为它刚刚被后台线程更新了！
                if (current != null && (current.IsDirty || current.IsNew) && !string.IsNullOrEmpty(current.BackupPath))
                {
                    if (File.Exists(current.BackupPath))
                    {
                        actualSourcePath = current.BackupPath;
                        // Debug.WriteLine($"Restoring from backup: {actualSourcePath}");
                    }
                }
                await LoadImage(fileToLoad, actualSourcePath, lazyload);

                ResetDirtyTracker();

                if (isFileLoadedFromCache)
                {
                    _savedUndoPoint = -1;
                    CheckDirtyState();
                }

            }
            finally
            {
                // [新增] 无论上面发生什么错误，最终都必须关闭加载状态
                _isLoadingImage = false;
                OnPropertyChanged("IsLoadingImage");
            }
        }
        public void RequestImageLoad(string filePath)
        {
            lock (_queueLock)
            {

                _pendingFilePath = filePath;
                if (!_isProcessingQueue)
                {
                    _isProcessingQueue = true;
                    _ = ProcessImageLoadQueueAsync();
                }
            }
        }
        private bool _lazyLoad = false;
        private async Task ProcessImageLoadQueueAsync()
        {
            _isLoadingImage = true;
            OnPropertyChanged("IsLoadingImage"); // 如果你有绑定属性，请通知界面

            try
            {
                while (true)
                {
                    string filePathToLoad;

                    // 进入临界区，检查并获取下一个任务
                    lock (_queueLock)
                    {
                        // 1. 检查是否还有待办事项
                        if (_pendingFilePath == null)
                        {
                            _isProcessingQueue = false;
                            break; // 退出循环
                        }
                        filePathToLoad = _pendingFilePath;
                        _pendingFilePath = null;
                    }
                    //   _lazyLoad = true;
                    await LoadAndDisplayImageInternalAsync(filePathToLoad);
                    //  _lazyLoad = false;
                }
            }
            finally
            {
                _isLoadingImage = false;
                OnPropertyChanged("IsLoadingImage");
            }
        }
        private async Task LoadAndDisplayImageInternalAsync(string filePath)
        {
            try
            {
                OpenImageAndTabs(filePath, lazyload: true);

            }
            catch (Exception ex)
            {
                // 最好有异常处理
                Debug.WriteLine($"Error loading image {filePath}: {ex.Message}");
            }
        }
        // 1. 在类级别定义支持的扩展名（静态只读，利用HashSet的哈希查找，极快）
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".png", ".jpg", ".jpeg", ".tif", ".tiff",
    ".gif", ".webp", ".bmp", ".ico", ".heic",".jfif"
};

        // 2. 改为异步方法
        private async Task ScanFolderImagesAsync(string filePath)
        {
            try
            {
                if (IsVirtualPath(filePath) || string.IsNullOrEmpty(filePath)) return;

                string folder = System.IO.Path.GetDirectoryName(filePath)!;
                if (!File.Exists(_currentFilePath) && !Directory.Exists(_currentFilePath)) { MainImageBar.IsSingleTabMode = true; }
                // 放到后台线程处理
                var sortedFiles = await Task.Run(() =>
                {
                    return Directory.EnumerateFiles(folder)
                        .Where(f =>
                        {
                            var ext = System.IO.Path.GetExtension(f);
                            return ext != null && AllowedExtensions.Contains(ext);
                        })
                        .OrderBy(f => f, NaturalStringComparer.Default)
                        // --- 修改结束 ---
                        .ToList();
                });

                var virtualPaths = FileTabs.Where(t => IsVirtualPath(t.FilePath))
                                            .Select(t => t.FilePath)
                                            .ToList();

                // 预分配内存
                var combinedFiles = new List<string>(virtualPaths.Count + sortedFiles.Count);
                combinedFiles.AddRange(virtualPaths);
                combinedFiles.AddRange(sortedFiles);

                _imageFiles = combinedFiles;

                _currentImageIndex = _imageFiles.IndexOf(filePath);

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scan Error: {ex.Message}");
            }
        }



        private BitmapImage DecodePreviewBitmap(byte[] imageBytes, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            try
            {
                using var ms = new System.IO.MemoryStream(imageBytes);

                // 1. 先只读取元数据获取原始尺寸，不解码像素，速度极快
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                int originalWidth = decoder.Frames[0].PixelWidth;

                // 重置流位置以供 BitmapImage 读取
                ms.Position = 0;

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;

                // 2. 只有当原图宽度大于 480 时才进行降采样
                if (originalWidth > 480)
                {
                    img.DecodePixelWidth = 480;
                }
                // 否则不设置 DecodePixelWidth（默认为0，即加载原图尺寸），避免小图报错或被拉伸

                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                // 预览图解码失败可以忽略，直接返回 null，不影响主图尝试
                return null;
            }
        }


        private BitmapImage DecodeFullResBitmap(byte[] imageBytes, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            try
            {

                using var ms = new System.IO.MemoryStream(imageBytes);

                // 先用解码器获取原始尺寸
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                int originalWidth = decoder.Frames[0].PixelWidth;
                int originalHeight = decoder.Frames[0].PixelHeight;

                ms.Position = 0; // 重置流位置以重新读取

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                img.StreamSource = ms;

                const int maxSize = 16384;
                if (originalWidth > maxSize || originalHeight > maxSize)
                {
                    if (originalWidth >= originalHeight) img.DecodePixelWidth = maxSize;
                    else img.DecodePixelHeight = maxSize;
                    Dispatcher.Invoke(() => ShowToast("⚠️ 图片过大，已自动压缩显示"));
                }

                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                // 这里返回 null 会导致 LoadImage 中的 fullResBitmap 为 null
                // 从而触发 LoadImage 最后的 catch 块或者 if (fullResBitmap == null) return;
                return null;
            }
        }

        //private Task<(int Width, int Height)> GetImageDimensionsAsync(byte[] imageBytes)
        //{
        //    return Task.Run(() =>
        //    {
        //        using var ms = new System.IO.MemoryStream(imageBytes);
        //        // Create a decoder but only access the metadata, which is very fast.
        //        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
        //        return (decoder.Frames[0].PixelWidth, decoder.Frames[0].PixelHeight);
        //    });
        //}

        private Task<(int Width, int Height)?> GetImageDimensionsAsync(byte[] imageBytes)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var ms = new System.IO.MemoryStream(imageBytes);
                    // 尝试读取元数据
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);

                    // 确保至少有一帧
                    if (decoder.Frames != null && decoder.Frames.Count > 0)
                    {
                        return ((int Width, int Height)?)(decoder.Frames[0].PixelWidth, decoder.Frames[0].PixelHeight);
                    }
                    return null;
                }
                catch (Exception)
                {
                    // 这里捕获 NotSupportedException, FileFormatException 等
                    // 返回 null 表示无法解析尺寸
                    return null;
                }
            });
        }

        private readonly object _lockObj = new object();
        private async Task LoadImage(string filePath, string? sourcePath = null, bool lazyload = false)
        {
            _isCurrentFileGif = false;
            GifPlayerImage.Visibility = Visibility.Collapsed;
            _loadImageCts?.Cancel();
            _loadImageCts = new CancellationTokenSource();
            var token = _loadImageCts.Token;
            if (_progressCts != null)
            {
                _progressCts.Cancel();
                _progressCts.Dispose();
                _progressCts = null;
            }
            string fileToRead = sourcePath ?? filePath;
            if (IsVirtualPath(filePath) && string.IsNullOrEmpty(sourcePath)) { await LoadBlankCanvasAsync(filePath); return; }


            if (!File.Exists(fileToRead))
            {
                // 只有非虚拟路径不存在时才报错
                ShowToast($"找不到图片文件: {fileToRead}");
                return;
            }

            try
            {
                var fileInfo = new FileInfo(fileToRead);
                if (fileInfo.Length == 0)
                {
                    await LoadBlankCanvasAsync(filePath, "文件为空，已创建空白画布");
                    return;
                }
            }
            catch (Exception ex)
            {
                // 获取文件信息失败（可能是权限问题），记录日志并尝试继续，或者直接降级
                Debug.WriteLine($"FileInfo check failed: {ex.Message}");
            }

            CancellationTokenSource progressCts = null;
            Task progressTask = null;

            try
            {
                // 步骤 1: 异步读取文件并快速获取最终尺寸
                var imageBytes = await File.ReadAllBytesAsync(fileToRead, token);
                if (token.IsCancellationRequested) return;

                string sizeString = FormatFileSize(imageBytes.Length);
                await Dispatcher.InvokeAsync(() => this.FileSize = sizeString);


                var dimensions = await GetImageDimensionsAsync(imageBytes);
                if (token.IsCancellationRequested) return;
                if (dimensions == null)
                {
                    // 方法1：直接调用错误处理（推荐）
                    await LoadBlankCanvasAsync(filePath, "文件头部损坏或格式不支持，已创建空白画布");
                    return;

                }
                var (originalWidth, originalHeight) = dimensions.Value;
                long totalPixels = (long)originalWidth * originalHeight;
                bool showProgress = totalPixels > 2000000 * PerformanceScore;
                if (showProgress)
                {
                    // 创建新的进度条控制源
                    _progressCts = new CancellationTokenSource();
                    var progressToken = _progressCts.Token; // 捕获当前Token

                    // 启动模拟任务（不 await）
                    _ = SimulateProgressAsync(progressToken, totalPixels, (msg) =>
                    {
                        if (progressToken.IsCancellationRequested) return;

                        Dispatcher.Invoke(() =>
                        {
                            if (!_isLoadingImage) return;

                            _imageSize = msg;
                            OnPropertyChanged(nameof(ImageSize));
                        });
                    });
                }
                else
                {
                    // 小图直接显示尺寸
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _imageSize = $"{originalWidth}×{originalHeight}像素";
                        OnPropertyChanged(nameof(ImageSize));
                    });
                }
                long _totalPixels = (long)originalWidth * originalHeight;
                bool isHugeImage = _totalPixels > 10_000_000 * PerformanceScore; // 阈值：5000万像素 (约8K分辨率以上)
                if (isHugeImage)
                {
                    try
                    {
                        await Task.Delay(100, token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                }

                // 步骤 2: 并行启动中等预览图和完整图的解码任务
                Task<BitmapImage> previewTask = Task.Run(() => DecodePreviewBitmap(imageBytes, token), token);
                Task<BitmapImage> fullResTask = Task.Run(() => DecodeFullResBitmap(imageBytes, token), token);

                // --- 阶段 0: (新增) 立即显示已缓存的缩略图 ---
                bool isInitialLayoutSet = false;
                // 查找与当前文件路径匹配的 Tab 项
                var tabItem = FileTabs.FirstOrDefault(t => t.FilePath == filePath);

                if (tabItem?.Thumbnail != null)
                {
                    // 如果找到了并且它已经有缩略图，立即在UI线程上显示它
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);
                        BackgroundImage.Source = tabItem.Thumbnail;

                        // 更新窗口标题等基本信息
                        _currentFileName = IsVirtualPath(filePath)
           ? (FileTabs.FirstOrDefault(t => t.FilePath == filePath)?.FileName ?? "未命名")
           : System.IO.Path.GetFileName(filePath);
                        _currentFilePath = filePath;
                        UpdateWindowTitle();
                        FitToWindow(1);
                        CenterImage(); // 或者你更新后的 UpdateImagePosition()
                        BackgroundImage.InvalidateVisual();
                        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                        isInitialLayoutSet = true; // 标记初始布局已完成
                        _canvasResizer.UpdateUI();

                    });
                }



                // --- 阶段 1: 等待 480p 预览图并更新 ---
                var previewBitmap = await previewTask;
                if (token.IsCancellationRequested || previewBitmap == null) return;

                await Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);

                    {
                        BackgroundImage.Source = previewBitmap;
                        _currentFileName = System.IO.Path.GetFileName(filePath);
                        _currentFilePath = filePath;
                        UpdateWindowTitle();
                        FitToWindow();
                        CenterImage();
                        BackgroundImage.InvalidateVisual();
                        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                        _canvasResizer.UpdateUI();
                    }
                });

                // --- 阶段 2: 等待完整图并最终更新 ---
                var fullResBitmap = await fullResTask;
                if (_progressCts != null)
                {
                    _progressCts.Cancel(); // 停止循环
                    _progressCts.Dispose();
                    _progressCts = null;
                }
                if (lazyload)
                {
                    try
                    {
                        await Task.Delay(50, token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                }
                if (token.IsCancellationRequested || fullResBitmap == null) return;

                // 获取元数据 (保持不变)
                string metadataString = await GetImageMetadataInfoAsync(imageBytes, filePath, fullResBitmap);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    long currentMem = Process.GetCurrentProcess().PrivateMemorySize64;
                    if (currentMem > 1024 * 1024 * 1024)
                    {
                        GC.Collect(2, GCCollectionMode.Forced, true);
                    }
                    // 1. 记录原始 DPI
                    _originalDpiX = fullResBitmap.DpiX;
                    _originalDpiY = fullResBitmap.DpiY;

                    // 计算统一后的尺寸
                    int width = fullResBitmap.PixelWidth;
                    int height = fullResBitmap.PixelHeight;

                    BitmapSource source = fullResBitmap;
                    if (source.Format != PixelFormats.Bgra32)
                    {
                        var formatted = new FormatConvertedBitmap();
                        formatted.BeginInit();
                        formatted.Source = fullResBitmap;
                        formatted.DestinationFormat = PixelFormats.Bgra32;
                        formatted.EndInit();
                        source = formatted;
                    }

                    _bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);

                    _bitmap.Lock();
                    try
                    {
                        source.CopyPixels(
                            new Int32Rect(0, 0, width, height),
                            _bitmap.BackBuffer, // 目标指针
                            _bitmap.BackBufferStride * height, // 缓冲区总大小
                            _bitmap.BackBufferStride // 步长
                        );

                        // 标记脏区以更新画面
                        _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                    }
                    catch (Exception copyEx)
                    {
                        Debug.WriteLine("内存拷贝失败: " + copyEx.Message);
                    }
                    finally
                    {
                        _bitmap.Unlock();
                    }

                    // 5. 【极其重要】主动释放资源并 GC
                    // 解除引用
                    source = null;
                    fullResBitmap = null;
                    RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.NearestNeighbor);

                    BackgroundImage.Source = _bitmap;
                    this.CurrentImageFullInfo = metadataString;

                    if (_surface == null) _surface = new CanvasSurface(_bitmap);
                    else _surface.Attach(_bitmap);

                    _undo?.ClearUndo();
                    _undo?.ClearRedo();
                    _isEdited = false;
                    SetPreviewSlider();
                    _imageSize = $"{_surface.Width}×{_surface.Height}像素";
                    OnPropertyChanged(nameof(ImageSize));
                    _hasUserManuallyZoomed = false;
                    FitToWindow();
                    CenterImage();
                    _canvasResizer.UpdateUI();

                    string ext = System.IO.Path.GetExtension(fileToRead)?.ToLower();
                    _isCurrentFileGif = (ext == ".gif");

                    if (_isCurrentFileGif && IsViewMode)
                    {
                        AnimationBehavior.SetSourceUri(GifPlayerImage, new Uri(fileToRead));
                        GifPlayerImage.Visibility = Visibility.Visible;   //if (SettingsManager.Instance.Current.StartInViewMode)
                        var controller = AnimationBehavior.GetAnimator(GifPlayerImage);
                        if (controller != null)
                        {
                            controller.Play();
                        }
                    }
                    else
                    {
                        // 如果不是 GIF，清空播放器资源
                        AnimationBehavior.SetSourceUri(GifPlayerImage, null);
                        GifPlayerImage.Visibility = Visibility.Collapsed;
                        BackgroundImage.Visibility = Visibility.Visible;
                    }


                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); // 稍微降低优先级，确保UI先响应
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"Image load for {filePath} was canceled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image {filePath}: {ex.Message}");

                // 取消可能的进度条
                _progressCts?.Cancel();
                await LoadBlankCanvasAsync(filePath, "文件损坏或格式不支持，已创建空白画布");
            }
            finally
            {

                // 清理进度条资源
                progressCts?.Dispose();
            }
        }

        private async Task SimulateProgressAsync(CancellationToken token, long totalPixels, Action<string> progressCallback)
        {
            // 1. 初始进度 (假设元数据和缩略图已完成)
            double currentProgress = 5.0;
            progressCallback($"正在加载 {currentProgress:0}%");
            int performanceScore = PerformanceScore; // 假设这是之前定义的全局静态变量
            if (performanceScore <= 0) performanceScore = 5; // 默认值

            double scoreFactor = 0.5 + (performanceScore * 0.25);
            double estimatedMs = (totalPixels / 60000.0) / scoreFactor;
            if (estimatedMs < 300) estimatedMs = 300;
            int interval = 50;
            double steps = estimatedMs / interval;
            double incrementPerStep = (95.0 - currentProgress) / steps;

            try
            {
                while (!token.IsCancellationRequested && currentProgress < 95.0)
                {
                    await Task.Delay(interval, token);
                    currentProgress += incrementPerStep;
                    if (currentProgress > 99) currentProgress = 99;

                    // 回调更新 UI
                    progressCallback($"正在加载 {(int)currentProgress}%");
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private async Task LoadBlankCanvasAsync(string filePath, string reason = null)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                // 1. 创建默认白色画布 (1200x900)
                int width = 1200;
                int height = 900;
                _bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);

                // 填充白色
                byte[] pixels = new byte[width * height * 4];
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 255;     // B
                    pixels[i + 1] = 255; // G
                    pixels[i + 2] = 255; // R
                    pixels[i + 3] = 255; // A
                }
                _bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);

                // 2. 设置状态
                _originalDpiX = 96.0;
                _originalDpiY = 96.0;

                RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.NearestNeighbor);
                BackgroundImage.Source = _bitmap;

                // 3. 确定显示名称
                // 如果是虚拟路径，尝试从 Tab 获取名字；如果是真实文件，获取文件名
                if (IsVirtualPath(filePath))
                {
                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
                    _currentFileName = tab?.FileName ?? "未命名";
                }
                else
                {
                    _currentFileName = System.IO.Path.GetFileName(filePath);
                }

                _currentFilePath = filePath;

                // 设置底部栏信息
                this.CurrentImageFullInfo = reason ?? "[新建图像] 内存文件";
                this.FileSize = "未保存";

                // 初始化 Surface 和 Canvas
                if (_surface == null) _surface = new CanvasSurface(_bitmap);
                else _surface.Attach(_bitmap);

                _undo?.ClearUndo();
                _undo?.ClearRedo();
                _isEdited = false; // 刚打开的空文件不算被编辑过，或者是空文件视为原状

                _imageSize = $"{width}×{height}像素";
                OnPropertyChanged(nameof(ImageSize));
                UpdateWindowTitle();

                // 隐藏 GIF 播放器
                AnimationBehavior.SetSourceUri(GifPlayerImage, null);
                GifPlayerImage.Visibility = Visibility.Collapsed;
                BackgroundImage.Visibility = Visibility.Visible;
                FitToWindow(1);
                CenterImage();
                _canvasResizer.UpdateUI();
                SetPreviewSlider();

                // 可选：提示用户
                if (!string.IsNullOrEmpty(reason))
                {
                    ShowToast(reason);
                }
            });
        }

    }
}