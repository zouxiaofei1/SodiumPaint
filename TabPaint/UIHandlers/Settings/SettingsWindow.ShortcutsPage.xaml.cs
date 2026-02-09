using System.Windows;
using System.Windows.Controls;

namespace TabPaint.Pages
{
    public partial class ShortcutsPage : UserControl
    {
        public ShortcutsPage()
        {
            InitializeComponent();
        }

        private void ResetShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var result = FluentMessageBox.Show(
              LocalizationManager.GetString("L_Settings_Shortcuts_Reset_Confirm"),
              LocalizationManager.GetString("L_Settings_Shortcuts_Reset_Title"),
              MessageBoxButton.YesNo,
              MessageBoxImage.Question,
               MainWindow.GetCurrentInstance()
            );

            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.Instance.Current.ResetShortcutsToDefault();
            }
        }
    }
}
