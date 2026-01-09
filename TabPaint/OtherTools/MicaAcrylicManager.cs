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
        
            else
            {
                DisableEffect(window);

                // 关键：确保 Win10 下允许窗口不透明，这样拖拽才丝滑
                window.Background = Application.Current.FindResource("WindowBackgroundBrush") as Brush;
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
            ((MainWindow)System.Windows.Application.Current.MainWindow).Background = Application.Current.FindResource("ToolAccentSubtleSelectedBrush") as Brush;

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

      
      
        private static bool IsWin11()
        {
            // 粗略判断：Win11 Version >= 22000
            var version = Environment.OSVersion.Version.Build;
            return version >= 22000;
        }
     
    }
}
