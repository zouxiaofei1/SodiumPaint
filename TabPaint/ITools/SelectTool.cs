
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
            public bool IsPasted = false;
            public override string Name => "Select";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Cross;

            public bool _selecting = false;
            public bool _draggingSelection = false;

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
            private bool _resizing = false;
            private Point _startMouse;
            private double _startW, _startH, _startX, _startY;
            public int lag = 0;
            // 句柄尺寸
            private const double HandleSize = 6;
            private DispatcherTimer _tabSwitchTimer;
            private FileTabItem _pendingTab; // 当前正在倒计时的目标 Tab
           // private DateTime _hoverStartTime;
            public enum ResizeAnchor
            {
                None,
                TopLeft, TopMiddle, TopRight,
                LeftMiddle, RightMiddle,
                BottomLeft, BottomMiddle, BottomRight
            }
            public DateTime LastSelectionDeleteTime { get; private set; } = DateTime.MinValue;

            private void EnsureTimer()
            {
                if (_tabSwitchTimer == null)
                {
                    _tabSwitchTimer = new DispatcherTimer();
                    _tabSwitchTimer.Interval = TimeSpan.FromMilliseconds(50); // 每50ms检查一次
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

                double elapsed = (DateTime.Now - _hoverStartTime).TotalMilliseconds;
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

                // --- 动画效果部分 ---
                if (elapsed > 200)
                {
                    // 1. 光标反馈
                    Mouse.OverrideCursor = Cursors.AppStarting;

                    // 2. 状态栏文字倒计时
                    double remaining = (1000 - elapsed) / 1000.0;
                    mw.SelectionSize = $"跳转至: {_pendingTab.FileName} ({remaining:F1}s)";

                    if (ctxForTimer != null)
                    {
                        double scaleFactor = 1.0 - (elapsed / 2000.0); // 稍微变小一点点
                        if (scaleFactor < 0.8) scaleFactor = 0.8;
                        ctxForTimer.SelectionPreview.Opacity = 0.5 + (Math.Sin(elapsed / 100.0) * 0.2);
                    }
                }

                // --- 触发切换 ---
                if (elapsed > 1000)
                {
                  
                    if (ctxForTimer != null) ctxForTimer.SelectionPreview.Opacity = 0.7; // 恢复默认拖拽透明度
                    Mouse.OverrideCursor = null;

                    _draggingSelection = false;
                   
                    // 准备数据
                    int w = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                    int h = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                    mw.TransferSelectionToTab(_pendingTab, _selectionData, w, h);
                    ResetSwitchTimer(); // 停止计时
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

                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (mw != null) mw.UpdateSelectionScalingMode();
            }
            private ToolContext ctxForTimer;
        }

    }
}