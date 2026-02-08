
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TabPaint主程序
//

namespace TabPaint
{
    public static class ListStackExtensions
    {
        public static void Push<T>(this List<T> list, T item)
        {
            list.Add(item);
        }
        public static T Pop<T>(this List<T> list)
        {
            if (list.Count == 0) throw new InvalidOperationException("Stack is empty");
            T last = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return last;
        }
        public static T Peek<T>(this List<T> list)
        {
            if (list.Count == 0) throw new InvalidOperationException("Stack is empty");
            return list[list.Count - 1];
        }
    }

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
            public long MemorySize { get; } // 估算内存占用 (Bytes)
            public DateTime Timestamp { get; } = DateTime.Now;

            public UndoAction(string filePath, FileTabItem tab, int index)
            {
                ActionType = UndoActionType.FileDelete;
                DeletedFilePath = filePath;
                DeletedTab = tab;
                DeletedTabIndex = index;
                MemorySize = 0;
            }
            public UndoAction(Int32Rect rect, byte[] pixels, UndoActionType actionType = UndoActionType.Draw)
            {
                ActionType = actionType;
                Rect = rect;
                Pixels = pixels;
                MemorySize = (pixels?.Length ?? 0);
            }
            public UndoAction(Int32Rect undoRect, byte[] undoPixels, Int32Rect redoRect, byte[] redoPixels)
            {
                ActionType = UndoActionType.Transform;
                UndoRect = undoRect;
                UndoPixels = undoPixels;
                RedoRect = redoRect;
                RedoPixels = redoPixels;
                MemorySize = (undoPixels?.Length ?? 0) + (redoPixels?.Length ?? 0);
            }
        }
        public class UndoRedoManager
        {
            private readonly CanvasSurface _surface;
            public List<UndoAction> _undo = new();
            public List<UndoAction> _redo = new();

            public List<UndoAction> GetUndoStack() => _undo;
            public List<UndoAction> GetRedoStack() => _redo;
            private byte[]? _preStrokeSnapshot;
            private readonly List<Int32Rect> _strokeRects = new();
            public int UndoCount => _undo.Count;
            private void UpdateUI()
            {
                var mw = MainWindow.GetCurrentInstance();
                mw.SetUndoRedoButtonState(); // 更新按钮可用性
                mw.CheckDirtyState();        // 更新红点状态
                mw.UpdateWindowTitle();
            }
            public UndoRedoManager(CanvasSurface surface) { _surface = surface; }

            public bool CanUndo => _undo.Count > 0;
            public bool CanRedo => _redo.Count > 0;

            private void TrimStack()
            {
                // 检查全局限制 (步数与内存)
                CheckGlobalUndoLimits();
            }

            public static void CheckGlobalUndoLimits()
            {
                var mw = MainWindow.GetCurrentInstance();
                if (mw == null) return;

                var settings = SettingsManager.Instance.Current;
                long maxMemory = (long)settings.MaxUndoMemoryMB * 1024 * 1024;
                int maxGlobalSteps = settings.MaxGlobalUndoSteps;

                while (true)
                {
                    long currentTotalMemory = 0;
                    int currentTotalSteps = 0;
                    var allStacks = new List<List<UndoAction>>();

                    foreach (var tab in mw.FileTabs)
                    {
                        List<UndoAction> u, r;
                        if (tab == mw._currentTabItem && mw._undo != null)
                        {
                            u = mw._undo._undo;
                            r = mw._undo._redo;
                        }
                        else
                        {
                            u = tab.UndoStack;
                            r = tab.RedoStack;
                        }

                        foreach (var action in u) currentTotalMemory += action.MemorySize;
                        foreach (var action in r) currentTotalMemory += action.MemorySize;
                        currentTotalSteps += (u.Count + r.Count);

                        if (u.Count > 0) allStacks.Add(u);
                        if (r.Count > 0) allStacks.Add(r);
                    }

                    // 检查是否在限制内
                    if (currentTotalMemory <= maxMemory && currentTotalSteps <= maxGlobalSteps)
                        break;

                    // 寻找全局最旧的操作 (Timestamp 最小的)
                    List<UndoAction> oldestStack = null;
                    DateTime oldestTime = DateTime.MaxValue;

                    foreach (var stack in allStacks)
                    {
                        if (stack.Count > 0 && stack[0].Timestamp < oldestTime)
                        {
                            oldestTime = stack[0].Timestamp;
                            oldestStack = stack;
                        }
                    }

                    if (oldestStack != null)
                    {
                        oldestStack.RemoveAt(0);
                    }
                    else break; // 无可清理
                }
            }

            public void PushTransformAction(Int32Rect undoRect, byte[] undoPixels, Int32Rect redoRect, byte[] redoPixels)
            {//自动SetUndoRedoButtonState和_redo.Clear()
                _undo.Add(new UndoAction(undoRect, undoPixels, redoRect, redoPixels));
                _redo.Clear(); // 新操作截断重做链
                TrimStack();
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
                var combined = ClampRect(CombineRects(_strokeRects), (MainWindow.GetCurrentInstance())._ctx.Bitmap.PixelWidth, (MainWindow.GetCurrentInstance())._ctx.Bitmap.PixelHeight);

                byte[] region = ExtractRegionFromSnapshot(_preStrokeSnapshot, combined, _surface.Bitmap.BackBufferStride);
                _undo.Push(new UndoAction(combined, region, undoActionType));
                TrimStack();
                UpdateUI();
                _preStrokeSnapshot = null;

                (MainWindow.GetCurrentInstance()).NotifyCanvasChanged();
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
                var mw = MainWindow.GetCurrentInstance();
                if (mw._deleteCommitTimer.IsEnabled && mw._pendingDeletionTabs.Count > 0)
                {
                    mw.RestoreLastDeletedTab();
                    return; // 拦截成功，不再执行画布撤销
                }
                if (_undo != null) // 确保 UndoManager 存在
                {
                    ImageUndo();
                }
            }
            public void ImageUndo()
            {
                var mw = MainWindow.GetCurrentInstance();



                // === 原有逻辑：处理选区清理 ===
                if (mw._router.CurrentTool is SelectTool sselTool && sselTool.HasActiveSelection)
                {
                    if (sselTool.IsPasted)
                    {
                        sselTool.Cleanup(mw._ctx);
                        mw.SetCropButtonState();
                        return; // 直接返回，不执行下面的 Stack Pop
                    }
                    if (!sselTool._hasLifted)
                    {
                        sselTool.Cleanup(mw._ctx);
                        mw.SetCropButtonState();
                        return;
                    }
                }

                if (!CanUndo || _surface?.Bitmap == null) return;

                var action = _undo.Pop();

                if (mw._router.CurrentTool is SelectTool selTool) selTool.Cleanup(mw._ctx);

                if (action.ActionType == UndoActionType.Transform)
                {
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
                            (MainWindow.GetCurrentInstance())._ctx.Surface.Bitmap.DpiX, (MainWindow.GetCurrentInstance())._ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);
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
                (MainWindow.GetCurrentInstance()).NotifyCanvasChanged();
            }
            public void PushExplicitImageUndo(WriteableBitmap oldBitmap)
            {
                if (oldBitmap == null) return;
                // 1. 确定区域
                Int32Rect rect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);

                // 2. 提取像素数据
                int stride = oldBitmap.BackBufferStride;
                byte[] pixels = new byte[stride * oldBitmap.PixelHeight];
                oldBitmap.CopyPixels(pixels, stride, 0);

                // 3. 创建撤销动作 (由于滤镜不改变尺寸，使用 Draw 类型即可)
                _undo.Push(new UndoAction(rect, pixels, UndoActionType.Draw));
                _redo.Clear();  // 4. 清空重做链并更新 UI
                TrimStack();
                UpdateUI();
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
                TrimStack();
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