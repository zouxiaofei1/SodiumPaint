using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using TabPaint.Controls;

namespace TabPaint.UIHandlers
{
    public partial class FavoriteWindow : Window
    {
        #region Win32 Interop

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        #endregion

        private Window _owner;
        private List<string> _pages = new List<string>();
        private string _activePage = "Default";
        private int _snapMode = 0;
        private bool _isSnapped = true;

        // ===== 拖动相关字段 =====
        private bool _isDragging = false;
        private Point _dragStartCursorScreen;
        private Point _dragStartWindowPos;
        private IntPtr _hwnd;
        private const double SnapThreshold = 20.0;
        private const double SnapGap = 2.0;

        private StackPanel GetPagesStack()
        {
            return this.FindName("PagesStackPanel") as StackPanel;
        }

        private FavoriteBarControl GetFavoriteContent()
        {
            return this.FindName("FavoriteContent") as FavoriteBarControl;
        }

        public FavoriteWindow(Window owner)
        {
            InitializeComponent();
            _owner = owner;
            this.Owner = owner;

            this.Loaded += (s, e) => LoadPages();

            this.SourceInitialized += FavoriteWindow_SourceInitialized;

            // 主窗口移动时：如果吸附状态则跟随
            _owner.LocationChanged += Owner_LocationOrSizeChanged;
            _owner.SizeChanged += Owner_LocationOrSizeChanged;
            _owner.StateChanged += (s, e) =>
            {
                if (_owner.WindowState == WindowState.Minimized) this.Hide();
                else if (this.IsVisible) this.Show();

                if (_isSnapped)
                {
                    UpdateSizeForSnapMode();
                    MoveToSnappedPosition();
                }
            };
        }

        private void FavoriteWindow_SourceInitialized(object sender, EventArgs e)
        {
            ThemeManager.SetWindowImmersiveDarkMode(this, ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
            MicaAcrylicManager.ApplyEffect(this);

            var helper = new WindowInteropHelper(this);
            _hwnd = helper.Handle;

            _isSnapped = true;
            UpdateSizeForSnapMode();
            MoveToSnappedPosition();
        }
        private void Owner_LocationOrSizeChanged(object sender, EventArgs e)
        {
            if (!this.IsVisible || _isDragging) return;
            if (_isSnapped)
            {
                UpdateSizeForSnapMode();
                MoveToSnappedPosition();
            }
        }

        #region 手动拖动逻辑

        private Point GetCursorPosDIU()
        {
            GetCursorPos(out POINT p);
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            return new Point(p.X / dpi.DpiScaleX, p.Y / dpi.DpiScaleY);
        }

        private void StarIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            // ★ 关键修改：检查点击源是否是交互控件，如果是则不启动拖动
            DependencyObject originalSource = e.OriginalSource as DependencyObject;
            if (IsInteractiveElement(originalSource)) return; // 让事件继续冒泡，交给按钮等控件自己处理

            _isDragging = true;
            _dragStartCursorScreen = GetCursorPosDIU();
            _dragStartWindowPos = new Point(this.Left, this.Top);

            var element = sender as UIElement;
            if (element != null)
            {
                element.CaptureMouse(); element.MouseMove += StarIcon_MouseMove;
                element.MouseLeftButtonUp += StarIcon_MouseLeftButtonUp;
            }

            e.Handled = true;
        }

        private bool IsInteractiveElement(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null && current != this)
            {
                if (current is System.Windows.Controls.Primitives.ButtonBase  // Button, RepeatButton, ToggleButton...
                    || current is System.Windows.Controls.TextBox
                    || current is System.Windows.Controls.Primitives.TextBoxBase
                    || current is System.Windows.Controls.ComboBox
                    || current is System.Windows.Controls.Primitives.ScrollBar
                    || current is System.Windows.Controls.Primitives.Thumb
                    || current is System.Windows.Controls.MenuItem
                    || current is System.Windows.Controls.ListBoxItem
                    || current is System.Windows.Controls.Primitives.Selector)
                {
                    return true;
                }

                if (current is FrameworkElement fe && fe.Cursor == Cursors.Hand)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }


        private void StarIcon_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            Point currentCursor = GetCursorPosDIU();
            double freeLeft = _dragStartWindowPos.X + (currentCursor.X - _dragStartCursorScreen.X);
            double freeTop = _dragStartWindowPos.Y + (currentCursor.Y - _dragStartCursorScreen.Y);
            int candidateSnap;
            double candidateDist;
            CalcSnapCandidate(freeLeft, freeTop, this.Width, this.Height, out candidateSnap, out candidateDist);

