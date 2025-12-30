using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using static TabPaint.MainWindow;
namespace TabPaint
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        static void s<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            
            base.OnStartup(e);
           
            string filePath;

            // 判断是否通过命令行传入文件路径
            if (e.Args is { Length: > 0 } && File.Exists(e.Args[0]))
            {
                filePath = e.Args[0];
            }
            else
            {
                // Visual Studio 调试默认打开


            }
TimeRecorder t = new TimeRecorder(); t.Reset(); t.Toggle();
            var window = new MainWindow();
            
           
            window.Show(); t.Toggle();
        }

    }

}
