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

            string filePath = "";

            // 判断是否通过命令行传入文件路径
            if (e.Args is { Length: > 0 } && File.Exists(e.Args[0]))
            {
                filePath = e.Args[0];
            }
            else
            {
#if DEBUG
                // 在这里取消注释你想要测试的路径，Release模式下这段代码会被自动忽略
             //    filePath = @"E:\dev\0000.png"; //10图片
                filePath = @"E:\dev\res\0000.png"; // 150+图片
                //filePatg = @"E:\dev\res\camera\IMG_20220916_213017.jpg"; // 1000+4k照片
                // filePath = @"E:\dev\res\pic\00A21CF65912690AD4AFA8C2E86D9FEC.jpg"; // 7000+图片文件夹
#endif
            }
            TimeRecorder t = new TimeRecorder(); t.Reset(); t.Toggle();
            var window = new MainWindow(filePath);
            
           
            window.Show(); t.Toggle();
        }

    }

}
