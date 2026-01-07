using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static TabPaint.MainWindow;

//
//启动项

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void CheckFilePathAvailibility(string path)
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
    }
}