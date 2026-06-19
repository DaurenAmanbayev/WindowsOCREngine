# WindowsOCREngine

A small command-line and library tool that performs OCR on PDF pages and image files using Windows built-in OCR (Windows.Media.Ocr). The application renders PDF pages, preprocesses images (padding, inversion, scaling), and extracts text using the Windows OCR engine. The project is implemented in C# targeting .NET 8 and is designed to run on Windows (desktop) environments that support WinRT OCR APIs.

## Features

- OCR for PDF documents (page-by-page rendering)
- OCR for common image formats: PNG, JPEG, BMP, TIFF, GIF
- Preprocessing tuned for better accuracy: padding, color inversion, uniform scaling
- Reusable OCR engine wrapper with language selection (example: `ru-RU`)
- Saves extracted text to a `.txt` file next to the input file

## Prerequisites

- Windows 10 (version 1809+) or Windows 11 required for WinRT OCR APIs
- .NET 8 SDK
- Visual Studio 2022 (recommended) or newer with .NET desktop development workload

**Note:** The project uses WinRT APIs (`Windows.Media.Ocr`, `Windows.Data.Pdf`, `Windows.Graphics.Imaging`) and therefore must run on Windows. Running on non-Windows platforms is not supported.

## Build

1. Clone the repository:

    ```bash
    git clone https://github.com/your/repo.git
    cd WindowsImagePdfOcr
    ```

2. Open the solution in Visual Studio 2022.
3. Restore NuGet packages and build the solution (Build > Build Solution).

Alternatively, build from the command line with the .NET SDK:


dotnet build -c Release


## Run

Usage (console):


WindowsImagePdfOcrApp.exe [-lang <tag>] [-pagelabel <text>] [-dpi <n>] "C:\path\to\file.pdf"


If no path is provided, the app uses a test path configured in `Program.cs`. All flags accept the forms
`-flag <value>`, `--flag <value>`, `-flag=<value>` and `--flag=<value>`, in any position.

- **`-lang <tag>`** — recognition-language override. Without it, the app auto-selects the first installed
  recognizer in priority order **kk → ru → en**. Matched by primary subtag, so `-lang ru`, `-lang RU`
  and `-lang ru-RU` are equivalent. If the requested language is not installed, a warning is printed and
  the app falls back to auto-selection.
- **`-pagelabel <text>`** — word used in the PDF page separator `--- <text> N ---`. Default depends on
  the OCR language: `Страница` (ru), `Бет` (kk), `Page` (otherwise).
- **`-dpi <n>`** — PDF render resolution (default **150**, clamped to 72–600). 150 is the sweet spot for
  typical A4 document scans (clean native render, best body-text recognition). Very high DPI enlarges
  glyphs and can actually *hurt* prose recognition, so raise it only for unusually small fonts. Affects
  PDF input only.

### Examples:

- OCR a PDF:

    ```bash
    WindowsImagePdfOcrApp.exe "C:\Scans\document.pdf"
    ```

- OCR an image:

    ```bash
    WindowsImagePdfOcrApp.exe "C:\Images\scan.png"
    ```

- Force Kazakh recognition:

    ```bash
    WindowsImagePdfOcrApp.exe -lang kk-KZ "C:\Scans\document.pdf"
    ```

- Russian scan at 400 DPI with a custom page label:

    ```bash
    WindowsImagePdfOcrApp.exe -lang ru-RU -dpi 400 -pagelabel "Лист" "C:\Scans\document.pdf"
    ```

The extracted text will be saved to `file.pdf.txt` or `scan.png.txt` next to the source file.

## Configuration

- **Language:** By default the app auto-selects the OCR recognizer in priority order **kk → ru → en** (suited to trilingual Kazakh/Russian/Latin content). Override it from the command line with `-lang <tag>` (e.g. `-lang kk-KZ`). If the requested/auto language is not installed, the engine falls back to an available recognizer.
- **Page separator:** Multi-page PDFs are split with `--- <label> N ---`. The label defaults to the OCR language (`Страница`/`Бет`/`Page`) and can be overridden with `-pagelabel <text>`.
- **Render DPI:** PDF pages are rasterized at `-dpi <n>` (default 150) before OCR. 150 gives the best balance on A4 document scans; higher values enlarge text and can degrade prose recognition with the Windows OCR engine, so only raise it for very small fonts. Configurable per run; the rasterization code lives in `PdfProcessor.cs`.
- **Scaling:** The engine applies a scaling factor (default 2.0) before recognition. You can change scaling or pre-processing in `PowerOcrEngine.cs`.

## Project layout

- `Program.cs` CLI entry point, detects file type and orchestrates processing
- `PdfProcessor.cs` Loads PDF, renders pages via `Windows.Data.Pdf`, converts pages to bitmaps
- `ImageProcessor.cs` Validates and loads images
- `PowerOcrEngine.cs` Image preprocessing (pad, invert, scale), converts to `SoftwareBitmap`, calls `OcrEngine`

## Performance and optimization tips

- **Parallelize PDF page processing** when working with multi-page PDFs. Use a limited concurrency (for example `Environment.ProcessorCount`) to avoid saturating CPU and IO.
- **Reduce render DPI** (or scale) for large documents if OCR quality remains acceptable � lower resolution reduces memory usage and recognition time.
- **Avoid unnecessary conversions:** prefer `SoftwareBitmap`/WinRT types where possible to reduce copying between managed and native buffers.
- **Reuse OCR engine instances** where thread-safety permits. If `OcrEngine` is not thread-safe, implement a pool of engines to reduce initialization overhead.
- **Add timing instrumentation** (Stopwatch) around rendering, conversion, and OCR steps to identify bottlenecks.

## Troubleshooting

- **File not found:** Ensure the full absolute path is provided and the app has read permissions.
- **Unsupported image:** Only common image formats are supported. Corrupted images may throw `ArgumentException` during load.
- **Missing OCR languages:** Install the required language packs for Windows if your language is not available to `OcrEngine`.
