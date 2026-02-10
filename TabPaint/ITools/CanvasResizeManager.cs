//
//CanvasResizeManager.cs
//画布大小调整管理器，通过在画布边缘显示手柄并响应拖拽，实现对画布尺寸的直观交互式调整。
//
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
            private const double HandleSize = AppConsts.CanvasResizeHandleSize;

            public CanvasResizeManager(MainWindow window)
            {
                _mainWindow = window;
                _overlay = _mainWindow.CanvasResizeOverlay;
            }

            // 每次缩放或画布改变大小时调用此方法刷新 UI
            public void UpdateUI()
            {
                _overlay.Children.Clear();
             
                if((MainWindow.GetCurrentInstance()).BackgroundImage.Source==null) return;
                // 获取当前画布尺寸
                double w = (MainWindow.GetCurrentInstance()).BackgroundImage.Source.Width;
                double h = (MainWindow.GetCurrentInstance()).BackgroundImage.Source.Height;

                // 确保 Overlay 大小与图片一致
                _overlay.Width = w;
                _overlay.Height = h;

                double scale = (MainWindow.GetCurrentInstance()).zoomscale;
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
                _startDragPoint = e.GetPosition((MainWindow.GetCurrentInstance()).CanvasWrapper); // 获取相对于 Grid 的坐标

                // 记录原始尺寸
                var bmp = (MainWindow.GetCurrentInstance()).BackgroundImage.Source as BitmapSource;
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

                var currentPoint = e.GetPosition((MainWindow.GetCurrentInstance()).CanvasWrapper);
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
                var currentPoint = e.GetPosition((MainWindow.GetCurrentInstance()).CanvasWrapper);
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
                double invScale = 1.0 / (MainWindow.GetCurrentInstance()).zoomscale;
                _previewBorder = new Rectangle
                {
                    Stroke = (Brush)(Brush)System.Windows.Application.Current.FindResource("TextPrimaryBrush"),
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    StrokeThickness = 1 * invScale,
                    IsHitTestVisible = false
                };
                _overlay.Children.Add(_previewBorder);
            }

            private void ApplyResize(Rect newBounds)
            {
                var mw = MainWindow.GetCurrentInstance();
                int newW = Math.Min((int)AppConsts.MaxCanvasSize, (int)newBounds.Width);
                int newH = Math.Min((int)AppConsts.MaxCanvasSize, (int)newBounds.Height);
                int offsetX = -(int)newBounds.X;
                int offsetY = -(int)newBounds.Y;

                if (newW <= 0 || newH <= 0) return;

                var oldBmp = mw._ctx.Surface.Bitmap;
                int oldW = oldBmp.PixelWidth;
                int oldH = oldBmp.PixelHeight;

                // 1. 捕获撤销像素
                var undoRect = new Int32Rect(0, 0, oldW, oldH);
                byte[] oldPixels = mw._undo.SafeExtractRegion(undoRect);

                // 2. 创建新位图并直接内存操作 (优化：移除中间 byte[] 拷贝)
                var newBmp = new WriteableBitmap(newW, newH, oldBmp.DpiX, oldBmp.DpiY, PixelFormats.Bgra32, null);
                int newStride = newBmp.BackBufferStride;
                int oldStride = oldBmp.BackBufferStride;

                newBmp.Lock();
                oldBmp.Lock();
                try
                {
                    unsafe
                    {
                        byte* pNew = (byte*)newBmp.BackBuffer;
                        byte* pOld = (byte*)oldBmp.BackBuffer;

                        // 快速填充白色背景
                        long totalSize = (long)newH * newStride;
                        System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref *pNew, (int)totalSize).Fill(AppConsts.ColorComponentMax);

                        int copyX = Math.Max(0, offsetX);
                        int copyY = Math.Max(0, offsetY);
                        int srcX = offsetX < 0 ? -offsetX : 0;
                        int srcY = offsetY < 0 ? -offsetY : 0;

                        int copyW = Math.Min(oldW - srcX, newW - copyX);
                        int copyH = Math.Min(oldH - srcY, newH - copyY);

                        if (copyW > 0 && copyH > 0)
                        {
                            int bytesPerRow = copyW * 4;
                            for (int y = 0; y < copyH; y++)
                            {
                                byte* pSrcLine = pOld + (srcY + y) * oldStride + (srcX * 4);
                                byte* pDestLine = pNew + (copyY + y) * newStride + (copyX * 4);
                                Buffer.MemoryCopy(pSrcLine, pDestLine, bytesPerRow, bytesPerRow);
                            }
                        }
                    }
                    newBmp.AddDirtyRect(new Int32Rect(0, 0, newW, newH));
                }
                finally
                {
                    oldBmp.Unlock();
                    newBmp.Unlock();
                }

                // 3. 捕获重做像素
                byte[] newPixels = new byte[newH * newStride];
                newBmp.CopyPixels(new Int32Rect(0, 0, newW, newH), newPixels, newStride, 0);

                mw._undo.PushTransformAction(undoRect, oldPixels, new Int32Rect(0, 0, newW, newH), newPixels);
                mw._ctx.Surface.ReplaceBitmap(newBmp);
                mw.NotifyCanvasSizeChanged(newW, newH);
                mw._bitmap = newBmp;
                UpdateUI(); 
                EnsureEdgeVisible(newBounds);
            }
            private void EnsureEdgeVisible(Rect resizeRect)
            {
                var scrollViewer = (MainWindow.GetCurrentInstance()).ScrollContainer;

                if (scrollViewer == null) return;
                scrollViewer.UpdateLayout();
                double scale = (MainWindow.GetCurrentInstance()).zoomscale;

                // 定义留白大小 (比如 50px)
                double padding = AppConsts.CanvasResizeEdgePadding;
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
                if (visible && (MainWindow.GetCurrentInstance()).BackgroundImage.Source == null)
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
