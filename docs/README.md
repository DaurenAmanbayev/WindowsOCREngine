# Project docs

Engineering notes captured during the post-processing / scan-tuning work (June 2026).

- **[findings.md](findings.md)** — empirical results: render-DPI sweep, the inversion no-op,
  `Windows.Media.Ocr` characteristics, what post-processing does and doesn't fix.
- **[architecture.md](architecture.md)** — the OCR text post-processing pipeline: stages, helpers,
  and the non-obvious design decisions (proving-letter homoglyph logic, protected tokens, etc.).
- **[roadmap.md](roadmap.md)** — candidate future improvements (preprocessing, output formats,
  ML correction, statistical tagging) with rough effort/value and feasibility in the .NET/offline stack.
- **[lessons.md](lessons.md)** — process lessons and gotchas worth not relearning.

These complement, not replace, the code comments and the root `README.md`.
