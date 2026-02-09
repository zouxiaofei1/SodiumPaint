// FavoriteWindowManager.cs
using System;
using System.Linq;
using System.Windows;

namespace TabPaint.UIHandlers
{
    public static class FavoriteWindowManager
    {
        private static FavoriteWindow _instance;
        private static readonly object _lock = new object();

        public static MainWindow CurrentAnchor { get; private set; }

        public static FavoriteWindow GetInstance()
        {
            lock (_lock)
            {
                if (_instance == null || !_instance.IsLoaded)
                {
                    _instance = new FavoriteWindow();
                    _instance.Closed += (s, e) =>
                    {
                        lock (_lock)
                        {
                            _instance = null; CurrentAnchor = null;
                        }
                    };
                }
                return _instance;
            }
        }
        public static void Toggle(MainWindow caller)
        {
            var win = GetInstance();

            if (win.IsVisible)
            {
                // 如果是同一个窗口再次点击，隐藏
                if (CurrentAnchor == caller)
                {
                    win.Hide();
                    return;
                }
                // 不同窗口点击：切换吸附目标
                SwitchAnchor(caller);
                win.Activate();
            }
            else
            {
                SwitchAnchor(caller);
                win.Show(); win.Activate();
                win.RefreshContent();
            }
        }
        public static void SwitchAnchor(MainWindow newAnchor)
        {
            if (newAnchor == null) return;
            if (CurrentAnchor == newAnchor && _instance?.IsVisible == true) return;

            CurrentAnchor = newAnchor;
            _instance?.AttachToAnchor(newAnchor);
        }
        public static void OnMainWindowClosing(MainWindow closingWindow)
        {
            var other = Application.Current.Windows
               .OfType<MainWindow>()
               .FirstOrDefault(w => w != closingWindow && w.IsLoaded);

            if (other != null)
            {
                if (CurrentAnchor == closingWindow)
                {
                    SwitchAnchor(other);
                }
            }
            else
            {
                // 没有其他 MainWindow 了，关闭 FavoriteWindow
                _instance?.Close();
            }
        }
        public static void SwitchAnchorDuringDrag(MainWindow newAnchor)
        {
            if (newAnchor == null || CurrentAnchor == newAnchor) return;

            CurrentAnchor = newAnchor;

            // 拖拽中只更新锚点的事件订阅，不强制移动位置
            _instance?.SwitchAnchorKeepPosition(newAnchor);
        }
        public static void OnMainWindowActivated(MainWindow activatedWindow)
        {
            // 如果 FavoriteWindow 可见且已吸附，自动跟随到激活的窗口
            if (_instance != null && _instance.IsVisible && _instance.IsSnapped)
            {
                SwitchAnchor(activatedWindow);
            }
        }
        public static MainWindow FindNearestMainWindow(Point screenPoint)
        {
            MainWindow nearest = null;
            double minDist = double.MaxValue;

            foreach (var w in Application.Current.Windows.OfType<MainWindow>())
            {
                if (!w.IsVisible || w.WindowState == WindowState.Minimized) continue;

                double cx = w.Left + w.ActualWidth / 2;
                double cy = w.Top + w.ActualHeight / 2;
                double dist = Math.Sqrt(Math.Pow(screenPoint.X - cx, 2) + Math.Pow(screenPoint.Y - cy, 2));

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = w;
                }
            }

            return nearest;
        }
    }
}
