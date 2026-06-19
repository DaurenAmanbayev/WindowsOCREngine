using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using WindowsImagePdfOcrApp.PostProcessing;

namespace WindowsImagePdfOcrApp
{
    public class PowerOcrEngine
    {
        private readonly OcrEngine _engine;
        private readonly bool _isSpaceJoiningLanguage;
        private readonly TextPostProcessingPipeline _postProcessor;
        private readonly PostProcessingContext _postContext;

        public PowerOcrEngine(string? languageTag = null, PostProcessingOptions? options = null)
        {
            // Initialization (Microsoft Original)
            if (string.IsNullOrEmpty(languageTag))
                languageTag = System.Globalization.CultureInfo.CurrentCulture.Name;

            Language selectedLanguage = new Language(languageTag);

            if (!OcrEngine.IsLanguageSupported(selectedLanguage))
            {
                var available = OcrEngine.AvailableRecognizerLanguages;
                var fallback = available.FirstOrDefault(l => l.LanguageTag.StartsWith(selectedLanguage.LanguageTag.Split('-')[0]))
                               ?? available.FirstOrDefault();
                if (fallback == null) throw new InvalidOperationException("OCR языки не найдены.");
                selectedLanguage = fallback;
            }

            _engine = OcrEngine.TryCreateFromLanguage(selectedLanguage);
            _isSpaceJoiningLanguage = IsLanguageSpaceJoining(selectedLanguage);

            // Post-processing pipeline (runs after every page/image, before text leaves the engine).
            _postContext = new PostProcessingContext(selectedLanguage.LanguageTag, _isSpaceJoiningLanguage, options ?? new PostProcessingOptions());
            _postProcessor = TextPostProcessingPipeline.CreateDefault(_postContext.Options);
        }

        public async Task<string> ExtractTextAsync(string imagePath)
        {
            if (!File.Exists(imagePath)) throw new FileNotFoundException("Файл не найден", imagePath);
            using var bmp = new Bitmap(imagePath);
            return await ExtractTextAsync(bmp);
        }

        public async Task<string> ExtractTextAsync(Bitmap inputBmp)
        {
            // 1. Padding (Microsoft Original)
            // We must add a border
            using var paddedBmp = PadImage(inputBmp);

            // 2. INVERT (TUNING #1) — ONLY for dark-background images (e.g. dark-mode screenshots),
            // where inversion turns "white on gray" into "black on white" and boosts small light-on-dark
            // fonts. A normal black-on-white scan must NOT be inverted (that yields white-on-black, which
            // the OCR engine reads worse — it expects dark text on a light background). Auto-detect by
            // average luminance so both scenarios work.
            // Decide on the raw input (the white padding border would bias the average toward "light").
            using var preparedBmp = IsDarkBackground(inputBmp) ? InvertColors(paddedBmp) : new Bitmap(paddedBmp);

            // 3. Scale 2.0 (TUNING #2)
            // Was 1.5 -> Now 2.0.
            // Fixes the "Лдти" (Идти) mistake.
            // BUT only upscale images that are still small. When the page was already rendered at a high
            // DPI (e.g. PdfProcessor renders PDFs at 300 DPI), a second ×2 double-upscales and blurs thin
            // Cyrillic strokes (телекоммуникаций -> телекоммунржащй). So skip ×2 once the image is large.
            const int alreadyHighResPx = 1600;
            double scaleFactor = 2.0;

            bool performScale = Math.Max(preparedBmp.Width, preparedBmp.Height) < alreadyHighResPx;
            if (performScale &&
                (preparedBmp.Width * scaleFactor > OcrEngine.MaxImageDimension ||
                 preparedBmp.Height * scaleFactor > OcrEngine.MaxImageDimension))
            {
                performScale = false;
            }

            using var finalBmp = performScale ? ScaleBitmapUniform(preparedBmp, scaleFactor) : new Bitmap(preparedBmp);

            // 4. Recognition
            using var softwareBitmap = await ConvertToSoftwareBitmap(finalBmp);
            var result = await _engine.RecognizeAsync(softwareBitmap);

            // 5. Assemble raw text, then run the universal post-processing pipeline.
            return _postProcessor.Process(ProcessOcrResult(result), _postContext);
        }

        // --- Helper: detect a dark background (i.e. the image needs inversion) ---
        private static bool IsDarkBackground(Bitmap bmp)
        {
            try
            {
                // Average luminance over a coarse grid (fast even on large high-DPI bitmaps). A normal
                // black-on-white document averages bright; a dark-mode screenshot averages dark.
                int stepX = Math.Max(1, bmp.Width / 100);
                int stepY = Math.Max(1, bmp.Height / 100);
                double sum = 0;
                int count = 0;
                for (int y = 0; y < bmp.Height; y += stepY)
                {
                    for (int x = 0; x < bmp.Width; x += stepX)
                    {
                        Color c = bmp.GetPixel(x, y);
                        sum += 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                        count++;
                    }
                }
                return count > 0 && (sum / count) < 128.0;
            }
            catch
            {
                // Unsupported (e.g. indexed) pixel format: assume a light background and don't invert.
                return false;
            }
        }

        // --- Helper: Invert Colors (Simple math) ---
        private Bitmap InvertColors(Bitmap original)
        {
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);
            newBitmap.SetResolution(original.HorizontalResolution, original.VerticalResolution);

            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                // Inversion matrix
                ColorMatrix colorMatrix = new ColorMatrix(
                   new float[][]
                   {
                      new float[] {-1, 0, 0, 0, 0},
                      new float[] {0, -1, 0, 0, 0},
                      new float[] {0, 0, -1, 0, 0},
                      new float[] {0, 0, 0, 1, 0},
                      new float[] {1, 1, 1, 0, 1}
                   });

                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                        0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            return newBitmap;
        }

        // --- Microsoft Original: PadImage ---
        private static Bitmap PadImage(Bitmap image, int minW = 64, int minH = 64)
        {
            int width = Math.Max(image.Width + 16, minW + 16);
            int height = Math.Max(image.Height + 16, minH + 16);

            Bitmap destination = new Bitmap(width, height, image.PixelFormat);
            destination.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (Graphics g = Graphics.FromImage(destination))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                int x = (width - image.Width) / 2;
                int y = (height - image.Height) / 2;
                g.DrawImage(image, x, y, image.Width, image.Height);
            }
            return destination;
        }

        // --- Microsoft Original: Scale (with scale argument 2.0) ---
        private static Bitmap ScaleBitmapUniform(Bitmap image, double scale)
        {
            int newWidth = (int)(image.Width * scale);
            int newHeight = (int)(image.Height * scale);

            Bitmap destination = new Bitmap(newWidth, newHeight, image.PixelFormat);
            destination.SetResolution(96, 96); // Fix DPI for stability

            using (Graphics g = Graphics.FromImage(destination))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;

                g.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return destination;
        }

        private async Task<SoftwareBitmap> ConvertToSoftwareBitmap(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
            return await decoder.GetSoftwareBitmapAsync();
        }

        private string ProcessOcrResult(OcrResult result)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var line in result.Lines)
            {
                if (_isSpaceJoiningLanguage) sb.AppendLine(line.Text);
                else sb.AppendLine(string.Join(" ", line.Words.Select(w => w.Text)));
            }
            return sb.ToString().Trim();
        }

        private static bool IsLanguageSpaceJoining(Language language)
        {
            return language.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase) ||
                   language.LanguageTag.Equals("ja", StringComparison.OrdinalIgnoreCase);
        }
    }
}
