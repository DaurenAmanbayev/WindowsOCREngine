using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Stage 5 (riskiest, conservative). Merges OCR line-breaks back into paragraphs: a line is joined
    /// to the previous one only when the previous line does NOT end in sentence punctuation and the
    /// current line starts with a lowercase letter. Blank lines and list/heading markers are hard
    /// breaks. Skipped for space-joining languages.
    /// </summary>
    public sealed class ParagraphReconstructor : ITextPostProcessor
    {
        public string Name => "ParagraphReconstructor";

        private static readonly HashSet<char> SentenceEnd = new() { '.', '!', '?', '…', ':', '»', '"', '”' };
        private static readonly Regex NumberMarker = new(@"^\d+[.)]", RegexOptions.Compiled);
        private static readonly Regex LetterMarker = new(@"^\p{L}[.)]\s", RegexOptions.Compiled);

        public string Process(string text, PostProcessingContext context)
        {
            if (string.IsNullOrEmpty(text) || context.IsSpaceJoiningLanguage) return text;

            var lines = TextLines.Split(text);
            var result = new List<string>();

            foreach (string line in lines)
            {
                if (line.Trim().Length == 0) { result.Add(line); continue; } // blank → hard break
                if (result.Count == 0) { result.Add(line); continue; }

                string prev = result[result.Count - 1];
                if (CanMerge(prev, line))
                    result[result.Count - 1] = prev.TrimEnd() + " " + line.TrimStart();
                else
                    result.Add(line);
            }

            return TextLines.Join(result);
        }

        private static bool CanMerge(string prev, string current)
        {
            string prevTrim = prev.TrimEnd();
            if (prevTrim.Length == 0) return false;
            if (SentenceEnd.Contains(prevTrim[prevTrim.Length - 1])) return false;

            string curTrim = current.TrimStart();
            if (curTrim.Length == 0) return false;
            if (IsListOrHeadingMarker(curTrim)) return false;

            char first = curTrim[0];
            return char.IsLetter(first) && char.IsLower(first);
        }

        private static bool IsListOrHeadingMarker(string trimmed)
        {
            char c0 = trimmed[0];
            if (c0 == '-' || c0 == '•' || c0 == '—' || c0 == '*' || c0 == '·') return true;
            return NumberMarker.IsMatch(trimmed) || LetterMarker.IsMatch(trimmed);
        }
    }
}
