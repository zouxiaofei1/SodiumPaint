using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TabPaint
{
    public partial class FluentMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private const string PathInfo = AppConsts.PathInfo;
        private const string PathQuestion = AppConsts.PathQuestion;
        private const string PathWarning = AppConsts.PathWarning;
        private const string PathError = AppConsts.PathError;
        private static readonly bool _isWin11 = MicaAcrylicManager.IsWin11();
        private string _logFolderPath = null;

        private FluentMessageBox()
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            if (!_isWin11)
            {
                FluentMsgboxRootBorder.CornerRadius =new CornerRadius(0);
                FluentSecondBorder.CornerRadius = new CornerRadius(0);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);
            MicaAcrylicManager.ApplyEffect(this);
            if (!MicaAcrylicManager.IsWin11())
            {
                this.Background = FindResource("ControlBackgroundBrush") as Brush;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) == null)
            {
                this.DragMove();
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
        public static MessageBoxResult Show(
     string message,
     string title = "TabPaint",
     MessageBoxButton button = MessageBoxButton.OK,
     MessageBoxImage icon = MessageBoxImage.Information,
     Window owner = null,
     string logFolderPath = null)
        {
            var msgBox = new FluentMessageBox();
            msgBox.TxtTitle.Text = title;
            msgBox.TxtMessage.Text = message;
            msgBox._logFolderPath = logFolderPath;

            if (!string.IsNullOrEmpty(logFolderPath))
            {
                msgBox.BtnOpenLog.Visibility = Visibility.Visible;
            }

            msgBox.SetupButtons(button);
            msgBox.SetupIcon(icon);

            if (owner != null)
            {
                msgBox.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ShowOwnerModal(msgBox, owner);
            }
            else
            {
                Window activeOwner = GetActiveWindow();
                if (activeOwner != null)
                {
                    msgBox.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    ShowOwnerModal(msgBox, activeOwner);
                }
                else
                {
                    msgBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    ShowNoOwnerModal(msgBox);
                }
            }

            return msgBox.Result;
        }

        private static Window GetActiveWindow()
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w.IsActive && w is not FluentMessageBox)
                    return w;
            }
            return MainWindow.GetCurrentInstance();
        }

        private static void ShowOwnerModal(FluentMessageBox dialog, Window owner)
        {
            try
            {
                dialog.Owner = owner;
            }
            catch
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            owner.IsEnabled = false;

            var frame = new DispatcherFrame();

            CancelEventHandler ownerClosing = null;
            ownerClosing = (s, e) =>
            {
                if (dialog.IsVisible) dialog.Close();
            };
            owner.Closing += ownerClosing;

            dialog.Closed += (s, e) =>
            {
                owner.Closing -= ownerClosing;
                owner.IsEnabled = true;
                if (owner.IsVisible)
                {
                    owner.Activate();
                    owner.Focus();
                }
                frame.Continue = false;
            };

            dialog.Show();
            Dispatcher.PushFrame(frame);
        }
        private static void ShowNoOwnerModal(FluentMessageBox dialog)
        {
            var frame = new DispatcherFrame();

            dialog.Closed += (s, e) =>
            {
                frame.Continue = false;
            };

            dialog.Show();
            Dispatcher.PushFrame(frame);
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (BtnCancel.Visibility == Visibility.Visible) Result = MessageBoxResult.Cancel;
            else if (BtnNo.Visibility == Visibility.Visible)
                Result = MessageBoxResult.No;
            else
                Result = MessageBoxResult.None;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)  { Result = MessageBoxResult.OK; Close();}

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }

        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_logFolderPath) && System.IO.Directory.Exists(_logFolderPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _logFolderPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to open log directory: " + ex.Message);
                }
            }
        }

        private void SetupIcon(MessageBoxImage icon)
        {
            if (icon == MessageBoxImage.None)
            {
                IconContainer.Visibility = Visibility.Collapsed;
                return;
            }
            IconContainer.Visibility = Visibility.Visible;

            string pathData = PathInfo;
            string colorKey = "SystemAccentBrush";

            switch (icon)
            {
                case MessageBoxImage.Error:
                    pathData = PathError;
                    colorKey = "DangerBrush";
                    break;
                case MessageBoxImage.Question:
                    pathData = PathQuestion;
                    colorKey = "SystemAccentBrush";
                    break;
                case MessageBoxImage.Warning:
                    pathData = PathWarning;
                    colorKey = "WarningBrush";
                    break;
                case MessageBoxImage.Information:
                    pathData = PathInfo;
                    colorKey = "SystemAccentBrush";
                    break;
            }

            try{ IconPath.Data = Geometry.Parse(pathData);  }
            catch
            {
                IconPath.Data = null;
            }

            IconPath.Fill = TryFindResource(colorKey) is Brush brush ? brush : Brushes.Black;
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
                    BtnNo.IsCancel = true;
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
    }
}
