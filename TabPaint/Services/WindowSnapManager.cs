using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace TabPaint.UIHandlers
{
    public enum SnapEdge
    {
        Bottom = 0,
        Top = 1,
        Right = 2,
        Left = 3
    }
    public class WindowSnapManager
    {
        #region Win32 Interop

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        #endregion

        #region 配置
        public double SnapThreshold { get; set; } = 20.0;
        public double SnapGap { get; set; } = 2.0;
        public double DetachThreshold { get; set; } = 20.0;

        #endregion

        #region 状态

        public bool IsSnapped { get; private set; } = true;
        public SnapEdge SnapEdge { get; private set; } = SnapEdge.Bottom;

        private readonly Window _satellite;   // 子窗口（被吸附的窗口）
        private Window _anchor;      // 主窗口（锚点）

        // 拖动状态
        private bool _isDragging;
        private Point _dragStartCursorScreen;
        private Point _dragStartWindowPos;
        private double _lastAnchorLeft = double.NaN;
        private double _lastAnchorTop = double.NaN;
        private double _lastAnchorWidth = double.NaN;
        private double _lastAnchorHeight = double.NaN;

        #endregion
        public void SwitchAnchorKeepPosition(Window newAnchor)
        {
            if (newAnchor == null || newAnchor == _anchor) return;

            // 取消旧锚点事件
            _anchor.LocationChanged -= OnAnchorChanged;
            _anchor.SizeChanged -= OnAnchorChanged;
            _anchor.StateChanged -= OnAnchorStateChanged;

            _anchor = newAnchor;

            // 订阅新锚点事件
            _anchor.LocationChanged += OnAnchorChanged;
            _anchor.SizeChanged += OnAnchorChanged;
            _anchor.StateChanged += OnAnchorStateChanged;

            SyncAnchorPosition();
            // 不调用 MoveToSnappedPosition()，保持当前位置
        }

        #region 事件
        public event Action<SnapEdge, bool> SnapStateChanged;

        #endregion

        public WindowSnapManager(Window satellite, Window anchor, SnapEdge initialEdge = SnapEdge.Bottom)
        {
            _satellite = satellite ?? throw new ArgumentNullException(nameof(satellite));
            _anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
            SnapEdge = initialEdge;
        }

        #region 公共 API

        public void Attach()
        {
            _anchor.LocationChanged += OnAnchorChanged;
            _anchor.SizeChanged += OnAnchorChanged;
            _anchor.StateChanged += OnAnchorStateChanged;

            IsSnapped = true;
            SyncAnchorPosition();
            MoveToSnappedPosition();
        }
        public void Detach()
        {
            _anchor.LocationChanged -= OnAnchorChanged;
            _anchor.SizeChanged -= OnAnchorChanged;
            _anchor.StateChanged -= OnAnchorStateChanged;
        }
        public void ResetSnap(SnapEdge? edge = null)
        {
            if (edge.HasValue) SnapEdge = edge.Value;
            IsSnapped = true;
            SyncAnchorPosition();
            MoveToSnappedPosition(); RaiseSnapStateChanged();
        }
        public void OnAnchorWindowStateChanged()
        {
            if (_anchor.WindowState == WindowState.Minimized)
            {
                _satellite.Hide();
            }
            else
            {
                if (IsSnapped)
                {
                    SyncAnchorPosition();
                    MoveToSnappedPosition();
                }
            }
        }

        public bool IsDragging => _isDragging;

        #endregion

        #region 拖动 API — 由子窗口的 MouseDown/Move/Up 调用

        public void BeginDrag(Point screenCursorDIU)
        {
            _isDragging = true;
            _dragStartCursorScreen = screenCursorDIU;
            _dragStartWindowPos = new Point(_satellite.Left, _satellite.Top);
        }

        public void UpdateDrag(Point screenCursorDIU)
        {
            if (!_isDragging) return;

            double deltaX = screenCursorDIU.X - _dragStartCursorScreen.X;
            double deltaY = screenCursorDIU.Y - _dragStartCursorScreen.Y;
            double freeLeft = _dragStartWindowPos.X + deltaX;
            double freeTop = _dragStartWindowPos.Y + deltaY;

            if (IsSnapped)
            {
                double perpendicularDelta = GetPerpendicularDelta(deltaX, deltaY);

                if (perpendicularDelta > DetachThreshold)
                {
                    if (IsFullyDetachedFromAnchor(freeLeft, freeTop, _satellite.Width, _satellite.Height))
                    {
                        IsSnapped = false;
                        RaiseSnapStateChanged();
                        MoveToFreePosition(freeLeft, freeTop);
                    }
                    else
                    {
                        MoveAlongSnappedEdge(deltaX, deltaY);
                    }
                }
                else
                {
                    MoveAlongSnappedEdge(deltaX, deltaY);
                }
            }
            else
            {
                // 自由拖动：检测是否靠近主窗口边缘
                SnapEdge candidateEdge;
                double candidateDist;
                CalcSnapCandidate(freeLeft, freeTop, _satellite.Width, _satellite.Height,
                                  out candidateEdge, out candidateDist);

                if (candidateDist <= SnapThreshold)
                {
                    IsSnapped = true;
                    SnapEdge = candidateEdge;
                    RaiseSnapStateChanged();
                    SnapToEdgeKeepingParallelPosition(freeLeft, freeTop);

                    // 重置拖动起点
                    _dragStartCursorScreen = screenCursorDIU;
                    _dragStartWindowPos = new Point(_satellite.Left, _satellite.Top);
                }
                else
                {
                    MoveToFreePosition(freeLeft, freeTop);
                }
            }
        }

        public void EndDrag()
        {
            if (!_isDragging) return;
            _isDragging = false;

            // 松手时再做一次磁吸检测
            if (!IsSnapped)
            {
                SnapEdge candidateEdge;
                double candidateDist;
                CalcSnapCandidate(_satellite.Left, _satellite.Top,
                                  _satellite.Width, _satellite.Height,
                                  out candidateEdge, out candidateDist);

                if (candidateDist <= SnapThreshold)
                {
                    IsSnapped = true;
                    SnapEdge = candidateEdge;
                    RaiseSnapStateChanged();
                    SyncAnchorPosition();
                    SnapToEdgeKeepingParallelPosition(_satellite.Left, _satellite.Top);
                }
            }

            SyncAnchorPosition();
        }

        #endregion

        #region 获取屏幕光标（DPI 感知）

        public Point GetCursorPosDIU()
        {
            GetCursorPos(out POINT p);
            DpiScale dpi = VisualTreeHelper.GetDpi(_satellite);
            return new Point(p.X / dpi.DpiScaleX, p.Y / dpi.DpiScaleY);
        }

        #endregion

        #region 主窗口事件处理

        private void OnAnchorChanged(object sender, EventArgs e)
        {
            if (!_satellite.IsVisible || _isDragging) return;
            if (IsSnapped)
            {
                UpdateSnappedFollowAnchor();
            }
        }

        private void OnAnchorStateChanged(object sender, EventArgs e)
        {
            OnAnchorWindowStateChanged();
        }

        #endregion

        #region 内部计算

        private void GetAnchorRect(out double left, out double top, out double width, out double height)
        {
            if (_anchor.WindowState == WindowState.Maximized)
            {
                left = SystemParameters.WorkArea.Left;
                top = SystemParameters.WorkArea.Top;
                width = SystemParameters.WorkArea.Width;
                height = SystemParameters.WorkArea.Height;
            }
            else
            {
                left = _anchor.Left;
                top = _anchor.Top;
                width = _anchor.ActualWidth;
                height = _anchor.ActualHeight;
            }
        }

        public void SyncAnchorPosition()
        {
            GetAnchorRect(out double l, out double t, out double w, out double h);
            _lastAnchorLeft = l;
            _lastAnchorTop = t;
            _lastAnchorWidth = w;
            _lastAnchorHeight = h;
        }

        private void MoveToSnappedPosition()
        {
            if (_anchor.WindowState == WindowState.Minimized || !_satellite.IsVisible) return;

            GetAnchorRect(out double aL, out double aT, out double aW, out double aH);

            switch (SnapEdge)
            {
                case SnapEdge.Bottom:
                    _satellite.Left = aL;
                    _satellite.Top = aT + aH + SnapGap;
                    break;
                case SnapEdge.Top:
                    _satellite.Left = aL;
                    _satellite.Top = aT - _satellite.Height - SnapGap;
                    break;
                case SnapEdge.Right:
                    _satellite.Left = aL + aW + SnapGap;
                    _satellite.Top = aT;
                    break;
                case SnapEdge.Left:
                    _satellite.Left = aL - _satellite.Width - SnapGap;
                    _satellite.Top = aT;
                    break;
            }

            ClampToWorkArea();
        }

        private void UpdateSnappedFollowAnchor()
        {
            if (_anchor.WindowState == WindowState.Minimized || !_satellite.IsVisible) return;

            GetAnchorRect(out double aL, out double aT, out double aW, out double aH);

            double dL = 0, dT = 0;
            if (!double.IsNaN(_lastAnchorLeft))
            {
                dL = aL - _lastAnchorLeft;
                dT = aT - _lastAnchorTop;
            }

            _lastAnchorLeft = aL;
            _lastAnchorTop = aT;
            _lastAnchorWidth = aW;
            _lastAnchorHeight = aH;

            switch (SnapEdge)
            {
                case SnapEdge.Bottom:
                    _satellite.Left += dL;
                    _satellite.Top = aT + aH + SnapGap;
                    break;
                case SnapEdge.Top:
                    _satellite.Left += dL;
                    _satellite.Top = aT - _satellite.Height - SnapGap;
                    break;
                case SnapEdge.Right:
                    _satellite.Left = aL + aW + SnapGap;
                    _satellite.Top += dT;
                    break;
                case SnapEdge.Left:
                    _satellite.Left = aL - _satellite.Width - SnapGap;
                    _satellite.Top += dT;
                    break;
            }

            ClampToWorkArea();
        }

        private double GetPerpendicularDelta(double deltaX, double deltaY)
        {
            switch (SnapEdge)
            {
                case SnapEdge.Bottom:
                case SnapEdge.Top:
                    return Math.Abs(deltaY);
                case SnapEdge.Right:
                case SnapEdge.Left:
                    return Math.Abs(deltaX);
                default:
                    return 0;
            }
        }

        private void MoveAlongSnappedEdge(double deltaX, double deltaY)
        {
            GetAnchorRect(out double aL, out double aT, out double aW, out double aH);

            switch (SnapEdge)
            {
                case SnapEdge.Bottom:
                    _satellite.Left = _dragStartWindowPos.X + deltaX;
                    _satellite.Top = aT + aH + SnapGap;
                    break;
                case SnapEdge.Top:
                    _satellite.Left = _dragStartWindowPos.X + deltaX;
                    _satellite.Top = aT - _satellite.Height - SnapGap;
                    break;
                case SnapEdge.Right:
                    _satellite.Left = aL + aW + SnapGap;
                    _satellite.Top = _dragStartWindowPos.Y + deltaY;
                    break;
                case SnapEdge.Left:
                    _satellite.Left = aL - _satellite.Width - SnapGap;
                    _satellite.Top = _dragStartWindowPos.Y + deltaY;
                    break;
            }

            ClampToWorkArea();
        }

        private void SnapToEdgeKeepingParallelPosition(double currentFreeLeft, double currentFreeTop)
        {
            GetAnchorRect(out double aL, out double aT, out double aW, out double aH);

            switch (SnapEdge)
            {
                case SnapEdge.Bottom:
                    _satellite.Left = currentFreeLeft;
                    _satellite.Top = aT + aH + SnapGap;
                    break;
                case SnapEdge.Top:
                    _satellite.Left = currentFreeLeft;
                    _satellite.Top = aT - _satellite.Height - SnapGap;
                    break;
                case SnapEdge.Right:
                    _satellite.Left = aL + aW + SnapGap;
                    _satellite.Top = currentFreeTop;
                    break;
                case SnapEdge.Left:
                    _satellite.Left = aL - _satellite.Width - SnapGap;
                    _satellite.Top = currentFreeTop;
                    break;
            }

            ClampToWorkArea();
        }
        public void SwitchAnchor(Window newAnchor)
        {
            if (newAnchor == null || newAnchor == _anchor) return;

            // 取消旧锚点的事件
            _anchor.LocationChanged -= OnAnchorChanged;
            _anchor.SizeChanged -= OnAnchorChanged;
            _anchor.StateChanged -= OnAnchorStateChanged;
            _anchor = newAnchor;
            _anchor.LocationChanged += OnAnchorChanged;
            _anchor.SizeChanged += OnAnchorChanged;
            _anchor.StateChanged += OnAnchorStateChanged;
            IsSnapped = true;
            SyncAnchorPosition();
            MoveToSnappedPosition();
            RaiseSnapStateChanged();
        }
        private void CalcSnapCandidate(double favL, double favT, double favW, double favH,
                                       out SnapEdge candidateEdge, out double candidateDist)
        {
            GetAnchorRect(out double aL, out double aT, out double aW, out double aH);

            double aR = aL + aW, aB = aT + aH;
            double fR = favL + favW, fB = favT + favH;

            double[] dists = new double[4];

            dists[0] = HOverlap(favL, fR, aL, aR) > 0 ? Math.Abs(favT - aB) : double.MaxValue;
            dists[1] = HOverlap(favL, fR, aL, aR) > 0 ? Math.Abs(aT - fB) : double.MaxValue;
            dists[2] = VOverlap(favT, fB, aT, aB) > 0 ? Math.Abs(favL - aR) : double.MaxValue;
            dists[3] = VOverlap(favT, fB, aT, aB) > 0 ? Math.Abs(aL - fR) : double.MaxValue;

            int best = 0;
            for (int i = 1; i < 4; i++)
                if (dists[i] < dists[best]) best = i;

            candidateEdge = (SnapEdge)best;
            candidateDist = dists[best];
        }

        private bool IsFullyDetachedFromAnchor(double fL, double fT, double fW, double fH)
        {
            GetAnchorRect(out double aL, out double aT, out double aW, out double aH);

            double margin = SnapGap + DetachThreshold;
            double eL = aL - margin, eT = aT - margin;
            double eR = aL + aW + margin, eB = aT + aH + margin;

            return (fL + fW < eL || fL > eR || fT + fH < eT || fT > eB);
        }

        private void MoveToFreePosition(double freeLeft, double freeTop)
        {
            _satellite.Left = freeLeft;
            _satellite.Top = freeTop;
            ClampToWorkArea();
        }

        private void ClampToWorkArea()
        {
            var wa = SystemParameters.WorkArea;
            if (_satellite.Left < wa.Left) _satellite.Left = wa.Left;
            if (_satellite.Top < wa.Top) _satellite.Top = wa.Top;
            if (_satellite.Left + _satellite.Width > wa.Right)
                _satellite.Left = wa.Right - _satellite.Width;
            if (_satellite.Top + _satellite.Height > wa.Bottom)
                _satellite.Top = wa.Bottom - _satellite.Height;
        }

        private static double HOverlap(double aL, double aR, double bL, double bR)
            => Math.Max(0, Math.Min(aR, bR) - Math.Max(aL, bL));

        private static double VOverlap(double aT, double aB, double bT, double bB)
            => Math.Max(0, Math.Min(aB, bB) - Math.Max(aT, bT));

        private void RaiseSnapStateChanged()
            => SnapStateChanged?.Invoke(SnapEdge, IsSnapped);

        #endregion
    }
}
