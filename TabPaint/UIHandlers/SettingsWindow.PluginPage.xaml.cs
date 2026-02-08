using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TabPaint.Controls;

namespace TabPaint.Pages
{
    public partial class PluginPage : UserControl
    {
        // 每个模型独立的 CancellationTokenSource
        private CancellationTokenSource _ctsRMBG;
        private CancellationTokenSource _ctsSR;
        private CancellationTokenSource _ctsInpaint;

        public PluginPage()
        {
            InitializeComponent();
            this.Loaded += PluginPage_Loaded;
            this.Unloaded += PluginPage_Unloaded;

            // 订阅取消事件
            FloatRMBG.CancelRequested += (s, e) => _ctsRMBG?.Cancel();
            FloatSR.CancelRequested += (s, e) => _ctsSR?.Cancel();
            FloatInpaint.CancelRequested += (s, e) => _ctsInpaint?.Cancel();
        }

        private void PluginPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _ctsRMBG?.Cancel();
            _ctsSR?.Cancel();
            _ctsInpaint?.Cancel();
        }

        private void PluginPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAllStatuses();
        }

        private void UpdateAllStatuses()
        {
            var aiService = new AiService(AppConsts.CacheDir);

            UpdateStatus(aiService.IsModelReady(AiService.AiTaskType.RemoveBackground),
                         TxtStatusRMBG, BtnInstallRMBG, BtnUninstallRMBG);
            UpdateStatus(aiService.IsModelReady(AiService.AiTaskType.SuperResolution),
                         TxtStatusSR, BtnInstallSR, BtnUninstallSR);
            UpdateStatus(aiService.IsModelReady(AiService.AiTaskType.Inpainting),
                         TxtStatusInpaint, BtnInstallInpaint, BtnUninstallInpaint);
        }

        private void UpdateStatus(bool isReady, TextBlock txt, Button btnInstall, Button btnUninstall)
        {
            if (isReady)
            {
                txt.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Installed");
                txt.Foreground = (System.Windows.Media.Brush)FindResource("SystemAccentBrush");
                btnInstall.Visibility = Visibility.Collapsed;
                btnUninstall.Visibility = Visibility.Visible;
            }
            else
            {
                txt.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                txt.Foreground = (System.Windows.Media.Brush)FindResource("TextTertiaryBrush");
                btnInstall.Visibility = Visibility.Visible;
                btnUninstall.Visibility = Visibility.Collapsed;
            }
        }

        // ─── Install Click Handlers ───

        private async void InstallRMBG_Click(object sender, RoutedEventArgs e)
        {
            _ctsRMBG?.Cancel();
            _ctsRMBG = new CancellationTokenSource();
            await InstallModel(AiService.AiTaskType.RemoveBackground,
                               TxtStatusRMBG, BtnInstallRMBG, FloatRMBG,
                               LocalizationManager.GetString("L_Settings_Plugins_Model_RMBG_Title"),
                               _ctsRMBG);
        }

        private async void InstallSR_Click(object sender, RoutedEventArgs e)
        {
            _ctsSR?.Cancel();
            _ctsSR = new CancellationTokenSource();
            await InstallModel(AiService.AiTaskType.SuperResolution,
                               TxtStatusSR, BtnInstallSR, FloatSR,
                               LocalizationManager.GetString("L_Settings_Plugins_Model_SR_Title"),
                               _ctsSR);
        }

        private async void InstallInpaint_Click(object sender, RoutedEventArgs e)
        {
            _ctsInpaint?.Cancel();
            _ctsInpaint = new CancellationTokenSource();
            await InstallModel(AiService.AiTaskType.Inpainting,
                               TxtStatusInpaint, BtnInstallInpaint, FloatInpaint,
                               LocalizationManager.GetString("L_Settings_Plugins_Model_Inpaint_Title"),
                               _ctsInpaint);
        }

        // ─── 核心安装方法：复用 DownloadProgressFloat ───

        private async Task InstallModel(
            AiService.AiTaskType type,
            TextBlock txtStatus,
            Button btnInstall,
            DownloadProgressFloat floatProgress,
            string taskName,
            CancellationTokenSource cts)
        {
            btnInstall.IsEnabled = false;
            txtStatus.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Downloading");

            try
            {
                var aiService = new AiService(AppConsts.CacheDir);

                var progressReporter = new Progress<AiDownloadStatus>(status =>
                {
                    // 直接调用已有控件的 UpdateProgress，
                    // 它会自动处理显示/隐藏、百分比、速度、大小
                    Dispatcher.Invoke(() =>
                    {
                        floatProgress.UpdateProgress(status, taskName);
                    });
                });

                await aiService.PrepareModelAsync(type, progressReporter, cts.Token);

                // 下载完成 → 淡出进度条
                floatProgress.Finish();
                UpdateAllStatuses();
            }
            catch (OperationCanceledException)
            {
                floatProgress.Finish();
                txtStatus.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                btnInstall.IsEnabled = true;
            }
            catch (Exception ex)
            {
                floatProgress.Finish();
                txtStatus.Text = "Error: " + ex.Message;
                btnInstall.IsEnabled = true;
            }
        }

        // ─── Uninstall Click Handlers ───

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
                string modelName = type switch
                {
                    AiService.AiTaskType.RemoveBackground => AppConsts.BgRem_ModelName,
                    AiService.AiTaskType.SuperResolution => AppConsts.Sr_ModelName,
                    AiService.AiTaskType.Inpainting => AppConsts.Inpaint_ModelName,
                    _ => ""
                };

                string modelPath = Path.Combine(AppConsts.CacheDir, modelName);
                if (File.Exists(modelPath))
                    File.Delete(modelPath);

                UpdateAllStatuses();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show("Uninstall failed: " + ex.Message, "Error", MessageBoxButton.OK);
            }
        }
    }
}
