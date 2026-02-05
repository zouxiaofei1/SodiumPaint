using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabPaint.Core
{
    public static class IcoEncoder
    {
        public static void Save(BitmapSource source, List<int> sizes, Stream outputStream)
        {
            var imagesData = new List<byte[]>();
            sizes.Sort((a, b) => b.CompareTo(a));

            foreach (var size in sizes)
            {
                var resized = ResizeAndFit(source, size);
                var pngBytes = EncodeToPng(resized);
                imagesData.Add(pngBytes);
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

        private static byte[] EncodeToPng(BitmapSource bitmap)
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(ms);
            return ms.ToArray();
        }
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
