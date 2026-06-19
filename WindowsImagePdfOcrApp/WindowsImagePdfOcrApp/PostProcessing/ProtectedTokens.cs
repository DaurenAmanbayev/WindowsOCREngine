using System.Linq;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Which stage is asking. The ALL-CAPS rule (c) is scoped to structural stages and excluded from
    /// the homoglyph fixer, so mixed-script caps headings (ГOCT -> ГОСТ) can still be corrected.
    /// </summary>
    public enum ProtectionScope
    {
        Structural,
        Homoglyph
    }

    /// <summary>
    /// Detects brand / model / code tokens that must pass through transforms untouched. A token is
    /// protected if ANY rule matches:
    ///   (a) mixes digits with letters        — A52, RTX4090, iPhone15, БУ-3
    ///   (b) internal camelCase / mixed case   — iPhone, GeForce, macOS
    ///   (c) ALL-CAPS length 2–6 (structural)  — USB, HDMI, LED, PDF
    ///   (d) hyphen-joined alphabetic code     — USB-C, Type-C, Wi-Fi
    ///   (e) present in the brand allowlist     — iPhone, Samsung, ...
    /// </summary>
    public static class ProtectedTokens
    {
        public static bool IsProtected(string token, PostProcessingContext ctx, ProtectionScope scope)
        {
            if (string.IsNullOrEmpty(token)) return false;

            // (e) brand allowlist (case-insensitive).
            if (ctx.BrandAllowlist.Contains(token)) return true;

            bool hasLetter = false, hasDigit = false, hasUpper = false, hasLower = false;
            foreach (char c in token)
            {
                if (char.IsLetter(c))
                {
                    hasLetter = true;
                    if (char.IsUpper(c)) hasUpper = true;
                    else if (char.IsLower(c)) hasLower = true;
                }
                else if (char.IsDigit(c))
                {
                    hasDigit = true;
                }
            }

            // (a) digits + letters together.
            if (hasLetter && hasDigit) return true;

            // (b) a lowercase letter appears before an uppercase one (camelCase / mixed case).
            if (HasLowerBeforeUpper(token)) return true;

            // (d) hyphen-joined alphabetic code with at least one uppercase part.
            if (IsHyphenCode(token)) return true;

            // (c) ALL-CAPS length 2–6 — structural stages only.
            if (scope == ProtectionScope.Structural
                && hasUpper && !hasLower && IsAllLetters(token)
                && token.Length >= 2 && token.Length <= 6)
            {
                return true;
            }

            return false;
        }

        private static bool HasLowerBeforeUpper(string token)
        {
            bool seenLower = false;
            foreach (char c in token)
            {
                if (char.IsLower(c)) seenLower = true;
                else if (char.IsUpper(c) && seenLower) return true;
            }
            return false;
        }

        private static bool IsAllLetters(string token)
        {
            foreach (char c in token)
                if (!char.IsLetter(c)) return false;
            return true;
        }

        private static bool IsHyphenCode(string token)
        {
            if (token.IndexOf('-') < 0) return false;
            string[] parts = token.Split('-');
            if (parts.Length < 2) return false;

            bool anyUpper = false;
            foreach (string part in parts)
            {
                if (part.Length == 0) return false;            // leading/trailing/double hyphen => not a code
                foreach (char c in part)
                    if (!char.IsLetter(c)) return false;       // any non-letter segment => not an alphabetic code
                if (part.Any(char.IsUpper)) anyUpper = true;
            }
            return anyUpper; // require an uppercase part so plain lowercase hyphenated words (что-то) are not caught
        }
    }
}
