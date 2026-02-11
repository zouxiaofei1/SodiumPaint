//
//EventHandler.Menu.cs
//顶部菜单栏的详细逻辑实现，包括文件保存、另存为、帮助窗口弹出、效果滤镜执行以及批量处理功能。
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

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnClearRecentFilesClick(object sender, EventArgs e)
        {
            SettingsManager.Instance.ClearRecentFiles();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var existingSettings = OwnedWindows.OfType<SettingsWindow>().FirstOrDefault();
            if (existingSettings != null)
            {
                existingSettings.Activate();
                return;
            }

            var settingsWindow = new SettingsWindow();
            SettingsManager.Instance.Current.PropertyChanged += (s, ev) =>
            {
                if (ev.PropertyName == "ViewInterpolationThreshold" ||
                    ev.PropertyName == "PaintInterpolationThreshold")
                {
                    this.Dispatcher.Invoke(() => { RefreshBitmapScalingMode(); });
                }
            };

            settingsWindow.ProgramVersion = this.ProgramVersion;
            settingsWindow.Owner = this;
            settingsWindow.Show();
        }
        private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();
        private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (!_programClosed) OnClosing();
        }

        private void CropMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool is SelectTool selectTool)
            {
                selectTool.CropToSelection(_ctx);
                SetCropButtonState();
                NotifyCanvasChanged();
                _canvasResizer.UpdateUI();
            }
        }
        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            CreateNewTab(TabInsertPosition.AfterCurrent, true);
        }

        private void OnRecycleBinClick(object sender, RoutedEventArgs e)
        {
        }
    }
}
