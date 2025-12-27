
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


        private void LoadTabPageAsync(int centerIndex)
        {
            // a.s("LoadTabPageAsync", centerIndex); // 调试日志
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            // 1. 确定当前“文件夹视图”的范围
            int start = Math.Max(0, centerIndex - PageSize);
            int end = Math.Min(_imageFiles.Count - 1, centerIndex + PageSize);
            var viewportPaths = new HashSet<string>();

            // 获取中心图片路径（用户明确选中的）
            string centerPath = (centerIndex >= 0 && centerIndex < _imageFiles.Count) ? _imageFiles[centerIndex] : null;

            for (int i = start; i <= end; i++) viewportPaths.Add(_imageFiles[i]);

            // 2. 清理阶段：只移除那些 "既不在视野内，又不是脏数据，也不是新文件" 的项
            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                bool isViewport = viewportPaths.Contains(tab.FilePath);
                bool isKeepAlive = tab.IsDirty || tab.IsNew;

                if (!isViewport && !isKeepAlive)
                {
                    FileTabs.RemoveAt(i);
                }
            }

            // 3. 添加阶段
            for (int i = start; i <= end; i++)
            {
                string path = _imageFiles[i];

                // --- [新增逻辑] ---
                // 如果该文件在黑名单里，且不是用户当前选中的那张图(centerPath)，则跳过不加载
                if (_explicitlyClosedFiles.Contains(path) && path != centerPath)
                {
                    continue;
                }
                // 如果用户强行选中了这张图(centerPath)，说明他反悔了，从黑名单移除
                if (path == centerPath && _explicitlyClosedFiles.Contains(path))
                {
                    _explicitlyClosedFiles.Remove(path);
                }
                // ------------------

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
                }
                finally
                {
                    _isProgrammaticScroll = false; // 🔓 解锁
                }
            });
        }

        private void ScrollToTabCenter(FileTabItem targetTab)
        {
            if (targetTab == null) return;

            // 使用 ContextIdle 优先级，确保在 UI 布局更新（比如 Tab 变大或变色后）再执行滚动
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (FileTabList == null) return;
                    var container = FileTabList.ItemContainerGenerator.ContainerFromItem(targetTab) as FrameworkElement;

                    if (container != null)
                    {
                        var transform = container.TransformToAncestor(FileTabList);
                        var rootPoint = transform.Transform(new Point(0, 0));

                        double itemLeft = rootPoint.X;
                        double itemWidth = container.ActualWidth;
                        double viewportWidth = FileTabsScroller.ViewportWidth;

                        double centerOffset = (itemLeft + itemWidth / 2) - (viewportWidth / 2);

                        if (centerOffset < 0) centerOffset = 0;
                        if (centerOffset > FileTabsScroller.ScrollableWidth) centerOffset = FileTabsScroller.ScrollableWidth;

                        FileTabsScroller.ScrollToHorizontalOffset(centerOffset);

                    }
                }
                catch (Exception ex)
                {
                }
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }


        private void ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)// 阻止边界反馈
        {
            e.Handled = true;
        }

        private async void CloseTab(FileTabItem item)
        {
            // 1. 脏检查（保持不变）
            if (item.IsDirty)
            {
                var result = System.Windows.MessageBox.Show(
                    $"图片 {item.DisplayName} 尚未保存，是否保存？",
                    "保存提示",
                    MessageBoxButton.YesNoCancel);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes) SaveSingleTab(item);
            }

            // 记录下要删除的路径和当前的选中状态
            string pathToRemove = item.FilePath;
            int removedUiIndex = FileTabs.IndexOf(item);
            bool wasSelected = item.IsSelected;

            // 2. 从 UI 列表移除 Tab
            FileTabs.Remove(item);

            // 3. 【核心修改】从后台数据源列表中彻底移除该文件
            // 这样下次滚动或者计算 Index 时，这张图就不存在了
            if (!string.IsNullOrEmpty(pathToRemove) && _imageFiles.Contains(pathToRemove))
            {
                _imageFiles.Remove(pathToRemove);
            }

            // 4. 同步底部的 Slider 范围（因为总数变少了）
            if (PreviewSlider != null)
            {
                // 重新设置最大值，防止 Slider 滑块位置越界
                PreviewSlider.Maximum = Math.Max(0, _imageFiles.Count - 1);
            }

            // 清理临时文件（保持不变）
            if (!string.IsNullOrEmpty(item.BackupPath) && File.Exists(item.BackupPath))
            {
                try { File.Delete(item.BackupPath); } catch { }
            }
            if (FileTabs.Count == 0)
            {
                _imageFiles.Clear();
                ResetToNewCanvas();
                return;
            }

            if (wasSelected)
            {
                int newIndex = removedUiIndex - 1;
                if (newIndex < 0) newIndex = 0;
                // 防止越界（虽然前面判空了）
                if (newIndex >= FileTabs.Count) newIndex = FileTabs.Count - 1;

                var newTab = FileTabs[newIndex];
                foreach (var tab in FileTabs) tab.IsSelected = false;
                newTab.IsSelected = true;
                _currentTabItem = newTab;

                if (!string.IsNullOrEmpty(newTab.FilePath))
                {
                    _currentImageIndex = _imageFiles.IndexOf(newTab.FilePath);
                }
                else
                {
                    _currentImageIndex = -1;
                }

                // 同步 Slider 的当前值
                if (PreviewSlider != null && _currentImageIndex >= 0)
                {
                    _isUpdatingUiFromScroll = true; // 上锁
                    PreviewSlider.Value = _currentImageIndex;
                    _isUpdatingUiFromScroll = false;
                }

                // 加载新 Tab 内容
                if (newTab.IsNew)
                {
                    if (!string.IsNullOrEmpty(newTab.BackupPath) && File.Exists(newTab.BackupPath))
                        await OpenImageAndTabs(newTab.BackupPath);
                    else
                    {
                        Clean_bitmap(1200, 900);
                        _currentFilePath = string.Empty;
                        _currentFileName = "未命名";
                    }
                }
                else
                {
                    await OpenImageAndTabs(newTab.FilePath);
                }
                ResetDirtyTracker();
            }
            else
            {
                if (_currentTabItem != null && !string.IsNullOrEmpty(_currentTabItem.FilePath))
                {
                    _currentImageIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);

                    // 同步 Slider
                    if (PreviewSlider != null && _currentImageIndex >= 0)
                    {
                        _isUpdatingUiFromScroll = true;
                        PreviewSlider.Value = _currentImageIndex;
                        _isUpdatingUiFromScroll = false;
                    }
                }
            }

            // 7. 最后统一刷新标题栏 (显示如 1/4)
            UpdateWindowTitle();
        }




        private void InitializeScrollPosition()
        {
            // 强制刷新一次布局，确保 LeftAddBtn.ActualWidth 能取到值
            FileTabsScroller.UpdateLayout();
            double hiddenWidth = LeftAddBtn.ActualWidth + LeftAddBtn.Margin.Left + LeftAddBtn.Margin.Right;
            if (FileTabsScroller.HorizontalOffset == 0)
            {
                FileTabsScroller.ScrollToHorizontalOffset(hiddenWidth);
            }
        }
       
        private void MarkAsSaved()
        {
            if (_currentTabItem == null) return;
            _savedUndoPoint = _undo.UndoCount;

            _currentTabItem.IsDirty = false;

            SaveSession();
        }
    }
}