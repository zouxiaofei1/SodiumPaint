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

        private string _logFolderPath = null;

        private FluentMessageBox()
        {
            InitializeComponent();
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

        // =====================================================
        // ★ 核心改动：静态 Show 方法使用局部模态
        // =====================================================
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

                // ★ 使用局部模态：只阻塞 owner，不影响其他主窗口
                ShowOwnerModal(msgBox, owner);
            }
            else
            {
                // 无 owner 时居中屏幕，使用普通 ShowDialog
                // （此时没有特定窗口需要保护，ShowDialog 是安全的）
                msgBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                msgBox.ShowDialog();
            }

            return msgBox.Result;
        }

        // =====================================================
        // ★ 内置的局部模态实现（针对 MessageBox 场景优化）
        // =====================================================
        private static void ShowOwnerModal(FluentMessageBox dialog, Window owner)
        {
            dialog.Owner = owner;
            owner.IsEnabled = false;

            var frame = new DispatcherFrame();

            // 防止 Owner 关闭时子窗口悬空
            CancelEventHandler ownerClosing = null;
            ownerClosing = (s, e) =>
            {
                if (dialog.IsVisible)
                    dialog.Close();
            };
            owner.Closing += ownerClosing;

            dialog.Closed += (s, e) =>
            {
                owner.Closing -= ownerClosing;
                owner.IsEnabled = true;
                if (owner.IsVisible)
                    owner.Activate(); frame.Continue = false;
            };

            // 非模态显示，但通过 DispatcherFrame 同步等待
            dialog.Show();
            Dispatcher.PushFrame(frame);
        }

        // =====================================================
        // 按钮事件：不再设置 DialogResult，直接 Close()
        // （Result 属性已经在 Close 之前赋值了）
        // =====================================================
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (BtnCancel.Visibility == Visibility.Visible) Result = MessageBoxResult.Cancel;
            else if (BtnNo.Visibility == Visibility.Visible)
                Result = MessageBoxResult.No;
            else
                Result = MessageBoxResult.None;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }

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

            try
            {
                IconPath.Data = Geometry.Parse(pathData);
            }
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
