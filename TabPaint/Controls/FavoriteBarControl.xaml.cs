using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabPaint.Controls
{
    public partial class FavoriteBarControl : UserControl
    {
        public event EventHandler CloseRequested;
        public event Action<string> ImageSelected;
        public event EventHandler FavoritesChanged;

        private string _currentPage = "Default";

        private StackPanel GetFavoriteStack()
        {
            return this.FindName("FavoriteStackPanel") as StackPanel;
        }

        public FavoriteBarControl()
        {
            InitializeComponent();
            
            this.Loaded += (s, e) => {
                if (!string.IsNullOrEmpty(_currentPage))
                    LoadFavorites(_currentPage);
            };

            this.KeyDown += FavoriteBarControl_KeyDown;
            this.Focusable = true;
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

        public void LoadFavorites(string pageName = "Default")
        {
            _currentPage = pageName;
            var stack = GetFavoriteStack();
            if (stack == null) return;
            
            stack.Children.Clear();

            string pagePath = Path.Combine(AppConsts.FavoriteDir, _currentPage);
            if (!Directory.Exists(pagePath))
            {
                Directory.CreateDirectory(pagePath);
            }

            var files = Directory.GetFiles(pagePath)
                                 .Where(f => AppConsts.IsSupportedImage(f))
                                 .OrderByDescending(f => File.GetCreationTime(f));

            foreach (var file in files)
            {
                AddImageToUI(file);
            }

            AddPlusButton();
        }

        private void AddPlusButton()
        {
            var stack = GetFavoriteStack();
            if (stack == null) return;

            var grid = new Grid
            {
                Width = 120,
                Height = 120,
                Margin = new Thickness(6),
                ToolTip = FindResource("L_Menu_File_Add")
            };

            var rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = (Brush)FindResource("BorderBrush"),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                RadiusX = 6,
                RadiusY = 6,
                Fill = (Brush)FindResource("GlassBackgroundLowBrush"),
                SnapsToDevicePixels = true
            };
            grid.Children.Add(rect);

            var iconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M11,19V13H5V11H11V5H13V11H19V13H13V19H11Z"),
                Fill = (Brush)FindResource("IconFillBrush"),
                Width = 24,
                Height = 24,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(iconPath);

            grid.Cursor = Cursors.Hand;
            grid.MouseLeftButtonDown += (s, e) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = string.Format(AppConsts.ImageFilterFormat, "Image Files", "PNG", "JPG", "WEBP", "BMP", "GIF", "TIF", "ICO", "SVG"),
                    Multiselect = true
                };
                if (dialog.ShowDialog() == true)
                {
                    foreach (var file in dialog.FileNames)
                    {
                        AddFavoriteFile(file);
                    }
                }
            };

            stack.Children.Add(grid);
        }

        private void AddImageToUI(string filePath)
        {
            var stack = GetFavoriteStack();
            if (stack == null) return;

            var grid = new Grid
            {
                Width = 120,
                Height = 120,
                Margin = new Thickness(6),
            };

            var border = new Border
            {
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                ToolTip = Path.GetFileName(filePath),
                Background = (Brush)FindResource("GlassBackgroundLowBrush"),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Opacity = 0.1 }
            };

            var img = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(4)
            };

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

            var deleteBtn = new Button
            {
                Style = (Style)FindResource("OtherCloseButtonStyle"),
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
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
                    stack.Children.Remove(grid);
                    FavoritesChanged?.Invoke(this, EventArgs.Empty);
                }
                catch { }
                e.Handled = true;
            };

            border.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    DataObject data = new DataObject();
                    data.SetData(DataFormats.FileDrop, new string[] { filePath });
                    var fileList = new System.Collections.Specialized.StringCollection { filePath };
                    data.SetFileDropList(fileList);
                    DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
                }
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    ImageSelected?.Invoke(filePath);
                }
            };

            var menu = new ContextMenu { Style = (Style)FindResource("Win11ContextMenuStyle") };
            var openItem = new MenuItem { Header = FindResource("L_Menu_File_Open"), Style = (Style)FindResource("Win11MenuItemStyle") };
            openItem.Click += (s, e) => ImageSelected?.Invoke(filePath);

            var deleteItem = new MenuItem { Header = FindResource("L_Ctx_DeleteFile"), Style = (Style)FindResource("Win11MenuItemStyle"), Foreground = Brushes.Red };
            deleteItem.Click += (s, e) =>
            {
                try 
                { 
                    File.Delete(filePath); 
                    stack.Children.Remove(grid); 
                    FavoritesChanged?.Invoke(this, EventArgs.Empty);
                } catch { }
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

            if (stack.Children.Count > 0)
            {
                stack.Children.Insert(stack.Children.Count - 1, grid);
            }
            else
            {
                stack.Children.Add(grid);
            }
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
                string pagePath = Path.Combine(AppConsts.FavoriteDir, _currentPage);
                string destPath = Path.Combine(pagePath, fileName);

                int i = 1;
                while (File.Exists(destPath))
                {
                    destPath = Path.Combine(pagePath, Path.GetFileNameWithoutExtension(fileName) + $"_{i++}" + Path.GetExtension(fileName));
                }

                File.Copy(filePath, destPath);
                AddImageToUI(destPath);
                FavoritesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        private void SaveAndAddImage(BitmapSource bitmap)
        {
            try
            {
                string fileName = string.Format("Pasted_{0:yyyyMMdd_HHmmss}.png", DateTime.Now);
                string pagePath = Path.Combine(AppConsts.FavoriteDir, _currentPage);
                string destPath = Path.Combine(pagePath, fileName);

                using (var fileStream = new FileStream(destPath, FileMode.Create))
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(fileStream);
                }
                AddImageToUI(destPath);
                FavoritesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }
    }
}
