
//
//EventHandler.ToolBar.cs
//工具栏相关的逻辑处理，包括工具切换（画笔、选区等）、旋转翻转操作、颜色选择以及缩放控制。
//
using System.ComponentModel;
using System.Drawing;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Controls;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private async Task SwitchWorkspaceToNewFile(string filePath)
        {
            try
            {
                if (_currentTabItem != null)
                {
                    UpdateTabThumbnail(_currentTabItem); // 更新缩略图
                    TriggerBackgroundBackup(); // 触发保存
                }
                int waitCount = 0;
                while (_isSavingFile && waitCount < 10)
                {
                    await Task.Delay(100);
                    waitCount++;
                }
                SaveSession();
                _loadImageCts?.Cancel();
                lock (_queueLock) { _pendingFilePath = null; }
                FileTabs.Clear();
                _imageFiles.Clear(); // 清空之前的文件夹扫描缓存
                _currentImageIndex = -1;

                // 4. 重置画布状态
                _undo?.ClearUndo();
                _undo?.ClearRedo();
                ResetDirtyTracker();
               _currentFilePath =  _workingPath = filePath;
                CheckFilePathAvailibility(filePath);
                await OpenImageAndTabs(filePath, refresh: true, lazyload: false, forceFolderScan: true); LoadSessionForCurrentWorkspace(filePath);
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_SwitchWorkspaceFailed_Prefix"), ex.Message));
            }
        }

        private void OnPenClick(object sender, RoutedEventArgs e)
        {
            SetBrushStyle(BrushStyle.Pencil);
        }
        private void OnPickColorClick(object s, RoutedEventArgs e)
        {
            LastTool = ((MainWindow)System.Windows.Application.Current.MainWindow)._router.CurrentTool;
            _router.SetTool(_tools.Eyedropper);
        }

        private void OnEraserClick(object s, RoutedEventArgs e)
        {
            SetBrushStyle(BrushStyle.Eraser);
        }
        private void OnFillClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Fill);
        private void OnSelectClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Select);

        private void OnEffectButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            btn.ContextMenu.IsOpen = true;
        }

        private void OnShapeStyleClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.MenuItem item && item.Tag is string tag)
            {
                var shapeTool = _tools.Shape as ShapeTool;
                if (shapeTool == null) return;

                if (Enum.TryParse(tag, out ShapeTool.ShapeType type))
                {
                    shapeTool.SetShapeType(type);
                    _router.SetTool(shapeTool); UpdateShapeSplitButtonIcon(type);
                    UpdateToolSelectionHighlight();
                }
                MainToolBar.ShapeToggle.IsChecked = false;
            }
        }
        private void UpdateShapeSplitButtonIcon(ShapeTool.ShapeType type)
        {
            string resKey = "Shapes_Image";

            switch (type)
            {
                case ShapeTool.ShapeType.Rectangle: resKey = "Icon_Shape_Rectangle"; break;
                case ShapeTool.ShapeType.Ellipse: resKey = "Icon_Shape_Ellipse"; break;
                case ShapeTool.ShapeType.Line: resKey = "Icon_Shape_Line"; break;
                case ShapeTool.ShapeType.Arrow: resKey = "Icon_Shape_Arrow"; break;
                case ShapeTool.ShapeType.RoundedRectangle: resKey = "Icon_Shape_RoundedRect"; break;
                case ShapeTool.ShapeType.Triangle: resKey = "Icon_Shape_Triangle"; break; // 需在资源中定义
                case ShapeTool.ShapeType.Diamond: resKey = "Icon_Shape_Diamond"; break;
                case ShapeTool.ShapeType.Pentagon: resKey = "Icon_Shape_Pentagon"; break;
                case ShapeTool.ShapeType.Star: resKey = "Icon_Shape_Star"; break;
                case ShapeTool.ShapeType.Bubble: resKey = "Icon_Shape_Bubble"; break;
            }

            MainToolBar.UpdateShapeIcon(resKey);
        }

        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            double targetScale = zoomscale / ZoomTimes;

            System.Windows.Point centerPoint = new System.Windows.Point(ScrollContainer.ViewportWidth / 2, ScrollContainer.ViewportHeight / 2);
            _hasUserManuallyZoomed = true;
            StartSmoothZoom(targetScale, centerPoint);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            double targetScale = zoomscale * ZoomTimes;

            // 2. 获取屏幕中心点
            System.Windows.Point centerPoint = new System.Windows.Point(ScrollContainer.ViewportWidth / 2, ScrollContainer.ViewportHeight / 2);
            _hasUserManuallyZoomed = true;
            // 3. 启动平滑动画
            StartSmoothZoom(targetScale, centerPoint);
        }
        private void OnRotateLeftClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(-90); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotateRightClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(90); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotate180Click(object sender, RoutedEventArgs e)
        {
            RotateBitmap(180); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }


        private void OnFlipVerticalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: true); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnFlipHorizontalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: false); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }
        private void FontSettingChanged(object sender, RoutedEventArgs e)
        {
            // 防止初始化时触发
            if (_tools == null || _router.CurrentTool != _tools.Text) return;
            //(_tools.Text as TextTool)?.UpdateCurrentTextBoxAttributes();
            if (_router.CurrentTool is TextTool textTool)
            {
                // 互斥逻辑：上下标不能同时存在
                if (sender == TextMenu.SubscriptBtn && TextMenu.SubscriptBtn.IsChecked == true) TextMenu.SuperscriptBtn.IsChecked = false;
                if (sender == TextMenu.SuperscriptBtn && TextMenu.SuperscriptBtn.IsChecked == true) TextMenu.SubscriptBtn.IsChecked = false;

                textTool.ApplySelectionAttributes(); // 应用选区样式
                textTool.UpdateCurrentTextBoxAttributes(); // 应用整体样式并重绘边框
            }
        }

        private void OnColorOneClick(object sender, RoutedEventArgs e)
        {
            useSecondColor = false;
            _ctx.PenColor = ForegroundColor;
            SelectedBrush = new SolidColorBrush(ForegroundColor);
            UpdateColorHighlight(); // 更新高亮
        }

        private void OnColorTwoClick(object sender, RoutedEventArgs e)
        {
            useSecondColor = true;
            _ctx.PenColor = BackgroundColor;
            SelectedBrush = new SolidColorBrush(BackgroundColor);
            UpdateColorHighlight(); // 更新高亮
        }

        private void OnColorButtonClick(object sender, RoutedEventArgs e)//选色按钮
        {
            if (e.OriginalSource is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                SelectedBrush = new SolidColorBrush(brush.Color);
                _ctx.PenColor = brush.Color;
                UpdateCurrentColor(_ctx.PenColor, useSecondColor);
            }
        }

        private void OnCustomColorClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Media.Color initialColor = _ctx.PenColor;
            if (initialColor == Colors.Transparent) initialColor = Colors.Black;

            var dlg = new ModernColorPickerWindow(initialColor);
            dlg.Owner = this; // 确保在主窗口之上

            if (dlg.ShowDialog() == true)
            {
                var color = dlg.SelectedColor;
                var brush = new SolidColorBrush(color);
                SelectedBrush = brush;

                _ctx.PenColor = color;
                UpdateCurrentColor(_ctx.PenColor, useSecondColor);
            }
        }
        private void OnStatusBarZoomChanged(object sender, ZoomRoutedEventArgs e)
        {
            double newScale = e.NewZoom;
            zoomscale = Math.Clamp(newScale, MinZoom, MaxZoom);
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
            UpdateSliderBarValue(zoomscale);

        }
        private void OnBrushStyleClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.MenuItem menuItem
                && menuItem.Tag is string tagString
                && Enum.TryParse(tagString, out BrushStyle style))
            {
               
                _router.SetTool(_tools.Pen);
                
                _ctx.PenStyle = style;
                UpdateBrushSplitButtonIcon(style);
                UpdateToolSelectionHighlight();
                AutoSetFloatBarVisibility();
                UpdateGlobalToolSettingsKey();
                // 关闭 UserControl 里的 ToggleButton
                MainToolBar.BrushToggle.IsChecked = false;
            }
        }
        private void UpdateBrushSplitButtonIcon(BrushStyle style)
        {
            string resKey = "Brush_Normal_Image"; // 默认
            bool isPath = true; // 大部分是 Path

            switch (style)
            {
                case BrushStyle.Round: resKey = "Brush_Normal_Image"; break;
                case BrushStyle.Square: resKey = "Brush_Rect_Image"; break;
                case BrushStyle.Brush: resKey = "Brush_Normal_Image"; break;
                case BrushStyle.Calligraphy: resKey = "Brush_Image"; isPath = false; break; // 它是 Image
                case BrushStyle.Spray: resKey = "Paint_Spray_Image"; break;
                case BrushStyle.Crayon: resKey = "Crayon_Image"; break;
                case BrushStyle.Watercolor: resKey = "Watercolor_Image"; break;
                case BrushStyle.Highlighter: resKey = "Highlighter_Image"; isPath = false; break; // Image
                case BrushStyle.Mosaic: resKey = "Mosaic_Image"; break;
                case BrushStyle.AiEraser: resKey = "AIEraser_Image"; isPath = true; break;
                case BrushStyle.GaussianBlur: resKey = "Blur_Image"; isPath = true;  break;
                    // Pencil 和 Eraser 通常在基础工具栏有独立按钮，这里也可以不用处理，或者给个默认图标
            }

            MainToolBar.UpdateBrushIcon(resKey, isPath);
        }

        private void ZoomMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = MyStatusBar.ZoomComboBox;

            if (combo != null && combo.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                double selectedScale = Convert.ToDouble(item.Tag);
                zoomscale = Math.Clamp(selectedScale, MinZoom, MaxZoom);
                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;

                UpdateSliderBarValue(zoomscale);
            }
        }

        private void OnTextClick(object sender, RoutedEventArgs e)
        {
            _router.SetTool(_tools.Text);
        }
        private bool _isTextBarDragging = false;
        private System.Windows.Point _textBarLastPoint;
        private const double DragSafetyMargin = AppConsts.DragSafetyMargin;

        private void TextEditBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border || e.OriginalSource is StackPanel)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    _isTextBarDragging = true;
                    _textBarLastPoint = e.GetPosition(this); // 获取相对于窗口的坐标
                    TextMenu.TextEditBar.CaptureMouse(); 
                    if (_tools?.Text is TextTool tt && tt._richTextBox != null)
                    {
                        tt._richTextBox.Focus();
                    }
                }
            }
        }
        private void TextEditBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isTextBarDragging)
            {
                System.Windows.Point currentPoint = e.GetPosition(this);

                // 1. 计算鼠标位移
                double offsetX = currentPoint.X - _textBarLastPoint.X;
                double offsetY = currentPoint.Y - _textBarLastPoint.Y;

                // 2. 预测新的 Transform 值
                double proposedTransformX = TextMenu.TextBarDragTransform.X + offsetX;
                double proposedTransformY = TextMenu.TextBarDragTransform.Y + offsetY;
                double windowWidth = this.ActualWidth;
                double windowHeight = this.ActualHeight;
                double barWidth = TextMenu.TextEditBar.ActualWidth;
                double barHeight = TextMenu.TextEditBar.ActualHeight;

                double initialLeft = (windowWidth - barWidth) / 2;
                double initialTop = TextMenu.TextEditBar.Margin.Top;

                // 计算移动后的绝对位置 (Left, Top)
                double absoluteLeft = initialLeft + proposedTransformX;
                double absoluteTop = initialTop + proposedTransformY;

                if (absoluteLeft + barWidth < DragSafetyMargin)
                {
                    proposedTransformX = DragSafetyMargin - barWidth - initialLeft;
                }
                else if (absoluteLeft > windowWidth - DragSafetyMargin)
                {
                    proposedTransformX = windowWidth - DragSafetyMargin - initialLeft;
                }

                if (absoluteTop < 0)
                {
                    proposedTransformY = -initialTop; // 刚好顶到窗口上边缘
                }
                else if (absoluteTop > windowHeight - DragSafetyMargin)
                {
                    proposedTransformY = windowHeight - DragSafetyMargin - initialTop;
                }
                TextMenu.TextBarDragTransform.X = proposedTransformX;
                TextMenu.TextBarDragTransform.Y = proposedTransformY;
                _textBarLastPoint = currentPoint;
            }
        }
        private void TextEditBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isTextBarDragging)
            {
                _isTextBarDragging = false;
                TextMenu.TextEditBar.ReleaseMouseCapture(); // 释放鼠标捕获
            }
        }
        public enum SelectionType { Rectangle, Lasso, MagicWand }

        // 2. 处理左侧主按钮点击
        private void OnSelectMainClick(object sender, RoutedEventArgs e)
        {
            // 切换到选择工具，保持当前的选区模式
            _router.SetTool(_tools.Select);
        }

        // 3. 处理下拉菜单点击
        private void OnSelectStyleClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string tag)
            {
                var selectTool = _tools.Select as SelectTool;
                if (selectTool == null) return;

                // 根据 Tag 切换模式
                switch (tag)
                {
                    case "Rectangle":
                        selectTool.SelectionType = SelectionType.Rectangle;
                        break;
                    case "Lasso":
                        selectTool.SelectionType = SelectionType.Lasso;
                        ShowToast(" (UI Only)");
                        break;
                    case "MagicWand": // 新增逻辑
                        selectTool.SelectionType = SelectionType.MagicWand;
                        ShowToast("Magic Wand Tool (UI Only)");
                        break;
                }

                // 激活工具
                _router.SetTool(_tools.Select);

                // 关闭下拉菜单
                MainToolBar.SubMenuPopupSelect.IsOpen = false;

                // 触发 UI 刷新（高亮更新）
                UpdateToolSelectionHighlight();

                // 更新主按钮图标
                UpdateSelectToolIcon(selectTool.SelectionType);
            }
        }

        private void UpdateSelectToolIcon(SelectionType type)
        {
            // 获取 ContentControl
            var iconHost = MainToolBar.CurrentSelectIconHost;
            iconHost.Content = null;

            if (type == SelectionType.MagicWand)
            {
                var wandPath = new System.Windows.Shapes.Path
                {
                    Data = TryFindResource("Wand_Image") as Geometry,
                    Fill = (System.Windows.Media.Brush)FindResource("IconFillBrush"),
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Width = 16,
                    Height = 16
                };
                iconHost.Content = wandPath;
            }
            else if (type == SelectionType.Lasso)
            {
                var lassoPath = new System.Windows.Shapes.Path
                {
                    Data = TryFindResource("Lasso_Image") as Geometry,
                    Stroke = (System.Windows.Media.Brush)FindResource("IconFillBrush"),
                   
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Width = 16, // 统一为 16
                    Height = 16
                };
                iconHost.Content = lassoPath;
            }
            else
            {
                // 恢复为矩形 Image
                var rectImg = new System.Windows.Controls.Image
                {
                    Source = (ImageSource)FindResource("Select_Image"),
                    Width = 16,
                    Height = 16
                };
                iconHost.Content = rectImg;
            }
        }

        private void OnBrushMainClick(object sender, RoutedEventArgs e)
        {
            if (!(_router.CurrentTool is PenTool))
            {
                _router.SetTool(_tools.Pen);
            }

            if (_ctx.PenStyle == BrushStyle.Pencil ||
                _ctx.PenStyle == BrushStyle.Eraser ||
                _ctx.PenStyle == BrushStyle.AiEraser)
            {
                // 这里设置为默认画笔样式，通常是 Brush 或 Round
                _ctx.PenStyle = BrushStyle.Brush;

                // 3. 必须手动触发 UI 更新，否则图标和属性栏不会变
                UpdateBrushSplitButtonIcon(_ctx.PenStyle);
                UpdateToolSelectionHighlight();
                AutoSetFloatBarVisibility();
                UpdateGlobalToolSettingsKey();
            }
        }

        // 点击左侧形状按钮：切换到形状工具（保持当前 ShapeType）
        private void OnShapeMainClick(object sender, RoutedEventArgs e)
        {
            if (!(_router.CurrentTool is ShapeTool))
            {
                _router.SetTool(_tools.Shape);
            }
        }


        private void FontSizeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TextMenu.FontSizeBox.Text, out _))
            {
                TextMenu.FontSizeBox.Text = _activeTextBox.FontSize.ToString(); // 还原为当前有效字号
            }
        }
    }
}