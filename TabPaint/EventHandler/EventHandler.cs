
//
//EventHandler.cs
//主窗口的事件处理部分，主要负责全局快捷键监听、模式切换以及各级菜单功能的逻辑分发。
//
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


namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private bool IsEditingTextField()
        {
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox ||
                Keyboard.FocusedElement is System.Windows.Controls.PasswordBox ||
                Keyboard.FocusedElement is System.Windows.Controls.RichTextBox)
            {
                return true;
            }
            return false;
        }
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

                if (_router.CurrentTool is TextTool tx && tx._richTextBox != null) return false;
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
                            ShowToast("L_Toast_Copied");
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
                _router.SetTool(_tools.Shape);
                e.Handled = true; return;
            }
            if (IsShortcut("View.ToggleMinimize", e))
            {
                if (this.WindowState != WindowState.Minimized)
                {
                    this.WindowState = WindowState.Minimized;
                }
                e.Handled = true;
                return;
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

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_router.CurrentTool is TextTool ttt && ttt._richTextBox != null && ttt._richTextBox.IsKeyboardFocused)
                {
                    switch (e.Key)
                    {
                        case Key.C: // 复制
                        case Key.V: // 粘贴
                        case Key.X: // 剪切
                        case Key.A: // 全选
                        case Key.Z: // 撤销 (建议加上，让用户能撤销输入的文字，而不是撤销整个文本框)
                        case Key.Y: // 重做
                            return;
                    }
                }
                switch (e.Key)
                {
                    case Key.Z:
                        if (_router.CurrentTool is TextTool textTool && textTool._richTextBox != null)
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

                                    if (!(_router.CurrentTool is TextTool)) _router.SetTool(_tools.Text);
                                    ApplyDetectedTextStyle(styleInfo);
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
                        if (_router.CurrentTool is TextTool tx && tx._richTextBox != null) break;
                        if (_router.CurrentTool != _tools.Select) break;
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
                            else if (SettingsManager.Instance.Current.EnableFileDeleteInPaintMode)
                                if ((DateTime.Now - st.LastSelectionDeleteTime).TotalSeconds < AppConsts.DoubleClickTimeThreshold)
                            {
                                st.ResetLastDeleteTime();
                                    ShowToast("L_Toast_PressDeleteAgain");
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
            if (IsEditingTextField())
            {
                // 1. 允许基础光标导航和删除键穿透给输入框
                switch (e.Key)
                {
                    case Key.Left:
                    case Key.Right:
                    case Key.Up:
                    case Key.Down:
                    case Key.Home:
                    case Key.End:
                    case Key.Delete:
                    case Key.Back:
                    case Key.Tab:
                    case Key.Enter:
                        return;
                }
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {
                        case Key.C: // 复制文本
                        case Key.V: // 粘贴文本
                        case Key.X: // 剪切文本
                        case Key.A: // 全选文本
                        case Key.Z: // 撤销输入
                        case Key.Y: // 重做输入
                            return;
                    }
                }
            }
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
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        // 状态变量
        private uint _lastClipboardSequenceNumber = 0;
        private DateTime _lastClipboardActionTime = DateTime.MinValue;
        // 计时器触发事件：真正的执行逻辑


        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == AppConsts.WM_MOUSEHWHEEL)
            {
                if (ScrollContainer != null && !_isZoomAnimating)
                {
                    short tilt = (short)((wParam.ToInt64() >> 16) & 0xFFFF);

                    if (tilt != 0)
                    {
                        double scrollAmount = tilt / 2.0;
                        ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + scrollAmount);
                        handled = true;
                    }
                }
            }
            if (msg == AppConsts.WM_CLIPBOARDUPDATE)
            {
                if (SettingsManager.Instance.Current.EnableClipboardMonitor)
                {
                    // 1. 获取当前剪切板的系统序列号
                    uint currentSeq = GetClipboardSequenceNumber();

                    if (currentSeq == _lastClipboardSequenceNumber)
                    {
                        // 忽略
                        return IntPtr.Zero;
                    }

                    var timeSinceLast = (DateTime.Now - _lastClipboardActionTime).TotalMilliseconds;
                    if (timeSinceLast < AppConsts.ClipboardCooldownMs)
                    {
                        _lastClipboardSequenceNumber = currentSeq;
                        return IntPtr.Zero;
                    }
                    _lastClipboardSequenceNumber = currentSeq;
                    _lastClipboardActionTime = DateTime.Now;
                    OnClipboardContentChanged();
                }
            }
            if (msg == AppConsts.WM_NCHITTEST)
            {
                if (_maximized)
                {
                    handled = true;
                    return (IntPtr)AppConsts.HTCLIENT; // HTCLIENT
                }

                var mousePos = PointFromScreen(new Point(
                    (short)(lParam.ToInt32() & 0xFFFF),
                    (short)((lParam.ToInt32() >> 16) & 0xFFFF)));

                double width = ActualWidth;
                double height = ActualHeight;

                int cornerArea = AppConsts.WindowCornerArea; // 角落区域大一点，方便对角线拖拽
                int sideArea = AppConsts.WindowSideArea;    // 侧边区域非常小，避让滚动条 (推荐4-6px)

                handled = true;

                if (mousePos.Y <= cornerArea && mousePos.X <= cornerArea) return (IntPtr)AppConsts.HTTOPLEFT;
                if (mousePos.Y <= cornerArea && mousePos.X >= width - cornerArea) return (IntPtr)AppConsts.HTTOPRIGHT;
                // 左下
                if (mousePos.Y >= height - cornerArea && mousePos.X <= cornerArea) return (IntPtr)AppConsts.HTBOTTOMLEFT;
                // 右下 (这是最常用的调整区域，保持大范围)
                if (mousePos.Y >= height - cornerArea && mousePos.X >= width - cornerArea) return (IntPtr)AppConsts.HTBOTTOMRIGHT;


                if (mousePos.Y <= sideArea) return (IntPtr)AppConsts.HTTOP;
                if (mousePos.Y >= height - sideArea) return (IntPtr)AppConsts.HTBOTTOM;

                if (mousePos.X <= sideArea) return (IntPtr)AppConsts.HTLEFT;
                if (mousePos.X >= width - sideArea) return (IntPtr)AppConsts.HTRIGHT;
                return (IntPtr)AppConsts.HTCLIENT; // HTCLIENT
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
            catch (Exception)
            {
            }
            }
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

            if (System.Windows.Clipboard.ContainsImage())  return System.Windows.Clipboard.GetImage();
            return null;
        }

        // 转换方法：GDI+ Bitmap -> WPF BitmapSource
        private BitmapSource ConvertDrawingBitmapToWPF(System.Drawing.Bitmap bitmap)
        {
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bitmap.GetHbitmap();

                var wpfBitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                wpfBitmap.Freeze();

                return wpfBitmap;
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }
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
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ClipboardError_Prefix"), ex.Message));
            }
        }
        private bool IsVisualAncestorOf<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T) return true;
                node = VisualTreeHelper.GetParent(node);
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

            if (isNext || isPrev) 
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
                   // _activeTextBox.SelectionStart = _activeTextBox.Text.Length;
                }
                e.Handled = true; // 阻止回车产生额外的换行或响铃
            }
        }
        private void UpdateUIStatus(double realScale)
        {
            if (MyStatusBar == null) return;
            MyStatusBar.ZoomComboBox.Text = realScale.ToString("P0");
            ZoomLevel = realScale.ToString("P0"); 

            // 更新滑块位置 (反向计算)
            double targetSliderVal = ZoomToSlider(realScale);
            _isInternalZoomUpdate = true;
            MyStatusBar.ZoomSliderControl.Value = targetSliderVal;
            _isInternalZoomUpdate = false;
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateImageBarSliderState(); UpdateToolSelectionHighlight();
            CheckFittoWindow();
        }
        public void CheckFittoWindow()
        {
            if (!_hasUserManuallyZoomed && _bitmap != null && _startupFinished)
            {
                // 使用 Dispatcher 稍作延迟，等待 ScrollViewer 的 Viewport 更新
                Dispatcher.InvokeAsync(() =>
                {
                    FitToWindow();
                }, DispatcherPriority.Loaded);
            }
        }
        private void SetZoom(double targetScale, Point? center = null, bool isIntermediate = false, bool slient = false)
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


            UpdateRulerPositions();
            if (IsViewMode && _startupFinished && !slient) 
            {
                ShowToast(newScale.ToString("P0"));
            }

        }
        // 动画相关字段
        private double _targetZoomScale; // 动画最终要达到的缩放比例
        private Point _zoomCenter;       // 缩放中心（鼠标位置）
        private bool _isZoomAnimating = false;
        private double _virtualScrollH;
        private double _virtualScrollV;
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
            catch (Exception)
            {
            }
        }
        private void OnZoomRendering(object sender, EventArgs e)
        {
            double delta = _targetZoomScale - zoomscale;
            bool isEnding = false;
            double nextScale;

            if (Math.Abs(delta) < AppConsts.ZoomSnapThreshold || Math.Abs(delta) < 0.00001)
            {
                nextScale = _targetZoomScale;
                isEnding = true;
            }
            else
            {
                nextScale = zoomscale + delta * AppConsts.ZoomLerpFactor / PerformanceScore;
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
            if (IsViewMode && _startupFinished) { ShowToast(zoomscale.ToString("P0")); }
            // 动画结束清理
            if (isEnding)
            {
                StopSmoothZoom();
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
                e.Handled = true;      // 滚轮向下(Delta < 0) -> 下一张; 滚轮向上(Delta > 0) -> 上一张
          
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