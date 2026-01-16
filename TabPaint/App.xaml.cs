using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices; // 必须引用，用于置顶窗口
using System.Windows;
using System.Windows.Threading;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class App : Application
    {
        // 保存 MainWindow 的静态引用，方便回调使用
        private static MainWindow _mainWindow;
        public static class a
        {
            public static void s(params object[] args)
            {
                // 可以根据需要拼接输出格式
                string message = string.Join(" ", args);
                Debug.WriteLine(message);
            }
        }
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

                        if (tab == null)
                        {
                            int indexInList = _mainWindow._imageFiles.IndexOf(filePath);
                            if (indexInList >= 0)
                            {
                                var newTab = new FileTabItem(filePath)
                                {
                                    IsNew = false,
                                    IsDirty = false
                                };

                                _mainWindow.FileTabs.Add(newTab);

                                // 立即切换
                                _mainWindow.SwitchToTab(newTab);
                                _mainWindow.ScrollToTabCenter(newTab);
                            }
                            else
                            {
                                _ = _mainWindow.OpenFilesAsNewTabs(new string[] { filePath });
                            }

                        }
                        else
                        {
                            _mainWindow.SwitchToTab(tab);
                            _mainWindow.ScrollToTabCenter(tab);
                        }
                        _mainWindow.UpdateImageBarSliderState();
                    }
                });
            });
            var currentSettings = SettingsManager.Instance.Current;
            LocalizationManager.ApplyLanguage(currentSettings.Language);
            currentSettings.PropertyChanged += Settings_PropertyChanged;
            AppTheme targetTheme = currentSettings.ThemeMode;
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
                  filePath = @"E:\dev\misc\0000.png"; //10图片
                //         filePath = @"E:\dev\res\"; // 150+图片
                //    filePath = @"E:\dev\res\camera\"; // 1000+4k照片
                // filePath = @"E:\dev\res\pic\"; // 7000+图片文件夹

#endif
            }
            base.OnStartup(e); _mainWindow = new MainWindow(filePath);
            _mainWindow.CheckFilePathAvailibility(filePath);
            // 如果设置了“启动进入看图模式” 且 “看图模式使用深色背景”
            // 则强制在启动时应用深色主题，无论全局设置是什么
            //bool isDarkForWindow = (ThemeManager.CurrentAppliedTheme == AppTheme.Dark) || (currentSettings.StartInViewMode && currentSettings.ViewUseDarkCanvasBackground && _mainWindow._currentFileExists);
            if (currentSettings.StartInViewMode && currentSettings.ViewUseDarkCanvasBackground && _mainWindow._currentFileExists)
            {
                targetTheme = AppTheme.Dark;
            }
            ThemeManager.ApplyTheme(targetTheme);





            _mainWindow.Show();
        }
        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var settings = (AppSettings)sender;

            if (e.PropertyName == nameof(AppSettings.ThemeMode))
            {
                ThemeManager.ApplyTheme(settings.ThemeMode);
                _mainWindow.SetUndoRedoButtonState();
                _mainWindow.AutoUpdateMaximizeIcon();
            }
            else if (e.PropertyName == nameof(AppSettings.ThemeAccentColor))
            {
                ThemeManager.RefreshAccentColor(settings.ThemeAccentColor);
            }

            // 语言变更：更新资源字典（已抽到 DynamicResource 的文本会即时更新）
            if (e.PropertyName == nameof(AppSettings.Language))
            {
                LocalizationManager.ApplyLanguage(settings.Language);
            }

            if (e.PropertyName == nameof(AppSettings.PenThickness))
            {
                if (_mainWindow._ctx != null)
                {
                    _mainWindow._ctx.PenThickness = SettingsManager.Instance.Current.PenThickness;
                }
            }
        }

    }
}
