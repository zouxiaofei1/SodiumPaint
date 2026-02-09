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
            public Geometry GeneratePixelEdgeGeometry(byte[] alphaMap, int width, int height,
     int offsetX, int offsetY, double zoomScale = 1.0, Rect? viewportRect = null)
            {
                if (alphaMap == null || width <= 0 || height <= 0) return null;
                int downsample = 1;
                if (zoomScale < 1.0)
                {
                    // 保证至少1，最大不超过16（避免极端缩小时全丢了）
                    downsample = Math.Clamp((int)Math.Ceiling(1.0 / zoomScale), 1, 16);
                }
                int clipX0 = 0, clipY0 = 0, clipX1 = width, clipY1 = height;

                if (viewportRect.HasValue && zoomScale >= 1.0)
                {
                    // 放大时才做视口裁剪（缩小时降采样已经够快了）
                    // viewportRect 是像素坐标系，需要转换为相对于选区的局部坐标
                    var vp = viewportRect.Value;
                    int margin = 2; // 多留2像素避免边缘闪烁
                    clipX0 = Math.Max(0, (int)Math.Floor(vp.Left - offsetX) - margin);
                    clipY0 = Math.Max(0, (int)Math.Floor(vp.Top - offsetY) - margin);
                    clipX1 = Math.Min(width, (int)Math.Ceiling(vp.Right - offsetX) + margin);
                    clipY1 = Math.Min(height, (int)Math.Ceiling(vp.Bottom - offsetY) + margin);

                    if (clipX0 >= clipX1 || clipY0 >= clipY1) return Geometry.Empty; // 完全不在视口内
                }
                if (downsample > 1)
                {
                    return GenerateDownsampledEdge(alphaMap, width, height, offsetX, offsetY, downsample);
                }
                else
                {
                    return GenerateClippedEdge(alphaMap, width, height, offsetX, offsetY,
                        clipX0, clipY0, clipX1, clipY1);
                }
            }
            private Geometry GenerateDownsampledEdge(byte[] alphaMap, int width, int height,
                int offsetX, int offsetY, int factor)
            {
                int stride = width * 4;
                int dsW = (width + factor - 1) / factor;
                int dsH = (height + factor - 1) / factor;
                bool[] dsMask = new bool[dsW * dsH];

                Parallel.For(0, dsH, dy =>
                {
                    for (int dx = 0; dx < dsW; dx++)
                    {
                        // 对应原图的 block 范围
                        int srcX0 = dx * factor;
                        int srcY0 = dy * factor;
                        int srcX1 = Math.Min(srcX0 + factor, width);
                        int srcY1 = Math.Min(srcY0 + factor, height);

                        bool found = false;
                        for (int sy = srcY0; sy < srcY1 && !found; sy++)
                        {
                            for (int sx = srcX0; sx < srcX1 && !found; sx++)
                            {
                                int idx = sy * stride + sx * 4 + 3;
                                if (idx < alphaMap.Length && alphaMap[idx] > 128)
                                    found = true;
                            }
                        }
                        dsMask[dy * dsW + dx] = found;
                    }
                });
                var hSegs = new List<(int y, int x1, int x2)>();
                for (int y = 0; y <= dsH; y++)
                {
                    int runStart = -1;
                    for (int x = 0; x < dsW; x++)
                    {
                        bool above = (y > 0) && dsMask[(y - 1) * dsW + x];
                        bool below = (y < dsH) && dsMask[y * dsW + x];
                        bool isEdge = above != below;

                        if (isEdge)
                        {
                            if (runStart < 0) runStart = x;
                        }
                        else
                        {
                            if (runStart >= 0)
                            {
                                hSegs.Add((y, runStart, x));
                                runStart = -1;
                            }
                        }
                    }
                    if (runStart >= 0) hSegs.Add((y, runStart, dsW));
                }

                // 垂直边缘
                var vSegs = new List<(int x, int y1, int y2)>();
                for (int x = 0; x <= dsW; x++)
                {
                    int runStart = -1;
                    for (int y = 0; y < dsH; y++)
                    {
                        bool left = (x > 0) && dsMask[y * dsW + (x - 1)];
                        bool right = (x < dsW) && dsMask[y * dsW + x];
                        bool isEdge = left != right;

                        if (isEdge)
                        {
                            if (runStart < 0) runStart = y;
                        }
                        else
                        {
                            if (runStart >= 0)
                            {
                                vSegs.Add((x, runStart, y));
                                runStart = -1;
                            }
                        }
                    }
                    if (runStart >= 0) vSegs.Add((x, runStart, dsH));
                }

                // 构建 Geometry，坐标映射回原图像素坐标
                var sg = new StreamGeometry();
                using (var ctx = sg.Open())
                {
                    foreach (var seg in hSegs)
                    {
                        // 降采样格子边界 → 原图像素坐标
                        double py = Math.Min(seg.y * factor, height) + offsetY;
                        double px1 = Math.Min(seg.x1 * factor, width) + offsetX;
                        double px2 = Math.Min(seg.x2 * factor, width) + offsetX;
                        ctx.BeginFigure(new Point(px1, py), false, false);
                        ctx.LineTo(new Point(px2, py), true, false);
                    }
                    foreach (var seg in vSegs)
                    {
                        double px = Math.Min(seg.x * factor, width) + offsetX;
                        double py1 = Math.Min(seg.y1 * factor, height) + offsetY;
                        double py2 = Math.Min(seg.y2 * factor, height) + offsetY;
                        ctx.BeginFigure(new Point(px, py1), false, false);
                        ctx.LineTo(new Point(px, py2), true, false);
                    }
                }
                sg.Freeze();
                return sg;
            }

            /// <summary>
            /// 视口裁剪后生成边缘（放大视图时使用）
            /// </summary>
            private Geometry GenerateClippedEdge(byte[] alphaMap, int width, int height,
                int offsetX, int offsetY, int clipX0, int clipY0, int clipX1, int clipY1)
            {
                int stride = width * 4;

                // 只扫描裁剪区域内的像素
                var hSegs = new List<(int y, int x1, int x2)>();
                var vSegs = new List<(int x, int y1, int y2)>();

                // 水平边缘（扫描 clipY0 到 clipY1+1 的行边界）
                for (int y = clipY0; y <= clipY1; y++)
                {
                    int runStart = -1;
                    for (int x = clipX0; x < clipX1; x++)
                    {
                        bool above = (y > 0 && y - 1 < height) && alphaMap[(y - 1) * stride + x * 4 + 3] > 128;
                        bool below = (y < height) && alphaMap[y * stride + x * 4 + 3] > 128;
                        bool isEdge = above != below;

                        if (isEdge)
                        {
                            if (runStart < 0) runStart = x;
                        }
                        else
                        {
                            if (runStart >= 0)
                            {
                                hSegs.Add((y, runStart, x));
                                runStart = -1;
                            }
                        }
                    }
                    if (runStart >= 0) hSegs.Add((y, runStart, clipX1));
                }

                // 垂直边缘
                for (int x = clipX0; x <= clipX1; x++)
                {
                    int runStart = -1;
                    for (int y = clipY0; y < clipY1; y++)
                    {
                        bool left = (x > 0 && x - 1 < width) && alphaMap[y * stride + (x - 1) * 4 + 3] > 128;
                        bool right = (x < width) && alphaMap[y * stride + x * 4 + 3] > 128;
                        bool isEdge = left != right;

                        if (isEdge)
                        {
                            if (runStart < 0) runStart = y;
                        }
                        else
                        {
                            if (runStart >= 0)
                            {
                                vSegs.Add((x, runStart, y));
                                runStart = -1;
                            }
                        }
                    }
                    if (runStart >= 0) vSegs.Add((x, runStart, clipY1));
                }

                var sg = new StreamGeometry();
                using (var ctx = sg.Open())
                {
                    foreach (var seg in hSegs)
                    {
                        ctx.BeginFigure(new Point(seg.x1 + offsetX, seg.y + offsetY), false, false);
                        ctx.LineTo(new Point(seg.x2 + offsetX, seg.y + offsetY), true, false);
                    }
                    foreach (var seg in vSegs)
                    {
                        ctx.BeginFigure(new Point(seg.x + offsetX, seg.y1 + offsetY), false, false);
                        ctx.LineTo(new Point(seg.x + offsetX, seg.y2 + offsetY), true, false);
                    }
                }
                sg.Freeze();
                return sg;
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
                var mw = ctx.ParentWindow;

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
                DrawHandles(ctx, overlay, rect, invScale, diff);
            }

            private void DrawRectangleOverlay(ToolContext ctx, Canvas overlay, Int32Rect rect, double invScale, double diff)
            {
                var mw = ctx.ParentWindow;
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
                DrawHandles(ctx, overlay, rect, invScale, diff);
            }
            private void DrawHandles(ToolContext ctx, Canvas overlay, Int32Rect rect, double invScale, double diff)
            {
                if (SelectionType != SelectionType.Rectangle) return;
                var mw = ctx.ParentWindow;

                foreach (var p in GetHandlePositions(rect))
                {
                    var handle = new System.Windows.Shapes.Rectangle
                    {
                        Width = AppConsts.SelectToolHandleSize * invScale,
                        Height = AppConsts.SelectToolHandleSize * invScale,
                        Fill = Brushes.White,
                        Stroke = mw._darkBackgroundBrush,
                        StrokeThickness = invScale
                    };
                    RenderOptions.SetEdgeMode(handle, EdgeMode.Unspecified);
                    Canvas.SetLeft(handle, p.X - AppConsts.SelectToolHandleSize * invScale / 2 + diff * 0.75);
                    Canvas.SetTop(handle, p.Y - AppConsts.SelectToolHandleSize * invScale / 2);
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
            private void DrawLassoTrace(ToolContext ctx)
            {
                var mw = ctx.ParentWindow;
                double invScale = 1 / mw.zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.Children.Clear();

                if (_lassoPoints == null || _lassoPoints.Count < 2) return;

                // 构建路径几何
                StreamGeometry geom = new StreamGeometry();
                using (StreamGeometryContext gc = geom.Open())
                {
                    gc.BeginFigure(_lassoPoints[0], false, false);
                    gc.PolyLineTo(_lassoPoints.Skip(1).ToList(), true, false);
                }

                var path = new System.Windows.Shapes.Path
                {
                    Stroke = Brushes.White,
                    StrokeThickness = 2 * invScale,
                    Data = geom,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                };
                var pathBlack = new System.Windows.Shapes.Path
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2 * invScale,
                    Data = geom,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    StrokeDashOffset = 4
                };

                overlay.Children.Add(path);
                overlay.Children.Add(pathBlack);
                overlay.Visibility = Visibility.Visible;
            }

            private void ProcessLassoSelection(ToolContext ctx)
            {
                if (_lassoPoints == null || _lassoPoints.Count < 3) { Cleanup(ctx); return; }

                // 1. 计算包围盒
                double minX = _lassoPoints.Min(p => p.X);
                double minY = _lassoPoints.Min(p => p.Y);
                double maxX = _lassoPoints.Max(p => p.X);
                double maxY = _lassoPoints.Max(p => p.Y);

                var rawRect = new Int32Rect((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
                _selectionRect = ClampRect(rawRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);

                if (_selectionRect.Width <= 0 || _selectionRect.Height <= 0) { Cleanup(ctx); return; }

                // 2. 提取原始矩形像素
                byte[] rawData = ctx.Surface.ExtractRegion(_selectionRect);
                if (rawData == null) return;

                var localPoints = _lassoPoints.Select(p => new Point(p.X - _selectionRect.X, p.Y - _selectionRect.Y)).ToList();
                StreamGeometry geom = new StreamGeometry();
                using (StreamGeometryContext gc = geom.Open())
                {
                    gc.BeginFigure(localPoints[0], true, true);
                    gc.PolyLineTo(localPoints.Skip(1).ToList(), true, true);
                }
                geom.Freeze();
                _selectionGeometry = geom;
                var visual = new DrawingVisual();
                using (DrawingContext dc = visual.RenderOpen())
                {
                    // 绘制白色形状，背景透明
                    dc.DrawGeometry(Brushes.White, null, geom);
                }
                var maskBmp = new RenderTargetBitmap(_selectionRect.Width, _selectionRect.Height, 96, 96, PixelFormats.Pbgra32);
                maskBmp.Render(visual);

                int stride = _selectionRect.Width * 4;
                _selectionAlphaMap = new byte[_selectionRect.Height * stride];
                maskBmp.CopyPixels(_selectionAlphaMap, stride, 0);

                for (int i = 0; i < rawData.Length; i += 4)
                {
                    // 遮罩Alpha通道在 i+3
                    if (_selectionAlphaMap[i + 3] < 128)
                    {
                        rawData[i + 0] = 0;
                        rawData[i + 1] = 0;
                        rawData[i + 2] = 0;
                        rawData[i + 3] = 0;
                    }
                }

                _selectionData = rawData;
                _originalRect = _selectionRect;

                CreatePreviewFromSelectionData(ctx);
            }
            private void ClearLassoRegion(ToolContext ctx, Int32Rect rect, Color color)
            {
                var clearMode = SettingsManager.Instance.Current.SelectionClearMode;
                ctx.Surface.Bitmap.Lock();
                try
                {
                    unsafe
                    {
                        byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                        int stride = ctx.Surface.Bitmap.BackBufferStride;
                        int maskStride = _selectionRect.Width * 4; // 遮罩的 stride

                        // 预计算填充色
                        byte tB = 0, tG = 0, tR = 0, tA = 0;
                        bool writeAlpha = true;
                        if (clearMode == SelectionClearMode.Transparent) { tB = 0; tG = 0; tR = 0; tA = 0; writeAlpha = true; }
                        else if (clearMode == SelectionClearMode.White) { tB = 255; tG = 255; tR = 255; tA = 255; writeAlpha = true; }

                        // 遍历区域
                        for (int y = 0; y < rect.Height; y++)
                        {
                            // 注意边界检查，防止遮罩和实际rect尺寸微小差异导致越界
                            if (y * maskStride >= _selectionAlphaMap.Length) break;

                            byte* rowPtr = basePtr + (rect.Y + y) * stride + rect.X * 4;

                            for (int x = 0; x < rect.Width; x++)
                            {
                                int maskIndex = y * maskStride + x * 4 + 3; // Alpha通道
                                if (maskIndex < _selectionAlphaMap.Length)
                                {
                                    // 只有当遮罩显示“此处被选中”（Alpha > 128）时，才清除画布上的像素
                                    if (_selectionAlphaMap[maskIndex] > 128)
                                    {
                                        rowPtr[0] = tB;
                                        rowPtr[1] = tG;
                                        rowPtr[2] = tR;
                                        if (writeAlpha) rowPtr[3] = tA;
                                    }
                                }
                                rowPtr += 4;
                            }
                        }
                    }
                    ctx.Surface.Bitmap.AddDirtyRect(rect);
                }
                finally
                {
                    ctx.Surface.Bitmap.Unlock();
                }
            }

            private void RunMagicWand(ToolContext ctx, Point startPt, int tolerance, bool union)
            {
                int w = ctx.Surface.Bitmap.PixelWidth;
                int h = ctx.Surface.Bitmap.PixelHeight;
                int startX = (int)startPt.X;
                int startY = (int)startPt.Y;

                if (startX < 0 || startX >= w || startY < 0 || startY >= h) return;

                // 1. 准备全图 Mask (bool array)
                // 如果是 Shift 追加模式，我们需要保留之前的选中状态
                bool[] mask = new bool[w * h];

                if (union && _selectionData != null && _selectionAlphaMap != null)
                {
                    // 将旧选区恢复到 mask 中
                    int oldX = _selectionRect.X;
                    int oldY = _selectionRect.Y;
                    int oldW = _selectionRect.Width;
                    int oldH = _selectionRect.Height;
                    int oldStride = oldW * 4;
                    for (int y = 0; y < oldH; y++)
                    {
                        for (int x = 0; x < oldW; x++)
                        {
                            int alphaIndex = y * oldStride + x * 4 + 3;
                            if (alphaIndex < _selectionAlphaMap.Length && _selectionAlphaMap[alphaIndex] > 128)
                            {
                                int globalX = oldX + x;
                                int globalY = oldY + y;
                                if (globalX >= 0 && globalX < w && globalY >= 0 && globalY < h)
                                {
                                    mask[globalY * w + globalX] = true;
                                }
                            }
                        }
                    }
                }
                ctx.Surface.Bitmap.Lock();     // 2. 执行泛洪填充 (BFS)
                try
                {
                    unsafe
                    {
                        byte* ptr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                        int stride = ctx.Surface.Bitmap.BackBufferStride;

                        // 获取目标颜色 (B, G, R, A)
                        byte* startPx = ptr + startY * stride + startX * 4;
                        byte targetB = startPx[0];
                        byte targetG = startPx[1];
                        byte targetR = startPx[2];
                        byte targetA = startPx[3];

                        Queue<int> q = new Queue<int>();
                        q.Enqueue(startX + startY * w);

                        // 如果起始点还没被选中，才开始Fill
                        if (!mask[startX + startY * w])
                        {
                            mask[startX + startY * w] = true; // 标记访问

                            // 4-邻域偏移
                            int[] dx = { 0, 0, 1, -1 };
                            int[] dy = { 1, -1, 0, 0 };

                            while (q.Count > 0)
                            {
                                int curr = q.Dequeue();
                                int cx = curr % w;
                                int cy = curr / w;

                                for (int i = 0; i < 4; i++)
                                {
                                    int nx = cx + dx[i];
                                    int ny = cy + dy[i];

                                    if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                                    {
                                        int nIdx = nx + ny * w;
                                        if (!mask[nIdx]) // 未访问过
                                        {
                                            byte* currPtr = ptr + ny * stride + nx * 4;
                                            byte b = currPtr[0];
                                            byte g = currPtr[1];
                                            byte r = currPtr[2];
                                            byte a = currPtr[3];
                                            bool match = (Math.Abs(b - targetB) <= tolerance) &&
                                                         (Math.Abs(g - targetG) <= tolerance) &&
                                                         (Math.Abs(r - targetR) <= tolerance) &&
                                                         (Math.Abs(a - targetA) <= tolerance);

                                            if (match)
                                            {
                                                mask[nIdx] = true;
                                                q.Enqueue(nIdx);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    ctx.Surface.Bitmap.Unlock();
                }

                // 3. 计算新的包围盒
                int minX = w, maxX = 0, minY = h, maxY = 0;
                bool hasSelection = false;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (mask[y * w + x])
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            hasSelection = true;
                        }
                    }
                }

                if (!hasSelection)
                {
                    Cleanup(ctx);
                    return;
                }

                // 4. 生成 _selectionData 和 _selectionAlphaMap
                int newW = maxX - minX + 1;
                int newH = maxY - minY + 1;
                _selectionRect = new Int32Rect(minX, minY, newW, newH);
                _originalRect = _selectionRect;

                // 提取原始像素数据
                byte[] rawData = ctx.Surface.ExtractRegion(_selectionRect);

                // 生成 AlphaMap (BGRA格式)
                int mapStride = newW * 4;
                _selectionAlphaMap = new byte[newH * mapStride];

                for (int y = 0; y < newH; y++)
                {
                    for (int x = 0; x < newW; x++)
                    {
                        int globalX = minX + x;
                        int globalY = minY + y;
                        bool selected = mask[globalY * w + globalX];

                        int pixelIdx = y * mapStride + x * 4;

                        // 设置 AlphaMap: 选中则 Alpha=255, 否则 0
                        _selectionAlphaMap[pixelIdx + 0] = 0; // B
                        _selectionAlphaMap[pixelIdx + 1] = 0; // G
                        _selectionAlphaMap[pixelIdx + 2] = 0; // R
                        _selectionAlphaMap[pixelIdx + 3] = selected ? (byte)255 : (byte)0; // A
                        if (!selected)
                        {   // 同时处理 _selectionData：未选中区域设为透明
                            rawData[pixelIdx + 0] = 0;
                            rawData[pixelIdx + 1] = 0;
                            rawData[pixelIdx + 2] = 0;
                            rawData[pixelIdx + 3] = 0;
                        }
                    }
                }

                _selectionData = rawData;
                _hasLifted = false;
                CreatePreviewFromSelectionData(ctx);
            }

            public WriteableBitmap GetSelectionBoundingBoxBitmap(ToolContext ctx)
            {
                if (_selectionData == null || _originalRect.Width <= 0 || _originalRect.Height <= 0)
                    return null;

                try
                {
                    // 从画布上提取包围盒区域的完整像素（包含套索外的像素）
                    var clampedRect = ClampRect(_originalRect,
                        ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);

                    if (clampedRect.Width <= 0 || clampedRect.Height <= 0) return null;

                    byte[] fullPixels = ctx.Surface.ExtractRegion(clampedRect);
                    if (fullPixels == null) return null;

                    // 如果已经 lift 了，需要把选区数据合成回去，
                    // 因为画布上已经被清除了
                    if (_hasLifted && _selectionData != null)
                    {
                        int stride = clampedRect.Width * 4;
                        for (int i = 0; i < fullPixels.Length && i < _selectionData.Length; i += 4)
                        {
                            byte srcA = _selectionData[i + 3];
                            if (srcA > 0)
                            {
                                // Alpha 合成：选区数据覆盖到完整像素上
                                if (srcA == 255)
                                {
                                    fullPixels[i + 0] = _selectionData[i + 0];
                                    fullPixels[i + 1] = _selectionData[i + 1];
                                    fullPixels[i + 2] = _selectionData[i + 2];
                                    fullPixels[i + 3] = _selectionData[i + 3];
                                }
                                else
                                {
                                    float a = srcA / 255f;
                                    float invA = 1f - a;
                                    fullPixels[i + 0] = (byte)(a * _selectionData[i + 0] + invA * fullPixels[i + 0]);
                                    fullPixels[i + 1] = (byte)(a * _selectionData[i + 1] + invA * fullPixels[i + 1]);
                                    fullPixels[i + 2] = (byte)(a * _selectionData[i + 2] + invA * fullPixels[i + 2]);
                                    fullPixels[i + 3] = (byte)Math.Min(255, srcA + fullPixels[i + 3] * invA);
                                }
                            }
                        }
                    }

                    var wb = new WriteableBitmap(clampedRect.Width, clampedRect.Height,
                        96, 96, PixelFormats.Bgra32, null);
                    wb.WritePixels(new Int32Rect(0, 0, clampedRect.Width, clampedRect.Height),
                        fullPixels, clampedRect.Width * 4, 0);
                    return wb;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("GetSelectionBoundingBoxBitmap error: " + ex.Message);
                    return null;
                }
            }

            /// <summary>
            /// 是否是不规则选区（套索或魔棒）
            /// </summary>
            public bool IsIrregularSelection =>
                SelectionType == SelectionType.Lasso || SelectionType == SelectionType.MagicWand;

            /// <summary>
            /// 获取选区的 alpha mask 副本（外部只读用途）
            /// </summary>
            public byte[] GetSelectionAlphaMapCopy()
            {
                if (_selectionAlphaMap == null) return null;
                byte[] copy = new byte[_selectionAlphaMap.Length];
                Buffer.BlockCopy(_selectionAlphaMap, 0, copy, 0, _selectionAlphaMap.Length);
                return copy;
            }

            /// <summary>
            /// 将 AI 结果与套索/魔棒 mask 做交集后替换选区数据
            /// </summary>
            public void ReplaceSelectionDataWithMask(ToolContext ctx, byte[] aiResultPixels, int w, int h)
            {
                if (aiResultPixels == null || _selectionAlphaMap == null) return;

                int stride = w * 4;
                int expectedLength = h * stride;

                // 安全检查
                if (aiResultPixels.Length < expectedLength || _selectionAlphaMap.Length < expectedLength)
                {
                    // 尺寸不匹配，回退到普通替换
                    ReplaceSelectionData(ctx, aiResultPixels, w, h);
                    return;
                }

                // 将 AI 结果与原始 mask 做交集：
                // 只保留原套索/魔棒选中区域内的 AI 抠图结果
                byte[] maskedResult = new byte[expectedLength];
                Buffer.BlockCopy(aiResultPixels, 0, maskedResult, 0, expectedLength);

                System.Threading.Tasks.Parallel.For(0, h, y =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * stride + x * 4;
                        byte maskAlpha = _selectionAlphaMap[i + 3];

                        if (maskAlpha <= 128)
                        {
                            // 套索/魔棒外的区域：完全透明
                            maskedResult[i + 0] = 0;
                            maskedResult[i + 1] = 0;
                            maskedResult[i + 2] = 0;
                            maskedResult[i + 3] = 0;
                        }
                        // 套索内的区域：保留 AI 的结果（AI 已经做了前景/背景分离）
                    }
                });

                // 更新 alpha map 为交集结果
                _selectionAlphaMap = new byte[expectedLength];
                System.Threading.Tasks.Parallel.For(0, h, y =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * stride + x * 4;
                        _selectionAlphaMap[i + 3] = maskedResult[i + 3];
                    }
                });

                // 调用基础替换方法更新预览
                _selectionData = maskedResult;
                _selectionRect = new Int32Rect(_selectionRect.X, _selectionRect.Y, w, h);
                _originalRect = _selectionRect;
                _transformStep = 0;

                CreatePreviewFromSelectionData(ctx);

                var mw = ctx.ParentWindow;
                mw.SelectionSize = $"{w}×{h}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
                mw.UpdateSelectionToolBarPosition();
            }

        }
    }
}