using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Stage 2. Normalizes ellipses and duplicated punctuation. The opinionated transforms
    /// (typographic quotes, spaced-hyphen → em-dash) are OFF by default and guarded so they only fire
    /// in safe cases.
    /// </summary>
    public sealed class PunctuationNormalizer : ITextPostProcessor
    {
        public string Name => "PunctuationNormalizer";

        private static readonly Regex SpacedDots = new(@"\.[ ]\.[ ]\.", RegexOptions.Compiled);
        private static readonly Regex SolidDots = new(@"\.{3,}", RegexOptions.Compiled);
        private static readonly Regex DupComma = new(@",{2,}", RegexOptions.Compiled);
        private static readonly Regex DupSemicolon = new(@";{2,}", RegexOptions.Compiled);
        private static readonly Regex ExclaimRun = new(@"!{4,}", RegexOptions.Compiled);
        private static readonly Regex QuestionRun = new(@"\?{4,}", RegexOptions.Compiled);

        public string Process(string text, PostProcessingContext context)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = SpacedDots.Replace(text, "…");
            text = SolidDots.Replace(text, "…");
            text = DupComma.Replace(text, ",");
            text = DupSemicolon.Replace(text, ";");
            text = ExclaimRun.Replace(text, "!!!");
            text = QuestionRun.Replace(text, "???");

            if (context.Options.SpacedHyphenToDash)
                text = text.Replace(" - ", " — ");

            if (context.Options.NormalizeQuotes)
            {
                var lines = TextLines.Split(text);
                for (int i = 0; i < lines.Count; i++) lines[i] = ConvertQuotes(lines[i]);
                text = TextLines.Join(lines);
            }

            return text;
        }

        /// <summary>
        /// Convert balanced straight double quotes on a line to guillemets «…». Abstains on lines with
        /// an inch/foot mark (quote adjacent to a digit) or an unpaired quote count. Apostrophes are
        /// never touched (only '"' is replaced).
        /// </summary>
        private static string ConvertQuotes(string line)
        {
            if (line.IndexOf('"') < 0) return line;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != '"') continue;
                bool digitBefore = i > 0 && char.IsDigit(line[i - 1]);
                bool digitAfter = i + 1 < line.Length && char.IsDigit(line[i + 1]);
                if (digitBefore || digitAfter) return line; // inch/foot mark — leave the whole line
            }

            if (line.Count(ch => ch == '"') % 2 != 0) return line; // unpaired — leave verbatim

            var sb = new StringBuilder(line.Length);
            bool open = true;
            foreach (char ch in line)
            {
                if (ch == '"') { sb.Append(open ? '«' : '»'); open = !open; }
                else sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
