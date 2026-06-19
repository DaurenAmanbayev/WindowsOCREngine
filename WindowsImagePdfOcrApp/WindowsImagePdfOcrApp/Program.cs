using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using WindowsImagePdfOcrApp.PostProcessing;

namespace WindowsImagePdfOcrApp
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // 1. Configure encoding for correct display of Cyrillic characters in the console
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // 2. Parse arguments: optional flags + a positional file path.
            //    Flags (aliases: --x, -x=<v>, --x=<v>):
            //      -lang <tag>        recognition language override (default auto kk -> ru -> en)
            //      -pagelabel <text>  PDF page separator word (default by language: Страница/Бет/Page)
            //      -dpi <n>           PDF render DPI (default 150)
            //    Examples: OcrTool.exe -lang ru-RU -dpi 300 "C:\Path\File.pdf"
            string? inputPath = null;
            string? langArg = null;
            string? pageLabelArg = null;
            int? dpiArg = null;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("-lang", StringComparison.OrdinalIgnoreCase) || arg.Equals("--lang", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) langArg = args[++i];
                    else Console.Error.WriteLine("[ERROR] -lang requires a language tag, e.g. -lang ru-RU");
                }
                else if (arg.StartsWith("-lang=", StringComparison.OrdinalIgnoreCase))
                {
                    langArg = arg.Substring("-lang=".Length);
                }
                else if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                {
                    langArg = arg.Substring("--lang=".Length);
                }
                else if (arg.Equals("-pagelabel", StringComparison.OrdinalIgnoreCase) || arg.Equals("--pagelabel", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) pageLabelArg = args[++i];
                    else Console.Error.WriteLine("[ERROR] -pagelabel requires a value, e.g. -pagelabel Страница");
                }
                else if (arg.StartsWith("-pagelabel=", StringComparison.OrdinalIgnoreCase))
                {
                    pageLabelArg = arg.Substring("-pagelabel=".Length);
                }
                else if (arg.StartsWith("--pagelabel=", StringComparison.OrdinalIgnoreCase))
                {
                    pageLabelArg = arg.Substring("--pagelabel=".Length);
                }
                else if (arg.Equals("-dpi", StringComparison.OrdinalIgnoreCase) || arg.Equals("--dpi", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int dpiVal)) { dpiArg = dpiVal; i++; }
                    else Console.Error.WriteLine("[ERROR] -dpi requires an integer, e.g. -dpi 300");
                }
                else if (arg.StartsWith("-dpi=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--dpi=", StringComparison.OrdinalIgnoreCase))
                {
                    string dpiText = arg.Substring(arg.IndexOf('=') + 1);
                    if (int.TryParse(dpiText, out int dpiVal)) dpiArg = dpiVal;
                    else Console.Error.WriteLine("[ERROR] -dpi requires an integer, e.g. -dpi=300");
                }
                else if (inputPath == null)
                {
                    inputPath = arg;
                }
            }
            inputPath ??= @"C:\Test\scan.pdf";

            Console.WriteLine($"=== Starting OCR Tool ===");
            Console.WriteLine($"Input file: {inputPath}");

            try
            {
                // 3. Initialize engine core (V12 - Invert + Scale 2.0)
                // Use -lang if supplied; otherwise auto-pick for trilingual KZ content (kk → ru → en).
                var options = new PostProcessingOptions();
                string language = LanguageSelector.PickBest(langArg);
                string pageLabel = pageLabelArg ?? PageLabelForLanguage(language);
                // Default 150 DPI: on A4 document scans it lands just above the ×2-skip threshold
                // (~1754px) for a clean native render, and the OCR engine reads body Cyrillic best at
                // this size. Higher DPI enlarges glyphs and blurs thin strokes (и/н/ш/щ), hurting prose
                // without improving the already-good technical data. Override per run with -dpi.
                int dpi = Math.Clamp(dpiArg ?? 150, 72, 600);
                Console.WriteLine($"OCR language: {language} | page label: {pageLabel} | render DPI: {dpi}");
                var engine = new PowerOcrEngine(language, options);

                // 4. Initialize processors
                var pdfProcessor = new PdfProcessor(engine, pageLabel, dpi);
                var imageProcessor = new ImageProcessor(engine);

                // 5. Determine file type and route processing
                string extension = Path.GetExtension(inputPath).ToLowerInvariant();

                if (extension == ".pdf")
                {
                    // Branch for PDFs
                    await ProcessPdfDocument(pdfProcessor, inputPath);
                }
                else
                {
                    // Branch for images (png, jpg, bmp, etc.)
                    await ProcessImageFile(imageProcessor, inputPath);
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"\n[ERROR] File not found: {ex.FileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[CRITICAL ERROR]: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }

        // --- METHOD 1: PDF Processing ---
        private static async Task ProcessPdfDocument(PdfProcessor processor, string filePath)
        {
            Console.WriteLine(">>> PDF document detected. Starting page-by-page rendering...");

            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Call PDF parsing logic
            string extractedText = await processor.ProcessPdfAsync(filePath);

            watch.Stop();
            Console.WriteLine($">>> Processing completed in {watch.Elapsed.TotalSeconds:F2} sec.");

            // Save and output
            SaveResult(filePath, extractedText);
        }

        // --- METHOD 2: Image Processing ---
        private static async Task ProcessImageFile(ImageProcessor processor, string filePath)
        {
            Console.WriteLine(">>> Image detected. Starting preprocessing and OCR...");

            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Call image parsing logic (validation -> bitmap -> OCR)
            string extractedText = await processor.ProcessImageAsync(filePath);

            watch.Stop();
            Console.WriteLine($">>> Processing completed in {watch.Elapsed.TotalSeconds:F2} sec.");

            // Save and output
            SaveResult(filePath, extractedText);
        }

        // --- Helper method to save the result ---
        private static void SaveResult(string originalPath, string text)
        {
            Console.WriteLine("\n--- BEGIN RESULT ---");
            // Print the first 500 characters to the console to avoid clutter when text is very large
            Console.WriteLine(text.Length > 500 ? text.Substring(0, 500) + "\n... [text truncated for console] ..." : text);
            Console.WriteLine("--- END RESULT ---");

            string outputPath = originalPath + ".txt";
            File.WriteAllText(outputPath, text);
            Console.WriteLine($"\n[SUCCESS] Full text saved to file: {outputPath}");
        }

        // Localized default page-separator label, keyed off the selected OCR language.
        private static string PageLabelForLanguage(string tag) => tag.Split('-')[0].ToLowerInvariant() switch
        {
            "ru" => "Страница",
            "kk" => "Бет",
            _ => "Page",
        };
    }
}
