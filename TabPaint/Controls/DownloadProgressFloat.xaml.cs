using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TabPaint.Controls
{
    public partial class DownloadProgressFloat : UserControl
    {
        private bool _isDragging = false;
        private Point _startPoint;
        public event EventHandler CancelRequested;
        public bool IsDraggable { get; set; } = true;
        public DownloadProgressFloat()
        {
            InitializeComponent();
        }

        // 外部调用的更新方法
        public void UpdateProgress(AiDownloadStatus status, string taskName = null)
        {
            if (this.Visibility != Visibility.Visible)
            {
                this.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                this.BeginAnimation(OpacityProperty, fadeIn);
            }

            // 1. 更新百分比和进度条
            double p = Math.Max(0, Math.Min(100, status.Percentage));
            PercentageText.Text = $"{p:F1}%";

            double totalWidth = this.ActualWidth > 0 ? this.ActualWidth - 26 : 274;
            ProgressBarFill.Width = (p / 100.0) * totalWidth;

            if (!string.IsNullOrEmpty(taskName))
            {
                TaskNameText.Text = taskName;
            }

            // 2. 更新大小信息 (例如: 15.4 MB / 120.5 MB)
            string currentSize = FormatFileSize(status.BytesReceived);
            string totalSize = status.TotalBytes > 0 ? FormatFileSize(status.TotalBytes) : "Unknown";
            SizeInfoText.Text = $"{currentSize} / {totalSize}";

            // 3. 更新速度信息 (例如: 2.5 MB/s)
            SpeedText.Text = $"{FormatFileSize((long)status.SpeedBytesPerSecond)}/s";
        }
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        public void Finish()
        {
            // 简单的淡出动画后隐藏
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
            fadeOut.Completed += (s, e) => { this.Visibility = Visibility.Collapsed; };
            this.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);

            this.Visibility = Visibility.Collapsed;
        }

        // 简单的拖动逻辑 (通过 RenderTransform)
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsDraggable) return;
            if (e.OriginalSource is DependencyObject obj && IsDescendantOf(obj, typeof(Button)))
                return;

            _isDragging = true;
            _startPoint = e.GetPosition(this.Parent as UIElement);
            this.CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!IsDraggable) { base.OnMouseMove(e); return; }
            if (_isDragging)
            {
                var currentPoint = e.GetPosition(this.Parent as UIElement);
                var diff = currentPoint - _startPoint;

                // 使用 RenderTransform 进行移动
                if (!(this.RenderTransform is TranslateTransform))
                {
                    this.RenderTransform = new TranslateTransform();
                }

                var transform = (TranslateTransform)this.RenderTransform;
                transform.X += diff.X;
                transform.Y += diff.Y;

                _startPoint = currentPoint;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
            base.OnMouseUp(e);
        }

        // 辅助判断点击源
        private bool IsDescendantOf(DependencyObject node, Type type)
        {
            while (node != null)
            {
                if (node.GetType() == type) return true;
                node = System.Windows.Media.VisualTreeHelper.GetParent(node);
            }
            return false;
        }
    }
}
