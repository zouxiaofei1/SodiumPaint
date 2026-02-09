using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabPaint.Controls;

namespace TabPaint.UIHandlers
{
    public partial class FavoriteWindow : Window
    {
        private Window _owner;
        private WindowSnapManager _snapManager;

        private List<string> _pages = new List<string>();
        private string _activePage = "Default";

        private StackPanel GetPagesStack() => this.FindName("PagesStackPanel") as StackPanel;
        private FavoriteBarControl GetFavoriteContent() => this.FindName("FavoriteContent") as FavoriteBarControl;

        public FavoriteWindow()
        {
            InitializeComponent();
            this.Loaded += (s, e) => LoadPages();
            this.SourceInitialized += OnSourceInitialized;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            ThemeManager.SetWindowImmersiveDarkMode(this, ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
            MicaAcrylicManager.ApplyEffect(this);
        }

        protected override void OnClosed(EventArgs e)
        {
           if(_snapManager!=null) _snapManager.Detach();
            base.OnClosed(e);
        }
        public void AttachToAnchor(Window newAnchor)
        {
            if (_snapManager == null)
            {
                // 首次创建
                _snapManager = new WindowSnapManager(this, newAnchor, SnapEdge.Bottom);
                _snapManager.SnapStateChanged += OnSnapStateChanged;

                if (this.IsLoaded)
                {
                    _snapManager.Attach();
                }
                else
                {
                    // 延迟到 Loaded 后 Attach
                    void handler(object s, RoutedEventArgs ev)
                    {
                        _snapManager.Attach();
                        this.Loaded -= handler;
                    }
                    this.Loaded += handler;
                }
            }
            else
            {
                // 切换锚点
                _snapManager.SwitchAnchor(newAnchor);
            }
        }
        public void RefreshContent()
        {
            GetFavoriteContent()?.LoadFavorites(_activePage);
            RefreshPageTabs();
        }

        private void OnSnapStateChanged(SnapEdge edge, bool isSnapped)
        {
            // 布局切换逻辑保持不变
        }
        #region 拖动（转发给 SnapManager）

        private void StarIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (IsInteractiveElement(e.OriginalSource as DependencyObject)) return;

            _snapManager.BeginDrag(_snapManager.GetCursorPosDIU());

            var element = sender as UIElement;
            if (element != null)
            {
                element.CaptureMouse(); element.MouseMove += StarIcon_MouseMove;
                element.MouseLeftButtonUp += StarIcon_MouseLeftButtonUp;
            }
            e.Handled = true;
        }
        public void SwitchAnchorKeepPosition(Window newAnchor)
        {
            _snapManager?.SwitchAnchorKeepPosition(newAnchor);
        }
        public bool IsSnapped => _snapManager?.IsSnapped ?? false;
        private void StarIcon_MouseMove(object sender, MouseEventArgs e)
        {
            var cursorPos = _snapManager.GetCursorPosDIU();
            _snapManager.UpdateDrag(cursorPos);

            // ★ 拖拽过程中检测最近的 MainWindow，动态切换锚点
            if (!_snapManager.IsSnapped)
            {
                var nearest = FavoriteWindowManager.FindNearestMainWindow(cursorPos);
                if (nearest != null && nearest != FavoriteWindowManager.CurrentAnchor)
                {
                    // 切换锚点但保持自由拖动状态
                    FavoriteWindowManager.SwitchAnchorDuringDrag(nearest);
                }
            }
        }

        private void StarIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _snapManager.EndDrag();

            // ★ 松手后确认最终吸附的窗口
            if (_snapManager.IsSnapped)
            {
                var cursorPos = _snapManager.GetCursorPosDIU();
                var nearest = FavoriteWindowManager.FindNearestMainWindow(cursorPos);
                if (nearest != null)
                {
                    FavoriteWindowManager.SwitchAnchor(nearest);
                }
            }

            var element = sender as UIElement;
            if (element != null)
            {
                element.ReleaseMouseCapture();
                element.MouseMove -= StarIcon_MouseMove;
                element.MouseLeftButtonUp -= StarIcon_MouseLeftButtonUp;
            }
            e.Handled = true;
        }

        private bool IsInteractiveElement(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null && current != this)
            {
                if (current is System.Windows.Controls.Primitives.ButtonBase
                    || current is TextBox
                    || current is System.Windows.Controls.Primitives.TextBoxBase
                    || current is ComboBox
                    || current is System.Windows.Controls.Primitives.ScrollBar
                    || current is System.Windows.Controls.Primitives.Thumb
                    || current is MenuItem
                    || current is ListBoxItem
                    || current is System.Windows.Controls.Primitives.Selector) return true;

                if (current is FrameworkElement fe && fe.Cursor == Cursors.Hand)
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        #endregion

        #region 页面管理（保持不变）

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
            MessageBox.Show("Favorite window is now a detachable panel. You can drag it to any MainWindow and it will remember its position. To close it, just click the star icon again or use the system close button.", "Info", MessageBoxButton.OK);
            this.Close();
        }



        #endregion
    }
}
