using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace TabPaint.Pages
{

    public partial class GeneralPage : UserControl
    {
        public GeneralPage()
        {
            InitializeComponent();
        }

        private void OnColorRadioClick(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Background is SolidColorBrush brush)
            {
                if (SettingsManager.Instance?.Current != null)
                {
                    SettingsManager.Instance.Current.ThemeAccentColor = brush.Color.ToString();
                }
            }
        }
    }
}
