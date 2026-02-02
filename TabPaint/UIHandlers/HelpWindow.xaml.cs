using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using XamlAnimatedGif; // 引用库

namespace TabPaint
{
    // 修改数据模型，存储 Uri 
    public class HelpPage
    {
        public Uri ImageUri { get; set; } // 换成 Uri 以支持 Gif 重载
        public string DescriptionKey { get; set; }
    }

    public partial class HelpWindow : Window, INotifyPropertyChanged
    {
        private List<HelpPage> _pages;
        private int _currentIndex = 0;
        private DispatcherTimer _gifDelayTimer;

        public event PropertyChangedEventHandler PropertyChanged;

        // 绑定属性改为 Uri
        public Uri CurrentImageUri => _pages.Count > 0 ? _pages[_currentIndex].ImageUri : null;
        public string CurrentDescription => _pages.Count > 0 ? LocalizationManager.GetString(_pages[_currentIndex].DescriptionKey) : "";

        public ObservableCollection<IndicatorItem> Indicators { get; set; } = new ObservableCollection<IndicatorItem>();

        public HelpWindow(List<HelpPage> pages)
        {
            InitializeComponent();
            _pages = pages;
            this.DataContext = this;

            // 初始化圆点
            foreach (var page in _pages)
            {
                Indicators.Add(new IndicatorItem { IsActive = false });
            }

            // 初始化计时器
            _gifDelayTimer = new DispatcherTimer();
            _gifDelayTimer.Interval = TimeSpan.FromSeconds(1.0); // 1秒延迟
            _gifDelayTimer.Tick += GifDelayTimer_Tick;

            // 初始加载
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_pages == null || _pages.Count == 0) return;

            // 1. 更新数据绑定
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentImageUri)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDescription)));

            // 2. 更新圆点
            for (int i = 0; i < Indicators.Count; i++)
            {
                Indicators[i].IsActive = (i == _currentIndex);
            }

            // 3. 处理 GIF 延迟播放逻辑
            _gifDelayTimer.Stop(); // 先停止之前的计时

            // XamlAnimatedGif 可能需要一点时间加载新的 SourceUri
            // 使用低优先级调度，确保 UI 绑定完成后再控制动画
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    // 获取当前图片的动画控制器
                    var controller = AnimationBehavior.GetAnimator(MainImageDisplay);
                    if (controller != null)
                    {
                        controller.Rewind(); // 倒带
                        controller.Pause();  // 暂停 (UI上会显示第一帧)
                        _gifDelayTimer.Start(); // 开始1秒计时
                    }
                }
                catch
                {
                    // 普通图片没有 Controller，忽略异常
                }
            }));
        }

        private void GifDelayTimer_Tick(object sender, EventArgs e)
        {
            _gifDelayTimer.Stop();
            try
            {
                var controller = AnimationBehavior.GetAnimator(MainImageDisplay);
                if (controller != null)
                {
                    controller.Play(); // 1秒后开始播放
                }
            }
            catch { }
        }

        private void Next()
        {
            _currentIndex = (_currentIndex + 1) % _pages.Count; // 循环
            UpdateUI();
        }

        private void Previous()
        {
            _currentIndex = (_currentIndex - 1 + _pages.Count) % _pages.Count; // 循环
            UpdateUI();
        }

        // --- 事件处理 ---

        private void NextButton_Click(object sender, MouseButtonEventArgs e) => Next();
        private void PrevButton_Click(object sender, MouseButtonEventArgs e) => Previous();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();

        // 3. ESC 关闭
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left) Previous();
            else if (e.Key == Key.Right) Next();
            else if (e.Key == Key.Escape) this.Close();
        }

        // 3. 点击窗口外部 (失去焦点) 关闭
        private void Window_Deactivated(object sender, EventArgs e)
        {
            // 为了防止只是切个窗口就被关掉，可以判断一下 Owner 是否还活着
            // 但通常 Deactivated 直接 Close 就能实现"轻弹窗"效果
            try
            {
                this.Close();
            }
            catch { }
        }
    }
}
