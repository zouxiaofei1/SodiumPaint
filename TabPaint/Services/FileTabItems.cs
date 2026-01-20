
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

//
//ImageBar图片选择框相关代码
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {


        private void LoadTabPageAsync(int centerIndex)
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            int start = Math.Max(0, centerIndex - PageSize);
            int end = Math.Min(_imageFiles.Count - 1, centerIndex + PageSize);
            var viewportPaths = new HashSet<string>();

            string centerPath = (centerIndex >= 0 && centerIndex < _imageFiles.Count) ? _imageFiles[centerIndex] : null;

            for (int i = start; i <= end; i++) viewportPaths.Add(_imageFiles[i]);
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
            for (int i = start; i <= end; i++)
            {
                string path = _imageFiles[i];

                // 过滤黑名单
                if (_explicitlyClosedFiles.Contains(path) && path != centerPath) continue;
                if (path == centerPath && _explicitlyClosedFiles.Contains(path)) _explicitlyClosedFiles.Remove(path);

                var existingTab = FileTabs.FirstOrDefault(t => t.FilePath == path);

                if (existingTab == null)
                {

                    var newTab = CreateTabFromPath(path);

                    // 插入排序逻辑
                    int insertIndex = 0;
                    bool inserted = false;
                    for (int j = 0; j < FileTabs.Count; j++)
                    {
                        var t = FileTabs[j];
                        int tIndex = _imageFiles.IndexOf(t.FilePath);

                        // 如果 tIndex == -1 (说明这个 Tab 可能刚被删了? 或者异常)，把它往后放
                        if (tIndex == -1 || tIndex > i)
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
            double viewportWidth = MainImageBar.Scroller.ViewportWidth;

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
        public void ScrollToTabCenter(FileTabItem targetTab)
        {
            if (targetTab == null) return;

            // 使用 ContextIdle 优先级，确保在 UI 布局更新（比如 Tab 变大或变色后）再执行滚动
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (MainImageBar.TabList == null) return;
                    var container = MainImageBar.TabList.ItemContainerGenerator.ContainerFromItem(targetTab) as FrameworkElement;

                    if (container != null)
                    {
                        var transform = container.TransformToAncestor(MainImageBar.TabList);
                        var rootPoint = transform.Transform(new Point(0, 0));

                        double itemLeft = rootPoint.X;
                        double itemWidth = container.ActualWidth;
                        double viewportWidth = MainImageBar.Scroller.ViewportWidth;

                        double centerOffset = (itemLeft + itemWidth / 2) - (viewportWidth / 2);

                        if (centerOffset < 0) centerOffset = 0;
                        if (centerOffset > MainImageBar.Scroller.ScrollableWidth) centerOffset = MainImageBar.Scroller.ScrollableWidth;

                        MainImageBar.Scroller.ScrollToHorizontalOffset(centerOffset);

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

        private async void CloseTab(FileTabItem item,bool slient=false)
        {
            // 1. 脏检查
            if (item.IsDirty&&!slient&& !SettingsManager.Instance.Current.SkipResetConfirmation)
            {
                var result = FluentMessageBox.Show(
                    string.Format(LocalizationManager.GetString("L_Msg_UnsavedClose_Content"), item.DisplayName),
                    LocalizationManager.GetString("L_Msg_UnsavedClose_Title"),
                    MessageBoxButton.YesNoCancel);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes) SaveSingleTab(item);
            }
            string pathToRemove = item.FilePath;
            int removedUiIndex = FileTabs.IndexOf(item);
            bool wasSelected = item.IsSelected;

            // 2. 从集合中移除
            FileTabs.Remove(item);
            if (!string.IsNullOrEmpty(pathToRemove) && _imageFiles.Contains(pathToRemove))
            {
                _imageFiles.Remove(pathToRemove);
            }
            if (!string.IsNullOrEmpty(item.BackupPath) && File.Exists(item.BackupPath))
            {
                try { File.Delete(item.BackupPath); } catch { }
            }
            if (FileTabs.Count == 0)
            {
                _imageFiles.Clear();
                ResetToNewCanvas(); // ResetToNewCanvas 内部通常会调用 CreateNewTab 确保至少有一个页
                UpdateImageBarSliderState();
                return;
            }
            if (wasSelected)
            {
                int newIndex = Math.Max(0, Math.Min(removedUiIndex - 1, FileTabs.Count - 1));
                var nextTab = FileTabs[newIndex];
                SwitchToTab(nextTab);
            }
            else
            {
                if (_currentTabItem != null)
                {
                    _currentImageIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                }
            }
            UpdateImageBarSliderState();
            UpdateWindowTitle();
            UpdateImageBarVisibilityState();
        }


        private void InitializeScrollPosition()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (MainImageBar == null || MainImageBar.AddButton == null) return;

                // 此时布局已自然完成，ActualWidth 可以直接读取
                double btnWidth = MainImageBar.AddButton.ActualWidth;

                // 极限优化：如果按钮大小是固定的（比如 40），直接写死 double btnWidth = 40; 能省去所有计算开销
                if (btnWidth == 0) btnWidth = 46; // 给个保底值

                double hiddenWidth = btnWidth
                                   + MainImageBar.AddButton.Margin.Left
                                   + MainImageBar.AddButton.Margin.Right;

                if (MainImageBar.Scroller.HorizontalOffset == 0)
                {
                    MainImageBar.Scroller.ScrollToHorizontalOffset(hiddenWidth);
                }
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void MarkAsSaved()
        {//仅mark不负责保存!!
            if (_currentTabItem == null) return;
            _savedUndoPoint = _undo.UndoCount;

            _currentTabItem.IsDirty = false;

           // SaveSession();
        }
    }
}