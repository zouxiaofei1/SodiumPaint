
using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

//
//TabPaint事件处理cs
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        private bool HandleGlobalShortcuts(object sender, KeyEventArgs e)
        {
            // --- 自定义部分 ---
            if (IsShortcut("View.ToggleMode", e))
            {
                TriggerModeChange();
                e.Handled = true;
                return true;
            }
            if (IsShortcut("View.RotateLeft", e))
            {
                RotateBitmap(-90);
                e.Handled = true;
                return true;
            }
            if (IsShortcut("View.RotateRight", e))
            {
                RotateBitmap(90);
                e.Handled = true;
                return true;
            }
            if (IsShortcut("View.VerticalFlip", e))
            {
                OnFlipVerticalClick(sender, e);
                e.Handled = true;
                return true;
            }
            if (IsShortcut("View.HorizontalFlip", e))
            {
                OnFlipHorizontalClick(sender, e);
                e.Handled = true;
                return true;
            }
            bool isNext = IsShortcut("View.NextImage", e);
            bool isPrev = IsShortcut("View.PrevImage", e);

            if (isNext || isPrev)
            {
                if (_router.CurrentTool is TextTool tx && tx._textBox != null) return false;
                // 如果是第一次按下（而不是按住不放触发的重复事件），初始化时间
                if (!_isNavigating)
                {
                    _isNavigating = true;
                    _navKeyPressStartTime = DateTime.Now;
                }

                if (isNext) ShowNextImage();
                if (isPrev) ShowPrevImage();

                e.Handled = true;
                return true;
            }
            if (IsShortcut("View.FullScreen", e))
            {
                MaximizeWindowHandler();
                e.Handled = true;
                return true;
            }

            // --- 硬编码/锁定部分 (如果有必须全局生效的锁定键，写在这里) ---

            return false;
        }

        private void HandleViewModeShortcuts(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.C:
                        if (_currentTabItem != null)
                        {
                            // 复用已有的复制逻辑
                            CopyTabToClipboard(_currentTabItem);
                            ShowToast("已复制");
                        }
                        e.Handled = true;
                        break;
                }
            }
        }

        private void HandlePaintModeShortcuts(object sender, KeyEventArgs e)
        {
            if (IsShortcut("Tool.SwitchToPen", e))
            {
                SetBrushStyle(BrushStyle.Pencil);
                e.Handled = true; return;
            }

            if (IsShortcut("Tool.SwitchToPick", e))
            {
                LastTool = _router.CurrentTool;
                _router.SetTool(_tools.Eyedropper);
                e.Handled = true; return;
            }

            if (IsShortcut("Tool.SwitchToEraser", e))
            {
                SetBrushStyle(BrushStyle.Eraser);

                e.Handled = true; return;
            }

            if (IsShortcut("Tool.SwitchToSelect", e))
            {
                _router.SetTool(_tools.Select);
                e.Handled = true; return;
            }

            if (IsShortcut("Tool.SwitchToFill", e))
            {
                _router.SetTool(_tools.Fill);
                e.Handled = true; return;
            }

            if (IsShortcut("Tool.SwitchToText", e))
            {
                _router.SetTool(_tools.Text);
                e.Handled = true; return;
            }

            if (IsShortcut("Tool.SwitchToBrush", e))
            {
                SetBrushStyle(BrushStyle.Round);
                e.Handled = true; return;
            }

            if (IsShortcut("Tool.SwitchToShape", e))
            {
                // 形状默认选中 "Rectangle" 
                _router.SetTool(_tools.Shape);
                e.Handled = true; return;
            }
            if (IsShortcut("Tool.ClipMonitor", e))
            {
                var settings = SettingsManager.Instance.Current;
                settings.EnableClipboardMonitor = !settings.EnableClipboardMonitor;
                e.Handled = true; return;
            }
            if (IsShortcut("Tool.RemoveBg", e)) { OnRemoveBackgroundClick(sender, e); return; }
            if (IsShortcut("Tool.ChromaKey", e)) { OnChromaKeyClick(sender, e); return; }
            if (IsShortcut("Tool.OCR", e)) { OnOcrClick(sender, e); return; }
            if (IsShortcut("Tool.ScreenPicker", e)) { OnScreenColorPickerClick(sender, e); return; }
            if (IsShortcut("Tool.CopyColorCode", e)) { OnCopyColorCodeClick(sender, e); return; }
            if (IsShortcut("Tool.AutoCrop", e)) { OnAutoCropClick(sender, e); return; }
            if (IsShortcut("Tool.AddBorder", e)) { OnAddBorderClick(sender, e); return; }

            // 文件高级
            if (IsShortcut("File.OpenWorkspace", e)) { OnOpenWorkspaceClick(sender, e); e.Handled = true; return; }
            if (IsShortcut("File.PasteNewTab", e)) { PasteClipboardAsNewTab(); e.Handled = true; return; }

            if (IsShortcut("Effect.Brightness", e)) { OnBrightnessContrastExposureClick(sender, e); e.Handled = true; return; } // Ctrl+Alt+Q
            if (IsShortcut("Effect.Temperature", e)) { OnColorTempTintSaturationClick(sender, e); e.Handled = true; return; } // Ctrl+Alt+W
            if (IsShortcut("Effect.Grayscale", e)) { OnConvertToBlackAndWhiteClick(sender, e); e.Handled = true; return; }   // Ctrl+Alt+E
            if (IsShortcut("Effect.Invert", e)) { OnInvertColorsClick(sender, e); e.Handled = true; return; }      // Ctrl+Alt+R
            if (IsShortcut("Effect.AutoLevels", e)) { OnAutoLevelsClick(sender, e); e.Handled = true; return; }  // Ctrl+Alt+T
            if (IsShortcut("Effect.Resize", e)) { OnResizeCanvasClick(sender, e); e.Handled = true; return; }      // Ctrl+Alt+Y

            // === B. 然后处理 锁定快捷键 (硬编码，不允许更改) ===

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        if (_router.CurrentTool is TextTool textTool && textTool._textBox != null)
                        {
                            textTool.GiveUpText(_ctx); // 只取消文本框，不撤销画布
                        }
                        else
                        {
                            Undo(); // 正常撤销画布操作
                        }
                        e.Handled = true;
                        break;

                    case Key.Y: Redo(); e.Handled = true; break;
                    case Key.S: OnSaveClick(sender, e); e.Handled = true; break;
                    case Key.N: OnNewClick(sender, e); e.Handled = true; break;
                    case Key.O: OnOpenClick(sender, e); e.Handled = true; break; // 普通打开
                    case Key.W:
                        var currentTab = FileTabs?.FirstOrDefault(t => t.IsSelected);
                        if (currentTab != null) CloseTab(currentTab);
                        e.Handled = true;
                        break;
                    case Key.V:
                        if (Clipboard.ContainsData(DataFormats.Rtf))
                        {
                            try
                            {
                                string rtfData = Clipboard.GetData(DataFormats.Rtf) as string;
                                var styleInfo = TextFormatHelper.ParseRtf(rtfData);

                                if (styleInfo != null && !string.IsNullOrWhiteSpace(styleInfo.Text))
                                {
                                    // A. 切换工具
                                    if (!(_router.CurrentTool is TextTool)) _router.SetTool(_tools.Text);

                                    // B. 【核心】应用样式到 UI 工具栏
                                    ApplyDetectedTextStyle(styleInfo);

                                    // C. 生成文本框 (TextTool 会自动读取刚刚更新的 UI 设置)
                                    Point center = new Point(ActualWidth / 2, ActualHeight / 2);
                                    if (_router.CurrentTool is TextTool tt)
                                    {
                                        tt.SpawnTextBox(_ctx, center, styleInfo.Text);
                                        e.Handled = true;
                                        return; // 成功处理，退出
                                    }
                                }
                            }
                            catch { /* 解析失败则回退到纯文本 */ }
                        }

                        // 2. 原有的纯文本逻辑 (作为回退)
                        if (Clipboard.ContainsText())
                        {
                            string text = Clipboard.GetText();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                if (!(_router.CurrentTool is TextTool)) _router.SetTool(_tools.Text);
                                Point center = new Point(ActualWidth / 2, ActualHeight / 2);
                                if (_router.CurrentTool is TextTool texttool)
                                {
                                    texttool.SpawnTextBox(_ctx, center, text);
                                    e.Handled = true;
                                    break;
                                }
                            }
                        }
                        bool isMultiFilePaste = false;
                        if (System.Windows.Clipboard.ContainsFileDropList())
                        {
                        }
                        if (!isMultiFilePaste)
                        {
                            _router.SetTool(_tools.Select);
                            if (_tools.Select is SelectTool st) st.PasteSelection(_ctx, true);
                        }
                        e.Handled = true;
                        break;
                    case Key.A:
                        if (_router.CurrentTool is TextTool tx && tx._textBox != null) break;
                        _router.SetTool(_tools.Select);
                        SelectTool stSelectAll = _router.GetSelectTool();
                        if (stSelectAll.HasActiveSelection) stSelectAll.CommitSelection(_ctx);
                        stSelectAll.Cleanup(_ctx);
                        stSelectAll.SelectAll(_ctx, false);
                        e.Handled = true;
                        break;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.Delete:
                        if (_tools.Select is SelectTool st)
                        {
                            if (st.HasActiveSelection)
                            {
                                st.DeleteSelection(_ctx);
                            }
                            else if ((DateTime.Now - st.LastSelectionDeleteTime).TotalSeconds < 2.0)
                            {
                                st.ResetLastDeleteTime();
                                ShowToast("再次按下 Delete 删除文件");
                            }
                            else
                            {
                                HandleDeleteFileAction();
                            }
                        }
                        e.Handled = true;
                        break;
                }
            }
        }
        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (HandleGlobalShortcuts(sender, e)) return;

            // 2. 根据模式分发
            if (IsViewMode)
            {
                HandleViewModeShortcuts(sender, e);
            }
            else
            {
                HandlePaintModeShortcuts(sender, e);
            }
        }


        private bool IsShortcut(string actionName, KeyEventArgs e)
        {
            var settings = SettingsManager.Instance.Current;
            if (settings.Shortcuts == null || !settings.Shortcuts.ContainsKey(actionName))
            {
                return false;


            }

            var item = settings.Shortcuts[actionName];

            // 必须处理 System Key (例如 Alt 键组合会被识别为 System)
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // 宽松匹配：如果设置了 Key.None，则视为禁用该快捷键
            // if (item.Key == Key.None) return false;

            return (key == item.Key && Keyboard.Modifiers == item.Modifiers);
        }



        private void EmptyClick(object sender, RoutedEventArgs e)
        {
            MainToolBar.RotateFlipMenuToggle.IsChecked = false;
            MainToolBar.BrushToggle.IsChecked = false;
        }

        private void InitializeClipboardMonitor()
        {

            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                _hwndSource = HwndSource.FromHwnd(helper.Handle);
                _hwndSource.AddHook(WndProc);
                // 默认注册监听，通过 bool 标志控制逻辑
                AddClipboardFormatListener(helper.Handle);
            }
        }
        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            _router.CurrentTool?.StopAction(_ctx);
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //  s(1);
            if (e.ClickCount == 2) // 双击标题栏切换最大化/还原
            {

                MaximizeRestore_Click(sender, null);
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (_maximized)
                {
                    // 记录按下位置，准备看是否拖动
                    _dragStartPoint = e.GetPosition(this);
                    _draggingFromMaximized = true;
                    MouseMove += Border_MouseMoveFromMaximized;
                }
                else
                {
                    DragMove(); // 普通拖动
                }
            }
        }

        private void Border_MouseMoveFromMaximized(object sender, System.Windows.Input.MouseEventArgs e)
        {

            if (_draggingFromMaximized && e.LeftButton == MouseButtonState.Pressed)
            {

                // 鼠标移动的阈值，比如 5px
                var currentPos = e.GetPosition(this);
                if (Math.Abs(currentPos.X - _dragStartPoint.X) > 5 ||
                    Math.Abs(currentPos.Y - _dragStartPoint.Y) > 5)
                {
                    // 超过阈值，恢复窗口大小，并开始拖动
                    _draggingFromMaximized = false;
                    MouseMove -= Border_MouseMoveFromMaximized;

                    _maximized = false;

                    var percentX = _dragStartPoint.X / ActualWidth;

                    Left = e.GetPosition(this).X - _restoreBounds.Width * percentX;
                    Top = e.GetPosition(this).Y;
                    Width = _restoreBounds.Width;
                    Height = _restoreBounds.Height;
                    SetMaximizeIcon();
                    DragMove();
                }
            }
        }
        // 在类成员变量区域添加

        // 在构造函数 MainWindow() 中调用此方法，或者直接把代码放进去

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        // 状态变量
        private uint _lastClipboardSequenceNumber = 0;
        private DateTime _lastClipboardActionTime = DateTime.MinValue;
        private const int CLIPBOARD_COOLDOWN_MS = 1000;
        // 计时器触发事件：真正的执行逻辑


        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                if (SettingsManager.Instance.Current.EnableClipboardMonitor)
                {
                    // 1. 获取当前剪切板的系统序列号
                    uint currentSeq = GetClipboardSequenceNumber();

                    // 2. 检查是否完全重复的消息 (序列号没变 = 剪切板内容没变，纯粹是系统发神经)
                    if (currentSeq == _lastClipboardSequenceNumber)
                    {
                        // 忽略
                        return IntPtr.Zero;
                    }

                    var timeSinceLast = (DateTime.Now - _lastClipboardActionTime).TotalMilliseconds;
                    if (timeSinceLast < CLIPBOARD_COOLDOWN_MS)
                    {

                        // 虽然跳过逻辑，但要更新序列号，以免冷却结束后把旧消息当新消息
                        _lastClipboardSequenceNumber = currentSeq;
                        return IntPtr.Zero;
                    }

                    // 4. 通过所有检查，记录状态并执行
                    _lastClipboardSequenceNumber = currentSeq;
                    _lastClipboardActionTime = DateTime.Now;
                    OnClipboardContentChanged();
                }
            }
            if (msg == WM_NCHITTEST)
            {
                if (_maximized)
                {
                    handled = true;
                    return (IntPtr)1; // HTCLIENT
                }

                var mousePos = PointFromScreen(new Point(
                    (short)(lParam.ToInt32() & 0xFFFF),
                    (short)((lParam.ToInt32() >> 16) & 0xFFFF)));

                double width = ActualWidth;
                double height = ActualHeight;

                int cornerArea = 16; // 角落区域大一点，方便对角线拖拽
                int sideArea = 8;    // 侧边区域非常小，避让滚动条 (推荐4-6px)

                handled = true;

                if (mousePos.Y <= cornerArea && mousePos.X <= cornerArea) return (IntPtr)HTTOPLEFT;
                if (mousePos.Y <= cornerArea && mousePos.X >= width - cornerArea) return (IntPtr)HTTOPRIGHT;
                // 左下
                if (mousePos.Y >= height - cornerArea && mousePos.X <= cornerArea) return (IntPtr)HTBOTTOMLEFT;
                // 右下 (这是最常用的调整区域，保持大范围)
                if (mousePos.Y >= height - cornerArea && mousePos.X >= width - cornerArea) return (IntPtr)HTBOTTOMRIGHT;


                if (mousePos.Y <= sideArea) return (IntPtr)HTTOP;
                if (mousePos.Y >= height - sideArea) return (IntPtr)HTBOTTOM;

                if (mousePos.X <= sideArea) return (IntPtr)HTLEFT;
                if (mousePos.X >= width - sideArea) return (IntPtr)HTRIGHT;
                return (IntPtr)1; // HTCLIENT
            }

            return IntPtr.Zero;
        }
        private void ClipboardMonitorToggle_Click(object sender, RoutedEventArgs e)
        {

        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private BitmapSource GetBestImageFromClipboard()
        {
            var dataObj = System.Windows.Clipboard.GetDataObject();
            if (dataObj == null) return null;

            // 1. [最优先] 尝试读取 "System.Drawing.Bitmap"
            // 既然你的格式列表里有这个，说明它是 .NET 对象，直接拿出来用 GDI+ 读取
            // GDI+ 会忽略错误的 Alpha 通道，显示出图片
            if (dataObj.GetDataPresent("System.Drawing.Bitmap"))
            {
                try
                {
                    var drawingBitmap = dataObj.GetData("System.Drawing.Bitmap") as System.Drawing.Bitmap;
                    if (drawingBitmap != null)
                    {
                        return ConvertDrawingBitmapToWPF(drawingBitmap);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("GDI+ Read Failed: " + ex.Message);
                }
            }

            // 2. [次优先] 尝试读取 "Bitmap" (标准 GDI 句柄)
            if (dataObj.GetDataPresent("Bitmap"))
            {
                try
                {
                    var drawingBitmap = dataObj.GetData("Bitmap") as System.Drawing.Bitmap;
                    if (drawingBitmap != null)
                    {
                        return ConvertDrawingBitmapToWPF(drawingBitmap);
                    }
                }
                catch { }
            }

            // 3. [保底] WPF 原生读取 (如果上面都失败了，只能面对可能全透明的结果)
            if (System.Windows.Clipboard.ContainsImage())
            {
                return System.Windows.Clipboard.GetImage();
            }

            return null;
        }

        // 转换方法：GDI+ Bitmap -> WPF BitmapSource
        private BitmapSource ConvertDrawingBitmapToWPF(System.Drawing.Bitmap bitmap)
        {
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                // 获取 GDI 句柄
                hBitmap = bitmap.GetHbitmap();

                // 利用 Interop 创建 WPF 位图
                var wpfBitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // 关键：冻结对象，使其可以在不同线程使用（如果不冻结，异步写入文件可能会报错）
                wpfBitmap.Freeze();

                return wpfBitmap;
            }
            finally
            {
                // 极其重要：必须手动释放 GDI 句柄，否则会造成 GDI 句柄泄漏导致程序崩溃
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }
                // bitmap.Dispose(); // DataObject 取出的对象通常不需要手动 Dispose，交给 GC 即可
            }
        }

        private async void OnClipboardContentChanged()
        {
            try
            {
                if (IsViewMode) return;
                var dataObj = System.Windows.Clipboard.GetDataObject();
                if (dataObj != null && dataObj.GetDataPresent(InternalClipboardFormat)) return;

                List<string> filesToLoad = new List<string>();

                // 情况 A: 剪切板是文件列表 (复制了文件)
                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var files = System.Windows.Clipboard.GetFileDropList();
                    foreach (var file in files) if (IsImageFile(file)) filesToLoad.Add(file);
                }
                // 情况 B: 剪切板是位图数据 (截图)
                else if (System.Windows.Clipboard.ContainsImage())
                {
                    var bitmapSource = GetBestImageFromClipboard();
                    if (bitmapSource != null)
                    {
                        // TabPaint 架构依赖文件路径，所以我们需要保存为临时缓存文件
                        string cachePath = SaveClipboardImageToCache(bitmapSource);
                        if (!string.IsNullOrEmpty(cachePath))
                        {
                            filesToLoad.Add(cachePath);
                        }
                    }
                }
                if (filesToLoad.Count > 0)
                {
                    await InsertImagesToTabs(filesToLoad.ToArray());
                    var settings = SettingsManager.Instance.Current;
                    if (settings.AutoPopupOnClipboardImage) RestoreWindow(this);
                }
            }
            catch (Exception ex)
            {
                // 处理剪切板被占用等异常，静默失败即可
                System.Diagnostics.Debug.WriteLine("Clipboard Access Error: " + ex.Message);
            }
        }
        private bool IsVisualAncestorOf<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T) return true;
                node = VisualTreeHelper.GetParent(node); // 关键：获取视觉树父级
            }
            return false;
        }


        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                _maximized = true;

                var workArea = SystemParameters.WorkArea;
                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;

                // 切换到还原图标
                SetRestoreIcon();
                WindowState = WindowState.Normal;
            }

        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            bool isNext = IsShortcut("View.NextImage", e);
            bool isPrev = IsShortcut("View.PrevImage", e);

            if (isNext || isPrev) // 根据你的实际快捷键添加
            {
                // 重置状态
                _isNavigating = false;
                _navKeyPressStartTime = DateTime.MinValue;
            }
        }


        private void Control_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // 1. 强制让当前的 ComboBox 失去焦点并应用更改
                DependencyObject focusScope = FocusManager.GetFocusScope((System.Windows.Controls.Control)sender);
                FocusManager.SetFocusedElement(focusScope, _activeTextBox);

                // 2. 将焦点还给画布上的文本框，让用户可以继续打字
                if (_activeTextBox != null)
                {
                    _activeTextBox.Focus();
                    // 将光标移到文字末尾
                    _activeTextBox.SelectionStart = _activeTextBox.Text.Length;
                }
                e.Handled = true; // 阻止回车产生额外的换行或响铃
            }
        }
        private void UpdateUIStatus(double realScale)
        {
            if (MyStatusBar == null) return;
                MyStatusBar.ZoomComboBox.Text = realScale.ToString("P0");
            ZoomLevel = realScale.ToString("P0"); // 如果你有绑定的属性

            // 更新滑块位置 (反向计算)
            double targetSliderVal = ZoomToSlider(realScale);
            _isInternalZoomUpdate = true;
            MyStatusBar.ZoomSliderControl.Value = targetSliderVal;
            _isInternalZoomUpdate = false;
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateImageBarSliderState(); UpdateToolSelectionHighlight();
            if (IsViewMode && !_hasUserManuallyZoomed && _bitmap != null && _startupFinished)
            {
                // 使用 Dispatcher 稍作延迟，等待 ScrollViewer 的 Viewport 更新
                Dispatcher.InvokeAsync(() =>
                {
                    FitToWindow();
                }, DispatcherPriority.Loaded);
            }
        }
        // 修改方法签名，增加 isIntermediate 参数，默认为 false
        private void SetZoom(double targetScale, Point? center = null, bool isIntermediate = false)
        {

            double oldScale = zoomscale;
            // 1. 计算最小缩放比例限制
            double minrate = 1.0;
            if (_bitmap != null)
            {
                double maxDim = Math.Max(Math.Max(BackgroundImage.Width, _bitmap.PixelWidth), Math.Max(BackgroundImage.Height, _bitmap.PixelHeight));
                if (maxDim > 0)
                    minrate = 1500.0 / maxDim;
            }
            double newScale = Math.Clamp(targetScale, MinZoom * minrate, MaxZoom);

            // 3. 确定缩放锚点
            Point anchorPoint;
            if (center.HasValue)
            {
                anchorPoint = center.Value;
            }
            else
            {
                anchorPoint = new Point(ScrollContainer.ViewportWidth / 2, ScrollContainer.ViewportHeight / 2);
            }

            // 4. 更新数据
            zoomscale = newScale;
            UpdateUIStatus(zoomscale);
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = newScale;

            RefreshBitmapScalingMode();

            // 5. 计算并应用滚动条偏移量 (核心锚点逻辑)
            double offsetX = ScrollContainer.HorizontalOffset;
            double offsetY = ScrollContainer.VerticalOffset;

            double newOffsetX = (offsetX + anchorPoint.X) * (newScale / oldScale) - anchorPoint.X;
            double newOffsetY = (offsetY + anchorPoint.Y) * (newScale / oldScale) - anchorPoint.Y;

            ScrollContainer.ScrollToHorizontalOffset(newOffsetX);
            ScrollContainer.ScrollToVerticalOffset(newOffsetY);
            if (IsViewMode) CheckBirdEyeVisibility();
            if (!IsViewMode) UpdateSelectionScalingMode();
            _canvasResizer.UpdateUI();
            if (_tools.Select is SelectTool st) st.RefreshOverlay(_ctx);
            if (_tools.Text is TextTool tx) tx.DrawTextboxOverlay(_ctx);
            if (!isIntermediate)
            {



                UpdateRulerPositions();
                if (IsViewMode && _startupFinished) { ShowToast(newScale.ToString("P0")); }
            }
        }

        // 动画相关字段
        private double _targetZoomScale; // 动画最终要达到的缩放比例
        private Point _zoomCenter;       // 缩放中心（鼠标位置）
        private bool _isZoomAnimating = false;
        private double _virtualScrollH;
        private double _virtualScrollV;
        private const double ZoomLerpFactor = 1; // 插值系数 (0.1-0.5)，越小越平滑，越大越跟手
        private const double ZoomSnapThreshold = 0.001; // 停止动画的阈值
        private void StartSmoothZoom(double targetScale, Point center)
        {
            try
            {
                double minrate = 1.0;
                if (_bitmap != null)
                {
                    double maxDim = Math.Max(Math.Max(BackgroundImage.Width, _bitmap.PixelWidth), Math.Max(BackgroundImage.Height, _bitmap.PixelHeight));
                    if (maxDim > 0)
                        minrate = 1500.0 / maxDim;
                }

                _targetZoomScale = Math.Clamp(targetScale, MinZoom * minrate, MaxZoom);
                if (Math.Abs(_targetZoomScale - zoomscale) < 0.0001) return;

                _zoomCenter = center;

                if (!_isZoomAnimating)
                {
                    // 动画开始前，先以当前 UI 的真实位置作为起点
                    _virtualScrollH = ScrollContainer.HorizontalOffset;
                    _virtualScrollV = ScrollContainer.VerticalOffset;

                    _isZoomAnimating = true;
                    CompositionTarget.Rendering += OnZoomRendering;
                }
            }
            catch (Exception ex)
            {
            }
        }


        private void OnZoomRendering(object sender, EventArgs e)
        {
            double delta = _targetZoomScale - zoomscale;
            bool isEnding = false;
            double nextScale;

            if (Math.Abs(delta) < ZoomSnapThreshold || Math.Abs(delta) < 0.00001)
            {
                nextScale = _targetZoomScale;
                isEnding = true;
            }
            else
            {
                nextScale = zoomscale + delta * ZoomLerpFactor / PerformanceScore;
            }

            double oldScale = zoomscale;

            // 2. 更新缩放 (View Model / UI)
            zoomscale = nextScale;
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = nextScale;

            double scaleRatio = nextScale / oldScale;

            _virtualScrollH = (_virtualScrollH + _zoomCenter.X) * scaleRatio - _zoomCenter.X;
            _virtualScrollV = (_virtualScrollV + _zoomCenter.Y) * scaleRatio - _zoomCenter.Y;

            ScrollContainer.ScrollToHorizontalOffset(_virtualScrollH);
            ScrollContainer.ScrollToVerticalOffset(_virtualScrollV);
            UpdateUIStatus(zoomscale);
            RefreshBitmapScalingMode();
            _canvasResizer.UpdateUI();
            if (IsViewMode) CheckBirdEyeVisibility();
            if (!IsViewMode) UpdateSelectionScalingMode();
            if (_tools.Select is SelectTool st) st.RefreshOverlay(_ctx);
            if (_tools.Text is TextTool tx) tx.DrawTextboxOverlay(_ctx);
            // 动画结束清理
            if (isEnding)
            {
                SetZoom(nextScale, _zoomCenter, isIntermediate: false);
                StopSmoothZoom();
            }
            else
            {
            }
        }


        private void StopSmoothZoom()
        {
            if (_isZoomAnimating)
            {
                _isZoomAnimating = false;
                CompositionTarget.Rendering -= OnZoomRendering;
            }
        }


        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            // 1. 处理 Shift + 滚轮 (水平滚动) - 优先级最高，保持不变
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                e.Handled = true;
                double scrollAmount = e.Delta > 0 ? -48 : 48;
                ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + scrollAmount);
                return;
            }

            // 获取当前按键状态和设置
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var wheelMode = SettingsManager.Instance.Current.ViewMouseWheelMode;

            bool isViewMode = IsViewMode;

            if (isViewMode && !isCtrl && wheelMode == MouseWheelMode.SwitchImage)
            {
                e.Handled = true;
                // 滚轮向下(Delta < 0) -> 下一张; 滚轮向上(Delta > 0) -> 上一张
                if (e.Delta < 0) ShowNextImage();
                else ShowPrevImage();
                return;
            }

            if (isCtrl || (isViewMode && wheelMode == MouseWheelMode.Zoom))
            {
                e.Handled = true; 
                _hasUserManuallyZoomed = true;
                // 获取鼠标在 ScrollContainer 中的位置作为缩放中心
                Point mousePos = e.GetPosition(ScrollContainer);
                double currentBase = _isZoomAnimating ? _targetZoomScale : zoomscale;

                // 计算缩放系数
                double deltaFactor = e.Delta > 0 ? ZoomTimes : 1 / ZoomTimes;
                double targetScale = currentBase * deltaFactor;

                // 启动平滑缩放
                StartSmoothZoom(targetScale, mousePos);
            }
        }


    }
}