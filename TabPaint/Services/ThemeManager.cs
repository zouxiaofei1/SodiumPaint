using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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
            if (theme == AppTheme.Dark)
            {
                isDark = true;
            }
            else if (theme == AppTheme.Light)
            {
                isDark = false;
            }
            else // System
            {
                isDark = IsSystemDark();
            }

            // 2. 替换 ResourceDictionary
            string dictPath = isDark ? "Resources/DarkTheme.xaml" : "Resources/LightTheme.xaml";
            var dictUri = new Uri($"pack://application:,,,/{dictPath}", UriKind.Absolute);

            // 获取当前的资源字典集合
            var mergedDicts = Application.Current.Resources.MergedDictionaries;

            // 找到旧的主题字典并移除 (假设通过 Source 判断，或者它是第一个)
            // 这里使用一个简单的逻辑：移除任何包含 LightTheme 或 DarkTheme 的字典
            // 也可以给你的 ResourceDictionary 加个 Key 来识别
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

            // 创建新字典
            var newDict = new ResourceDictionary() { Source = dictUri };

            // 替换逻辑 (为了防闪烁，先加后减，或者直接替换)
            if (oldDict != null)
            {
                int index = mergedDicts.IndexOf(oldDict);
                mergedDicts[index] = newDict; // 直接替换保持顺序
            }
            else
            {
                mergedDicts.Add(newDict);
            }

            CurrentAppliedTheme = isDark ? AppTheme.Dark : AppTheme.Light;
            var iconsDict = new ResourceDictionary();
            // 注意这里要用 pack URI 格式
            iconsDict.Source = new Uri("pack://application:,,,/Resources/Icons/Icons.xaml");

            if (iconsDict != null&& ((MainWindow)System.Windows.Application.Current.MainWindow)!=null)
            {
                // 移除再添加，强制让所有引用了这些图标的 DynamicResource 重新求值
                Application.Current.Resources.MergedDictionaries.Remove(iconsDict);
                Application.Current.Resources.MergedDictionaries.Add(iconsDict);
            }
            // 3. 刷新 Mica 和 标题栏颜色
            UpdateWindowStyle(isDark);
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
