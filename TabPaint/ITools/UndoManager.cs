
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void Undo()
        {

            _undo.Undo(); _ctx.IsDirty = true;
            SetUndoRedoButtonState();
            _canvasResizer.UpdateUI();

        }
        private void Redo()
        {
            _undo.Redo(); _ctx.IsDirty = true;
            SetUndoRedoButtonState();
            _canvasResizer.UpdateUI();
        }

        public class UndoAction
        {
            public Int32Rect Rect { get; }
            public byte[] Pixels { get; }
            public Int32Rect UndoRect { get; }      // 撤销时恢复的尺寸
            public byte[] UndoPixels { get; }       // 撤销时恢复的像素
            public Int32Rect RedoRect { get; }      // 重做时恢复的尺寸
            public byte[] RedoPixels { get; }       // 重做时恢复的像素
            public UndoActionType ActionType { get; }
            public string DeletedFilePath { get; }
            public FileTabItem DeletedTab { get; }
            public int DeletedTabIndex { get; }
            public UndoAction(string filePath, FileTabItem tab, int index)
            {
                ActionType = UndoActionType.FileDelete;
                DeletedFilePath = filePath;
                DeletedTab = tab;
                DeletedTabIndex = index;
            }
            public UndoAction(Int32Rect rect, byte[] pixels, UndoActionType actionType = UndoActionType.Draw)
            {
                ActionType = actionType;
                Rect = rect;
                Pixels = pixels;
            }
            public UndoAction(Int32Rect undoRect, byte[] undoPixels, Int32Rect redoRect, byte[] redoPixels)
            {
                ActionType = UndoActionType.Transform;
                UndoRect = undoRect;
                UndoPixels = undoPixels;
                RedoRect = redoRect;
                RedoPixels = redoPixels;
            }
        }
        public class UndoRedoManager
        {
            private readonly CanvasSurface _surface;
            public readonly Stack<UndoAction> _undo = new();
            public readonly Stack<UndoAction> _redo = new();
            private byte[]? _preStrokeSnapshot;
            private readonly List<Int32Rect> _strokeRects = new();
            public int UndoCount => _undo.Count;
            private void UpdateUI()
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                mw.SetUndoRedoButtonState(); // 更新按钮可用性
                mw.CheckDirtyState();        // 更新红点状态
                mw.UpdateWindowTitle();
            }
            public UndoRedoManager(CanvasSurface surface) { _surface = surface; }

            public bool CanUndo => _undo.Count > 0;
            public bool CanRedo => _redo.Count > 0;
            public void PushTransformAction(Int32Rect undoRect, byte[] undoPixels, Int32Rect redoRect, byte[] redoPixels)
            {//自动SetUndoRedoButtonState和_redo.Clear()
                _undo.Push(new UndoAction(undoRect, undoPixels, redoRect, redoPixels));
                _redo.Clear(); // 新操作截断重做链
                UpdateUI();
            }
            // ---------- 绘制操作 ----------
            public void BeginStroke()
            {
                if (_surface?.Bitmap == null) return;

                int bytes = _surface.Bitmap.BackBufferStride * _surface.Height;
                _preStrokeSnapshot = new byte[bytes];
                _surface.Bitmap.Lock();
                System.Runtime.InteropServices.Marshal.Copy(_surface.Bitmap.BackBuffer, _preStrokeSnapshot, 0, bytes);
                _surface.Bitmap.Unlock();
                _strokeRects.Clear();
                _redo.Clear(); // 新操作截断重做链
            }

            public void AddDirtyRect(Int32Rect rect) => _strokeRects.Add(rect);

            public void CommitStroke(UndoActionType undoActionType = UndoActionType.Draw)//一般绘画
            {
               
                if (_preStrokeSnapshot == null || _strokeRects.Count == 0 || _surface?.Bitmap == null)
                {
                    _preStrokeSnapshot = null;
                    return;
                }
                var combined = ClampRect(CombineRects(_strokeRects), ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelWidth, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelHeight);

                byte[] region = ExtractRegionFromSnapshot(_preStrokeSnapshot, combined, _surface.Bitmap.BackBufferStride);
                _undo.Push(new UndoAction(combined, region, undoActionType));
                UpdateUI();
                _preStrokeSnapshot = null;

                ((MainWindow)System.Windows.Application.Current.MainWindow).NotifyCanvasChanged();
            }

            public void internalUndoAction(UndoAction action)
            {
                var redoPixels = _surface.ExtractRegion(action.Rect);
                _redo.Push(new UndoAction(action.Rect, redoPixels));
                // 执行 Undo
                _surface.WriteRegion(action.Rect, action.Pixels);
            }
            // ---------- 撤销 / 重做 ----------
            public void Undo()
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (mw._deleteCommitTimer.IsEnabled && mw._pendingDeletionTabs.Count > 0)
                {
                    mw.RestoreLastDeletedTab();
                    return; // 拦截成功，不再执行画布撤销
                }
                // ------------------------------------

                // 下面是原有的画布撤销逻辑
                if (_undo != null) // 确保 UndoManager 存在
                {
                    ImageUndo();
                }
            }
            public void ImageUndo()
            {
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

    

                // === 原有逻辑：处理选区清理 ===
                if (mw._router.CurrentTool is SelectTool sselTool && sselTool.HasActiveSelection)
                {
                    if (!sselTool._hasLifted)
                    {
                        sselTool.Cleanup(mw._ctx);
                        mw.SetCropButtonState();
                        return;
                    }
                }

                if (!CanUndo || _surface?.Bitmap == null) return;

                // === 原有逻辑：从栈中取出动作 ===
                var action = _undo.Pop();

                // 如果 action 是 FileDelete 类型（如果你选择将其入栈）
                if (action.ActionType == UndoActionType.FileDelete)
                {
                    // 这里可以执行恢复逻辑，但建议使用下面提到的 Timer 方案更稳定
                }

                if (mw._router.CurrentTool is SelectTool selTool) selTool.Cleanup(mw._ctx);

                if (action.ActionType == UndoActionType.Transform)
                {
                    // ... (保持你原有的 Transform 逻辑不变) ...
                    var currentRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight);
                    var currentPixels = _surface.ExtractRegion(currentRect);
                    _redo.Push(new UndoAction(currentRect, currentPixels, action.RedoRect, action.RedoPixels));

                    var wb = new WriteableBitmap(action.UndoRect.Width, action.UndoRect.Height,
                            mw._ctx.Surface.Bitmap.DpiX, mw._ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                    wb.WritePixels(action.UndoRect, action.UndoPixels, wb.BackBufferStride, 0);
                    _surface.ReplaceBitmap(wb);
                    mw.NotifyCanvasSizeChanged(action.UndoRect.Width, action.UndoRect.Height);
                }
                else // Draw Action
                {
                    internalUndoAction(action);
                    if (action.ActionType == UndoActionType.Selection && _undo.Count > 0)
                    {
                        var pairedAction = _undo.Pop();
                        internalUndoAction(pairedAction);
                    }
                }

                UpdateUI();
                mw.NotifyCanvasChanged();
            }


            public void Redo()
            {
                if (!CanRedo || _surface?.Bitmap == null) return;

                var action = _redo.Pop();

                if (action.ActionType == UndoActionType.Transform)
                {
                    // 1. 准备对应的 Undo Action
                    var currentRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight);
                    var currentPixels = _surface.ExtractRegion(currentRect);

                    _undo.Push(new UndoAction(
                        currentRect,       // 撤销这个 Redo 会回到当前状态
                        currentPixels,
                        action.RedoRect,   // 执行这个 Redo 会回到裁剪后的状态
                        action.RedoPixels
                    ));
                    var wb = new WriteableBitmap(action.RedoRect.Width, action.RedoRect.Height,
                            ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.Bitmap.DpiX, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);
                    wb.WritePixels(action.RedoRect, action.RedoPixels, wb.BackBufferStride, 0);
                    // 替换主位图
                    _surface.ReplaceBitmap(wb);
                }
                else // Draw Action
                {
                    // 准备 Undo Action
                    var undoPixels = _surface.ExtractRegion(action.Rect);
                    _undo.Push(new UndoAction(action.Rect, undoPixels));
                    _surface.WriteRegion(action.Rect, action.Pixels);
                }

                UpdateUI();
                ((MainWindow)System.Windows.Application.Current.MainWindow).NotifyCanvasChanged();
            }
            public void PushFullImageUndo()
            { // ---------- 供整图操作调用 ----------
                if (_surface?.Bitmap == null) return; /// 在整图变换(旋转/翻转/新建)之前，准备一个完整快照并保存redo像素

                var rect = new Int32Rect(0, 0,
                    _surface.Bitmap.PixelWidth,
                    _surface.Bitmap.PixelHeight);

                var currentPixels = SafeExtractRegion(rect);
                _undo.Push(new UndoAction(rect, currentPixels));
                _redo.Clear();
            }
            private static Int32Rect CombineRects(List<Int32Rect> rects)
            {
                int minX = rects.Min(r => r.X);
                int minY = rects.Min(r => r.Y);
                int maxX = rects.Max(r => r.X + r.Width);
                int maxY = rects.Max(r => r.Y + r.Height);
                return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
            }
            private static byte[] ExtractRegionFromSnapshot(byte[] fullData, Int32Rect rect, int stride)
            {

                byte[] region = new byte[rect.Width * rect.Height * 4];
                for (int row = 0; row < rect.Height; row++)
                {
                    int srcOffset = (rect.Y + row) * stride + rect.X * 4;
                    Buffer.BlockCopy(fullData, srcOffset, region, row * rect.Width * 4, rect.Width * 4);
                }
                return region;
            }

            public byte[] SafeExtractRegion(Int32Rect rect)
            {
                // 检查合法范围，防止尺寸变化导致越界
                if (rect.X < 0 || rect.Y < 0 ||
                    rect.X + rect.Width > _surface.Bitmap.PixelWidth ||
                    rect.Y + rect.Height > _surface.Bitmap.PixelHeight ||
                    rect.Width <= 0 || rect.Height <= 0)
                {
                    // 返回当前整图快照（安全退化）
                    int bytes = _surface.Bitmap.BackBufferStride * _surface.Bitmap.PixelHeight;
                    byte[] data = new byte[bytes];
                    _surface.Bitmap.Lock();
                    System.Runtime.InteropServices.Marshal.Copy(_surface.Bitmap.BackBuffer, data, 0, bytes);
                    _surface.Bitmap.Unlock();
                    return data;
                }

                return _surface.ExtractRegion(rect);
            }

            // 清空重做链
            public void ClearRedo()
            {
                _redo.Clear();
            }
            public void ClearUndo()
            {
                _undo.Clear();
            }
        }
    }
}