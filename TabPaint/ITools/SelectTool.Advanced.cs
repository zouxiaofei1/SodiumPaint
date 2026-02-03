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
            // ========== 轮廓追踪相关方法 ==========

            /// <summary>
            /// 从 AlphaMap 生成轮廓 Geometry（支持多区域和孔洞）
            /// </summary>
            private Geometry GenerateContourGeometry(byte[] alphaMap, int width, int height, int offsetX, int offsetY)
            {
                if (alphaMap == null || width <= 0 || height <= 0) return null;

                // 1. 将 AlphaMap 转换为简单的 bool 数组
                bool[,] mask = new bool[height, width];
                int stride = width * 4;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = y * stride + x * 4 + 3; // Alpha 通道
                        mask[y, x] = idx < alphaMap.Length && alphaMap[idx] > 128;
                    }
                }

                // 2. 使用 Marching Squares 提取所有轮廓
                var allContours = ExtractAllContours(mask, width, height);

                if (allContours.Count == 0) return null;

                // 3. 将轮廓转换为 Geometry
                var geometryGroup = new GeometryGroup();
                geometryGroup.FillRule = FillRule.EvenOdd; // 支持孔洞

                foreach (var contour in allContours)
                {
                    if (contour.Count < 3) continue;

                    var figure = new PathFigure();
                    figure.StartPoint = new Point(contour[0].X + offsetX, contour[0].Y + offsetY);
                    figure.IsClosed = true;
                    figure.IsFilled = false;

                    // 简化轮廓点（Douglas-Peucker 算法可选，这里用简单的跳点）
                    var simplifiedPoints = SimplifyContour(contour, 1.0);

                    var segments = new PolyLineSegment();
                    for (int i = 1; i < simplifiedPoints.Count; i++)
                    {
                        segments.Points.Add(new Point(simplifiedPoints[i].X + offsetX, simplifiedPoints[i].Y + offsetY));
                    }
                    figure.Segments.Add(segments);

                    var pathGeometry = new PathGeometry();
                    pathGeometry.Figures.Add(figure);
                    geometryGroup.Children.Add(pathGeometry);
                }

                geometryGroup.Freeze();
                return geometryGroup;
            }

            /// <summary>
            /// 提取所有轮廓（外轮廓和孔洞）
            /// </summary>
            private List<List<Point>> ExtractAllContours(bool[,] mask, int width, int height)
            {
                var contours = new List<List<Point>>();
                bool[,] visited = new bool[height, width];

                // 扫描所有边界点
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // 找到一个边界点：当前点是选中的，且左边是未选中的（或在边界）
                        if (mask[y, x] && !visited[y, x])
                        {
                            bool isEdge = (x == 0 || !mask[y, x - 1]);
                            if (isEdge)
                            {
                                var contour = TraceContour(mask, visited, x, y, width, height);
                                if (contour != null && contour.Count >= 3)
                                {
                                    contours.Add(contour);
                                }
                            }
                        }
                    }
                }

                return contours;
            }

            /// <summary>
            /// Moore-Neighbor 轮廓追踪算法
            /// </summary>
            private List<Point> TraceContour(bool[,] mask, bool[,] visited, int startX, int startY, int width, int height)
            {
                var contour = new List<Point>();

                // 8邻域方向：从右开始顺时针
                // 0=右, 1=右下, 2=下, 3=左下, 4=左, 5=左上, 6=上, 7=右上
                int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
                int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };

                int x = startX;
                int y = startY;
                int dir = 4; // 初始方向：从左边进入（所以开始向左搜索）

                int startDir = dir;
                bool firstStep = true;

                int maxIterations = width * height * 2; // 防止无限循环
                int iterations = 0;

                do
                {
                    contour.Add(new Point(x, y));
                    visited[y, x] = true;

                    // 从当前方向的逆时针方向开始搜索
                    int searchDir = (dir + 5) % 8; // 逆时针回退3步
                    bool found = false;

                    for (int i = 0; i < 8; i++)
                    {
                        int checkDir = (searchDir + i) % 8;
                        int nx = x + dx[checkDir];
                        int ny = y + dy[checkDir];

                        if (nx >= 0 && nx < width && ny >= 0 && ny < height && mask[ny, nx])
                        {
                            x = nx;
                            y = ny;
                            dir = checkDir;
                            found = true;
                            break;
                        }
                    }

                    if (!found) break;

                    iterations++;
                    if (iterations > maxIterations) break;

                    // 检查是否回到起点
                    if (!firstStep && x == startX && y == startY)
                    {
                        break;
                    }
                    firstStep = false;

                } while (true);

                return contour;
            }

            /// <summary>
            /// 简化轮廓点（减少点数，提高渲染性能）
            /// </summary>
            private List<Point> SimplifyContour(List<Point> points, double tolerance)
            {
                if (points.Count <= 2) return points;

                // 简单的角度过滤：移除共线点
                var result = new List<Point> { points[0] };

                for (int i = 1; i < points.Count - 1; i++)
                {
                    var prev = result[result.Count - 1];
                    var curr = points[i];
                    var next = points[i + 1];

                    // 计算方向变化
                    double dx1 = curr.X - prev.X;
                    double dy1 = curr.Y - prev.Y;
                    double dx2 = next.X - curr.X;
                    double dy2 = next.Y - curr.Y;

                    // 如果方向改变，保留该点
                    if (Math.Abs(dx1 * dy2 - dy1 * dx2) > tolerance)
                    {
                        result.Add(curr);
                    }
                }

                result.Add(points[points.Count - 1]);
                return result;
            }

            /// <summary>
            /// 更高效的轮廓生成（基于像素边缘）
            /// </summary>
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

            /// <summary>
            /// 将离散线段连接成连续路径
            /// </summary>
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

                    // 向前延伸
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

            /// <summary>
            /// 绘制矩形选区框
            /// </summary>
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

            /// <summary>
            /// 魔棒调整时显示半透明遮罩预览
            /// </summary>
            private void DrawWandPreviewMask(ToolContext ctx, Canvas overlay, Int32Rect rect, double invScale)
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