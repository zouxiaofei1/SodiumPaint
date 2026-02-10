using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace TabPaint.Core
{
    public static class IcoEncoder
    {
        public static void Save(BitmapSource source, List<int> sizes, Stream outputStream)
        {
            var imagesData = new List<byte[]>();
            sizes.Sort((a, b) => b.CompareTo(a));

            // 1. 将源图转换为 SkiaBitmap (一次转换，多次缩放)
            using var skSrc = new SKBitmap(source.PixelWidth, source.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            source.CopyPixels(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight), 
                skSrc.GetPixels(), 
                source.PixelHeight * (source.PixelWidth * 4), 
                source.PixelWidth * 4);

            foreach (var size in sizes)
            {
                // 2. 使用 SkiaSharp 高效缩放并保持比例居中
                using var skDest = ResizeAndFitSkia(skSrc, size);
                using var image = SKImage.FromBitmap(skDest);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                imagesData.Add(data.ToArray());
            }

            var writer = new BinaryWriter(outputStream);
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)sizes.Count);
            int offset = 6 + (16 * sizes.Count); // 数据开始的位置 = Header(6) + Entries(16 * N)

            for (int i = 0; i < sizes.Count; i++)
            {
                var size = sizes[i];
                var dataLength = imagesData[i].Length;
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((short)1);
                writer.Write((short)32);
                writer.Write(dataLength);
                writer.Write(offset);
                offset += dataLength;
            }
            foreach (var data in imagesData)
            {
                writer.Write(data);
            }

            writer.Flush();
        }

        private static SKBitmap ResizeAndFitSkia(SKBitmap source, int targetSize)
        {
            var dest = new SKBitmap(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(dest);
            canvas.Clear(SKColors.Transparent);

            float ratio = Math.Min((float)targetSize / source.Width, (float)targetSize / source.Height);
            float newWidth = source.Width * ratio;
            float newHeight = source.Height * ratio;
            float x = (targetSize - newWidth) / 2;
            float y = (targetSize - newHeight) / 2;

            var destRect = new SKRect(x, y, x + newWidth, y + newHeight);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
            canvas.DrawBitmap(source, destRect, paint);
            
            return dest;
        }

        [Obsolete("Use ResizeAndFitSkia for better performance")]
        private static BitmapSource ResizeAndFit(BitmapSource source, int targetSize)
        {
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                double ratio = Math.Min((double)targetSize / source.PixelWidth, (double)targetSize / source.PixelHeight);
                double newWidth = source.PixelWidth * ratio;
                double newHeight = source.PixelHeight * ratio;
                double x = (targetSize - newWidth) / 2;
                double y = (targetSize - newHeight) / 2;
                dc.DrawImage(source, new Rect(x, y, newWidth, newHeight));
            }

            var renderBitmap = new RenderTargetBitmap(targetSize, targetSize, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            if (renderBitmap.CanFreeze) renderBitmap.Freeze();
            return renderBitmap;
        }
    }
}
