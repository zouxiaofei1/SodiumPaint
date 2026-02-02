
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
               // if (_richTextBox != null && !string.IsNullOrWhiteSpace(_richTextBox.Text)) CommitText(ctx);

                if (_richTextBox != null && ctx.EditorOverlay.Children.Contains(_richTextBox))
                {
                    ctx.EditorOverlay.Children.Remove(_richTextBox);
                    _richTextBox = null;
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
              //  bool hastext = (_richTextBox != null && !string.IsNullOrWhiteSpace(_richTextBox.Text));
                Cleanup(ctx);
                //if (hastext)
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
                if (_richTextBox == null) return;

                double invScale = 1 / mw.zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.Children.Clear();

                double x = Canvas.GetLeft(_richTextBox);
                double y = Canvas.GetTop(_richTextBox);
                double w = _richTextBox.ActualWidth;
                double h = _richTextBox.ActualHeight;
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
                if (_richTextBox == null) return ResizeAnchor.None;
                double size = 12 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                double x1 = Canvas.GetLeft(_richTextBox);
                double y1 = Canvas.GetTop(_richTextBox);
                double x2 = x1 + _richTextBox.ActualWidth;
                double y2 = y1 + _richTextBox.ActualHeight;
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

            public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
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
                if (_richTextBox != null && !_resizing && !_dragging) // 如果没有在操作中，才检测光标
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
                if (_richTextBox != null)
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
                                    _richTextBox.Width = newW;
                                    Canvas.SetLeft(_richTextBox, rightEdge - newW);
                                    double newH = Math.Max(1, _startH - dy);
                                    _richTextBox.Height = newH;
                                    Canvas.SetTop(_richTextBox, bottomEdge - newH);
                                }
                                break;

                            case ResizeAnchor.TopMiddle:
                                {
                                    double newH = Math.Max(1, _startH - dy);
                                    _richTextBox.Height = newH;
                                    Canvas.SetTop(_richTextBox, bottomEdge - newH);
                                }
                                break;

                            case ResizeAnchor.TopRight:
                                {
                                    _richTextBox.Width = Math.Max(1, _startW + dx);
                                    double newH = Math.Max(1, _startH - dy);
                                    _richTextBox.Height = newH;
                                    Canvas.SetTop(_richTextBox, bottomEdge - newH);
                                }
                                break;

                            case ResizeAnchor.LeftMiddle:
                                {
                                    double newW = Math.Max(1, _startW - dx);
                                    _richTextBox.Width = newW;
                                    Canvas.SetLeft(_richTextBox, rightEdge - newW);
                                }
                                break;

                            case ResizeAnchor.RightMiddle:
                                _richTextBox.Width = Math.Max(1, _startW + dx);
                                break;

                            case ResizeAnchor.BottomLeft:
                                {
                                    double newW = Math.Max(1, _startW - dx);
                                    _richTextBox.Width = newW;
                                    Canvas.SetLeft(_richTextBox, rightEdge - newW);
                                    _richTextBox.Height = Math.Max(1, _startH + dy);
                                }
                                break;

                            case ResizeAnchor.BottomMiddle:
                                _richTextBox.Height = Math.Max(1, _startH + dy);
                                break;

                            case ResizeAnchor.BottomRight:
                                _richTextBox.Width = Math.Max(1, _startW + dx);
                                _richTextBox.Height = Math.Max(1, _startH + dy);
                                break;
                        }
                        DrawTextboxOverlay(ctx); // 实时重绘边框
                    }
                    else if (_dragging)
                    {
                        // 移动 TextBox
                        Canvas.SetLeft(_richTextBox, _startX + dx);
                        Canvas.SetTop(_richTextBox, _startY + dy);

                        // 实时重绘边框跟随移动
                        DrawTextboxOverlay(ctx);
                    }
                }
            }



            public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                MainWindow mw = ((MainWindow)System.Windows.Application.Current.MainWindow);
                if (mw.IsViewMode) return;

                // 如果文本框存在，优先检测交互逻辑
                if (_richTextBox != null)
                {
                    Point pixelPos = ctx.ToPixel(viewPos); // 转换为像素坐标用于检测

                    // 1. 检测是否点击了【调整手柄】 (Resize)
                    var anchor = HitTestTextboxHandle(pixelPos);
                    if (anchor != ResizeAnchor.None)
                    {
                        _resizing = true;
                        _currentAnchor = anchor;
                        _startMouse = pixelPos; // 记录鼠标像素位置
                        _startW = _richTextBox.ActualWidth;
                        _startH = _richTextBox.ActualHeight;
                        _startX = Canvas.GetLeft(_richTextBox);
                        _startY = Canvas.GetTop(_richTextBox);

                        // 捕获鼠标以保证拖动流畅
                        if (ctx.EditorOverlay.IsHitTestVisible)
                            ctx.EditorOverlay.CaptureMouse();
                        return;
                    }

                    // 2. 检测是否点击了【边框区域】 (Move / Drag)
                    if (IsInsideBorder(pixelPos))
                    {
                        _dragging = true;
                        _startMouse = pixelPos;
                        _startX = Canvas.GetLeft(_richTextBox);
                        _startY = Canvas.GetTop(_richTextBox);

                        if (ctx.EditorOverlay.IsHitTestVisible)
                            ctx.EditorOverlay.CaptureMouse();
                        return;
                    }

                    // 3. 检测是否点击了【文本框内部】 (Edit / Focus)
                    double left = Canvas.GetLeft(_richTextBox);
                    double top = Canvas.GetTop(_richTextBox);
                    // 注意：这里用 viewPos 或 pixelPos 需保持一致，建议统一用 pixelPos 对比 Canvas 坐标
                    bool inside = pixelPos.X >= left && pixelPos.X <= left + _richTextBox.ActualWidth &&
                                  pixelPos.Y >= top && pixelPos.Y <= top + _richTextBox.ActualHeight;

                    if (inside)
                    {
                        // 点击内部 → 选中并进入编辑
                        ctx.EditorOverlay.IsHitTestVisible = true;
                        SelectCurrentBox();
                        return;
                    }
                    else
                    {
                        // 4. 点击了【完全外部】 → 提交当前文本，准备创建新文本
                        CommitText(ctx);
                        // 只有当文本框确实被 Commit 销毁了，才继续下面的创建逻辑
                        if (_richTextBox == null)
                        {
                            // 如果需要点击外部立即创建新框，可以在这里记录起点
                            // 否则直接返回，等待下一次点击
                            _startPos = viewPos;
                            _dragging = true; // 这里的 dragging 是指“拖拽创建新框”
                        }
                        return;
                    }
                }
                else
                {
                    // 没有编辑框 → 记录起点，准备创建新框
                    _startPos = viewPos;
                    _dragging = true;
                }
            }
            private bool HasImagesOrTables(System.Windows.Controls.RichTextBox rtb)
            {
                // 简单遍历 Block 检查是否有 Table 或 BlockUIContainer
                foreach (var block in rtb.Document.Blocks)
                {
                    if (block is Table || block is BlockUIContainer) return true;
                }
                return false;
            }

            private bool IsInsideBorder(Point px)
            {
                if (_richTextBox == null) return false;

                double x = Canvas.GetLeft(_richTextBox);
                double y = Canvas.GetTop(_richTextBox);
                double w = _richTextBox.ActualWidth;
                double h = _richTextBox.ActualHeight;
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

            public override void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (mw._router.CurrentTool != mw._tools.Text) return;
                if (_resizing || (_dragging && _richTextBox != null))
                {
                    _resizing = false;
                    _dragging = false;
                    _currentAnchor = ResizeAnchor.None;

                    // 释放鼠标捕获，这样下次点击才能正常工作
                    ctx.EditorOverlay.ReleaseMouseCapture();
                  
                    // 既然是拖动结束，就不需要执行下面的创建逻辑了，直接返回
                    return;
                }
                if (_dragging && _richTextBox == null)
                {
                    if (lag > 0) { lag -= 1; return; }
                    _dragging = false;

                    _richTextBox = CreateRichTextBox(ctx, _startPos.X, _startPos.Y);
                    _richTextBox.Width = 500;
                    _richTextBox.MinHeight = 50;

                    // 重要：将事件绑定到 RichTextBox
                    SetupRichTextBoxEvents(ctx, _richTextBox);

                    ctx.EditorOverlay.Visibility = Visibility.Visible;
                    ctx.EditorOverlay.IsHitTestVisible = true;
                    Canvas.SetZIndex(ctx.EditorOverlay, 999);
                    ctx.EditorOverlay.Children.Add(_richTextBox);

                    ((MainWindow)System.Windows.Application.Current.MainWindow).ShowTextToolbarFor(_richTextBox);

                    _richTextBox.Focus();
                }
            }
            private void SetupRichTextBoxEvents(ToolContext ctx, System.Windows.Controls.RichTextBox rtb)
            {
                rtb.Loaded += (s, e) => { DrawTextboxOverlay(ctx); rtb.Focus(); };

                // 只有当没有选中文本时，Delete 键才删除框
                rtb.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Delete && rtb.Selection.IsEmpty && new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text.Trim() == "")
                    {
                        // 逻辑：删除空框
                        CleanUpUI(ctx);
                        e.Handled = true;
                    }
                };

                // 当内容变化时，可能需要更新选区框大小（如果我们要自适应高度）
                rtb.TextChanged += (s, e) =>
                {
                    // 这里可以添加高度自适应逻辑
                };
            }

            // 6. 核心：重写 CommitText (渲染位图)
            public void CommitText(ToolContext ctx)
            {
                if (_richTextBox == null) return;

                // ---------------------------------------------------------
                // 1. 修复光标残留问题
                // ---------------------------------------------------------
                // 将光标颜色设为透明
                _richTextBox.CaretBrush = Brushes.Transparent;
                // 清空选区（防止蓝色的选中背景被画进去）
                var end = _richTextBox.Document.ContentEnd;
                _richTextBox.Selection.Select(end, end);
                // 禁止获取焦点
                _richTextBox.Focusable = false;
                _richTextBox.IsReadOnly = true;

                // 强制刷新布局，确保光标隐藏的状态被更新到视觉树上
                _richTextBox.UpdateLayout();

                // 检查是否有内容 (防止空框提交)
                string plainText = new TextRange(_richTextBox.Document.ContentStart, _richTextBox.Document.ContentEnd).Text;
                if (string.IsNullOrWhiteSpace(plainText) && !HasImagesOrTables(_richTextBox))
                {
                    CleanUpUI(ctx);
                    lag = 2;
                    return;
                }

                // 获取参数
                double canvasLeft = Canvas.GetLeft(_richTextBox);
                double canvasTop = Canvas.GetTop(_richTextBox);
                int width = (int)Math.Ceiling(_richTextBox.ActualWidth);
                int height = (int)Math.Ceiling(_richTextBox.ActualHeight);

                // 安全检查
                if (width <= 0 || height <= 0) { CleanUpUI(ctx); lag = 2; return; }

                try
                {
                    // === 修改开始：使用 DrawingVisual 修正偏移 ===
                    var rtbBitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);

                    var drawingVisual = new DrawingVisual();
                    using (var context = drawingVisual.RenderOpen())
                    {
                        var brush = new VisualBrush(_richTextBox)
                        {
                            Stretch = Stretch.None,
                            TileMode = TileMode.None,
                            AlignmentX = AlignmentX.Left,
                            AlignmentY = AlignmentY.Top
                        };
                        context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
                    }
                    rtbBitmap.Render(drawingVisual);
                    // === 修改结束 ===

                    // 4. 计算“交集区域” (Intersection)
                    int canvasW = ctx.Surface.Bitmap.PixelWidth;
                    int canvasH = ctx.Surface.Bitmap.PixelHeight;

                    // 计算目标矩形 (Canvas 坐标系)
                    int destX = (int)Math.Max(0, canvasLeft);
                    int destY = (int)Math.Max(0, canvasTop);
                    int destRight = (int)Math.Min(canvasW, canvasLeft + width);
                    int destBottom = (int)Math.Min(canvasH, canvasTop + height);

                    int drawW = destRight - destX;
                    int drawH = destBottom - destY;

                    if (drawW <= 0 || drawH <= 0)
                    {
                        CleanUpUI(ctx);
                        lag = 2;
                        return;
                    }

                    int srcX = (int)(destX - canvasLeft);
                    int srcY = (int)(destY - canvasTop);

                    // 5. 提取像素
                    int stride = drawW * 4;
                    int bufferSize = drawH * stride;

                    byte[] sourcePixels = new byte[bufferSize];
                    byte[] destPixels = new byte[bufferSize];

                    // 从 RTB 读取 (此时 rtbBitmap 已经是修正后的，(0,0)就是文字开始的地方)
                    rtbBitmap.CopyPixels(new Int32Rect(srcX, srcY, drawW, drawH), sourcePixels, stride, 0);

                    // 从 Canvas 读取
                    var writeableBitmap = ctx.Surface.Bitmap;
                    Int32Rect dirtyRect = new Int32Rect(destX, destY, drawW, drawH);
                    writeableBitmap.CopyPixels(dirtyRect, destPixels, stride, 0);

                    // 6. 混合
                    double globalOpacityFactor = _richTextBox.Opacity;
                    AlphaBlendBatch(sourcePixels, destPixels, drawW, drawH, stride, 0, globalOpacityFactor);

                    // 7. 写回 Canvas
                    ctx.Undo.BeginStroke();
                    ctx.Undo.AddDirtyRect(dirtyRect);
                    writeableBitmap.WritePixels(dirtyRect, destPixels, stride, 0);
                    ctx.Undo.CommitStroke();
                }

                catch (Exception ex)
                {
                    Debug.WriteLine("CommitText Error: " + ex.Message);
                }
                finally
                {
                    CleanUpUI(ctx);
                    lag = 2;
                }
            }


            private void SelectCurrentBox()
            {
                if (_richTextBox != null)
                {
                    Keyboard.Focus(_richTextBox);
                    _richTextBox.Focus();
                }
            }

            private void DeselectCurrentBox(ToolContext ctx)
            {
                if (_richTextBox != null)
                {
                    ctx.EditorOverlay.Children.Remove(_richTextBox);
                    _richTextBox = null;
                }
            }
            private System.Windows.Controls.RichTextBox CreateRichTextBox(ToolContext ctx, double x, double y)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

                var rtb = new System.Windows.Controls.RichTextBox
                {
                    FontSize = 24,
                    Foreground = new SolidColorBrush(ctx.PenColor),
                    Opacity = TabPaint.SettingsManager.Instance.Current.PenOpacity,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent, // 必须透明
                    Padding = new Thickness(5),
                    AcceptsReturn = true,
                    AcceptsTab = true, // 允许制表符
                                       // 关键：FlowDocument 设置
                    Document = new FlowDocument()
                    {
                        PagePadding = new Thickness(0), // 去除文档默认边距
                        LineHeight = 1, // 防止行距过大
                    }
                };

                // 移除原有的 TextWrapping 属性，FlowDocument 默认会自动换行，
                // 但我们需要根据宽度调整。设置 PageWidth 可以强制换行，或者让它自适应。
                rtb.Document.TextAlignment = TextAlignment.Left;

                Canvas.SetLeft(rtb, x);
                Canvas.SetTop(rtb, y);

                // 应用初始设置
                ApplyTextSettings(rtb);

                return rtb;
            }
            public void ApplyTextSettings(System.Windows.Controls.RichTextBox tb)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (tb == null) return;

                // 1. 字体与大小 (这部分 RichTextBox 支持直接设置，会继承给内部元素)
                if (mw.FontFamilyBox.SelectedValue != null)
                    tb.FontFamily = new FontFamily(mw.FontFamilyBox.SelectedValue.ToString());

                if (double.TryParse(mw.FontSizeBox.Text, out double size))
                    tb.FontSize = Math.Max(1, size);

                // 2. 粗体/斜体
                tb.FontWeight = (mw.BoldBtn.IsChecked == true) ? FontWeights.Bold : FontWeights.Normal;
                tb.FontStyle = (mw.ItalicBtn.IsChecked == true) ? FontStyles.Italic : FontStyles.Normal;

                // 3. 装饰线 (下划线 + 删除线) - 需要作用于 TextRange ✨
                var decors = new TextDecorationCollection();
                if (mw.UnderlineBtn.IsChecked == true) decors.Add(TextDecorations.Underline);
                if (mw.StrikeBtn.IsChecked == true) decors.Add(TextDecorations.Strikethrough);

                // 获取整个文档的范围并应用装饰线
                TextRange allText = new TextRange(tb.Document.ContentStart, tb.Document.ContentEnd);
                allText.ApplyPropertyValue(Inline.TextDecorationsProperty, decors);

                // 4. 对齐 - 作用于 Document ✨
                if (mw.AlignLeftBtn.IsChecked == true) tb.Document.TextAlignment = TextAlignment.Left;
                else if (mw.AlignCenterBtn.IsChecked == true) tb.Document.TextAlignment = TextAlignment.Center;
                else if (mw.AlignRightBtn.IsChecked == true) tb.Document.TextAlignment = TextAlignment.Right;

                // 5. 颜色与背景
                tb.Foreground = mw.SelectedBrush;
                if (mw.TextBackgroundBtn.IsChecked == true)
                    tb.Background = mw.BackgroundBrush;
                else
                    tb.Background = Brushes.Transparent;
            }

            public void UpdateCurrentTextBoxAttributes()
            {
                if (_richTextBox == null) return;

                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                ApplyTextSettings(_richTextBox);

                _richTextBox.UpdateLayout();
                DrawTextboxOverlay(mw._ctx);
            }
            private void CleanUpUI(ToolContext ctx)
            {
                MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                mw.HideTextToolbar();
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                if (ctx.EditorOverlay.Children.Contains(_richTextBox))
                    ctx.EditorOverlay.Children.Remove(_richTextBox);

                mw.SetUndoRedoButtonState();
                _richTextBox = null;
                lag = 2;
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
            private void SetupTextBoxEvents(ToolContext ctx, System.Windows.Controls.RichTextBox tb)
            {
                // 绘制虚线框和句柄
                tb.Loaded += (s, e) => { DrawTextboxOverlay(ctx); };

                tb.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Delete)
                    {
                        CommitText(ctx);
                        ctx.EditorOverlay.Children.Remove(tb);
                        _richTextBox = null;
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

                if (_richTextBox != null) CommitText(ctx);
                Point px = ctx.ToPixel(viewPos);
                _richTextBox = CreateRichTextBox(ctx, px.X, px.Y);
                if (!string.IsNullOrEmpty(text))
                {
                    var range = new TextRange(_richTextBox.Document.ContentStart, _richTextBox.Document.ContentEnd);
                    range.Text = text;
                }


                _richTextBox.MaxWidth = 1000;
                _richTextBox.Width = Double.NaN; // 让宽度自适应内容
                _richTextBox.Height = Double.NaN;

                // 显示 UI
                ctx.EditorOverlay.Visibility = Visibility.Visible;
                ctx.EditorOverlay.IsHitTestVisible = true;
                Canvas.SetZIndex(ctx.EditorOverlay, 999);
                ctx.EditorOverlay.Children.Add(_richTextBox);

                ((MainWindow)System.Windows.Application.Current.MainWindow).ShowTextToolbarFor(_richTextBox);

                // 绑定关键事件（原本写在 OnPointerUp 里的那一大段）
                SetupTextBoxEvents(ctx, _richTextBox);

                ctx.EditorOverlay.PreviewMouseUp -= Overlay_PreviewMouseUp; // 防止重复订阅
                ctx.EditorOverlay.PreviewMouseUp += Overlay_PreviewMouseUp;

                ctx.EditorOverlay.PreviewMouseMove -= Overlay_PreviewMouseMove;
                ctx.EditorOverlay.PreviewMouseMove += Overlay_PreviewMouseMove;

                ctx.EditorOverlay.PreviewMouseDown -= Overlay_PreviewMouseDown;
                ctx.EditorOverlay.PreviewMouseDown += Overlay_PreviewMouseDown;

                _richTextBox.UpdateLayout();
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
                    _startW = _richTextBox.ActualWidth;
                    _startH = _richTextBox.ActualHeight;
                    _startX = Canvas.GetLeft(_richTextBox);
                    _startY = Canvas.GetTop(_richTextBox);
                    ctx.EditorOverlay.CaptureMouse();
                    e.Handled = true;
                }
                else if (IsInsideBorder(pixelPos))
                {
                    _dragging = true;
                    _startMouse = pixelPos;
                    _startX = Canvas.GetLeft(_richTextBox);
                    _startY = Canvas.GetTop(_richTextBox);
                    ctx.EditorOverlay.CaptureMouse();
                    e.Handled = true;
                }
                else
                {
                    OnPointerDown(ctx, pos);
                }
            }

            //public void CommitText(ToolContext ctx)
            //{
            //    if (_richTextBox == null) return;
            //    if (string.IsNullOrWhiteSpace(_richTextBox.Text))
            //    {
            //        ((MainWindow)System.Windows.Application.Current.MainWindow).HideTextToolbar();
            //        ctx.SelectionOverlay.Children.Clear();
            //        ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
            //        if (ctx.EditorOverlay.Children.Contains(_richTextBox))
            //            ctx.EditorOverlay.Children.Remove(_richTextBox);
            //        lag = 2;
            //        return;
            //    }
            //    double tweakX = 2.0;
            //    double tweakY = 1.0;
            //    // 1. 获取位置信息
            //    double tbLeft = Canvas.GetLeft(_richTextBox);
            //    double tbTop = Canvas.GetTop(_richTextBox);
            //    double tbWidth = _richTextBox.ActualWidth;
            //    double tbHeight = _richTextBox.ActualHeight;
            //    var formattedText = new FormattedText(
            //        _richTextBox.Text,
            //        CultureInfo.CurrentCulture,
            //        System.Windows.FlowDirection.LeftToRight,
            //        new Typeface(_richTextBox.FontFamily, _richTextBox.FontStyle, _richTextBox.FontWeight, _richTextBox.FontStretch),
            //        _richTextBox.FontSize,
            //        _richTextBox.Foreground,
            //        96.0 // 强制 96 DPI，确保像素大小与逻辑大小 1:1
            //    )
            //    {
            //        MaxTextWidth = Math.Max(1, tbWidth - _richTextBox.Padding.Left - _richTextBox.Padding.Right),
            //        MaxTextHeight = double.MaxValue,
            //        Trimming = TextTrimming.None,
            //        TextAlignment = _richTextBox.TextAlignment
            //    };
            //    formattedText.SetTextDecorations(_richTextBox.TextDecorations);

            //    // 3. 渲染到 Visual
            //    var visual = new DrawingVisual();
            //    using (var dc = visual.RenderOpen())
            //    {
            //        // 如果文本框有背景色，画背景
            //        if (_richTextBox.Background is SolidColorBrush bgBrush && bgBrush.Color.A > 0)
            //        {
            //            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, tbWidth, tbHeight));
            //        }

            //        // 使用 Grayscale 渲染文本，避免 ClearType 在透明背景上产生彩色边缘
            //        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
            //        TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Display);

            //        dc.DrawText(formattedText, new Point(_richTextBox.Padding.Left + tweakX, _richTextBox.Padding.Top + tweakY));
            //    }

            //    int width = (int)Math.Ceiling(tbWidth);
            //    int height = (int)Math.Ceiling(tbHeight);
            //    if (width <= 0 || height <= 0) return;
            //    var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
            //    rtb.Render(visual);

            //    int stride = width * 4;
            //    byte[] sourcePixels = new byte[height * stride];
            //    rtb.CopyPixels(sourcePixels, stride, 0);
            //    int x = (int)tbLeft;
            //    int y = (int)tbTop;

            //    var writeableBitmap = ctx.Surface.Bitmap; 
            //    int canvasWidth = writeableBitmap.PixelWidth;
            //    int canvasHeight = writeableBitmap.PixelHeight;

            //    // 计算实际可操作的矩形区域 (Clip)
            //    int safeX = Math.Max(0, x);
            //    int safeY = Math.Max(0, y);
            //    int safeRight = Math.Min(canvasWidth, x + width);
            //    int safeBottom = Math.Min(canvasHeight, y + height);
            //    int safeW = safeRight - safeX;
            //    int safeH = safeBottom - safeY;

            //    if (safeW <= 0 || safeH <= 0)
            //    {
            //        CleanUpUI(ctx);
            //        return;
            //    }
            //    byte[] destPixels = new byte[safeH * stride];
            //    Int32Rect dirtyRect = new Int32Rect(safeX, safeY, safeW, safeH);
            //    writeableBitmap.CopyPixels(dirtyRect, destPixels, stride, 0);

            //    int sourceOffsetX = safeX - x;
            //    int sourceOffsetY = safeY - y;
            //    int sourceStartIndex = sourceOffsetY * stride + sourceOffsetX * 4;

            //    double globalOpacityFactor = TabPaint.SettingsManager.Instance.Current.PenOpacity;
            //    AlphaBlendBatch(sourcePixels, destPixels, safeW, safeH, stride, sourceStartIndex, globalOpacityFactor);

            //    // 8. 写回混合后的结果
            //    ctx.Undo.BeginStroke();
            //    ctx.Undo.AddDirtyRect(dirtyRect);
            //    writeableBitmap.WritePixels(dirtyRect, destPixels, stride, 0);
            //    ctx.Undo.CommitStroke();

            //    // UI 清理
            //    CleanUpUI(ctx);
            //    lag = 2;
            //}


        }
    }
}