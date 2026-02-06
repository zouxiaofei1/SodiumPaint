
//
//MainWindow.xaml.cs
//主窗口的逻辑实现，负责界面交互、工具初始化、文件打开与标签管理等核心功能。
//
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TabPaint.MainWindow;
using TabPaint.Controls;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        public MainWindow(string path)
        {
            _workingPath = path;
            _currentFilePath = path;
            CheckFilePathAvailibility(_currentFilePath);
            // PerformanceScore 移至 Loaded 异步加载
            InitializeComponent();
            RestoreWindowBounds();

            if (SettingsManager.Instance.Current.StartInViewMode && _currentFileExists)
            {
                IsViewMode = true;
                ThicknessPanel.Visibility = Visibility.Collapsed;
                OpacityPanel.Visibility = Visibility.Collapsed;
            }
            this.ContentRendered += MainWindow_ContentRendered;
            DataContext = this;
            InitDebounceTimer();
            InitWheelLockTimer();
            Loaded += MainWindow_Loaded;

            InitializeAutoSave();
            this.Focusable = true;
        }


        private async void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            InitializeLazyControls(); Dispatcher.BeginInvoke(new Action(() =>
            {
                ThemeManager.LoadLazyIcons();
            }), DispatcherPriority.Background);
            if (IsViewMode) OnModeChanged(true, isSilent: true);

            // 由于 MyStatusBar 和 MainToolBar 现在是分帧加载的，这里需要延迟初始化依赖它们的逻辑
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MyStatusBar != null)
                {
                    MyStatusBar.ZoomSliderControl.ValueChanged += (s, e) =>
                    {
                        if (_isInternalZoomUpdate) return;
                        double targetScale = SliderToZoom(MyStatusBar.ZoomSliderControl.Value);
                        //SetZoom(targetScale, slient: true);
                    };
                }
                SetCropButtonState();
            }), DispatcherPriority.Loaded);

            _canvasResizer = new CanvasResizeManager(this); ;
            LoadSession();
            if (!string.IsNullOrEmpty(_currentFilePath) && Directory.Exists(_currentFilePath))
            {
                _currentFilePath = FindFirstImageInDirectory(_currentFilePath);
            }


            if (!string.IsNullOrEmpty(_currentFilePath) && (File.Exists(_currentFilePath))) await OpenImageAndTabs(_currentFilePath, true);
            else
            {

                {
                    if (FileTabs.Count == 0)
                    {
                        CreateNewTab(TabInsertPosition.AfterCurrent, true);
                    }
                    else
                    {
                        SwitchToTab(FileTabs[0]);
                    }
                }
            }

            RestoreAppState();
            InitializeScrollPosition();
            //if (BlanketMode) FitToWindow();
            _startupFinished = true;

        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e); // 建议保留 base 调用

            MicaAcrylicManager.ApplyEffect(this);

            MicaEnabled = true; var currentSettings = SettingsManager.Instance.Current;
            bool isDark = (ThemeManager.CurrentAppliedTheme == AppTheme.Dark) || (currentSettings.StartInViewMode && currentSettings.ViewUseDarkCanvasBackground && _currentFileExists);
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);
            InitializeClipboardMonitor();

            var src = (HwndSource)PresentationSource.FromVisual(this);
            if (src != null)
            {
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
            }// 初始化 Mica
        }
        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // 为了性能和避免闪烁，可以加个判断，如果已经是 Mica 则不重复设置
            if (!MicaEnabled)
            {
                MicaAcrylicManager.ApplyEffect(this);
                MicaEnabled = true;
            }
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            await Task.Yield();
            
            // 异步加载性能评分，避免阻塞 UI
            _ = Task.Run(() =>
            {
                int score = QuickBenchmark.EstimatePerformanceScore();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    PerformanceScore = score;
                }));
            });

            this.Focus();

            try
            {

                _deleteCommitTimer = new System.Windows.Threading.DispatcherTimer();
                _deleteCommitTimer.Interval = TimeSpan.FromSeconds(AppConsts.DeleteCommitTimerSeconds); // 2秒
                _deleteCommitTimer.Tick += (s, e) => CommitPendingDeletions();
                StateChanged += MainWindow_StateChanged;
                Select = new SelectTool();
                this.Deactivated += MainWindow_Deactivated;

                // Canvas 事件
                CanvasWrapper.MouseDown += OnCanvasMouseDown;
                CanvasWrapper.MouseMove += OnCanvasMouseMove;
                CanvasWrapper.MouseUp += OnCanvasMouseUp;
                CanvasWrapper.MouseLeave += OnCanvasMouseLeave;

                // 初始化工具
                _surface = new CanvasSurface(_bitmap);
                _undo = new UndoRedoManager(_surface);
                _ctx = new ToolContext(_surface, _undo, BackgroundImage, SelectionPreview, SelectionOverlayCanvas, EditorOverlayCanvas, CanvasWrapper);
                _tools = new ToolRegistry();
                _ctx.ViewElement.Cursor = _tools.Pen.Cursor;
                _router = new InputRouter(_ctx, _tools.Pen);
                _originalGridBrush = CanvasWrapper.Background;
                SettingsManager.Instance.Current.PropertyChanged += OnSettingsPropertyChanged;
                UpdateCanvasVisuals();
                this.PreviewKeyDown += (s, e) =>
                {
                    MainWindow_PreviewKeyDown(s, e);
                    _router.OnPreviewKeyDown(s, e);
                };

                _ = Task.Delay(TimeSpan.FromSeconds(AppConsts.DragTempCleanupDelaySeconds)).ContinueWith(async _ =>
                    {
                        await CheckAndCleanDragTempAsync();
                    }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_LoadFailed_Prefix"), ex.Message));
            }
            finally
            {

                _isInitialLayoutComplete = true;
                if (FileTabs.Count > 0)
                { // 模拟触发一次滚动检查
                    OnFileTabsScrollChanged(MainImageBar.Scroller, null);
                }
            }
        }
        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabPaint.SettingsManager.Instance.Current.ViewUseDarkCanvasBackground) ||
                e.PropertyName == nameof(TabPaint.SettingsManager.Instance.Current.ViewShowTransparentGrid))
            {
                UpdateCanvasVisuals();
            }
        }
        private void UpdateCanvasVisuals()
        {
            var settings = SettingsManager.Instance.Current;

            if (IsViewMode)
            {
                if (settings.ViewUseDarkCanvasBackground)
                {
                    ScrollContainer.Background = _darkBackgroundBrush;
                }
                else
                {
                    ScrollContainer.Background = Brushes.Transparent;
                }
                if (settings.ViewShowTransparentGrid)
                {
                    CanvasWrapper.Background = _originalGridBrush;
                }
                else
                {
                    CanvasWrapper.Background = Brushes.White;
                }
            }
            else
            {
                ScrollContainer.Background = Brushes.Transparent;
                CanvasWrapper.Background = _originalGridBrush;
            }
        }


        private string FindFirstImageInDirectory(string folderPath)
        {
            try
            {
                var allFiles = Directory.GetFiles(folderPath);

                var firstImage = allFiles
                    .Where(f => IsImageFile(f))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase) //自然语言顺序
                    .FirstOrDefault();

                return firstImage; // 如果没找到，返回 null
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ReadFolderFailed_Prefix"), ex.Message));

                return null;
            }
        }
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {  // 工具函数 - 查找所有子元素
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T) yield return (T)child;

                    foreach (T childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
                }
            }
        }
        private bool IsImageFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path)?.ToLower();
            return AppConsts.ImageExtensions.Contains(ext);
        }

        public async Task OpenFilesAsNewTabs(string[] files)
        {
            if (files == null || files.Length == 0) return;

            int insertIndex = _imageFiles.Count;
            int uiInsertIndex = FileTabs.Count;

            if (_currentTabItem != null && !_currentTabItem.IsNew)
            {
                int currentIndexInFiles = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentIndexInFiles >= 0) insertIndex = currentIndexInFiles + 1;

                int currentIndexInTabs = FileTabs.IndexOf(_currentTabItem);
                if (currentIndexInTabs >= 0) uiInsertIndex = currentIndexInTabs + 1;
            }

            FileTabItem firstNewTab = null;
            int addedCount = 0;

            foreach (string file in files)
            {
                if (IsImageFile(file))
                {
                    if (_imageFiles.Contains(file)) continue; // 去重

                    _imageFiles.Insert(insertIndex + addedCount, file);
                    var newTab = new FileTabItem(file) { IsLoading = true };

                    if (uiInsertIndex + addedCount <= FileTabs.Count)
                        FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                    else
                        FileTabs.Add(newTab);

                    _ = newTab.LoadThumbnailAsync(AppConsts.DefaultThumbnailWidth, AppConsts.DefaultThumbnailHeight);
                    if (firstNewTab == null) firstNewTab = newTab;
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();
                _router.CleanUpSelectionandShape();
                if (firstNewTab != null)
                {
                    if (_currentTabItem != null) _currentTabItem.IsSelected = false;
                    firstNewTab.IsSelected = true;
                    _currentTabItem = firstNewTab;
                    await OpenImageAndTabs(firstNewTab.FilePath);
                    MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 1);

                }
            }
        }


        private void FitToWindow(double addscale = 1,bool needcanvasUpdateUI=true)
        {
            if (SettingsManager.Instance.Current.IsFixedZoom && _firstFittoWindowdone) return;
            if (BackgroundImage.Source != null)
            {
                a.s("fit to window start");
                double imgWidth = BackgroundImage.Source.Width;
                double imgHeight = BackgroundImage.Source.Height;
                double viewWidth = ScrollContainer.ViewportWidth;
                double viewHeight = ScrollContainer.ViewportHeight;

                double scaleX = viewWidth / imgWidth;
                double scaleY = viewHeight / imgHeight;

                double fitScale = Math.Min(scaleX, scaleY); // 保持纵横比适应
                zoomscale = fitScale * addscale * AppConsts.FitToWindowMarginFactor;

                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
                UpdateSliderBarValue(zoomscale);

                if(needcanvasUpdateUI)_canvasResizer.UpdateUI();//关掉可以节省2-5ms
                _firstFittoWindowdone = true;
            }
        }
        private async void PasteClipboardAsNewTab()
        {
            List<string> filesToProcess = new List<string>();

            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var dropList = Clipboard.GetFileDropList();
                    if (dropList != null)
                    {
                        foreach (string file in dropList)
                        {
                            if (IsImageFile(file)) filesToProcess.Add(file);
                        }
                    }
                }
                else if (Clipboard.ContainsImage())
                {
                    var bitmapSource = Clipboard.GetImage();
                    if (bitmapSource != null)
                    {
                        string cacheDir = AppConsts.CacheDir;
                        if (!System.IO.Directory.Exists(cacheDir)) System.IO.Directory.CreateDirectory(cacheDir);

                        string fileName = $"Paste_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        string filePath = System.IO.Path.Combine(cacheDir, fileName);

                        using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                        {
                            System.Windows.Media.Imaging.BitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
                            encoder.Save(fileStream);
                        }

                        filesToProcess.Add(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ClipboardReadFailed_Prefix"), ex.Message));

                return;
            }

            if (filesToProcess.Count == 0) return;
            int insertIndex = _imageFiles.Count; // 默认插到最后
            int uiInsertIndex = FileTabs.Count;

            if (_currentTabItem != null && !_currentTabItem.IsNew)
            {
                int currentIndexInFiles = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentIndexInFiles >= 0) insertIndex = currentIndexInFiles + 1;
                int currentIndexInTabs = FileTabs.IndexOf(_currentTabItem);
                if (currentIndexInTabs >= 0) uiInsertIndex = currentIndexInTabs + 1;
            }

            FileTabItem firstNewTab = null;
            int addedCount = 0;

            foreach (string file in filesToProcess)
            {
                if (_imageFiles.Contains(file)) continue;
                _imageFiles.Insert(insertIndex + addedCount, file);

                var newTab = new FileTabItem(file);
                newTab.IsLoading = true;

                if (uiInsertIndex + addedCount <= FileTabs.Count)FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                else FileTabs.Add(newTab);

                // 异步加载缩略图
                _ = newTab.LoadThumbnailAsync(AppConsts.DefaultThumbnailWidth, AppConsts.DefaultThumbnailHeight);

                // 记录第一张新图，用于稍后跳转
                if (firstNewTab == null) firstNewTab = newTab;

                addedCount++;
            }

            if (addedCount > 0)
            {
                // 更新 Slider 范围
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();

                if (firstNewTab != null)
                {
                    if (_currentTabItem != null) _currentTabItem.IsSelected = false;

                    // 选中新图
                    firstNewTab.IsSelected = true;
                    _currentTabItem = firstNewTab;

                    await OpenImageAndTabs(firstNewTab.FilePath);
                    // 确保新加的图片在视野内
                    MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 1);
                }
            }
        }
        private async Task CheckAndCleanDragTempAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_dragTempDir)) return;

                    var dirInfo = new DirectoryInfo(_dragTempDir);
                    var files = dirInfo.GetFiles();
                    int maxFileCount = 100; // 设定阈值：超过100个文件触发清理
                    int targetCount = 20;   // 清理目标：清理到只剩20个
                    if (files.Length > maxFileCount)
                    {
                        var sortedFiles = files.OrderBy(f => f.CreationTime).ToList();// 按创建时间升序排列（最旧的在前）
                        int deleteCount = files.Length - targetCount;

                        int deleted = 0;
                        foreach (var file in sortedFiles)
                        {
                            if (deleted >= deleteCount) break;
                            try
                            {
                                file.Delete(); // 尝试删除
                                deleted++;
                            }
                            catch (IOException) { /* 文件可能被占用，跳过 */ }
                            catch (UnauthorizedAccessException) { /* 无权限，跳过 */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 避免因清理逻辑报错导致程序崩溃，仅记录日志
                    System.Diagnostics.Debug.WriteLine($"[TabPaint] DragTemp cleanup failed: {ex.Message}");
                }
            });
        }


        private string GetPixelFormatString(System.Windows.Media.PixelFormat format)
        {
            // 简单映射常见格式名
            return format.ToString().Replace("Rgb", "RGB").Replace("Bgr", "BGR");
        }
        private void CenterImage()
        {
            if (_bitmap == null || BackgroundImage == null)
                return;

            BackgroundImage.Width = BackgroundImage.Source.Width;
            BackgroundImage.Height = BackgroundImage.Source.Height;

            if (ScrollContainer != null)
            {
                ScrollContainer.ScrollToHorizontalOffset(
                    (BackgroundImage.Width - ScrollContainer.ViewportWidth) / 2);
                ScrollContainer.ScrollToVerticalOffset(
                    (BackgroundImage.Height - ScrollContainer.ViewportHeight) / 2);
            }

            BackgroundImage.VerticalAlignment = VerticalAlignment.Center;
        }

        private void InitializeToastTimer()
        {
            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromMilliseconds(ToastDuration);
            _toastTimer.Tick += (s, e) => HideToast(); // 计时结束触发淡出
        }

        public void ShowToast(string messageOrKey)
        {
            if (_toastTimer == null) InitializeToastTimer();
            _toastTimer.Stop();
            string message = LocalizationManager.GetString(messageOrKey);
            InfoToastText.Text = message;

            if (InfoToast.Opacity < 1.0)
            {
                InfoToast.BeginAnimation(OpacityProperty, null);

                DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(AppConsts.ToastFadeInMs));
                fadeIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                InfoToast.BeginAnimation(OpacityProperty, fadeIn);
            }
            _toastTimer.Start();
        }

        // 独立的淡出方法
        private void HideToast()
        {
            _toastTimer.Stop(); // 停止计时器

            DoubleAnimation fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(AppConsts.ToastFadeOutMs));
            fadeOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };

            InfoToast.BeginAnimation(OpacityProperty, fadeOut);
        }

        private DateTime _navKeyPressStartTime = DateTime.MinValue;
        private bool _isNavigating = false;
        private int CalculateNavigationGap()
        {
            if (_navKeyPressStartTime == DateTime.MinValue) return 1;

            var duration = (DateTime.Now - _navKeyPressStartTime).TotalMilliseconds;

            if (duration < AppConsts.NavGapLevel1Ms) return 1;
            if (duration < AppConsts.NavGapLevel2Ms) return 2;
            if (PerformanceScore > AppConsts.HighPerformanceThreshold) return 5; else return 3;
        }


        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return string.Format(LocalizationManager.GetString("L_Format_Size_B"), bytes);
            if (bytes < 1024 * 1024)
                return string.Format(LocalizationManager.GetString("L_Format_Size_KB"), bytes / 1024.0);
            return string.Format(LocalizationManager.GetString("L_Format_Size_MB"), bytes / 1024.0 / 1024.0);
        }
        private void ShowNextImage()
        {
            MoveImageIndex(1);
        }
        private void ShowPrevImage()
        {
            MoveImageIndex(-1);
        }
        private void MoveImageIndex(int direction) // direction: 1 or -1
        {
            if (_imageFiles.Count == 0 || _currentImageIndex < 0 || FileTabs == null) return;
            if (FileTabs.Count < 2) return;
            _router.CleanUpSelectionandShape();
            if (_isEdited && !string.IsNullOrEmpty(_currentFilePath))
            {

                SaveBitmap(_currentFilePath);
                _isEdited = false;
            }

            int gap = CalculateNavigationGap();
            int actualStep = gap * direction;
            int newIndex = _currentImageIndex + actualStep;

            if (newIndex >= _imageFiles.Count)
            {
                newIndex = newIndex % _imageFiles.Count; // 循环回到开头附近
                if (gap == 1) ShowToast("L_Toast_FirstImage");
            }
            else if (newIndex < 0)
            {
                newIndex = (_imageFiles.Count + (newIndex % _imageFiles.Count)) % _imageFiles.Count;
                if (gap == 1) ShowToast("L_Toast_LastImage");
            }

            _currentImageIndex = newIndex;

            RequestImageLoad(_imageFiles[_currentImageIndex]);
            ScrollToTabCenter(_currentTabItem ?? FileTabs.FirstOrDefault(t => t.FilePath == _imageFiles[newIndex]));
        }


        private string SaveClipboardImageToCache(BitmapSource source)
        {
            try
            {
                string cacheDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "Clipboard");
                if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                string fileName = $"Paste_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                string filePath = System.IO.Path.Combine(cacheDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    encoder.Save(fileStream);
                }
                return filePath;
            }
            catch
            {
                return null;
            }
        }

        private async Task InsertImagesToTabs(string[] files)
        {
            if (files == null || files.Length == 0) return;

            int insertIndex = _imageFiles.Count; // 默认插到最后
            int uiInsertIndex = FileTabs.Count;
            if (_currentTabItem != null && !_currentTabItem.IsNew)
            {
                int currentIndexInFiles = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentIndexInFiles >= 0) insertIndex = currentIndexInFiles + 1;

                int currentIndexInTabs = FileTabs.IndexOf(_currentTabItem);
                if (currentIndexInTabs >= 0) uiInsertIndex = currentIndexInTabs + 1;
            }

            FileTabItem firstNewTab = null;
            int addedCount = 0;

            foreach (string file in files)
            {
                // 去重检查
                if (_imageFiles.Contains(file)) continue;
                _imageFiles.Insert(insertIndex + addedCount, file);

                var newTab = new FileTabItem(file);
                newTab.IsLoading = true;

                if (uiInsertIndex + addedCount <= FileTabs.Count)
                    FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                else
                    FileTabs.Add(newTab);

                // 异步加载缩略图
                _ = newTab.LoadThumbnailAsync(AppConsts.DefaultThumbnailWidth, AppConsts.DefaultThumbnailHeight);

                if (firstNewTab == null) firstNewTab = newTab;
                addedCount++;
            }

            if (addedCount > 0)
            {
                // 更新 Slider 范围
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();

                if (firstNewTab != null)
                {
                    if (_currentTabItem != null) _currentTabItem.IsSelected = false;

                    firstNewTab.IsSelected = true;
                    _currentTabItem = firstNewTab;
                    await OpenImageAndTabs(firstNewTab.FilePath);

                    // 滚动 ImageBar
                    MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 1);
                }
            }
        }
        private void ScrollContainer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            CheckBirdEyeVisibility();
            UpdateBirdEyeView();
            UpdateRulerPositions();
        }
        private void UpdateRulerPositions()
        {
            if (!SettingsManager.Instance.Current.ShowRulers || BackgroundImage == null) return;

            Point relativePoint = CanvasWrapper.TranslatePoint(new Point(0, 0), ScrollContainer);

            // 获取 ZoomTransform 的当前缩放
            double currentZoom = ZoomTransform.ScaleX;

            // 更新标尺
            RulerTop.OriginOffset = relativePoint.X;
            RulerTop.ZoomFactor = currentZoom;
            RulerTop.InvalidateVisual(); // 触发重绘

            RulerLeft.OriginOffset = relativePoint.Y;
            RulerLeft.ZoomFactor = currentZoom;
            RulerLeft.InvalidateVisual();
        }
        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
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
        private void RefreshBitmapScalingMode()
        {
            if (BackgroundImage == null) return; // 防止空引用

            var settings = TabPaint.SettingsManager.Instance.Current;
            double threshold = (IsViewMode ? settings.ViewInterpolationThreshold : settings.PaintInterpolationThreshold) / 100.0;
            if (zoomscale >= threshold)
            {
                if (RenderOptions.GetBitmapScalingMode(BackgroundImage) != BitmapScalingMode.NearestNeighbor) RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.NearestNeighbor);
            }
            else
            {
                if (RenderOptions.GetBitmapScalingMode(BackgroundImage) != BitmapScalingMode.Linear) RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);
            }
        }

        private void OnScrollContainerMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPos = e.GetPosition(ScrollContainer);

                // 计算偏移量：鼠标向左移，视口应该向右移，所以是 上次位置 - 当前位置
                double deltaX = _lastMousePosition.X - currentPos.X;
                double deltaY = _lastMousePosition.Y - currentPos.Y;

                ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + deltaX);
                ScrollContainer.ScrollToVerticalOffset(ScrollContainer.VerticalOffset + deltaY);
                _lastMousePosition = currentPos;
            }
            if (SettingsManager.Instance.Current.ShowRulers)
            {
                Point pos = e.GetPosition(ScrollContainer);
                RulerTop.MouseMarker = pos.X;
                RulerLeft.MouseMarker = pos.Y;
            }
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateRulerPositions();
        }
        private void OnScrollContainerMouseUp(object sender, MouseButtonEventArgs e)
        {
            // 1. 处理右键菜单加载
            if (e.ChangedButton == MouseButton.Right)
            {
                // 如果菜单还没加载过，进行加载
                if (ScrollContainer.ContextMenu == null)
                {
                    LoadCanvasContextMenu();
                }

                // 再次检查（因为加载可能失败），如果成功则打开
                if (ScrollContainer.ContextMenu != null)
                {
                    ScrollContainer.ContextMenu.PlacementTarget = ScrollContainer; // 确保定位准确
                    ScrollContainer.ContextMenu.IsOpen = true;
                }
            }

            // ... 下面保留你原有的平移、画图结束逻辑 ...
            if (_isPanning)
            {
                _isPanning = false;
                ScrollContainer.ReleaseMouseCapture();
                SetViewCursor(false);
            }

            if (_isLoadingImage) return;
            if (!IsViewMode)
            {
                if (Mouse.OverrideCursor != null) Mouse.OverrideCursor = null;
                Point pos = e.GetPosition(CanvasWrapper);
                _router.ViewElement_MouseUp(pos, e);
            }
        }
        private void LoadCanvasContextMenu()
        {
            try
            {
                // 1. 动态读取独立的资源字典文件
                // 【注意】请确保 CanvasMenu.xaml 的“生成操作”是 "Page" 或 "Resource"
                // 路径格式：/程序集名称;component/文件夹/文件名.xaml
                // 假设你的文件在根目录或 Resources 目录下，请根据实际情况修改字符串
                var resourceUri = new Uri("pack://application:,,,/Controls/ContextMenus/CanvasMenu.xaml");
                var dictionary = new ResourceDictionary { Source = resourceUri };

                // 2. 从字典中提取 ContextMenu (Key 必须与 XAML 中的 x:Key 一致)
                var menu = dictionary["MainImageCtxMenu"] as ContextMenu;

                if (menu != null)
                {
                    // 3. 递归遍历所有项进行事件绑定
                    foreach (var item in menu.Items)
                    {
                        BindCanvasMenuEvents(item);
                    }

                    // 4. 赋值给控件
                    ScrollContainer.ContextMenu = menu;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"主菜单加载失败: {ex.Message}");
                // 可以在这里加一个 MessageBox 提示调试
            }
        }
        // 辅助方法：递归绑定事件
        private void BindCanvasMenuEvents(object item)
        {
            // 情况 A: 标准 MenuItem
            if (item is MenuItem menuItem)
            {
                // 先解绑防止重复（虽然懒加载只执行一次，但为了安全）
                menuItem.Click -= OnCanvasMenuClickDispatcher;

                // 根据 Tag 绑定通用处理函数，或者直接绑定具体函数
                // 这里为了代码整洁，我们使用 Switch 匹配 Tag
                if (menuItem.Tag != null)
                {
                    switch (menuItem.Tag.ToString())
                    {
                        case "Copy": menuItem.Click += OnCopyClick; break;
                        case "Cut": menuItem.Click += OnCutClick; break;
                        case "Paste": menuItem.Click += OnPasteClick; break;

                        // --- 小工具 ---
                        case "RemoveBackground": menuItem.Click += OnRemoveBackgroundClick; break;
                        case "ChromaKey": menuItem.Click += OnChromaKeyClick; break;
                        case "Ocr": menuItem.Click += OnOcrClick; break;
                        case "ScreenColorPicker": menuItem.Click += OnScreenColorPickerClick; break;
                        case "CopyColorCode": menuItem.Click += OnCopyColorCodeClick; break;
                        case "AutoCrop": menuItem.Click += OnAutoCropClick; break;
                        case "AddBorder": menuItem.Click += OnAddBorderClick; break;
                        case "AiUpscale": menuItem.Click += OnAiUpscaleClick; break;
                        case "AiOcr": menuItem.Click += OnAiOcrClick; break;
                    }
                }

                // 递归：如果 MenuItem 下面还有子菜单 (Items)
                if (menuItem.Items.Count > 0)
                {
                    foreach (var subItem in menuItem.Items)
                    {
                        BindCanvasMenuEvents(subItem);
                    }
                }
            }
            // 情况 B: 自定义控件 DelayedMenuItem (重要！你的小工具都在这里面)
            else if (item is TabPaint.Controls.DelayedMenuItem delayedItem)
            {
                foreach (var subItem in delayedItem.Items)
                {
                    BindCanvasMenuEvents(subItem);
                }
            }
        }

        // 可选：如果你不想上面写那么多 +=，可以用这个通用分发器，
        // 但上面的 switch += 方式性能更好且更直观。
        private void OnCanvasMenuClickDispatcher(object sender, RoutedEventArgs e)
        {
            // 这里的代码已经在上面的 BindCanvasMenuEvents 里用 += 具体方法 替代了，
            // 所以这个方法其实可以不需要，除非你希望所有菜单走同一个入口打印日志。
        }


        private bool _isDragOverlayVisible = false;

        public void UpdateSelectionScalingMode()
        {

            if (SelectionPreview == null) return;

            double currentZoomPercent = ZoomTransform.ScaleX * 100.0;

            // 2. 获取设置阈值
            double threshold = SettingsManager.Instance.Current.PaintInterpolationThreshold;

            var mode = (currentZoomPercent >= threshold)
                ? BitmapScalingMode.NearestNeighbor
                : BitmapScalingMode.Linear;

            if (RenderOptions.GetBitmapScalingMode(SelectionPreview) != mode)
            {
                RenderOptions.SetBitmapScalingMode(SelectionPreview, mode);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_programClosed) OnClosing();
        }
        public bool IsTransferringSelection { get; private set; } = false;

        // 暂存传输的选区数据
        private byte[] _transferSelectionData;
        private int _transferWidth;
        private int _transferHeight;

        public async void TransferSelectionToTab(FileTabItem targetTab, byte[] selectionData, int width, int height)
        {
            if (targetTab == null || targetTab == _currentTabItem) return;

            IsTransferringSelection = true;
            _transferSelectionData = selectionData;
            _transferWidth = width;
            _transferHeight = height;

            try
            {
                SwitchToTab(targetTab);
            }
            finally
            {
            }
        }
        public void RestoreTransferredSelection()
        {
            if (!IsTransferringSelection || _transferSelectionData == null || _surface == null) return;

            try
            {
                // 1. 构造 BitmapSource
                var bmp = BitmapSource.Create(_transferWidth, _transferHeight,
                    _surface.Bitmap.DpiX, _surface.Bitmap.DpiY,
                    PixelFormats.Bgra32, null, _transferSelectionData, _transferWidth * 4);

                SelectTool st = _router.GetSelectTool();

                // 确保切换到 Select 工具
                if (_router.CurrentTool != st)
                {
                    _router.SetTool(st);
                }
                st._selectionData = null;
                st._selectionRect = new Int32Rect(0, 0, 0, 0);

                // 同时也隐藏旧的预览框，防止闪烁
                if (_ctx.SelectionPreview != null)
                {
                    _ctx.SelectionPreview.Visibility = Visibility.Collapsed;
                    _ctx.SelectionPreview.Source = null;
                }
                st.InsertImageAsSelection(_ctx, bmp, expandCanvas: true);

                st.ForceDragState();

                NotifyCanvasChanged();
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_RestoreSelectionFailed_Prefix"), ex.Message));

            }
            finally
            {
                IsTransferringSelection = false;
                _transferSelectionData = null;
            }
        }
        private void SelectionToolBar_CopyClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool is SelectTool selectTool)
            {
                selectTool.CopySelection(_ctx);
                ShowToast("L_Main_Ctx_Copy");
            }
        }

        private void SelectionToolBar_AiRemoveBgClick(object sender, RoutedEventArgs e)
        {
            OnRemoveBackgroundClick(sender, e);
            // 修改为控制 Holder 的可见性
            if (SelectionToolHolder != null) SelectionToolHolder.Visibility = Visibility.Collapsed;
        }

        private void SelectionToolBar_OcrClick(object sender, RoutedEventArgs e)
        {
            OnOcrClick(sender, e);
            // 修改为控制 Holder 的可见性
            if (SelectionToolHolder != null) SelectionToolHolder.Visibility = Visibility.Collapsed;
        }

        public void UpdateSelectionToolBarPosition()
        {
            // 如果还没初始化且当前没有选区，直接返回，避免不必要的实例化
            var selectTool = _router?.CurrentTool as SelectTool;
            if (_selectionToolBar == null && (selectTool == null || !selectTool.HasActiveSelection))
            {
                return;
            }

            // 确保控件已加载 (访问属性会触发加载)
            var toolbar = this.SelectionToolBar;
            var holder = this.SelectionToolHolder; // 引用 XAML 中的 ContentControl

            if (!IsViewMode && selectTool != null && selectTool.HasActiveSelection)
            {
                Int32Rect rect = selectTool._selectionRect;
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    holder.Visibility = Visibility.Collapsed;
                    return;
                }

                Point p1 = _ctx.FromPixel(new Point(rect.X, rect.Y));
                Point p2 = _ctx.FromPixel(new Point(rect.X + rect.Width, rect.Y + rect.Height));

                Point rootPos = CanvasWrapper.TranslatePoint(p1, (UIElement)this.Content);
                Point rootPosEnd = CanvasWrapper.TranslatePoint(p2, (UIElement)this.Content);

                double selTop = rootPos.Y;
                double selLeft = rootPos.X;
                double selWidth = rootPosEnd.X - rootPos.X;

                // 注意：这里可能需要硬编码或测量实际宽度，因为第一次显示时 ActualWidth 可能为 0
                double toolbarHeight = 45;
                double toolbarWidth = 140;

                double top = selTop - toolbarHeight - 10;
                double left = selLeft + (selWidth - toolbarWidth) / 2;

                if (top < 40) top = rootPosEnd.Y + 10;
                if (top + toolbarHeight > this.ActualHeight - 20) top = this.ActualHeight - toolbarHeight - 20;

                if (left < 10) left = 10;
                if (left + toolbarWidth > this.ActualWidth - 10) left = this.ActualWidth - toolbarWidth - 10;

                // 关键修改：设置 Holder 的 Margin
                holder.Margin = new Thickness(left, top, 0, 0);
                holder.Visibility = Visibility.Visible;
            }
            else
            {
                holder.Visibility = Visibility.Collapsed;
            }
        }

        public void SyncTextToolbarState(RichTextBox rtb)
        {
            var selection = rtb.Selection;

            // 字体粗细
            var weight = selection.GetPropertyValue(TextElement.FontWeightProperty);
            TextMenu.BoldBtn.IsChecked = (weight != DependencyProperty.UnsetValue) && ((FontWeight)weight == FontWeights.Bold);

            // 斜体
            var style = selection.GetPropertyValue(TextElement.FontStyleProperty);
            TextMenu.ItalicBtn.IsChecked = (style != DependencyProperty.UnsetValue) && ((FontStyle)style == FontStyles.Italic);

            // 下划线/删除线
            var decor = selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
            TextMenu.UnderlineBtn.IsChecked = false;
            TextMenu.StrikeBtn.IsChecked = false;
            if (decor != null)
            {
                // 简单判断，实际可能需要遍历
                foreach (var d in decor)
                {
                    if (d.Location == TextDecorationLocation.Underline) TextMenu.UnderlineBtn.IsChecked = true;
                    if (d.Location == TextDecorationLocation.Strikethrough) TextMenu.StrikeBtn.IsChecked = true;
                }
            }

            // 上下标
            var baseline = selection.GetPropertyValue(Inline.BaselineAlignmentProperty);
            TextMenu.SubscriptBtn.IsChecked = false;
            TextMenu.SuperscriptBtn.IsChecked = false;
            if (baseline != DependencyProperty.UnsetValue)
            {
                var bl = (BaselineAlignment)baseline;
                if (bl == BaselineAlignment.Subscript) TextMenu.SubscriptBtn.IsChecked = true;
                if (bl == BaselineAlignment.Superscript) TextMenu.SuperscriptBtn.IsChecked = true;
            }

            // 高亮
            var bg = selection.GetPropertyValue(TextElement.BackgroundProperty);
            TextMenu.HighlightBtn.IsChecked = (bg != null && bg != DependencyProperty.UnsetValue && bg != Brushes.Transparent);

            // 阴影 (阴影是整个框的属性，不是 Selection 的，所以不用在这里读 Selection，直接读 Effect)
            TextMenu.ShadowBtn.IsChecked = (rtb.Effect is System.Windows.Media.Effects.DropShadowEffect);
        }

        private void UpdateImageBarVisibilityState()
        {
            if (MainImageBar == null || FileTabs == null) return;


            bool isSingle = FileTabs.Count <= 1;
            if (MainImageBar.IsSingleTabMode != isSingle)
            {
                MainImageBar.IsSingleTabMode = isSingle;
                CheckFittoWindow();
            }
        }
    }
}
