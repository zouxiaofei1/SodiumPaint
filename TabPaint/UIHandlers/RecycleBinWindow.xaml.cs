//这个窗口已经停止使用，相关代码仅供参考，未来可能会被删除

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace TabPaint.UIHandlers
{
    public partial class RecycleBinWindow : Window
    {
        public ObservableCollection<RecycleItem> Items { get; set; } = new ObservableCollection<RecycleItem>();

        public RecycleBinWindow()
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            RecycleItemsControl.ItemsSource = Items;
            LoadItems();
        }

        private void LoadItems()
        {
            Items.Clear();
            string cachePath = Path.Combine(AppConsts.CacheDir, "RecycleBin");
            if (!Directory.Exists(cachePath))
            {
                EmptyHint.Visibility = Visibility.Visible;
                return;
            }

            var files = Directory.GetFiles(cachePath, "*.png");
            foreach (var file in files)
            {
                var creationTime = File.GetCreationTime(file);
                var expiryDate = creationTime.AddDays(7);
                var remainingDays = (expiryDate - DateTime.Now).Days;

                if (remainingDays < 0)
                {
                    try { File.Delete(file); } catch { }
                    continue;
                }

                Items.Add(new RecycleItem
                {
                    FilePath = file,
                    Thumbnail = LoadThumbnail(file),
                    DaysLeft = remainingDays
                });
            }

            EmptyHint.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private BitmapSource LoadThumbnail(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.DecodePixelWidth = 100;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RecycleItem item)
            {
                // Logic to restore: inform MainWindow or service
                OnRestoreRequested?.Invoke(item.FilePath);
                Items.Remove(item);
                EmptyHint.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RecycleItem item)
            {
                try
                {
                    File.Delete(item.FilePath);
                    Items.Remove(item);
                    EmptyHint.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        public event Action<string> OnRestoreRequested;
    }

    public class RecycleItem
    {
        public string FilePath { get; set; }
        public BitmapSource Thumbnail { get; set; }
        public int DaysLeft { get; set; }
        public string DaysLeftString => string.Format(Application.Current.FindResource("L_RecycleBin_DaysLeft_Format").ToString(), DaysLeft);
    }
}
