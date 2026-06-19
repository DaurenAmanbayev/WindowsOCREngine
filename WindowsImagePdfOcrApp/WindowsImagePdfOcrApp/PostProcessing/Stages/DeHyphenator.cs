using System.Collections.Generic;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Stage 3. Joins words split by a trailing hyphen at a line break (пробле-\nма → проблема).
    /// Includes the code-boundary guard so a hyphenated Latin code is never fused (Wi-\nfi ↛ Wifi).
    /// Skipped for space-joining languages (but stray soft hyphens are still stripped).
    /// </summary>
    public sealed class DeHyphenator : ITextPostProcessor
    {
        public string Name => "DeHyphenator";

        public string Process(string text, PostProcessingContext context)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (context.IsSpaceJoiningLanguage)
                return text.Replace("­", string.Empty);

            var lines = TextLines.Split(text);
            var result = new List<string>();
            string? current = null;

            foreach (string line in lines)
            {
                if (current == null) { current = line; continue; }
                if (TryJoin(current, line, out string joined)) current = joined;
                else { result.Add(current); current = line; }
            }
            if (current != null) result.Add(current);

            string output = TextLines.Join(result);
            // Remove any soft hyphens that were not at a line break.
            return output.Replace("­", string.Empty);
        }

        private static bool TryJoin(string line, string next, out string joined)
        {
            joined = string.Empty;

            string trimmed = line.TrimEnd();
            if (trimmed.Length < 2) return false;

            char last = trimmed[trimmed.Length - 1];
            if (!IsHyphen(last)) return false;
            if (!char.IsLetter(trimmed[trimmed.Length - 2])) return false;

            string nextTrimmed = next.TrimStart();
            if (nextTrimmed.Length == 0) return false;
            char firstNext = nextTrimmed[0];
            if (!char.IsLetter(firstNext) || !char.IsLower(firstNext)) return false;

            // Code-boundary guard: a short Latin run + hyphen + Latin run looks like an alphabetic
            // code segment (Wi-Fi, Type-C). Abstain — body-text de-hyphenation targets Cyrillic words.
            string head = TrailingLetters(trimmed.Substring(0, trimmed.Length - 1));
            string tail = LeadingLetters(nextTrimmed);
            if (head.Length > 0 && head.Length <= 3 && IsLatinRun(head) && IsLatinRun(tail))
                return false;

            joined = trimmed.Substring(0, trimmed.Length - 1) + nextTrimmed;
            return true;
        }

        private static bool IsHyphen(char c) => c == '-' || c == '­' || c == '‐';

        private static string TrailingLetters(string s)
        {
            int end = s.Length;
            int i = end;
            while (i > 0 && char.IsLetter(s[i - 1])) i--;
            return s.Substring(i, end - i);
        }

        private static string LeadingLetters(string s)
        {
            int i = 0;
            while (i < s.Length && char.IsLetter(s[i])) i++;
            return s.Substring(0, i);
        }

        private static bool IsLatinRun(string s)
        {
            if (s.Length == 0) return false;
            foreach (char c in s)
                if (!ScriptClassifier.IsLatinLetter(c)) return false;
            return true;
        }
    }
}
