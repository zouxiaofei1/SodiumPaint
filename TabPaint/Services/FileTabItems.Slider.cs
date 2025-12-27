
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
//包括PreviewSlider滑块和滚轮滚动imagebar的相关代码
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class FileTabItem : INotifyPropertyChanged
        {  // 当滑块拖动时触发
           


        }
        private bool _isSyncingSlider = false; // 防止死循环
        private bool _isUpdatingUiFromScroll = false;
        private void OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            
            if (_isProgrammaticScroll) return;   
            if (!_isInitialLayoutComplete) return;
            if (e == null) return;

            double itemWidth = 124; // 请确保这与 XAML 中 Tab 的实际宽度(包含Margin)一致
         
            // 1. 计算当前视图内可见的 Tab 范围（局部索引）
            int firstLocalIndex = (int)(FileTabsScroller.HorizontalOffset / itemWidth);
            int visibleCount = (int)(FileTabsScroller.ViewportWidth / itemWidth) + 2;

            // 2. 【核心修复】将局部索引映射为全局索引，同步 Slider
            if (!_isSyncingSlider && FileTabs.Count > 0 && firstLocalIndex >= 0 && firstLocalIndex < FileTabs.Count)
            {
                var firstVisibleTab = FileTabs[firstLocalIndex];
                int globalIndex = _imageFiles.IndexOf(firstVisibleTab.FilePath);

                if (globalIndex >= 0 && PreviewSlider.Value != globalIndex)
                {
                    _isUpdatingUiFromScroll = true;
                    PreviewSlider.Value = globalIndex;
                    _isUpdatingUiFromScroll = false;
                }
            }
            

            bool needLoadThumbnail = false;

            // 3. 【向后加载】逻辑优化 (Load Next)
            // 只要看到最后 5 个以内，且还有更多文件，就加载
            // 阈值调大到 5，防止滚动过快时出现空白
            if (FileTabs.Count > 0 &&
        firstLocalIndex + visibleCount >= FileTabs.Count - 5 &&
        FileTabs.Count < _imageFiles.Count)
            {
                var lastTab = FileTabs.Last();
                int lastFileIndex = _imageFiles.IndexOf(lastTab.FilePath);

                if (lastFileIndex >= 0 && lastFileIndex < _imageFiles.Count - 1)
                {
                    int takeCount = PageSize;
                    var nextItems = _imageFiles.Skip(lastFileIndex + 1).Take(takeCount);

                    foreach (var path in nextItems)
                    {
                        // --- [修改部分 START] ---
                        // 增加黑名单检查：如果已经在Tab里，或者被手动关闭过，就跳过
                        if (!FileTabs.Any(t => t.FilePath == path) && !_explicitlyClosedFiles.Contains(path))
                        {
                            FileTabs.Add(new FileTabItem(path));
                        }
                        // --- [修改部分 END] ---
                    }
                    needLoadThumbnail = true;
                }
            }

            // 4. 【向前加载】逻辑优化 (Load Previous)
            if (firstLocalIndex < 3 && FileTabs.Count > 0)
            {
                var firstTab = FileTabs[0];
                int firstFileIndex = _imageFiles.IndexOf(firstTab.FilePath);

                if (firstFileIndex > 0)
                {
                    // ... [保留计算 takeCount 的代码] ...
                    int takeCount = PageSize;
                    int start = Math.Max(0, firstFileIndex - takeCount);
                    int actualTake = firstFileIndex - start;

                    if (actualTake > 0)
                    {
                        var prevPaths = _imageFiles.Skip(start).Take(actualTake).Reverse();

                        int insertCount = 0;
                        foreach (var path in prevPaths)
                        {
                            // --- [修改部分 START] ---
                            if (!FileTabs.Any(t => t.FilePath == path) && !_explicitlyClosedFiles.Contains(path))
                            {
                                FileTabs.Insert(0, new FileTabItem(path));
                                insertCount++;
                            }
                            // --- [修改部分 END] ---
                        }

                        if (insertCount > 0)
                        {
                            FileTabsScroller.ScrollToHorizontalOffset(FileTabsScroller.HorizontalOffset + insertCount * itemWidth);
                            needLoadThumbnail = true;
                        }
                    }
                }
            }

            // 5. 触发缩略图懒加载
            if (needLoadThumbnail || Math.Abs(e.HorizontalChange) > 1 || Math.Abs(e.ExtentWidthChange) > 1)
            {
                int checkStart = Math.Max(0, firstLocalIndex - 2);
                int checkEnd = Math.Min(firstLocalIndex + visibleCount + 2, FileTabs.Count);

                for (int i = checkStart; i < checkEnd; i++)
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
        private void OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                // 增加滚动速度系数 (例如 1.5倍)，让滚轮更跟手
                double scrollSpeed = 1.5;
                double offset = scrollViewer.HorizontalOffset - (e.Delta * scrollSpeed);

                // 边界检查
                if (offset < 0) offset = 0;
                if (offset > scrollViewer.ScrollableWidth) offset = scrollViewer.ScrollableWidth;

                scrollViewer.ScrollToHorizontalOffset(offset);
                e.Handled = true;
            }
        }

        private async void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 如果是由滚动条触发的 Slider 变化，什么都不做，防止循环
            if (_isUpdatingUiFromScroll) return;

            // 如果正在初始化，跳过
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            // 防止过于频繁触发
            if (_isSyncingSlider) return;
            _isSyncingSlider = true;

            try
            {
                int index = (int)Math.Round(e.NewValue);
                // 边界保护
                if (index < 0) index = 0;
                if (index >= _imageFiles.Count) index = _imageFiles.Count - 1;

                // 这里是【拖动滑块】的逻辑：跳转到该位置
                // 1. 重新生成以该索引为中心的 Tab 列表
                await RefreshTabPageAsync(index, true);

                // 注意：RefreshTabPageAsync 里会重置 FileTabs，
                // 这会自动触发 OnFileTabsScrollChanged，但由于我们处于 _isSyncingSlider = true 状态，
                // 且 ScrollChanged 里有校验，应该没问题。
            }
            finally
            {
                _isSyncingSlider = false;
            }
        }
        private void SetPreviewSlider()
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;
            PreviewSlider.Minimum = 0;
            PreviewSlider.Maximum = _imageFiles.Count - 1;
            PreviewSlider.Value = _currentImageIndex;
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
    }
}