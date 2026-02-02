using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        public enum ResizeAnchor
        {
            None,
            TopLeft, TopMiddle, TopRight,
            LeftMiddle, RightMiddle,
            BottomLeft, BottomMiddle, BottomRight
        }

        private const double MaxCanvasSize = AppConsts.MaxCanvasSize;
        public class CanvasResizeManager
        {
            private readonly MainWindow _mainWindow;
            private readonly Canvas _overlay;
            private bool _isResizing = false;
            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            private Point _startDragPoint;
            private Int32Rect _startRect; // 拖拽开始时的画布尺寸
            private Rectangle _previewBorder; // 拖拽时的虚线框

            // 样式配置
            private const double HandleSize = 8.0;

            public CanvasResizeManager(MainWindow window)
            {
                _mainWindow = window;
                _overlay = _mainWindow.CanvasResizeOverlay;
            }

            // 每次缩放或画布改变大小时调用此方法刷新 UI
            public void UpdateUI()
            {
                _overlay.Children.Clear();
             
                if(((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source==null) return;
                // 获取当前画布尺寸
                double w = ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source.Width;
                double h = ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source.Height;

                // 确保 Overlay 大小与图片一致
                _overlay.Width = w;
                _overlay.Height = h;

                double scale = ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                double invScale = 1.0 / scale;
                double size = HandleSize * invScale;

                if (_mainWindow.IsViewMode) return;
                // 2. 绘制 8 个手柄
                var handles = GetHandlePositions(w, h);
                foreach (var kvp in handles)
                {
                    var rect = new Rectangle
                    {
                        Width = size,
                        Height = size,
                        Fill = Brushes.White,
                        Stroke = new SolidColorBrush(Color.FromRgb(160, 160, 160)), // 建议改用浅灰色
                        StrokeThickness = 1 * invScale,
                        Tag = kvp.Key, // 存储锚点类型
                        Cursor = GetCursor(kvp.Key)
                    };

                    // 居中定位
                    Canvas.SetLeft(rect, kvp.Value.X - size / 2);
                    Canvas.SetTop(rect, kvp.Value.Y - size / 2);

                    // 绑定事件
                    rect.MouseLeftButtonDown += OnHandleDown;
                    rect.MouseLeftButtonUp += OnHandleUp;
                    rect.MouseMove += OnHandleMove;

                    _overlay.Children.Add(rect);
                }
            }

            private void OnHandleDown(object sender, MouseButtonEventArgs e)
            {
                var rect = sender as Rectangle;
                _currentAnchor = (ResizeAnchor)rect.Tag;
                _isResizing = true;
                _startDragPoint = e.GetPosition(((MainWindow)System.Windows.Application.Current.MainWindow).CanvasWrapper); // 获取相对于 Grid 的坐标

                // 记录原始尺寸
                var bmp = ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source as BitmapSource;
                _startRect = new Int32Rect(0, 0, (int)bmp.PixelWidth, (int)bmp.PixelHeight);

                // 捕获鼠标
                rect.CaptureMouse();

                // 创建预览虚线框
                CreatePreviewBorder();
                e.Handled = true;
            }

            private void OnHandleMove(object sender, MouseEventArgs e)
            {
                if (!_isResizing) return;

                var currentPoint = e.GetPosition(((MainWindow)System.Windows.Application.Current.MainWindow).CanvasWrapper);
                var rect = CalculateNewRect(currentPoint);

                // 更新预览框位置和大小
                Canvas.SetLeft(_previewBorder, rect.X);
                Canvas.SetTop(_previewBorder, rect.Y);
                _previewBorder.Width = Math.Max(1, rect.Width);
                _previewBorder.Height = Math.Max(1, rect.Height);
            }

            private void OnHandleUp(object sender, MouseButtonEventArgs e)
            {
                if (!_isResizing) return;

                var rect = sender as Rectangle;
                rect.ReleaseMouseCapture();
                _isResizing = false;

                // 计算最终矩形
                var currentPoint = e.GetPosition(((MainWindow)System.Windows.Application.Current.MainWindow).CanvasWrapper);
                var finalRect = CalculateNewRect(currentPoint); // 这里拿到的是相对于原图左上角的 Rect

                // 移除预览框
                if (_previewBorder != null)
                {
                    _overlay.Children.Remove(_previewBorder);
                    _previewBorder = null;
                }

                // 提交更改
                ApplyResize(finalRect);
            }

            private Rect CalculateNewRect(Point currentMouse)
            {
                double dx = currentMouse.X - _startDragPoint.X;
                double dy = currentMouse.Y - _startDragPoint.Y;

                double startW = _startRect.Width;
                double startH = _startRect.Height;
                double rightEdge = startW;
                double bottomEdge = startH;

                // 初始化为无变化状态
                double newX = 0;
                double newY = 0;
                double newW = startW;
                double newH = startH;

                switch (_currentAnchor)
                {
                    case ResizeAnchor.TopLeft:
                        // 1. 计算宽度，限制最小值1，限制最大值 MaxCanvasSize
                        newW = Math.Min(MaxCanvasSize, Math.Max(1, startW - dx));
                        newX = rightEdge - newW;

                        newH = Math.Min(MaxCanvasSize, Math.Max(1, startH - dy));
                        newY = bottomEdge - newH;
                        break;

                    case ResizeAnchor.TopMiddle:
                        newH = Math.Min(MaxCanvasSize, Math.Max(1, startH - dy));
                        newY = bottomEdge - newH;
                        break;

                    case ResizeAnchor.TopRight:
                        // 向右拉伸，X保持为0，只限制宽度
                        newW = Math.Min(MaxCanvasSize, Math.Max(1, startW + dx));

                        newH = Math.Min(MaxCanvasSize, Math.Max(1, startH - dy));
                        newY = bottomEdge - newH;
                        break;

                    case ResizeAnchor.LeftMiddle:
                        newW = Math.Min(MaxCanvasSize, Math.Max(1, startW - dx));
                        newX = rightEdge - newW;
                        break;

                    case ResizeAnchor.RightMiddle:
                        newW = Math.Min(MaxCanvasSize, Math.Max(1, startW + dx));
                        break;

                    case ResizeAnchor.BottomLeft:
                        newW = Math.Min(MaxCanvasSize, Math.Max(1, startW - dx));
                        newX = rightEdge - newW;

                        newH = Math.Min(MaxCanvasSize, Math.Max(1, startH + dy));
                        break;

                    case ResizeAnchor.BottomMiddle:
                        newH = Math.Min(MaxCanvasSize, Math.Max(1, startH + dy));
                        break;

                    case ResizeAnchor.BottomRight:
                        newW = Math.Min(MaxCanvasSize, Math.Max(1, startW + dx));
                        newH = Math.Min(MaxCanvasSize, Math.Max(1, startH + dy));
                        break;
                }

                return new Rect(newX, newY, newW, newH);
            }



            private void CreatePreviewBorder()
            {
                double invScale = 1.0 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                _previewBorder = new Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    StrokeThickness = 1 * invScale,
                    IsHitTestVisible = false
                };
                _overlay.Children.Add(_previewBorder);
            }

            private void ApplyResize(Rect newBounds)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                int newW = Math.Min((int)AppConsts.MaxCanvasSize, (int)newBounds.Width);
                int newH = Math.Min((int)AppConsts.MaxCanvasSize, (int)newBounds.Height);
                int offsetX = -(int)newBounds.X;
                int offsetY = -(int)newBounds.Y;

                if (newW <= 0 || newH <= 0) return;

                // 1. 获取当前图像数据 (Undo 需要)
                var currentBmp = mw._ctx.Surface.Bitmap; // 假设这是当前的 WriteableBitmap
                var rect = new Int32Rect(0, 0, currentBmp.PixelWidth, currentBmp.PixelHeight);

                // 获取全图数据用于 Undo
                byte[] oldPixels = mw._undo.SafeExtractRegion(rect);

                // 2. 创建新位图
                var newBmp = new WriteableBitmap(newW, newH, currentBmp.DpiX, currentBmp.DpiY, PixelFormats.Bgra32, null);
                byte[] whiteBg = new byte[newW * newH * 4];
                for (int i = 0; i < whiteBg.Length; i++) whiteBg[i] = 255;
                newBmp.WritePixels(new Int32Rect(0, 0, newW, newH), whiteBg, newBmp.BackBufferStride, 0);

                // 3. 将旧图像绘制到新图像的指定偏移位置
                int copyX = Math.Max(0, offsetX); // 在新图中的起始X
                int copyY = Math.Max(0, offsetY); // 在新图中的起始Y
                int srcX = offsetX < 0 ? -offsetX : 0; // 如果 offsetX 是正数(裁剪)，源图从 srcX 开始
                int srcY = offsetY < 0 ? -offsetY : 0;

                int copyW = Math.Min(rect.Width - srcX, newW - copyX);
                int copyH = Math.Min(rect.Height - srcY, newH - copyY);

                if (copyW > 0 && copyH > 0)
                {
                    var srcRect = new Int32Rect(srcX, srcY, copyW, copyH);
                    var srcPixels = mw._ctx.Surface.ExtractRegion(srcRect); // 这里需要支持从 Surface 获取指定区域

                    newBmp.WritePixels(new Int32Rect(copyX, copyY, copyW, copyH), srcPixels, copyW * 4, 0);
                }
                byte[] newPixels = new byte[newW * newH * 4];
                newBmp.CopyPixels(newPixels, newBmp.BackBufferStride, 0);

                mw._undo.PushTransformAction(
                    rect, oldPixels,                // Undo: 回到旧尺寸，旧像素
                    new Int32Rect(0, 0, newW, newH), newPixels // Redo: 回到新尺寸，新像素
                );
                mw._ctx.Surface.ReplaceBitmap(newBmp);
                mw.NotifyCanvasSizeChanged(newW, newH);
                mw._bitmap = newBmp;
                UpdateUI(); 
                EnsureEdgeVisible(newBounds);
            }
            private void EnsureEdgeVisible(Rect resizeRect)
            {
                var scrollViewer = ((MainWindow)System.Windows.Application.Current.MainWindow).ScrollContainer;

                if (scrollViewer == null) return;
                scrollViewer.UpdateLayout();
                double scale = ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;

                // 定义留白大小 (比如 50px)
                double padding = 50;
                if (resizeRect.X < 0)
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + (resizeRect.X * scale) - padding);
                }
                // 向右扩展：检查右边缘是否在视野内
                else if (resizeRect.Width > _startRect.Width) // 宽度变大了
                {
                    double newVisualWidth = resizeRect.Width * scale;
                    // 如果新宽度超出了视口，且是为了看右边
                    if (_currentAnchor == ResizeAnchor.RightMiddle ||
                        _currentAnchor == ResizeAnchor.TopRight ||
                        _currentAnchor == ResizeAnchor.BottomRight)
                    {
                        // 稍微向右滚动一点，露出右边缘，但不一定滚到底
                        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + (resizeRect.Width - _startRect.Width) * scale + padding);
                    }
                }
                if (resizeRect.Y < 0)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + (resizeRect.Y * scale) - padding);
                }
                // 向下扩展
                else if (resizeRect.Height > _startRect.Height)
                {
                    if (_currentAnchor == ResizeAnchor.BottomMiddle ||
                        _currentAnchor == ResizeAnchor.BottomLeft ||
                        _currentAnchor == ResizeAnchor.BottomRight)
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + (resizeRect.Height - _startRect.Height) * scale + padding);
                    }
                }
            }
            private Dictionary<ResizeAnchor, Point> GetHandlePositions(double w, double h)
            {
                return new Dictionary<ResizeAnchor, Point>
            {
                { ResizeAnchor.TopLeft, new Point(0, 0) },
                { ResizeAnchor.TopMiddle, new Point(w/2, 0) },
                { ResizeAnchor.TopRight, new Point(w, 0) },
                { ResizeAnchor.LeftMiddle, new Point(0, h/2) },
                { ResizeAnchor.RightMiddle, new Point(w, h/2) },
                { ResizeAnchor.BottomLeft, new Point(0, h) },
                { ResizeAnchor.BottomMiddle, new Point(w/2, h) },
                { ResizeAnchor.BottomRight, new Point(w, h) },
            };
            }
            public void SetHandleVisibility(bool visible)
            {
                _overlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (visible && ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source == null)
                {
                    _overlay.Visibility = Visibility.Collapsed;
                }
            }

            private Cursor GetCursor(ResizeAnchor anchor)
            {
                switch (anchor)
                {
                    case ResizeAnchor.TopLeft: return Cursors.SizeNWSE;
                    case ResizeAnchor.TopMiddle: return Cursors.SizeNS;
                    case ResizeAnchor.TopRight: return Cursors.SizeNESW;
                    case ResizeAnchor.LeftMiddle: return Cursors.SizeWE;
                    case ResizeAnchor.RightMiddle: return Cursors.SizeWE;
                    case ResizeAnchor.BottomLeft: return Cursors.SizeNESW;
                    case ResizeAnchor.BottomMiddle: return Cursors.SizeNS;
                    case ResizeAnchor.BottomRight: return Cursors.SizeNWSE;
                    default: return Cursors.Arrow;
                }
            }
        }
    }
}
