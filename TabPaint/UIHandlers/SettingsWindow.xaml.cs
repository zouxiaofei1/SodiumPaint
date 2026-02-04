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
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

//设置窗口


namespace TabPaint
{
    public partial class SettingsWindow : Window
    {
        public string ProgramVersion { get; set; } = "";
        private bool _isNavExpanded = true;
        private const double NAV_EXPANDED_WIDTH = 220;
        private const double NAV_COLLAPSED_WIDTH = 48;
        private DispatcherTimer _toastTimer;
        private bool _isToastVisible = false;
        private bool MicaEnabled = false;
        private DispatcherTimer _updateToastTimer;
        private string _latestVersionUrl = ""; // 用于存储点击跳转的地址
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

            this.Loaded += (s, e) =>
            {
                SetHighResIcon();
                CheckUpdateOnLoad(); // <--- 调用自动检查
            };

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

            _updateToastTimer = new DispatcherTimer();
            _updateToastTimer.Interval = TimeSpan.FromSeconds(5);
            _updateToastTimer.Tick += (s, args) => HideUpdateToast();
        }

        private void ShowToast()
        {
            // 重置计时器，如果已经在显示，则延长显示时间
            _toastTimer.Stop();

            // 如果已经在显示，不需要重新跑动画，只需要重置时间
            if (SavedToast.Visibility != Visibility.Visible)
            {
                AnimateShow(SavedToast);
                _isToastVisible = true;
            }

            _toastTimer.Start();
        }

        private void HideToast()
        {
            _toastTimer.Stop();
            _isToastVisible = false;
            AnimateHide(SavedToast);
        }
        private void ShowUpdateToast(string versionTag, string url)
        {
            _latestVersionUrl = url;

            string title = LocalizationManager.GetString("L_Update_Found_Title") ?? "New Update Available";
            TxtUpdateVer.Text = $"{versionTag} ready to download";

            // 启用交互
            UpdateToast.IsHitTestVisible = true;

            _updateToastTimer.Stop();

            if (UpdateToast.Visibility != Visibility.Visible)
            {
                AnimateShow(UpdateToast);
            }

            _updateToastTimer.Start();
        }

        private void HideUpdateToast()
        {
            _updateToastTimer.Stop();
            UpdateToast.IsHitTestVisible = false;
            AnimateHide(UpdateToast);
        }

        // 点击 Toast 的事件处理
        private void UpdateToast_Click(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(_latestVersionUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_latestVersionUrl) { UseShellExecute = true });
                }
                catch { }
            }
            // 调用新的隐藏方法
            HideUpdateToast();
        }




        private static bool _hasCheckedUpdateThisSession = false;

        private async void CheckUpdateOnLoad()
        {
            // 如果希望每次打开设置窗口都检查，请去掉下面这行判断
            if (_hasCheckedUpdateThisSession) return;

            await CheckForUpdatesAsync(isManual: false);
            _hasCheckedUpdateThisSession = true;
        }
        private void OpenCacheFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 定位到 %LOCALAPPDATA%\TabPaint
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string cachePath = Path.Combine(localAppData, "TabPaint");

                // 如果目录不存在，先创建它（防止打开报错）
                if (!Directory.Exists(cachePath))
                {
                    Directory.CreateDirectory(cachePath);
                }

                // 使用资源管理器打开
                Process.Start(new ProcessStartInfo
                {
                    FileName = cachePath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)  {}
        }
        private async Task CheckForUpdatesAsync(bool isManual)
        {
            try
            {
                string owner = "zouxiaofei1";
                string repo = "TabPaint";
                string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                string releaseUrl = $"https://github.com/{owner}/{repo}/releases/latest"; // 默认跳转地址

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("TabPaint-Client");
                    client.Timeout = TimeSpan.FromSeconds(5);

                    string jsonResponse = await client.GetStringAsync(apiUrl);
                    var match = Regex.Match(jsonResponse, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");

                    if (match.Success)
                    {
                        string latestVersionTag = match.Groups[1].Value;

                        if (IsNewerVersion(ProgramVersion, latestVersionTag))
                        {
                            // === 修改点：发现新版本，调用 Toast ===
                            ShowUpdateToast(latestVersionTag, releaseUrl);
                        }
                        else if (isManual)
                        {
                            // 手动检查且是最新版，依然可以用 MessageBox 提示，或者也做一个简单的 Toast
                            FluentMessageBox.Show(
                                LocalizationManager.GetString("L_Update_Latest_Desc") ?? "You are using the latest version.",
                                LocalizationManager.GetString("L_Update_Latest_Title") ?? "Up to date",
                                MessageBoxButton.OK);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (isManual)
                {
                    FluentMessageBox.Show($"Check update failed: {ex.Message}", "Error", MessageBoxButton.OK);
                }
                Debug.WriteLine("Update check failed: " + ex.Message);
            }
        }
        private void AnimateShow(UIElement element)
        {
            if (element.Visibility == Visibility.Visible) return; // 已经在显示了

            element.Visibility = Visibility.Visible;
            element.Opacity = 0;

            // 1. 透明度淡入
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            if (element is FrameworkElement fe && fe.RenderTransform is TranslateTransform trans)
            {
                trans.X = 50; // 初始位置在右侧
                var slideIn = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                trans.BeginAnimation(TranslateTransform.XProperty, slideIn);
            }
        }

        // 通用隐藏动画
        private void AnimateHide(UIElement element)
        {
            if (element.Visibility == Visibility.Collapsed) return;

            // 1. 透明度淡出
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
            element.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            // 2. 位移滑出 (向右滑出)
            if (element is FrameworkElement fe && fe.RenderTransform is TranslateTransform trans)
            {
                var slideOut = new DoubleAnimation(50, TimeSpan.FromMilliseconds(200));

                // 关键：动画结束后设置为 Collapsed，释放空间
                slideOut.Completed += (s, e) =>
                {
                    element.Visibility = Visibility.Collapsed;
                };

                trans.BeginAnimation(TranslateTransform.XProperty, slideOut);
            }
            else
            {
                // 如果没有 Transform，直接隐藏
                element.Visibility = Visibility.Collapsed;
            }
        }
        
       
        private bool IsNewerVersion(string currentRaw, string latestRaw)
        {
            try
            {
                // 去掉 'v' 前缀并修剪空格
                var current = Version.Parse(currentRaw.TrimStart('v', 'V').Trim());
                var latest = Version.Parse(latestRaw.TrimStart('v', 'V').Trim());

                return latest > current;
            }
            catch
            {
                // 解析失败（比如版本号格式不对），默认不提示更新
                return false;
            }
        }
        private void SettingsWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
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
            if (hwnd == IntPtr.Zero)return;
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

        private void OnOpenUrlClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string url = btn.Tag.ToString();
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    FluentMessageBox.Show($"{LocalizationManager.GetString("L_Toast_OpenUrlFailed")}: {ex.Message}",
                                LocalizationManager.GetString("L_Common_Error"),
                                MessageBoxButton.OK);
                }
            }
        }
    }
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
