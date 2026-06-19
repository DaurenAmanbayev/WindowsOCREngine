using System;
using System.Collections.Generic;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Immutable context threaded through every stage. Carries the language actually selected by the
    /// OCR engine (not a hardcoded default), the space-joining flag (zh/ja), the brand allowlist used
    /// by <see cref="ProtectedTokens"/>, and the per-stage options.
    /// </summary>
    public sealed class PostProcessingContext
    {
        public string PrimaryLanguage { get; }
        public bool IsSpaceJoiningLanguage { get; }
        public ISet<string> BrandAllowlist { get; }
        public PostProcessingOptions Options { get; }

        public PostProcessingContext(
            string? primaryLanguage,
            bool isSpaceJoiningLanguage,
            PostProcessingOptions? options = null,
            ISet<string>? brandAllowlist = null)
        {
            PrimaryLanguage = primaryLanguage ?? string.Empty;
            IsSpaceJoiningLanguage = isSpaceJoiningLanguage;
            Options = options ?? new PostProcessingOptions();
            BrandAllowlist = brandAllowlist ?? DefaultBrandAllowlist;
        }

        /// <summary>
        /// Small, extensible allowlist of brand / product tokens that must survive every transform.
        /// Compared case-insensitively. Tuning point for brand/model-heavy corpora.
        /// </summary>
        public static readonly ISet<string> DefaultBrandAllowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "iPhone", "iPad", "iMac", "iOS", "macOS", "Apple", "AirPods", "Watch",
            "Samsung", "Galaxy", "Huawei", "Honor", "Xiaomi", "Redmi", "Poco", "Realme",
            "Oppo", "Vivo", "Nokia", "Motorola", "OnePlus", "Google", "Pixel",
            "Sony", "LG", "Lenovo", "Asus", "Acer", "Dell", "HP", "MSI",
            "Intel", "AMD", "Nvidia", "GeForce", "Radeon", "Ryzen", "Core",
            "Bluetooth", "Wi-Fi", "USB", "HDMI", "Type-C", "Android", "Windows",
        };
    }
}
