
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static TabPaint.MainWindow;

//
//ImageBar后台逻辑 
//

namespace TabPaint
{

    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public bool _isSavingFile = false;
        // 用于追踪每个 Tab ID 对应的正在进行的保存任务
        private Dictionary<string, Task> _activeSaveTasks = new Dictionary<string, Task>();

        private CancellationTokenSource _saveCts;

        private void TriggerBackgroundBackup()
        {

            if (_currentTabItem == null) return;
            if (_surface?.Bitmap == null) return;

            // 1. 基础检查
            if (_currentTabItem.IsDirty == false && !_currentTabItem.IsNew) return;
            if (_currentCanvasVersion == _lastBackedUpVersion &&
                !string.IsNullOrEmpty(_currentTabItem.BackupPath) &&
                File.Exists(_currentTabItem.BackupPath)) return;
            if (_saveCts != null)
            {
                _saveCts.Cancel();
                _saveCts.Dispose();
            }
            var myCts = new CancellationTokenSource();
            var token = myCts.Token;
            long versionToRecord = _currentCanvasVersion;
            string fileId = _currentTabItem.Id;
            string cacheDir = _cacheDir; // 避免闭包捕获

            var w = _surface.Bitmap.PixelWidth;
            var h = _surface.Bitmap.PixelHeight;
            var dpiX = _surface.Bitmap.DpiX;
            var dpiY = _surface.Bitmap.DpiY;
            var format = _surface.Bitmap.Format;
            var palette = _surface.Bitmap.Palette;

            // 计算步长
            int stride = (w * format.BitsPerPixel + 7) / 8;
            // 分配内存 (8K图约130MB，如果内存压力大后续可以用 ArrayPool 优化)
            byte[] rawPixels = new byte[h * stride];
            try { _surface.Bitmap.CopyPixels(new Int32Rect(0, 0, w, h), rawPixels, stride, 0); }
            catch (Exception ex) { Debug.WriteLine($"CopyPixels failed: {ex.Message}"); return; }
            _lastBackedUpVersion = versionToRecord;
            _isSavingFile = true;

            Task saveTask = Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    string fileName = $"{fileId}.png";
                    string fullPath = Path.Combine(cacheDir, fileName);

                    if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                    var frame = BitmapSource.Create(w, h, dpiX, dpiY, format, palette, rawPixels, stride);

                    // 使用临时文件写入，避免文件锁冲突或写入一半被读取
                    string tempPath = fullPath + ".tmp";

                    using (var fileStream = new FileStream(tempPath, FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(frame));
                        encoder.Save(fileStream);
                    }

                    if (token.IsCancellationRequested)
                    {
                        File.Delete(tempPath);
                        return;
                    }

                    // 原子操作替换文件
                    System.IO.File.Move(tempPath, fullPath, true);

                    // 更新 UI 模型
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var targetTab = FileTabs.FirstOrDefault(t => t.Id == fileId);
                        if (targetTab != null)
                        {
                            targetTab.BackupPath = fullPath;
                            targetTab.LastBackupTime = DateTime.Now;
                        }
                    }, DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AutoBackup Failed: {ex.Message}");
                }
                finally
                {
                    // 任务结束，从字典中移除自己
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_activeSaveTasks.ContainsKey(fileId) && _activeSaveTasks[fileId].Id == Task.CurrentId)
                        {
                            _activeSaveTasks.Remove(fileId);
                        }
                        _isSavingFile = false; // 只有当这是最后一个任务时才设为 false (简单处理)
                    });
                    myCts.Dispose();
                }
            }, token);
            if (_activeSaveTasks.ContainsKey(fileId)) _activeSaveTasks[fileId] = saveTask;
            else _activeSaveTasks.Add(fileId, saveTask);

            SaveSession();
        }

        private BitmapSource RenderCurrentCanvasToBitmap()
        {
            double width = BackgroundImage.ActualWidth;
            double height = BackgroundImage.ActualHeight;

            if (width <= 0 || height <= 0) return null;
            Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
            double dpiX = m.M11 * 96.0;
            double dpiY = m.M22 * 96.0;
            RenderTargetBitmap rtb = new RenderTargetBitmap(
                (int)(width * m.M11),
                (int)(height * m.M22),
                dpiX,
                dpiY,
                PixelFormats.Pbgra32);
            rtb.Render(BackgroundImage);

            rtb.Freeze();

            return rtb;
        }
        private BitmapSource GetHighResImageForTab(FileTabItem tabItem)
        {
            if (tabItem == null) return null;
            if (tabItem == _currentTabItem)
            {
                return RenderCurrentCanvasToBitmap();
            }
            else
            {
                if (!string.IsNullOrEmpty(tabItem.FilePath) && System.IO.File.Exists(tabItem.FilePath))
                {
                    return LoadBitmapFromFile(tabItem.FilePath);
                }
                return tabItem.Thumbnail as BitmapSource;
            }
        }
        private BitmapSource LoadBitmapFromFile(string path)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(path)?.ToLower();
                if (ext == ".svg")
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    return DecodeSvg(bytes, CancellationToken.None);
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }

        private void InitializeAutoSave()
        {
            // 确保缓存目录存在
            if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);

            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(3); // 3秒停笔后触发
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        }
        public void NotifyCanvasChanged()
        {
            if (_currentTabItem == null) return;
            _pendingDeleteUndo = null;
            _currentCanvasVersion++;
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;

            _autoSaveTimer.Stop();
            double delayMs = 2000; // 基础延迟 2秒
            if (BackgroundImage.Source is BitmapSource source)
            {
                double pixels = source.PixelWidth * source.PixelHeight;
                delayMs = pixels / 200 / PerformanceScore;
            }
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _autoSaveTimer.Start();
            CheckDirtyState();
        }

        private BitmapSource GenerateBlankThumbnail()
        {
            int width = 100;
            int height = 60;
            var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                // 绘制白色背景
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                context.DrawRectangle(null, new Pen(Application.Current.FindResource("ListItemPressedBrush") as Brush, 1), new Rect(0.5, 0.5, width - 1, height - 1));
            }
            bmp.Render(drawingVisual);
            bmp.Freeze();
            return bmp;
        }

        private void ResetToNewCanvas()
        {
            CreateNewTab(TabInsertPosition.AtEnd, true);
        }
        private void UpdateTabThumbnail(string path)
        {
            // 在 ObservableCollection 中找到对应的 Tab
            var tab = FileTabs.FirstOrDefault(t => t.FilePath == path);
            if (tab == null) return;

            double targetWidth = 100;
            double scale = targetWidth / _bitmap.PixelWidth;

            var transformedBitmap = new TransformedBitmap(_bitmap, new ScaleTransform(scale, scale));

            var newThumb = new WriteableBitmap(transformedBitmap);
            newThumb.Freeze();

            // 触发 UI 更新
            tab.Thumbnail = newThumb;
            string key = tab.FilePath;
            if (tab.IsNew && !string.IsNullOrEmpty(tab.BackupPath)) key = tab.BackupPath;

            if (!string.IsNullOrEmpty(key))
            {
                MainWindow.GlobalThumbnailCache.Add(key, transformedBitmap);
            }
        }
        private void UpdateTabThumbnail(FileTabItem tabItem)
        {//用当前canvas更新tabitem的thumbail
            if (tabItem == null || BackgroundImage.ActualWidth <= 0) return;

            try
            {
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)BackgroundImage.ActualWidth,
                    (int)BackgroundImage.ActualHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                rtb.Render(BackgroundImage);
                rtb.Freeze(); // 冻结以便跨线程使用（如果需要）

                double scale = 60.0 / rtb.PixelHeight;
                if (scale > 1) scale = 1; // 不放大

                var scaleTransform = new ScaleTransform(scale, scale);
                var transformedBitmap = new TransformedBitmap(rtb, scaleTransform);
                transformedBitmap.Freeze();

                // 3. 立即更新 UI (ViewModel)
                tabItem.Thumbnail = transformedBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail update failed: {ex.Message}");
            }
        }
        private void UpdateTabThumbnailFromBitmap(FileTabItem tabItem, BitmapSource bitmap)
        {//用当前canvas更新tabitem的thumbail
            if (tabItem == null || BackgroundImage.ActualWidth <= 0) return;

            try
            {
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)bitmap.PixelWidth,
                    (int)bitmap.PixelHeight,
                    96d, 96d, PixelFormats.Pbgra32);


                double scale = 60.0 / rtb.PixelHeight;
                if (scale > 1) scale = 1; // 不放大

                var scaleTransform = new ScaleTransform(scale, scale);
                var transformedBitmap = new TransformedBitmap(bitmap, scaleTransform);
                transformedBitmap.Freeze();

                // 3. 立即更新 UI (ViewModel)
                tabItem.Thumbnail = transformedBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail update failed: {ex.Message}");
            }
        }
        private async void AutoSaveTimer_Tick(object sender, EventArgs e)
        {

            if (System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                // 鼠标还按着，重置计时器，推迟 500ms 后再试
                _autoSaveTimer.Stop();
                _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
                _autoSaveTimer.Start();
                return;
            }

            _autoSaveTimer.Stop(); // 停止计时
            if (_currentTabItem != null)
            {
                UpdateTabThumbnail(_currentTabItem);
                TriggerBackgroundBackup();
            }
        }
        private BitmapSource GetCurrentCanvasSnapshotSafe()
        {
            if (BackgroundImage == null || BackgroundImage.ActualWidth <= 0) return null;

            try
            {
                // 1. 渲染
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)BackgroundImage.ActualWidth,
                    (int)BackgroundImage.ActualHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                rtb.Render(BackgroundImage);
                var safeBitmap = new WriteableBitmap(rtb);
                safeBitmap.Freeze(); // 冻结以供跨线程使用

                return safeBitmap;
            }
            catch
            {
                return null;
            }
        }
        private BitmapSource GetCurrentCanvasSnapshot()
        {
            return GetCurrentCanvasSnapshotSafe();
        }
        public async void OnClosing()
        {
            if (_programClosed) return;
            _programClosed = true;

            try
            {
                this.Hide();

                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource.Dispose();
                    _hwndSource = null;
                }

                SaveAppState();
                CommitPendingDeletions();
                // 立即保存当前的
                if (_currentTabItem != null && _currentTabItem.IsDirty && !_isSavingFile)
                {
                    _autoSaveTimer.Stop();
                    var bmp = GetCurrentCanvasSnapshot();
                    if (bmp != null)
                    {
                        if (!System.IO.Directory.Exists(_cacheDir))
                        {
                            System.IO.Directory.CreateDirectory(_cacheDir);
                        }

                        string path = System.IO.Path.Combine(_cacheDir, $"{_currentTabItem.Id}.png");

                        // 使用 try-catch 包裹具体的文件写入，防止单个文件写入失败导致崩溃
                        try
                        {
                            using (var fs = new FileStream(path, FileMode.Create))
                            {
                                BitmapEncoder encoder = new PngBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(bmp));
                                encoder.Save(fs);
                            }
                            _currentTabItem.BackupPath = path;
                        }
                        catch (Exception ex)
                        {
                            // 可以在这里记录日志，但不要抛出异常，让程序继续关闭
                            System.Diagnostics.Debug.WriteLine($"保存退出缓存失败: {ex.Message}");
                        }
                    }
                }

                SaveSession(); // 更新 JSON
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnClosing 全局异常: {ex.Message}");
            }
            finally
            {
                // 确保窗口关闭
                this.Close();
            }
        }
        private int GetNextAvailableUntitledNumber()
        {
            // 获取当前正在使用的所有“未命名”编号
            var usedNumbers = new HashSet<int>();

            foreach (var path in _imageFiles)
            {
                if (IsVirtualPath(path))
                {
                    // 提取 ::TABPAINT_NEW:: 之后的数字
                    string numPart = path.Replace(VirtualFilePrefix, "");
                    if (int.TryParse(numPart, out int num))
                    {
                        usedNumbers.Add(num);
                    }
                }
            }
            foreach (var pendingTab in _pendingDeletionTabs)
            {
                if (IsVirtualPath(pendingTab.FilePath))
                {
                    string numPart = pendingTab.FilePath.Replace(VirtualFilePrefix, "");
                    if (int.TryParse(numPart, out int num))
                    {
                        usedNumbers.Add(num);
                    }
                }
            }
            // 从 1 开始找，第一个不在 HashSet 里的数字就是我们要的
            int candidate = 1;
            while (usedNumbers.Contains(candidate))
            {
                candidate++;
            }
            return candidate;
        }
        private void SaveSession()
        {
            // 1. 准备当前内存中的数据
            var currentSessionTabs = new List<SessionTabInfo>();
            string currentContextDir = null;  // 获取当前上下文目录：必须基于真实的物理文件路径

            // 优先从 _imageFiles 列表中找第一个真实的文件路径来确定当前“工作目录”
            string? firstRealFile = _imageFiles?.FirstOrDefault(f => !string.IsNullOrEmpty(f) && !IsVirtualPath(f));

            if (firstRealFile != null)
            {
                try
                {
                    currentContextDir = System.IO.Path.GetDirectoryName(firstRealFile);
                    if (currentContextDir != null)
                        currentContextDir = System.IO.Path.GetFullPath(currentContextDir);
                }
                catch { currentContextDir = null; }
            }

            foreach (var tab in FileTabs)
            {
                // 只有修改过的或新建的才需要记入 Session
                if (tab.IsDirty || tab.IsNew)
                {
                    string? tabDir = null;
                    // 如果是真实文件，获取它的实际目录
                    if (!string.IsNullOrEmpty(tab.FilePath) && !IsVirtualPath(tab.FilePath))
                    {
                        try
                        {
                            tabDir = System.IO.Path.GetDirectoryName(tab.FilePath);
                            if (tabDir != null) tabDir = System.IO.Path.GetFullPath(tabDir);
                        }
                        catch { tabDir = null; }
                    }
                    if (string.IsNullOrEmpty(tabDir)) tabDir = currentContextDir;
                    currentSessionTabs.Add(new SessionTabInfo
                    {
                        Id = tab.Id,
                        OriginalPath = tab.FilePath, // 这里可能是 ::TABPAINT_NEW::1
                        BackupPath = tab.BackupPath,
                        IsDirty = tab.IsDirty,
                        IsNew = tab.IsNew,
                        WorkDirectory = tabDir,
                        UntitledNumber = tab.UntitledNumber
                    });
                }
            }

            var finalTabsToSave = new List<SessionTabInfo>();
            finalTabsToSave.AddRange(currentSessionTabs);
            if (File.Exists(_sessionPath))  // 合并旧 Session 数据
            {
                try
                {
                    var oldJson = File.ReadAllText(_sessionPath);
                    var oldSession = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(oldJson);

                    if (oldSession != null && oldSession.Tabs != null)
                    {
                        // 如果当前有确定的目录，则保留其他目录的 Tab
                        foreach (var oldTab in oldSession.Tabs)
                        {
                            string? oldTabDir = oldTab.WorkDirectory;

                            // 兼容逻辑：处理旧数据中没有 WorkDirectory 的情况
                            if (string.IsNullOrEmpty(oldTabDir) && !string.IsNullOrEmpty(oldTab.OriginalPath) && !IsVirtualPath(oldTab.OriginalPath))
                            {
                                try { oldTabDir = System.IO.Path.GetDirectoryName(oldTab.OriginalPath); } catch { }
                            }

                            if (oldTabDir != null)
                            {
                                try { oldTabDir = System.IO.Path.GetFullPath(oldTabDir); } catch { }
                            }

                            bool isDifferentDir = (currentContextDir == null) ||
                                                 (oldTabDir != null && !oldTabDir.Equals(currentContextDir, StringComparison.OrdinalIgnoreCase));

                            if (isDifferentDir)
                            {
                                if (!finalTabsToSave.Any(t => t.Id == oldTab.Id))
                                    finalTabsToSave.Add(oldTab);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Merge session failed: " + ex.Message);
                }
            }
            var session = new PaintSession
            {
                LastViewedFile = _currentTabItem?.FilePath ?? (_imageFiles.Count > _currentImageIndex ? _imageFiles[_currentImageIndex] : null),
                Tabs = finalTabsToSave
            };
            try
            {
                string? dir = System.IO.Path.GetDirectoryName(_sessionPath);
                if (dir != null) Directory.CreateDirectory(dir);

                string json = System.Text.Json.JsonSerializer.Serialize(session);
                File.WriteAllText(_sessionPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("保存会话失败: " + ex.Message);
            }
        }


        private void LoadSession()
        {
            if (!File.Exists(_sessionPath)) return;

            try
            {
                var json = File.ReadAllText(_sessionPath);
                var session = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(json);
                string startupDir = null;
                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    startupDir = System.IO.Path.GetDirectoryName(_currentFilePath);
                    if (startupDir != null) startupDir = System.IO.Path.GetFullPath(startupDir);
                }

                if (session.Tabs != null)
                {
                    foreach (var info in session.Tabs)
                    {
                        {
                            string tabDir = info.WorkDirectory;

                            // 如果旧版本没有记录 WorkDirectory，尝试从 OriginalPath 推断
                            if (string.IsNullOrEmpty(tabDir) && !string.IsNullOrEmpty(info.OriginalPath))
                            {
                                tabDir = System.IO.Path.GetDirectoryName(info.OriginalPath);
                            }

                            if (tabDir != null)
                            {
                                try
                                {
                                    tabDir = System.IO.Path.GetFullPath(tabDir);
                                }
                                catch (Exception ex) { }
                            }
                            if (string.Compare(tabDir, startupDir, StringComparison.OrdinalIgnoreCase) != 0 && !info.IsNew)
                            {// 目录不匹配且不是新建文件，跳过
                                continue;
                            }
                            if (info.IsNew && !info.IsDirty) continue;
                        }

                        if (!string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath))
                        {
                            var tab = new FileTabItem(info.OriginalPath)
                            {
                                Id = info.Id,
                                IsNew = info.IsNew,
                                IsDirty = info.IsDirty,
                                BackupPath = info.BackupPath,
                                UntitledNumber = info.UntitledNumber,
                                IsLoading = true,
                            };
                            //s(tab.BackupPath);
                            FileTabs.Add(tab);

                            _ = tab.LoadThumbnailAsync(100, 60).ContinueWith(t =>
                            {
                                tab.IsLoading = false;
                            }, TaskScheduler.FromCurrentSynchronizationContext());
                        }
                    }
                }
                UpdateImageBarVisibilityState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Session Load Error: {ex.Message}");
            }
        }
        private void ResetDirtyTracker()
        {

            if (_undo != null) { _undo.ClearUndo(); _undo.ClearRedo(); } // 1. 清空撤销栈
            if (_currentTabItem != null && _currentTabItem.IsDirty) { _savedUndoPoint = -1; }// 2. 智能重置保存点
            else
            {
                _savedUndoPoint = 0;
                if (_currentTabItem != null) _currentTabItem.IsDirty = false;
            }
            SetUndoRedoButtonState();
        }
        enum TabInsertPosition
        {
            AfterCurrent,
            AtEnd,
            AtStart
        }
        private void CreateNewTab(TabInsertPosition tabposition = TabInsertPosition.AfterCurrent, bool switchto = false)
        {
            int availableNumber = GetNextAvailableUntitledNumber();
            string virtualPath = $"{VirtualFilePrefix}{availableNumber}";

            // 2. 创建 Tab 对象
            var newTab = new FileTabItem(virtualPath)
            {
                IsNew = true,
                UntitledNumber = availableNumber,
                IsDirty = false,
                Id = $"Virtual_{availableNumber}",
                BackupPath = null
            };

            newTab.Thumbnail = GenerateBlankThumbnail();

            // 3. 确定插入位置 (保持之前的逻辑)
            int listInsertIndex = _imageFiles.Count;

            if (_currentTabItem != null)
            {
                int currentListIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentListIndex >= 0) listInsertIndex = currentListIndex + 1;
            }

            // 4. 更新数据源
            _imageFiles.Insert(listInsertIndex, virtualPath);

            // UI 插入逻辑 (这里为了简单，我们插在当前选中项后面，或者末尾)

            int uiInsertIndex = _currentTabItem != null ? FileTabs.IndexOf(_currentTabItem) + 1 : FileTabs.Count;
            if (tabposition == TabInsertPosition.AfterCurrent)
                FileTabs.Insert(uiInsertIndex, newTab);
            if (tabposition == TabInsertPosition.AtEnd)
            {
                FileTabs.Add(newTab);
                if (VisualTreeHelper.GetChildrenCount(MainImageBar.TabList) > 0) MainImageBar.Scroller.ScrollToRightEnd();
            }
            if (tabposition == TabInsertPosition.AtStart)
                FileTabs.Insert(0, newTab);
            if (switchto) SwitchToTab(newTab);

            UpdateImageBarVisibilityState();
            UpdateImageBarSliderState();
        }

        public async void SwitchToTab(FileTabItem tab)
        {
            if (_currentTabItem == tab) return;
            if (tab == null) return;

            if (_currentTabItem != null)
            {
                _autoSaveTimer.Stop();

                if (_currentTabItem.IsDirty || _currentTabItem.IsNew)
                {
                    UpdateTabThumbnail(_currentTabItem);
                }

                // 保存当前 Tab 的撤销栈
                _currentTabItem.UndoStack = new List<UndoAction>(_undo.GetUndoStack());
                _currentTabItem.RedoStack = new List<UndoAction>(_undo.GetRedoStack());
                _currentTabItem.SavedUndoPoint = _savedUndoPoint;
            }

            tab.LastAccessTime = DateTime.Now;

            if (!IsTransferringSelection)
            {
                _router.CleanUpSelectionandShape();
            }

            // 1. UI 选中状态同步
            foreach (var t in FileTabs) t.IsSelected = (t == tab);

            _currentFilePath = tab.FilePath;
            _currentFileName = tab.FileName;
            _currentImageIndex = _imageFiles.IndexOf(tab.FilePath);

            {
                await OpenImageAndTabs(tab.FilePath);
            }

            // 还原新 Tab 的撤销栈
            _undo.ClearUndo();
            _undo.ClearRedo();
            if (tab.UndoStack != null)
            {
                foreach (var action in tab.UndoStack) _undo.GetUndoStack().Add(action);
            }
            if (tab.RedoStack != null)
            {
                foreach (var action in tab.RedoStack) _undo.GetRedoStack().Add(action);
            }
            _savedUndoPoint = tab.SavedUndoPoint;

            // 4. 状态重置 (不再直接调用 ResetDirtyTracker，因为我们要保留撤销栈)
            // ResetDirtyTracker(); 
            _currentTabItem = tab;
            SetUndoRedoButtonState();
            UpdateWindowTitle();

            if (IsTransferringSelection)
            {
                await Task.Delay(50);
                RestoreTransferredSelection();
            }
        }
        private void ResetCanvasView()
        {
            // 使用 Loaded 优先级，确保 ScrollViewer 已经感知到了新的图片尺寸
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (ScrollContainer.ViewportWidth > 0 && ScrollContainer.ExtentWidth > 0)
                {
                    double offsetX = (ScrollContainer.ExtentWidth - ScrollContainer.ViewportWidth) / 2;
                    double offsetY = (ScrollContainer.ExtentHeight - ScrollContainer.ViewportHeight) / 2;

                    // 3. 执行滚动 (防止负数)
                    ScrollContainer.ScrollToHorizontalOffset(Math.Max(0, offsetX));
                    ScrollContainer.ScrollToVerticalOffset(Math.Max(0, offsetY));
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private bool SaveSingleTab(FileTabItem tab)
        {
            try
            {
                if (tab.IsNew && IsVirtualPath(tab.FilePath))
                {
                    var bmp = GetHighResImageForTab(tab);
                    if (bmp == null) return false;

                    Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                    dlg.FileName = tab.FileName;
                    dlg.DefaultExt = ".png";
                    dlg.Filter = "PNG Image (.png)|*.png|JPEG Image (.jpg)|*.jpg|All files (*.*)|*.*";

                    if (dlg.ShowDialog() == true)
                    {
                        string realPath = dlg.FileName;
                        string oldPath = tab.FilePath; // 记录虚拟路径

                        // 保存文件
                        using (var fs = new FileStream(realPath, FileMode.Create))
                        {
                            BitmapEncoder encoder;
                            if (realPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                realPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                                encoder = new JpegBitmapEncoder();
                            else
                                encoder = new PngBitmapEncoder();

                            encoder.Frames.Add(BitmapFrame.Create(bmp));
                            encoder.Save(fs);
                        }

                        // 更新数据列表
                        int index = _imageFiles.IndexOf(oldPath);
                        if (index >= 0) _imageFiles[index] = realPath;
                        else _imageFiles.Add(realPath);

                        // 更新 Tab 状态
                        tab.FilePath = realPath;
                        tab.IsNew = false;
                        tab.IsDirty = false;

                        if (tab == _currentTabItem)
                        {
                            _currentFilePath = realPath;
                            _currentFileName = tab.FileName;
                            UpdateWindowTitle();
                        }

                        // 清理备份
                        if (File.Exists(tab.BackupPath)) File.Delete(tab.BackupPath);
                        tab.BackupPath = null;

                        return true; // 保存成功
                    }
                    return false; // 用户取消了对话框
                }
                else
                {
                    // 2. 普通文件保存 (已有路径)
                    if (tab == _currentTabItem)
                    {
                        var bmp = GetCurrentCanvasSnapshot();
                        if (bmp == null) return false;

                        using (var fs = new FileStream(tab.FilePath, FileMode.Create))
                        {
                            BitmapEncoder encoder;
                            if (tab.FilePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                tab.FilePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                                encoder = new JpegBitmapEncoder();
                            else
                                encoder = new PngBitmapEncoder();

                            encoder.Frames.Add(BitmapFrame.Create(bmp));
                            encoder.Save(fs);
                        }
                    }
                    else if (File.Exists(tab.BackupPath))
                    {
                        // 后台标签：将缓存覆盖到原位
                        File.Copy(tab.BackupPath, tab.FilePath, true);
                    }
                    else
                    {
                        tab.IsDirty = false;
                        return true;
                    }

                    tab.IsDirty = false;
                    tab.IsNew = false;

                    if (File.Exists(tab.BackupPath)) File.Delete(tab.BackupPath);
                    tab.BackupPath = null;

                    return true;
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_SaveFailed_Prefix"), ex.Message));
                return false;
            }
        }

        public void CheckDirtyState()
        {
            if (_currentTabItem == null || _undo == null) return;

            int currentCount = _undo.UndoCount;
            bool isDirty = currentCount != _savedUndoPoint;
            if (_currentTabItem.IsDirty != isDirty)
            {
                _currentTabItem.IsDirty = isDirty;
            }
        }
        private void SaveBitmapToPng(BitmapSource bitmap, string filePath)
        {
            if (bitmap == null) return;

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(fileStream);
            }
        }// 修改原 LoadSession 方法，或者新建一个专门用于工作区切换加载的方法
        private void LoadSessionForCurrentWorkspace(string workspaceFilePath)
        {
            if (!File.Exists(_sessionPath)) return;

            try
            {
                string workspaceDir = System.IO.Path.GetDirectoryName(workspaceFilePath);
                if (workspaceDir != null) workspaceDir = System.IO.Path.GetFullPath(workspaceDir);

                var json = File.ReadAllText(_sessionPath);
                var session = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(json);

                if (session.Tabs != null)
                {
                    foreach (var info in session.Tabs)
                    {
                        // 1. 路径判定
                        string tabDir = info.WorkDirectory;

                        // 兼容旧数据
                        if (string.IsNullOrEmpty(tabDir) && !string.IsNullOrEmpty(info.OriginalPath) && !IsVirtualPath(info.OriginalPath))
                        {
                            try { tabDir = System.IO.Path.GetDirectoryName(info.OriginalPath); } catch { }
                        }

                        if (tabDir != null) tabDir = System.IO.Path.GetFullPath(tabDir);
                        bool shouldLoad = false;

                        if (workspaceDir != null && tabDir != null &&
                            string.Equals(tabDir, workspaceDir, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldLoad = true;
                        }

                        if (shouldLoad)
                        {
                            // 检查是否已经在 FileTabs 里了（防止重复添加）
                            if (FileTabs.Any(t => t.Id == info.Id || t.FilePath == info.OriginalPath))
                            {
                                var existingTab = FileTabs.First(t => t.Id == info.Id || t.FilePath == info.OriginalPath);

                                if (!string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath))
                                {
                                    existingTab.BackupPath = info.BackupPath;
                                    existingTab.IsDirty = info.IsDirty;
                                    existingTab.IsNew = info.IsNew; // 或者是 false，取决于逻辑
                                                                    // 触发重新加载缩略图显示修改后的样子
                                    _ = existingTab.LoadThumbnailAsync(100, 60);
                                }
                            }
                            else
                            {
                                // 如果列表里没有（比如是一个新建的未命名文件，物理磁盘上不存在），则添加
                                if (!string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath))
                                {
                                    var tab = new FileTabItem(info.OriginalPath)
                                    {
                                        Id = info.Id,
                                        IsNew = info.IsNew,
                                        IsDirty = info.IsDirty,
                                        BackupPath = info.BackupPath,
                                        UntitledNumber = info.UntitledNumber

                                    };
                                    FileTabs.Add(tab);
                                    _ = tab.LoadThumbnailAsync(100, 60);
                                }
                            }
                        }
                    }
                }
                UpdateImageBarVisibilityState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Workspace Session Load Error: {ex.Message}");
            }
        }

        private bool IsVirtualPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith(VirtualFilePrefix);
        }
        private string GenerateVirtualPath()   // 格式： ::TABPAINT_NEW::{ID}
        {
            return $"{VirtualFilePrefix}{GetNextAvailableUntitledNumber()}";
        }
    }
}