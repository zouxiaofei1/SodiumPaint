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
            
            // 手动绑定事件以提高编译稳定性
            if (ToleranceCombo != null)
            {
                ToleranceCombo.SelectionChanged += ToleranceCombo_SelectionChanged;
                ToleranceCombo.PreviewKeyDown += ToleranceCombo_PreviewKeyDown;
                ToleranceCombo.LostFocus += ToleranceCombo_LostFocus;
                ToleranceCombo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, 
                    new TextChangedEventHandler(ToleranceCombo_TextChanged));
            }
        }

        public int Tolerance
        {
            get => ToleranceSlider != null ? (int)ToleranceSlider.Value : 20;
            set
            {
                if (ToleranceSlider != null && ToleranceSlider.Value != value)
                {
                    _isUpdating = true;
                    ToleranceSlider.Value = value;
                    if (ToleranceCombo != null) ToleranceCombo.Text = value.ToString();
                    _isUpdating = false;
                }
            }
        }

        private void ToleranceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating || ToleranceCombo == null) return;

            int val = (int)e.NewValue;
            _isUpdating = true;
            ToleranceCombo.Text = val.ToString();
            _isUpdating = false;

            ToleranceChanged?.Invoke(this, val);
        }

        private void ToleranceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || ToleranceCombo == null) return;
            if (ToleranceCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int val))
            {
                UpdateTolerance(val);
            }
        }

        private void ToleranceCombo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || ToleranceCombo == null) return;
            if (int.TryParse(ToleranceCombo.Text, out int val))
            {
                UpdateTolerance(val, false); // Don't update text while typing
            }
        }

        private void ToleranceCombo_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyInput();
                e.Handled = true;
            }
        }

        private void ToleranceCombo_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyInput();
        }

        private void UpdateTolerance(int val, bool updateText = true)
        {
            if (ToleranceSlider == null) return;
            val = Math.Max(0, Math.Min(255, val));
            _isUpdating = true;
            ToleranceSlider.Value = val;
            if (updateText && ToleranceCombo != null) ToleranceCombo.Text = val.ToString();
            _isUpdating = false;
            ToleranceChanged?.Invoke(this, val);
        }

        private void ApplyInput()
        {
            if (ToleranceCombo == null) return;
            if (int.TryParse(ToleranceCombo.Text, out int val))
            {
                UpdateTolerance(val);
            }
            else
            {
                ToleranceCombo.Text = Tolerance.ToString();
            }
        }

        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject obj && (IsDescendantOf(obj, typeof(Slider)) || IsDescendantOf(obj, typeof(ComboBox)) || IsDescendantOf(obj, typeof(TextBox))))
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
