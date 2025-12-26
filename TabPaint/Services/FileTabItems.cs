
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
            // 1. 脏检查（保持原有逻辑）
            if (item.IsDirty)
            {
                var result = System.Windows.MessageBox.Show(
                    $"图片 {item.DisplayName} 尚未保存，是否保存？",
                    "保存提示",
                    MessageBoxButton.YesNoCancel);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes)
                {
                    SaveSingleTab(item); // 保存逻辑
                }
                // 如果选 No，直接继续往下走
            }
            int removedIndex = FileTabs.IndexOf(item);
            bool wasSelected = item.IsSelected; // 或者 item == _currentTabItem

            // 3. 移除 Tab
            FileTabs.Remove(item);

            // 清理该 Tab 的临时缓存文件（如果是 dirty 的或者 new 的，且用户选择关闭/不保存）
            if (!string.IsNullOrEmpty(item.BackupPath) && File.Exists(item.BackupPath))
            {
                try { File.Delete(item.BackupPath); } catch { }
            }

            // 4. 情况 A：列表空了 -> 生成新的空图片
            if (FileTabs.Count == 0)
            {
                ResetToNewCanvas();
                return;
            }
            if (wasSelected)
            {
                int newIndex = removedIndex - 1;
                if (newIndex < 0) newIndex = 0; // 边界修正

                // 获取新 Tab 对象
                var newTab = FileTabs[newIndex];

                // 更新 UI 选中态
                foreach (var tab in FileTabs) tab.IsSelected = false;
                newTab.IsSelected = true;
                _currentTabItem = newTab;

                // 加载新 Tab 的画布内容
                if (newTab.IsNew)
                {
                    if (!string.IsNullOrEmpty(newTab.BackupPath) && File.Exists(newTab.BackupPath))
                    {
                        await OpenImageAndTabs(newTab.BackupPath);
                    }
                    else
                    {
                        // 纯新页，清空画布
                        Clean_bitmap(1200, 900);
                        _currentFilePath = string.Empty;
                        _currentFileName = "未命名"; // 可以在这里加上 newTab.DisplayName
                        UpdateWindowTitle();
                    }
                }
                else
                {
                    await OpenImageAndTabs(newTab.FilePath);
                }
                ResetDirtyTracker();
            }
            // 情况 C：关闭的是后台 Tab -> 保持当前画面不变，无需操作
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