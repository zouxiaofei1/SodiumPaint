using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TabPaint.Controls
{
    public partial class TaskProgressFloat : UserControl
    {
        private bool _isDragging = false;
        private Point _startPoint;
        public event EventHandler CancelRequested;
        public bool IsDraggable { get; set; } = true;
        public TaskProgressFloat()
        {
            InitializeComponent();
        }

        public void SetIcon(string icon)
        {
            // 使用 FindName 以避免某些环境下的编译引用问题
            var iconText = this.FindName("TaskIconText") as TextBlock;
            if (iconText != null) iconText.Text = icon;
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
            var percentageText = this.FindName("PercentageText") as TextBlock;
            if (percentageText != null) percentageText.Text = $"{p:F1}%";

            double totalWidth = this.ActualWidth > 0 ? this.ActualWidth - 26 : 274;
            var progressBarFill = this.FindName("ProgressBarFill") as FrameworkElement;
            if (progressBarFill != null) progressBarFill.Width = (p / 100.0) * totalWidth;

            if (!string.IsNullOrEmpty(taskName))
            {
                var taskNameText = this.FindName("TaskNameText") as TextBlock;
                if (taskNameText != null) taskNameText.Text = taskName;
            }

            // 2. 更新大小信息 (例如: 15.4 MB / 120.5 MB)
            string currentSize = FormatFileSize(status.BytesReceived);
            string totalSize = status.TotalBytes > 0 ? FormatFileSize(status.TotalBytes) : "Unknown";
            var sizeInfoText = this.FindName("SizeInfoText") as TextBlock;
            if (sizeInfoText != null) sizeInfoText.Text = $"{currentSize} / {totalSize}";

            // 3. 更新速度信息 (例如: 2.5 MB/s)
            var speedText = this.FindName("SpeedText") as TextBlock;
            if (speedText != null) speedText.Text = $"{FormatFileSize((long)status.SpeedBytesPerSecond)}/s";
        }

        /// <summary>
        /// 通用的进度更新方法，适用于非下载任务
        /// </summary>
        public void UpdateProgress(double percentage, string taskName = null, string leftText = null, string rightText = null)
        {
            if (this.Visibility != Visibility.Visible)
            {
                this.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                this.BeginAnimation(OpacityProperty, fadeIn);
            }

            double p = Math.Max(0, Math.Min(100, percentage));
            var percentageText = this.FindName("PercentageText") as TextBlock;
            if (percentageText != null) percentageText.Text = $"{p:F1}%";

            double totalWidth = this.ActualWidth > 0 ? this.ActualWidth - 26 : 274;
            var progressBarFill = this.FindName("ProgressBarFill") as FrameworkElement;
            if (progressBarFill != null) progressBarFill.Width = (p / 100.0) * totalWidth;

            if (!string.IsNullOrEmpty(taskName))
            {
                var taskNameText = this.FindName("TaskNameText") as TextBlock;
                if (taskNameText != null) taskNameText.Text = taskName;
            }
            if (leftText != null)
            {
                var sizeInfoText = this.FindName("SizeInfoText") as TextBlock;
                if (sizeInfoText != null) sizeInfoText.Text = leftText;
            }
            if (rightText != null)
            {
                var speedText = this.FindName("SpeedText") as TextBlock;
                if (speedText != null) speedText.Text = rightText;
            }
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
