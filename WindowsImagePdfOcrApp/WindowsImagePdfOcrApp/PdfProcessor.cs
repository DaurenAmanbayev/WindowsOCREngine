using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WindowsImagePdfOcrApp
{
    public class PdfProcessor
    {
        private readonly PowerOcrEngine _ocrEngine;
        private readonly string _pageLabel;
        private readonly int _renderDpi;

        public PdfProcessor(PowerOcrEngine engine, string pageLabel = "Page", int renderDpi = 150)
        {
            _ocrEngine = engine;
            _pageLabel = string.IsNullOrWhiteSpace(pageLabel) ? "Page" : pageLabel;
            _renderDpi = renderDpi;
        }

        public async Task<string> ProcessPdfAsync(string pdfPath)
        {
            // 1. Validate path and get the file via Windows Storage API
            // WinRT requires an absolute path
            string fullPath = Path.GetFullPath(pdfPath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("PDF not found", fullPath);

            StorageFile file = await StorageFile.GetFileFromPathAsync(fullPath);

            // 2. Load the PDF document
            PdfDocument pdfDoc = await PdfDocument.LoadFromFileAsync(file);

            StringBuilder fullText = new StringBuilder();
            Console.WriteLine($"Pages found: {pdfDoc.PageCount}");

            // 3. Process each page
            for (uint i = 0; i < pdfDoc.PageCount; i++)
            {
                Console.WriteLine($"Processing page {i + 1} of {pdfDoc.PageCount}...");

                using (var page = pdfDoc.GetPage(i))
                {
                    // Render the page into an in-memory stream
                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        // Render at the target DPI for sharper glyphs (native is ~96 DPI, too low for
                        // scans). page.Size is in 96-DPI units, so scale = dpi/96.
                        var options = new PdfPageRenderOptions();
                        double scale = _renderDpi / 96.0;
                        uint w = (uint)Math.Round(page.Size.Width * scale);
                        uint h = (uint)Math.Round(page.Size.Height * scale);

                        // Clamp proportionally to the OCR engine's max bitmap dimension.
                        uint maxDim = OcrEngine.MaxImageDimension;
                        if (w > maxDim || h > maxDim)
                        {
                            double fit = Math.Min((double)maxDim / w, (double)maxDim / h);
                            w = (uint)(w * fit);
                            h = (uint)(h * fit);
                        }
                        options.DestinationWidth = w;
                        options.DestinationHeight = h; // both set proportionally → aspect preserved

                        await page.RenderToStreamAsync(stream, options);

                        // Convert WinRT stream to .NET Bitmap
                        using (var bitmap = await StreamToBitmap(stream))
                        {
                            // 4. Call our powerful OCR engine (V12)
                            string pageText = await _ocrEngine.ExtractTextAsync(bitmap);

                            fullText.AppendLine($"--- {_pageLabel} {i + 1} ---");
                            fullText.AppendLine(pageText);
                            fullText.AppendLine();
                        }
                    }
                }
            }

            return fullText.ToString();
        }

        // Helper for stream conversion
        private async Task<Bitmap> StreamToBitmap(IRandomAccessStream winRtStream)
        {
            // Copy data from WinRT stream into a .NET MemoryStream
            using (var netStream = new MemoryStream())
            {
                var reader = new DataReader(winRtStream.GetInputStreamAt(0));
                await reader.LoadAsync((uint)winRtStream.Size);
                byte[] buffer = new byte[winRtStream.Size];
                reader.ReadBytes(buffer);

                await netStream.WriteAsync(buffer, 0, buffer.Length);
                netStream.Position = 0;

                return new Bitmap(netStream);
            }
        }
    }
}
