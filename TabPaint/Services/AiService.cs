using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    public class AiService
    {
        private const string BgRem_ModelUrl_HF = "https://huggingface.co/briaai/RMBG-1.4/resolve/main/onnx/model.onnx";
        private const string BgRem_ModelUrl_MS = "https://modelscope.cn/models/AI-ModelScope/RMBG-1.4/resolve/master/onnx/model.onnx";
        private const string BgRem_ModelName = "rmbg-1.4.onnx";
        private const string ExpectedMD5 = "8bb9b16ff49cda31e7784852873cfd0d"; 

        private readonly string _cacheDir;

        private const string Sr_ModelUrl_HF = "https://modelscope.cn/models/AXERA-TECH/Real-ESRGAN/resolve/master/onnx/realesrgan-x4-256.onnx";
        // 备用源 (由于 github release 国内下载慢，建议替换为你自己的 OSS 地址或者国内镜像)
        private const string Sr_ModelUrl_Mirror = "https://modelscope.cn/models/AXERA-TECH/Real-ESRGAN/resolve/master/onnx/realesrgan-x4-256.onnx";
        private const string Sr_ModelName = "realesrgan-x4plus.onnx";
        private const int TileSize = 256; // 切块大小，越小内存占用越低，但推理次数越多
        private const int TileOverlap = 16; // 重叠区域，防止拼接缝隙
        private const int ScaleFactor = 4; 


        public AiService(string cacheDir)
        {
            _cacheDir = cacheDir;
            if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
        }
        public enum AiTaskType { RemoveBackground, SuperResolution }

        public async Task<string> PrepareModelAsync(AiTaskType taskType, IProgress<double> progress)
        {
            // --- 配置部分 ---
            string modelName = taskType == AiTaskType.RemoveBackground ? BgRem_ModelName : Sr_ModelName;

            // 背景移除模型 (RMBG-1.4) 的 MD5
            string expectedMd5_Bg = "8bb9b16ff49cda31e7784852873cfd0d";
            string expectedMd5_Sr = "25C354305A32B59300A610BCD7846977";

            string expectedMd5 = taskType == AiTaskType.RemoveBackground ? expectedMd5_Bg : expectedMd5_Sr;

            string urlMain = taskType == AiTaskType.RemoveBackground ? BgRem_ModelUrl_HF : Sr_ModelUrl_HF;
            string urlMirror = taskType == AiTaskType.RemoveBackground ? BgRem_ModelUrl_MS : Sr_ModelUrl_Mirror;

            // 简单的本地化策略：中文环境优先使用镜像源
            bool preferMirror = CultureInfo.CurrentCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            string primaryUrl = preferMirror ? urlMirror : urlMain;
            string secondaryUrl = preferMirror ? urlMain : urlMirror;

            string finalPath = Path.Combine(_cacheDir, modelName);

            // 1. 检查已存在的文件
            if (File.Exists(finalPath))
            {
                if(expectedMd5==null )return finalPath; // 未提供MD5，直接返回已存在文件（开发阶段用）
                // 验证现有文件完整性
                if (await VerifyMd5Async(finalPath, expectedMd5))
                {
                    return finalPath; // 文件存在且校验通过
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AI] MD5 mismatch for existing file. Deleting {modelName}...");
                    try { File.Delete(finalPath); } catch { } // 校验失败，删除坏文件，准备重新下载
                }
            }

            // 2. 下载逻辑
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "TabPaint-Client/1.0");
                client.Timeout = TimeSpan.FromMinutes(20); // 大模型下载给予充足时间

                // 尝试主链接
                if (!await DownloadAndValidateAsync(client, primaryUrl, finalPath, expectedMd5, progress))
                {
                    System.Diagnostics.Debug.WriteLine("[AI] Primary source failed. Trying secondary...");
                    // 失败则尝试备用链接
                    if (!await DownloadAndValidateAsync(client, secondaryUrl, finalPath, expectedMd5, progress))
                    {
                        throw new Exception(string.Format(LocalizationManager.GetString("L_AI_Error_DownloadFailed"), modelName));
                    }
                }
            }

            return finalPath;
        }

        private async Task<bool> DownloadAndValidateAsync(HttpClient client, string url, string destPath, string expectedMd5, IProgress<double> progress)
        {
            string tempPath = destPath + ".tmp"; // 使用临时文件

            try
            {
                // 如果有残留的临时文件，先删除
                if (File.Exists(tempPath)) File.Delete(tempPath);

                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode) return false;

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var totalRead = 0L;
                        var buffer = new byte[8192]; // 8KB buffer
                        var isMoreToRead = true;
                        int lastReportedPercent = -1;

                        do
                        {
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fileStream.WriteAsync(buffer, 0, read);
                                totalRead += read;

                                if (totalBytes != -1 && progress != null)
                                {
                                    int percent = (int)((double)totalRead / totalBytes * 100);
                                    // 减少 UI 更新频率，每 1% 更新一次即可
                                    if (percent > lastReportedPercent)
                                    {
                                        progress.Report((double)percent);
                                        lastReportedPercent = percent;
                                    }
                                }
                            }
                        } while (isMoreToRead);
                    }
                }

                // 下载完成，开始校验
                if (await VerifyMd5Async(tempPath, expectedMd5))
                {
                    if (File.Exists(destPath)) File.Delete(destPath);
                    File.Move(tempPath, destPath); // 原子操作：重命名
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AI] Downloaded file MD5 mismatch.");
                    File.Delete(tempPath); // 校验失败，删除临时文件
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI] Download error: {ex.Message}");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                return false;
            }
        }

        private async Task<bool> VerifyMd5Async(string filePath, string expectedMd5)
        {
            if (string.IsNullOrEmpty(expectedMd5)) return true; // 如果未提供MD5，默认跳过校验（开发阶段用）
            if (!File.Exists(filePath)) return false;

            return await Task.Run(() =>
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    var hashStr = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                    // System.Diagnostics.Debug.WriteLine($"File: {Path.GetFileName(filePath)}, Hash: {hashStr}, Expected: {expectedMd5}");

                    return hashStr.Equals(expectedMd5.ToLowerInvariant());
                }
            });
        }


        public async Task<byte[]> RunInferenceAsync(string modelPath, WriteableBitmap originalBmp)
        {
            int targetW = 1024;
            int targetH = 1024;
            var resized = new TransformedBitmap(originalBmp,
                new ScaleTransform((double)targetW / originalBmp.PixelWidth, (double)targetH / originalBmp.PixelHeight));
            var wb = new WriteableBitmap(resized);
            int inputStride = wb.BackBufferStride;
            byte[] inputPixels = new byte[targetH * inputStride];
            wb.CopyPixels(inputPixels, inputStride, 0);

            // B. 准备原图数据 (用于最后合成) - UI 线程
            int origW = originalBmp.PixelWidth;
            int origH = originalBmp.PixelHeight;
            int origStride = originalBmp.BackBufferStride;
            byte[] originalPixels = new byte[origH * origStride];
            originalBmp.CopyPixels(originalPixels, origStride, 0); // 必须在主线程读原图

            // C. 后台线程
            return await Task.Run(() =>
            {
                using var session = new InferenceSession(modelPath);

                // 转换 Tensor
                var tensor = PreprocessPixelsToTensor(inputPixels, targetW, targetH, inputStride);

                // 推理
                string inputName = session.InputMetadata.Keys.First();
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };
                using var results = session.Run(inputs);

                // 后处理 (传入原图的 byte[] 数据，而不是 Bitmap 对象)
                var outputTensor = results.First().AsTensor<float>();
                return PostProcess(outputTensor, originalPixels, origW, origH, origStride);
            });
        }
        public async Task<WriteableBitmap> RunSuperResolutionAsync(string modelPath, WriteableBitmap inputBitmap, IProgress<double> progress)
        {
            // 1. 获取原图数据
            int w = inputBitmap.PixelWidth;
            int h = inputBitmap.PixelHeight;
            int stride = inputBitmap.BackBufferStride;
            byte[] inputPixels = new byte[h * stride];
            inputBitmap.CopyPixels(inputPixels, stride, 0);

            // 2. 准备输出
            int outW = w * ScaleFactor;
            int outH = h * ScaleFactor;
            var outputBitmap = new WriteableBitmap(outW, outH, 96, 96, PixelFormats.Bgra32, null);
            int outStride = outputBitmap.BackBufferStride;
            byte[] outputPixels = new byte[outH * outStride];

            // 3. 运行推理 (后台线程)
            await Task.Run(() =>
            {
                var sessionOptions = new SessionOptions();
                try
                {
                    sessionOptions.AppendExecutionProvider_DML(0);
                }
                catch
                {
                    // 回退 CPU
                    sessionOptions.AppendExecutionProvider_CPU();
                }

                using var session = new InferenceSession(modelPath, sessionOptions);

                // 计算分块
                int tilesX = (int)Math.Ceiling((double)w / TileSize);
                int tilesY = (int)Math.Ceiling((double)h / TileSize);
                int totalTiles = tilesX * tilesY;
                int processedTiles = 0;

                for (int y = 0; y < h; y += TileSize)
                {
                    for (int x = 0; x < w; x += TileSize)
                    {
                        // 1. 计算有效区域 (Valid Region)
                        // 比如在边缘时，validW 可能是 48
                        int validW = Math.Min(TileSize, w - x);
                        int validH = Math.Min(TileSize, h - y);

                        // 2. 提取 Tensor (关键修改：传入固定的 TileSize)
                        // 我们需要告诉函数：给我一个 TileSize 大小的容器，但只填入 validW 大小的数据
                        var tileTensor = ExtractTileToTensor(inputPixels, x, y, validW, validH, TileSize, stride, w, h);

                        // 3. 推理 (此时输入的 tensor 永远是 [1, 3, TileSize, TileSize])
                        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), tileTensor) };
                        using var results = session.Run(inputs);
                        var outputTensor = results.First().AsTensor<float>();

                        // 4. 写回结果 (关键修改：传入 validW, validH)
                        // 告诉函数：虽然 tensor 很大，但我只取 validW * ScaleFactor 这一部分
                        WriteTensorToPixels(outputTensor, outputPixels, x * ScaleFactor, y * ScaleFactor, validW, validH, outW, outH, outStride);

                        processedTiles++;
                        progress?.Report((double)processedTiles / totalTiles * 100);
                    }
                }

            });

            // 4. 将像素写回 Bitmap
            outputBitmap.WritePixels(new Int32Rect(0, 0, outW, outH), outputPixels, outStride, 0);
            return outputBitmap;
        }
        private DenseTensor<float> ExtractTileToTensor(byte[] pixels, int startX, int startY, int validW, int validH, int fixedSize, int stride, int fullW, int fullH)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, fixedSize, fixedSize });
            Parallel.For(0, validH, y =>
            {
                int srcY = startY + y;
                // 安全检查
                if (srcY >= fullH) return;

                for (int x = 0; x < validW; x++)
                {
                    int srcX = startX + x;
                    if (srcX >= fullW) continue;

                    int offset = srcY * stride + srcX * 4;

                    // BGRA -> RGB Normalized
                    float b = pixels[offset + 0] / 255.0f;
                    float g = pixels[offset + 1] / 255.0f;
                    float r = pixels[offset + 2] / 255.0f;

                    // 填入 Tensor
                    tensor[0, 0, y, x] = r;
                    tensor[0, 1, y, x] = g;
                    tensor[0, 2, y, x] = b;
                }
            });
            return tensor;
        }

        private void WriteTensorToPixels(Tensor<float> tensor, byte[] outPixels, int destX, int destY, int validW, int validH, int fullW, int fullH, int stride)
        {
            // 计算放大后的有效区域
            int validTargetH = validH * ScaleFactor;
            int validTargetW = validW * ScaleFactor;

            // 只循环有效区域，Tensor 中多余的 Padding 部分直接忽略
            Parallel.For(0, validTargetH, y =>
            {
                int finalY = destY + y;
                if (finalY >= fullH) return;

                for (int x = 0; x < validTargetW; x++)
                {
                    int finalX = destX + x;
                    if (finalX >= fullW) continue;

                    // RGB -> BGRA
                    float r = tensor[0, 0, y, x];
                    float g = tensor[0, 1, y, x];
                    float b = tensor[0, 2, y, x];

                    // Clamp 0-1
                    r = Math.Clamp(r, 0f, 1f);
                    g = Math.Clamp(g, 0f, 1f);
                    b = Math.Clamp(b, 0f, 1f);

                    int offset = finalY * stride + finalX * 4;

                    outPixels[offset + 0] = (byte)(b * 255);
                    outPixels[offset + 1] = (byte)(g * 255);
                    outPixels[offset + 2] = (byte)(r * 255);
                    outPixels[offset + 3] = 255;
                }
            });
        }

        private DenseTensor<float> PreprocessPixelsToTensor(byte[] pixels, int w, int h, int stride)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });

            // 使用 Parallel 加速纯数据循环
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int offset = y * stride + x * 4;
                    if (offset + 2 >= pixels.Length) continue;

                    // WPF 是 BGRA 顺序
                    float b = pixels[offset + 0] / 255.0f;
                    float g = pixels[offset + 1] / 255.0f;
                    float r = pixels[offset + 2] / 255.0f;

                    // 标准化 (0-1)
                    tensor[0, 0, y, x] = r;
                    tensor[0, 1, y, x] = g;
                    tensor[0, 2, y, x] = b;
                }
            });

            return tensor;
        }

        private DenseTensor<float> Preprocess(WriteableBitmap bmp, int targetW, int targetH)
        {
            // 高质量缩放
            var resized = new TransformedBitmap(bmp, new ScaleTransform((double)targetW / bmp.PixelWidth, (double)targetH / bmp.PixelHeight));
            var wb = new WriteableBitmap(resized);

            var tensor = new DenseTensor<float>(new[] { 1, 3, targetH, targetW });
            int stride = wb.BackBufferStride;

            wb.Lock();
            unsafe
            {
                byte* ptr = (byte*)wb.BackBuffer;

                // 使用 Parallel 加速预处理循环
                Parallel.For(0, targetH, y =>
                {
                    for (int x = 0; x < targetW; x++)
                    {
                        int offset = y * stride + x * 4;
                        float b = ptr[offset + 0] / 255.0f;
                        float g = ptr[offset + 1] / 255.0f;
                        float r = ptr[offset + 2] / 255.0f;

                        tensor[0, 0, y, x] = r;
                        tensor[0, 1, y, x] = g;
                        tensor[0, 2, y, x] = b;
                    }
                });
            }
            wb.Unlock();
            return tensor;
        }

        private byte[] PostProcess(Tensor<float> maskTensor, byte[] originalPixels, int w, int h, int stride)
        {
            // 创建结果数组，先复制原图内容
            byte[] resultPixels = new byte[originalPixels.Length];
            Array.Copy(originalPixels, resultPixels, originalPixels.Length);

            int maskW = maskTensor.Dimensions[3];
            int maskH = maskTensor.Dimensions[2];

            Parallel.For(0, h, y =>
            {
                int maskY = (int)((float)y / h * maskH);
                if (maskY >= maskH) maskY = maskH - 1;

                for (int x = 0; x < w; x++)
                {
                    int maskX = (int)((float)x / w * maskW);
                    if (maskX >= maskW) maskX = maskW - 1;

                    float alphaVal = maskTensor[0, 0, maskY, maskX];

                    int offset = y * stride + x * 4;

                    if (offset + 3 < resultPixels.Length)
                    {
                        byte originalAlpha = resultPixels[offset + 3];
                        // 混合 Alpha
                        resultPixels[offset + 3] = (byte)(originalAlpha * Math.Clamp(alphaVal, 0, 1));
                    }
                }
            });

            return resultPixels;
        }
    }
}
