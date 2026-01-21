
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TEXTtool
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        public partial class TextTool : ToolBase
        {
          
            public override void Cleanup(ToolContext ctx)
            {
                MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (_textBox != null && !string.IsNullOrWhiteSpace(_textBox.Text)) CommitText(ctx);

                if (_textBox != null && ctx.EditorOverlay.Children.Contains(_textBox))
                {
                    ctx.EditorOverlay.Children.Remove(_textBox);
                    _textBox = null;
                }
                if (ctx.SelectionOverlay != null)
                {
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                }
                mw.HideTextToolbar();

                // 5️⃣ 重置工具状态
                _dragging = false;
                _resizing = false;
                _currentAnchor = ResizeAnchor.None;
                _textRect = new Int32Rect();
                lag = 0;

                Mouse.OverrideCursor = null;
                if (mw._canvasResizer != null) mw._canvasResizer.SetHandleVisibility(true);
            }
            public void GiveUpText(ToolContext ctx)
            {
                bool hastext = (_textBox != null && !string.IsNullOrWhiteSpace(_textBox.Text));
                Cleanup(ctx);
                if (hastext)
                {
                    ctx.Undo.Undo();
                    ctx.Undo._redo.Pop();
                    ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                }
            }
            public override void SetCursor(ToolContext ctx)
            {
                System.Windows.Input.Mouse.OverrideCursor = null;

                if (ctx.ViewElement != null)
                {
                    ctx.ViewElement.Cursor = this.Cursor;
                }
            }
            private List<Point> GetHandlePositions(Int32Rect rect)
            {
                var handles = new List<Point>();
                double x1 = rect.X;
                double y1 = rect.Y;
                double x2 = rect.X + rect.Width;
                double y2 = rect.Y + rect.Height;
                double mx = (x1 + x2) / 2;
                double my = (y1 + y2) / 2;
                //s(rect);
                handles.Add(new Point(x1, y1)); // TL
                handles.Add(new Point(mx, y1)); // TM
                handles.Add(new Point(x2, y1)); // TR
                handles.Add(new Point(x1, my)); // LM
                handles.Add(new Point(x2, my)); // RM
                handles.Add(new Point(x1, y2)); // BL
                handles.Add(new Point(mx, y2)); // BM
                handles.Add(new Point(x2, y2)); // BR

                return handles;
            }

            public void DrawTextboxOverlay(ToolContext ctx)
            {
                MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (_textBox == null) return;

                double invScale = 1 / mw.zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.Children.Clear();

                // 获取 TextBox 坐标和尺寸
                double x = Canvas.GetLeft(_textBox);
                double y = Canvas.GetTop(_textBox);
                double w = _textBox.ActualWidth;
                double h = _textBox.ActualHeight;
                var rect = new Int32Rect((int)x, (int)y, (int)w, (int)h);

                var outline = new System.Windows.Shapes.Rectangle  // 虚线框
                {
                    Stroke = mw._darkBackgroundBrush,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    StrokeThickness = invScale * 1.5,
                    Width = rect.Width,
                    Height = rect.Height
                };
                Canvas.SetLeft(outline, rect.X);
                Canvas.SetTop(outline, rect.Y);
                overlay.Children.Add(outline);

                // 八个句柄
                foreach (var p in GetHandlePositions(rect))
                {
                    var handle = new System.Windows.Shapes.Rectangle
                    {
                        Width = HandleSize * invScale,
                        Height = HandleSize * invScale,
                        Fill = Brushes.White,
                        Stroke = mw._darkBackgroundBrush,
                        StrokeThickness = invScale
                    };
                    Canvas.SetLeft(handle, p.X - HandleSize * invScale / 2);
                    Canvas.SetTop(handle, p.Y - HandleSize * invScale / 2);
                    overlay.Children.Add(handle);
                }

                overlay.IsHitTestVisible = false;
                overlay.Visibility = Visibility.Visible;
                if (mw._canvasResizer != null) mw._canvasResizer.SetHandleVisibility(false);
            }

            // 判断是否点击到句柄
            private ResizeAnchor HitTestTextboxHandle(Point px)
            {
                if (_textBox == null) return ResizeAnchor.None;
                double size = 12 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                double x1 = Canvas.GetLeft(_textBox);
                double y1 = Canvas.GetTop(_textBox);
                double x2 = x1 + _textBox.ActualWidth;
                double y2 = y1 + _textBox.ActualHeight;
                double mx = (x1 + x2) / 2;
                double my = (y1 + y2) / 2;

                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopLeft;
                if (Math.Abs(px.X - mx) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopRight;
                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - my) <= size) return ResizeAnchor.LeftMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - my) <= size) return ResizeAnchor.RightMiddle;
                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomLeft;
                if (Math.Abs(px.X - mx) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomRight;

                return ResizeAnchor.None;
            }

            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                if ((_dragging || _resizing) && Mouse.LeftButton == MouseButtonState.Released)
                {
                    _dragging = false;
                    _resizing = false;
                    _currentAnchor = ResizeAnchor.None;
                    if (ctx.EditorOverlay.IsMouseCaptured)
                    {
                        ctx.EditorOverlay.ReleaseMouseCapture();
                    }
                    Mouse.OverrideCursor = null;
                    return; // 直接退出，不执行后面的移动逻辑
                }
                var px = ctx.ToPixel(viewPos);

                // 1️⃣ 光标状态更新逻辑 (增加移动光标检测)
                if (_textBox != null && !_resizing && !_dragging) // 如果没有在操作中，才检测光标
                {
                    var anchor = HitTestTextboxHandle(px);
                    if (anchor != ResizeAnchor.None)
                    {
                        // 命中句柄 -> 显示调整大小光标
                        switch (anchor)
                        {
                            case ResizeAnchor.TopLeft:
                            case ResizeAnchor.BottomRight:
                                Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNWSE;
                                break;
                            case ResizeAnchor.TopRight:
                            case ResizeAnchor.BottomLeft:
                                Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNESW;
                                break;
                            case ResizeAnchor.LeftMiddle:
                            case ResizeAnchor.RightMiddle:
                                Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeWE;
                                break;
                            case ResizeAnchor.TopMiddle:
                            case ResizeAnchor.BottomMiddle:
                                Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNS;
                                break;
                        }
                    }
                    else if (IsInsideBorder(px))
                    {
                        // 命中虚线边框 -> 显示移动光标 (十字箭头) ✨✨✨
                        Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeAll;
                    }
                    else
                    {
                        // 既没中句柄也没中边框 -> 恢复默认
                        Mouse.OverrideCursor = null;
                    }
                }

                // 2️⃣ 具体的交互逻辑
                if (_textBox != null)
                {
                    double dx = px.X - _startMouse.X;
                    double dy = px.Y - _startMouse.Y;

                    // A. 处理调整大小 (Resizing)
                    if (_resizing)
                    {
                        double rightEdge = _startX + _startW;
                        double bottomEdge = _startY + _startH;
                        switch (_currentAnchor)
                        {
                            case ResizeAnchor.TopLeft:
                                {
                                    double newW = Math.Max(1, _startW - dx);
                                    _textBox.Width = newW;
                                    Canvas.SetLeft(_textBox, rightEdge - newW);
                                    double newH = Math.Max(1, _startH - dy);
                                    _textBox.Height = newH;
                                    Canvas.SetTop(_textBox, bottomEdge - newH);
                                }
                                break;

                            case ResizeAnchor.TopMiddle:
                                {
                                    double newH = Math.Max(1, _startH - dy);
                                    _textBox.Height = newH;
                                    Canvas.SetTop(_textBox, bottomEdge - newH);
                                }
                                break;

                            case ResizeAnchor.TopRight:
                                {
                                    _textBox.Width = Math.Max(1, _startW + dx);
                                    double newH = Math.Max(1, _startH - dy);
                                    _textBox.Height = newH;
                                    Canvas.SetTop(_textBox, bottomEdge - newH);
                                }
                                break;

                            case ResizeAnchor.LeftMiddle:
                                {
                                    double newW = Math.Max(1, _startW - dx);
                                    _textBox.Width = newW;
                                    Canvas.SetLeft(_textBox, rightEdge - newW);
                                }
                                break;

                            case ResizeAnchor.RightMiddle:
                                _textBox.Width = Math.Max(1, _startW + dx);
                                break;

                            case ResizeAnchor.BottomLeft:
                                {
                                    double newW = Math.Max(1, _startW - dx);
                                    _textBox.Width = newW;
                                    Canvas.SetLeft(_textBox, rightEdge - newW);
                                    _textBox.Height = Math.Max(1, _startH + dy);
                                }
                                break;

                            case ResizeAnchor.BottomMiddle:
                                _textBox.Height = Math.Max(1, _startH + dy);
                                break;

                            case ResizeAnchor.BottomRight:
                                _textBox.Width = Math.Max(1, _startW + dx);
                                _textBox.Height = Math.Max(1, _startH + dy);
                                break;
                        }
                        DrawTextboxOverlay(ctx); // 实时重绘边框
                    }
                    else if (_dragging)
                    {
                        // 移动 TextBox
                        Canvas.SetLeft(_textBox, _startX + dx);
                        Canvas.SetTop(_textBox, _startY + dy);

                        // 实时重绘边框跟随移动
                        DrawTextboxOverlay(ctx);
                    }
                }
            }



            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                MainWindow mw = ((MainWindow)System.Windows.Application.Current.MainWindow);
                if (mw.IsViewMode) return;
                if (_textBox != null)
                {
                    Point p = viewPos;
                    double left = Canvas.GetLeft(_textBox);
                    double top = Canvas.GetTop(_textBox);

                    bool inside = p.X >= left && p.X <= left + _textBox.ActualWidth &&
                                  p.Y >= top && p.Y <= top + _textBox.ActualHeight;

                    if (inside)
                    {
                        // 点击内部 → 选中并进入编辑
                        ctx.EditorOverlay.IsHitTestVisible = true;
                        SelectCurrentBox();
                        return;
                    }
                    else
                    {
                        CommitText(ctx);
                        DeselectCurrentBox(ctx); if (mw._canvasResizer != null) mw._canvasResizer.SetHandleVisibility(true);
                        ctx.EditorOverlay.IsHitTestVisible = false;
                        return;
                    }
                }
                else
                {
                    // 没有编辑框 → 记录起点
                    _startPos = viewPos;
                    _dragging = true;
                }

            }
            private bool IsInsideBorder(Point px)
            {
                if (_textBox == null) return false;

                double x = Canvas.GetLeft(_textBox);
                double y = Canvas.GetTop(_textBox);
                double w = _textBox.ActualWidth;
                double h = _textBox.ActualHeight;
                double borderThickness = Math.Max(5 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale, 10);

                // 外矩形 (扩大边框宽度)
                bool inOuter = px.X >= x - borderThickness &&
                               px.X <= x + w + borderThickness &&
                               px.Y >= y - borderThickness &&
                               px.Y <= y + h + borderThickness;

                // 内矩形 (缩小边框宽度)
                bool inInner = px.X >= x + borderThickness &&
                               px.X <= x + w - borderThickness &&
                               px.Y >= y + borderThickness &&
                               px.Y <= y + h - borderThickness;
                // 必须在外矩形内 && 不在内矩形内 → 才是边框区域
                return inOuter && !inInner;
            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos)
            {
                MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (mw._router.CurrentTool != mw._tools.Text) return;
                if (_resizing || (_dragging && _textBox != null))
                {
                    _resizing = false;
                    _dragging = false;
                    _currentAnchor = ResizeAnchor.None;

                    // 释放鼠标捕获，这样下次点击才能正常工作
                    ctx.EditorOverlay.ReleaseMouseCapture();
                  
                    // 既然是拖动结束，就不需要执行下面的创建逻辑了，直接返回
                    return;
                }
                if (_dragging && _textBox == null)
                {//创建新的文本框

                    if (lag > 0)
                    {
                        lag -= 1;
                        return;
                    }
                    _dragging = false;

                    _textBox = CreateTextBox(ctx, _startPos.X, _startPos.Y);
                    _textBox.Width = 500;
                    _textBox.MinHeight = 20;
                    _textBox.Height = Double.NaN;
                    // ⬇️ 通知主窗口显示状态栏

                    ctx.EditorOverlay.Visibility = Visibility.Visible;
                    ctx.EditorOverlay.IsHitTestVisible = true;
                    Canvas.SetZIndex(ctx.EditorOverlay, 999);
                    ctx.EditorOverlay.Children.Add(_textBox);


                    ((MainWindow)System.Windows.Application.Current.MainWindow).ShowTextToolbarFor(_textBox);


                    // 绘制虚线框和8个句柄 ⚡⚡
                    _textBox.Loaded += (s, e) =>
                    {
                        DrawTextboxOverlay(ctx); // 已布局完成
                    };

                    ctx.EditorOverlay.PreviewMouseUp += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay);
                        OnPointerUp(ctx, pos);
                    };


                    ctx.EditorOverlay.PreviewMouseMove += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay);
                        OnPointerMove(ctx, pos);
                    };
                    ctx.EditorOverlay.PreviewMouseDown += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay); // 获取当前点击在 Overlay 上的位置
                        Point pixelPos = ctx.ToPixel(pos);            // 转为画布像素坐标

                        var anchor = HitTestTextboxHandle(pixelPos);

                        // 1. 命中句柄 -> 缩放模式
                        if (anchor != ResizeAnchor.None)
                        {
                            _resizing = true;
                            _currentAnchor = anchor;
                            _startMouse = pixelPos;             // 记录当前鼠标位置
                            _startW = _textBox.ActualWidth;
                            _startH = _textBox.ActualHeight;
                            _startX = Canvas.GetLeft(_textBox);
                            _startY = Canvas.GetTop(_textBox);

                            ctx.EditorOverlay.CaptureMouse();   
                            e.Handled = true;
                        }
                        // 2. 命中虚线边框 -> 移动模式
                        else if (IsInsideBorder(pixelPos))
                        {
                            _dragging = true;
                            _startMouse = pixelPos;         
                            _startX = Canvas.GetLeft(_textBox); // 记录当前文本框位置
                            _startY = Canvas.GetTop(_textBox);

                            ctx.EditorOverlay.CaptureMouse(); 
                            e.Handled = true;                   // 防止事件传给 TextBox 导致光标闪烁
                        }
                        else
                        {
                            OnPointerDown(ctx, pos);
                        }
                    };

                    _textBox.PreviewKeyDown += (s, e) =>
                    {
                        if (e.Key == Key.Delete)
                        {
                            CommitText(ctx);
                            ctx.EditorOverlay.Children.Remove(_textBox);
                            _textBox = null;
                            ctx.EditorOverlay.IsHitTestVisible = false;
                            e.Handled = true;
                        }
                    };

                    _textBox.Focusable = true;
                    _textBox.Loaded += (s, e) => _textBox.Focus();
                }
            }
            private void SelectCurrentBox()
            {
                if (_textBox != null)
                {
                    Keyboard.Focus(_textBox);
                    _textBox.Focus();
                }
            }

            private void DeselectCurrentBox(ToolContext ctx)
            {
                if (_textBox != null)
                {
                    ctx.EditorOverlay.Children.Remove(_textBox);
                    _textBox = null;
                }
            }
            private System.Windows.Controls.TextBox CreateTextBox(ToolContext ctx, double x, double y)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

                var tb = new System.Windows.Controls.TextBox
                {
                    MaxLength = 100000,
                    FontSize = 24, // 默认值，会被 ApplyTextSettings 覆盖
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(ctx.PenColor),
                    Opacity = TabPaint.SettingsManager.Instance.Current.PenOpacity,
                    // 初始透明
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Padding = new Thickness(5) // 给一点内边距好看
                };

                Canvas.SetLeft(tb, x);
                Canvas.SetTop(tb, y);

                // 立即应用当前工具栏的设置
                ApplyTextSettings(tb);

                return tb;
            }
            public void ApplyTextSettings(System.Windows.Controls.TextBox tb)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (tb == null) return;

                // 1. 字体与大小
                if (mw.FontFamilyBox.SelectedValue != null)
                    tb.FontFamily = new FontFamily(mw.FontFamilyBox.SelectedValue.ToString());

                if (double.TryParse(mw.FontSizeBox.Text, out double size))
                    tb.FontSize = Math.Max(1, size);

                // 2. 粗体/斜体
                tb.FontWeight = (mw.BoldBtn.IsChecked == true) ? FontWeights.Bold : FontWeights.Normal;
                tb.FontStyle = (mw.ItalicBtn.IsChecked == true) ? FontStyles.Italic : FontStyles.Normal;

                // 3. 装饰线 (下划线 + 删除线)
                var decors = new TextDecorationCollection();
                if (mw.UnderlineBtn.IsChecked == true) decors.Add(TextDecorations.Underline);
                if (mw.StrikeBtn.IsChecked == true) decors.Add(TextDecorations.Strikethrough);
                tb.TextDecorations = decors;

                // 4. 对齐
                if (mw.AlignLeftBtn.IsChecked == true) tb.TextAlignment = TextAlignment.Left;
                else if (mw.AlignCenterBtn.IsChecked == true) tb.TextAlignment = TextAlignment.Center;
                else if (mw.AlignRightBtn.IsChecked == true) tb.TextAlignment = TextAlignment.Right;
                tb.Foreground = mw.SelectedBrush;
                if (mw.TextBackgroundBtn.IsChecked == true)
                    tb.Background = mw.BackgroundBrush;
                else
                    tb.Background = Brushes.Transparent;
            }
            public void UpdateCurrentTextBoxAttributes()
            {
                if (_textBox == null) return;

                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                ApplyTextSettings(_textBox);

                _textBox.UpdateLayout();
                DrawTextboxOverlay(mw._ctx);
            }
            private void CleanUpUI(ToolContext ctx)
            {
                MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                mw.HideTextToolbar();
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                if (ctx.EditorOverlay.Children.Contains(_textBox))
                    ctx.EditorOverlay.Children.Remove(_textBox);

                mw.SetUndoRedoButtonState();
                _textBox = null;
                lag = 1;
                if (mw._canvasResizer != null) mw._canvasResizer.SetHandleVisibility(false);
            }
            private unsafe void AlphaBlendBatch(byte[] sourcePixels, byte[] destPixels, int width, int height, int stride, int sourceStartIdx, double globalOpacity)
            {
                int opacityScale = (int)(globalOpacity * 255);

                if (opacityScale <= 0) return;

                fixed (byte* pSrcBase = sourcePixels)
                fixed (byte* pDstBase = destPixels)
                {
                    for (int row = 0; row < height; row++)
                    {
                        byte* pSrcRow = pSrcBase + sourceStartIdx + (row * stride);
                        byte* pDstRow = pDstBase + (row * stride);

                        for (int col = 0; col < width; col++)
                        {
                            byte rawSrcA = pSrcRow[3];
                            if (rawSrcA == 0)
                            {
                                pSrcRow += 4;
                                pDstRow += 4;
                                continue;
                            }

                            int srcA, srcR, srcG, srcB;

                            if (opacityScale == 255)
                            {
                                // 满不透明度，直接读取
                                srcB = pSrcRow[0];
                                srcG = pSrcRow[1];
                                srcR = pSrcRow[2];
                                srcA = rawSrcA;
                            }
                            else
                            {
                                srcB = (pSrcRow[0] * opacityScale) / 255;
                                srcG = (pSrcRow[1] * opacityScale) / 255;
                                srcR = (pSrcRow[2] * opacityScale) / 255;
                                srcA = (rawSrcA * opacityScale) / 255;
                            }

                            if (srcA == 0)
                            {
                                pSrcRow += 4;
                                pDstRow += 4;
                                continue;
                            }

                            if (srcA == 255)
                            {
                                pDstRow[0] = (byte)srcB;
                                pDstRow[1] = (byte)srcG;
                                pDstRow[2] = (byte)srcR;
                                pDstRow[3] = 255; // 目标 alpha 变成 255
                            }
                            else
                            {
                                // Alpha 混合: Out = Src + Dst * (1 - SrcA)
                                int invAlpha = 255 - srcA;

                                pDstRow[0] = (byte)(srcB + (pDstRow[0] * invAlpha) / 255); // B
                                pDstRow[1] = (byte)(srcG + (pDstRow[1] * invAlpha) / 255); // G
                                pDstRow[2] = (byte)(srcR + (pDstRow[2] * invAlpha) / 255); // R
                                pDstRow[3] = (byte)(srcA + (pDstRow[3] * invAlpha) / 255); // A
                            }

                            pSrcRow += 4;
                            pDstRow += 4;
                        }
                    }
                }
            }
            // 1. 新增：将事件绑定逻辑提取为独立方法，避免重复代码
            private void SetupTextBoxEvents(ToolContext ctx, System.Windows.Controls.TextBox tb)
            {
                // 绘制虚线框和句柄
                tb.Loaded += (s, e) => { DrawTextboxOverlay(ctx); };

                tb.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Delete)
                    {
                        CommitText(ctx);
                        ctx.EditorOverlay.Children.Remove(tb);
                        _textBox = null;
                        ctx.EditorOverlay.IsHitTestVisible = false;
                        e.Handled = true;
                    }
                };

                tb.Focusable = true;
                tb.Loaded += (s, e) => tb.Focus();
            }

            // 2. 新增：公共接口，用于外部调用（粘贴/拖拽）
            public void SpawnTextBox(ToolContext ctx, Point viewPos, string text)
            {
                _dragging = false;
                _resizing = false;
                if (ctx.EditorOverlay.IsMouseCaptured) ctx.EditorOverlay.ReleaseMouseCapture();

                if (_textBox != null) CommitText(ctx);
                Point px = ctx.ToPixel(viewPos);
                _textBox = CreateTextBox(ctx, px.X, px.Y);
                _textBox.Text = text; // 填入文字

                _textBox.MaxWidth = 1000;
                _textBox.Width = Double.NaN; // 让宽度自适应内容
                _textBox.Height = Double.NaN;

                // 显示 UI
                ctx.EditorOverlay.Visibility = Visibility.Visible;
                ctx.EditorOverlay.IsHitTestVisible = true;
                Canvas.SetZIndex(ctx.EditorOverlay, 999);
                ctx.EditorOverlay.Children.Add(_textBox);

                ((MainWindow)System.Windows.Application.Current.MainWindow).ShowTextToolbarFor(_textBox);

                // 绑定关键事件（原本写在 OnPointerUp 里的那一大段）
                SetupTextBoxEvents(ctx, _textBox);

                ctx.EditorOverlay.PreviewMouseUp -= Overlay_PreviewMouseUp; // 防止重复订阅
                ctx.EditorOverlay.PreviewMouseUp += Overlay_PreviewMouseUp;

                ctx.EditorOverlay.PreviewMouseMove -= Overlay_PreviewMouseMove;
                ctx.EditorOverlay.PreviewMouseMove += Overlay_PreviewMouseMove;

                ctx.EditorOverlay.PreviewMouseDown -= Overlay_PreviewMouseDown;
                ctx.EditorOverlay.PreviewMouseDown += Overlay_PreviewMouseDown;

                _textBox.UpdateLayout();
                DrawTextboxOverlay(ctx);
            }
            private void Overlay_PreviewMouseUp(object sender, MouseButtonEventArgs e)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                Point pos = e.GetPosition(mw._ctx.EditorOverlay);
                OnPointerUp(mw._ctx, pos);
            }

            private void Overlay_PreviewMouseMove(object sender, MouseEventArgs e)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                Point pos = e.GetPosition(mw._ctx.EditorOverlay);
                OnPointerMove(mw._ctx, pos);
            }

            private void Overlay_PreviewMouseDown(object sender, MouseButtonEventArgs e)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                var ctx = mw._ctx;

                Point pos = e.GetPosition(ctx.EditorOverlay);
                Point pixelPos = ctx.ToPixel(pos);

                var anchor = HitTestTextboxHandle(pixelPos);

                if (anchor != ResizeAnchor.None)
                {
                    _resizing = true;
                    _currentAnchor = anchor;
                    _startMouse = pixelPos;
                    _startW = _textBox.ActualWidth;
                    _startH = _textBox.ActualHeight;
                    _startX = Canvas.GetLeft(_textBox);
                    _startY = Canvas.GetTop(_textBox);
                    ctx.EditorOverlay.CaptureMouse();
                    e.Handled = true;
                }
                else if (IsInsideBorder(pixelPos))
                {
                    _dragging = true;
                    _startMouse = pixelPos;
                    _startX = Canvas.GetLeft(_textBox);
                    _startY = Canvas.GetTop(_textBox);
                    ctx.EditorOverlay.CaptureMouse();
                    e.Handled = true;
                }
                else
                {
                    OnPointerDown(ctx, pos);
                }
            }

            public void CommitText(ToolContext ctx)
            {
                if (_textBox == null) return;
                if (string.IsNullOrWhiteSpace(_textBox.Text))
                {
                    ((MainWindow)System.Windows.Application.Current.MainWindow).HideTextToolbar();
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                    if (ctx.EditorOverlay.Children.Contains(_textBox))
                        ctx.EditorOverlay.Children.Remove(_textBox);
                    lag = 2;
                    return;
                }
                double tweakX = 2.0;
                double tweakY = 1.0;
                // 1. 获取位置信息
                double tbLeft = Canvas.GetLeft(_textBox);
                double tbTop = Canvas.GetTop(_textBox);
                double tbWidth = _textBox.ActualWidth;
                double tbHeight = _textBox.ActualHeight;
                var formattedText = new FormattedText(
                    _textBox.Text,
                    CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface(_textBox.FontFamily, _textBox.FontStyle, _textBox.FontWeight, _textBox.FontStretch),
                    _textBox.FontSize,
                    _textBox.Foreground,
                    96.0 // 强制 96 DPI，确保像素大小与逻辑大小 1:1
                )
                {
                    MaxTextWidth = Math.Max(1, tbWidth - _textBox.Padding.Left - _textBox.Padding.Right),
                    MaxTextHeight = double.MaxValue,
                    Trimming = TextTrimming.None,
                    TextAlignment = _textBox.TextAlignment
                };
                formattedText.SetTextDecorations(_textBox.TextDecorations);

                // 3. 渲染到 Visual
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    // 如果文本框有背景色，画背景
                    if (_textBox.Background is SolidColorBrush bgBrush && bgBrush.Color.A > 0)
                    {
                        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, tbWidth, tbHeight));
                    }

                    // 使用 Grayscale 渲染文本，避免 ClearType 在透明背景上产生彩色边缘
                    TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
                    TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Display);

                    dc.DrawText(formattedText, new Point(_textBox.Padding.Left + tweakX, _textBox.Padding.Top + tweakY));
                }

                int width = (int)Math.Ceiling(tbWidth);
                int height = (int)Math.Ceiling(tbHeight);
                if (width <= 0 || height <= 0) return;
                var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
                rtb.Render(visual);

                int stride = width * 4;
                byte[] sourcePixels = new byte[height * stride];
                rtb.CopyPixels(sourcePixels, stride, 0);
                int x = (int)tbLeft;
                int y = (int)tbTop;

                var writeableBitmap = ctx.Surface.Bitmap; 
                int canvasWidth = writeableBitmap.PixelWidth;
                int canvasHeight = writeableBitmap.PixelHeight;

                // 计算实际可操作的矩形区域 (Clip)
                int safeX = Math.Max(0, x);
                int safeY = Math.Max(0, y);
                int safeRight = Math.Min(canvasWidth, x + width);
                int safeBottom = Math.Min(canvasHeight, y + height);
                int safeW = safeRight - safeX;
                int safeH = safeBottom - safeY;

                if (safeW <= 0 || safeH <= 0)
                {
                    CleanUpUI(ctx);
                    return;
                }
                byte[] destPixels = new byte[safeH * stride];
                Int32Rect dirtyRect = new Int32Rect(safeX, safeY, safeW, safeH);
                writeableBitmap.CopyPixels(dirtyRect, destPixels, stride, 0);

                int sourceOffsetX = safeX - x;
                int sourceOffsetY = safeY - y;
                int sourceStartIndex = sourceOffsetY * stride + sourceOffsetX * 4;

                double globalOpacityFactor = TabPaint.SettingsManager.Instance.Current.PenOpacity;
                AlphaBlendBatch(sourcePixels, destPixels, safeW, safeH, stride, sourceStartIndex, globalOpacityFactor);

                // 8. 写回混合后的结果
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(dirtyRect);
                writeableBitmap.WritePixels(dirtyRect, destPixels, stride, 0);
                ctx.Undo.CommitStroke();

                // UI 清理
                CleanUpUI(ctx);
                lag = 1;
            }


        }
    }
}