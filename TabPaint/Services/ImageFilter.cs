
using System.ComponentModel;

using System.Windows;


//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void ProcessMosaic(byte[] pixels, int width, int height, int stride, int blockSize)
        {
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    IntPtr dataPtr = (IntPtr)ptr;    // 转换为 IntPtr 以便在 Lambda 中捕获
                    Parallel.For(0, (height + blockSize - 1) / blockSize, by =>
                    {
                        byte* basePtr = (byte*)dataPtr;

                        int yStart = by * blockSize;
                        int yEnd = Math.Min(yStart + blockSize, height);

                        for (int bx = 0; bx < (width + blockSize - 1) / blockSize; bx++)
                        {
                            int xStart = bx * blockSize;
                            int xEnd = Math.Min(xStart + blockSize, width);

                            long sumB = 0, sumG = 0, sumR = 0;
                            int count = 0;

                            for (int y = yStart; y < yEnd; y++)
                            {
                                byte* row = basePtr + y * stride;
                                for (int x = xStart; x < xEnd; x++)
                                {
                                    int offset = x * 4;
                                    sumB += row[offset];
                                    sumG += row[offset + 1];
                                    sumR += row[offset + 2];
                                    count++;
                                }
                            }
                            if (count == 0) continue;
                            byte avgB = (byte)(sumB / count);
                            byte avgG = (byte)(sumG / count);
                            byte avgR = (byte)(sumR / count);

                            for (int y = yStart; y < yEnd; y++)
                            {
                                byte* row = basePtr + y * stride;
                                for (int x = xStart; x < xEnd; x++)
                                {
                                    int offset = x * 4;
                                    row[offset] = avgB;
                                    row[offset + 1] = avgG;
                                    row[offset + 2] = avgR;
                                }
                            }
                        }
                    });
                }
            }
        }
        private void ProcessGaussianBlur(byte[] pixels, int width, int height, int stride, int radius)
        {  // 5. 实现 Gaussian Blur 算法 (使用分离卷积以提高性能)
            int kernelSize = radius * 2 + 1;
            double[] kernel = new double[kernelSize];
            double sigma = radius / 2.0;
            if (sigma < 1) sigma = 1;
            double twoSigmaSquare = 2.0 * sigma * sigma;
            double sum = 0.0;
            for (int i = 0; i < kernelSize; i++)
            {
                double x = i - radius;
                kernel[i] = Math.Exp(-(x * x) / twoSigmaSquare);
                sum += kernel[i];
            }
            for (int i = 0; i < kernelSize; i++) kernel[i] /= sum;

            byte[] tempPixels = new byte[pixels.Length];
            Array.Copy(pixels, tempPixels, pixels.Length);
            unsafe
            {
                fixed (byte* srcPtr = tempPixels)    // --- 第一步：水平模糊 ---
                fixed (byte* destPtr = pixels)
                fixed (double* kPtr = kernel)
                {
                    IntPtr sAddr = (IntPtr)srcPtr;
                    IntPtr dAddr = (IntPtr)destPtr;
                    IntPtr kAddr = (IntPtr)kPtr;

                    Parallel.For(0, height, y =>
                    {
                        byte* sP = (byte*)sAddr;
                        byte* dP = (byte*)dAddr;
                        double* kernelP = (double*)kAddr;

                        byte* srcRow = sP + y * stride;
                        byte* destRow = dP + y * stride;

                        for (int x = 0; x < width; x++)
                        {
                            double r = 0, g = 0, b = 0;
                            for (int k = -radius; k <= radius; k++)
                            {
                                int px = x + k;
                                if (px < 0) px = 0; else if (px >= width) px = width - 1;
                                int off = px * 4;
                                double weight = kernelP[k + radius];
                                b += srcRow[off] * weight;
                                g += srcRow[off + 1] * weight;
                                r += srcRow[off + 2] * weight;
                            }
                            destRow[x * 4] = (byte)b;
                            destRow[x * 4 + 1] = (byte)g;
                            destRow[x * 4 + 2] = (byte)r;
                            destRow[x * 4 + 3] = srcRow[x * 4 + 3];
                        }
                    });
                }
                Array.Copy(pixels, tempPixels, pixels.Length);     // 同步中间结果
                fixed (byte* srcPtr = tempPixels)    // --- 第二步：垂直模糊 ---
                fixed (byte* destPtr = pixels)
                fixed (double* kPtr = kernel)
                {
                    IntPtr sAddr = (IntPtr)srcPtr;
                    IntPtr dAddr = (IntPtr)destPtr;
                    IntPtr kAddr = (IntPtr)kPtr;

                    Parallel.For(0, width, x =>
                    {
                        byte* sP = (byte*)sAddr;
                        byte* dP = (byte*)dAddr;
                        double* kernelP = (double*)kAddr;

                        for (int y = 0; y < height; y++)
                        {
                            double r = 0, g = 0, b = 0;
                            for (int k = -radius; k <= radius; k++)
                            {
                                int py = y + k;
                                if (py < 0) py = 0; else if (py >= height) py = height - 1;
                                int off = py * stride + x * 4;
                                double weight = kernelP[k + radius];
                                b += sP[off] * weight;
                                g += sP[off + 1] * weight;
                                r += sP[off + 2] * weight;
                            }
                            int destOff = y * stride + x * 4;
                            dP[destOff] = (byte)b;
                            dP[destOff + 1] = (byte)g;
                            dP[destOff + 2] = (byte)r;
                            dP[destOff + 3] = sP[destOff + 3];
                        }
                    });
                }
            }
        }
        private void ProcessBrown(byte[] pixels, int width, int height, int stride)
        {
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    IntPtr ptrHandle = (IntPtr)ptr;
                    Parallel.For(0, height, y =>
                    {
                        byte* row = (byte*)ptrHandle + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            byte b = row[x * 4];
                            byte g = row[x * 4 + 1];
                            byte r = row[x * 4 + 2];
                            double gray = r * AppConsts.GrayWeightR + g * AppConsts.GrayWeightG + b * AppConsts.GrayWeightB;
                            double newR = gray + AppConsts.FilterBrownRAddition;
                            double newG = gray + AppConsts.FilterBrownGAddition;
                            double newB = gray + AppConsts.FilterBrownBAddition;

                            row[x * 4 + 2] = (byte)Math.Clamp(newR, 0, 255); // R
                            row[x * 4 + 1] = (byte)Math.Clamp(newG, 0, 255); // G
                            row[x * 4] = (byte)Math.Clamp(newB, 0, 255); // B
                        }
                    });
                }
            }
        }

        private void ProcessSharpen(byte[] pixels, int width, int height, int stride)
        {
            byte[] srcPixels = (byte[])pixels.Clone();

            unsafe
            {
                fixed (byte* destPtr = pixels)
                fixed (byte* srcPtr = srcPixels)
                {
                    IntPtr destHandle = (IntPtr)destPtr;
                    IntPtr srcHandle = (IntPtr)srcPtr;

                    Parallel.For(1, height - 1, y => // 跳过边缘行
                    {
                        byte* pSrc = (byte*)srcHandle;
                        byte* pDestRow = (byte*)destHandle + y * stride;

                        for (int x = 1; x < width - 1; x++) // 跳过边缘列
                        {
                            int offset = y * stride + x * 4;
                            int sumB = 0, sumG = 0, sumR = 0;
                            for (int ky = -1; ky <= 1; ky++)
                            {
                                for (int kx = -1; kx <= 1; kx++)
                                {
                                    int neighborOffset = (y + ky) * stride + (x + kx) * 4;
                                    int kernelVal = 0; 
                                    if((ky == 0 && kx == 0)) kernelVal= 5;
                                    if(ky == 0 && (kx == -1 || kx ==1)) kernelVal= -1;
                                    else if(kx ==0 && (ky == -1 || ky ==1)) kernelVal= -1;
                                    sumB += pSrc[neighborOffset] * kernelVal;
                                    sumG += pSrc[neighborOffset + 1] * kernelVal;
                                    sumR += pSrc[neighborOffset + 2] * kernelVal;
                                }
                            }

                            // 写入结果并截断到 0-255
                            pDestRow[x * 4] = (byte)Math.Clamp(sumB, 0, 255);
                            pDestRow[x * 4 + 1] = (byte)Math.Clamp(sumG, 0, 255);
                            pDestRow[x * 4 + 2] = (byte)Math.Clamp(sumR, 0, 255);
                            // Alpha 保持原样
                            pDestRow[x * 4 + 3] = pSrc[offset + 3];
                        }
                    });
                }
            }
        }
        private void ProcessSepia(byte[] pixels, int width, int height, int stride)
        {
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    IntPtr ptrHandle = (IntPtr)ptr;
                    Parallel.For(0, height, y =>
                    {
                        byte* row = (byte*)ptrHandle + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int b = row[x * 4];
                            int g = row[x * 4 + 1];
                            int r = row[x * 4 + 2];

                            int tr = (int)(AppConsts.SepiaR1 * r + AppConsts.SepiaR2 * g + AppConsts.SepiaR3 * b);
                            int tg = (int)(AppConsts.SepiaG1 * r + AppConsts.SepiaG2 * g + AppConsts.SepiaG3 * b);
                            int tb = (int)(AppConsts.SepiaB1 * r + AppConsts.SepiaB2 * g + AppConsts.SepiaB3 * b);

                            row[x * 4 + 2] = (byte)Math.Min(255, tr);
                            row[x * 4 + 1] = (byte)Math.Min(255, tg);
                            row[x * 4] = (byte)Math.Min(255, tb);
                        }
                    });
                }
            }
        }


        private void ProcessVignette(byte[] pixels, int width, int height, int stride)
        {
            double centerX = width / 2.0;
            double centerY = height / 2.0;
            double maxDist = Math.Sqrt(centerX * centerX + centerY * centerY);

            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    IntPtr ptrHandle = (IntPtr)ptr;
                    Parallel.For(0, height, y =>
                    {
                        byte* row = (byte*)ptrHandle + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            double dx = x - centerX;
                            double dy = y - centerY;
                            double dist = Math.Sqrt(dx * dx + dy * dy);

                            double factor = 1.0 - (dist / maxDist) * (dist / maxDist);
                            factor = Math.Max(0, Math.Min(1, factor));

                            row[x * 4] = (byte)(row[x * 4] * factor);
                            row[x * 4 + 1] = (byte)(row[x * 4 + 1] * factor);
                            row[x * 4 + 2] = (byte)(row[x * 4 + 2] * factor);
                        }
                    });
                }
            }
        }

        private void ProcessGlow(byte[] pixels, int width, int height, int stride)
        {
            byte[] srcPixels = (byte[])pixels.Clone();
            unsafe
            {
                fixed (byte* destPtr = pixels)
                {
                    IntPtr destHandle = (IntPtr)destPtr;
                    fixed (byte* srcPtr = srcPixels)
                    {
                        IntPtr srcHandle = (IntPtr)srcPtr;

                        Parallel.For(0, height, y =>
                        {
                            byte* pSrcStart = (byte*)srcHandle;
                            byte* pDestStart = (byte*)destHandle;
                            byte* destRow = pDestStart + y * stride;

                            for (int x = 0; x < width; x++)
                            {
                                int sumB = 0, sumG = 0, sumR = 0, count = 0;

                                // 3x3 卷积
                                for (int ky = -2; ky <= 2; ky += 2)
                                {
                                    int py = y + ky;
                                    if (py < 0 || py >= height) continue;
                                    for (int kx = -2; kx <= 2; kx += 2)
                                    {
                                        int px = x + kx;
                                        if (px < 0 || px >= width) continue;

                                        int offset = py * stride + px * 4;
                                        sumB += pSrcStart[offset];
                                        sumG += pSrcStart[offset + 1];
                                        sumR += pSrcStart[offset + 2];
                                        count++;
                                    }
                                }

                                byte blurB = (byte)(sumB / count);
                                byte blurG = (byte)(sumG / count);
                                byte blurR = (byte)(sumR / count);

                                int curOff = y * stride + x * 4;
                                byte origB = pSrcStart[curOff];
                                byte origG = pSrcStart[curOff + 1];
                                byte origR = pSrcStart[curOff + 2];

                                // Screen Mode
                                destRow[x * 4] = (byte)(255 - ((255 - origB) * (255 - blurB) / 255));
                                destRow[x * 4 + 1] = (byte)(255 - ((255 - origG) * (255 - blurG) / 255));
                                destRow[x * 4 + 2] = (byte)(255 - ((255 - origR) * (255 - blurR) / 255));
                            }
                        });
                    }
                }
            }
        }
        private void ProcessOilPaint(byte[] pixels, int width, int height, int stride, int radius, int intensityLevels)
        {
            byte[] srcPixels = (byte[])pixels.Clone();

            unsafe
            {
                fixed (byte* destPtr = pixels)
                {
                    IntPtr destHandle = (IntPtr)destPtr;
                    fixed (byte* srcPtr = srcPixels)
                    {
                        IntPtr srcHandle = (IntPtr)srcPtr;

                        Parallel.For(0, height, y =>
                        {
                            byte* pSrcStart = (byte*)srcHandle;
                            byte* pDestStart = (byte*)destHandle;
                            byte* destRow = pDestStart + y * stride;

                            // 线程局部变量
                            float[] curSigma = new float[4];
                            int[] curMeanR = new int[4];
                            int[] curMeanG = new int[4];
                            int[] curMeanB = new int[4];
                            int[] curCnt = new int[4];

                            for (int x = 0; x < width; x++)
                            {
                                Array.Clear(curSigma, 0, 4);
                                Array.Clear(curMeanR, 0, 4);
                                Array.Clear(curMeanG, 0, 4);
                                Array.Clear(curMeanB, 0, 4);
                                Array.Clear(curCnt, 0, 4);

                                for (int ky = -radius; ky <= radius; ky++)
                                {
                                    int py = y + ky;
                                    if (py < 0 || py >= height) continue;
                                    for (int kx = -radius; kx <= radius; kx++)
                                    {
                                        int px = x + kx;
                                        if (px < 0 || px >= width) continue;

                                        int q = (ky <= 0 ? 0 : 2) + (kx <= 0 ? 0 : 1);

                                        int off = py * stride + px * 4;
                                        byte b = pSrcStart[off];
                                        byte g = pSrcStart[off + 1];
                                        byte r = pSrcStart[off + 2];

                                        curMeanR[q] += r;
                                        curMeanG[q] += g;
                                        curMeanB[q] += b;
                                        curCnt[q]++;

                                        float val = (r + g + b) / 3.0f;
                                        curSigma[q] += val * val;
                                    }
                                }

                                int minVarIndex = 0;
                                float minVar = float.MaxValue;

                                for (int i = 0; i < 4; i++)
                                {
                                    if (curCnt[i] == 0) continue;
                                    float cnt = curCnt[i];
                                    float meanVal = (curMeanR[i] + curMeanG[i] + curMeanB[i]) / (3.0f * cnt);
                                    float variance = (curSigma[i] / cnt) - (meanVal * meanVal);

                                    if (variance < minVar)
                                    {
                                        minVar = variance;
                                        minVarIndex = i;
                                    }
                                }

                                int cntBest = curCnt[minVarIndex];
                                if (cntBest > 0)
                                {
                                    destRow[x * 4] = (byte)(curMeanB[minVarIndex] / cntBest);
                                    destRow[x * 4 + 1] = (byte)(curMeanG[minVarIndex] / cntBest);
                                    destRow[x * 4 + 2] = (byte)(curMeanR[minVarIndex] / cntBest);
                                }
                            }
                        });
                    }
                }
            }
        }
    }
}
