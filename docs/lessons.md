# Process lessons & gotchas

Worth not relearning.

## Measure, don't eyeball
We compared DPI variants by reading lines side by side over many rounds. It worked but was slow and
subjective. **Build a CER/WER eval harness early** (see roadmap) so quality changes are numbers, not
impressions. This is the single biggest process lesson of the session.

## Test hypotheses before declaring them fixes
The "unconditional inversion hurts prose" hypothesis was plausible and confidently stated — and **wrong**.
The experiment (byte-identical output after making inversion conditional) disproved it: `Windows.Media.Ocr`
is polarity-invariant on high-contrast text. Lesson: ship a change as an *experiment*, then measure;
don't present the hypothesis as the conclusion.

## `dotnet build` ≠ run
A "rerun" produced output identical to the previous run because the **old binary** was still being
executed (build not picked up / stale exe). Verify the DLL timestamp, and prefer `dotnet run -c Release`
or run the freshly built exe path explicitly.

## "Same byte count" ≠ "same content" — but matching lines do
Cyrillic chars are 2 bytes in UTF-8, so a substitution like `ППР`→`ШТР` keeps the byte count. A diff of
**content** (not file size) is the source of truth for "what changed". In this session the byte-identical
size *plus* identical sampled lines is what revealed the inversion no-op.

## Adversarial plan review caught real bugs
The homoglyph design went through several rounds of critique before any code. That caught genuine
correctness bugs while they were still cheap: proving-letter dominance vs. naive letter count; the
soft-hyphen vs. control-char strip ordering; the incomplete/asymmetric homoglyph map; the Russian
abbreviation/decimal hazard in the period-space rule. Worth the rounds.

## Validate pure logic off-platform
The app only builds on Windows (WinRT), but the `PostProcessing` classes are pure BCL. Porting the
decision logic (homoglyph proving-letter, protected tokens, de-hyphenation guard, initials lookbehind) to
Python let us run the acceptance cases and catch logic errors **without** a Windows build. Keep the core
logic dependency-free precisely so it stays verifiable this way (and unit-testable).

## Counterintuitive engine behavior is real
Higher DPI made body prose **worse**, not better, with this engine. Don't assume "more resolution = more
accuracy"; the recognizer has a preferred text-size range (~150 DPI here). Always confirm tuning
assumptions on actual data.

## Keep the door open, ship the safe core
The deferred items (Hunspell, LLM, tables, searchable PDF) were designed-for but not built. The shipped
core is deterministic, offline, and brand/code-safe. For legal/technical documents, *not corrupting*
IPs/numbers/models matters more than aggressively "correcting" — which is why protected tokens and
conservative rules exist, and why generative ML is gated.
