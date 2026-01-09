using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint.Controls;
using static TabPaint.MainWindow;

//
//启动项

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public void CheckFilePathAvailibility(string path)
        {
            if (string.IsNullOrEmpty(path)) _currentFileExists = false;
            if((File.Exists(path)|| System.IO.Directory.Exists(path))&&(!IsVirtualPath(path)))
            {
                _currentFileExists = true;
            }
            else
            {
                _currentFileExists = false;
            }
        }
        static public void RestoreWindow(System.Windows.Window window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.Activate();
            window.Topmost = true;  // 临时置顶
            window.Topmost = false; // 取消置顶
            window.Focus();

            // 如果需要更激进的置顶，可以使用 SetForegroundWindow API
            SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(window).Handle);
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}