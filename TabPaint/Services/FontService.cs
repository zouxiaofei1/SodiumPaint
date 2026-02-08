using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using System.Windows.Markup;

namespace TabPaint.Services
{
    public class FontDisplayItem
    {
        public string DisplayName { get; set; }
        public FontFamily FontFamily { get; set; }

        public override string ToString() => DisplayName;
    }

    public static class FontService
    {
        private static List<FontDisplayItem> _cachedFonts;
        private static readonly object _lock = new object();

        public static List<FontDisplayItem> GetSystemFonts()
        {
            if (_cachedFonts != null) return _cachedFonts;

            lock (_lock)
            {
                if (_cachedFonts != null) return _cachedFonts;

                var currentLang = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
                var zhCn = XmlLanguage.GetLanguage("zh-cn");
                var enUs = XmlLanguage.GetLanguage("en-us");

                var fontItems = new List<FontDisplayItem>();

                foreach (var font in Fonts.SystemFontFamilies)
                {
                    if (IsSymbolFont(font)) continue;

                    string name = GetLocalizedFontName(font, currentLang, zhCn, enUs);
                    fontItems.Add(new FontDisplayItem { DisplayName = name, FontFamily = font });
                }

                _cachedFonts = fontItems.OrderBy(f => f.DisplayName).ToList();
                return _cachedFonts;
            }
        }

        public static FontDisplayItem GetDefaultFont(List<FontDisplayItem> fonts)
        {
            if (fonts == null || fonts.Count == 0) return null;

            return fonts.FirstOrDefault(f => f.FontFamily.Source.Contains("Microsoft YaHei"))
                   ?? fonts.FirstOrDefault(f => f.FontFamily.Source.Contains("微软雅黑"))
                   ?? fonts.FirstOrDefault(f => f.FontFamily.Source.Contains("Arial"))
                   ?? fonts.FirstOrDefault(f => f.FontFamily.Source.Contains("宋体"))
                   ?? fonts.FirstOrDefault();
        }

        private static string GetLocalizedFontName(FontFamily fontFamily, XmlLanguage currentLang, XmlLanguage zhCn, XmlLanguage enUs)
        {
            if (fontFamily.FamilyNames.TryGetValue(currentLang, out string name)) return name;
            if (fontFamily.FamilyNames.TryGetValue(zhCn, out name)) return name;
            if (fontFamily.FamilyNames.TryGetValue(enUs, out name)) return name;
            return fontFamily.Source;
        }

        private static bool IsSymbolFont(FontFamily font)
        {
            string name = font.Source.ToLower();
            return name.Contains("webdings") ||
                   name.Contains("wingdings") ||
                   name.Contains("symbol") ||
                   name.Contains("marlett") ||
                   name.Contains("holomdl2") ||
                   name.Contains("segway") ||
                   name.Contains("emoji");
        }
    }
}