            if (candidateDist <= SnapThreshold)
            {
                // 进入吸附：snap到对应边
                _isSnapped = true;
                _snapMode = candidateSnap;
                UpdateSizeForSnapMode();
                MoveToSnappedPosition();
            }
            else
            {
                _isSnapped = false;
                var workArea = SystemParameters.WorkArea;
                if (freeLeft < workArea.Left) freeLeft = workArea.Left;
                if (freeTop < workArea.Top) freeTop = workArea.Top;
                if (freeLeft + this.Width > workArea.Right) freeLeft = workArea.Right - this.Width;
                if (freeTop + this.Height > workArea.Bottom) freeTop = workArea.Bottom - this.Height;

                this.Left = freeLeft;
                this.Top = freeTop;
            }
        }

        private void StarIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;

            var element = sender as UIElement;
            if (element != null)
            {
                element.ReleaseMouseCapture();
                element.MouseMove -= StarIcon_MouseMove;
                element.MouseLeftButtonUp -= StarIcon_MouseLeftButtonUp;
            }

            // 松手时再做一次磁吸检测
            if (!_isSnapped)
            {
                int candidateSnap;
                double candidateDist;
                CalcSnapCandidate(this.Left, this.Top, this.Width, this.Height, out candidateSnap, out candidateDist);

                if (candidateDist <= SnapThreshold)
                {
                    _isSnapped = true;
                    _snapMode = candidateSnap;
                    UpdateSizeForSnapMode();
                    MoveToSnappedPosition();
                }
            }

