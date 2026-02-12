using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public class GradientTool : ToolBase
    {
        public override string Name => "Gradient";
        private Point _startPos;
        private bool _isDrawing = false;
        private byte[] _originalPixels;

        public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f)
        {
            if (ctx.ParentWindow.IsViewMode) return;

            _startPos = ctx.ToPixel(viewPos);
            _isDrawing = true;

            // Save state for preview
            int stride = ctx.Bitmap.BackBufferStride;
            int height = ctx.Bitmap.PixelHeight;
            _originalPixels = new byte[stride * height];
            ctx.Bitmap.CopyPixels(_originalPixels, stride, 0);

            ctx.CapturePointer();
            ctx.Undo.BeginStroke();
        }

        public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
        {
            if (!_isDrawing) return;

            Point currentPos = ctx.ToPixel(viewPos);
            RenderGradient(ctx, _startPos, currentPos, true);
        }

        public override void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f)
        {
            if (!_isDrawing) return;

            Point endPos = ctx.ToPixel(viewPos);
            RenderGradient(ctx, _startPos, endPos, false);

            ctx.Undo.AddDirtyRect(new Int32Rect(0, 0, ctx.Bitmap.PixelWidth, ctx.Bitmap.PixelHeight));
            ctx.Undo.CommitStroke();

            _isDrawing = false;
            _originalPixels = null;
            ctx.ReleasePointerCapture();
            ctx.IsDirty = true;
        }

        private void RenderGradient(ToolContext ctx, Point start, Point end, bool isPreview)
        {
            int width = ctx.Bitmap.PixelWidth;
            int height = ctx.Bitmap.PixelHeight;

            // Use WPF DrawingContext to render gradient to the WriteableBitmap
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                // Fill with original state first if preview
                if (isPreview && _originalPixels != null)
                {
                    // This is inefficient but works for preview. 
                    // Better approach: use a separate preview layer if available.
                    // But here we'll just draw the gradient over the entire canvas anyway.
                }

                var brush = new LinearGradientBrush(
                    ctx.ParentWindow.ForegroundColor,
                    ctx.ParentWindow.BackgroundColor,
                    start,
                    end);
                brush.MappingMode = BrushMappingMode.Absolute;
                brush.SpreadMethod = GradientSpreadMethod.Pad;

                dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            }

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            ctx.Bitmap.Lock();
            rtb.CopyPixels(new Int32Rect(0, 0, width, height), ctx.Bitmap.BackBuffer, ctx.Bitmap.BackBufferStride * height, ctx.Bitmap.BackBufferStride);
            ctx.Bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            ctx.Bitmap.Unlock();
        }

        public override void SetCursor(ToolContext ctx)
        {
            if (ctx.ParentWindow.IsViewMode) return;
            ctx.ViewElement.Cursor = Cursors.Cross;
        }

        public override void Cleanup(ToolContext ctx)
        {
            _isDrawing = false;
            _originalPixels = null;
        }
    }
}
