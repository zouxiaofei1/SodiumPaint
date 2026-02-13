using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TabPaint;
using static TabPaint.MainWindow;

public partial class ShapeTool : ToolBase
{
    private Geometry BuildArrowGeometry(Point start, Point end, double headLength)
    {
        Vector vec = end - start;
        if (vec.LengthSquared < 0.001) vec = new Vector(0, 1);
        vec.Normalize();
        if (Double.IsNaN(vec.X)) vec = new Vector(1, 0);

        Vector backVec = -vec;
        double angle = AppConsts.ArrowAngleDegrees;
        Matrix m1 = Matrix.Identity; m1.Rotate(angle);
        Matrix m2 = Matrix.Identity; m2.Rotate(-angle);

        Vector wing1 = m1.Transform(backVec) * headLength;
        Vector wing2 = m2.Transform(backVec) * headLength;
        Point p1 = end + wing1;
        Point p2 = end + wing2;

        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.LineTo(end, true, false);
            ctx.LineTo(p1, true, false);
            ctx.BeginFigure(end, false, false);
            ctx.LineTo(p2, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private Geometry BuildRegularPolygon(Rect rect, int sides, double startAngle)
    {
        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            double centerX = rect.X + rect.Width / 2.0;
            double centerY = rect.Y + rect.Height / 2.0;
            double radiusX = rect.Width / 2.0;
            double radiusY = rect.Height / 2.0;

            for (int i = 0; i < sides; i++)
            {
                double angle = startAngle + 2 * Math.PI * i / sides;
                double x = centerX + radiusX * Math.Cos(angle);
                double y = centerY + radiusY * Math.Sin(angle);
                Point pt = new Point(x, y);

                if (i == 0) ctx.BeginFigure(pt, true, true);
                else ctx.LineTo(pt, true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private Geometry BuildStarGeometry(Rect rect)
    {
        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            double centerX = rect.X + rect.Width / 2.0;
            double centerY = rect.Y + rect.Height / 2.0;
            double radiusX = rect.Width / 2.0;
            double radiusY = rect.Height / 2.0;
            double innerRadiusRatio = AppConsts.ShapeToolStarInnerRadiusRatio;

            int numPoints = 5;
            double angleStep = Math.PI / numPoints;
            double currentAngle = -Math.PI / 2;

            for (int i = 0; i < numPoints * 2; i++)
            {
                double rX = (i % 2 == 0) ? radiusX : radiusX * innerRadiusRatio;
                double rY = (i % 2 == 0) ? radiusY : radiusY * innerRadiusRatio;

                double x = centerX + rX * Math.Cos(currentAngle);
                double y = centerY + rY * Math.Sin(currentAngle);
                Point pt = new Point(x, y);

                if (i == 0) ctx.BeginFigure(pt, true, true);
                else ctx.LineTo(pt, true, false);

                currentAngle += angleStep;
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private Geometry BuildBubbleGeometry(Rect rect)
    {
        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            double radius = Math.Min(rect.Width, rect.Height) * AppConsts.ShapeToolBubbleRadiusRatio;
            double tailHeight = rect.Height * AppConsts.ShapeToolBubbleTailHeightRatio;
            double bodyHeight = rect.Height - tailHeight;
            Rect bodyRect = new Rect(rect.X, rect.Y, rect.Width, bodyHeight);
            Point tailStart = new Point(rect.X + rect.Width * AppConsts.ShapeToolBubbleTailStartRatio, rect.Y + bodyHeight);
            Point tailTip = new Point(rect.X + rect.Width * AppConsts.ShapeToolBubbleTailStartRatio, rect.Y + rect.Height);
            Point tailEnd = new Point(rect.X + rect.Width * AppConsts.ShapeToolBubbleTailEndRatio, rect.Y + bodyHeight);
            ctx.BeginFigure(new Point(bodyRect.X + radius, bodyRect.Y), true, true);
            ctx.LineTo(new Point(bodyRect.Right - radius, bodyRect.Top), true, true);
            ctx.ArcTo(new Point(bodyRect.Right, bodyRect.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
            ctx.LineTo(new Point(bodyRect.Right, bodyRect.Bottom - radius), true, true);
            ctx.ArcTo(new Point(bodyRect.Right - radius, bodyRect.Bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
            ctx.LineTo(tailStart, true, true);
            ctx.LineTo(tailTip, true, true);
            ctx.LineTo(tailEnd, true, true);
            ctx.LineTo(new Point(bodyRect.Left + radius, bodyRect.Bottom), true, true);
            ctx.ArcTo(new Point(bodyRect.Left, bodyRect.Bottom - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
            ctx.LineTo(new Point(bodyRect.Left, bodyRect.Top + radius), true, true);
            ctx.ArcTo(new Point(bodyRect.Left + radius, bodyRect.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
        }
        geometry.Freeze();
        return geometry;
    }
}