using System.Collections.Generic;

namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// Runs an ordered list of <see cref="ITextPostProcessor"/> stages in sequence. The default chain
    /// (Whitespace → Punctuation → DeHyphenator → Homoglyph → Paragraph) is built from
    /// <see cref="PostProcessingOptions"/> toggles.
    /// </summary>
    public sealed class TextPostProcessingPipeline
    {
        private readonly IReadOnlyList<ITextPostProcessor> _stages;

        public TextPostProcessingPipeline(IReadOnlyList<ITextPostProcessor> stages)
        {
            _stages = stages;
        }

        public string Process(string text, PostProcessingContext context)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (ITextPostProcessor stage in _stages)
                text = stage.Process(text, context);
            return text;
        }

        public static TextPostProcessingPipeline CreateDefault(PostProcessingOptions options)
        {
            options ??= new PostProcessingOptions();
            var stages = new List<ITextPostProcessor>();

            if (options.WhitespaceNormalization) stages.Add(new WhitespaceNormalizer());
            if (options.PunctuationNormalization) stages.Add(new PunctuationNormalizer());
            if (options.DeHyphenation) stages.Add(new DeHyphenator());
            if (options.HomoglyphCorrection) stages.Add(new HomoglyphFixer());
            if (options.ParagraphReconstruction) stages.Add(new ParagraphReconstructor());

            return new TextPostProcessingPipeline(stages);
        }
    }
}
