using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using System.Runtime.InteropServices.WindowsRuntime;

namespace TabPaint
{
    public class OcrService
    {
        private OcrEngine _ocrEngine;

        public OcrService()
        {
            InitBestEngine();
        }

        private void InitBestEngine()
        {
            _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

            if (_ocrEngine == null)
            {
                try 
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language(Language.CurrentInputMethodLanguageTag));
                }
                catch { }
            }

            if (_ocrEngine == null)
            {
                // 最后的保底，随便找一个支持的
                var firstLang = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault();
                if (firstLang != null)
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(firstLang);
                }
            }
        }

        private bool IsCjk(char c)
        {
            // 包含中日韩字符范围
            if (c >= 0x4E00 && c <= 0x9FFF) return true;
            if (c >= 0xFF00 && c <= 0xFFEF) return true;
            if (c >= 0x3000 && c <= 0x303F) return true;
            return false;
        }
        private BitmapSource PreprocessImage(BitmapSource source)
        {
            
            double scale = 1.0;
            if (source.PixelHeight < 400 || source.PixelWidth < 400)
            {
                scale = 2.0; 
            }
            else
            {
                scale = 1.5;
            }

            if (scale > 1.0)
            {
                return new TransformedBitmap(source, new ScaleTransform(scale, scale));
            }
            return source;
        }

        public async Task<string> RecognizeTextAsync(BitmapSource wpfBitmap)
        {
            if (_ocrEngine == null)
            {
                InitBestEngine();
                if (_ocrEngine == null) return LocalizationManager.GetString("L_OCR_Error_NoLangPack");
            }

            try
            {
                // 1. 预处理：放大图片
                var processedBitmap = PreprocessImage(wpfBitmap);

                using (var ms = new MemoryStream())
                {
                    // 2. 优化：使用 BMP 编码器，速度比 PNG 快，且无压缩损耗
                    var encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(processedBitmap));
                    encoder.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    var randomAccessStream = ms.AsRandomAccessStream();
                    var decoder = await global::Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);

                    // 获取 SoftwareBitmap
                    using (var softwareBitmap = await decoder.GetSoftwareBitmapAsync())
                    {
                        // 3. 执行 OCR 识别
                        var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);

                        if (ocrResult.Lines.Count == 0) return null;

                        // 4. 拼接结果
                        StringBuilder sb = new StringBuilder();
                        foreach (var line in ocrResult.Lines)
                        {
                            for (int i = 0; i < line.Words.Count; i++)
                            {
                                var currentWord = line.Words[i];
                                sb.Append(currentWord.Text);

                                // 智能空格处理
                                if (i < line.Words.Count - 1)
                                {
                                    var nextWord = line.Words[i + 1];
                                    bool currentIsCjk = currentWord.Text.Any(IsCjk);
                                    bool nextIsCjk = nextWord.Text.Any(IsCjk);

                                    if (!currentIsCjk && !nextIsCjk)
                                    {
                                        sb.Append(" ");
                                    }
                                }
                            }
                            sb.AppendLine(); 
                        }
                        return sb.ToString().Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                return string.Format(LocalizationManager.GetString("L_OCR_Failed_Prefix"), ex.Message);
            }
        }
    }
}
