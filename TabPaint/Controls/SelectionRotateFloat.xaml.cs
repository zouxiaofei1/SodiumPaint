using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TabPaint.Controls
{
    public partial class SelectionRotateFloat : UserControl
    {
        public event RoutedEventHandler AngleChanged;

        public SelectionRotateFloat()
        {
            InitializeComponent();
        }

        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private bool _isUpdating = false;

        private void RotateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            if (RotateValueText != null)
                RotateValueText.Text = ((int)e.NewValue).ToString();

            AngleChanged?.Invoke(this, new RoutedEventArgs());

            _isUpdating = false;
        }

        private void RotateValueText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            if (int.TryParse(RotateValueText.Text, out int val))
            {
                if (val < -180) val = -180;
                if (val > 180) val = 180;
                RotateSlider.Value = val;

                AngleChanged?.Invoke(this, new RoutedEventArgs());
            }
            _isUpdating = false;
        }

        public void SetValue(int value)
        {
            _isUpdating = true;
            RotateSlider.Value = value;
            RotateValueText.Text = value.ToString();
            _isUpdating = false;
        }
    }
}
