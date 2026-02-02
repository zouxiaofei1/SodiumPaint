
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//ImageBar图片选择框相关代码
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class FileTabItem : INotifyPropertyChanged
        {
            private string _filePath;
            public string FilePath
            {
                get => _filePath;
                set
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
            private int _untitledNumber;
            public int UntitledNumber
            {
                get => _untitledNumber;
                set
                {
                    _untitledNumber = value;
                    OnPropertyChanged(nameof(UntitledNumber));
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
            public string FileName
            {
                get
                {
                    // 1. 优先检查是否是虚拟路径
                    if (!string.IsNullOrEmpty(FilePath) && FilePath.StartsWith("::TABPAINT_NEW::"))
                        return $"未命名 {UntitledNumber}";

                    // 2. 如果是真实存在的物理路径
                    if (!string.IsNullOrEmpty(FilePath))
                    {
                        try { return System.IO.Path.GetFileName(FilePath); }
                        catch { return FilePath; } // 防止非法路径字符崩溃
                    }

                    // 3. 兜底逻辑
                    return IsNew ? $"未命名 {UntitledNumber}" : "未命名";
                }
            }

            public string DisplayName
            {
                get
                {
                    // 1. 优先检查是否是虚拟路径
                    if (!string.IsNullOrEmpty(FilePath) && FilePath.StartsWith("::TABPAINT_NEW::"))
                        return $"未命名 {UntitledNumber}";

                    // 2. 如果是真实路径，去掉扩展名显示
                    if (!string.IsNullOrEmpty(FilePath))
                    {
                        try { return System.IO.Path.GetFileNameWithoutExtension(FilePath); }
                        catch { return FilePath; }
                    }

                    // 3. 兜底
                    return IsNew ? $"未命名 {UntitledNumber}" : "未命名";
                }
            }
            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
            }

            private bool _isLoading;
            public bool IsLoading
            {
                get => _isLoading;
                set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
            }

            private bool _isDirty;
            public bool IsDirty
            {
                get => _isDirty;
                set { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
            }

            private bool _isNew;
            public bool IsNew
            {
                get => _isNew;
                set
                {
                    _isNew = value;
                    OnPropertyChanged(nameof(IsNew));
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(FileName));
                }
            }


            private BitmapSource? _thumbnail;
            public BitmapSource? Thumbnail
            {
                get => _thumbnail;
                set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
            }

            public ICommand CloseCommand { get; set; }
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string BackupPath { get; set; }
            public DateTime LastBackupTime { get; set; }
            public FileTabItem(string path)
            {
                FilePath = path;
            }

            private CancellationTokenSource _loadCts;
            public async Task LoadThumbnailAsync(int containerWidth, int containerHeight)
            {
                // 0. 基础检查
                if (Thumbnail != null) return;

                // 确定 Key (通常用 FilePath，如果是未命名且有备份，用备份路径或ID)
                string cacheKey = FilePath;
                if (IsNew && !string.IsNullOrEmpty(BackupPath)) cacheKey = BackupPath;
                if (string.IsNullOrEmpty(cacheKey)) return;

                // 1. 【一级缓存检查】: 内存里有没有？有直接拿！
                var cachedImage = MainWindow.GlobalThumbnailCache.Get(cacheKey);
                if (cachedImage != null)
                {
                    Thumbnail = cachedImage;
                    IsLoading = false;
                    return;
                }

                // 2. 准备开始异步加载

                // 取消上一次针对这个Tab的未完成请求（防止快速滚动时积压）
                if (_loadCts != null)
                {
                    _loadCts.Cancel();
                    _loadCts = null;
                }

                _loadCts = new CancellationTokenSource();
                var token = _loadCts.Token;

                IsLoading = true;

                try
                {
                    // 防抖：给一点点缓冲时间，如果用户滑得飞快，这里还没过就被取消了
                    await Task.Delay(50, token);

                    // 3. 申请信号量：限制同时解码的线程数，防止显存爆炸
                    await MainWindow._thumbnailSemaphore.WaitAsync(token);

                    BitmapSource loadedBitmap = null;

                    try
                    {
                        if (token.IsCancellationRequested) return;

                        // 4. 开始读盘解码
                        loadedBitmap = await Task.Run(() =>
                        {
                            if (token.IsCancellationRequested) return null;

                            string targetPath = null;
                            if (!string.IsNullOrEmpty(BackupPath) && File.Exists(BackupPath)) targetPath = BackupPath;
                            else if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath)) targetPath = FilePath;

                            if (targetPath == null) return null;

                            try
                            {
                                string ext = System.IO.Path.GetExtension(targetPath)?.ToLower();
                                if (ext == ".svg")
                                {
                                    byte[] bytes = File.ReadAllBytes(targetPath);
                                    // 注意：这里需要访问 MainWindow 的 DecodeSvg，
                                    // 因为 DecodeSvg 是 MainWindow 的实例方法，
                                    // 我们可以通过 Application.Current.MainWindow 获取实例
                                    var mw = System.Windows.Application.Current.Dispatcher.Invoke(() => (MainWindow)System.Windows.Application.Current.MainWindow);
                                    return mw.DecodeSvg(bytes, token);
                                }

                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.UriSource = new Uri(targetPath);

                                bmp.DecodePixelWidth = 100;

                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.EndInit();
                                bmp.Freeze(); // 冻结以跨线程
                                return bmp;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Thumb load error: {ex.Message}");
                                return null;
                            }
                        }, token);
                    }
                    finally
                    {
                        // 无论如何都要释放信号量
                        MainWindow._thumbnailSemaphore.Release();
                    }

                    if (token.IsCancellationRequested) return;

                    // 5. 存入缓存并更新 UI
                    if (loadedBitmap != null)
                    {
                        MainWindow.GlobalThumbnailCache.Add(cacheKey, loadedBitmap);

                        // 切换回 UI 线程更新属性
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            Thumbnail = loadedBitmap;
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，忽略
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Async load failed: {ex.Message}");
                }
                finally
                {
                    if (!token.IsCancellationRequested)
                        IsLoading = false;

                    _loadCts?.Dispose();
                    _loadCts = null;
                }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        private const int PageSize = 10; // 每页标签数量（可调整）

        public ObservableCollection<FileTabItem> FileTabs { get; }
            = new ObservableCollection<FileTabItem>();
        private bool _isProgrammaticScroll = false;
        // 文件总数绑定属性
        public int ImageFilesCount;
        private bool _isInitialLayoutComplete = false;
        private HashSet<string> _explicitlyClosedFiles = new HashSet<string>();
        private long _currentCanvasVersion = 0;
        private const string VirtualFilePrefix = "::TABPAINT_NEW::";

        // 上次成功备份时的版本号
        private long _lastBackedUpVersion = -1;
        private FileTabItem _mouseDownTabItem;
        private int _dragThreshold = 50;//判定拖拽的阈值（像素）
    }
}