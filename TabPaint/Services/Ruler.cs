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
        private const double SegmentHeight = AppConsts.RulerSegmentHeight; // 短刻度高度
        private const double SegmentHeightMid = AppConsts.RulerSegmentHeightMid; // 中刻度高度
        private const double SegmentHeightLong = AppConsts.RulerSegmentHeightLong; // 长刻度高度
        public static readonly DependencyProperty SelectionStartProperty =
    DependencyProperty.Register("SelectionStart", typeof(double), typeof(Ruler),
        new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double SelectionStart
        {
            get { return (double)GetValue(SelectionStartProperty); }
            set { SetValue(SelectionStartProperty, value); }
        }

        // 选区结束位置（像素坐标）
        public static readonly DependencyProperty SelectionEndProperty =
            DependencyProperty.Register("SelectionEnd", typeof(double), typeof(Ruler),
                new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double SelectionEnd
        {
            get { return (double)GetValue(SelectionEndProperty); }
            set { SetValue(SelectionEndProperty, value); }
        }

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
        private void DrawSelectionHighlight(DrawingContext drawingContext, double zoom)
        {
            if (SelectionStart < 0 || SelectionEnd <= SelectionStart) return;

            // 像素坐标 → 屏幕坐标
            double screenStart = SelectionStart * zoom + OriginOffset;
            double screenEnd = SelectionEnd * zoom + OriginOffset;

            double maxVal = (Orientation == RulerOrientation.Horizontal) ? ActualWidth : ActualHeight;

            // 裁剪到可见范围
            screenStart = Math.Max(0, screenStart);
            screenEnd = Math.Min(maxVal, screenEnd);

            if (screenEnd <= screenStart) return;

            // 使用主题色半透明作为高亮背景
            Brush accentBrush = (Brush)TryFindResource("SystemAccentBrush") ?? Brushes.DodgerBlue;
            Color accentColor;
            if (accentBrush is SolidColorBrush scb)
                accentColor = scb.Color;
            else
                accentColor = Colors.DodgerBlue;

            Brush highlightBrush = new SolidColorBrush(Color.FromArgb(15, accentColor.R, accentColor.G, accentColor.B)); 
            if (highlightBrush.CanFreeze) highlightBrush.Freeze();

            // 高亮条下方的实色细线（标记边界）
            Brush edgeBrush = new SolidColorBrush(Color.FromArgb(12, accentColor.R, accentColor.G, accentColor.B));
            if (edgeBrush.CanFreeze) edgeBrush.Freeze();
            Pen edgePen = new Pen(edgeBrush, 1);
            edgePen.Freeze();

            if (Orientation == RulerOrientation.Horizontal)
            {
                // 高亮背景条
                drawingContext.DrawRectangle(highlightBrush, null,
                    new Rect(screenStart, 0, screenEnd - screenStart, ActualHeight));

                // 左右边界线
                drawingContext.DrawLine(edgePen, new Point(screenStart, 0), new Point(screenStart, ActualHeight));
                drawingContext.DrawLine(edgePen, new Point(screenEnd, 0), new Point(screenEnd, ActualHeight));
            }
            else
            {
                // 高亮背景条
                drawingContext.DrawRectangle(highlightBrush, null,
                    new Rect(0, screenStart, ActualWidth, screenEnd - screenStart));

                // 上下边界线
                drawingContext.DrawLine(edgePen, new Point(0, screenStart), new Point(ActualWidth, screenStart));
                drawingContext.DrawLine(edgePen, new Point(0, screenEnd), new Point(ActualWidth, screenEnd));
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            Brush textBrush = (Brush)TryFindResource("TextPrimaryBrush") ?? Brushes.Black;
            Brush tickBrush = (Brush)TryFindResource("TextTertiaryBrush") ?? Brushes.Gray;
            Brush borderBrush = (Brush)TryFindResource("BorderMediumBrush") ?? Brushes.Gray;
            Brush bgBrush = (Brush)TryFindResource("GlassBackgroundMediumBrush") ?? Brushes.Transparent;

            Pen borderPen = new Pen(borderBrush, 1);
            borderPen.Freeze();
            Pen tickPen = new Pen(tickBrush, 1);
            tickPen.Freeze();
            Pen textPen = new Pen(textBrush, 1);
            textPen.Freeze();

            drawingContext.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (Orientation == RulerOrientation.Horizontal)
                drawingContext.DrawLine(borderPen, new Point(0, ActualHeight), new Point(ActualWidth, ActualHeight));
            else
                drawingContext.DrawLine(borderPen, new Point(ActualWidth, 0), new Point(ActualWidth, ActualHeight));

            double zoom = ZoomFactor;
            if (zoom <= 0.0001) zoom = 0.0001;

            // ========== 绘制选区高亮 ==========
            DrawSelectionHighlight(drawingContext, zoom);

            // ========== 原有刻度绘制逻辑 ==========
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
            double selScreenStart = -1, selScreenEnd = -1;
            bool hasSelection = SelectionStart >= 0 && SelectionEnd > SelectionStart;
            if (hasSelection)
            {
                selScreenStart = SelectionStart * zoom + OriginOffset;
                selScreenEnd = SelectionEnd * zoom + OriginOffset;
            }

            // 选区内的刻度用主题色
            Brush accentTextBrush = (Brush)TryFindResource("SystemAccentBrush") ?? Brushes.DodgerBlue;
            Pen accentTickPen = new Pen(accentTextBrush, 1);
            accentTickPen.Freeze();

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

                    // 判断当前刻度是否在选区范围内
                    bool inSelection = hasSelection && screenPos >= selScreenStart - 0.5 && screenPos <= selScreenEnd + 0.5;
                    Pen currentTickPen = inSelection ? accentTickPen : tickPen;
                    Brush currentTextBrush = inSelection ? accentTextBrush : textBrush;

                    if (Orientation == RulerOrientation.Horizontal)
                    {
                        drawingContext.DrawLine(currentTickPen, new Point(screenPos, ActualHeight - tickHeight), new Point(screenPos, ActualHeight));

                        if (isMainTick)
                        {
                            FormattedText text = new FormattedText(
                                currentVal.ToString("F0"),
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                10,
                                currentTextBrush,
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);

                            drawingContext.DrawText(text, new Point(screenPos + 2, 0));
                        }
                    }
                    else // Vertical
                    {
                        drawingContext.DrawLine(currentTickPen, new Point(ActualWidth - tickHeight, screenPos), new Point(ActualWidth, screenPos));

                        if (isMainTick)
                        {
                            FormattedText text = new FormattedText(
                                currentVal.ToString("F0"),
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                10,
                                currentTextBrush,
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
        private bool IsCloseToMultiple(double value, double step)
        {
            double tolerance = step * 0.001;
            double remainder = Math.Abs(value % step);
            return remainder < tolerance || Math.Abs(remainder - step) < tolerance;
        }

    }
}
