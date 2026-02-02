using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

//设置窗口


namespace TabPaint
{
    public partial class SettingsWindow : Window
    {
        public string ProgramVersion { get; set; } = "v0.9.3";
        private bool _isNavExpanded = true;
        private const double NAV_EXPANDED_WIDTH = 220;
        private const double NAV_COLLAPSED_WIDTH = 48;
        private DispatcherTimer _toastTimer;
        private bool _isToastVisible = false;
        private bool MicaEnabled = false;

        public SettingsWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            this.Activated += SettingsWindow_Activated;
            // 订阅设置变更事件以触发 Toast
            if (SettingsManager.Instance.Current != null)
            {
                SettingsManager.Instance.Current.PropertyChanged += Settings_PropertyChanged;
            }

            this.Loaded += (s, e) => SetHighResIcon();
            this.Unloaded += (s, e) =>
            {
                if (SettingsManager.Instance.Current != null)
                {
                    SettingsManager.Instance.Current.PropertyChanged -= Settings_PropertyChanged;
                }
            };

            this.SizeChanged += SettingsWindow_SizeChanged;
            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromSeconds(1); // Toast 显示时长
            _toastTimer.Tick += (s, args) => HideToast();
        }
    
        private void SettingsWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 阈值设为 750 (可根据实际 UI 调整)
            // 如果窗口变小且当前是展开状态 -> 自动收起
            if (e.NewSize.Width < 750 && _isNavExpanded)
            {
                SetSidebarState(false);
            }
            // 如果窗口变大且当前是收起状态 -> 自动展开 (可选，如果只希望单向自动收起，可注释掉下面这行)
            else if (e.NewSize.Width >= 750 && !_isNavExpanded)
            {
                SetSidebarState(true);
            }
        }
        private void SetSidebarState(bool expand)
        {


            if (_isNavExpanded == expand) return;
            _isNavExpanded = expand;

            double targetWidth = expand ? NAV_EXPANDED_WIDTH : NAV_COLLAPSED_WIDTH;

            // 使用原生的 DoubleAnimation，非常稳定
            DoubleAnimation anim = new DoubleAnimation();
            // 使用 ActualWidth 保证动画从当前宽度平滑过渡
            anim.From = SidebarBorder.ActualWidth;
            anim.To = targetWidth;
            anim.Duration = TimeSpan.FromMilliseconds(200);
            anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 对 Border 的 Width 属性进行动画，而不是 ColumnDefinition
            SidebarBorder.BeginAnimation(Border.WidthProperty, anim);

            // 控制文字显示逻辑
            Visibility textVis = expand ? Visibility.Visible : Visibility.Collapsed;
            // 2. 隐藏/显示所有文字

            if (TxtGeneral != null) TxtGeneral.Visibility = textVis;
            if (TxtPaint != null) TxtPaint.Visibility = textVis;
            if (TxtView != null) TxtView.Visibility = textVis;
            if (TxtShortcuts != null) TxtShortcuts.Visibility = textVis;
            if (TxtAdvanced != null) TxtAdvanced.Visibility = textVis;
            if (TxtAbout != null) TxtAbout.Visibility = textVis;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 确保句柄已经准备好
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            // 设置暗色模式标题栏
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);

            MicaAcrylicManager.ApplyEffect(this);
            if (!MicaAcrylicManager.IsWin11())
            {
                var chromeLow = FindResource("ChromeLowBrush") as Brush;
                SidebarBorder.Background = chromeLow;
                // 同时设置主窗口背景
                this.Background = FindResource("WindowBackgroundBrush") as Brush;
            }
        }
        private void SettingsWindow_Activated(object sender, EventArgs e)
        {
            // 为了性能和避免闪烁，可以加个判断，如果已经是 Mica 则不重复设置
            if (!MicaEnabled)
            {
                MicaAcrylicManager.ApplyEffect(this);
                MicaEnabled = true;
            }
        }
        private void SetHighResIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/Resources/TabPaint.ico");
                var decoder = BitmapDecoder.Create(iconUri, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                var bestFrame = decoder.Frames.OrderByDescending(f => f.Width).FirstOrDefault();
                if (bestFrame != null)
                {
                    // 这里假设 About 页面里的图标还在
                    if (AppIcon != null) AppIcon.Source = bestFrame;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Icon load failed: " + ex.Message);
            }
        }

        #region Navigation & Window Logic

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void HamburgerBtn_Click(object sender, RoutedEventArgs e)
        {
            // 切换状态
            SetSidebarState(!_isNavExpanded);
        }

        private bool _isInternalChange = false;
        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GeneralPanel == null) return; // 尚未加载

            if (_isInternalChange) return;
            System.Windows.Controls.ListBox source = sender as System.Windows.Controls.ListBox;
            if (source.SelectedIndex == -1) return;

            _isInternalChange = true;
            if (source == NavListBox) BottomListBox.SelectedIndex = -1;
            else NavListBox.SelectedIndex = -1;

            // 隐藏所有面板
            GeneralPanel.Visibility = Visibility.Collapsed;
            PaintPanel.Visibility = Visibility.Collapsed;
            ViewPanel.Visibility = Visibility.Collapsed;
            ShortcutPanel.Visibility = Visibility.Collapsed;
            AdvancedPanel.Visibility = Visibility.Collapsed;
            AboutPanel.Visibility = Visibility.Collapsed;

            // 显示选中面板
            if (source == NavListBox && NavListBox.SelectedItem is ListBoxItem item)
            {
                string tag = item.Tag?.ToString();
                switch (tag)
                {
                    case "General": GeneralPanel.Visibility = Visibility.Visible; break;
                    case "Paint": PaintPanel.Visibility = Visibility.Visible; break;
                    case "View": ViewPanel.Visibility = Visibility.Visible; break;
                    case "Shortcuts": ShortcutPanel.Visibility = Visibility.Visible; break;
                    case "Advanced": AdvancedPanel.Visibility = Visibility.Visible; break;
                }
            }
            else if (source == BottomListBox)
            {
                AboutPanel.Visibility = Visibility.Visible;
            }
            _isInternalChange = false;
        }

        #endregion

        #region Toast Logic

        private DateTime _lastToastTime = DateTime.MinValue;
        private bool _isToasting = false;

        private async void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 忽略一些不需要提示的变更，或者可以全提示
            if (e.PropertyName == "Shortcuts") return; // 快捷键可能是对象引用变化，不提示

            // 防抖，避免连续触发
            if ((DateTime.Now - _lastToastTime).TotalMilliseconds < 500 && _isToasting) return;

            _lastToastTime = DateTime.Now;
            _isToasting = true;

            ShowToast();

            // 等待1秒
            await Task.Delay(1000);

            // 如果距离上次触发超过1秒（说明没有新的触发），则隐藏
            if ((DateTime.Now - _lastToastTime).TotalMilliseconds >= 1000)
            {
                HideToast();
                _isToasting = false;
            }
        }

        private void ShowToast()
        {
            // 如果已经在显示，只需重置计时器，保持显示状态
            if (_isToastVisible)
            {
                _toastTimer.Stop();
                _toastTimer.Start();
                return;
            }

            _isToastVisible = true;

            // 进场动画 (从右侧滑入 或者 原地淡入)
            // 这里使用 Margin 动画模拟从右侧划入，初始 Margin 设为 Right: -20

            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(250)) { EasingFunction = new CubicEase() };
            SavedToast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // 修改 Margin 让它从稍微靠右的位置移入
            // 假设目标位置是 Margin="0,30,30,0" (Top=30, Right=30)
            var moveIn = new ThicknessAnimation(
                new Thickness(0, 30, 10, 0), // 起始位置 (稍微偏右)
                new Thickness(0, 30, 30, 0), // 结束位置 (正常位置)
                TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SavedToast.BeginAnimation(FrameworkElement.MarginProperty, moveIn);

            // 启动计时器，2秒后自动隐藏
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            _toastTimer.Stop();
            _isToastVisible = false;

            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            SavedToast.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            // 向上浮动消失，或者向右滑出
            var moveOut = new ThicknessAnimation(
                new Thickness(0, 30, 30, 0),
                new Thickness(0, 10, 30, 0), // 向上飘一点
                TimeSpan.FromMilliseconds(300));
            SavedToast.BeginAnimation(FrameworkElement.MarginProperty, moveOut);
        }


        #endregion

        #region Helper Methods (Keep existing logic)

        private void FactoryReset_Click(object sender, RoutedEventArgs e)
        {
            var result = FluentMessageBox.Show(
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset_Confirm"),
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset"),
              MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string tempBatPath = Path.Combine(Path.GetTempPath(), "tabpaint_reset.bat");

                string batContent = $@"
                        @echo off
                        timeout /t 1 /nobreak > NUL
                        rd /s /q ""{appDataPath}""
                        start """" ""{currentExe}""
                        del ""%~f0""
                        ";
                File.WriteAllText(tempBatPath, batContent);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = tempBatPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show(
                   string.Format(LocalizationManager.GetString("L_Msg_ResetFailed"), ex.Message),
                   LocalizationManager.GetString("L_Common_Error"),
                   MessageBoxButton.OK);
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (double.TryParse(textBox.Text, out double value))
                {
                    if (value < 0) value = 0;
                    if (value > 5000) value = 5000;
                    textBox.Text = value.ToString("0");
                    var binding = textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                    binding?.UpdateSource();
                }
                else
                {
                    textBox.Text = "0";
                }
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ResetShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var result = FluentMessageBox.Show(
              LocalizationManager.GetString("L_Settings_Shortcuts_Reset_Confirm"),
              LocalizationManager.GetString("L_Settings_Shortcuts_Reset_Title"),
              MessageBoxButton.YesNo
            );

            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.Instance.Current.ResetShortcutsToDefault();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                string url = e.Uri.AbsoluteUri;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show($"{LocalizationManager.GetString("L_Toast_OpenUrlFailed")}: {ex.Message}",
                            LocalizationManager.GetString("L_Common_Error"),
                            MessageBoxButton.OK);
            }
        }

        #endregion
    }

    // 用于 GridLength 动画的辅助类
    public class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);
        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public GridLength From { get; set; }
        public GridLength To { get; set; }
        public IEasingFunction EasingFunction { get; set; }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            double fromVal = From.Value;
            double toVal = To.Value;

            if (fromVal > toVal)
                return new GridLength((1 - animationClock.CurrentProgress.Value) * (fromVal - toVal) + toVal, GridUnitType.Pixel);
            else
                return new GridLength(animationClock.CurrentProgress.Value * (toVal - fromVal) + fromVal, GridUnitType.Pixel);
        }
    }
}
