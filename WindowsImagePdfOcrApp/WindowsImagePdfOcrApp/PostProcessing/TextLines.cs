using System.Collections.Generic;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Shared line model for line-oriented stages (DeHyphenator, ParagraphReconstructor), so they do
    /// not each re-derive "what is a line". Always splits on '\n' (callers normalize line endings via
    /// WhitespaceNormalizer first, but Split defensively normalizes too).
    /// </summary>
    internal static class TextLines
    {
        public static List<string> Split(string text)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            return new List<string>(text.Split('\n'));
        }

        public static string Join(IEnumerable<string> lines) => string.Join("\n", lines);
    }
}
