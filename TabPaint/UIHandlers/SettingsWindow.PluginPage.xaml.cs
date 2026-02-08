using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TabPaint;

namespace TabPaint.Pages
{
    public partial class PluginPage : UserControl
    {
        private CancellationTokenSource _cts;

        public PluginPage()
        {
            InitializeComponent();
            this.Loaded += PluginPage_Loaded;
            this.Unloaded += (s, e) => _cts?.Cancel();
        }

        private void PluginPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAllStatuses();
        }

        private void UpdateAllStatuses()
        {
            // 获取 AiService 实例。通常在 App 或 MainWindow 中有单例，或者需要新建。
            // 查阅代码发现 MainWindow 有 AiService 引用，或者可以通过 CacheDir 创建临时实例。
            var aiService = new AiService(AppConsts.CacheDir);

            UpdateStatus(aiService.IsModelReady(AiService.AiTaskType.RemoveBackground), TxtStatusRMBG, BtnInstallRMBG, BtnUninstallRMBG);
            UpdateStatus(aiService.IsModelReady(AiService.AiTaskType.SuperResolution), TxtStatusSR, BtnInstallSR, BtnUninstallSR);
            UpdateStatus(aiService.IsModelReady(AiService.AiTaskType.Inpainting), TxtStatusInpaint, BtnInstallInpaint, BtnUninstallInpaint);
        }

        private void UpdateStatus(bool isReady, TextBlock txt, Button btnInstall, Button btnUninstall)
        {
            if (isReady)
            {
                txt.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Installed");
                btnInstall.Visibility = Visibility.Collapsed;
                btnUninstall.Visibility = Visibility.Visible;
            }
            else
            {
                txt.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                btnInstall.Visibility = Visibility.Visible;
                btnUninstall.Visibility = Visibility.Collapsed;
            }
        }

        private async void InstallRMBG_Click(object sender, RoutedEventArgs e)
        {
            await InstallModel(AiService.AiTaskType.RemoveBackground, TxtStatusRMBG, ProgressRMBG, BtnInstallRMBG);
        }

        private async void InstallSR_Click(object sender, RoutedEventArgs e)
        {
            await InstallModel(AiService.AiTaskType.SuperResolution, TxtStatusSR, ProgressSR, BtnInstallSR);
        }

        private async void InstallInpaint_Click(object sender, RoutedEventArgs e)
        {
            await InstallModel(AiService.AiTaskType.Inpainting, TxtStatusInpaint, ProgressInpaint, BtnInstallInpaint);
        }

        private async Task InstallModel(AiService.AiTaskType type, TextBlock txtStatus, ProgressBar progress, Button btnInstall)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            
            btnInstall.IsEnabled = false;
            progress.Visibility = Visibility.Visible;
            progress.Value = 0;
            txtStatus.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Downloading");

            try
            {
                var aiService = new AiService(AppConsts.CacheDir);
                var progressReporter = new Progress<AiDownloadStatus>(s =>
                {
                    Dispatcher.Invoke(() => progress.Value = s.Percentage);
                });

                await aiService.PrepareModelAsync(type, progressReporter, _cts.Token);

                UpdateAllStatuses();
            }
            catch (OperationCanceledException)
            {
                txtStatus.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                btnInstall.IsEnabled = true;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Error: " + ex.Message;
                btnInstall.IsEnabled = true;
            }
            finally
            {
                progress.Visibility = Visibility.Collapsed;
            }
        }

        private void UninstallRMBG_Click(object sender, RoutedEventArgs e)
        {
            UninstallModel(AiService.AiTaskType.RemoveBackground);
        }

        private void UninstallSR_Click(object sender, RoutedEventArgs e)
        {
            UninstallModel(AiService.AiTaskType.SuperResolution);
        }

        private void UninstallInpaint_Click(object sender, RoutedEventArgs e)
        {
            UninstallModel(AiService.AiTaskType.Inpainting);
        }

        private void UninstallModel(AiService.AiTaskType type)
        {
            var result = FluentMessageBox.Show(
                LocalizationManager.GetString("L_Settings_Plugins_Uninstall_Confirm"),
                LocalizationManager.GetString("L_Settings_Plugins_Uninstall"),
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string modelName = "";
                switch (type)
                {
                    case AiService.AiTaskType.RemoveBackground:
                        modelName = AppConsts.BgRem_ModelName;
                        break;
                    case AiService.AiTaskType.SuperResolution:
                        modelName = AppConsts.Sr_ModelName;
                        break;
                    case AiService.AiTaskType.Inpainting:
                        modelName = AppConsts.Inpaint_ModelName;
                        break;
                }

                string modelPath = Path.Combine(AppConsts.CacheDir, modelName);
                if (File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                }

                UpdateAllStatuses();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show("Uninstall failed: " + ex.Message, "Error", MessageBoxButton.OK);
            }
        }
    }
}
