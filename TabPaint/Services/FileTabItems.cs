
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        public class FileTabItem : INotifyPropertyChanged
        {
            public string FilePath { get; set; } // 允许 set，因为新建文件可能一开始没有路径
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
                    if (!string.IsNullOrEmpty(FilePath))
                        return System.IO.Path.GetFileName(FilePath);
                    if (IsNew) // 如果是新建文件，显示 "未命名 X"
                        return $"未命名 {UntitledNumber}";

                    return "未命名";
                }
            }

            public string DisplayName// 🔄 修改：DisplayName (不带扩展名) 的显示逻辑同理
            {
                get
                {
                    if (!string.IsNullOrEmpty(FilePath))
                        return System.IO.Path.GetFileNameWithoutExtension(FilePath);

                    if (IsNew)
                        return $"未命名 {UntitledNumber}";

                    return "未命名";
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

            // 🔴 状态：是否修改未保存
            private bool _isDirty;
            public bool IsDirty
            {
                get => _isDirty;
                set { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
            }

            // 🔵 状态：是否是纯新建的内存文件
            private bool _isNew;
            public bool IsNew
            {
                get => _isNew;
                set { _isNew = value; OnPropertyChanged(nameof(IsNew)); }
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

            // ... LoadThumbnailAsync 方法保持不变 ...
            public async Task LoadThumbnailAsync(int containerWidth, int containerHeight)
            {
                if (IsNew || string.IsNullOrEmpty(FilePath)) return;

                var thumbnail = await Task.Run(() =>
                {
                    try
                    {
                        using (var img = System.Drawing.Image.FromFile(FilePath)) { /*...*/ }

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(FilePath);
                        bmp.DecodePixelWidth = 100;
                        bmp.CacheOption = BitmapCacheOption.OnLoad; // 关键
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                    catch { return null; }
                });
                if (thumbnail != null) Thumbnail = thumbnail;
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

        private void LoadTabPageAsync(int centerIndex)
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            // 1. 确定当前“文件夹视图”的范围
            int start = Math.Max(0, centerIndex - PageSize);
            int end = Math.Min(_imageFiles.Count - 1, centerIndex + PageSize);
            var viewportPaths = new HashSet<string>();
            for (int i = start; i <= end; i++) viewportPaths.Add(_imageFiles[i]);

            // 2. 清理阶段：只移除那些 "既不在视野内，又不是脏数据，也不是新文件" 的项
            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                bool isViewport = viewportPaths.Contains(tab.FilePath);
                bool isKeepAlive = tab.IsDirty || tab.IsNew; // 🔥 关键：只要脏了或者新了，就永远不删

                if (!isViewport && !isKeepAlive)
                {
                    FileTabs.RemoveAt(i);
                }
            }

            for (int i = start; i <= end; i++)
            {
                string path = _imageFiles[i];
                var existingTab = FileTabs.FirstOrDefault(t => t.FilePath == path);

                if (existingTab == null)
                {
                    // 创建新 Tab
                    var newTab = new FileTabItem(path);
                    newTab.IsLoading = true;
                    _ = newTab.LoadThumbnailAsync(100, 60);

                    int insertIndex = 0;
                    bool inserted = false;

                    for (int j = 0; j < FileTabs.Count; j++)
                    {
                        var t = FileTabs[j];

                        // 如果遇到新建文件(IsNew)或视野外的脏文件(不在viewportPaths里)，说明如果不插在这里，后面就都是特殊文件了
                        bool isSpecial = t.IsNew || (!string.IsNullOrEmpty(t.FilePath) && !viewportPaths.Contains(t.FilePath));

                        if (isSpecial)
                        {
                            FileTabs.Insert(j, newTab);
                            inserted = true;
                            break;
                        }

                        // 如果是普通视野文件，按索引比较
                        int tIndex = _imageFiles.IndexOf(t.FilePath);
                        if (tIndex > i)
                        {
                            FileTabs.Insert(j, newTab);
                            inserted = true;
                            break;
                        }
                    }

                    if (!inserted) FileTabs.Add(newTab);
                }
                else
                {
                }
            }

        }

        

        private async Task RefreshTabPageAsync(int centerIndex, bool refresh = false)
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            if (refresh)
            {
                LoadTabPageAsync(centerIndex);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }

            // 计算当前选中图片在 FileTabs 中的索引
            var currentTab = FileTabs.FirstOrDefault(t => t.FilePath == _imageFiles[centerIndex]);
            if (currentTab == null) return;

            int selectedIndex = FileTabs.IndexOf(currentTab);
            if (selectedIndex < 0) return;

            double itemWidth = 124;
            double viewportWidth = FileTabsScroller.ViewportWidth;

            // 如果窗口还没加载完，ViewportWidth 可能是 0，这时候滚动没意义且可能报错
            if (viewportWidth <= 0) return;

            double targetOffset = selectedIndex * itemWidth - viewportWidth / 2 + itemWidth / 2;

            targetOffset = Math.Max(0, targetOffset);
            double maxOffset = Math.Max(0, FileTabs.Count * itemWidth - viewportWidth);
            targetOffset = Math.Min(targetOffset, maxOffset);

            // 🔥 关键修复：使用 Dispatcher 并在滚动期间上锁
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _isProgrammaticScroll = true; 
                    FileTabsScroller.ScrollToHorizontalOffset(targetOffset);
                    FileTabsScroller.UpdateLayout(); 
                }
                finally
                {
                    _isProgrammaticScroll = false; // 🔓 解锁
                }
            });
        }

        private void OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isProgrammaticScroll) return;
            if (!_isInitialLayoutComplete || _isUpdatingUiFromScroll) return;

            double itemWidth = 124;
            int firstIndex = (int)(FileTabsScroller.HorizontalOffset / itemWidth);
            int visibleCount = (int)(FileTabsScroller.ViewportWidth / itemWidth) + 2;
            int lastIndex = firstIndex + visibleCount;

            if (PreviewSlider.Value != firstIndex)
            {
                _isUpdatingUiFromScroll = true; 
                PreviewSlider.Value = firstIndex;
                _isUpdatingUiFromScroll = false; 
            }
            bool needload = false;

            if (FileTabs.Count > 0&&lastIndex >= FileTabs.Count - 2 && FileTabs.Count < _imageFiles.Count) // 阈值调小一点，体验更丝滑
            {
                var lastTab = FileTabs[FileTabs.Count - 1];
                int lastFileIndex = _imageFiles.IndexOf(lastTab.FilePath);

                if (lastFileIndex >= 0 && lastFileIndex < _imageFiles.Count - 1)
                {
                    var nextItems = _imageFiles.Skip(lastFileIndex + 1).Take(PageSize);

                    foreach (var path in nextItems)
                    {
                        if (!FileTabs.Any(t => t.FilePath == path))
                        {
                            FileTabs.Add(new FileTabItem(path));
                        }
                    }
                    needload = true;
                }
            }


            // 前端加载 (修复版)
            if (firstIndex < 2 && FileTabs.Count > 0)
            {
                // 获取当前列表第一个文件的真实索引
                var firstTab = FileTabs[0];
                int firstFileIndex = _imageFiles.IndexOf(firstTab.FilePath);

                if (firstFileIndex > 0) // 如果前面还有图
                {
                    // 计算需要拿多少张
                    int takeCount = PageSize;
                    // 如果前面不够 PageSize 张了，就只拿剩下的
                    if (firstFileIndex < PageSize) takeCount = firstFileIndex;

                    // 关键修复：从 firstFileIndex - takeCount 开始拿
                    int start = firstFileIndex - takeCount;

                    var prevPaths = _imageFiles.Skip(start).Take(takeCount);

                    // 使用 Insert(0, ...) 会导致大量 UI 重绘，建议反转顺序逐个插入
                    int insertPos = 0;
                    foreach (var path in prevPaths)
                    {
                        if (!FileTabs.Any(t => t.FilePath == path))
                        {
                            FileTabs.Insert(insertPos, new FileTabItem(path));
                            insertPos++; // 保持插入顺序
                        }
                    }

                    // 修正滚动条位置，防止因为插入元素导致视图跳动
                    FileTabsScroller.ScrollToHorizontalOffset(FileTabsScroller.HorizontalOffset + insertPos * itemWidth);
                    needload = true;
                }
            }

            if (needload || e.HorizontalChange != 0 || e.ExtentWidthChange != 0)  // 懒加载缩略图，仅当有新增或明显滚动时触发
            {
                int end = Math.Min(lastIndex, FileTabs.Count);
                for (int i = firstIndex; i < end; i++)
                {
                    var tab = FileTabs[i];
                    if (tab.Thumbnail == null && !tab.IsLoading)
                    {
                        tab.IsLoading = true;
                        _ = tab.LoadThumbnailAsync(100, 60);
                    }
                }
            }
        }

        private void OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)// 鼠标滚轮横向滚动
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                // 横向滚动
                double offset = scrollViewer.HorizontalOffset - (e.Delta);
                scrollViewer.ScrollToHorizontalOffset(offset);
                e.Handled = true;
            }
        }

        
        private void ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)// 阻止边界反馈
        {
            e.Handled = true;
        }



        private async void OnFileTabClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is FileTabItem clickedItem)
            {
                if (_currentTabItem != null && _currentTabItem == clickedItem)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(_currentFilePath) &&
                    clickedItem.FilePath == _currentFilePath &&
                    !clickedItem.IsNew)
                {
                    return;
                }
                foreach (var tab in FileTabs) tab.IsSelected = false;
                clickedItem.IsSelected = true;

                // 2. 核心逻辑：分支判断
                if (clickedItem.IsNew)
                {
                    // 如果是从普通文件切换到“新建未命名”
                    if (_currentTabItem != clickedItem)
                    {
                        // 保存上一个文件的缓存（如果需要）
                        if (_currentTabItem != null && !_currentTabItem.IsNew)
                        {
                            await SaveCurrentToCacheAsync();
                        }

                        Clean_bitmap(1200, 900); // 初始化白板
                        _currentFilePath = string.Empty;
                        _currentFileName = "未命名";
                        UpdateWindowTitle();
                    }
                }
                else
                {
                    await OpenImageAndTabs(clickedItem.FilePath);
                }

                // 3. 记录当前正在激活的 Tab
                _currentTabItem = clickedItem;
            }
        }


        // 补充定义：在类成员里加一个引用，记录当前是谁
        private FileTabItem _currentTabItem;

        private bool _isDragging = false;
        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseOverThumb(e)) return;

            _isDragging = true;
            var slider = (Slider)sender;
            slider.CaptureMouse();
            UpdateSliderValueFromPoint(slider, e.GetPosition(slider));

            // 标记事件已处理，防止其他控件响应
            e.Handled = true;
        }

        private void Slider_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 仅当我们通过点击轨道开始拖动时，才处理 MouseMove 事件
            if (_isDragging)
            {
                var slider = (Slider)sender;
                // 持续更新 Slider 的值
                UpdateSliderValueFromPoint(slider, e.GetPosition(slider));
            }
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 如果我们正在拖动
            if (_isDragging)
            {
                _isDragging = false;
                var slider = (Slider)sender;
                // 释放鼠标捕获
                slider.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var slider = (Slider)sender;

            // 根据滚轮方向调整值
            double change = slider.LargeChange; // 使用 LargeChange 作为滚动步长
            if (e.Delta < 0)
            {
                change = -change;
            }

            slider.Value += change;
            e.Handled = true;
        }
        private async void UpdateSliderValueFromPoint(Slider slider, Point position)
        {
            double ratio = position.Y / slider.ActualHeight; 
            double value = slider.Minimum + (slider.Maximum - slider.Minimum) * (1 - ratio);

            value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, value)); // 确保值在有效范围内

            slider.Value = value;
            
            await OpenImageAndTabs(_imageFiles[(int)value], true);
        }
        private bool IsMouseOverThumb(MouseButtonEventArgs e)/// 检查鼠标事件的原始源是否是 Thumb 或其内部的任何元素。
        {
            var slider = (Slider)e.Source;
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            if (track == null) return false;

            return track.Thumb.IsMouseOver;
        }
        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject    // 这是一个通用的辅助方法，用于在可视化树中查找特定类型的子控件
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                {
                    return (T)child;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        private void SetPreviewSlider()
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;
            PreviewSlider.Minimum = 0;
            PreviewSlider.Maximum = _imageFiles.Count - 1;
            PreviewSlider.Value = _currentImageIndex;
        }
    }
}