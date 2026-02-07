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
            _owner.SizeChanged += UpdatePosition;
            _owner.StateChanged += (s, e) => {
                if (_owner.WindowState == WindowState.Minimized) this.Hide();
                else if (this.IsVisible) this.Show();
                UpdatePosition(s, e);
            };
        }

        private int _snapMode = 0; // 0: BR, 1: BL, 2: TR, 3: TL, 4: RC, 5: LC, 6: BC, 7: TC

        private void FavoriteWindow_SourceInitialized(object sender, EventArgs e)
        {
            ThemeManager.SetWindowImmersiveDarkMode(this, ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
            MicaAcrylicManager.ApplyEffect(this);
            UpdatePosition(null, null);
        }

        private void StarIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
                UpdateSnapMode();
                UpdatePosition(null, null);
            }
        }

        private void UpdateSnapMode()
        {
            if (_owner == null) return;

            double ownerLeft = _owner.Left;
            double ownerTop = _owner.Top;
            double ownerWidth = _owner.ActualWidth;
            double ownerHeight = _owner.ActualHeight;

            if (_owner.WindowState == WindowState.Maximized)
            {
                ownerLeft = SystemParameters.WorkArea.Left;
                ownerTop = SystemParameters.WorkArea.Top;
                ownerWidth = SystemParameters.WorkArea.Width;
                ownerHeight = SystemParameters.WorkArea.Height;
            }

            double relX = this.Left - ownerLeft;
            double relY = this.Top - ownerTop;

            // 磁吸锚点坐标（相对于 owner）
            var targets = new (double x, double y)[]
            {
                (ownerWidth - this.ActualWidth - 20, ownerHeight - this.ActualHeight - 40), // 0: BR
                (20, ownerHeight - this.ActualHeight - 40),                                // 1: BL
                (ownerWidth - this.ActualWidth - 20, 40),                                   // 2: TR
                (20, 40),                                                                  // 3: TL
                (ownerWidth - this.ActualWidth - 10, (ownerHeight - this.ActualHeight) / 2),// 4: RC
                (10, (ownerHeight - this.ActualHeight) / 2),                               // 5: LC
                ((ownerWidth - this.ActualWidth) / 2, ownerHeight - this.ActualHeight - 40),// 6: BC
                ((ownerWidth - this.ActualWidth) / 2, 40)                                   // 7: TC
            };

            double minSqDist = double.MaxValue;
            int bestMode = 0;

            for (int i = 0; i < targets.Length; i++)
            {
                double dist = Math.Pow(relX - targets[i].x, 2) + Math.Pow(relY - targets[i].y, 2);
                if (dist < minSqDist)
                {
                    minSqDist = dist;
                    bestMode = i;
                }
            }

            _snapMode = bestMode;
        }

        private void UpdatePosition(object sender, EventArgs e)
        {
            if (_owner == null || _owner.WindowState == WindowState.Minimized || !this.IsVisible) return;

            double ownerLeft = _owner.Left;
            double ownerTop = _owner.Top;
            double ownerWidth = _owner.ActualWidth;
            double ownerHeight = _owner.ActualHeight;

            if (_owner.WindowState == WindowState.Maximized)
            {
                ownerLeft = SystemParameters.WorkArea.Left;
                ownerTop = SystemParameters.WorkArea.Top;
                ownerWidth = SystemParameters.WorkArea.Width;
                ownerHeight = SystemParameters.WorkArea.Height;
            }

            double left = this.Left;
            double top = this.Top;

            switch (_snapMode)
            {
                case 0: // BR
                    left = ownerLeft + ownerWidth - this.ActualWidth - 20;
                    top = ownerTop + ownerHeight - this.ActualHeight - 40;
                    break;
                case 1: // BL
                    left = ownerLeft + 20;
                    top = ownerTop + ownerHeight - this.ActualHeight - 40;
                    break;
                case 2: // TR
                    left = ownerLeft + ownerWidth - this.ActualWidth - 20;
                    top = ownerTop + 40;
                    break;
                case 3: // TL
                    left = ownerLeft + 20;
                    top = ownerTop + 40;
                    break;
                case 4: // RC
                    left = ownerLeft + ownerWidth - this.ActualWidth - 10;
                    top = ownerTop + (ownerHeight - this.ActualHeight) / 2;
                    break;
                case 5: // LC
                    left = ownerLeft + 10;
                    top = ownerTop + (ownerHeight - this.ActualHeight) / 2;
                    break;
                case 6: // BC
                    left = ownerLeft + (ownerWidth - this.ActualWidth) / 2;
                    top = ownerTop + ownerHeight - this.ActualHeight - 40;
                    break;
                case 7: // TC
                    left = ownerLeft + (ownerWidth - this.ActualWidth) / 2;
                    top = ownerTop + 40;
                    break;
            }

            this.Left = left;
            this.Top = top;
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
