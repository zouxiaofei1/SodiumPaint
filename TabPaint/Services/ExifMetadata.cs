
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

//
//图片加载队列机制
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private Task<string> GetImageMetadataInfoAsync(byte[] imageBytes, string filePath, BitmapImage bitmap)
        {
            return Task.Run(() =>
            {
                try
                {
                    StringBuilder sb = new StringBuilder();

                    // --- 1. 文件与画布信息 ---
                    sb.AppendLine("[文件信息]");
                    sb.AppendLine($"路径: {filePath}");
                    sb.AppendLine($"大小: {(imageBytes.Length / 1024.0 / 1024.0):F2} MB");
                    sb.AppendLine();

                    sb.AppendLine("[画布信息]");
                    sb.AppendLine($"尺寸: {bitmap.PixelWidth} × {bitmap.PixelHeight} px");
                    // 【新增】位深度读取
                    sb.AppendLine($"位深: {bitmap.Format.BitsPerPixel} bit");
                    sb.AppendLine($"DPI: {bitmap.DpiX:F0} × {bitmap.DpiY:F0}");
                    sb.AppendLine($"格式: {bitmap.Format}");

                    // --- 2. 尝试读取 EXIF 元数据 ---
                    using var ms = new MemoryStream(imageBytes);
                    // 这里使用 BitmapCreateOptions.PreservePixelFormat 以尽可能保留原始信息，虽不影响 Metadata 读取
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);

                    if (decoder.Frames[0].Metadata is BitmapMetadata metadata)
                    {
                        StringBuilder exifSb = new StringBuilder();

                        // 设备信息
                        string device = "";
                        if (!string.IsNullOrEmpty(metadata.CameraManufacturer)) device += metadata.CameraManufacturer + " ";
                        if (!string.IsNullOrEmpty(metadata.CameraModel)) device += metadata.CameraModel;
                        if (!string.IsNullOrEmpty(device)) exifSb.AppendLine($"设备: {device.Trim()}");

                        // --- 核心摄影参数 (修复数值解析) ---

                        // 1. 曝光时间 (ExposureTime) - ID: 33434
                        // 逻辑: 如果 < 1秒，显示为分数 (1/100)，否则显示为秒 (1.5s)
                        var expVal = TryGetQuery(metadata, "/app1/ifd/exif/{uint=33434}");
                        if (expVal != null)
                        {
                            double seconds = ParseUnsignedRational(expVal);
                            if (seconds > 0)
                            {
                                if (seconds < 1.0)
                                    exifSb.AppendLine($"曝光时间: 1/{Math.Round(1.0 / seconds)} 秒");
                                else
                                    exifSb.AppendLine($"曝光时间: {seconds} 秒");
                            }
                        }

                        // 2. 光圈值 (FNumber) - ID: 33437
                        var fVal = TryGetQuery(metadata, "/app1/ifd/exif/{uint=33437}");
                        if (fVal != null)
                        {
                            double fNum = ParseUnsignedRational(fVal);
                            if (fNum > 0) exifSb.AppendLine($"光圈值: f/{fNum:0.0}"); // 保留1位小数，如 f/1.8
                        }

                        // 3. ISO 速度 - ID: 34855 (通常直接是 Short/UShort)
                        var iso = TryGetQuery(metadata, "/app1/ifd/exif/{uint=34855}");
                        if (iso != null) exifSb.AppendLine($"ISO速度: ISO-{iso}");

                        // 4. 曝光补偿 (ExposureBiasValue) - ID: 37380
                        // 注意：这是有符号分数 (SRATIONAL)，可以为负
                        var biasVal = TryGetQuery(metadata, "/app1/ifd/exif/{uint=37380}");
                        if (biasVal != null)
                        {
                            double bias = ParseSignedRational(biasVal);
                            // 格式化为 +0.3 / -1.0 / 0
                            string biasStr = bias == 0 ? "0" : (bias > 0 ? $"+{bias:0.##}" : $"{bias:0.##}");
                            exifSb.AppendLine($"曝光补偿: {biasStr} EV");
                        }

                        // 5. 焦距 (FocalLength) - ID: 37386
                        var focalVal = TryGetQuery(metadata, "/app1/ifd/exif/{uint=37386}");
                        if (focalVal != null)
                        {
                            double focal = ParseUnsignedRational(focalVal);
                            exifSb.AppendLine($"焦距: {focal} mm");
                        }

                        // 6. 35mm等效焦距 - ID: 41989 (通常是 Short)
                        var focal35 = TryGetQuery(metadata, "/app1/ifd/exif/{uint=41989}");
                        if (focal35 != null) exifSb.AppendLine($"35mm焦距: {focal35} mm");

                        // 测光模式
                        var meter = TryGetQuery(metadata, "/app1/ifd/exif/{uint=37383}");
                        if (meter != null) exifSb.AppendLine($"测光模式: {MapMeteringMode(meter)}");

                        // 闪光灯
                        var flash = TryGetQuery(metadata, "/app1/ifd/exif/{uint=37385}");
                        if (flash != null) exifSb.AppendLine($"闪光灯: {((Convert.ToInt32(flash) & 1) == 1 ? "开启" : "关闭")}");

                        // 软件/后期
                        if (!string.IsNullOrEmpty(metadata.ApplicationName)) exifSb.AppendLine($"处理软件: {metadata.ApplicationName}");
                        if (!string.IsNullOrEmpty(metadata.DateTaken)) exifSb.AppendLine($"拍摄日期: {metadata.DateTaken}");

                        // 镜头信息
                        var lens = TryGetQuery(metadata, "/app1/ifd/exif/{uint=42036}");
                        if (lens != null) exifSb.AppendLine($"镜头: {lens}");

                        // 合并
                        if (exifSb.Length > 0)
                        {
                            sb.AppendLine();
                            sb.AppendLine("[照片元数据]");
                            sb.Append(exifSb.ToString());
                        }
                    }
                    return sb.ToString().TrimEnd();
                }
                catch (Exception ex)
                {
                    return "无法解析详细信息: " + ex.Message;
                }
            });
        }

        // --- 辅助方法区 ---

        private object TryGetQuery(BitmapMetadata metadata, string query)
        {
            try { if (metadata.ContainsQuery(query)) return metadata.GetQuery(query); } catch { }
            return null;
        }

        // 解析无符号分数 (RATIONAL)
        private double ParseUnsignedRational(object value)
        {
            if (value == null) return 0;

            // 情况1: WPF 已经将其解析为 ulong (也就是那串巨大的数字)
            if (value is ulong raw)
            {
                uint numerator = (uint)(raw & 0xFFFFFFFF); // 低32位是分子
                uint denominator = (uint)(raw >> 32);      // 高32位是分母
                return denominator == 0 ? 0 : (double)numerator / denominator;
            }
            // 情况2: 有时是 long
            if (value is long rawLong)
            {
                uint numerator = (uint)(rawLong & 0xFFFFFFFF);
                uint denominator = (uint)(rawLong >> 32);
                return denominator == 0 ? 0 : (double)numerator / denominator;
            }
            // 情况3: 某些解码器直接返回 double
            if (value is double d) return d;

            return 0;
        }

        // 解析有符号分数 (SRATIONAL) - 用于曝光补偿
        private double ParseSignedRational(object value)
        {
            if (value == null) return 0;

            // 逻辑与上面类似，但是要处理符号
            if (value is long raw)
            {
                int numerator = (int)(raw & 0xFFFFFFFF);
                int denominator = (int)(raw >> 32);
                return denominator == 0 ? 0 : (double)numerator / denominator;
            }
            if (value is ulong rawU)
            {
                // 强转回 int
                int numerator = (int)(rawU & 0xFFFFFFFF);
                int denominator = (int)(rawU >> 32);
                return denominator == 0 ? 0 : (double)numerator / denominator;
            }
            return 0;
        }

        private string MapMeteringMode(object val)
        {
            int code = Convert.ToInt32(val);
            return code switch
            {
                0 => "未知",
                1 => "平均测光",
                2 => "中央重点平均测光",
                3 => "点测光",
                4 => "多点测光",
                5 => "模式测光",
                6 => "部分测光",
                255 => "其他",
                _ => "未知"
            };
        }

    }
}