using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TabPaint.Behaviors
{
    public class AdvancedToolTipBehavior
    {
        public static readonly DependencyProperty DetailedToolTipProperty =
            DependencyProperty.RegisterAttached(
                "DetailedToolTip",
                typeof(object),
                typeof(AdvancedToolTipBehavior),
                new PropertyMetadata(null));

        public static object GetDetailedToolTip(DependencyObject obj)
        {
            return obj.GetValue(DetailedToolTipProperty);
        }

        public static void SetDetailedToolTip(DependencyObject obj, object value)
        {
            obj.SetValue(DetailedToolTipProperty, value);
        }

        public static readonly DependencyProperty EnableAdvancedToolTipProperty =
            DependencyProperty.RegisterAttached(
                "EnableAdvancedToolTip",
                typeof(bool),
                typeof(AdvancedToolTipBehavior),
                new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnableAdvancedToolTip(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableAdvancedToolTipProperty);
        }

        public static void SetEnableAdvancedToolTip(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableAdvancedToolTipProperty, value);
        }

        private static readonly DependencyProperty OriginalToolTipProperty =
            DependencyProperty.RegisterAttached("OriginalToolTip", typeof(object), typeof(AdvancedToolTipBehavior), new PropertyMetadata(null));

        // 存储计时器
        private static readonly DependencyProperty TimerProperty =
            DependencyProperty.RegisterAttached("Timer", typeof(DispatcherTimer), typeof(AdvancedToolTipBehavior), new PropertyMetadata(null));

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.MouseEnter += Element_MouseEnter;
                    element.MouseLeave += Element_MouseLeave;
                    element.ToolTipOpening += Element_ToolTipOpening;
                }
                else
                {
                    element.MouseEnter -= Element_MouseEnter;
                    element.MouseLeave -= Element_MouseLeave;
                    element.ToolTipOpening -= Element_ToolTipOpening;
                }
            }
        }

        private static void Element_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            // 确保每次打开Tooltip时，先显示原始的基础提示
            var element = sender as FrameworkElement;
            var original = element.GetValue(OriginalToolTipProperty);
            if (original != null)
            {
                element.ToolTip = original;
            }
        }

        private static void Element_MouseEnter(object sender, MouseEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            if (element.GetValue(OriginalToolTipProperty) == null)
            {
                element.SetValue(OriginalToolTipProperty, element.ToolTip);
            }
            var original = element.GetValue(OriginalToolTipProperty);
            if (original != null) element.ToolTip = original;
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1.5); 
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                // 切换到详细提示
                var detailed = GetDetailedToolTip(element);
                if (detailed != null)
                {
                    element.ToolTip = detailed;

                    if (element.ToolTip is ToolTip tt)
                    {
                        tt.IsOpen = true;
                    }
                }
            };

            element.SetValue(TimerProperty, timer);
            timer.Start();
        }
        private static void Element_MouseLeave(object sender, MouseEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            // 1. 停止计时器
            var timer = element.GetValue(TimerProperty) as DispatcherTimer;
            if (timer != null)
            {
                timer.Stop();
                element.SetValue(TimerProperty, null);
            }
            var original = element.GetValue(OriginalToolTipProperty);
            if (original != null)
            {
                element.ToolTip = original;
            }
        }
    }
}
