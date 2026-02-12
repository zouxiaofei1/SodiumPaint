using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Controls
{
    public partial class WandToleranceFloat : UserControl
    {
        private bool _isDragging = false;
        private Point _startPoint;
        private bool _isUpdating = false;

        public event EventHandler<int> ToleranceChanged;

        public WandToleranceFloat()
        {
            InitializeComponent();
        }

        public int Tolerance
        {
            get => (int)ToleranceSlider.Value;
            set
            {
                if (ToleranceSlider.Value != value)
                {
                    _isUpdating = true;
                    ToleranceSlider.Value = value;
                    ToleranceInput.Text = value.ToString();
                    _isUpdating = false;
                }
            }
        }

        private void ToleranceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;

            int val = (int)e.NewValue;
            _isUpdating = true;
            ToleranceInput.Text = val.ToString();
            _isUpdating = false;

            ToleranceChanged?.Invoke(this, val);
        }

        private void ToleranceInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyInput();
                e.Handled = true;
            }
        }

        private void ToleranceInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyInput();
        }

        private void ApplyInput()
        {
            if (int.TryParse(ToleranceInput.Text, out int val))
            {
                val = Math.Max(0, Math.Min(255, val));
                Tolerance = val;
                ToleranceChanged?.Invoke(this, val);
            }
            else
            {
                ToleranceInput.Text = Tolerance.ToString();
            }
        }

        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject obj && (IsDescendantOf(obj, typeof(Slider)) || IsDescendantOf(obj, typeof(TextBox))))
                return;

            _isDragging = true;
            _startPoint = e.GetPosition(this.Parent as UIElement);
            this.CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPoint = e.GetPosition(this.Parent as UIElement);
                var diff = currentPoint - _startPoint;
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
                node = VisualTreeHelper.GetParent(node);
            }
            return false;
        }

        public void Show()
        {
            this.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
        }
    }
}
