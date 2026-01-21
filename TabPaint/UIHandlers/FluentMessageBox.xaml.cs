using System.Windows;
using System.Windows.Input;

namespace TabPaint
{
    public partial class FluentMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private FluentMessageBox()
        {
            InitializeComponent();
        }

        public static MessageBoxResult Show(string message, string title = "TabPaint", MessageBoxButton button = MessageBoxButton.OK, Window owner = null)
        {
            var msgBox = new FluentMessageBox();
            msgBox.TxtTitle.Text = title;
            msgBox.TxtMessage.Text = message;

            if (owner != null)
            {
                msgBox.Owner = owner;
                msgBox.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                msgBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            msgBox.SetupButtons(button);
            msgBox.ShowDialog();
            return msgBox.Result;
        }

        private void SetupButtons(MessageBoxButton button)
        {
            switch (button)
            {
                case MessageBoxButton.OK:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnOk.IsDefault = true;
                    break;

                case MessageBoxButton.OKCancel:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnOk.IsDefault = true;
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnCancel.IsCancel = true;
                    break;

                case MessageBoxButton.YesNo:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnYes.IsDefault = true;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnNo.IsCancel = true; // No 绑定 Esc
                    break;

                case MessageBoxButton.YesNoCancel:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnYes.IsDefault = true;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnCancel.IsCancel = true;
                    break;
            }
        }
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (BtnCancel.Visibility == Visibility.Visible)
            {
                Result = MessageBoxResult.Cancel;
            }
            else if (BtnNo.Visibility == Visibility.Visible)
            {
                Result = MessageBoxResult.No; // 或者 .None
            }
            else
            {
                Result = MessageBoxResult.None; // 强行关闭视为不做操作
            }
            Close();
        }

        // 按钮响应
        private void BtnOk_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.OK; Close(); }
        private void BtnYes_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Yes; Close(); }
        private void BtnNo_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.No; Close(); }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Cancel; Close(); }
    }
}
