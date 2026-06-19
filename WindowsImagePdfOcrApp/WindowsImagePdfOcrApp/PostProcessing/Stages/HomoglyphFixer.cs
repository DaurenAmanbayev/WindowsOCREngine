using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Stage 4. Fixes inter-alphabet (Cyrillic/Latin) homoglyph errors inside mixed-script tokens.
    /// The dominant script is decided by PROVING letters (unambiguous letters), not by letter count;
    /// the stage abstains when both or neither side proves. Protected tokens (rules a,b,d,e — the
    /// ALL-CAPS rule c is excluded here) are left untouched. Skipped for space-joining languages.
    /// </summary>
    public sealed class HomoglyphFixer : ITextPostProcessor
    {
        public string Name => "HomoglyphFixer";

        // A "word" for protection/analysis: letters, digits and hyphens (so USB-C / iPhone15 are seen whole).
        private static readonly Regex WordRx = new(@"[\p{L}\p{Nd}-]+", RegexOptions.Compiled);

        public string Process(string text, PostProcessingContext context)
        {
            if (string.IsNullOrEmpty(text) || context.IsSpaceJoiningLanguage) return text;
            return WordRx.Replace(text, m => FixWord(m.Value, context));
        }

        private static string FixWord(string word, PostProcessingContext context)
        {
            if (!word.Any(char.IsLetter)) return word;
            if (ProtectedTokens.IsProtected(word, context, ProtectionScope.Homoglyph)) return word;

            bool hasCyr = false, hasLat = false, provesCyr = false, provesLat = false;
            foreach (char c in word)
            {
                Script s = ScriptClassifier.Classify(c);
                if (s == Script.Cyrillic)
                {
                    hasCyr = true;
                    if (Homoglyphs.Proves(c, Script.Cyrillic)) provesCyr = true;
                }
                else if (s == Script.Latin)
                {
                    hasLat = true;
                    if (Homoglyphs.Proves(c, Script.Latin)) provesLat = true;
                }
            }

            if (!(hasCyr && hasLat)) return word;            // not mixed-script → nothing to fix
            if (provesCyr && !provesLat) return Map(word, toCyrillic: true);
            if (provesLat && !provesCyr) return Map(word, toCyrillic: false);
            return word;                                     // both or neither prove → abstain
        }

        private static string Map(string word, bool toCyrillic)
        {
            var sb = new StringBuilder(word.Length);
            foreach (char c in word)
            {
                if (toCyrillic && Homoglyphs.LatinToCyrillic.TryGetValue(c, out char cyr)) sb.Append(cyr);
                else if (!toCyrillic && Homoglyphs.CyrillicToLatin.TryGetValue(c, out char lat)) sb.Append(lat);
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
