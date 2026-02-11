//
//EventHandler.Menu.cs
//fileedit两菜单
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using TabPaint.Controls;
using TabPaint.UIHandlers;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnHelpClick(object sender, RoutedEventArgs e)
        {
            var helpPages = new List<HelpPage>();
            try
            {
                helpPages.Add(new HelpPage
                {
                    ImageUri = new Uri("pack://application:,,,/Resources/help-1.gif"),
                    DescriptionKey = "L_Help_Desc_1"
                });

                helpPages.Add(new HelpPage
                {
                    ImageUri = new Uri("pack://application:,,,/Resources/help-2.gif"),
                    DescriptionKey = "L_Help_Desc_2"
                });

                helpPages.Add(new HelpPage
                {
                    ImageUri = new Uri("https://media.giphy.com/media/v1.Y2lkPTc5MGI3NjExcDNoM3Z.../cat-typing.gif"),
                    DescriptionKey = "L_Help_Desc_3"
                });

                if (helpPages.Count > 0)
                {
                    var helpWin = new HelpWindow(helpPages);
                    helpWin.Owner = this;
                    helpWin.Show();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void OnAppTitleBarLogoMiddleClick(object sender, RoutedEventArgs e)
        {
            if (_currentTabItem != null)  CloseTab(_currentTabItem);
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)=> MaximizeWindowHandler();

        private void OnExitClick(object sender, RoutedEventArgs e)  =>   App.GlobalExit();

        private void Minimize_Click(object sender, RoutedEventArgs e) =>   WindowState = WindowState.Minimized;

    }
}