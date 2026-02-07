using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace TabPaint.Controls
{
    public partial class FavoriteBarControl : UserControl
    {
        public event EventHandler CloseRequested;
        public event Action<string> ImageSelected;

        public FavoriteBarControl()
        {
            InitializeComponent();
            LoadFavorites();

            // 允许粘贴
            this.KeyDown += FavoriteBarControl_KeyDown;
            this.Focusable = true;

            // 允许拖放
            this.AllowDrop = true;
        }

        private void FavoriteBarControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (Clipboard.ContainsImage())
                {
                    var bitmap = Clipboard.GetImage();
                    SaveAndAddImage(bitmap);
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    foreach (var file in files)
                    {
                        AddFavoriteFile(file);
                    }
                }
            }
        }

        public void LoadFavorites()
        {
            if (FavoriteWrapPanel == null) return;
            FavoriteWrapPanel.Children.Clear();
            if (!Directory.Exists(AppConsts.FavoriteDir))
            {
                Directory.CreateDirectory(AppConsts.FavoriteDir);
                return;
            }

            var files = Directory.GetFiles(AppConsts.FavoriteDir)
                                 .Where(f => AppConsts.IsSupportedImage(f))
                                 .OrderByDescending(f => File.GetCreationTime(f));

            foreach (var file in files)
            {
                AddImageToUI(file);
            }
        }

        private void AddImageToUI(string filePath)
        {
            var grid = new Grid
            {
                Width = 120,
                Height = 120,
                Margin = new Thickness(6),
            };

            var border = new Border
            {
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                ToolTip = Path.GetFileName(filePath),
                Background = (System.Windows.Media.Brush)FindResource("GlassBackgroundLowBrush"),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Opacity = 0.1 }
            };

            var img = new Image
            {
                Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(4)
            };

            // 使用低内存占用的方式加载缩略图
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.DecodePixelWidth = 200;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                img.Source = bitmap;
            }
            catch { }

            border.Child = img;
            grid.Children.Add(border);

            // 右上角删除按钮
            var deleteBtn = new Button
            {
                Style = (Style)FindResource("OtherCloseButtonStyle"),
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                Visibility = Visibility.Collapsed,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(0)
            };
            grid.Children.Add(deleteBtn);

            grid.MouseEnter += (s, e) => deleteBtn.Visibility = Visibility.Visible;
            grid.MouseLeave += (s, e) => deleteBtn.Visibility = Visibility.Collapsed;

            deleteBtn.Click += (s, e) =>
            {
                try
                {
                    File.Delete(filePath);
                    FavoriteWrapPanel.Children.Remove(grid);
                }
                catch { }
                e.Handled = true;
            };

            // 拖拽支持 (从 FavoriteBar 拖出)
            border.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    DataObject data = new DataObject();
                    data.SetData(DataFormats.FileDrop, new string[] { filePath });

                    // 额外支持直接拖入 Explorer 或其他应用
                    var fileList = new System.Collections.Specialized.StringCollection { filePath };
                    data.SetFileDropList(fileList);

                    DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
                }
            };

            // 双击插入到画布
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    ImageSelected?.Invoke(filePath);
                }
            };

            // 右键菜单
            var menu = new ContextMenu { Style = (Style)FindResource("Win11ContextMenuStyle") };
            var openItem = new MenuItem { Header = FindResource("L_Menu_File_Open"), Style = (Style)FindResource("Win11MenuItemStyle") };
            openItem.Click += (s, e) => ImageSelected?.Invoke(filePath);

            var deleteItem = new MenuItem { Header = FindResource("L_Ctx_DeleteFile"), Style = (Style)FindResource("Win11MenuItemStyle"), Foreground = System.Windows.Media.Brushes.Red };
            deleteItem.Click += (s, e) =>
            {
                try { File.Delete(filePath); FavoriteWrapPanel.Children.Remove(grid); } catch { }
            };

            var explorerItem = new MenuItem { Header = FindResource("L_Menu_File_OpenFolder"), Style = (Style)FindResource("Win11MenuItemStyle") };
            explorerItem.Click += (s, e) =>
            {
                try
                {
                    string argument = "/select,\"" + filePath + "\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                catch { }
            };

            menu.Items.Add(openItem);
            menu.Items.Add(explorerItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(deleteItem);
            border.ContextMenu = menu;

            FavoriteWrapPanel.Children.Add(grid);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnWrapPanelDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void OnWrapPanelDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    AddFavoriteFile(file);
                }
            }
        }

        private void AddFavoriteFile(string filePath)
        {
            if (!AppConsts.IsSupportedImage(filePath)) return;
            try
            {
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(AppConsts.FavoriteDir, fileName);

                // 处理重名
                int i = 1;
                while (File.Exists(destPath))
                {
                    destPath = Path.Combine(AppConsts.FavoriteDir, Path.GetFileNameWithoutExtension(fileName) + $"_{i++}" + Path.GetExtension(fileName));
                }

                File.Copy(filePath, destPath);
                AddImageToUI(destPath);
            }
            catch { }
        }

        private void SaveAndAddImage(BitmapSource bitmap)
        {
            try
            {
                string fileName = $"Pasted_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string destPath = Path.Combine(AppConsts.FavoriteDir, fileName);

                using (var fileStream = new FileStream(destPath, FileMode.Create))
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(fileStream);
                }
                AddImageToUI(destPath);
            }
            catch { }
        }
    }
}
