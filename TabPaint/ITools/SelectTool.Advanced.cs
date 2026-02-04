using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

//
//SelectTool类的Magicwand 和Lasso
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class SelectTool : ToolBase
        {
            private Geometry GeneratePixelEdgeGeometry(byte[] alphaMap, int width, int height, int offsetX, int offsetY)
            {
                if (alphaMap == null || width <= 0 || height <= 0) return null;

                int stride = width * 4;
                var segments = new List<(Point Start, Point End)>();

                // 扫描所有像素，找出边缘线段
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = y * stride + x * 4 + 3;
                        bool current = idx < alphaMap.Length && alphaMap[idx] > 128;

                        if (!current) continue;

                        // 检查四个方向，如果邻居不是选中状态，则添加边缘
                        // 上边缘
                        if (y == 0 || alphaMap[(y - 1) * stride + x * 4 + 3] <= 128)
                        {
                            segments.Add((new Point(x, y), new Point(x + 1, y)));
                        }
                        // 下边缘
                        if (y == height - 1 || alphaMap[(y + 1) * stride + x * 4 + 3] <= 128)
                        {
                            segments.Add((new Point(x, y + 1), new Point(x + 1, y + 1)));
                        }
                        // 左边缘
                        if (x == 0 || alphaMap[y * stride + (x - 1) * 4 + 3] <= 128)
                        {
                            segments.Add((new Point(x, y), new Point(x, y + 1)));
                        }
                        // 右边缘
                        if (x == width - 1 || alphaMap[y * stride + (x + 1) * 4 + 3] <= 128)
                        {
                            segments.Add((new Point(x + 1, y), new Point(x + 1, y + 1)));
                        }
                    }
                }

                if (segments.Count == 0) return null;

                // 将线段连接成路径
                var paths = ConnectSegments(segments);

                var geometryGroup = new GeometryGroup();

                foreach (var path in paths)
                {
                    if (path.Count < 2) continue;

                    var streamGeometry = new StreamGeometry();
                    using (var ctx = streamGeometry.Open())
                    {
                        ctx.BeginFigure(new Point(path[0].X + offsetX, path[0].Y + offsetY), false, true);

                        for (int i = 1; i < path.Count; i++)
                        {
                            ctx.LineTo(new Point(path[i].X + offsetX, path[i].Y + offsetY), true, false);
                        }
                    }
                    streamGeometry.Freeze();
                    geometryGroup.Children.Add(streamGeometry);
                }

                geometryGroup.Freeze();
                return geometryGroup;
            }
            private List<List<Point>> ConnectSegments(List<(Point Start, Point End)> segments)
            {
                var paths = new List<List<Point>>();
                var remaining = new HashSet<int>(Enumerable.Range(0, segments.Count));

                // 建立端点索引
                var startIndex = new Dictionary<(int, int), List<int>>();
                var endIndex = new Dictionary<(int, int), List<int>>();

                for (int i = 0; i < segments.Count; i++)
                {
                    var s = segments[i];
                    var startKey = ((int)s.Start.X, (int)s.Start.Y);
                    var endKey = ((int)s.End.X, (int)s.End.Y);

                    if (!startIndex.ContainsKey(startKey)) startIndex[startKey] = new List<int>();
                    if (!endIndex.ContainsKey(endKey)) endIndex[endKey] = new List<int>();

                    startIndex[startKey].Add(i);
                    endIndex[endKey].Add(i);
                }

                while (remaining.Count > 0)
                {
                    int first = remaining.First();
                    remaining.Remove(first);

                    var path = new List<Point> { segments[first].Start, segments[first].End };

                    // 向后延伸
                    bool extended = true;
                    while (extended)
                    {
                        extended = false;
                        var lastPoint = path[path.Count - 1];
                        var key = ((int)lastPoint.X, (int)lastPoint.Y);

                        if (startIndex.TryGetValue(key, out var candidates))
                        {
                            foreach (var idx in candidates)
                            {
                                if (remaining.Contains(idx))
                                {
                                    remaining.Remove(idx);
                                    path.Add(segments[idx].End);
                                    extended = true;
                                    break;
                                }
                            }
                        }

                        if (!extended && endIndex.TryGetValue(key, out candidates))
                        {
                            foreach (var idx in candidates)
                            {
                                if (remaining.Contains(idx))
                                {
                                    remaining.Remove(idx);
                                    path.Add(segments[idx].Start);
                                    extended = true;
                                    break;
                                }
                            }
                        }
                    }
                    extended = true;
                    while (extended)
                    {
                        extended = false;
                        var firstPoint = path[0];
                        var key = ((int)firstPoint.X, (int)firstPoint.Y);

                        if (endIndex.TryGetValue(key, out var candidates))
                        {
                            foreach (var idx in candidates)
                            {
                                if (remaining.Contains(idx))
                                {
                                    remaining.Remove(idx);
                                    path.Insert(0, segments[idx].Start);
                                    extended = true;
                                    break;
                                }
                            }
                        }

                        if (!extended && startIndex.TryGetValue(key, out candidates))
                        {
                            foreach (var idx in candidates)
                            {
                                if (remaining.Contains(idx))
                                {
                                    remaining.Remove(idx);
                                    path.Insert(0, segments[idx].End);
                                    extended = true;
                                    break;
                                }
                            }
                        }
                    }

                    paths.Add(path);
                }

                return paths;
            }
            private void DrawIrregularContour(ToolContext ctx, Canvas overlay, Geometry geometry,
    Int32Rect rect, double invScale, double diff, bool applyTransform = true)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

                Geometry drawGeometry = geometry;
                if (applyTransform)
                {
                    var cloneGeom = geometry.Clone();
                    cloneGeom.Transform = new TranslateTransform(rect.X + diff * 0.75, rect.Y);
                    drawGeometry = cloneGeom;
                }
                else
                {
                    // 魔棒模式下，偏移已经在生成时处理了，只需要处理 diff
                    if (Math.Abs(diff) > 0.001)
                    {
                        var cloneGeom = geometry.Clone();
                        cloneGeom.Transform = new TranslateTransform(diff * 0.75, 0);
                        drawGeometry = cloneGeom;
                    }
                }

                // 白色底线
                var whitePath = new System.Windows.Shapes.Path
                {
                    Stroke = Brushes.White,
                    StrokeThickness = invScale * 1.5,
                    Data = drawGeometry,
                    SnapsToDevicePixels = false,
                    Opacity = 0.9
                };

                // 黑色虚线（蚂蚁线动画）
                var blackPath = new System.Windows.Shapes.Path
                {
                    Stroke = mw._darkBackgroundBrush,
                    StrokeThickness = invScale * 1.5,
                    Data = drawGeometry,
                    SnapsToDevicePixels = false,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                };

                // 蚂蚁线动画
                DoubleAnimation animation = new DoubleAnimation
                {
                    From = 0,
                    To = 8,
                    Duration = new Duration(TimeSpan.FromSeconds(1)),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                blackPath.BeginAnimation(System.Windows.Shapes.Path.StrokeDashOffsetProperty, animation);

                overlay.Children.Add(whitePath);
                overlay.Children.Add(blackPath);

                // 绘制 8 个句柄（在包围盒上）
                DrawHandles(overlay, rect, invScale, diff);
            }

            private void DrawRectangleOverlay(ToolContext ctx, Canvas overlay, Int32Rect rect, double invScale, double diff)
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                var geometry = new RectangleGeometry(new Rect(rect.X, rect.Y, rect.Width, rect.Height));

                // 白色底线
                var whiteBase = new System.Windows.Shapes.Path
                {
                    Stroke = Brushes.White,
                    StrokeThickness = invScale * AppConsts.SelectToolOutlineThickness,
                    Data = geometry,
                    SnapsToDevicePixels = false,
                    Opacity = 0.8
                };
                overlay.Children.Add(whiteBase);

                // 黑色虚线
                var outlinePath = new System.Windows.Shapes.Path
                {
                    Stroke = mw._darkBackgroundBrush,
                    StrokeThickness = invScale * AppConsts.SelectToolOutlineThickness,
                    Data = geometry,
                    SnapsToDevicePixels = false,
                    StrokeDashArray = new DoubleCollection { AppConsts.SelectToolDashLength, AppConsts.SelectToolDashLength }
                };

                DoubleAnimation animation = new DoubleAnimation
                {
                    From = 0,
                    To = AppConsts.SelectToolAnimationTo,
                    Duration = new Duration(TimeSpan.FromSeconds(AppConsts.SelectToolAnimationDurationSeconds)),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                outlinePath.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, animation);
                overlay.Children.Add(outlinePath);

                // 绘制句柄
                DrawHandles(overlay, rect, invScale, diff);
            }
            private void DrawHandles(Canvas overlay, Int32Rect rect, double invScale, double diff)
            {
                if (SelectionType != SelectionType.Rectangle) return;
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

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
                    RenderOptions.SetEdgeMode(handle, EdgeMode.Unspecified);
                    Canvas.SetLeft(handle, p.X - HandleSize * invScale / 2 + diff * 0.75);
                    Canvas.SetTop(handle, p.Y - HandleSize * invScale / 2);
                    overlay.Children.Add(handle);
                }
            }
            private void DrawWandPreviewMask(ToolContext ctx, Canvas overlay, Int32Rect rect, double invScale) /// 魔棒调整时显示半透明遮罩预览
            {
                if (_selectionAlphaMap == null || rect.Width <= 0 || rect.Height <= 0) return;

                try
                {
                    // 创建半透明遮罩图像
                    int stride = rect.Width * 4;
                    byte[] previewPixels = new byte[rect.Height * stride];

                    // 选中区域显示为半透明蓝色
                    for (int i = 0; i < _selectionAlphaMap.Length; i += 4)
                    {
                        if (_selectionAlphaMap[i + 3] > 128)
                        {
                            previewPixels[i + 0] = 255;  // B
                            previewPixels[i + 1] = 100;  // G
                            previewPixels[i + 2] = 100;  // R
                            previewPixels[i + 3] = 80;   // A (半透明)
                        }
                    }

                    var maskBitmap = BitmapSource.Create(
                        rect.Width, rect.Height,
                        96, 96,
                        PixelFormats.Bgra32,
                        null,
                        previewPixels,
                        stride);

                    var maskImage = new System.Windows.Controls.Image
                    {
                        Source = maskBitmap,
                        Width = rect.Width,
                        Height = rect.Height,
                        Stretch = System.Windows.Media.Stretch.Fill,
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(maskImage, rect.X);
                    Canvas.SetTop(maskImage, rect.Y);
                    overlay.Children.Insert(0, maskImage); // 插入到最底层
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("DrawWandPreviewMask error: " + ex.Message);
                }
            }
        }
    }
}