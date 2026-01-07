using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TabPaint
{
  
    public enum RulerOrientation { Horizontal, Vertical }

    public class Ruler : FrameworkElement
    {
        private const double SegmentHeight = 5; // 短刻度高度
        private const double SegmentHeightMid = 10; // 中刻度高度
        private const double SegmentHeightLong = 15; // 长刻度高度

        // 依赖属性：缩放比例
        public static readonly DependencyProperty ZoomFactorProperty =
            DependencyProperty.Register("ZoomFactor", typeof(double), typeof(Ruler),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double ZoomFactor
        {
            get { return (double)GetValue(ZoomFactorProperty); }
            set { SetValue(ZoomFactorProperty, value); }
        }

        // 依赖属性：原点偏移量 (画布左上角相对于ScrollViewer的位置)
        public static readonly DependencyProperty OriginOffsetProperty =
            DependencyProperty.Register("OriginOffset", typeof(double), typeof(Ruler),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double OriginOffset
        {
            get { return (double)GetValue(OriginOffsetProperty); }
            set { SetValue(OriginOffsetProperty, value); }
        }

        // 依赖属性：方向
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(RulerOrientation), typeof(Ruler),
                new FrameworkPropertyMetadata(RulerOrientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

        public RulerOrientation Orientation
        {
            get { return (RulerOrientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        // 依赖属性：鼠标位置标记
        public static readonly DependencyProperty MouseMarkerProperty =
            DependencyProperty.Register("MouseMarker", typeof(double), typeof(Ruler),
                 new FrameworkPropertyMetadata(-100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double MouseMarker
        {
            get { return (double)GetValue(MouseMarkerProperty); }
            set { SetValue(MouseMarkerProperty, value); }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // 1. 绘制背景
            drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(20, 245, 245, 245)), null, new Rect(0, 0, ActualWidth, ActualHeight));

            // 2. 绘制边缘分割线
            Pen borderPen = new Pen(Brushes.Gray, 1);
            borderPen.Freeze();
            if (Orientation == RulerOrientation.Horizontal)
                drawingContext.DrawLine(borderPen, new Point(0, ActualHeight), new Point(ActualWidth, ActualHeight));
            else
                drawingContext.DrawLine(borderPen, new Point(ActualWidth, 0), new Point(ActualWidth, ActualHeight));

            double zoom = ZoomFactor;
            if (zoom <= 0.0001) zoom = 0.0001; // 防止除以零

            double desiredPixelSpacing = 80.0; // 屏幕上每隔多少像素画一个大刻度才舒服？
            double rawStep = desiredPixelSpacing / zoom;

            // 找一个最接近的整洁数字 (1, 2, 5, 10, 20, 50, 100, 200...)
            // 计算数量级
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double residual = rawStep / magnitude;

            double logicalStep;
            if (residual > 5) logicalStep = 10 * magnitude;
            else if (residual > 2) logicalStep = 5 * magnitude;
            else if (residual > 1) logicalStep = 2 * magnitude;
            else logicalStep = magnitude;

            // 中刻度步长 (大刻度的一半)
            double midStep = logicalStep / 2.0;
            // 小刻度步长 (大刻度的十分之一，如果太密就不画)
            double smallStep = logicalStep / 10.0;

            // 屏幕上小刻度的间距，如果小于 4 像素，就不画小刻度了，太密看不清
            bool drawSmallTicks = (smallStep * zoom) > 4.0;

            // ==========================================

            double maxVal = (Orientation == RulerOrientation.Horizontal ? ActualWidth : ActualHeight);

            // 字体设置
            Typeface typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            Pen tickPen = new Pen(Brushes.DarkGray, 1);
            Pen textPen = new Pen(Brushes.Black, 1);
            tickPen.Freeze();
            textPen.Freeze();

            // 计算起始点
            double startValue = -OriginOffset / zoom;
            // 从稍早一点的地方开始画，避免边缘闪烁
            double startStepIndex = Math.Floor(startValue / smallStep);
            double currentVal = startStepIndex * smallStep;

            // 为了性能，最多画 2000 个刻度 (防止死循环或极端情况)
            int safetyCount = 0;

            while (safetyCount++ < 2000)
            {
                double screenPos = (currentVal * zoom) + OriginOffset;

                if (screenPos > maxVal) break; // 超出屏幕范围

                if (screenPos >= -20) // 只绘制可见区域
                {
                    // 判断当前刻度类型
                    bool isMainTick = IsCloseToMultiple(currentVal, logicalStep);
                    bool isMidTick = !isMainTick && IsCloseToMultiple(currentVal, midStep);

                    // 如果是小刻度，且太密了，就跳过
                    if (!isMainTick && !isMidTick && !drawSmallTicks)
                    {
                        currentVal += smallStep;
                        continue;
                    }

                    double tickHeight = isMainTick ? SegmentHeightLong : (isMidTick ? SegmentHeightMid : SegmentHeight);

                    if (Orientation == RulerOrientation.Horizontal)
                    {
                        // 画线
                        drawingContext.DrawLine(tickPen, new Point(screenPos, ActualHeight - tickHeight), new Point(screenPos, ActualHeight));

                        // 画数字 (只在主刻度画)
                        if (isMainTick)
                        {
                            FormattedText text = new FormattedText(
                                currentVal.ToString("F0"),
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                10,
                                Brushes.Black,
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);

                            drawingContext.DrawText(text, new Point(screenPos + 2, 0));
                        }
                    }
                    else // Vertical (竖向标尺)
                    {
                        // 画刻度线
                        drawingContext.DrawLine(tickPen, new Point(ActualWidth - tickHeight, screenPos), new Point(ActualWidth, screenPos));

                        if (isMainTick)
                        {
                            FormattedText text = new FormattedText(
                                currentVal.ToString("F0"),
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                10,
                                Brushes.Black,
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);


                            double xBase = ActualWidth - text.Height - 2;
                            double yBase = screenPos + (text.Width / 2)+10;

                            drawingContext.PushTransform(new RotateTransform(-90, xBase, yBase));
                            drawingContext.DrawText(text, new Point(xBase, yBase));
                            drawingContext.Pop();
                        }
                    }
                }

                // 推进循环
                currentVal += smallStep;

                // 浮点数累加修正 (防止 0.1 + 0.2 = 0.300000004)
                currentVal = Math.Round(currentVal / smallStep) * smallStep;
            }

            // 绘制鼠标红线
            Pen markerPen = new Pen(Brushes.Red, 1);
            markerPen.Freeze();
            if (MouseMarker >= 0 && MouseMarker <= maxVal)
            {
                if (Orientation == RulerOrientation.Horizontal)
                    drawingContext.DrawLine(markerPen, new Point(MouseMarker, 0), new Point(MouseMarker, ActualHeight));
                else
                    drawingContext.DrawLine(markerPen, new Point(0, MouseMarker), new Point(ActualWidth, MouseMarker));
            }
        }

        // 辅助函数：判断 value 是否接近 step 的倍数
        private bool IsCloseToMultiple(double value, double step)
        {
            double tolerance = step * 0.001;
            double remainder = Math.Abs(value % step);
            return remainder < tolerance || Math.Abs(remainder - step) < tolerance;
        }

    }
}
