
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Controls;

//
//TabPaint事件处理cs
//Toolbar包括上下两个工具栏！
//

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
                    _router.SetTool(shapeTool);
                    UpdateToolSelectionHighlight();
                    // 更新图标逻辑保持不变
                    switch (type)
                    {
                        case ShapeTool.ShapeType.Rectangle:
                            MainToolBar.CurrentShapeIcon.Data = (Geometry)FindResource("Icon_Shape_Rectangle");
                            break;
                        case ShapeTool.ShapeType.Ellipse:
                            MainToolBar.CurrentShapeIcon.Data = (Geometry)FindResource("Icon_Shape_Ellipse");
                            break;
                        case ShapeTool.ShapeType.Line:
                            MainToolBar.CurrentShapeIcon.Data = (Geometry)FindResource("Icon_Shape_Line");
                            break;
                        case ShapeTool.ShapeType.Arrow:
                            MainToolBar.CurrentShapeIcon.Data = (Geometry)FindResource("Icon_Shape_Arrow");
                            break;
                        case ShapeTool.ShapeType.RoundedRectangle:
                            MainToolBar.CurrentShapeIcon.Data = (Geometry)FindResource("Icon_Shape_RoundedRect");
                            break;
                    }
                }
                MainToolBar.ShapeToggle.IsChecked = false;
            }
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
            (_tools.Text as TextTool)?.UpdateCurrentTextBoxAttributes();
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

                UpdateToolSelectionHighlight();
                AutoSetFloatBarVisibility();
                UpdateGlobalToolSettingsKey();
                // 关闭 UserControl 里的 ToggleButton
                MainToolBar.BrushToggle.IsChecked = false;
            }
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
        private const double DragSafetyMargin = 50.0;

        private void TextEditBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border || e.OriginalSource is StackPanel)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    _isTextBarDragging = true;
                    _textBarLastPoint = e.GetPosition(this); // 获取相对于窗口的坐标
                    TextEditBar.CaptureMouse(); 
                    if (_tools?.Text is TextTool tt && tt._textBox != null)
                    {
                        tt._textBox.Focus();
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
                double proposedTransformX = TextBarDragTransform.X + offsetX;
                double proposedTransformY = TextBarDragTransform.Y + offsetY;
                double windowWidth = this.ActualWidth;
                double windowHeight = this.ActualHeight;
                double barWidth = TextEditBar.ActualWidth;
                double barHeight = TextEditBar.ActualHeight;

                double initialLeft = (windowWidth - barWidth) / 2;
                double initialTop = TextEditBar.Margin.Top;

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
                TextBarDragTransform.X = proposedTransformX;
                TextBarDragTransform.Y = proposedTransformY;
                _textBarLastPoint = currentPoint;
            }
        }
        private void TextEditBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isTextBarDragging)
            {
                _isTextBarDragging = false;
                TextEditBar.ReleaseMouseCapture(); // 释放鼠标捕获
            }
        }


        private void FontSizeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(FontSizeBox.Text, out _))
            {
                FontSizeBox.Text = _activeTextBox.FontSize.ToString(); // 还原为当前有效字号
            }
        }
    }
}