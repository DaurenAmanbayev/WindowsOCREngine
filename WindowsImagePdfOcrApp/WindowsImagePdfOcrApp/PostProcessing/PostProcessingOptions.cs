namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Per-stage on/off toggles plus a few conservative sub-options. All core stages are ON by
    /// default; the opinionated punctuation transforms are OFF by default. A future CLI / appsettings
    /// layer can map straight onto this POCO.
    /// </summary>
    public sealed class PostProcessingOptions
    {
        // Core stages (on by default).
        public bool WhitespaceNormalization { get; set; } = true;
        public bool PunctuationNormalization { get; set; } = true;
        public bool DeHyphenation { get; set; } = true;
        public bool HomoglyphCorrection { get; set; } = true;
        public bool ParagraphReconstruction { get; set; } = true;

        // Opinionated punctuation sub-options (off by default — see PunctuationNormalizer notes).
        public bool NormalizeQuotes { get; set; } = false;
        public bool SpacedHyphenToDash { get; set; } = false;
    }
}
