using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

//
//复制管理mica和亚克力特效的js
//

namespace TabPaint
{
 
    public static class MicaAcrylicManager
    {
        
        static void s<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ===== DWM API（Win11 Mica） =====
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref int pvAttribute, int cbAttribute);
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;

            public MARGINS(int allMargins)
            {
                cxLeftWidth = allMargins;
                cxRightWidth = allMargins;
                cyTopHeight = allMargins;
                cyBottomHeight = allMargins;
            }
        }
        private enum DWMWINDOWATTRIBUTE : int
        {
            DWMWA_SYSTEMBACKDROP_TYPE2 = 33,
            DWMWA_SYSTEMBACKDROP_TYPE = 38
        }

        private enum DWMSBT : int
        {
            DWMSBT_AUTO = 0,
            DWMSBT_NONE = 1,
            DWMSBT_MAINWINDOW = 2, // Mica
            DWMSBT_TRANSIENTWINDOW = 3,
            DWMSBT_TABBEDWINDOW = 4 // Acrylic
        }

        // ===== Win10 Acrylic API =====
        [StructLayout(LayoutKind.Sequential)]
        private struct ACCENT_POLICY
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWCOMPOSITIONATTRIBUTE_DATA
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4 // Windows 10 Acrylic
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBUTE_DATA data);

        /// <summary>
        /// 自动检测系统并启用 Mica（Win11）或 Acrylic（Win10）。
        /// </summary>
        public static void ApplyEffect(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            if (IsWin11())
            {

                EnableMica(hwnd);
            }
            else if (IsWin10OrLater())
            {
                EnableAcrylic(hwnd);
            }
            else
            {
                // 其他平台使用普通背景
            }
        }
        public static void DisableEffect(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            if (IsWin11())
            {

                DisableMica(hwnd);
            }
          
        }
        public static void DisableMica(IntPtr hwnd)
        {
            ((MainWindow)System.Windows.Application.Current.MainWindow).Background = new SolidColorBrush(Color.FromArgb(255, 243, 243, 243));

            int backdropType = (int)DWMSBT.DWMSBT_NONE;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        }
        private static void EnableMica(IntPtr hwnd)/// 启用 Win11 Mica 效果
        {
            ((MainWindow)System.Windows.Application.Current.MainWindow).Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
            int cornerPref = 2; // 2 = rounded
            DwmSetWindowAttribute(hwnd, (DWMWINDOWATTRIBUTE)33, ref cornerPref, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE

            int backdropType = (int)DWMSBT.DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        }
        [DllImport("dwmapi.dll")]
        private static extern int DwmIsCompositionEnabled(out bool enabled);

        private static void EnableAcrylic(IntPtr hwnd)
        {
            // 1. 确保 DWM 合成已开启（Win10 默认开启，但在某些精简版或远程桌面可能关闭）
            DwmIsCompositionEnabled(out bool compositionEnabled);
            if (!compositionEnabled) return;

            // 2. 关键：在 Win10 上，为了避免卡顿，必须确保 WPF 认为窗口是不透明的，
            // 但 DWM 认为客户区是玻璃。
            // 我们不需要修改 AllowsTransparency，但需要修改背景色。

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var win = (MainWindow)System.Windows.Application.Current.MainWindow;
                // 设置为纯透明，让 DWM 接管背景绘制
                win.Background = Brushes.Transparent;
                if (win.ResizeMode == ResizeMode.NoResize)
                    win.ResizeMode = ResizeMode.CanResize;
            });

            // 3. 启用亚克力 (使用原有 API，但参数微调)
            var accent = new ACCENT_POLICY
            {
                AccentState = (int)AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT,
                AccentFlags = 2, 
                GradientColor = 0x99F3F3F3
            };

            int accentSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WINDOWCOMPOSITIONATTRIBUTE_DATA
            {
                Attribute = 19, // WCA_ACCENT_POLICY
                SizeOfData = accentSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);

            // 4. 【核心修复】 DwmExtendFrameIntoClientArea
            // 在 Win10 上，必须配合 WindowChrome 使用才能完美。
            // 确保你的 XAML 中有 <WindowChrome.WindowChrome> 标签
            var margins = new MARGINS(-1);
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
      
        private static bool IsWin11()
        {
            // 粗略判断：Win11 Version >= 22000
            var version = Environment.OSVersion.Version.Build;
            return version >= 22000;
        }
        private static bool IsWin10OrLater()
        {
            var version = Environment.OSVersion.Version.Major;
            return version >= 10;
        }
    }
}
