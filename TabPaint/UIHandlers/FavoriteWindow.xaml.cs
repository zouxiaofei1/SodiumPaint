using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace TabPaint.UIHandlers
{
    public partial class FavoriteWindow : Window
    {
        private Window _owner;

        public FavoriteWindow(Window owner)
        {
            InitializeComponent();
            _owner = owner;
            this.Owner = owner;

            this.SourceInitialized += FavoriteWindow_SourceInitialized;
            _owner.LocationChanged += UpdatePosition;
            _owner.SizeChanged += UpdatePosition;
            _owner.StateChanged += (s, e) => {
                if (_owner.WindowState == WindowState.Minimized) this.Hide();
                else if (this.IsVisible) this.Show();
                UpdatePosition(s, e);
            };
        }

        private void FavoriteWindow_SourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            ThemeManager.SetWindowImmersiveDarkMode(this, ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
            MicaAcrylicManager.ApplyEffect(this);
            UpdatePosition(null, null);
        }

        private void UpdatePosition(object sender, EventArgs e)
        {
            if (_owner == null || _owner.WindowState == WindowState.Minimized || !this.IsVisible) return;

            // 处理最大化时 Left/Top 可能为负的问题
            double ownerLeft = _owner.Left;
            double ownerTop = _owner.Top;
            double ownerWidth = _owner.ActualWidth;
            double ownerHeight = _owner.ActualHeight;

            if (_owner.WindowState == WindowState.Maximized)
            {
                // 最大化时，直接使用 WorkArea
                ownerLeft = SystemParameters.WorkArea.Left;
                ownerTop = SystemParameters.WorkArea.Top;
                ownerWidth = SystemParameters.WorkArea.Width;
                ownerHeight = SystemParameters.WorkArea.Height;
            }

            // 吸附在 MainWindow 右下角，状态栏上方
            double left = ownerLeft + ownerWidth - this.ActualWidth - 20;
            double top = ownerTop + ownerHeight - this.ActualHeight - 40;

            this.Left = left;
            this.Top = top;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        public void ToggleVisibility()
        {
            if (this.IsVisible) this.Hide();
            else
            {
                this.Show();
                UpdatePosition(null, null);
                this.Activate();
                FavoriteContent.Focus();
            }
        }
    }
}
