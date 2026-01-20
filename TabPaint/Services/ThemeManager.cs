using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Forms;

namespace TabPaint
{

    public static class ThemeManager
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string RegistryValueName = "AppsUseLightTheme";

        public static AppTheme CurrentAppliedTheme { get; private set; }

        // 监听系统颜色改变
        static ThemeManager()
        {
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        }

        private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General &&
                SettingsManager.Instance.Current.ThemeMode == AppTheme.System)
            {
                ApplyTheme(AppTheme.System);
            }
        }

        public static void ApplyTheme(AppTheme theme)
        {
            // 1. 确定实际要应用的主题 (Light 或 Dark)
            bool isDark = false;
            if (theme == AppTheme.Dark) isDark = true;
            else if (theme == AppTheme.Light) isDark = false;
            else isDark = IsSystemDark(); // System

            // 2. 替换基础 ResourceDictionary (Light/Dark)
            string dictPath = isDark ? "Resources/DarkTheme.xaml" : "Resources/LightTheme.xaml";
            var dictUri = new Uri($"pack://application:,,,/{dictPath}", UriKind.Absolute);
            var mergedDicts = Application.Current.Resources.MergedDictionaries;

            // 移除旧字典
            ResourceDictionary oldDict = null;
            foreach (var dict in mergedDicts)
            {
                if (dict.Source != null &&
                   (dict.Source.OriginalString.Contains("LightTheme.xaml") ||
                    dict.Source.OriginalString.Contains("DarkTheme.xaml")))
                {
                    oldDict = dict;
                    break;
                }
            }

            var newDict = new ResourceDictionary() { Source = dictUri };
            if (oldDict != null)
            {
                int index = mergedDicts.IndexOf(oldDict);
                mergedDicts[index] = newDict;
            }
            else
            {
                mergedDicts.Add(newDict);
            }

            // 3. 记录当前主题状态
            CurrentAppliedTheme = isDark ? AppTheme.Dark : AppTheme.Light;

            // 4. 图标字典重载 (保持原有逻辑)
            var iconsDict = new ResourceDictionary();
            iconsDict.Source = new Uri("pack://application:,,,/Resources/Icons/Icons.xaml");
            if (iconsDict != null && ((MainWindow)Application.Current.MainWindow) != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(iconsDict);
                Application.Current.Resources.MergedDictionaries.Add(iconsDict);
            }

            if (!IsWin11())
            {
                ApplyWin10FallbackBackground(isDark);
            }

            // 5. 刷新标题栏和强调色
            UpdateWindowStyle(isDark);
            RefreshAccentColor(SettingsManager.Instance.Current.ThemeAccentColor);

        }
        private static bool IsWin11()
        {
            return Environment.OSVersion.Version.Build >= 22000;
        }

        // 【新增方法】Win10 专用背景覆盖
        private static void ApplyWin10FallbackBackground(bool isDark)
        {
            var resources = Application.Current.Resources;


            if (isDark)
            {
                var win10DarkBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020"));


                win10DarkBg.Freeze();

                // 覆盖资源
                if (resources.Contains("WindowBackgroundBrush")) resources.Remove("WindowBackgroundBrush");
                resources["WindowBackgroundBrush"] = win10DarkBg;

            }
            else
            {
                // Win10 浅色模式：使用经典的 Windows 灰白
                var win10LightBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F3"));
                win10LightBg.Freeze();

                if (resources.Contains("WindowBackgroundBrush")) resources.Remove("WindowBackgroundBrush");
                resources["WindowBackgroundBrush"] = win10LightBg;
            }
        }
        public static void RefreshAccentColor(string hexColor = null)
        {
            if (string.IsNullOrEmpty(hexColor))
            {
                // 注意：这里仅在确定 SettingsManager 已经初始化完成后调用才安全
                hexColor = SettingsManager.Instance.Current.ThemeAccentColor;
            }
            MainWindow mw = ((MainWindow)Application.Current.MainWindow);
            UpdateAccentResources(hexColor);
        }

        private static void UpdateAccentResources(string hexColor)
        {
            try
            {
                Color baseColor = (Color)ColorConverter.ConvertFromString(hexColor);
                Color hoverColor = ChangeColorBrightness(baseColor, 0.2f); // 变亮 20%
                Color pressedColor = ChangeColorBrightness(baseColor, -0.2f); // 变暗 20%
                Color borderColor = ChangeColorBrightness(baseColor, -0.1f); // 稍微变暗做边框

                // 2. 创建画刷 (Freeze 提高性能)
                var resources = Application.Current.Resources;

                // --- ToolAccent (工具栏图标/选中状态) ---
                SetSolidBrush(resources, "ToolAccentBrush", baseColor);
                SetSolidBrush(resources, "ToolAccentHoverBrush", hoverColor);
                SetSolidBrush(resources, "ToolAccentBorderBrush", borderColor);

                // --- ToolAccentSubtle (透明背景系列) ---
                SetSolidBrush(resources, "ToolAccentSubtleHoverBrush", baseColor, 0.10);
                SetSolidBrush(resources, "ToolAccentSubtlePressedBrush", baseColor, 0.20);
                SetSolidBrush(resources, "ToolAccentSubtleSelectedBrush", baseColor, 0.15);
                SetSolidBrush(resources, "ToolAccentSubtleHoverSelectedBrush", baseColor, 0.25);

                // --- SystemAccent (滑块/复选框/RadioButton) ---
                // 统一让系统控件也使用这个颜色
                SetSolidBrush(resources, "SystemAccentBrush", baseColor);
                SetSolidBrush(resources, "SystemAccentHoverBrush", hoverColor);
                SetSolidBrush(resources, "SystemAccentPressedBrush", pressedColor);
                SetSolidBrush(resources, "SliderTrackFillBrush", baseColor); // 滑块填充

                // 列表选中项边框
                SetSolidBrush(resources, "ListItemSelectedBorderBrush", baseColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting accent color: {ex.Message}");
            }
        }

        private static void SetSolidBrush(ResourceDictionary resources, string key, Color color, double opacity = 1.0)
        {
            var brush = new SolidColorBrush(color) { Opacity = opacity };
            brush.Freeze();

            // 如果资源已存在，移除它以确保覆盖（对于 MergedDictionaries 这种方式比较稳妥）
            if (resources.Contains(key)) resources.Remove(key);
            resources[key] = brush;
        }

        // 辅助方法：调整亮度
        private static Color ChangeColorBrightness(Color color, float correctionFactor)
        {
            float red = (float)color.R;
            float green = (float)color.G;
            float blue = (float)color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }
        private static bool IsSystemDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    object registryValueObject = key?.GetValue(RegistryValueName);
                    if (registryValueObject == null) return false;
                    return (int)registryValueObject == 0;
                }
            }
            catch
            {
                return false; // 读取失败默认浅色
            }
        }

        private static void UpdateWindowStyle(bool isDark)
        {
            foreach (Window window in Application.Current.Windows)
            {
                SetWindowImmersiveDarkMode(window, isDark);
                // 这里可能还需要通知你的 MicaAcrylicManager 刷新
                // MicaAcrylicManager.UpdateTheme(window, isDark); 
                var bgBrush = Application.Current.FindResource("WindowBackgroundBrush") as Brush;
                window.Background = bgBrush;
            }
        }

        // --- Win32 API 设置标题栏深色模式 ---
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static bool SetWindowImmersiveDarkMode(Window window, bool enabled)
        {
            if (window == null) return false;
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return false;

            int attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
            int useImmersiveDarkMode = enabled ? 1 : 0;

            if (DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) != 0)
            {
                // 尝试旧版本的 Attribute
                attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) != 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
