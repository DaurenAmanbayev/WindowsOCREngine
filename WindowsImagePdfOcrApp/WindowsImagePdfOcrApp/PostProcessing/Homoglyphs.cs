using System.Collections.Generic;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// The single source of truth for Cyrillic/Latin confusable pairs and for the derived "proving"
    /// test. A proving letter is an unambiguous letter of a script — used to decide a mixed-script
    /// token's dominant script (NOT a majority count). The proving set is derived as a complement of
    /// the confusable map; it is never hand-listed (so it stays complete and symmetric).
    /// </summary>
    internal static class Homoglyphs
    {
        /// <summary>
        /// Latin -> Cyrillic confusable pairs. COMPLETE list of genuine look-alikes:
        ///   Uppercase: A B C E H K M O P T X Y
        ///   Lowercase: a c e o p x y
        /// The i / и / і cluster is deliberately ABSENT (a three-way ambiguity a binary model can't
        /// resolve — deferred to the dictionary spell-check stage).
        /// </summary>
        public static readonly IReadOnlyDictionary<char, char> LatinToCyrillic = new Dictionary<char, char>
        {
            ['A'] = 'А', ['B'] = 'В', ['C'] = 'С', ['E'] = 'Е', ['H'] = 'Н', ['K'] = 'К',
            ['M'] = 'М', ['O'] = 'О', ['P'] = 'Р', ['T'] = 'Т', ['X'] = 'Х', ['Y'] = 'У',
            ['a'] = 'а', ['c'] = 'с', ['e'] = 'е', ['o'] = 'о', ['p'] = 'р', ['x'] = 'х', ['y'] = 'у',
        };

        /// <summary>Cyrillic -> Latin, the inverse of <see cref="LatinToCyrillic"/>.</summary>
        public static readonly IReadOnlyDictionary<char, char> CyrillicToLatin = Invert(LatinToCyrillic);

        /// <summary>
        /// Letters frozen against PROVING even though they are not in a confusable pair:
        /// Latin i/I (look-alike of Kazakh і) and Russian и/И (excluded by the cluster rule).
        /// Kazakh і/І are intentionally NOT here — a Cyrillic і the OCR emitted is strong proof the
        /// token is Kazakh, so it proves. The cluster only governs *rewriting* (handled by the map
        /// simply not containing i/и/і), not proving.
        /// </summary>
        private static readonly HashSet<char> NonProving = new() { 'i', 'I', 'и', 'И' };

        /// <summary>True if <paramref name="c"/> is half of a confusable pair (a look-alike letter).</summary>
        public static bool IsAmbiguous(char c) => LatinToCyrillic.ContainsKey(c) || CyrillicToLatin.ContainsKey(c);

        /// <summary>
        /// True if <paramref name="c"/> is an unambiguous letter of <paramref name="script"/> — i.e. it
        /// proves the token belongs to that script. proving = (letters of the script) − (confusable
        /// halves) − (the i/и/і non-proving members).
        /// </summary>
        public static bool Proves(char c, Script script)
        {
            if (IsAmbiguous(c)) return false;
            if (NonProving.Contains(c)) return false;
            return ScriptClassifier.Classify(c) == script;
        }

        private static Dictionary<char, char> Invert(IReadOnlyDictionary<char, char> map)
        {
            var inverted = new Dictionary<char, char>(map.Count);
            foreach (var kv in map) inverted[kv.Value] = kv.Key;
            return inverted;
        }
    }
}
