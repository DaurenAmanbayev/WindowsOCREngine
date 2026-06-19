# Roadmap — candidate improvements

Exploratory notes (not commitments). Grouped by goal, with rough effort/value and feasibility in the
current **.NET 8 / Windows / offline / zero-dependency** stack.

## Top picks (highest ROI)

1. **Quality metric (CER/WER) + a tiny ground-truth set.** We tuned DPI by eyeballing across many
   rounds — that does not scale. A handful of hand-verified pages + a CER/WER script makes every later
   change (DPI, engine, preprocessing, stage) measurable instead of guessed. **Build this first** — it
   underpins everything else.

2. **Image preprocessing: deskew / denoise / adaptive binarization / auto-rotate.** The biggest untouched
   quality lever for real scans (currently only pad + invert + scale). Classic CV, **no ML**; often beats
   any post-correction because many errors come from thin strokes on a noisy/skewed background.

3. **Batch / folder processing (+ parallel pages).** Today it is one file per invocation. Accept a
   folder/glob, process recursively with progress + summary. Parallelize pages with an engine pool
   (`OcrEngine` is not thread-safe). Plus a `-out <path>` flag to control output location/name.

4. **Searchable PDF output.** Highest user value for document management: overlay the recognized text on
   the original scan → a PDF you can search/select while keeping the image. Requires the word
   **bounding boxes** currently discarded in `ProcessOcrResult`.

## Catalog by category

**Recognition quality (engine / preprocessing)**
- Deskew / denoise / adaptive (Otsu/Sauvola) binarization / auto-orientation. *(top pick #2)*
- Per-page adaptive DPI (by detected text size) instead of a fixed value.
- Multi-engine: Windows OCR + **Tesseract 5 (LSTM) `rus`+`kaz`** (via `Tesseract.NET`), pick the better.
  Likely removes the `телекоммунржащй`-class errors at the source; offline, lightweight. Worth an A/B.

**Structure / layout (unblock the discarded `OcrResult` bounding boxes)**
- **Table reconstruction** by word X-coordinates — directly fixes the collapsed tables (ППР pp. 7–10).
- Multi-column reading order; header/footer/page-number stripping; heading detection by line height.

**Output formats**
- Searchable PDF *(top pick #4)*, Markdown, **JSON with coordinates**, hOCR/ALTO.
- **Entity extraction by regex** (IPs, e-mails, dates, contract numbers, device models, `nnm-*` nodes)
  → structured sidecar JSON. Cheap, high value for these technical documents.

**Post-processing extensions**
- Hunspell `ru+kk` spell-check stage (deferred `#10`): fixes light word errors, dictionary-constrained
  (safe — won't touch numbers/codes). Note: heavy garble (edit-distance 4–5) is beyond plain SymSpell.
- Abbreviation/domain dictionary (`ПАК`, `ЕСМ`, `АСУПТ`).

**UX / ergonomics**
- `appsettings.json` for defaults (language, dpi, stage toggles) instead of flags each run.
- Progress/ETA, exit codes, quiet/verbose, logging.
- GUI (WinForms/WPF — the flags are already enabled in the `.csproj`, just unused): drag-drop, preview,
  manual correction. Watch-folder daemon for auto-OCR of new files.

**Engineering / reliability**
- xUnit test project (stages are pure functions — ideal; move `PostProcessing` to a `netstandard` lib so
  tests run cross-platform).

## ML & error correction (notes from the exploration)

Two distinct injection points: **(A) post-correct the text** vs **(B) replace/augment the OCR engine**.
Heavy ML is **not** required — the cheap tiers carry most of the value. Risk note: generative correction
(seq2seq/LLM) can silently corrupt IPs/contract numbers/models in legal/technical docs — gate it with the
existing `ProtectedTokens` mechanism and a diff/human-in-the-loop.

| Approach | Weight | Hardware | Offline + .NET | Fixes | Distortion risk |
|---|---|---|---|---|---|
| Dictionary (SymSpell/Hunspell ru+kk) | 10–50 MB | CPU, µs | ✅ C# native | typos ≤2–3 edits | low |
| n-gram LM + noisy-channel | MB–GB | CPU, fast | ✅ C#/ONNX | + context | low |
| Char seq2seq (ByT5-small) | 0.2–1 GB | CPU slow / small GPU | ✅ ONNX Runtime | heavy garble | medium |
| Local LLM 3–8B (4-bit) | 2–6 GB | GPU preferred | ✅ LLamaSharp | almost all | **high** |
| Cloud LLM (Claude/API) | — | — | ❌ internet, $/call | maximum | high (manageable) |
| **(B) Tesseract LSTM `rus+kaz`** | 30–100 MB | CPU | ✅ Tesseract.NET | **root cause** | — |

**Recommendation:** fix the root first (try Tesseract `rus+kaz` A/B) before bolting on correction; then a
safe dictionary stage; reach for seq2seq/LLM only if context-aware reconstruction is genuinely needed.

## Search / tagging (statistical, notes)

Feasible **without touching the OCR app** (post-step on the `.txt` sidecars):
- If the goal is *search*: feed `.txt` to a full-text index (OpenSearch/Typesense) — BM25 +
  `significant_terms` gives statistical tag facets out of the box, zero project code.
- If the goal is *explicit tags*: YAKE / TF-IDF (bigrams) + **lemmatization** (pymorphy2/mystem) +
  a **dictionary filter** (drop OCR-garbage non-words) + ru/kk stopwords. Statistical, no training.
- Caveat: OCR noise + Russian morphology mean "pure statistics" in practice needs the dictionary/lemma
  layer, or ~10–20% of tags are OCR junk. Named entities (IPs, models, nodes) are better via regex/NER.
