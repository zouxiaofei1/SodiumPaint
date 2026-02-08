using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private Window _owner;
        private List<string> _pages = new List<string>();
        private string _activePage = "Default";

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
            _owner.LocationChanged += UpdatePosition;
            _owner.SizeChanged += (s, e) =>
            {
                UpdateSizeForSnapMode();
                UpdatePosition(s, e);
            };
            _owner.StateChanged += (s, e) =>
            {
                if (_owner.WindowState == WindowState.Minimized) this.Hide();
                else if (this.IsVisible) this.Show();
                UpdateSizeForSnapMode();
                UpdatePosition(s, e);
            };
        }

        private int _snapMode = 0;

        private void FavoriteWindow_SourceInitialized(object sender, EventArgs e)
        {
            ThemeManager.SetWindowImmersiveDarkMode(this, ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
            MicaAcrylicManager.ApplyEffect(this);
            UpdateSizeForSnapMode();
            UpdatePosition(null, null);
        }

        private void StarIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
                UpdateSnapMode();
                UpdateSizeForSnapMode();
                UpdatePosition(null, null);
            }
        }
        private void UpdateSnapMode()
        {
            if (_owner == null) return;

            double ownerLeft, ownerTop, ownerWidth, ownerHeight;
            GetOwnerRect(out ownerLeft, out ownerTop, out ownerWidth, out ownerHeight);

            // 计算 FavoriteWindow 中心点
            double cx = this.Left + this.ActualWidth / 2;
            double cy = this.Top + this.ActualHeight / 2;

            // 计算主窗口中心
            double ownerCx = ownerLeft + ownerWidth / 2;
            double ownerCy = ownerTop + ownerHeight / 2;

            // 相对主窗口中心的偏移
            double dx = cx - ownerCx;
            double dy = cy - ownerCy;

            // 归一化到主窗口宽高比例，判断在哪个象限/方向
            double nx = dx / (ownerWidth / 2);
            double ny = dy / (ownerHeight / 2);

            if (Math.Abs(nx) > Math.Abs(ny))
            {
                // 水平方向更远 → 左或右
                _snapMode = nx > 0 ? 2 : 3; // 2=右, 3=左
            }
            else
            {
                // 垂直方向更远 → 上或下
                _snapMode = ny > 0 ? 0 : 1; // 0=底, 1=顶
            }
        }
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
                case 1: // 底部case 1: // 顶部
                    this.Width = ownerWidth;
                    this.Height = 140;
                    SetHorizontalLayout();
                    break;
                case 2: // 右侧
                case 3: // 左侧
                    this.Width = 140;
                    this.Height = ownerHeight;
                    SetVerticalLayout();
                    break;
            }
        }
        private void SetHorizontalLayout()
        {
            var stack = GetFavoriteContent()?.FindName("FavoriteStackPanel") as StackPanel;
            if (stack != null)
            {
                stack.Orientation = Orientation.Horizontal;
            }

            // 找到 FavoriteBarControl 内的 ScrollViewer 并调整
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

        /// <summary>
        /// 设置内部布局为纵向（左侧/右侧吸附时）
        /// </summary>
        private void SetVerticalLayout()
        {
            var stack = GetFavoriteContent()?.FindName("FavoriteStackPanel") as StackPanel;
            if (stack != null)
            {
                stack.Orientation = Orientation.Vertical;
            }

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

        /// <summary>
        /// 辅助：在可视树中查找指定类型的子元素
        /// </summary>
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

        private void UpdatePosition(object sender, EventArgs e)
        {
            if (_owner == null || _owner.WindowState == WindowState.Minimized || !this.IsVisible) return;

            double ownerLeft, ownerTop, ownerWidth, ownerHeight;
            GetOwnerRect(out ownerLeft, out ownerTop, out ownerWidth, out ownerHeight);

            // 先同步尺寸
            UpdateSizeForSnapMode();

            double gap = 4; // 窗口间距

            switch (_snapMode)
            {
                case 0: // 底部外侧
                    MessageBox.Show("SnapMode: Bottom");
                    this.Left = ownerLeft;
                    this.Top = ownerTop + ownerHeight + gap;
                    break;
                case 1: // 顶部外侧
                        MessageBox.Show("SnapMode: Top");
                    this.Left = ownerLeft;
                    this.Top = ownerTop - this.Height - gap;
                    break;
                case 2: // 右侧外侧
                    MessageBox.Show("SnapMode: Right");
                    this.Left = ownerLeft + ownerWidth + gap;
                    this.Top = ownerTop;
                    break;
                case 3: // 左侧外侧
                    MessageBox.Show("SnapMode: Left");
                    this.Left = ownerLeft - this.Width - gap;
                    this.Top = ownerTop;
                    break;
            }

            // ★ 边界保护：确保不超出屏幕工作区
            var workArea = SystemParameters.WorkArea;
            if (this.Left < workArea.Left) this.Left = workArea.Left;
            if (this.Top < workArea.Top) this.Top = workArea.Top;
            if (this.Left + this.Width > workArea.Right) this.Left = workArea.Right - this.Width;
            if (this.Top + this.Height > workArea.Bottom) this.Top = workArea.Bottom - this.Height;
        }
      

        private void LoadPages()
        {
            if (!Directory.Exists(AppConsts.FavoriteDir))
            {
                Directory.CreateDirectory(AppConsts.FavoriteDir);
            }

            _pages = Directory.GetDirectories(AppConsts.FavoriteDir)
                              .Select(Path.GetFileName)
                              .ToList();

            if (!_pages.Contains("Default"))
            {
                _pages.Insert(0, "Default");
                string defaultPath = Path.Combine(AppConsts.FavoriteDir, "Default");
                if (!Directory.Exists(defaultPath))
                {
                    Directory.CreateDirectory(defaultPath);
                }
            }

            RefreshPageTabs();
        }

        private void RefreshPageTabs()
        {
            var stack = GetPagesStack();
            if (stack == null) return;
            stack.Children.Clear();
            
            // 页面从下往上排：反转列表顺序，使最新的（或后添加的）在最上方生长，Default 在最下方
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

                // 如果是空页面且不是 Default，显示右上角删除按钮
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
            while (_pages.Contains(newPageName))
            {
                newPageName = "Page " + (++i);
            }

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
                this.Show();
                UpdatePosition(null, null);
                this.Activate();
                GetFavoriteContent()?.Focus();
                GetFavoriteContent()?.LoadFavorites(_activePage);
                RefreshPageTabs();
            }
        }
    }
}
