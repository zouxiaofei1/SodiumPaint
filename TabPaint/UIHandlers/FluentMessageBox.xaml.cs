using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace TabPaint
{
    public partial class FluentMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        // SVG Path Data
        private const string PathInfo = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M13,17H11V11H13V17M13,9H11V7H13V9Z";
        private const string PathQuestion = "M12,2C17.52,2 22,6.48 22,12C22,17.52 17.52,22 12,22C6.48,22 2,17.52 2,12C2,6.48 6.48,2 12,2M11,19H13V17H11M12,5C10.6,5 9.27,5.57 8.5,6.5C7.94,7.2 7.7,8.08 7.75,9H9.72C9.72,8.65 10,8 11.2,7.7C12.4,7.4 13.5,8 13.7,9C13.88,10 13.25,10.5 12.6,11C11.66,11.73 11,12.5 11,14.5H13C13,13.29 13.55,12.7 14.4,12C15.5,11.16 16.5,10.2 16.27,8.2C16.06,6.42 14.5,5 12,5Z";
        private const string PathWarning = "M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16";
        private const string PathError = "M12,2C17.53,2 22,6.47 22,12C22,17.53 17.53,22 12,22C6.47,22 2,17.53 2,12C2,6.47 6.47,2 12,2M15.59,7L12,10.59L8.41,7L7,8.41L10.59,12L7,15.59L8.41,17L12,13.41L15.59,17L17,15.59L13.41,12L17,8.41L15.59,7Z";

        private FluentMessageBox()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 1. 适配深色/浅色主题
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);

            // 2. 启用 Mica 特效
            MicaAcrylicManager.ApplyEffect(this);

            // 3. 非 Win11 系统的回退处理
            if (!MicaAcrylicManager.IsWin11())
            {
                // 如果不支持 Mica，使用实色背景防止完全透明
                this.Background = FindResource("ControlBackgroundBrush") as Brush;
            }
        }

        /// <summary>
        /// 全窗口拖动逻辑
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 确保不是在按钮或其他交互控件上点击
            if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.Button>(source) == null)
            {
                this.DragMove();
            }
        }

        // 辅助方法：查找父级控件
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        public static MessageBoxResult Show(string message, string title = "TabPaint", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information, Window owner = null)
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
            msgBox.SetupIcon(icon);

            msgBox.ShowDialog();
            return msgBox.Result;
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
                    colorKey = "DangerBrush"; // 确保你在资源里定义了这个 Brush
                    break;
                case MessageBoxImage.Question:
                    pathData = PathQuestion;
                    colorKey = "SystemAccentBrush";
                    break;
                case MessageBoxImage.Warning:
                    pathData = PathWarning;
                    colorKey = "WarningBrush"; // 确保定义了
                    break;
                case MessageBoxImage.Information:
                    pathData = PathInfo;
                    colorKey = "SystemAccentBrush";
                    break;
            }

            // --- 修改点开始 ---
            try
            {
                // 显式解析并赋值
                var geometry = Geometry.Parse(pathData);
                IconPath.Data = geometry;
            }
            catch (Exception)
            {
                // 如果解析失败，至少不让程序崩，或者给一个默认形状
                IconPath.Data = null;
            }
            // --- 修改点结束 ---

            if (TryFindResource(colorKey) is Brush brush)
            {
                IconPath.Fill = brush;
            }
            else
            {
                // 如果找不到资源，给一个默认颜色，防止因为透明而"看不见"
                IconPath.Fill = Brushes.Black;
            }
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
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (BtnCancel.Visibility == Visibility.Visible)
            {
                Result = MessageBoxResult.Cancel;
            }
            else if (BtnNo.Visibility == Visibility.Visible)
            {
                Result = MessageBoxResult.No;
            }
            else
            {
                Result = MessageBoxResult.None; // 强行关闭视为不做操作
            }
            Close();
        }
        // 按钮事件处理保持不变
        private void BtnOk_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.OK; Close(); }
        private void BtnYes_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Yes; Close(); }
        private void BtnNo_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.No; Close(); }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Cancel; Close(); }
    }
}
