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
            Brush textBrush = (Brush)TryFindResource("TextPrimaryBrush") ?? Brushes.Black;
            Brush tickBrush = (Brush)TryFindResource("TextTertiaryBrush") ?? Brushes.Gray;
            Brush borderBrush = (Brush)TryFindResource("BorderMediumBrush") ?? Brushes.Gray;
            Brush bgBrush = (Brush)TryFindResource("GlassBackgroundMediumBrush") ?? Brushes.Transparent;

            // 冻结画刷以提升性能 (OnRender会被频繁调用)
            if (textBrush.CanFreeze) textBrush = textBrush.Clone(); // 确保是副本以便Freeze? 其实通常资源已经是Freezed的，但在代码中创建Pen最好Freeze Pen
            Pen borderPen = new Pen(borderBrush, 1);
            borderPen.Freeze();

            Pen tickPen = new Pen(tickBrush, 1);
            tickPen.Freeze();

            Pen textPen = new Pen(textBrush, 1); // 如果需要文字轮廓才用这个，DrawText不需要Pen
            textPen.Freeze();
            drawingContext.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (Orientation == RulerOrientation.Horizontal)
                drawingContext.DrawLine(borderPen, new Point(0, ActualHeight), new Point(ActualWidth, ActualHeight));
            else
                drawingContext.DrawLine(borderPen, new Point(ActualWidth, 0), new Point(ActualWidth, ActualHeight));

            double zoom = ZoomFactor;
            if (zoom <= 0.0001) zoom = 0.0001;

            double desiredPixelSpacing = 80.0;
            double rawStep = desiredPixelSpacing / zoom;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double residual = rawStep / magnitude;

            double logicalStep;
            if (residual > 5) logicalStep = 10 * magnitude;
            else if (residual > 2) logicalStep = 5 * magnitude;
            else if (residual > 1) logicalStep = 2 * magnitude;
            else logicalStep = magnitude;

            double midStep = logicalStep / 2.0;
            double smallStep = logicalStep / 10.0;
            bool drawSmallTicks = (smallStep * zoom) > 4.0;
            double maxVal = (Orientation == RulerOrientation.Horizontal ? ActualWidth : ActualHeight);

            Typeface typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            double startValue = -OriginOffset / zoom;
            double startStepIndex = Math.Floor(startValue / smallStep);
            double currentVal = startStepIndex * smallStep;
            int safetyCount = 0;

            while (safetyCount++ < 2000)
            {
                double screenPos = (currentVal * zoom) + OriginOffset;

                if (screenPos > maxVal) break;

                if (screenPos >= -20)
                {
                    bool isMainTick = IsCloseToMultiple(currentVal, logicalStep);
                    bool isMidTick = !isMainTick && IsCloseToMultiple(currentVal, midStep);

                    if (!isMainTick && !isMidTick && !drawSmallTicks)
                    {
                        currentVal += smallStep;
                        continue;
                    }

                    double tickHeight = isMainTick ? SegmentHeightLong : (isMidTick ? SegmentHeightMid : SegmentHeight);

                    if (Orientation == RulerOrientation.Horizontal)
                    {
                        drawingContext.DrawLine(tickPen, new Point(screenPos, ActualHeight - tickHeight), new Point(screenPos, ActualHeight));

                        if (isMainTick)
                        {
                            FormattedText text = new FormattedText(
                                currentVal.ToString("F0"),
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                10,
                                textBrush, 
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);

                            drawingContext.DrawText(text, new Point(screenPos + 2, 0));
                        }
                    }
                    else // Vertical
                    { // 画刻度线 (使用 tickPen)
                        drawingContext.DrawLine(tickPen, new Point(ActualWidth - tickHeight, screenPos), new Point(ActualWidth, screenPos));

                        if (isMainTick)
                        {
                            FormattedText text = new FormattedText(
                                currentVal.ToString("F0"),
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                10,
                                textBrush, 
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);

                            double xBase = ActualWidth - text.Height - 2;
                            double yBase = screenPos + (text.Width / 2) + 10;

                            drawingContext.PushTransform(new RotateTransform(-90, xBase, yBase));
                            drawingContext.DrawText(text, new Point(xBase, yBase));
                            drawingContext.Pop();
                        }
                    }
                }
                currentVal += smallStep;
                currentVal = Math.Round(currentVal / smallStep) * smallStep;
            }

            // 绘制鼠标红线 (保持红色或使用 DangerBrush)
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
        private bool IsCloseToMultiple(double value, double step)
        {
            double tolerance = step * 0.001;
            double remainder = Math.Abs(value % step);
            return remainder < tolerance || Math.Abs(remainder - step) < tolerance;
        }

    }
}
