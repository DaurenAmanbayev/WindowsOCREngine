# OCR text post-processing — architecture & decisions

The pipeline turns raw `Windows.Media.Ocr` output into clean, well-formatted text that is safe for
**trilingual Kazakhstan content** (Kazakh + Russian Cyrillic + Latin brand/model names). It is offline,
zero-NuGet, and runs at the single choke point `PowerOcrEngine.ExtractTextAsync` (per page/image).

## Layout

`WindowsImagePdfOcrApp/PostProcessing/`

- `ITextPostProcessor` — stage contract `string Process(string, PostProcessingContext)`. Pure + idempotent.
- `TextPostProcessingPipeline` — ordered stages; `CreateDefault(options)` builds the core chain.
- `PostProcessingOptions` — per-stage toggles (core on; opinionated quote/dash transforms off).
- `PostProcessingContext` — selected language, `IsSpaceJoiningLanguage`, brand allowlist.
- Helpers: `ScriptClassifier`, `Homoglyphs`, `ProtectedTokens`, `TextLines`.
- `Stages/` — `WhitespaceNormalizer`, `PunctuationNormalizer`, `DeHyphenator`, `HomoglyphFixer`,
  `ParagraphReconstructor`.

Language selection: `LanguageSelector.PickBest(preferred)` matches installed recognizers by **primary
subtag, case-insensitively**, priority `kk → ru → en`; `-lang` overrides; warns if neither kk nor ru.

## Stage order (load-bearing)

`Whitespace → Punctuation → DeHyphenator → Homoglyph → Paragraph`

## Key decisions (the non-obvious "why")

- **Three-class script model** (`ScriptClassifier`): Cyrillic (incl. Kazakh `ә ғ қ ң ө ұ ү һ і` via
  Unicode block `U+0400–U+052F`), Latin, Common. **Never `[а-я]` ranges.** Case via `char.IsUpper/IsLower`
  so Kazakh works in de-hyphenation/paragraphs.

- **Protected tokens** (`ProtectedTokens`, rules a–e): brand/model/code tokens pass transforms untouched
  — digit+letter (`A52`), camelCase (`iPhone`), ALL-CAPS 2–6 (`USB`), hyphen-code (`USB-C`), allowlist.
  Rule (c) ALL-CAPS is **scoped to structural stages and excluded from `HomoglyphFixer`**, so mixed-script
  caps headings (`ГOCT → ГОСТ`, `USВ → USB`) still get fixed.

- **Homoglyph dominance by *proving letters*, not letter count** (`Homoglyphs`, `HomoglyphFixer`):
  - A *proving letter* is unambiguous (not half of a confusable pair, not in the `i/и/і` cluster).
  - Dominant script = the one with a proving letter; convert the other script's look-alikes into it;
    **abstain** if both or neither prove. Counting fails on caps headings (`ГOCT` is 1 Cyr vs 3 Lat →
    would mis-pick Latin and abstain). Proving-letter fixes all cases.
  - The proving set is **derived as a complement** of the confusable map at runtime, never hand-listed
    (a hand-list silently drifts, e.g. forgetting `у`/`У` from the `y↔у` pair).
  - The **`i/и/і` cluster is frozen** (Latin i / Russian и / Kazakh і) — never auto-rewritten, so
    `кітап` ≠ `китап`. Disambiguation is deferred to a future Kazakh-dictionary spell-check.

- **Initials stay tight** (`WhitespaceNormalizer`): the period-space rule uses a negative lookbehind
  `(?<!\b\p{Lu})([.!?])(\p{Lu})`, so `А.Д.` is left as-is while real sentence breaks (`конец.Начало`,
  `США.Затем`) still get a space. Russian abbreviations/decimals (`т.д.`, `3.14`, `3,14`, `10:30`) are
  also protected (space only before an uppercase letter / only after `,;:` before a letter).

- **Soft-hyphen ordering**: `WhitespaceNormalizer` strips control/zero-width chars but **preserves
  U+00AD**, which `DeHyphenator` needs to heal line-break hyphenation, then strips strays.

- **De-hyphenation code guard**: a short Latin run + hyphen + Latin run (`Wi-\nfi`) is **not** fused
  (would destroy a code); body-text Cyrillic de-hyphenation (`пробле-\nма → проблема`) still works.

- **Paragraph reconstruction is conservative**: merge a wrapped line into the previous one only when the
  previous line lacks sentence-ending punctuation and the current line starts lowercase; list/heading
  markers are hard breaks. (Limitation: a continuation that starts with a capital — e.g. a proper noun —
  is not merged.)

## Integration points

- `PowerOcrEngine` runs the pipeline per page/image, after `ProcessOcrResult`, before text leaves the
  engine. PDF page separators (`--- <label> N ---`, label localized by language + `-pagelabel`) are added
  later in `PdfProcessor`, so the pipeline never sees them.
- Image preprocessing in `PowerOcrEngine`: pad → **conditional** invert (dark images only) → ×2 scale
  (skipped when already large) → recognize. See `docs/findings.md` for the DPI/inversion rationale.

## Validation

Stages are pure string functions. The homoglyph/protected-token/de-hyphenation/initials logic was
validated by porting it to Python and running the acceptance cases (the C# app itself only builds on
Windows). A proper xUnit project is a recommended next step (see roadmap).
