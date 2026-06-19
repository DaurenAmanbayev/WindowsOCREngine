using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Stage 1. Normalizes line endings and whitespace and fixes spacing around punctuation — without
    /// breaking Russian abbreviations or decimals. NOT tokenized, so it relies on conservative,
    /// char/line-level rules rather than the protected-token gate.
    /// </summary>
    public sealed class WhitespaceNormalizer : ITextPostProcessor
    {
        public string Name => "WhitespaceNormalizer";

        private static readonly Regex MultiSpace = new(@"[ ]{2,}", RegexOptions.Compiled);
        private static readonly Regex SpaceBeforePunct = new(@"[ ]+([.,;:!?»\)\]])", RegexOptions.Compiled);
        private static readonly Regex SpaceAfterOpen = new(@"([(«\[])[ ]+", RegexOptions.Compiled);
        // Insert a space after .!? ONLY before an uppercase letter (a real sentence break) — never
        // inside т.д. / 3.14 (next char is lowercase / a digit). The negative lookbehind (?<!\b\p{Lu})
        // also suppresses it after a lone uppercase initial at a word boundary, so А.Д. stays tight
        // (while США.Затем and конец.Начало still get a space).
        private static readonly Regex PeriodSpace = new(@"(?<!\b\p{Lu})([.!?])(\p{Lu})", RegexOptions.Compiled);
        // Insert a space after ,;: ONLY before a letter — never inside 3,14 or 10:30 (next char a digit).
        private static readonly Regex CommaSpace = new(@"([,;:])(\p{L})", RegexOptions.Compiled);
        private static readonly Regex BlankLines = new(@"\n{3,}", RegexOptions.Compiled);

        public string Process(string text, PostProcessingContext context)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // Strip control/zero-width (Cc/Cf) and exotic space separators, but PRESERVE the U+00AD
            // soft hyphen (DeHyphenator depends on it) and '\n'. Map tabs and Unicode spaces to ' '.
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '\n') { sb.Append('\n'); continue; }
                if (c == '­') { sb.Append(c); continue; }
                if (c == '\t') { sb.Append(' '); continue; }

                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.SpaceSeparator) { sb.Append(' '); continue; }
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format) continue;
                sb.Append(c);
            }

            string[] lines = sb.ToString().Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = MultiSpace.Replace(lines[i], " ");
                line = SpaceBeforePunct.Replace(line, "$1");
                line = SpaceAfterOpen.Replace(line, "$1");
                line = PeriodSpace.Replace(line, "$1 $2");
                line = CommaSpace.Replace(line, "$1 $2");
                lines[i] = line.TrimEnd();
            }

            string joined = string.Join("\n", lines);
            joined = BlankLines.Replace(joined, "\n\n");
            return joined;
        }
    }
}
