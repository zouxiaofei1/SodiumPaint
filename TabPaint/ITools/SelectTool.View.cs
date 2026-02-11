using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

//
//SelectTool类的渲染实现
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class SelectTool : ToolBase
        {
            public void Cleanup(ToolContext ctx)
            {
                HidePreview(ctx);
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                var mw = ctx.ParentWindow;
                if (mw._canvasResizer != null)   mw._canvasResizer.SetHandleVisibility(true);
        
                _originalRect = new Int32Rect();
                _selectionRect = new Int32Rect();
                _selecting = false;
                IsPasted = false;
                _draggingSelection = false;
                _resizing = false;
                _currentAnchor = ResizeAnchor.None;
                _selectionData = null;
                _selectionAlphaMap = null;  // 添加
                _selectionGeometry = null;  // 添加
                _isWandAdjusting = false;   // 添加
                lag = 0; ctx.ParentWindow.UpdateSelectionToolBarPosition(); ctx.ParentWindow.ClearRulerSelection();
            }

            public void RefreshOverlay(ToolContext ctx)
            {
                if (_selectionRect.Width > 0 && _selectionRect.Height > 0)   DrawOverlay(ctx, _selectionRect);
            }


            public void DrawOverlay(ToolContext ctx, Int32Rect rect)
            {
             
                var mw = ctx.ParentWindow;
                if (mw._canvasResizer != null)  mw._canvasResizer.SetHandleVisibility(false);
                double invScale = 1 / mw.zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.ClipToBounds = false;
                overlay.Children.Clear();

                double diff = mw.CanvasWrapper.RenderSize.Width -
                              (int)mw.CanvasWrapper.RenderSize.Width;

                if (SelectionType == SelectionType.Lasso && _selectionGeometry != null)
                {
                    DrawIrregularContour(ctx, overlay, _selectionGeometry, rect, invScale, diff);
                }

                else if (SelectionType == SelectionType.MagicWand && _selectionAlphaMap != null && !_isWandAdjusting)
                {
                    // 生成轮廓 Geometry
                    var wandGeometry = GeneratePixelEdgeGeometry(
                        _selectionAlphaMap,
                        rect.Width,
                        rect.Height,
                        rect.X,
                        rect.Y);

                    if (wandGeometry != null)
                    {
                        _selectionGeometry = wandGeometry; // 缓存起来供后续使用
                        DrawIrregularContour(ctx, overlay, wandGeometry, rect, invScale, diff, false);
                    }
                    else DrawRectangleOverlay(ctx, overlay, rect, invScale, diff);// 回退到矩形框
                }
                else if (SelectionType == SelectionType.MagicWand && _isWandAdjusting)
                {
                    DrawWandPreviewMask(ctx, overlay, rect, invScale);
                }
                else
                {
                    DrawRectangleOverlay(ctx, overlay, rect, invScale, diff);
                }

                mw.UpdateSelectionScalingMode();
                ctx.SelectionOverlay.IsHitTestVisible = false;
                ctx.SelectionOverlay.Visibility = Visibility.Visible;
            }
            public Int32Rect GetSelectionRect() => _selectionRect;
            public ResizeAnchor HitTestHandle(Point px, Int32Rect rect)
            {
                double size = AppConsts.SelectToolHandleSize / ctxForTimer.ParentWindow.zoomscale; // 句柄大小
                double x1 = rect.X;
                double y1 = rect.Y;
                double x2 = rect.X + rect.Width;
                double y2 = rect.Y + rect.Height;
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

            private List<Point> GetHandlePositions(Int32Rect rect)
            {
                var handles = new List<Point>();
                double x1 = rect.X;
                double y1 = rect.Y;
                double x2 = rect.X + rect.Width;
                double y2 = rect.Y + rect.Height;
                double mx = (x1 + x2) / 2;
                double my = (y1 + y2) / 2;

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

            public void ClearSelections(ToolContext ctx)
            {
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                _resizing = false;
                _draggingSelection = false;
                _selecting = false;
                _currentAnchor = ResizeAnchor.None;
                _selectionRect.Width = _selectionRect.Height = 0;
            }


            private void SetPreviewPosition(ToolContext ctx, int pixelX, int pixelY)
            {
                ctx.SelectionPreview.UseLayoutRounding = false;
                var bitmap = ctx.Surface.Bitmap;
                ctx.SelectionPreview.Stretch = System.Windows.Media.Stretch.Fill;

                double scaleX = AppConsts.StandardDpi / bitmap.DpiX;
                double scaleY = AppConsts.StandardDpi / bitmap.DpiY;

                ctx.SelectionPreview.Width = _selectionRect.Width * scaleX;
                ctx.SelectionPreview.Height = _selectionRect.Height * scaleY;


              double diff = ctx.ParentWindow.CanvasWrapper.RenderSize.Width - (int)ctx.ParentWindow.CanvasWrapper.RenderSize.Width;
                // 计算位移
                double localX = pixelX * scaleX+diff*0.75 ;
                double localY = pixelY * scaleY;
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(localX, localY);
            }


            private void DeleteFileWithDelay(string filePath, int delayMilliseconds)
            {
                // 使用 Task.Run 启动一个后台线程，不阻塞 UI
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(delayMilliseconds);
                        if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                    }
                    catch (Exception ex){}
                });
            }
            private void ResetPreviewState(ToolContext ctx)
            {
                ctx.SelectionPreview.Visibility = Visibility.Collapsed;
                ctx.SelectionPreview.Source = null;
                ctx.SelectionPreview.RenderTransform = Transform.Identity;
                ctx.SelectionPreview.Clip = null;
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);
                _transformStep = 0;
                _originalRect = new Int32Rect();
            }
            public void GiveUpSelection(ToolContext ctx)
            {
                if (ctx == null) return;
                CommitSelection(ctx);
                Cleanup(ctx);
                ctx.Undo.Undo(); ctx.ParentWindow.UpdateSelectionToolBarPosition();
            }
            public void CommitSelection(ToolContext ctx, bool shape = false)
            {
              
                if (_selectionData == null) return;
                // 1. 准备撤销记录
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_selectionRect);

                // 2. 准备源数据 (处理缩放)
                byte[] finalData = _selectionData;
                int finalWidth = _selectionRect.Width;
                int finalHeight = _selectionRect.Height;
                int finalStride = finalWidth * 4;
                if (_originalRect.Width != _selectionRect.Width || _originalRect.Height != _selectionRect.Height)
                {
                    if (_originalRect.Width <= 0 || _originalRect.Height <= 0) return;
                    int expectedStride = _originalRect.Width * 4;
                    int actualStride = _selectionData.Length / _originalRect.Height;
                    int dataStride = Math.Min(expectedStride, actualStride);

                    // 创建原始 BitmapSource
                    var src = BitmapSource.Create(
                        _originalRect.Width, _originalRect.Height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _selectionData, dataStride);
                    var resized = ctx.ParentWindow.ResampleBitmap(src, finalWidth, finalHeight);

                    // 提取像素数据
                    finalStride = resized.PixelWidth * 4;
                    finalData = new byte[finalHeight * finalStride];
                    resized.CopyPixels(finalData, finalStride, 0);
                }
                // 3. 执行透明度混合写入 (Alpha Blending)
                BlendPixels(ctx.Surface.Bitmap, _selectionRect.X, _selectionRect.Y, finalWidth, finalHeight, finalData, finalStride);

                ctx.Undo.CommitStroke(shape ? UndoActionType.Draw : UndoActionType.Selection);   //清理
                HidePreview(ctx); IsPasted = false;
                _selectionData = null;
                ctx.IsDirty = true;
                lag = 1;
                _transformStep = 0;
                _originalRect = new Int32Rect();
                var mw = ctx.ParentWindow;
                mw.SetUndoRedoButtonState();
                ResetPreviewState(ctx);
                mw.NotifyCanvasChanged();
                mw.UpdateSelectionToolBarPosition();
            }
            private void BlendPixels(WriteableBitmap targetBmp, int x, int y, int w, int h, byte[] sourcePixels, int sourceStride)
            {
                targetBmp.Lock();
                try
                {
                    int targetW = targetBmp.PixelWidth;
                    int targetH = targetBmp.PixelHeight;

                    int drawX = Math.Max(0, x);
                    int drawY = Math.Max(0, y);
                    int right = Math.Min(targetW, x + w);
                    int bottom = Math.Min(targetH, y + h);
                    int drawW = right - drawX;
                    int drawH = bottom - drawY;

                    if (drawW <= 0 || drawH <= 0) return;

                    int srcOffsetX = drawX - x;
                    int srcOffsetY = drawY - y;

                    unsafe
                    {
                        byte* pTargetBase = (byte*)targetBmp.BackBuffer;
                        int targetStride = targetBmp.BackBufferStride;

                        for (int r = 0; r < drawH; r++)
                        {
                            long srcRowIndex = (long)(srcOffsetY + r) * sourceStride + (long)srcOffsetX * 4;
                            byte* pTargetRow = pTargetBase + (drawY + r) * targetStride + drawX * 4;

                            for (int c = 0; c < drawW; c++)
                            {
                                if (srcRowIndex + c * 4 + 3 >= sourcePixels.Length) break;

                                byte srcB = sourcePixels[srcRowIndex + c * 4 + 0];
                                byte srcG = sourcePixels[srcRowIndex + c * 4 + 1];
                                byte srcR = sourcePixels[srcRowIndex + c * 4 + 2];
                                byte srcA = sourcePixels[srcRowIndex + c * 4 + 3];
                                if (srcA == 0)
                                {
                                    pTargetRow += 4;
                                    continue;
                                }
                                if (srcA == 255)
                                {
                                    pTargetRow[0] = srcB;
                                    pTargetRow[1] = srcG;
                                    pTargetRow[2] = srcR;
                                    pTargetRow[3] = 255;
                                }
                                else
                                {
                                    byte dstB = pTargetRow[0];
                                    byte dstG = pTargetRow[1];
                                    byte dstR = pTargetRow[2];
                                    byte dstA = pTargetRow[3];
                                    float sa = srcA / 255.0f;       // 归一化 Alpha (0.0 - 1.0)
                                    float da = dstA / 255.0f;
                                    float outA = sa + da * (1.0f - sa);    // 计算最终 Alpha: a_out = as + ad * (1 - as)
                                    if (outA > 0)
                                    { // 如果最终透明度为0（理论上不会进这里因为 srcA>0），直接跳过
                                        float factorDest = da * (1.0f - sa);
                                        pTargetRow[0] = (byte)((srcB * sa + dstB * factorDest) / outA);
                                        pTargetRow[1] = (byte)((srcG * sa + dstG * factorDest) / outA);
                                        pTargetRow[2] = (byte)((srcR * sa + dstR * factorDest) / outA);
                                        pTargetRow[3] = (byte)(outA * 255.0f);
                                    }
                                }
                                pTargetRow += 4;
                            }
                        }
                    }
                    targetBmp.AddDirtyRect(new Int32Rect(drawX, drawY, drawW, drawH));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("BlendPixels Error: " + ex.Message);
                }
                finally
                {
                    targetBmp.Unlock();
                }
            }
            private void HidePreview(ToolContext ctx)
            {
                var mw = ctx.ParentWindow;
                if (mw._canvasResizer != null)mw._canvasResizer.SetHandleVisibility(true);
                ctx.SelectionPreview.Visibility = Visibility.Collapsed;
            }



            public bool IsPointInSelection(Point px)
            {
                // 1. 先检查包围盒
                bool inRect = px.X >= _selectionRect.X &&
                              px.X < _selectionRect.X + _selectionRect.Width &&
                              px.Y >= _selectionRect.Y &&
                              px.Y < _selectionRect.Y + _selectionRect.Height;

                if (!inRect) return false;
                if (SelectionType == SelectionType.Rectangle) return true;
                if (_transformStep > 0) return true;

                // 4. 未变换过：精确检查 AlphaMap
                if (_selectionAlphaMap != null)
                {
                    int localX = (int)(px.X - _selectionRect.X);
                    int localY = (int)(px.Y - _selectionRect.Y);
                    int stride = _selectionRect.Width * 4;

                    int index = localY * stride + localX * 4 + 3;

                    if (index >= 0 && index < _selectionAlphaMap.Length)
                    {
                        return _selectionAlphaMap[index] > 10;
                    }
                }

                // 5. 兜底：如果 Geometry 存在，用它判断
                if (_selectionGeometry != null)
                    return _selectionGeometry.FillContains(px);

                return true;
            }

            private void ClearRect(ToolContext ctx, Int32Rect rect, Color color)
            {
                // 获取当前设置
                var clearMode = SettingsManager.Instance.Current.SelectionClearMode;

                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;
                    byte targetB = 0, targetG = 0, targetR = 0, targetA = 0; // 预先计算好要写入的值，避免在循环中判断
                    bool writeAlpha = true;

                    switch (clearMode)
                    {
                        case SelectionClearMode.Transparent:// 全0
                            targetB = 0; targetG = 0; targetR = 0; targetA = 0;
                            writeAlpha = true;
                            break;
                        case SelectionClearMode.White:
                            targetB = 255; targetG = 255; targetR = 255; targetA = 255; // 全255
                            writeAlpha = true;
                            break;
                        case SelectionClearMode.PreserveAlpha:
                            targetB = color.B; targetG = color.G; targetR = color.R;    // 使用传入的 color (通常是背景色) 的RGB，但不改写 A
                            writeAlpha = false;
                            break;
                    }

                    // 针对不同模式优化循环
                    for (int y = rect.Y; y < rect.Y + rect.Height; y++)
                    {
                        byte* rowPtr = basePtr + y * stride + rect.X * 4;

                        if (writeAlpha)
                        {
                            for (int x = 0; x < rect.Width; x++)
                            {
                                rowPtr[0] = targetB;
                                rowPtr[1] = targetG;
                                rowPtr[2] = targetR;
                                rowPtr[3] = targetA;
                                rowPtr += 4;
                            }
                        }
                        else
                        {
                            for (int x = 0; x < rect.Width; x++)
                            {
                                rowPtr[0] = targetB;
                                rowPtr[1] = targetG;
                                rowPtr[2] = targetR;// 跳过 Alpha
                                rowPtr += 4;
                            }
                        }
                    }
                }
                // 标记脏区域以更新 UI
                var pixelWidth = ctx.Surface.Bitmap.PixelWidth;
                var pixelHeight = ctx.Surface.Bitmap.PixelHeight;
                ctx.Surface.Bitmap.AddDirtyRect(ClampRect(rect, pixelWidth, pixelHeight));

                ctx.Surface.Bitmap.Unlock();
            }

        }
    }
}