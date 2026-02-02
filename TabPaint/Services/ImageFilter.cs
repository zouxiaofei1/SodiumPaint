
using System.ComponentModel;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
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

                            int tr = (int)(0.393 * r + 0.769 * g + 0.189 * b);
                            int tg = (int)(0.349 * r + 0.686 * g + 0.168 * b);
                            int tb = (int)(0.272 * r + 0.534 * g + 0.131 * b);

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
            // 因为卷积需要读取原始邻域像素，我们必须拷贝一份作为“只读源”
            byte[] srcPixels = (byte[])pixels.Clone();

            unsafe
            {
                // 目标数组(pixels)的指针
                fixed (byte* destPtr = pixels)
                {
                    IntPtr destHandle = (IntPtr)destPtr;

                    // 源数组(srcPixels)的指针
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
        // 4. 油画 (Oil Paint) - 需要源数据副本
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
