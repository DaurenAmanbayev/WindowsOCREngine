namespace WindowsImagePdfOcrApp.PostProcessing
{
    /// <summary>
    /// A single text post-processing stage. Stages are pure: same input + context => same output,
    /// and they must be idempotent (f(f(x)) == f(x)). The public contract is string -> string.
    /// </summary>
    public interface ITextPostProcessor
    {
        /// <summary>Short stage name (diagnostics only).</summary>
        string Name { get; }

        /// <summary>Transform <paramref name="text"/>. Must never throw on normal input.</summary>
        string Process(string text, PostProcessingContext context);
    }
}
