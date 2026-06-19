# Empirical findings

Results from tuning the OCR pipeline on real Russian scans (notably
`ППР по тестированию системы мониторинга.pdf`, a 10-page A4 scan with no text layer).

## 1. Render DPI: 150 is the sweet spot (default), higher hurts prose

We rendered the same scan at 150 / 200 / 250 / 300 DPI (all with the internal ×2 upscale **disabled** —
see §3 — so this is a clean DPI sweep) and compared the same diagnostic lines.

| Probe | 150 | 200 | 250 | 300 |
|---|:--:|:--:|:--:|:--:|
| `ППР:` (prose) | ✅ | ✗ `ГШР` | ✗ `ГУГТР` | ✗ `ШТР` |
| `Подрядчик` ×3 | ✅✅✅ | ✗✗✅ | ✗✗✗ | ✗✗✗ |
| `инфраструктуры` | ✅✅ | ✗✅ | ✅✗ | ✗✗ |
| `Synergy 12000 Frame` | ✅ (w/ HPE) | ✅ (no HPE) | ✗ `пате` | ✗ `Пате` |
| `RAID 10 HDD` | ✅ | ✅ | ✗ | ✗ |
| contract IDs (`N…0376`+`N…3107`) | ✅ both | ~ (lost 3107) | ~ (lost 3107) | ✅ both |
| `PowerEdge R6525` | ~ `R652S` | ✗ `R6S2S` | ✅ | ✅ |

**Conclusion:** body prose degrades monotonically as DPI rises (thin Cyrillic strokes и/н/ш/щ/р blur as
glyphs enlarge), while technical data (codes, IPs, model/contract numbers) is roughly flat — **already
good at 150**. 300 DPI is *not* "higher quality"; it shifts text into a size the engine reads worse.

**Default is now 150 DPI** (`Program.cs`, `PdfProcessor` ctor). Override per run with `-dpi`. 150 is also
near the practical floor for a "clean native render": on A4 it is ~1754 px, just above the ×2-skip
threshold (§3); below ~137 DPI the internal ×2 re-engages and blurs everything again.

> Caveat: derived from one typical A4 document. Very small fonts / faxes may prefer higher DPI — hence
> `-dpi` stays a flag.

## 2. Inversion is a no-op for high-contrast scans (hypothesis disproved)

`PowerOcrEngine.InvertColors` was applied unconditionally (originally added for dark-mode screenshots:
"turn white-on-gray into black-on-white"). We hypothesized that inverting a normal black-on-white scan
to white-on-black was hurting prose, and made inversion conditional on `IsDarkBackground` (average
luminance < 128).

**Result: byte-identical output** before/after the change on this scan. `Windows.Media.Ocr` is
effectively **polarity-invariant on high-contrast text** — it reads black-on-white and white-on-black the
same. Inversion only matters for **low-contrast dark-mode** images (where the matrix also changes
contrast). So inversion was **not** the cause of the prose noise; DPI was (§1).

The conditional-inversion change was kept anyway (harmless and logically correct — skip a pointless
invert), but it is **not** a quality lever here.

## 3. The internal ×2 upscale must not double-up on a high-DPI render

`PowerOcrEngine` upscales ×2.0 to help small inputs. When the page is already rendered at a high DPI,
a second ×2 **double-upscales** and blurs thin strokes (`телекоммуникаций` → `телекоммунржащй`). Fix:
skip ×2 once the image is already large (`max(W,H) ≥ 1600 px`). Small images/screenshots and low-DPI
renders still get ×2 (old behavior). This fix is what made **low DPI viable** (clean native 150 instead
of a blurry 96×2).

## 4. `Windows.Media.Ocr` characteristics (learned)

- **No per-word confidence scores** (the API does not expose them) — confidence filtering is impossible.
- **Has per-word bounding boxes** (`OcrWord.BoundingRect`), currently **discarded** in
  `PowerOcrEngine.ProcessOcrResult` when flattening to plain text. Unlocking these enables tables,
  searchable PDF, layout, JSON (see roadmap).
- **Polarity-robust** on high-contrast text (§2).
- **Reads best at ~150 DPI** for body Cyrillic; degrades on enlarged text.
- **Trade-off by content:** dense small content (codes, numbers, diagram labels) benefits from more
  pixels per glyph; ordinary prose prefers smaller. There is no single DPI that is best at both.

## 5. What post-processing does and doesn't fix

Fixes well (deterministic, safe): line-wrap → paragraph reconstruction, spacing/punctuation, hyphenated
line-breaks, Cyrillic/Latin **inter-alphabet** homoglyphs (brand/code-safe).

Does **not** fix (out of scope by design): intra-script shape errors (`телекоммуникаций` →
`телекоммунржащй`, `nnm` → `ппт`), digit/letter confusions (`Netflow` → `Netf10w`), collapsed table
structure. These are OCR-layer problems — addressable only by better recognition (preprocessing / a
different engine) or a dictionary/LLM stage, not by text post-processing.
