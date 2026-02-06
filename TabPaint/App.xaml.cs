//
//App.xaml.cs
//应用程序入口点，负责初始化设置、异常处理、单实例检测以及主窗口的启动。
//
//
//App.xaml.cs
//应用程序入口点，负责初始化设置、异常处理、单实例检测以及主窗口的启动。
//
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices; // 必须引用，用于置顶窗口
using System.Text;
using System.Windows;
using System.Windows.Threading;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class App : Application
    {
        // 保存 MainWindow 的静态引用，方便回调使用
        private static MainWindow _mainWindow;
        private static readonly string LogDirectory = System.IO.Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "TabPaint", "CrashLogs");
        public static class a
        {
            public static void s(params object[] args)
            {
                // 可以根据需要拼接输出格式
                string message = string.Join(" ", args);
                Debug.WriteLine(message);
            }
        }
        private void SetupExceptionHandling()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                LogException(e.Exception, "UIThread");
                e.Handled = true; // 设置为 true 可以防止程序直接闪退，但建议视情况决定是否继续运行
                ShutdownAppWithErrorMessage(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                LogException(exception, "AppDomain");
                // 这种异常通常无法恢复，记录后程序即将终止
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogException(e.Exception, "TaskScheduler");
                e.SetObserved(); // 标记为已观察，防止程序崩溃
            };
        }
        private static void LogException(Exception ex, string source)
        {
            try
            {
                if (ex == null) return;

                // 确保目录存在
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"Crash_{timestamp}.txt";
                string fullPath = System.IO.Path.Combine(LogDirectory, filename);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Time: {DateTime.Now}");
                sb.AppendLine($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
                sb.AppendLine($"Source: {source}");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Type: {ex.GetType().FullName}");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.StackTrace);

                if (ex.InnerException != null)
                {
                    sb.AppendLine(new string('=', 50));
                    sb.AppendLine("Inner Exception:");
                    sb.AppendLine(ex.InnerException.Message);
                    sb.AppendLine(ex.InnerException.StackTrace);
                }

                File.WriteAllText(fullPath, sb.ToString());
            }
            catch (Exception logEx)
            {
                // 如果写日志都失败了，只能写到调试输出里
                Debug.WriteLine($"Failed to log crash: {logEx.Message}");
            }
        }
        private void ShutdownAppWithErrorMessage(Exception ex)
        {
            string msg = $"TabPaint 遇到错误需要关闭。\n\n错误信息: {ex.Message}\n\n日志已保存至: {LogDirectory}";
            FluentMessageBox.Show(msg, "程序崩溃", MessageBoxButton.OK, MessageBoxImage.Error, null, LogDirectory);

            try
            {
            }
            catch { }

            Environment.Exit(1);
        }
        protected override void OnStartup(StartupEventArgs e)
        {//680ms
       
            // 1. 立即启动配置预加载（并行）
            var settingsTask = Task.Run(() => SettingsManager.Instance);

            // 启用分级 JIT 编译优化
            try
            {
                string profileRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint", "Profiles");
                Directory.CreateDirectory(profileRoot);
                System.Runtime.ProfileOptimization.SetProfileRoot(profileRoot);
                System.Runtime.ProfileOptimization.StartProfile("Startup.profile");
            }//4ms
            catch { }
 
            SetupExceptionHandling();//0.9ms
            //检查单实例
         
            if (!SingleInstance.IsFirstInstance())//0.3ms
            {
                SingleInstance.SendArgsToFirstInstance(e.Args);
                Environment.Exit(0);
                return;
            } 
          
            _ = Task.Run(() =>//创建线程池，10ms
            {
                SingleInstance.ListenForArgs((filePath) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
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
            });
            // 1. 获取启动路径并检查其有效性（一次性检查）

  
            string filePath = "";//<0.1ms
            bool fileExists = false;
            if (e.Args is { Length: > 0 })
            {
                string inputPath = e.Args[0];
                if (System.IO.File.Exists(inputPath) || System.IO.Directory.Exists(inputPath))
                {
                    filePath = inputPath;
                    fileExists = true;
                }
            }
          
            // 等待配置加载完成
            var settingsManager = settingsTask.Result;
            var currentSettings = settingsManager.Current; //15ms
         
            LocalizationManager.ApplyLanguage(currentSettings.Language);//2ms

            currentSettings.PropertyChanged += Settings_PropertyChanged;
           
            AppTheme targetTheme = currentSettings.ThemeMode;//<0.1ms
            if (currentSettings.StartInViewMode && currentSettings.ViewUseDarkCanvasBackground && fileExists)
            {
                targetTheme = AppTheme.Dark;
            }


            // 在创建 MainWindow 之前应用主题，避免 InitializeComponent 时的资源浪费
            ThemeManager.ApplyTheme(targetTheme);  //2ms

            // 3. 创建并启动主窗口
            base.OnStartup(e);//<0.1ms
            _mainWindow = new MainWindow(filePath, fileExists);//240ms
                _mainWindow.Show();//340ms
         

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
