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
            // 1. 准备不同尺寸的 PNG 数据
            var imagesData = new List<byte[]>();

            // 确保尺寸排序（大尺寸在前还是后不影响文件结构，但通常为了元数据整洁）
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

            // 3. 写入 Icon Directory Entries
            int offset = 6 + (16 * sizes.Count); // 数据开始的位置 = Header(6) + Entries(16 * N)

            for (int i = 0; i < sizes.Count; i++)
            {
                var size = sizes[i];
                var dataLength = imagesData[i].Length;

                // Width (1 byte): 0 means 256
                writer.Write((byte)(size == 256 ? 0 : size));
                // Height (1 byte)
                writer.Write((byte)(size == 256 ? 0 : size));
                // ColorCount (1 byte): 0 if >= 8bpp
                writer.Write((byte)0);
                // Reserved (1 byte)
                writer.Write((byte)0);
                // Planes (2 bytes): 1
                writer.Write((short)1);
                // BitCount (2 bytes): 32 (RGBA)
                writer.Write((short)32);
                // BytesInRes (4 bytes): size of image data
                writer.Write(dataLength);
                // ImageOffset (4 bytes)
                writer.Write(offset);

                offset += dataLength;
            }

            // 4. 写入实际的 PNG 图片数据
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

        // 核心：非矩形图片长边缩短，周围透明填充
        private static BitmapSource ResizeAndFit(BitmapSource source, int targetSize)
        {
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                // 计算比例
                double ratio = Math.Min((double)targetSize / source.PixelWidth, (double)targetSize / source.PixelHeight);
                double newWidth = source.PixelWidth * ratio;
                double newHeight = source.PixelHeight * ratio;

                // 计算居中偏移
                double x = (targetSize - newWidth) / 2;
                double y = (targetSize - newHeight) / 2;
                dc.DrawImage(source, new Rect(x, y, newWidth, newHeight));
            }

            var renderBitmap = new RenderTargetBitmap(targetSize, targetSize, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);

            // 冻结以跨线程使用
            if (renderBitmap.CanFreeze) renderBitmap.Freeze();
            return renderBitmap;
        }
    }
}
