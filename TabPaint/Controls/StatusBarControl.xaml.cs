using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TabPaint.Controls
{
    public class ZoomRoutedEventArgs : RoutedEventArgs
    {
        public double NewZoom { get; }
        public ZoomRoutedEventArgs(RoutedEvent routedEvent, double newZoom) : base(routedEvent)
        {
            NewZoom = newZoom;
        }
    }

    public partial class StatusBarControl : UserControl
    {
        // 1. 定义路由事件
        public static readonly RoutedEvent ClipboardMonitorClickEvent = EventManager.RegisterRoutedEvent(
            "ClipboardMonitorClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent FitToWindowClickEvent = EventManager.RegisterRoutedEvent(
            "FitToWindowClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent ZoomOutClickEvent = EventManager.RegisterRoutedEvent(
            "ZoomOutClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent ZoomInClickEvent = EventManager.RegisterRoutedEvent(
            "ZoomInClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent ZoomSelectionChangedEvent = EventManager.RegisterRoutedEvent(
            "ZoomSelectionChanged", RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(StatusBarControl));

        // 2. 暴露事件
        public event RoutedEventHandler ClipboardMonitorClick
        {
            add { AddHandler(ClipboardMonitorClickEvent, value); }
            remove { RemoveHandler(ClipboardMonitorClickEvent, value); }
        }
        public event RoutedEventHandler FitToWindowClick
        {
            add { AddHandler(FitToWindowClickEvent, value); }
            remove { RemoveHandler(FitToWindowClickEvent, value); }
        }
        public event RoutedEventHandler ZoomOutClick
        {
            add { AddHandler(ZoomOutClickEvent, value); }
            remove { RemoveHandler(ZoomOutClickEvent, value); }
        }
        public event RoutedEventHandler ZoomInClick
        {
            add { AddHandler(ZoomInClickEvent, value); }
            remove { RemoveHandler(ZoomInClickEvent, value); }
        }
        public event SelectionChangedEventHandler ZoomSelectionChanged
        {
            add { AddHandler(ZoomSelectionChangedEvent, value); }
            remove { RemoveHandler(ZoomSelectionChangedEvent, value); }
        }

        // 3. 暴露内部控件 (为了保持 MainWindow 代码兼容性)
        public ComboBox ZoomComboBox => ZoomMenu;
        public ToggleButton ClipboardToggle => ClipboardMonitorToggle;
        public Slider ZoomSliderControl => ZoomSlider;


        public StatusBarControl()
        {
            InitializeComponent();
        }

        // 4. 内部事件触发逻辑
        private void OnClipboardMonitorToggleClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ClipboardMonitorClickEvent));
        }

        private void OnFitToWindowClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(FitToWindowClickEvent));
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ZoomOutClickEvent));
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ZoomInClickEvent));
        }

        private void OnZoomMenuSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZoomMenu.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (double.TryParse(item.Tag.ToString(), out double val))
                {
                    RaiseZoomChanged(val);
                }
            }
        }
        public static readonly RoutedEvent ZoomChangedEvent = EventManager.RegisterRoutedEvent(
          "ZoomChanged", RoutingStrategy.Bubble, typeof(EventHandler<ZoomRoutedEventArgs>), typeof(StatusBarControl));

        // 2. 暴露事件
        public event EventHandler<ZoomRoutedEventArgs> ZoomChanged
        {
            add { AddHandler(ZoomChangedEvent, value); }
            remove { RemoveHandler(ZoomChangedEvent, value); }
        }


        // B. 按下回车键时
        private void OnZoomMenuPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryApplyZoomText();

                e.Handled = true; // 标记已处理，防止发出系统提示音
            }
        }

        // C. 失去焦点时 (比如点到了画布上)
        private void OnZoomMenuLostFocus(object sender, RoutedEventArgs e)
        {
           // System.Windows.MessageBox.Show("23OnZoomMenuLostFocus45");
            TryApplyZoomText();
        }
        private void OnStatusBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 只有当点击的是 StatusBar 自身或者非按钮类控件时才抢夺焦点
            // 这样不会影响点击其他按钮的功能
            if (sender is UIElement element)
            {
                element.Focus();
            }
        }

        // D. 解析文本并触发事件的通用方法
        private void TryApplyZoomText()
        {
            string text = ZoomMenu.Text.Trim();

            // 去掉可能存在的 % 号
            text = text.Replace("%", "");

            if (double.TryParse(text, out double result))
            {
                double finalZoom = result / 100.0;

                RaiseZoomChanged(finalZoom);
            }
        }

        // 触发事件的辅助方法
        private void RaiseZoomChanged(double zoom)
        {
            RaiseEvent(new ZoomRoutedEventArgs(ZoomChangedEvent, zoom));
        }
    }
}
