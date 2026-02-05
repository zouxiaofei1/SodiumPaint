using System.Windows;
using System.Windows.Controls;
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
                // 直接更新设置中的强调色
                // 注意：这里假设 SettingsManager.Instance.Current 不为 null
                if (SettingsManager.Instance?.Current != null)
                {
                    SettingsManager.Instance.Current.ThemeAccentColor = brush.Color.ToString();
                }
            }
        }
    }
}
