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
//
//TabPaint主程序
//

namespace TabPaint
{
    public class ZoomToInverseValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double scale && scale > 0)
            {
                // 获取基础数值 (参数传入，默认为1)
                double baseSize = 1.0;
                if (parameter != null && double.TryParse(parameter.ToString(), out double p))
                {
                    baseSize = p;
                }

                // 计算反向值： 基础值 / 缩放倍率
                double result = baseSize / scale ;

                // 如果目标是 Thickness (用于 BorderThickness 或 Margin)
                if (targetType == typeof(Thickness))
                {
                    return new Thickness(result);
                }

                // 如果目标是 double (用于 Shadow BlurRadius)
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
           
            // 获取子元素想要的尺寸
            var size = base.MeasureOverride(constraint);
            // 向上取整 (Ceiling)，避免内容被切掉
            return new Size(Math.Ceiling(size.Width), Math.Ceiling(size.Height));
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            // 强制将分配给子元素的区域取整
            var snappedSize = new Size(Math.Ceiling(arrangeSize.Width), Math.Ceiling(arrangeSize.Height));
            base.ArrangeOverride(snappedSize);
            return snappedSize;
        }
    }
    public class NonLinearConverter : IValueConverter
    {
        // 设定的最大阈值，对应 XAML 中的 Maximum="5000"
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
                // 公式：Data = 5000 * (Slider / 100)^2
                double ratio = sVal / MaxSliderValue;
                return Math.Round(MaxDataValue * ratio * ratio); // 取整，避免小数点
            }
            return 0.0;
        }
    }
    public class NaturalStringComparer : IComparer<string>
    {
        // 单例模式，避免重复创建对象
        public static readonly NaturalStringComparer Default = new NaturalStringComparer();

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [SuppressUnmanagedCodeSecurity] // 稍微提升一点性能
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

        /// <summary>
        /// 严苛版性能评估 (< 1ms)
        /// 目标：将普通办公本压在 3-5 分，主流游戏本 6-8 分，顶级工作站 9-10 分。
        /// </summary>
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
                else if (cpuTicks < 9000) score += 1.0;   // 老旧机器
                // > 9000 ticks: 0分

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                // 大于 2K 分辨率 (约360万像素)
                if (screenWidth * screenHeight > 3600000)
                {
                    if (cpuTicks > 4500)
                    {
                        score -= 1.5;
                    }
                }

                // 兜底修正
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
                // 开发阶段建议保留此行，观察不同机器的真实 Ticks 数据以便微调
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

            // 防止 JIT 优化移除循环
            if (result == 999999) Debug.WriteLine("");

            return sw.ElapsedTicks;
        }
    }
    public class NonLinearRangeConverter : IValueConverter
    {
        // 最小粗细
        private const double MinSize = 1.0;
        // 最大粗细
        private const double MaxSize = 400.0;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double sliderVal)
            {
                // 确保 sliderVal 在 0~1 之间
                double t = Math.Max(0.0, Math.Min(1.0, sliderVal));

                // 使用平方曲线 (t * t) 使得小数值部分调节更细腻
                double result = MinSize + (MaxSize - MinSize) * (t * t);

                return Math.Round(result); // 取整，因为像素通常是整数
            }
            return MinSize;
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double thickness || value is int || value is float)
            {
                double v = System.Convert.ToDouble(value);

                // 反向计算比例
                double ratio = (v - MinSize) / (MaxSize - MinSize);

                // 开根号还原线性进度
                double sliderVal = Math.Sqrt(Math.Max(0.0, ratio));

                return sliderVal;
            }
            return 0.0;
        }
    }
    public class ScaleToTileRectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double scale = 1.0;
            if (value is double d) scale = d;

            // 防止除以0或极小值
            if (scale < 0.01) scale = 0.01;

            // 参数传入基础格子大小 (例如 20)
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

            // 公式: x = 100 * log(y/min) / log(max/min)
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
                // 第一次调用：Stopwatch 为空，开始计时
                if (_stopwatch == null)
                {
                    _stopwatch = Stopwatch.StartNew();
                    a.s("计时开始...");
                }
                // 第二次调用：Stopwatch 正在运行，停止并打印耗时
                else if (_stopwatch.IsRunning)
                {
                    _stopwatch.Stop();
                    s($"耗时：{_stopwatch.Elapsed.TotalMilliseconds} 毫秒");

                    // 可选：如果希望第三次调用重新开始，可以重置
                    // _stopwatch = null; 
                }
                else
                {
                    Console.WriteLine("计时器已结束。如需重新开始，请重置状态。");
                }
            }

            // 重置方法（可选）
            public void Reset()
            {
                _stopwatch = null;
            }
        }

    }
}
