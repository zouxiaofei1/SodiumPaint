using System.Runtime.InteropServices; // 必须引用，用于置顶窗口
using System.Windows;
using System.Windows.Threading;

namespace TabPaint
{
    public partial class App : Application
    {
        // 保存 MainWindow 的静态引用，方便回调使用
        private static MainWindow _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. 检查单实例
            if (!SingleInstance.IsFirstInstance())
            {
                SingleInstance.SendArgsToFirstInstance(e.Args);
                Environment.Exit(0);
                return;
            }
            SingleInstance.ListenForArgs((filePath) =>
            {
                // 注意：管道是在后台线程，操作 UI 必须回到主线程 (Dispatcher)
                Current.Dispatcher.Invoke(() =>
                {
                    if (_mainWindow != null)
                    {
                        RestoreWindow(_mainWindow);
                        var tab = _mainWindow.FileTabs.FirstOrDefault(t => t.FilePath == filePath);
                        if (tab == null) _ = _mainWindow.OpenFilesAsNewTabs(new string[] { filePath }); 
                        else
                             _mainWindow.SwitchToTab(tab);
                        _mainWindow.UpdateImageBarSliderState();
                    }
                });
            });

            base.OnStartup(e);

            // --- 原有的启动逻辑 ---
            string filePath = "";
            if (e.Args is { Length: > 0 })
            {
                string inputPath = e.Args[0];
                if (System.IO.File.Exists(inputPath) || System.IO.Directory.Exists(inputPath))
                {
                    filePath = inputPath;
                }
            }
            else
            {
#if DEBUG
                filePath = @"E:\dev\"; //10图片
                                       //filePath = @"E:\dev\res\"; // 150+图片
                                       //filePatg = @"E:\dev\res\camera\"; // 1000+4k照片
                                       // filePath = @"E:\dev\res\pic\"; // 7000+图片文件夹
#endif
            }

            _mainWindow = new MainWindow(filePath);
            _mainWindow.Show();
        }

        private void RestoreWindow(Window window)
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
