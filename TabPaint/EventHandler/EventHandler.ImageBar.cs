//
//EventHandler.ImageBar.cs
//标签栏相关的事件处理，包括标签切换、关闭、右键菜单功能（复制、文件夹打开）以及标签页排序逻辑。
//
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Windows;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnTabStickImageClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is FileTabItem tabItem)
            {
                StickTabImage(tabItem);
            }
            // 兼容性处理：如果 ImageBarControl 里的 Invoke 传的是 e.OriginalSource
            else if (e.OriginalSource is MenuItem originItem && originItem.Tag is FileTabItem originTab)
            {
                StickTabImage(originTab);
            }
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Win32Point lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };
        private void StickTabImage(FileTabItem tabItem)
        {
            if (tabItem == null) return;

            try
            {
                var bitmap = GetHighResImageForTab(tabItem);

                if (bitmap != null)
                {
                    if (bitmap.IsFrozen == false && bitmap.CanFreeze)
                    {
                        bitmap.Freeze();
                    }

                    var stickyWin = new StickyWindow(bitmap);

                    Win32Point p;
                    GetCursorPos(out p);
                    var mouseX = p.X;
                    var mouseY = p.Y;
                    stickyWin.Left = mouseX - (stickyWin.Width / 2);
                    stickyWin.Top = mouseY - (stickyWin.Height / 2);

                    stickyWin.Show();
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Stick Image Failed: {ex.Message}");
            }
        }
        private void OnFileTabCloseClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (e.OriginalSource is FrameworkElement element && element.Tag is FileTabItem clickedItem)
            {
                CloseTab(clickedItem);
            }
        }
        private async void OnFileTabClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element && element.DataContext is FileTabItem clickedItem)
            {
                SwitchToTab(clickedItem);
            }
        }

        private FileTabItem CreateNewUntitledTab()
        {
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsDirty = false,
                UntitledNumber = GetNextAvailableUntitledNumber(),
                Thumbnail = GenerateBlankThumbnail()
            };
            return newTab;
        }

        private void OnPrependTabClick(object sender, RoutedEventArgs e)
        {

            CreateNewTab(TabInsertPosition.AtStart, false);
        }
        private void SaveAll(bool isDoubleClick)
        {
            // 筛选出所有脏文件
            var dirtyTabs = FileTabs.Where(t => t.IsDirty).ToList();
            if (dirtyTabs.Count == 0) return;
            int successCount = 0;
            foreach (var tab in dirtyTabs)
            {
                // 跳过没有路径的新建文件 (避免弹出10个保存对话框)
                if (IsVirtualPath(tab.FilePath) && (!isDoubleClick)) continue;
                if (SaveSingleTab(tab)) successCount++;
            }
            SaveSession();
            // 简单提示 (实际项目中建议用 Statusbar)
            if (successCount > 0)
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_SavedCount_Format"), successCount));
        }
        private void OnSaveAllDoubleClick(object sender, RoutedEventArgs e)
        {
            SaveAll(true);
        }
        private void OnSaveAllClick(object sender, RoutedEventArgs e)
        {
            SaveAll(false);
        }
        private void OnClearUneditedClick(object sender, RoutedEventArgs e)
        {
            var dirtyTabs = FileTabs.Where(t => t.IsDirty).ToList();
            var dirtyPaths = new HashSet<string>(dirtyTabs.Select(t => t.FilePath));

            var filesToRemove = _imageFiles.Where(f => !dirtyPaths.Contains(f)).ToList();

            foreach (var path in filesToRemove)
            {
                if (!IsVirtualPath(path))  _explicitlyClosedFiles.Add(path);
            }

            _imageFiles = _imageFiles.Where(f => dirtyPaths.Contains(f)).ToList();

            var originalCurrent = _currentTabItem;
            bool currentWasRemoved = originalCurrent != null && !originalCurrent.IsDirty;

            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                if (!tab.IsDirty)
                {
                    if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath))
                    {
                        try { File.Delete(tab.BackupPath); } catch { }
                    }
                    FileTabs.RemoveAt(i);
                }
            }
            if (FileTabs.Count == 0)  ResetToNewCanvas();
            else
            {
                if (currentWasRemoved)
                {
                    var nextTab = FileTabs.Last();
                    SwitchToTab(nextTab);
                }
                else
                {
                    if (_currentTabItem != null)_currentImageIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                }
            }

            UpdateImageBarSliderState();
            ShowToast(string.Format(LocalizationManager.GetString("L_Toast_CleanedCount_Format"), filesToRemove.Count));
        }
        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            CreateNewTab(TabInsertPosition.AtEnd, true); CheckFittoWindow();
        }
        private async void OnDiscardAllClick(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.Instance.Current.SkipResetConfirmation)
            {
                var result = FluentMessageBox.Show(
                  LocalizationManager.GetString("L_Msg_ResetWorkspace_Content"),
                  LocalizationManager.GetString("L_Msg_ResetWorkspace_Title"),
                  MessageBoxButton.YesNo
                 );

                if (result != MessageBoxResult.Yes) return;
            }
          
            _autoSaveTimer.Stop();
            _activeSaveTasks.Clear(); // 清空任务字典
            FileTabs.Clear();
            if (_surface != null && _surface.Bitmap != null)
            {
                try
                {
                    _surface.Bitmap.Lock();
                    int w = _surface.Bitmap.PixelWidth;
                    int h = _surface.Bitmap.PixelHeight;
                    int stride = _surface.Bitmap.BackBufferStride;
                    // 全白填充 (假设是 Bgra32)
                    unsafe
                    {
                        byte* pPixels = (byte*)_surface.Bitmap.BackBuffer;
                        int len = h * stride;
                        for (int i = 0; i < len; i++)
                        {
                            pPixels[i] = 255;
                        }
                    }
                    _surface.Bitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
                    _surface.Bitmap.Unlock();
                }
                catch { }
            }
            if (_currentTabItem != null)
            {
                _currentTabItem.IsDirty = false;
                _currentTabItem.IsNew = false;
                _currentTabItem.BackupPath = null; // 切断缓存路径关联
            }
            CloseTab(_currentTabItem);
            try
            {
                _router.CleanUpSelectionandShape();

                // 1. 删除 Session.json
                if (File.Exists(_sessionPath))
                {
                    File.Delete(_sessionPath);
                }

                if (Directory.Exists(_cacheDir))
                {
                    string[] cacheFiles = Directory.GetFiles(_cacheDir);
                    foreach (string file in cacheFiles)
                    {
                        try
                        {
                            if (file.EndsWith(".onnx")) continue;
                            File.Delete(file);
                        }
                        catch {}
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_CleanupFailed_Prefix"), ex.Message));
            }
            var originalCurrentTab = _currentTabItem;
            bool currentTabAffected = false;
            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                tab.BackupPath = null;

                if (tab.IsNew)
                {
                    // A. 对于新建的文件：直接移除
                    if (tab == originalCurrentTab) currentTabAffected = true;

                    if (_imageFiles.Contains(tab.FilePath))
                    {
                        _imageFiles.Remove(tab.FilePath);
                    }

                    FileTabs.RemoveAt(i);
                }
                else if (tab.IsDirty)
                {
                    tab.IsDirty = false;
                    if (tab == originalCurrentTab) currentTabAffected = true;
                    tab.IsLoading = true;
                    await tab.LoadThumbnailAsync(100, 60);
                    tab.IsLoading = false;
                }
            }

            if (FileTabs.Count == 0)
            {
                _imageFiles.Clear();
                ResetToNewCanvas();
            }
            else if (currentTabAffected)
            {
                if (!FileTabs.Contains(originalCurrentTab))
                {
                    var firstTab = FileTabs.FirstOrDefault();
                    if (firstTab != null)
                    {
                        foreach (var t in FileTabs) t.IsSelected = false;
                        firstTab.IsSelected = true;
                        SwitchToTab(firstTab);
                    }
                }
                else
                {
                    if (_currentTabItem != null)
                    {
                        await OpenImageAndTabs(_currentTabItem.FilePath);
                        ResetDirtyTracker();
                    }
                }
            }
            else
            {
                ResetDirtyTracker();
            }

            GC.Collect();
            UpdateImageBarSliderState();
            if (File.Exists(_workingPath)) await SwitchWorkspaceToNewFile(_workingPath);
            if (!string.IsNullOrEmpty(_workingPath) && Directory.Exists(_workingPath))
            {
                _workingPath = FindFirstImageInDirectory(_workingPath); await SwitchWorkspaceToNewFile(_workingPath);
            }
           

        }
        private void OnTabOpenFolderClick(object sender, RoutedEventArgs e)
        {
            // 获取绑定的 Tab 对象
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                if (string.IsNullOrEmpty(tab.FilePath)) return;
                if (!System.IO.File.Exists(tab.FilePath))
                {
                    ShowToast("L_Toast_FileNotFound");
                    return;
                }
                try
                {
                    // 3. 使用 explorer.exe 的 /select 参数来打开文件夹并选中文件
                    string argument = $"/select, \"{tab.FilePath}\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                catch (Exception ex)
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_OpenFolderFailed_Prefix"), ex.Message));
                }
            }
        }


        private void OnFileTabPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                if (e.OriginalSource is FrameworkElement element && element.DataContext is FileTabItem clickedItem)

                {
                    CloseTab(clickedItem); // 复用已有的关闭逻辑
                    e.Handled = true; // 阻止事件冒泡，防止触发其他点击行为
                }
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                _dragStartPoint = e.GetPosition(null);
                var button = sender as System.Windows.Controls.Button;
                _mouseDownTabItem = button?.DataContext as FileTabItem;
            }
        }
        private string PrepareDragFilePath(FileTabItem tab)
        {

            if (!tab.IsDirty && !tab.IsNew && !IsVirtualPath(tab.FilePath))return tab.FilePath;
            try
            {
                BitmapSource bitmapToSave = null;

                if (tab == _currentTabItem)
                {
                    bitmapToSave = GetCurrentCanvasSnapshotSafe();
                }
                else
                {
                    // 尝试读取后台备份 (BackupPath)
                    if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath))
                    {
                        bitmapToSave = LoadBitmapFromFile(tab.BackupPath);
                    }
                    else
                    {
                        if (!IsVirtualPath(tab.FilePath)) return tab.FilePath;
                    }
                }

                if (bitmapToSave != null)
                {
                    string tempFolder = System.IO.Path.Combine(_cacheDir, "DragTemp");
                    if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

                    string fileName = tab.FileName;

                    // 确保有后缀名，如果没有默认给png
                    if (!fileName.Contains(".")) fileName += ".png";

                    string tempFilePath = System.IO.Path.Combine(tempFolder, fileName);

                    bool isJpeg = fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                  fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
                    if (isJpeg) bitmapToSave = ConvertToWhiteBackground(bitmapToSave);

                    using (var fs = new FileStream(tempFilePath, FileMode.Create))
                    {
                        BitmapEncoder encoder;
                        if (isJpeg)
                        {
                            var jpgEncoder = new JpegBitmapEncoder();
                            jpgEncoder.QualityLevel = 90; // 建议设置高质量
                            encoder = jpgEncoder;
                        }
                        else
                        {
                            encoder = new PngBitmapEncoder();
                        }

                        encoder.Frames.Add(BitmapFrame.Create(bitmapToSave));
                        encoder.Save(fs);
                    }
                    return tempFilePath;
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_PrepareDragFailed_Prefix"), ex.Message));
            }

            // 如果上面失败了，且原路径是真实存在的，作为兜底返回原路径
            if (!IsVirtualPath(tab.FilePath) && File.Exists(tab.FilePath))
            {
                return tab.FilePath;
            }

            return null;
        }

        private void OnFileTabPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (_mouseDownTabItem == null) return;

            Vector diff = _dragStartPoint - e.GetPosition(null);

            if (Math.Abs(diff.X) < _dragThreshold && Math.Abs(diff.Y) < _dragThreshold) return;
            try
            {
                var dataObject = new System.Windows.DataObject();

                dataObject.SetData("TabPaintReorderItem", _mouseDownTabItem);
                dataObject.SetData("TabPaintInternalDrag", true);

                string externalDragPath = PrepareDragFilePath(_mouseDownTabItem);

                if (!string.IsNullOrEmpty(externalDragPath) && System.IO.File.Exists(externalDragPath))
                {
                    var fileList = new System.Collections.Specialized.StringCollection();
                    fileList.Add(externalDragPath);
                    dataObject.SetFileDropList(fileList);
                }

                DragDrop.DoDragDrop((DependencyObject)sender, dataObject, DragDropEffects.Copy);

                e.Handled = true;
                _mouseDownTabItem = null;
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_DragStartFailed_Prefix"), ex.Message));
            }
        }


        private void OnFileTabLeave(object sender, DragEventArgs e)
        {
            ClearDragFeedback(sender);
        }
        private void OnFileTabReorderDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintReorderItem"))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;

                if (sender is Grid grid)
                {
                    // 获取鼠标在当前 Item 内的相对坐标
                    Point p = e.GetPosition(grid);
                    double width = grid.ActualWidth;

                    var insLine = FindVisualChild<Border>(grid, "InsertLine");

                    if (insLine == null) return;
                    insLine.Visibility = Visibility.Visible;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        private T FindVisualChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // 如果是要找的类型且名字匹配 (如果传了名字)
                if (child is T tChild && (string.IsNullOrEmpty(childName) || tChild.Name == childName))
                {
                    return tChild;
                }
                var result = FindVisualChild<T>(child, childName);
                if (result != null) return result;
            }
            return null;
        }
        private void ClearDragFeedback(object sender)
        {
            if (sender is Grid grid)
            {
                var leftLine = FindVisualChild<Border>(grid, "InsertLine");
                if (leftLine != null) leftLine.Visibility = Visibility.Collapsed;
            }
        }

        private void OnFileTabDrop(object sender, System.Windows.DragEventArgs e)
        {
            ClearDragFeedback(sender);

            if (e.Data.GetDataPresent("TabPaintReorderItem"))
            {
                var sourceTab = e.Data.GetData("TabPaintReorderItem") as FileTabItem;

                var targetGrid = sender as Grid;
                var targetTab = targetGrid?.DataContext as FileTabItem;

                if (sourceTab != null && targetTab != null && sourceTab != targetTab)
                {
                    int oldUIIndex = FileTabs.IndexOf(sourceTab);
                    int targetUIIndex = FileTabs.IndexOf(targetTab);

                    // 判断是插入到前面(Left)还是后面(Right)
                    Point p = e.GetPosition(targetGrid);
                    bool insertAfter = p.X >= targetGrid.ActualWidth / 2;

                    int newUIIndex = targetUIIndex;

                    if (insertAfter)newUIIndex++;
                    if (oldUIIndex < newUIIndex) newUIIndex--;
 
                    // 简单校验
                    if (newUIIndex < 0) newUIIndex = 0;
                    if (newUIIndex >= FileTabs.Count) newUIIndex = FileTabs.Count - 1;
                    if (oldUIIndex != newUIIndex)
                    {
                        FileTabs.Move(oldUIIndex, newUIIndex);

                        // 同步更新 _imageFiles 列表
                        string sourcePath = sourceTab.FilePath;
                        if (!string.IsNullOrEmpty(sourcePath) && _imageFiles.Contains(sourcePath))
                        {
                            _imageFiles.Remove(sourcePath);
                            int newTgtIdx = -1;
                            if (newUIIndex > 0)
                            {
                                var prevTab = FileTabs[newUIIndex - 1];
                                newTgtIdx = _imageFiles.IndexOf(prevTab.FilePath);
                                // 插在它后面
                                if (newTgtIdx >= 0) _imageFiles.Insert(newTgtIdx + 1, sourcePath);
                                else _imageFiles.Add(sourcePath); // Fallback
                            }
                            else
                            {
                                // 插在最前面
                                _imageFiles.Insert(0, sourcePath);
                            }
                        }

                        if (_currentTabItem != null)
                        {
                            _currentImageIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                        }
                        UpdateWindowTitle(); // 如果需要
                    }
                }
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }
        private void OnTabCopyClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                CopyTabToClipboard(tab);
            }
        }

        private void OnTabCutClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                CopyTabToClipboard(tab);
                CloseTab(tab);
            }
        }

        private async void OnTabPasteClick(object sender, RoutedEventArgs e)
        {
            // 获取插入位置：在右键点击的 Tab 后面
            int insertIndex = -1; // 默认最后
            int uiInsertIndex = FileTabs.Count;

            if (sender is MenuItem item && item.Tag is FileTabItem targetTab)
            {
                int targetUiIndex = FileTabs.IndexOf(targetTab);
                if (targetUiIndex >= 0)
                {
                    uiInsertIndex = targetUiIndex + 1;
                    // 尝试在 _imageFiles 里找到对应位置
                    if (!string.IsNullOrEmpty(targetTab.FilePath))
                    {
                        int fileIndex = _imageFiles.IndexOf(targetTab.FilePath);
                        if (fileIndex >= 0) insertIndex = fileIndex + 1;
                    }
                }
            }

            if (insertIndex == -1) insertIndex = _imageFiles.Count;

            bool hasHandled = false;
            FileTabItem tabToSelect = null; // 用于记录最后需要选中的 Tab

            IDataObject data = Clipboard.GetDataObject();
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    int addedCount = 0;
                    foreach (string file in files)
                    {
                        if (!IsImageFile(file)) continue;

                        // 检查是否已存在
                        if (!_imageFiles.Contains(file))
                        {
                            // 1. 不存在：插入新 Tab
                            _imageFiles.Insert(insertIndex + addedCount, file);

                            var newTab = new FileTabItem(file) { IsLoading = true };
                            FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                            _ = newTab.LoadThumbnailAsync(100, 60);

                            tabToSelect = newTab; // 标记需要切换到新文件
                            addedCount++;
                        }
                        else
                        {
                            // 2. 已存在：查找对应的 Tab
                            var existingTab = FileTabs.FirstOrDefault(t => string.Equals(t.FilePath, file, StringComparison.OrdinalIgnoreCase));
                            if (existingTab != null)
                            {
                                tabToSelect = existingTab; // 标记需要切换到已存在的文件
                            }
                        }
                    }
                    if (addedCount > 0 || tabToSelect != null) hasHandled = true;
                }
            }
            else if (data.GetDataPresent(DataFormats.Bitmap))
            {
                try
                {
                    BitmapSource source = Clipboard.GetImage();

                    if (source != null)
                    {
                        var newTab = CreateNewUntitledTab();

                        string cacheFileName = $"{newTab.Id}.png";
                        string fullCachePath = System.IO.Path.Combine(_cacheDir, cacheFileName);

                        using (var fileStream = new FileStream(fullCachePath, FileMode.Create))
                        {
                            BitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(source));
                            encoder.Save(fileStream);
                        }

                        newTab.BackupPath = fullCachePath;
                        newTab.IsDirty = true;
                        UpdateTabThumbnailFromBitmap(newTab, source);
                        FileTabs.Insert(uiInsertIndex, newTab);

                        // 滚动条稍微向右移动一点，增加视觉反馈
                        MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 120);

                        tabToSelect = newTab; // 标记需要切换
                        hasHandled = true;
                    }
                }
                catch (Exception ex)
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_PasteFailed"), ex.Message));
                }
            }

            if (hasHandled)
            {
                // 刷新底部 Slider 数量
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();
                UpdateImageBarSliderState();
                if (tabToSelect != null)
                {
                    SwitchToTab(tabToSelect);
                }
            }
        }
        private void OnTabDeleteClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                CloseTab(tab);
            }
        }
        private void OnTabFileDeleteClick(object sender, RoutedEventArgs e)
        {
            // 这里的“删除”是物理删除文件
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                if (tab.IsNew && string.IsNullOrEmpty(tab.FilePath))
                {
                    CloseTab(tab); // 如果是没保存的新建画布，直接关掉
                    return;
                }
                if (!SettingsManager.Instance.Current.SkipResetConfirmation)
                {
                    var result = FluentMessageBox.Show(
                     string.Format(LocalizationManager.GetString("L_Msg_DeleteFile_Content"), tab.FileName),
                     LocalizationManager.GetString("L_Msg_DeleteFile_Title"),
                     MessageBoxButton.YesNo
                   );
                    if (result != MessageBoxResult.Yes) return;
                }
                string path = tab.FilePath;

                CloseTab(tab);
                try
                {
                    if (File.Exists(path))
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                }
                catch (Exception ex)  { ShowToast(string.Format(LocalizationManager.GetString("L_Toast_DeleteFailed"), ex.Message));}

            }
        }
        private void CopyTabToClipboard(FileTabItem tab)
        {
            var dataObject = new DataObject();
            BitmapSource heavyBitmap = null;

            try
            {
                if (tab.IsDirty || tab.IsNew)
                {
                    // 如果是脏文件，从画布或缓存获取最新状态
                    heavyBitmap = GetCurrentCanvasSnapshotSafe(); 
                    if (heavyBitmap == null && !string.IsNullOrEmpty(tab.BackupPath))
                    {
                        heavyBitmap = LoadBitmapFromFile(tab.BackupPath);
                    }
                }
                else
                {
                    if (!IsVirtualPath(tab.FilePath))
                    {
                        heavyBitmap = LoadBitmapFromFile(tab.FilePath);
                    }
                }

                if (heavyBitmap != null)
                {
                    dataObject.SetImage(heavyBitmap);
                }
                string pathForClipboard = null;

                if (!tab.IsDirty && !tab.IsNew && !string.IsNullOrEmpty(tab.FilePath) && File.Exists(tab.FilePath))
                {
                    // CASE A: 原文件存在且未修改 -> 直接使用原路径
                    pathForClipboard = tab.FilePath;
                }
                else if (heavyBitmap != null)
                {
                    string clipDir = System.IO.Path.Combine(_cacheDir, "ClipboardTemp");
                    if (!Directory.Exists(clipDir)) Directory.CreateDirectory(clipDir);

                    // 确定文件名
                    string fileName = tab.FileName;
                    // 确保有扩展名
                    if (string.IsNullOrEmpty(System.IO.Path.GetExtension(fileName)))
                    {
                        fileName += ".png";
                    }

                    string tempPath = System.IO.Path.Combine(clipDir, fileName);

                    // 保存临时文件
                    using (var fs = new FileStream(tempPath, FileMode.Create))
                    {
                        BitmapEncoder encoder;
                        string ext = System.IO.Path.GetExtension(fileName).ToLower();

                        if (ext == ".jpg" || ext == ".jpeg")
                        {
                            encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                        }
                        else
                        {
                            encoder = new PngBitmapEncoder();
                        }

                        encoder.Frames.Add(BitmapFrame.Create(heavyBitmap));
                        encoder.Save(fs);
                    }
                    pathForClipboard = tempPath;
                }

                // 将路径放入剪贴板
                if (pathForClipboard != null)
                {
                    var fileList = new System.Collections.Specialized.StringCollection();
                    fileList.Add(pathForClipboard);
                    dataObject.SetFileDropList(fileList);
                }

                // --- 3. 提交到剪贴板 ---
                Clipboard.SetDataObject(dataObject, true);
            }
            catch (Exception)
            {
                ShowToast("L_Toast_CopyFailed");
            }
            finally
            {
                if (heavyBitmap != null && (tab.IsDirty || tab.IsNew))
                {
                    heavyBitmap = null;
                    GC.Collect();
                }
            }
        }
        // #endregion
    }
}