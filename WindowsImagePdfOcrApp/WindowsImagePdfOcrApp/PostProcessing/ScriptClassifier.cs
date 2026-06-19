using System.Globalization;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    public enum Script
    {
        /// <summary>Cyrillic letters, including Kazakh-extended (ә ғ қ ң ө ұ ү һ і).</summary>
        Cyrillic,
        /// <summary>Basic Latin letters A–Z / a–z.</summary>
        Latin,
        /// <summary>Everything else: digits, punctuation, whitespace, symbols, other scripts.</summary>
        Common
    }

    /// <summary>
    /// Three-class character classifier. Uses Unicode blocks, never an [а-я] range, so the full
    /// Kazakh alphabet classifies as Cyrillic. Case decisions always go through char.IsUpper/IsLower.
    /// </summary>
    public static class ScriptClassifier
    {
        public static Script Classify(char c)
        {
            if (IsCyrillicLetter(c)) return Script.Cyrillic;
            if (IsLatinLetter(c)) return Script.Latin;
            return Script.Common;
        }

        public static bool IsCyrillicLetter(char c)
        {
            if (!char.IsLetter(c)) return false;
            // Cyrillic (U+0400–U+04FF) covers Russian and all Kazakh-extended letters;
            // Cyrillic Supplement (U+0500–U+052F) covers rarer additions.
            int code = c;
            return (code >= 0x0400 && code <= 0x04FF) || (code >= 0x0500 && code <= 0x052F);
        }

        public static bool IsLatinLetter(char c)
        {
            if (!char.IsLetter(c)) return false;
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        /// <summary>True for digits, punctuation, separators — never letters of any script.</summary>
        public static bool IsCommon(char c)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            switch (cat)
            {
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.DashPunctuation:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.OtherPunctuation:
                    return true;
                default:
                    return !char.IsLetter(c);
            }
        }
    }
}
