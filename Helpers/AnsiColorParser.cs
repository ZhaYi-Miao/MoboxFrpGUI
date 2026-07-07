using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using WpfMedia = System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;

namespace MoboxFrpGUI.Helpers
{
    public class AnsiColorParser
    {
        private static readonly Regex AnsiRegex = new Regex(@"\x1b\[([0-9;]*)m", RegexOptions.Compiled);

        public struct StyledText
        {
            public string Text;
            public WpfBrush? Foreground;
            public bool IsBold;
        }

        public static List<StyledText> Parse(string input)
        {
            var result = new List<StyledText>();
            if (string.IsNullOrEmpty(input)) return result;

            WpfBrush? currentBrush = null;
            bool isBold = false;
            int lastIndex = 0;

            MatchCollection matches = AnsiRegex.Matches(input);
            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    string text = input.Substring(lastIndex, match.Index - lastIndex);
                    result.Add(new StyledText { Text = text, Foreground = currentBrush, IsBold = isBold });
                }

                string codes = match.Groups[1].Value;
                string[] codeList = codes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string code in codeList)
                {
                    if (int.TryParse(code, out int num))
                    {
                        switch (num)
                        {
                            case 0:
                                currentBrush = null;
                                isBold = false;
                                break;
                            case 1:
                                isBold = true;
                                break;
                            case 30:
                                currentBrush = WpfBrushes.Black;
                                break;
                            case 31:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0xE7, 0x4C, 0x3C));
                                break;
                            case 32:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0x2E, 0xCC, 0x71));
                                break;
                            case 33:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0xF1, 0xC4, 0x0F));
                                break;
                            case 34:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0x3B, 0x82, 0xF6));
                                break;
                            case 35:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0xA8, 0x55, 0xF7));
                                break;
                            case 36:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0x06, 0xB6, 0xD4));
                                break;
                            case 37:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0xE5, 0xE7, 0xEB));
                                break;
                            case 90:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0x6B, 0x72, 0x80));
                                break;
                            case 91:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0xFC, 0x81, 0x81));
                                break;
                            case 92:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0x86, 0xEF, 0xA8));
                                break;
                            case 93:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0xFC, 0xD3, 0x4D));
                                break;
                            case 94:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0x60, 0xA5, 0xFA));
                                break;
                            case 95:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0xC0, 0x84, 0xFC));
                                break;
                            case 96:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0x22, 0xD3, 0xEE));
                                break;
                            case 97:
                                currentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0xF9, 0xFA, 0xFB));
                                break;
                        }
                    }
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < input.Length)
            {
                string text = input.Substring(lastIndex);
                result.Add(new StyledText { Text = text, Foreground = currentBrush, IsBold = isBold });
            }

            return result;
        }

        public static void AppendColoredText(FlowDocument doc, string text, WpfBrush defaultForeground)
        {
            string[] paragraphs = text.Split(new[] { '\n' }, StringSplitOptions.None);

            for (int i = 0; i < paragraphs.Length; i++)
            {
                string line = paragraphs[i];
                var para = new Paragraph { Margin = new Thickness(0) };

                List<StyledText> styledTexts = Parse(line);
                if (styledTexts.Count == 0)
                {
                    para.Inlines.Add(new Run(string.Empty));
                }
                else
                {
                    foreach (StyledText st in styledTexts)
                    {
                        var run = new Run(st.Text);
                        if (st.Foreground != null)
                        {
                            run.Foreground = st.Foreground;
                        }
                        else
                        {
                            run.Foreground = defaultForeground;
                        }
                        if (st.IsBold)
                        {
                            run.FontWeight = FontWeights.Bold;
                        }
                        para.Inlines.Add(run);
                    }
                }

                doc.Blocks.Add(para);
            }
        }
    }
}
