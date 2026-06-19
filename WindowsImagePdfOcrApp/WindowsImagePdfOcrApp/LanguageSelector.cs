using System;
using System.Linq;
using Windows.Media.Ocr;

namespace WindowsImagePdfOcrApp
{
    /// <summary>
    /// Picks the OCR recognizer language for trilingual KZ content. An explicit <paramref name="preferred"/>
    /// tag (from the -lang CLI flag) wins if it is installed; otherwise falls back to the automatic
    /// priority chain kk → ru → en. All matching is by PRIMARY SUBTAG, case-insensitively (so "kk",
    /// "KK-KZ", "kk-Cyrl-KZ" all count). Warns on stderr when a requested language is missing, and when
    /// neither Kazakh nor Russian is installed (glyphs missed at the OCR layer cannot be recovered later).
    /// </summary>
    public static class LanguageSelector
    {
        private static readonly string[] Priority = { "kk", "ru", "en" };

        public static string PickBest(string? preferred = null)
        {
            var available = OcrEngine.AvailableRecognizerLanguages;

            // Explicit user choice via -lang. Matched by primary subtag, case-insensitively, so
            // "-lang ru", "-lang RU" and "-lang ru-RU" all resolve to the installed ru recognizer.
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                string wanted = PrimarySubtag(preferred);
                foreach (var lang in available)
                {
                    if (PrimarySubtag(lang.LanguageTag) == wanted)
                        return lang.LanguageTag;
                }

                Console.Error.WriteLine(
                    $"[WARNING] Requested OCR language '{preferred}' is not installed. " +
                    "Falling back to automatic selection (kk -> ru -> en). " +
                    "Install it via Windows Settings > Time & Language > Language.");
            }

            foreach (string pref in Priority)
            {
                foreach (var lang in available)
                {
                    if (PrimarySubtag(lang.LanguageTag) == pref)
                        return lang.LanguageTag;
                }
            }

            bool hasKkOrRu = available.Any(l =>
            {
                string p = PrimarySubtag(l.LanguageTag);
                return p == "kk" || p == "ru";
            });

            if (!hasKkOrRu)
            {
                Console.Error.WriteLine(
                    "[WARNING] No Kazakh (kk) or Russian (ru) OCR recognizer is installed. " +
                    "Kazakh/Russian glyphs may be misrecognized at the OCR layer and cannot be recovered " +
                    "by post-processing. Install the language via Windows Settings > Time & Language > Language.");
            }

            // Fall back to whatever recognizer exists; PowerOcrEngine will validate/fall back again.
            return available.FirstOrDefault()?.LanguageTag ?? "en-US";
        }

        private static string PrimarySubtag(string tag) =>
            tag.Split('-')[0].ToLowerInvariant();
    }
}
