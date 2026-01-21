
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;


//
//TEXTtool
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void FontSettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_router.CurrentTool is TextTool textTool)
            {
                textTool.UpdateCurrentTextBoxAttributes();
            }

        }

        public class TextStyleInfo
        {
            public string Text { get; set; }
            public FontFamily FontFamily { get; set; }
            public double FontSize { get; set; }
            public FontWeight FontWeight { get; set; }
            public FontStyle FontStyle { get; set; }
            public TextDecorationCollection TextDecorations { get; set; }
            public SolidColorBrush Foreground { get; set; }
        }// 在 MainWindow 类中添加
        public void ApplyDetectedTextStyle(TextStyleInfo style)
        {
            if (style == null) return;

            // 1. 应用字体
            if (style.FontFamily != null)
            {
                FontFamilyBox.SelectedValue = style.FontFamily;
            }

            double fontSizeInPoints = style.FontSize * 72.0 / 96.0;
            FontSizeBox.Text = Math.Round(fontSizeInPoints).ToString();

            // 3. 应用粗体/斜体
            BoldBtn.IsChecked = (style.FontWeight >= FontWeights.Bold);
            ItalicBtn.IsChecked = (style.FontStyle == FontStyles.Italic || style.FontStyle == FontStyles.Oblique);

            // 4. 应用下划线/删除线
            UnderlineBtn.IsChecked = false;
            StrikeBtn.IsChecked = false;
            if (style.TextDecorations != null)
            {
                foreach (var dec in style.TextDecorations)
                {
                    if (dec.Location == TextDecorationLocation.Underline) UnderlineBtn.IsChecked = true;
                    if (dec.Location == TextDecorationLocation.Strikethrough) StrikeBtn.IsChecked = true;
                }
            }

            // 5. 应用颜色
            if (style.Foreground != null)
            {
                SelectedBrush = style.Foreground;

                _ctx.PenColor = style.Foreground.Color;
                UpdateCurrentColor(_ctx.PenColor, useSecondColor);
            }
        }


        public static class TextFormatHelper
        {
            public static TextStyleInfo ParseRtf(string rtfContent)
            {
                if (string.IsNullOrEmpty(rtfContent)) return null;

                // 1. 创建一个临时的 RichTextBox 用于解析
                var rtb = new RichTextBox();
                var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);

                try
                {
                    // 2. 加载 RTF 数据
                    using (var stream = new MemoryStream(Encoding.Default.GetBytes(rtfContent)))
                    {
                        range.Load(stream, DataFormats.Rtf);
                    }

                    // 如果内容为空，返回 null
                    if (range.IsEmpty) return null;

                    // 3. 获取纯文本
                    string text = range.Text.TrimEnd('\r', '\n');

                    TextRange targetRange = range;

                    var fontFamily = targetRange.GetPropertyValue(TextElement.FontFamilyProperty) as FontFamily;
                    var fontSizeObj = targetRange.GetPropertyValue(TextElement.FontSizeProperty);
                    var fontWeightObj = targetRange.GetPropertyValue(TextElement.FontWeightProperty);
                    var fontStyleObj = targetRange.GetPropertyValue(TextElement.FontStyleProperty);
                    var textDecors = targetRange.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
                    var foreground = targetRange.GetPropertyValue(TextElement.ForegroundProperty) as SolidColorBrush;

                    double fontSize = (fontSizeObj is double d) ? d : 24.0; // 默认 24
                    FontWeight fontWeight = (fontWeightObj is FontWeight w) ? w : FontWeights.Normal;
                    FontStyle fontStyle = (fontStyleObj is FontStyle s) ? s : FontStyles.Normal;

                    // 如果获取不到颜色（比如 UnsetValue），给个默认黑色
                    if (foreground == null || foreground == DependencyProperty.UnsetValue)
                    {
                        foreground = Brushes.Black;
                    }


                    return new TextStyleInfo
                    {
                        Text = text,
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                        FontStyle = fontStyle,
                        TextDecorations = textDecors,
                        Foreground = foreground
                    };
                }
                catch
                {
                    return null; // 解析失败降级处理
                }
            }
        }
        public partial class TextTool : ToolBase
        {

            public override string Name => "Text";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.IBeam;

            private Int32Rect _textRect;
            public System.Windows.Controls.TextBox _textBox;
            private Point _startPos;
            private bool _dragging = false;

            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            private bool _resizing = false;
            private Point _startMouse;
            private double _startW, _startH, _startX, _startY;

            // 句柄尺寸
            private const double HandleSize = 6;
            private int lag = 0;
            private bool _justDismissed = false; // 用于记录当前点击是否是为了销毁上一个文本框

            public enum ResizeAnchor
            {
                None,
                TopLeft, TopMiddle, TopRight,
                LeftMiddle, RightMiddle,
                BottomLeft, BottomMiddle, BottomRight
            }

        }
    }
}