            e.Handled = true;
        }
        private void CalcSnapCandidate(double favLeft, double favTop, double favWidth, double favHeight, out int candidateSnap, out double candidateDist)
        {
            double ownerLeft, ownerTop, ownerWidth, ownerHeight;
            GetOwnerRect(out ownerLeft, out ownerTop, out ownerWidth, out ownerHeight);

            double ownerRight = ownerLeft + ownerWidth;
            double ownerBottom = ownerTop + ownerHeight;

            double favRight = favLeft + favWidth;
            double favBottom = favTop + favHeight;
            double[] dists = new double[4];
            dists[0] = HorizontalOverlap(favLeft, favRight, ownerLeft, ownerRight) > 0
                       ? Math.Abs(favTop - ownerBottom)
                       : double.MaxValue;

            // 顶部吸附：FavoriteWindow 在主窗口上方
            dists[1] = HorizontalOverlap(favLeft, favRight, ownerLeft, ownerRight) > 0
                       ? Math.Abs(ownerTop - favBottom)
                       : double.MaxValue;

            // 右侧吸附：FavoriteWindow 在主窗口右边
            dists[2] = VerticalOverlap(favTop, favBottom, ownerTop, ownerBottom) > 0
                       ? Math.Abs(favLeft - ownerRight)
                       : double.MaxValue;

            // 左侧吸附：FavoriteWindow 在主窗口左边
            dists[3] = VerticalOverlap(favTop, favBottom, ownerTop, ownerBottom) > 0
                       ? Math.Abs(ownerLeft - favRight)
                       : double.MaxValue;

            // 找最小距离
            candidateSnap = 0;
            candidateDist = dists[0];
            for (int i = 1; i < 4; i++)
            {
                if (dists[i] < candidateDist)
                {
                    candidateDist = dists[i];
                    candidateSnap = i;
                }
            }
        }

        private double HorizontalOverlap(double aLeft, double aRight, double bLeft, double bRight)
        {
            double overlapLeft = Math.Max(aLeft, bLeft);
            double overlapRight = Math.Min(aRight, bRight);
            return Math.Max(0, overlapRight - overlapLeft);
        }

        private double VerticalOverlap(double aTop, double aBottom, double bTop, double bBottom)
        {
            double overlapTop = Math.Max(aTop, bTop);
            double overlapBottom = Math.Min(aBottom, bBottom);
            return Math.Max(0, overlapBottom - overlapTop);
        }

        #endregion

        #region 吸附位置/尺寸计算

        private void GetOwnerRect(out double left, out double top, out double width, out double height)
        {
            if (_owner.WindowState == WindowState.Maximized)
            {
                left = SystemParameters.WorkArea.Left;
                top = SystemParameters.WorkArea.Top;
                width = SystemParameters.WorkArea.Width;
                height = SystemParameters.WorkArea.Height;
            }
            else
            {
                left = _owner.Left;
                top = _owner.Top;
                width = _owner.ActualWidth;
                height = _owner.ActualHeight;
            }
        }

        private void UpdateSizeForSnapMode()
        {
            double ownerLeft, ownerTop, ownerWidth, ownerHeight;
            GetOwnerRect(out ownerLeft, out ownerTop, out ownerWidth, out ownerHeight);

            switch (_snapMode)
            {
                case 0:
                case 1:
                    this.Width = ownerWidth;
                    this.Height = 150;
                    SetHorizontalLayout();
                    break;
                case 2:
                case 3:
                    this.Width = 150;
                    this.Height = ownerHeight;
                    SetVerticalLayout();
                    break;
            }
        }
        private void MoveToSnappedPosition()
        {
            if (_owner == null || _owner.WindowState == WindowState.Minimized || !this.IsVisible) return;

            double ownerLeft, ownerTop, ownerWidth, ownerHeight;
            GetOwnerRect(out ownerLeft, out ownerTop, out ownerWidth, out ownerHeight);

            double targetWidth = this.Width;
            double targetHeight = this.Height;

            switch (_snapMode)
            {
                case 0: // 底部
                    this.Left = ownerLeft;
                    this.Top = ownerTop + ownerHeight + SnapGap;
                    break;
                case 1: // 顶部
                    this.Left = ownerLeft;
                    this.Top = ownerTop - targetHeight - SnapGap;
                    break;
                case 2: // 右侧
                    this.Left = ownerLeft + ownerWidth + SnapGap;
                    this.Top = ownerTop;
                    break;
                case 3: // 左侧
                    this.Left = ownerLeft - targetWidth - SnapGap;
                    this.Top = ownerTop;
                    break;
            }

            // 边界保护
            var workArea = SystemParameters.WorkArea;
            if (this.Left < workArea.Left) this.Left = workArea.Left;
            if (this.Top < workArea.Top) this.Top = workArea.Top;
            if (this.Left + this.Width > workArea.Right) this.Left = workArea.Right - this.Width;
            if (this.Top + this.Height > workArea.Bottom) this.Top = workArea.Bottom - this.Height;
        }

        private void SetHorizontalLayout()
        {
            var rootGrid = this.FindName("RootGrid") as Grid;
            var navPanel = this.FindName("NavPanel") as Grid;
            var contentPanel = this.FindName("ContentPanel") as Grid;
            var navColumn = this.FindName("NavColumn") as ColumnDefinition;
            var navRow = this.FindName("NavRow") as RowDefinition;

            if (rootGrid == null || navPanel == null || contentPanel == null) return;

            // 列布局：48 | *
            navColumn.Width = new GridLength(48);
            navRow.Height = new GridLength(0);

            // 导航栏：左侧
            Grid.SetColumn(navPanel, 0);
            Grid.SetRow(navPanel, 0);
            Grid.SetRowSpan(navPanel, 2);
            Grid.SetColumnSpan(navPanel, 1);

            // 内容：右侧
            Grid.SetColumn(contentPanel, 1);
            Grid.SetRow(contentPanel, 0);
            Grid.SetRowSpan(contentPanel, 2);
            Grid.SetColumnSpan(contentPanel, 1);

            // 导航栏内部：纵向排列页签
            var pagesStack = GetPagesStack();
            if (pagesStack != null)
                pagesStack.Orientation = Orientation.Vertical;

            // 内容区：横向排列收藏项
            var stack = GetFavoriteContent()?.FindName("FavoriteStackPanel") as StackPanel;
            if (stack != null)
                stack.Orientation = Orientation.Horizontal;

            var favoriteControl = GetFavoriteContent();
            if (favoriteControl != null)
            {
                var sv = FindChild<ScrollViewer>(favoriteControl);
                if (sv != null)
                {
                    sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                    sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }
            }
        }

        private void SetVerticalLayout()
        {
            var rootGrid = this.FindName("RootGrid") as Grid;
            var navPanel = this.FindName("NavPanel") as Grid;
            var contentPanel = this.FindName("ContentPanel") as Grid;
            var navColumn = this.FindName("NavColumn") as ColumnDefinition;
            var navRow = this.FindName("NavRow") as RowDefinition;

            if (rootGrid == null || navPanel == null || contentPanel == null) return;
            navColumn.Width = new GridLength(0);
            navRow.Height = new GridLength(48);

            // 内容：上方，占满宽度
            Grid.SetColumn(contentPanel, 0);
            Grid.SetRow(contentPanel, 0);
            Grid.SetColumnSpan(contentPanel, 2);
            Grid.SetRowSpan(contentPanel, 1);

            // 导航栏：下方，占满宽度
            Grid.SetColumn(navPanel, 0);
            Grid.SetRow(navPanel, 1);
            Grid.SetColumnSpan(navPanel, 2);
            Grid.SetRowSpan(navPanel, 1);

            // 导航栏内部：横向排列页签
            var pagesStack = GetPagesStack();
            if (pagesStack != null)
                pagesStack.Orientation = Orientation.Horizontal;

            // 内容区：纵向排列收藏项
            var stack = GetFavoriteContent()?.FindName("FavoriteStackPanel") as StackPanel;
            if (stack != null)
                stack.Orientation = Orientation.Vertical;

            var favoriteControl = GetFavoriteContent();
            if (favoriteControl != null)
            {
                var sv = FindChild<ScrollViewer>(favoriteControl);
                if (sv != null)
                {
                    sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                }
            }
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

        #region 页面管理（不变）

        private void LoadPages()
        {
            if (!Directory.Exists(AppConsts.FavoriteDir))
                Directory.CreateDirectory(AppConsts.FavoriteDir);

            _pages = Directory.GetDirectories(AppConsts.FavoriteDir)
                              .Select(Path.GetFileName)
                              .ToList();

            if (!_pages.Contains("Default"))
            {
                _pages.Insert(0, "Default");
                string defaultPath = Path.Combine(AppConsts.FavoriteDir, "Default");
                if (!Directory.Exists(defaultPath))
                    Directory.CreateDirectory(defaultPath);
            }

            RefreshPageTabs();
        }

        private void RefreshPageTabs()
        {
            var stack = GetPagesStack();
            if (stack == null) return;
            stack.Children.Clear();

            var reversedPages = new List<string>(_pages);
            reversedPages.Reverse();

            foreach (var page in reversedPages)
            {
                var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };

                var btn = new Button
                {
                    Content = page.Length > 2 ? page.Substring(0, 2) : page,
                    Style = (Style)FindResource("RoundedMenuButtonStyle"),
                    Width = 32,
                    Height = 32,
                    ToolTip = page,
                    Tag = page
                };

                if (page == _activePage)
                {
                    btn.Background = (Brush)FindResource("SystemAccentBrush");
                    btn.Foreground = Brushes.White;
                }

                btn.Click += (s, e) =>
                {
                    _activePage = (string)((Button)s).Tag;
                    GetFavoriteContent()?.LoadFavorites(_activePage);
                    RefreshPageTabs();
                };

                grid.Children.Add(btn);

                if (page != "Default")
                {
                    string pagePath = Path.Combine(AppConsts.FavoriteDir, page);
                    bool isEmpty = !Directory.Exists(pagePath) || !Directory.GetFiles(pagePath).Any();

                    if (isEmpty)
                    {
                        var delBtn = new Button
                        {
                            Style = (Style)FindResource("OtherCloseButtonStyle"),
                            Width = 14,
                            Height = 14,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new Thickness(0, -2, -2, 0),
                            Content = "×",
                            FontSize = 10,
                            Padding = new Thickness(0),
                            Tag = page
                        };
                        delBtn.Click += (s, e) =>
                        {
                            try
                            {
                                if (Directory.Exists(pagePath)) Directory.Delete(pagePath, true);
                                _pages.Remove(page);
                                if (_activePage == page) _activePage = "Default";
                                GetFavoriteContent()?.LoadFavorites(_activePage);
                                RefreshPageTabs();
                            }
                            catch { }
                        };
                        grid.Children.Add(delBtn);
                    }

                    var menu = new ContextMenu();
                    var deleteItem = new MenuItem { Header = FindResource("L_Ctx_DeleteFile") };
                    deleteItem.Click += (s2, e2) =>
                    {
                        if (MessageBox.Show("Delete page and all its images?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            try
                            {
                                Directory.Delete(Path.Combine(AppConsts.FavoriteDir, page), true);
                                _pages.Remove(page);
                                if (_activePage == page) _activePage = "Default";
                                GetFavoriteContent()?.LoadFavorites(_activePage);
                                RefreshPageTabs();
                            }
                            catch { }
                        }
                    };
                    menu.Items.Add(deleteItem);
                    btn.ContextMenu = menu;
                }

                stack.Children.Add(grid);
            }
        }

        private void AddPage_Click(object sender, RoutedEventArgs e)
        {
            string newPageName = "Page " + (_pages.Count);
            int i = _pages.Count;
            while (_pages.Contains(newPageName)) newPageName = "Page " + (++i);

            try
            {
                Directory.CreateDirectory(Path.Combine(AppConsts.FavoriteDir, newPageName));
                _pages.Add(newPageName);
                _activePage = newPageName;
                GetFavoriteContent()?.LoadFavorites(_activePage);
                RefreshPageTabs();
            }
            catch { }
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
                _isSnapped = true; // 重新打开时默认吸附
                this.Show();
                UpdateSizeForSnapMode(); MoveToSnappedPosition(); this.Activate();
                GetFavoriteContent()?.Focus();
                GetFavoriteContent()?.LoadFavorites(_activePage);
                RefreshPageTabs();
            }
        }

        #endregion
    }
}
