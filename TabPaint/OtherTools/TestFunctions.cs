using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
//
//TabPaint主程序
//

namespace TabPaint
{

    public class ColorMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
            {
                return parameter.ToString();
            }
            return Binding.DoNothing;
        }
    }
    public class ZoomToInverseValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double scale && scale > 0)
            {
                double baseSize = 1.0;
                if (parameter != null && double.TryParse(parameter.ToString(), out double p))
                {
                    baseSize = p;
                }
                double result = baseSize / scale ;

                if (targetType == typeof(Thickness))
                {
                    return new Thickness(result);
                }
                return result;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class PixelSnapper : Decorator
    {
        public PixelSnapper()
        {
            this.SnapsToDevicePixels = true;
            this.UseLayoutRounding = true;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var size = base.MeasureOverride(constraint);
            return new Size(Math.Ceiling(size.Width), Math.Ceiling(size.Height));
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var snappedSize = new Size(Math.Ceiling(arrangeSize.Width), Math.Ceiling(arrangeSize.Height));
            base.ArrangeOverride(snappedSize);
            return snappedSize;
        }
    }
    public class NonLinearConverter : IValueConverter
    {
        private const double MaxDataValue = 5000.0;
        private const double MaxSliderValue = 100.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 数据 -> Slider 位置 (开根号，为了让大数值被压缩)
            if (value is double dVal)
            {
                // 公式：Slider = 100 * Sqrt(Data / 5000)
                return MaxSliderValue * Math.Sqrt(dVal / MaxDataValue);
            }
            if (value is int iVal)
            {
                return MaxSliderValue * Math.Sqrt((double)iVal / MaxDataValue);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Slider 位置 -> 数据 (平方，恢复数据)
            if (value is double sVal)
            {
                double ratio = sVal / MaxSliderValue;
                return Math.Round(MaxDataValue * ratio * ratio); // 取整，避免小数点
            }
            return 0.0;
        }
    }
    public class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Default = new NaturalStringComparer();

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [SuppressUnmanagedCodeSecurity] 
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public int Compare(string? x, string? y)
        {
            // 处理 null 情况
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            return StrCmpLogicalW(x, y);
        }
    }
    public static class QuickBenchmark
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);

        public static int EstimatePerformanceScore()
        {
            double score = 0;
            var sw = Stopwatch.StartNew();

            try
            {
                int coreCount = Environment.ProcessorCount;
                if (coreCount >= 20) score += 2.5;       // i7-13700K, i9, R9 等
                else if (coreCount >= 12) score += 2.0;  // 现代标压 i5/i7, R7
                else if (coreCount >= 8) score += 1.5;   // 主流轻薄本
                else if (coreCount >= 4) score += 0.5;   // 入门级/老旧双核
                // 4核以下 0分

                long memKb = 0;
                if (GetPhysicallyInstalledSystemMemory(out memKb))
                {
                    long memGb = memKb / 1024 / 1024;
                    if (memGb >= 30) score += 1.5;       // 32GB及以上
                    else if (memGb >= 15) score += 1.0;  // 16GB
                    else if (memGb >= 7) score += 0.5;   // 8GB
                    // 8GB以下 0分
                }
                long cpuTicks = RunStrictMicroTest();

                if (cpuTicks < 1800) score += 6.0;
                else if (cpuTicks < 2200) score += 5.0;
                else if (cpuTicks < 3000) score += 4.0;
                else if (cpuTicks < 4500) score += 3.0;
                else if (cpuTicks < 6500) score += 2.0;   // 普通办公本区间
                else if (cpuTicks < 9000) score += 1.0; 
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                // 大于 2K 分辨率 (约360万像素)
                if (screenWidth * screenHeight > 3600000)if (cpuTicks > 4500) score -= 1.5;
               
                if (score > 10) score = 10;
                if (score < 1) score = 1;
            }
            catch
            {
                return 4; // 发生异常给个及格分下的保守值
            }
            finally
            {
                sw.Stop();
                Debug.WriteLine($"[Benchmark] Ticks: {RunStrictMicroTest()} | Score: {score} | Time: {sw.Elapsed.TotalMilliseconds:F4}ms");
            }
            return (int)Math.Round(score);
        }

        private static long RunStrictMicroTest()
        {
            var sw = Stopwatch.StartNew();
            int result = 0;
            for (int i = 1; i < 150000; i++)
            {
                result += (i * 3) ^ (i % 7);
            }
            sw.Stop();

            if (result == 999999) Debug.WriteLine("");

            return sw.ElapsedTicks;
        }
    }
    public class DynamicRangeConverter : IMultiValueConverter
    {
        private const double MinSize = 1.0;
        private double GetMaxSizeForKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return 400.0; // 默认
         //   
            if (key == "Shape") return 24.0; // ★ Shape 工具上限锁死为 24
            if (key == "Pen_Pencil") return 10.0;

            return 400.0; // 其他画笔默认 400
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == DependencyProperty.UnsetValue)
                return 0.0;

            double thickness = System.Convert.ToDouble(values[0]);
            string toolKey = values[1] as string;

            double maxSize = GetMaxSizeForKey(toolKey);
            double ratio = (thickness - MinSize) / (maxSize - MinSize);

            // 开根号还原线性进度
            double sliderVal = Math.Sqrt(Math.Max(0.0, Math.Min(1.0, ratio)));
      
            return sliderVal;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if (value is double sliderVal)
            {
                string toolKey = SettingsManager.Instance.Current.CurrentToolKey;
                double maxSize = GetMaxSizeForKey(toolKey);

                double t = Math.Max(0.0, Math.Min(1.0, sliderVal));
                double result = MinSize + (maxSize - MinSize) * (t * t);
                return new object[] { Math.Round(result), Binding.DoNothing };
            }

            return new object[] { MinSize, Binding.DoNothing };
        }
    }
    public class ScaleToTileRectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double scale = 1.0;
            if (value is double d) scale = d;
            if (scale < 0.01) scale = 0.01;
            double baseSize = 20.0;
            if (parameter != null && double.TryParse(parameter.ToString(), out double parsedSize))
            {
                baseSize = parsedSize;
            }

            double newSize = baseSize / scale;

            // 返回一个新的 Rect 用于 Viewport
            return new Rect(0, 0, newSize, newSize);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        #region s

        public static void s<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        public static void s(){ System.Windows.MessageBox.Show("空messagebox", "标题", MessageBoxButton.OK, MessageBoxImage.Information);}
        public static void msgbox<T>(T a) {System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);}
        public static void s2<T>(T a) {Debug.Print(a.ToString()); }
        public static class a
        {
            public static void s(params object[] args)
            {
                // 可以根据需要拼接输出格式
                string message = string.Join(" ", args);
                Debug.WriteLine(message);
            }
        }
        #endregion

        private const double MinZoomReal = 0.1;  // 10%
        private const double MaxZoomReal = 16.0; // 1600%
        private double ZoomToSlider(double realZoom)
        {
            // 越界保护
            if (realZoom < MinZoomReal) realZoom = MinZoomReal;
            if (realZoom > MaxZoomReal) realZoom = MaxZoomReal;
            return 100.0 * Math.Log(realZoom / MinZoomReal) / Math.Log(MaxZoomReal / MinZoomReal);
        }
        private double SliderToZoom(double sliderValue)
        {
            // 公式: y = min * (max/min)^(x/100)
            double percent = sliderValue / 100.0;
            return MinZoomReal * Math.Pow(MaxZoomReal / MinZoomReal, percent);
        }

        public class TimeRecorder
        {
            private Stopwatch _stopwatch;

            public void Toggle()
            {
                if (_stopwatch == null)
                {
                    _stopwatch = Stopwatch.StartNew();
                    a.s("计时开始...");
                }
                else if (_stopwatch.IsRunning)
                {
                    _stopwatch.Stop();
                    s($"耗时：{_stopwatch.Elapsed.TotalMilliseconds} 毫秒");
                }
                else
                {
                    Console.WriteLine("计时器已结束。如需重新开始，请重置状态。");
                }
            }
            public void Reset()
            {
                _stopwatch = null;
            }
        }

    }
}
