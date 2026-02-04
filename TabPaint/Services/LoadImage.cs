using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using XamlAnimatedGif; // 添加这一行
using SkiaSharp;
using Svg.Skia;
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
                        try { await pendingSave;  }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Wait save failed: " + ex.Message);
                        }
                    }

                // 等待结束后，重新检查 BackupPath，因为它刚刚被后台线程更新了！
                if (current != null && (current.IsDirty || current.IsNew) && !string.IsNullOrEmpty(current.BackupPath))
                {
                    if (File.Exists(current.BackupPath)) actualSourcePath = current.BackupPath;

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
                    await LoadAndDisplayImageInternalAsync(filePathToLoad);
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
                Debug.WriteLine($"Error loading image {filePath}: {ex.Message}");
            }
        }
        // 1. 在类级别定义支持的扩展名（静态只读，利用HashSet的哈希查找，极快）
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(AppConsts.ImageExtensions, StringComparer.OrdinalIgnoreCase);
        private async Task ScanFolderImagesAsync(string filePath)
        {
            try
            {
                if (IsVirtualPath(filePath) || string.IsNullOrEmpty(filePath)) return;

                string folder = System.IO.Path.GetDirectoryName(filePath)!;
                if (!File.Exists(_currentFilePath) && !Directory.Exists(_currentFilePath)) { MainImageBar.IsSingleTabMode = true; }
                var sortedFiles = await Task.Run(() =>
                {
                    return Directory.EnumerateFiles(folder)
                        .Where(f =>
                        {
                            var ext = System.IO.Path.GetExtension(f);
                            return ext != null && AllowedExtensions.Contains(ext);
                        })
                        .OrderBy(f => f, NaturalStringComparer.Default)
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

                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                int originalWidth = decoder.Frames[0].PixelWidth;

                // 重置流位置以供 BitmapImage 读取
                ms.Position = 0;

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;

                if (originalWidth > AppConsts.PreviewDecodeWidth) img.DecodePixelWidth = AppConsts.PreviewDecodeWidth;
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
        private BitmapSource DecodeWithSkiaAndIcc(byte[] imageBytes)
        {
            try
            {
                using var ms = new MemoryStream(imageBytes);
                using var codec = SKCodec.Create(ms);
                if (codec == null) return null;

                // --- 1. 准备色彩空间和参数 ---
                using var srgbSpace = SKColorSpace.CreateSrgb();
                var info = new SKImageInfo(
                    codec.Info.Width,
                    codec.Info.Height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul,
                    srgbSpace);
                using var originalBitmap = new SKBitmap(info);

                // 这一步 Skia 会同时完成：解码 + ICC转sRGB + 格式转Bgra + 填充Alpha
                var result = codec.GetPixels(info, originalBitmap.GetPixels());

                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                {
                    Debug.WriteLine($"Skia decode status: {result}");
                    return null;
                }
                var origin = codec.EncodedOrigin;

                // 如果不需要旋转，直接转换并返回
                if (origin == SKEncodedOrigin.TopLeft)
                {
                    return SkiaBitmapToWpfSource(originalBitmap);
                }
                int newWidth = (origin == SKEncodedOrigin.RightTop || origin == SKEncodedOrigin.LeftBottom) ? info.Height : info.Width;
                int newHeight = (origin == SKEncodedOrigin.RightTop || origin == SKEncodedOrigin.LeftBottom) ? info.Width : info.Height;

                var rotatedInfo = info.WithSize(newWidth, newHeight);
                using var rotatedBitmap = new SKBitmap(rotatedInfo);
                using var canvas = new SKCanvas(rotatedBitmap);
                canvas.Clear(SKColors.Transparent);

                // 坐标系变换
                switch (origin)
                {
                    case SKEncodedOrigin.RightTop: // 90度
                        canvas.Translate(newWidth, 0);
                        canvas.RotateDegrees(90);
                        break;
                    case SKEncodedOrigin.BottomRight: // 180度
                        canvas.Translate(newWidth, newHeight);
                        canvas.RotateDegrees(180);
                        break;
                    case SKEncodedOrigin.LeftBottom: // 270度
                        canvas.Translate(0, newHeight);
                        canvas.RotateDegrees(270);
                        break;
                        // 其他镜像模式暂略，通常只需处理这几个
                }
                using (var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High })
                {
                    canvas.DrawBitmap(originalBitmap, 0, 0, paint);
                }
                canvas.Flush();

                // 转换回 WPF 对象
                return SkiaBitmapToWpfSource(rotatedBitmap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Skia ICC Decode Error: {ex.Message}");
                return null;
            }
        }


        private BitmapSource SkiaBitmapToWpfSource(SKBitmap skBitmap)
        {
            // 1. 创建 WPF 的 WriteableBitmap
            var wb = new WriteableBitmap(skBitmap.Width, skBitmap.Height, 96, 96, PixelFormats.Bgra32, null);

            wb.Lock();
            try
            {
                // 2. 检查源数据信息
                var info = skBitmap.Info;
                unsafe
                {
                    void* srcPtr = (void*)skBitmap.GetPixels();  // 获取源地址 (Skia)
                    void* dstPtr = (void*)wb.BackBuffer;
                    long bytesToCopy = (long)skBitmap.Height * skBitmap.RowBytes;
                    Buffer.MemoryCopy(srcPtr, dstPtr, bytesToCopy, bytesToCopy);  // 执行拷贝
                }
                wb.AddDirtyRect(new Int32Rect(0, 0, skBitmap.Width, skBitmap.Height));//  标记脏区，通知 WPF 更新画面
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bitmap Copy Error: {ex.Message}");
                return null;
            }
            finally
            {
                wb.Unlock();
            }

            wb.Freeze();
            return wb;
        }
        private BitmapSource DecodeFullResBitmap(byte[] imageBytes, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            if (SettingsManager.Instance.Current.EnableIccColorCorrection)
            {
                try
                {
                    var skiaBitmap = DecodeWithSkiaAndIcc(imageBytes);
                    if (skiaBitmap != null) return skiaBitmap;
                }
                catch (Exception ex)
                {
                    // 如果 Skia 解码失败（比如不支持的格式），回退到下面的 WPF 原生解码
                    Debug.WriteLine($"Skia ICC Decode failed, falling back to WPF: {ex.Message}");
                }
            }
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

                const int maxSize = (int)AppConsts.MaxCanvasSize;
                if (originalWidth > maxSize || originalHeight > maxSize)
                {
                    if (originalWidth >= originalHeight) img.DecodePixelWidth = maxSize;
                    else img.DecodePixelHeight = maxSize;
                    Dispatcher.Invoke(() => ShowToast("L_Toast_ImageTooLarge"));
                }

                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }

        private Task<(int Width, int Height)?> GetImageDimensionsAsync(byte[] imageBytes, string filePath)
        {
            return Task.Run(() =>
            {
                try
                {
                    string ext = System.IO.Path.GetExtension(filePath)?.ToLower();
                    if (ext == ".svg")
                    {
                        using var ms = new System.IO.MemoryStream(imageBytes);
                        using var svg = new SKSvg();
                        svg.Load(ms);
                        if (svg.Picture != null)  // 检查是否加载成功
                        {
                            int w = (int)svg.Picture.CullRect.Width;  // 获取画布尺寸
                            int h = (int)svg.Picture.CullRect.Height;
                            if (w <= 0) w = 800;
                            if (h <= 0) h = 600;

                            return ((int Width, int Height)?)(w, h);
                        }
                        return null;
                    }
                    using var msNormal = new System.IO.MemoryStream(imageBytes);
                    var decoder = BitmapDecoder.Create(msNormal, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);

                    if (decoder.Frames != null && decoder.Frames.Count > 0)
                    {
                        return ((int Width, int Height)?)(decoder.Frames[0].PixelWidth, decoder.Frames[0].PixelHeight);
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    // 可以在这里打个断点或者输出日志，看看是不是 SVG 格式真的有问题
                    System.Diagnostics.Debug.WriteLine($"GetDimensions Error: {ex.Message}");
                    return null;
                }
            });
        }

        internal BitmapImage DecodeSvg(byte[] imageBytes, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            try
            {
                using var ms = new System.IO.MemoryStream(imageBytes);
                using var svg = new SKSvg();
                svg.Load(ms);

                if (svg.Picture == null) return null;
                int width = (int)svg.Picture.CullRect.Width;
                int height = (int)svg.Picture.CullRect.Height;
                if (width <= 0) width = 800;     // 如果 SVG 没有定义尺寸，给个默认值
                if (height <= 0) height = 600;
                const int maxSize = (int)AppConsts.MaxCanvasSize;  // 限制最大尺寸
                if (width > maxSize || height > maxSize)
                {
                    float scale = Math.Min((float)maxSize / width, (float)maxSize / height);
                    width = (int)(width * scale);
                    height = (int)(height * scale);
                }

                using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                float scaleX = (float)width / svg.Picture.CullRect.Width;
                float scaleY = (float)height / svg.Picture.CullRect.Height;
                var matrix = SKMatrix.CreateScale(scaleX, scaleY);

                canvas.DrawPicture(svg.Picture, ref matrix);
                canvas.Flush();

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var outMs = new System.IO.MemoryStream(data.ToArray());

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = outMs;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SVG Decode Error: " + ex.Message);
                return null;
            }
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
            if (IsVirtualPath(filePath) && string.IsNullOrEmpty(sourcePath))
            {
                await LoadBlankCanvasAsync(filePath);
                return;
            }

            if (!File.Exists(fileToRead))
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_FileNotFound_Format"), fileToRead));
                return;
            }

            try
            {
                var fileInfo = new FileInfo(fileToRead);
                if (fileInfo.Length == 0)
                {
                    await LoadBlankCanvasAsync(filePath, LocalizationManager.GetString("L_Load_Reason_Empty"));
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


                var dimensions = await GetImageDimensionsAsync(imageBytes, filePath);
                if (token.IsCancellationRequested) return;
                if (dimensions == null)
                {
                    await LoadBlankCanvasAsync(filePath, LocalizationManager.GetString("L_Load_Reason_Header"));
                    return;

                }
                var (originalWidth, originalHeight) = dimensions.Value;
                long totalPixels = (long)originalWidth * originalHeight;
                bool showProgress = totalPixels > AppConsts.PerformanceScorePixelThreshold * PerformanceScore;
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
                        _imageSize = $"{originalWidth}×{originalHeight}"+ LocalizationManager.GetString("L_Main_Unit_Pixel");
                        OnPropertyChanged(nameof(ImageSize));
                    });
                }
                long _totalPixels = (long)originalWidth * originalHeight;
                bool isHugeImage = _totalPixels > AppConsts.HugeImagePixelThreshold * PerformanceScore; // 阈值：5000万像素 (约8K分辨率以上)
                if (isHugeImage)
                {
                    try
                    {
                        await Task.Delay(AppConsts.ImageLoadDelayHugeMs, token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                }

                // 步骤 2: 并行启动中等预览图和完整图的解码任务
                string extension = System.IO.Path.GetExtension(filePath)?.ToLower();
                Task<BitmapImage> previewTask;
                Task<BitmapSource> fullResTask;

                if (extension == ".svg")
                {
                    previewTask = Task.Run(() => DecodeSvg(imageBytes, token), token);
                    fullResTask = previewTask.ContinueWith(t => (BitmapSource)t.Result, TaskContinuationOptions.OnlyOnRanToCompletion);
                }
                else
                {
                    previewTask = Task.Run(() => DecodePreviewBitmap(imageBytes, token), token);
                    fullResTask = Task.Run(() => DecodeFullResBitmap(imageBytes, token), token);
                }

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
                        CenterImage(); 
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
                        await Task.Delay(AppConsts.ImageLoadDelayLazyMs, token);
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
                    if (currentMem > AppConsts.MemoryLimitForAggressiveRelease)
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
                    _imageSize = $"{_surface.Width}×{_surface.Height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
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
                await LoadBlankCanvasAsync(filePath, LocalizationManager.GetString("L_Load_Reason_Corrupt"));
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
            string loadingFormat = LocalizationManager.GetString("L_Progress_Loading_Format");
            progressCallback(string.Format(loadingFormat, (int)currentProgress));
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
                    progressCallback(string.Format(loadingFormat, (int)currentProgress));
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
                int width = AppConsts.DefaultBlankCanvasWidth;
                int height = AppConsts.DefaultBlankCanvasHeight;
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
                    _currentFileName = tab?.FileName ?? LocalizationManager.GetString("L_Common_Untitled");
                }
                else
                {
                    _currentFileName = System.IO.Path.GetFileName(filePath);
                }

                _currentFilePath = filePath;

                // 设置底部栏信息
                this.CurrentImageFullInfo = reason ?? LocalizationManager.GetString("L_Load_Info_Memory");
                this.FileSize = LocalizationManager.GetString("L_Status_Unsaved");

                // 初始化 Surface 和 Canvas
                if (_surface == null) _surface = new CanvasSurface(_bitmap);
                else _surface.Attach(_bitmap);

                _undo?.ClearUndo();
                _undo?.ClearRedo();
                _isEdited = false; // 刚打开的空文件不算被编辑过，或者是空文件视为原状

                _imageSize = $"{width}×{height}{LocalizationManager.GetString("L_Main_Unit_Pixel")}";
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
                if (!string.IsNullOrEmpty(reason)) ShowToast(reason);
            });
        }

    }
}
