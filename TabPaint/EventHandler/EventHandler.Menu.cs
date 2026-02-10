//
//EventHandler.Menu.cs
//顶部菜单栏的详细逻辑实现，包括文件保存、另存为、帮助窗口弹出、效果滤镜执行以及批量处理功能。
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using TabPaint.Controls;
using TabPaint.UIHandlers;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnHelpClick(object sender, RoutedEventArgs e)
        {
            var helpPages = new List<HelpPage>();
            try
            {
                helpPages.Add(new HelpPage
                {
                    ImageUri = new Uri("pack://application:,,,/Resources/help-1.gif"),
                    DescriptionKey = "L_Help_Desc_1"
                });

                helpPages.Add(new HelpPage
                {
                    ImageUri = new Uri("pack://application:,,,/Resources/help-2.gif"),
                    DescriptionKey = "L_Help_Desc_2"
                });

                helpPages.Add(new HelpPage
                {
                    ImageUri = new Uri("https://media.giphy.com/media/v1.Y2lkPTc5MGI3NjExcDNoM3Z.../cat-typing.gif"),
                    DescriptionKey = "L_Help_Desc_3"
                });

                if (helpPages.Count > 0)
                {
                    var helpWin = new HelpWindow(helpPages);
                    helpWin.Owner = this;
                    helpWin.Show();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void OnAppTitleBarLogoMiddleClick(object sender, RoutedEventArgs e)
        {
            if (_currentTabItem != null)
            {
                CloseTab(_currentTabItem);
            }
        }

        private void OnMosaicClick(object sender, RoutedEventArgs e)
        {
            var dialog = new TabPaint.Windows.FilterStrengthWindow(LocalizationManager.GetString("L_Menu_Effect_Mosaic"), 10, 2, 100);
            dialog.Owner = this;
            dialog.ShowOwnerModal(this);

            if (dialog.IsConfirmed)
            {
                ApplyFilter(FilterType.Mosaic, dialog.ResultValue);
            }
        }

        private void OnGaussianBlurClick(object sender, RoutedEventArgs e)
        {
            var dialog = new TabPaint.Windows.FilterStrengthWindow(LocalizationManager.GetString("L_Menu_Effect_GaussianBlur"), 5, 1, 50);
            dialog.Owner = this;
            dialog.ShowOwnerModal(this);

            if (dialog.IsConfirmed)
            {
                ApplyFilter(FilterType.GaussianBlur, dialog.ResultValue);
            }
        }

        private void OnSepiaClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Sepia);
        private void OnOilPaintingClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.OilPainting);
        private void OnVignetteClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Vignette);
        private void OnGlowClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Glow);

        private enum FilterType { Sepia, OilPainting, Vignette, Glow, Sharpen, Brown, Mosaic, GaussianBlur }

        private void OnSharpenClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Sharpen);
        private void OnBrownClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Brown);

        private async void ApplyFilter(FilterType type, int strength = 0)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            _undo.PushFullImageUndo();

            var bmp = _surface.Bitmap;
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = bmp.BackBufferStride;

            byte[] rawPixels = new byte[height * stride];
            bmp.CopyPixels(rawPixels, stride, 0);

            await Task.Run(() =>
            {
                switch (type)
                {
                    case FilterType.Sepia:
                        ProcessSepia(rawPixels, width, height, stride);
                        break;
                    case FilterType.Vignette:
                        ProcessVignette(rawPixels, width, height, stride);
                        break;
                    case FilterType.Glow:
                        ProcessGlow(rawPixels, width, height, stride);
                        break;
                    case FilterType.OilPainting:
                        ProcessOilPaint(rawPixels, width, height, stride, 4, 10);
                        break;
                    case FilterType.Sharpen:
                        ProcessSharpen(rawPixels, width, height, stride);
                        break;
                    case FilterType.Brown:
                        ProcessBrown(rawPixels, width, height, stride);
                        break;
                    case FilterType.Mosaic:
                        ProcessMosaic(rawPixels, width, height, stride, strength);
                        break;
                    case FilterType.GaussianBlur:
                        ProcessGaussianBlur(rawPixels, width, height, stride, strength);
                        break;
                }
            });

            bmp.WritePixels(new Int32Rect(0, 0, width, height), rawPixels, stride, 0);

            CheckDirtyState();
            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            ShowToast($"L_Toast_Effect_{type}");
        }

        private void OnNewWindowClick(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow newWindow = new MainWindow(string.Empty, false, loadSession: false);
                newWindow.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"{LocalizationManager.GetString("L_Common_Error")}: {ex.Message}");
            }
        }

        private void OnInvertColorsClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
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

                    Parallel.For(0, height, y =>
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            row[x * 4] = (byte)(255 - row[x * 4]);
                            row[x * 4 + 1] = (byte)(255 - row[x * 4 + 1]);
                            row[x * 4 + 2] = (byte)(255 - row[x * 4 + 2]);
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
            ShowToast("L_Toast_Effect_Inverted");
        }

        private void OnAutoLevelsClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
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

                    int[] histR = new int[256];
                    int[] histG = new int[256];
                    int[] histB = new int[256];
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
                    float clipPercent = 0.005f;
                    int threshold = (int)(totalPixels * clipPercent);

                    void GetMinMax(int[] hist, out byte min, out byte max)
                    {
                        min = 0; max = 255;
                        int count = 0;
                        for (int i = 0; i < 256; i++)
                        {
                            count += hist[i];
                            if (count > threshold) { min = (byte)i; break; }
                        }
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

                    byte[] lutR = BuildLevelLut(minR, maxR);
                    byte[] lutG = BuildLevelLut(minG, maxG);
                    byte[] lutB = BuildLevelLut(minB, maxB);

                    Parallel.For(0, height, y =>
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            row[x * 4] = lutB[row[x * 4]];
                            row[x * 4 + 1] = lutG[row[x * 4 + 1]];
                            row[x * 4 + 2] = lutR[row[x * 4 + 2]];
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
            ShowToast("L_Toast_Effect_AutoLevels");
        }

        private byte[] BuildLevelLut(byte min, byte max)
        {
            byte[] lut = new byte[256];
            if (max <= min)
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

        private async void OnRecentFileClick(object sender, string filePath)
        {
            if (File.Exists(filePath))
            {
                var (existingWindow, existingTab) = FindWindowHostingFile(filePath);
                if (existingWindow != null && existingTab != null)
                {
                    existingWindow.FocusAndSelectTab(existingTab);
                    return;
                }

                string[] files = [filePath];
                await OpenFilesAsNewTabs(files);

                UpdateImageBarSliderState();
            }
            else ShowToast(string.Format(LocalizationManager.GetString("L_Toast_FileNotFound_Format"), filePath));
        }

        private void OnClearRecentFilesClick(object sender, EventArgs e)
        {
            SettingsManager.Instance.ClearRecentFiles();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var existingSettings = OwnedWindows.OfType<SettingsWindow>().FirstOrDefault();
            if (existingSettings != null)
            {
                existingSettings.Activate();
                return;
            }

            var settingsWindow = new SettingsWindow();
            SettingsManager.Instance.Current.PropertyChanged += (s, ev) =>
            {
                if (ev.PropertyName == "ViewInterpolationThreshold" ||
                    ev.PropertyName == "PaintInterpolationThreshold")
                {
                    this.Dispatcher.Invoke(() => { RefreshBitmapScalingMode(); });
                }
            };

            settingsWindow.ProgramVersion = this.ProgramVersion;
            settingsWindow.Owner = this;
            settingsWindow.Show();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath) || IsVirtualPath(_currentFilePath)) OnSaveAsClick(sender, e);
            else SaveBitmap(_currentFilePath);
        }

        private void OnSaveAsClick(object sender, RoutedEventArgs e)
        {
            string defaultName = _currentTabItem?.DisplayName ?? "image";
            if (!defaultName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                defaultName += ".png";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = PicFilterString,
                FileName = defaultName
            };
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
                SaveBitmap(newPath);
                _currentFilePath = newPath;
                _currentFileName = System.IO.Path.GetFileName(newPath);

                if (_currentTabItem != null)
                {
                    _currentTabItem.FilePath = newPath;
                    if (_currentTabItem.IsNew)
                    {
                        _currentTabItem.IsNew = false;
                        if (!_imageFiles.Contains(newPath)) _imageFiles.Add(newPath);
                    }
                    else if (!_imageFiles.Contains(newPath))
                    {
                        _imageFiles.Add(newPath);
                    }
                    _currentImageIndex = _imageFiles.IndexOf(newPath);
                }

                _isFileSaved = true;
                UpdateWindowTitle();
            }
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select);

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CopySelection(_ctx);
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select);

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CutSelection(_ctx, true);
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select);

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.PasteSelection(_ctx, false);
        }

        private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();
        private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();

        private void OpenAdjustColorWindowSafe(int initialTabIndex)
        {
            if (_surface.Bitmap == null) return;
            _router.CleanUpSelectionandShape();

            var oldBitmapState = _surface.Bitmap.Clone();

            var dialog = new AdjustColorWindow(_surface.Bitmap, initialTabIndex)
            {
                Owner = this
            };

            if (dialog.ShowOwnerModal(this) == true)
            {
                _undo.PushExplicitImageUndo(oldBitmapState);
                NotifyCanvasChanged();
                CheckDirtyState();
                SetUndoRedoButtonState();
            }
        }

        private void OnBrightnessContrastExposureClick(object sender, RoutedEventArgs e)
        {
            OpenAdjustColorWindowSafe(0);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (!_programClosed) OnClosing();
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
            App.GlobalExit();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ThicknessSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Visible;
            UpdateThicknessPreviewPosition();

            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(ThicknessSlider.Value);
        }

        private void ThicknessSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Collapsed;
            ThicknessTip.Visibility = Visibility.Collapsed;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialLayoutComplete) return;
            if (ThicknessTip == null || ThicknessTipText == null || ThicknessSlider == null || ThicknessSlider.Visibility != Visibility.Visible)
                return;
            if (_isUpdatingToolSettings)
            {
                ThicknessTip.Visibility = Visibility.Collapsed;
                return;
            }
            double realSize = PenThickness;

            SetThicknessSlider_Pos(e.NewValue);
            UpdateThicknessPreviewPosition();

            ThicknessTipText.Text = $"{(int)Math.Round(realSize)}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
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
                UpdateImageBarSliderState();
            }
        }

        private async void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = PicFilterString,
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                string[] files = dlg.FileNames;
                await OpenFilesAsNewTabs(files);
                foreach (var file in files)
                    SettingsManager.Instance.AddRecentFile(file);
                UpdateImageBarSliderState();
            }
        }

        private void OnColorTempTintSaturationClick(object sender, RoutedEventArgs e)
        {
            OpenAdjustColorWindowSafe(1);
        }

        private void OnConvertToBlackAndWhiteClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;
            _router.CleanUpSelectionandShape();
            _undo.PushFullImageUndo();
            ConvertToBlackAndWhite(_bitmap);
            CheckDirtyState();
            SetUndoRedoButtonState();
            ShowToast("L_Toast_Effect_BW");
        }

        private async void OnResizeCanvasClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            int originalW = _surface.Bitmap.PixelWidth;
            int originalH = _surface.Bitmap.PixelHeight;
            var dialog = new ResizeCanvasDialog(originalW, originalH);
            if (dialog.ShowOwnerModal(this) == true)
            {
                int targetWidth = dialog.ImageWidth;
                int targetHeight = dialog.ImageHeight;
                bool isCanvasMode = dialog.IsCanvasResizeMode;
                bool keepRatio = dialog.IsAspectRatioLocked;
                double scaleX = (double)targetWidth / originalW;
                double scaleY = (double)targetHeight / originalH;
                if (isCanvasMode) ResizeCanvasDimensions(targetWidth, targetHeight);
                else ResizeCanvas(targetWidth, targetHeight);

                CheckDirtyState();
                if (_canvasResizer != null) _canvasResizer.UpdateUI();
                if (dialog.ApplyToAll)
                {
                    await BatchResizeImages(targetWidth, targetHeight, scaleX, scaleY, isCanvasMode, keepRatio);
                }
            }
        }

        private async Task BatchResizeImages(int targetW, int targetH, double refScaleX, double refScaleY, bool isCanvasMode, bool keepRatio)
        {
            string currentTabId = _currentTabItem?.Id;

            // 预处理：确保所有虚拟路径或无物理文件的标签页都有备份文件
            foreach (var path in _imageFiles.ToList())
            {
                var tab = FileTabs.FirstOrDefault(t => t.FilePath == path);
                if (tab == null) continue;
                if (tab.Id == currentTabId) continue;

                if (IsVirtualPath(path) && (string.IsNullOrEmpty(tab.BackupPath) || !File.Exists(tab.BackupPath)))
                {
                    // 为空白虚拟标签页生成备份图
                    var blank = GenerateBlankThumbnail(); // 默认是 200x120，我们需要更大一点的吗？
                    // 理想情况下应该用默认画布尺寸，但为了简单和支持虚拟路径应用，我们为其创建一个基本文件
                    string fullPath = Path.Combine(_cacheDir, $"{tab.Id}.png");
                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                    SaveBitmapToPng(blank, fullPath);
                    tab.BackupPath = fullPath;
                }
            }

            var tasksInfo = _imageFiles
                .Select(path =>
                {
                    var existingTab = FileTabs.FirstOrDefault(t => t.FilePath == path);
                    if (existingTab != null && existingTab.Id == currentTabId) return null;

                    string sourcePath = path;
                    string tabId = existingTab?.Id;

                    if (existingTab != null && !string.IsNullOrEmpty(existingTab.BackupPath) && File.Exists(existingTab.BackupPath))
                    {
                        sourcePath = existingTab.BackupPath;
                    }
                    else if (_offScreenBackupInfos.TryGetValue(path, out var offlineInfo) && !string.IsNullOrEmpty(offlineInfo.BackupPath) && File.Exists(offlineInfo.BackupPath))
                    {
                        sourcePath = offlineInfo.BackupPath;
                        tabId = offlineInfo.Id;
                    }

                    if (string.IsNullOrEmpty(tabId)) tabId = Guid.NewGuid().ToString();

                    return new { OriginalPath = path, SourcePath = sourcePath, TabId = tabId };
                })
                .Where(x => x != null && !string.IsNullOrEmpty(x.SourcePath) && File.Exists(x.SourcePath))
                .ToList();

            if (tasksInfo.Count == 0) return;

            string taskTitle = LocalizationManager.GetString("L_Toast_BatchResizeStart") ?? "Batch Resizing...";
            ShowToast(taskTitle);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TaskProgressPopup.SetIcon("⚙️");
                TaskProgressPopup.UpdateProgress(0, taskTitle, $"0 / {tasksInfo.Count}", "");
            });

            int processedCount = 0;
            int maxDegreeOfParallelism = Environment.ProcessorCount;
            using (var semaphore = new SemaphoreSlim(maxDegreeOfParallelism))
            {
                var tasks = new List<Task>();
                foreach (var info in tasksInfo)
                {
                    var tabToUpdate = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                    if (tabToUpdate != null) tabToUpdate.IsLoading = true;

                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            string newCachePath = null;
                            BitmapSource thumbnailResult = null;

                            Thread renderThread = new Thread(() =>
                            {
                                try
                                {
                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.UriSource = new Uri(Path.GetFullPath(info.SourcePath));
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.EndInit();
                                    bmp.Freeze();

                                    BitmapSource resultBmp = null;
                                    if (isCanvasMode)
                                    {
                                        resultBmp = ResizeBitmapCanvas(bmp, targetW, targetH);
                                    }
                                    else
                                    {
                                        int finalW, finalH;
                                        if (keepRatio)
                                        {
                                            finalW = (int)Math.Round(bmp.PixelWidth * refScaleX);
                                            finalH = (int)Math.Round(bmp.PixelHeight * refScaleY);
                                            finalW = Math.Max(1, finalW);
                                            finalH = Math.Max(1, finalH);
                                        }
                                        else
                                        {
                                            finalW = targetW;
                                            finalH = targetH;
                                        }

                                        // 优化：在批量处理中使用 SkiaSharp 进行高质量缩放
                                        // 确保格式为 Bgra32 以免 CopyPixels 失败或颜色错误
                                        BitmapSource bgraBmp = (bmp.Format == PixelFormats.Bgra32) ? bmp : new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                                        
                                        using var skSrc = new SKBitmap(bgraBmp.PixelWidth, bgraBmp.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                                        bgraBmp.CopyPixels(new Int32Rect(0, 0, bgraBmp.PixelWidth, bgraBmp.PixelHeight), skSrc.GetPixels(), bgraBmp.PixelHeight * (bgraBmp.PixelWidth * 4), bgraBmp.PixelWidth * 4);

                                        using var skDest = new SKBitmap(finalW, finalH, SKColorType.Bgra8888, SKAlphaType.Premul);
                                        skSrc.ScalePixels(skDest, SKFilterQuality.High);

                                        resultBmp = SkiaBitmapToWpfSource(skDest);
                                    }

                                    resultBmp.Freeze();
                                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                                    string fileName = $"{info.TabId}_resize_{DateTime.Now.Ticks}.png";
                                    string fullPath = Path.Combine(_cacheDir, fileName);

                                    using (var fs = new FileStream(fullPath, FileMode.Create))
                                    {
                                        BitmapEncoder encoder = new PngBitmapEncoder();
                                        encoder.Frames.Add(BitmapFrame.Create(resultBmp));
                                        encoder.Save(fs);
                                    }
                                    newCachePath = fullPath;
                                    if (resultBmp.PixelWidth > 200)
                                    {
                                        var scale = 200.0 / resultBmp.PixelWidth;
                                        var thumb = new TransformedBitmap(resultBmp, new ScaleTransform(scale, scale));
                                        thumb.Freeze();
                                        thumbnailResult = thumb;
                                    }
                                    else
                                    {
                                        thumbnailResult = resultBmp;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Batch Resize Error: {ex.Message}");
                                }
                            });
                            renderThread.SetApartmentState(ApartmentState.STA);
                            renderThread.IsBackground = true;
                            renderThread.Start();
                            renderThread.Join();

                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                if (!string.IsNullOrEmpty(newCachePath))
                                {
                                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                                    if (tab != null)
                                    {
                                        tab.BackupPath = newCachePath;
                                        tab.IsDirty = true;
                                        tab.LastBackupTime = DateTime.Now;
                                        if (thumbnailResult != null) tab.Thumbnail = thumbnailResult;
                                        tab.IsLoading = false;
                                    }
                                    UpdateSessionBackupInfo(info.OriginalPath, newCachePath, true, info.TabId);
                                }
                                else
                                {
                                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                                    if (tab != null) tab.IsLoading = false;
                                }

                                processedCount++;
                                double p = (double)processedCount / tasksInfo.Count * 100;
                                TaskProgressPopup.UpdateProgress(p, null, $"{processedCount} / {tasksInfo.Count}", "");
                            });
                        }
                        finally { semaphore.Release(); }
                    });
                    tasks.Add(task);
                }
                await Task.WhenAll(tasks);
            }

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SaveSession();
                TaskProgressPopup.Finish();
            }, System.Windows.Threading.DispatcherPriority.Background);
            ShowToast(LocalizationManager.GetString("L_Toast_BatchResizeComplete") ?? "Batch resize complete.");
        }

        private BitmapSource ResizeBitmapCanvas(BitmapSource source, int targetW, int targetH)
        {
            var rtb = new RenderTargetBitmap(targetW, targetH, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                double x = (targetW - source.PixelWidth) / 2.0;
                double y = (targetH - source.PixelHeight) / 2.0;
                ctx.DrawImage(source, new Rect(x, y, source.PixelWidth, source.PixelHeight));
            }
            rtb.Render(dv);
            return rtb;
        }

        private async void OnWatermarkClick(object sender, RoutedEventArgs e)
        {
            var oldBitmap = _surface.Bitmap;
            var undoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);

            byte[] undoPixels = new byte[undoRect.Height * oldBitmap.BackBufferStride];
            oldBitmap.CopyPixels(undoRect, undoPixels, oldBitmap.BackBufferStride, 0);
            var dlg = new WatermarkWindow(_surface.Bitmap, WatermarkPreviewLayer) { Owner = this };
            bool? dialogResult = WindowHelper.ShowOwnerModal(dlg, this);

            if (dialogResult == true)
            {
                var newBitmap = _surface.Bitmap;
                var redoPixels = new byte[undoRect.Height * newBitmap.BackBufferStride];
                newBitmap.CopyPixels(undoRect, redoPixels, newBitmap.BackBufferStride, 0);

                _undo.PushTransformAction(undoRect, undoPixels, undoRect, redoPixels);
                NotifyCanvasChanged();
                SetUndoRedoButtonState();

                if (dlg.ApplyToAll) await ApplyWatermarkToAllTabs(dlg.CurrentSettings);
            }
            else { NotifyCanvasChanged(); }
        }

        private async Task ApplyWatermarkToAllTabs(WatermarkSettings settings)
        {
            if (settings == null) return;
            string currentTabId = _currentTabItem?.Id;

            // 预处理：确保所有虚拟路径或无物理文件的标签页都有备份文件
            foreach (var path in _imageFiles.ToList())
            {
                var tab = FileTabs.FirstOrDefault(t => t.FilePath == path);
                if (tab == null) continue;
                if (tab.Id == currentTabId) continue;

                if (IsVirtualPath(path) && (string.IsNullOrEmpty(tab.BackupPath) || !File.Exists(tab.BackupPath)))
                {
                    // 为空白虚拟标签页生成备份图
                    var blank = GenerateBlankThumbnail(); 
                    string fullPath = Path.Combine(_cacheDir, $"{tab.Id}.png");
                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                    SaveBitmapToPng(blank, fullPath);
                    tab.BackupPath = fullPath;
                }
            }

            var tasksInfo = _imageFiles
                .Select(path =>
                {
                    var existingTab = FileTabs.FirstOrDefault(t => t.FilePath == path);
                    if (existingTab != null && existingTab.Id == currentTabId) return null;

                    string sourcePath = path;
                    string tabId = existingTab?.Id;

                    if (existingTab != null && !string.IsNullOrEmpty(existingTab.BackupPath) && File.Exists(existingTab.BackupPath))
                    {
                        sourcePath = existingTab.BackupPath;
                    }
                    else if (_offScreenBackupInfos.TryGetValue(path, out var offlineInfo) && !string.IsNullOrEmpty(offlineInfo.BackupPath) && File.Exists(offlineInfo.BackupPath))
                    {
                        sourcePath = offlineInfo.BackupPath;
                        tabId = offlineInfo.Id;
                    }

                    if (string.IsNullOrEmpty(tabId)) tabId = Guid.NewGuid().ToString();

                    return new { OriginalPath = path, SourcePath = sourcePath, TabId = tabId };
                })
                .Where(x => x != null && !string.IsNullOrEmpty(x.SourcePath) && File.Exists(x.SourcePath))
                .ToList();

            if (tasksInfo.Count == 0) return;

            string taskTitle = LocalizationManager.GetString("L_Toast_BatchStart") ?? "Batch Processing...";
            ShowToast(taskTitle);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TaskProgressPopup.SetIcon("✨");
                TaskProgressPopup.UpdateProgress(0, taskTitle, $"0 / {tasksInfo.Count}", "");
            });

            int processedCount = 0;
            int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);
            using (var semaphore = new SemaphoreSlim(maxDegreeOfParallelism))
            {
                var tasks = new List<Task>();
                foreach (var info in tasksInfo)
                {
                    var tabToUpdate = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                    if (tabToUpdate != null) tabToUpdate.IsLoading = true;

                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            string? newCachePath = null;
                            BitmapSource? thumbnailResult = null;

                            // 批量处理性能优化：使用单一长期运行的 STA 任务避免频繁线程创建开销
                            var tcs = new TaskCompletionSource<bool>();
                            var renderThread = new Thread(() =>
                            {
                                try
                                {
                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.UriSource = new Uri(Path.GetFullPath(info.SourcePath));
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.EndInit();
                                    bmp.Freeze();

                                    var renderedBmp = WatermarkWindow.ApplyWatermarkToBitmap(bmp, settings);
                                    
                                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                                    string fullPath = Path.Combine(_cacheDir, $"{info.TabId}_{DateTime.Now.Ticks}.png");

                                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                                    {
                                        var encoder = new PngBitmapEncoder();
                                        encoder.Frames.Add(BitmapFrame.Create(renderedBmp));
                                        encoder.Save(fileStream);
                                    }
                                    newCachePath = fullPath;

                                    // 缩略图生成优化
                                    if (renderedBmp.PixelWidth > 200)
                                    {
                                        double scale = 200.0 / renderedBmp.PixelWidth;
                                        var thumb = new TransformedBitmap(renderedBmp, new ScaleTransform(scale, scale));
                                        thumb.Freeze();
                                        thumbnailResult = thumb;
                                    }
                                    else thumbnailResult = renderedBmp;

                                    tcs.SetResult(true);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Batch WM Error: {ex.Message}");
                                    tcs.SetException(ex);
                                }
                            });
                            renderThread.SetApartmentState(ApartmentState.STA);
                            renderThread.Start();
                            await tcs.Task;

                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                if (!string.IsNullOrEmpty(newCachePath))
                                {
                                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                                    if (tab != null)
                                    {
                                        tab.BackupPath = newCachePath;
                                        tab.IsDirty = true;
                                        tab.LastBackupTime = DateTime.Now;
                                        if (thumbnailResult != null) tab.Thumbnail = thumbnailResult;
                                        tab.IsLoading = false;
                                    }
                                    UpdateSessionBackupInfo(info.OriginalPath, newCachePath, true, info.TabId);
                                }
                                else
                                {
                                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                                    if (tab != null) tab.IsLoading = false;
                                }

                                processedCount++;
                                double p = (double)processedCount / tasksInfo.Count * 100;
                                TaskProgressPopup.UpdateProgress(p, null, $"{processedCount} / {tasksInfo.Count}", "");
                            });
                        }
                        finally { semaphore.Release(); }
                    });
                    tasks.Add(task);
                }
                await Task.WhenAll(tasks);
            }

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SaveSession();
                TaskProgressPopup.Finish();
            }, System.Windows.Threading.DispatcherPriority.Background);
            ShowToast(LocalizationManager.GetString("L_Toast_BatchComplete") ?? "Batch watermark applied.");
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            CreateNewTab(TabInsertPosition.AfterCurrent, true);
        }

        private void OnRecycleBinClick(object sender, RoutedEventArgs e)
        {
        }
    }
}
