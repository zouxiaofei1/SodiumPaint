
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

//
//SelectTool类的定义
//

namespace TabPaint
{

    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        public partial class SelectTool : ToolBase
        {
   
            public SelectionType SelectionType { get; set; } = SelectionType.Rectangle;
            public bool IsPasted = false;
            public override string Name => "Select";
            private System.Windows.Input.Cursor _cachedCursor;
            public override System.Windows.Input.Cursor Cursor
            {
                get  { return System.Windows.Input.Cursors.Cross; }
            }
            public Int32Rect SelectionRect => _selectionRect;
            public bool IsSelecting => _selecting;  
            public bool HasRulerHighlight =>
                (_selectionData != null && _selectionRect.Width > 0 && _selectionRect.Height > 0)
                || (_selecting && _selectionRect.Width > 0 && _selectionRect.Height > 0);
            public bool _selecting = false;
            public bool _draggingSelection = false;
            private List<Point> _lassoPoints;
            public Geometry? _selectionGeometry;
            public byte[]? _selectionAlphaMap;
            private Point _startPixel;
            private Point _clickOffset;
            public Int32Rect _selectionRect;
            public Int32Rect _originalRect;
            public byte[]? _selectionData;
            private int _transformStep = 0; // 0 = 未操作，>0 = 已操作
            private byte[]? _clipboardData;
            private int _clipboardWidth;
            private int _clipboardHeight;
            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            public bool _resizing = false;
            private Point _startMouse;
            private double _startW, _startH, _startX, _startY;
            public int lag = 0;
            // 句柄尺寸
            private DispatcherTimer _tabSwitchTimer;
            private FileTabItem _pendingTab;
            private int _wandTolerance = AppConsts.DefaultWandTolerance; // 当前容差
            private Point _wandStartPoint; // 点击的起始点
            private Color _wandStartColor; // 起始点的颜色
            private bool _isWandAdjusting = false; // 是否正在拖拽调整容差
            private bool[] _wandMaskBuffer; // 用于缓存全图的选中状态(bool)，避免重复申请内存
            public enum ResizeAnchor
            {
                None,
                TopLeft, TopMiddle, TopRight,
                LeftMiddle, RightMiddle,
                BottomLeft, BottomMiddle, BottomRight
            }
            public DateTime LastSelectionDeleteTime { get; private set; } = DateTime.MinValue;
            public override void SetCursor(ToolContext ctx)
            {
                System.Windows.Input.Mouse.OverrideCursor = null;

                if (ctx.ViewElement != null)   ctx.ViewElement.Cursor = this.Cursor;
            }
            private void EnsureTimer()
            {
                if (_tabSwitchTimer == null)
                {
                    _tabSwitchTimer = new DispatcherTimer();
                    _tabSwitchTimer.Interval = TimeSpan.FromMilliseconds(AppConsts.TabSwitchCheckIntervalMs); // 每50ms检查一次
                    _tabSwitchTimer.Tick += OnTabSwitchTimerTick;
                }
            }
            private void OnTabSwitchTimerTick(object sender, EventArgs e)
            {
                if (_pendingTab == null || !_draggingSelection)
                {
                    ResetSwitchTimer();
                    return;
                }
                bool isCleanSelection = ctxForTimer.Undo.UndoCount <= 1 && ctxForTimer.Undo._redo.Count == 0;
                double targetDelay = isCleanSelection ? AppConsts.QuickDragDelayMs : AppConsts.SlowDragDelayMs;
                double elapsed = (DateTime.Now - _hoverStartTime).TotalMilliseconds;
                var mw = ctxForTimer.ParentWindow;
                if (elapsed > AppConsts.HoverStartTimeThresholdMs)
                {
                    // 1. 光标反馈
                    Mouse.OverrideCursor = Cursors.AppStarting;

                    // 2. 状态栏文字倒计时
                    double remaining = (targetDelay - elapsed) / 1000.0;
                    string cleanStateText = isCleanSelection ? LocalizationManager.GetString("L_Selection_Drag_Quick") : "";

                    string statusText = string.Format(
            LocalizationManager.GetString("L_Selection_Drag_Jump"),
            _pendingTab.FileName,
            cleanStateText,
            remaining
        );
                    mw.SelectionSize = statusText;

                    if (ctxForTimer != null)
                    {
                        double scaleFactor = 1.0 - (elapsed / (targetDelay * 2));
                        if (scaleFactor < 0.8) scaleFactor = 0.8;
                        ctxForTimer.SelectionPreview.Opacity = 0.5 + (Math.Sin(elapsed / 100.0) * 0.2);
                    }
                }
                if (elapsed > targetDelay)
                {
                    if (ctxForTimer != null) ctxForTimer.SelectionPreview.Opacity = 0.7; // 恢复默认拖拽透明度
                    Mouse.OverrideCursor = null;

                    _draggingSelection = false;
                    int w = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                    int h = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                    byte[] dataClone = null;
                    if (_selectionData != null)
                    {
                        dataClone = new byte[_selectionData.Length];
                        Array.Copy(_selectionData, dataClone, _selectionData.Length);
                    }
                    mw.TransferSelectionToTab(_pendingTab, dataClone, w, h);

                    ResetSwitchTimer();
                }
            }
            private void ResetSwitchTimer()
            {
                if (_tabSwitchTimer != null && _tabSwitchTimer.IsEnabled)
                {
                    _tabSwitchTimer.Stop();
                }
                _pendingTab = null;
                Mouse.OverrideCursor = null;

                // 恢复预览图状态
                if (ctxForTimer != null) ctxForTimer.SelectionPreview.Opacity = 1.0;

                var mw = ctxForTimer?.ParentWindow;
                if (mw != null) mw.UpdateSelectionScalingMode();
            }
            private ToolContext ctxForTimer;
        }

    }
}