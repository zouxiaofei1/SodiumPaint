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
        private const string PathInfo = AppConsts.PathInfo;
        private const string PathQuestion = AppConsts.PathQuestion;
        private const string PathWarning = AppConsts.PathWarning;
        private const string PathError = AppConsts.PathError;

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
