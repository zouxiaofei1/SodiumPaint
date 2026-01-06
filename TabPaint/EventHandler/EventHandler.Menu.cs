
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TabPaint事件处理cs
//menu及位于那一行的所有东西
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnInvertColorsClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;

            // 1. 记录 Undo (整图操作推荐 PushFullImageUndo)
            _undo.PushFullImageUndo();

            var bmp = _surface.Bitmap;
            bmp.Lock();
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)bmp.BackBuffer;
                    int stride = bmp.BackBufferStride;
                    int height = bmp.PixelHeight;
                    int width = bmp.PixelWidth;

                    // 并行处理反色
                    Parallel.For(0, height, y =>
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            // 反色公式：255 - 原值
                            // 注意：通常不反转 Alpha 通道 (pixel[3])
                            row[x * 4] = (byte)(255 - row[x * 4]);     // B
                            row[x * 4 + 1] = (byte)(255 - row[x * 4 + 1]); // G
                            row[x * 4 + 2] = (byte)(255 - row[x * 4 + 2]); // R
                        }
                    });
                }
                bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            }
            finally
            {
                bmp.Unlock();
            }

            // 更新状态
            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            ShowToast("已应用反色");
        }

        private void OnAutoLevelsClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;

            // 1. 记录 Undo
            _undo.PushFullImageUndo();

            var bmp = _surface.Bitmap;
            bmp.Lock();
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)bmp.BackBuffer;
                    int stride = bmp.BackBufferStride;
                    int height = bmp.PixelHeight;
                    int width = bmp.PixelWidth;
                    long totalPixels = width * height;

                    // --- 第一步：统计直方图 ---
                    // 为了简化，这里计算 RGB 的综合亮度直方图，或者分别计算 R,G,B 通道
                    // 自动色阶通常是针对 R, G, B 三个通道分别拉伸，这样可以修正色偏

                    int[] histR = new int[256];
                    int[] histG = new int[256];
                    int[] histB = new int[256];

                    // 采样统计 (为了性能，如果是超大图可以考虑跳跃采样，这里做全采样)
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            histB[row[x * 4]]++;
                            histG[row[x * 4 + 1]]++;
                            histR[row[x * 4 + 2]]++;
                        }
                    }

                    // --- 第二步：寻找切入点 (Min/Max) ---
                    // 通常忽略两端 0.5% 的极值像素，避免噪点影响
                    float clipPercent = 0.005f;
                    int threshold = (int)(totalPixels * clipPercent);

                    void GetMinMax(int[] hist, out byte min, out byte max)
                    {
                        min = 0; max = 255;
                        int count = 0;
                        // 找 min
                        for (int i = 0; i < 256; i++)
                        {
                            count += hist[i];
                            if (count > threshold) { min = (byte)i; break; }
                        }
                        // 找 max
                        count = 0;
                        for (int i = 255; i >= 0; i--)
                        {
                            count += hist[i];
                            if (count > threshold) { max = (byte)i; break; }
                        }
                    }

                    GetMinMax(histB, out byte minB, out byte maxB);
                    GetMinMax(histG, out byte minG, out byte maxG);
                    GetMinMax(histR, out byte minR, out byte maxR);

                    // --- 第三步：生成查找表 (LUT) 优化性能 ---
                    byte[] lutR = BuildLevelLut(minR, maxR);
                    byte[] lutG = BuildLevelLut(minG, maxG);
                    byte[] lutB = BuildLevelLut(minB, maxB);

                    // --- 第四步：应用映射 ---
                    Parallel.For(0, height, y =>
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            row[x * 4] = lutB[row[x * 4]];     // B
                            row[x * 4 + 1] = lutG[row[x * 4 + 1]]; // G
                            row[x * 4 + 2] = lutR[row[x * 4 + 2]]; // R
                        }
                    });
                }
                bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            }
            finally
            {
                bmp.Unlock();
            }

            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            ShowToast("已自动调整色阶");
        }

        // 辅助方法：构建色阶映射表
        private byte[] BuildLevelLut(byte min, byte max)
        {
            byte[] lut = new byte[256];
            if (max <= min) // 避免除以零或异常情况
            {
                for (int i = 0; i < 256; i++) lut[i] = (byte)i;
                return lut;
            }

            float scale = 255.0f / (max - min);
            for (int i = 0; i < 256; i++)
            {
                if (i <= min) lut[i] = 0;
                else if (i >= max) lut[i] = 255;
                else
                {
                    lut[i] = (byte)((i - min) * scale);
                }
            }
            return lut;
        }
        private void OnRecentFileClick(object sender, string filePath)
        {
            if (File.Exists(filePath))
            {
                // 调用你现有的打开逻辑
                // 假设是 OpenImageAndTabs 或类似的入口
                OpenImageAndTabs(filePath, true);
            }
            else
            {
                ShowToast($"文件未找到：\n{filePath}");
                // 可选：如果文件不存在，从列表中移除？
            }
        }

        private void OnClearRecentFilesClick(object sender, EventArgs e)
        {
            SettingsManager.Instance.ClearRecentFiles();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            // 打开设置窗口
            
            var settingsWindow = new SettingsWindow();
            TabPaint.SettingsManager.Instance.Current.PropertyChanged += (s, e) =>
            {
                // 当这两个阈值属性发生变化时，强制刷新渲染模式
                if (e.PropertyName == "ViewInterpolationThreshold" ||
                    e.PropertyName == "PaintInterpolationThreshold")
                {
                    // 确保在 UI 线程执行
                    this.Dispatcher.Invoke(() =>
                    {
                        RefreshBitmapScalingMode();
                    });
                }
            };

            settingsWindow.ProgramVersion = this.ProgramVersion;
            settingsWindow.Owner = this; // 设置主窗口为父窗口，实现模态
            settingsWindow.ShowDialog();
        }



        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            // 如果是空路径 OR 是虚拟路径，都视为"从未保存过"，走另存为
            if (string.IsNullOrEmpty(_currentFilePath) || IsVirtualPath(_currentFilePath))
            {
                OnSaveAsClick(sender, e);
            }
            else
            {
                SaveBitmap(_currentFilePath);
            }
        }


        private void OnSaveAsClick(object sender, RoutedEventArgs e)
        {
            // 1. 准备默认文件名
            // 如果是新建的，DisplayName 会返回 "未命名 1"，如果是已有的，会返回原文件名
            string defaultName = _currentTabItem?.DisplayName ?? "image";
            if (!defaultName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                defaultName += ".png";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = PicFilterString,
                FileName = defaultName
            };

            // 2. 需求2：默认位置为打开的文件夹 (即 _currentFilePath 所在目录)
            string initialDir = "";
            if (!string.IsNullOrEmpty(_currentFilePath))
                initialDir = System.IO.Path.GetDirectoryName(_currentFilePath);
            else if (_imageFiles != null && _imageFiles.Count > 0)
                initialDir = System.IO.Path.GetDirectoryName(_imageFiles[0]);

            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                dlg.InitialDirectory = initialDir;

            if (dlg.ShowDialog() == true)
            {
                string newPath = dlg.FileName;
                SaveBitmap(newPath); // 实际保存文件

                // 3. 更新状态
                _currentFilePath = newPath;
                _currentFileName = System.IO.Path.GetFileName(newPath);

                if (_currentTabItem != null)
                {
                    // 这里会触发 FilePath 的 setter，进而自动触发 DisplayName 的通知
                    _currentTabItem.FilePath = newPath;

                    if (_currentTabItem.IsNew)
                    {
                        _currentTabItem.IsNew = false; // 也会触发 DisplayName 更新通知
                        if (!_imageFiles.Contains(newPath)) _imageFiles.Add(newPath);
                    }
                    else if (!_imageFiles.Contains(newPath))
                    {
                        _imageFiles.Add(newPath);
                    }

                    _currentImageIndex = _imageFiles.IndexOf(newPath);
                   // s(_currentImageIndex);
                }

                _isFileSaved = true;
                UpdateWindowTitle();
            }
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            // 确保 SelectTool 是当前工具
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CopySelection(_ctx);
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CutSelection(_ctx, true);
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.PasteSelection(_ctx, false);

        }

        private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();
        private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();
        private void OnBrightnessContrastExposureClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;// 1. (为Undo做准备) 保存当前图像的完整快照
            var fullRect = new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight);
            _undo.PushFullImageUndo(); // 2. 创建对话框，并传入主位图的一个克隆体用于预览
            var dialog = new AdjustBCEWindow(_bitmap, BackgroundImage);   // 3. 显示对话框并根据结果操作
            if (dialog.ShowDialog() == true)
            {// 4. 从对话框获取处理后的位图
                WriteableBitmap adjustedBitmap = dialog.FinalBitmap;   // 5. 将处理后的像素数据写回到主位图 (_bitmap) 中
                int stride = adjustedBitmap.BackBufferStride;
                int byteCount = adjustedBitmap.PixelHeight * stride;
                byte[] pixelData = new byte[byteCount];
                adjustedBitmap.CopyPixels(pixelData, stride, 0);
                _bitmap.WritePixels(fullRect, pixelData, stride, 0); 
                CheckDirtyState();
                SetUndoRedoButtonState();
            }
            else
            {  // 用户点击了 "取消" 或关闭了窗口
                _undo.Undo(); // 弹出刚刚压入的快照
                _undo.ClearRedo(); // 清空因此产生的Redo项
                SetUndoRedoButtonState();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if(!_programClosed)OnClosing();
            //Close();
        }



        private void CropMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool is SelectTool selectTool)
            {
                selectTool.CropToSelection(_ctx);
                SetCropButtonState();
                NotifyCanvasChanged();
                _canvasResizer.UpdateUI();
            }
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            MaximizeWindowHandler();
        }


        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ThicknessSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Visible;
            UpdateThicknessPreviewPosition(); // 初始定位

            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(ThicknessSlider.Value);
        }

        private void ThicknessSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Collapsed;

            ThicknessTip.Visibility = Visibility.Collapsed;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialLayoutComplete) return;

            // Check: 防止空引用
            if (ThicknessTip == null || ThicknessTipText == null || ThicknessSlider == null)
                return;

            double t = e.NewValue;
            double realSize = 1.0 + (400.0 - 1.0) * (t * t);
            PenThickness = realSize;
            UpdateThicknessPreviewPosition();

            ThicknessTipText.Text = $"{(int)realSize} 像素";
            SetThicknessSlider_Pos(e.NewValue);
            ThicknessTip.Visibility = Visibility.Visible;
        }
        private async void OnOpenWorkspaceClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择图片以建立新工作区",
                Filter = PicFilterString,
                Multiselect = false     
            };

            if (dlg.ShowDialog() == true)
            {
                string file = dlg.FileName;
                SettingsManager.Instance.AddRecentFile(file);

                await SwitchWorkspaceToNewFile(file);
            }
        }


        private void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = PicFilterString,
                 Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                string[] files= dlg.FileNames;
                OpenFilesAsNewTabs(files);
                foreach (var file in files)
                    SettingsManager.Instance.AddRecentFile(file);
                UpdateImageBarSliderState();
            }
        }
        private void OnColorTempTintSaturationClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;
            _undo.PushFullImageUndo();// 1. (为Undo做准备) 保存当前图像的完整快照
            var dialog = new AdjustTTSWindow(_bitmap); // 2. 创建对话框，并传入主位图的一个克隆体用于预览
                                                       // 注意：这里我们传入的是 _bitmap 本身，因为 AdjustTTSWindow 内部会自己克隆一个原始副本


            if (dialog.ShowDialog() == true) // 更新撤销/重做按钮的状态
            {
                SetUndoRedoButtonState();
                CheckDirtyState();
            }
            else// 用户点击了 "取消"
            {
                _undo.Undo();
                _undo.ClearRedo();
                SetUndoRedoButtonState();
            }
        }
        private void OnConvertToBlackAndWhiteClick(object sender, RoutedEventArgs e)
        {

            if (_bitmap == null) return;  // 1. 检查图像是否存在
            _undo.PushFullImageUndo();
            ConvertToBlackAndWhite(_bitmap);
            CheckDirtyState();
            SetUndoRedoButtonState();
            ShowToast("已应用黑白");
        }
        private void OnResizeCanvasClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;

            // 1. 创建并配置对话框
            var dialog = new ResizeCanvasDialog(
                _surface.Bitmap.PixelWidth,
                _surface.Bitmap.PixelHeight
            );
            dialog.Owner = this; // 设置所有者

            if (dialog.ShowDialog() == true)
            {
                int newWidth = dialog.ImageWidth;
                int newHeight = dialog.ImageHeight;

                // 2. 根据模式分流逻辑
                if (dialog.IsCanvasResizeMode)
                {
                    ResizeCanvasDimensions(newWidth, newHeight);
                }
                else
                {
                    // 模式 B：缩放图像 (拉伸像素，你原有的 ResizeCanvas 方法)
                    ResizeCanvas(newWidth, newHeight);
                }

                CheckDirtyState();

                // 确保 UI 组件（如 ResizeOverlay）更新
                if (_canvasResizer != null) _canvasResizer.UpdateUI();
            }
        }


        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            CreateNewTab(TabInsertPosition.AfterCurrent,true);
        }
    }
}