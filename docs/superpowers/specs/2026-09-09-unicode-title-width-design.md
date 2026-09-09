# Titles keep their language

The user requested non-ASCII title-rule width on 2026-09-09. The original
`docs/archive/original.md` requests a rule that follows the title because it
looks good. This increment extends that existing experience without changing
the author's spelling, normalization, note ownership or grammar.

## Interaction and recommendation

An author types `// café 日本語 👩‍💻` and presses Enter. Its rule contains 14
marks: four Latin cells, two spaces, six CJK cells, and two emoji cells. A
decomposed accent stays decomposed. Imported mismatches retain the existing
Align title preview; Apply/Undo/Save keep their current distinct effects.

Counting UTF-16 code units miscounts combining marks and supplementary text.
Counting graphemes alone misses wide CJK and emoji. Measuring only Windows
pixels would make source repairs depend on local font installation. The
recommended combination is a shared documented cell policy for stored marks
and measured native dash spacing for the Notes display. A wrong mark count
must still look short or long; presentation cannot conceal a pending repair.

## Shared policy, with its actual limits

Use checked-in tables derived from Unicode 17.0.0 EastAsianWidth and emoji data,
with exact source URLs, hashes, license and a deterministic generator. Ordinary
clusters occupy one cell; wide/fullwidth and emoji presentation occupy two.
Ambiguous-width characters occupy one cell. .NET StringInfo supplies grapheme
boundaries; this does not claim independent Unicode segmentation conformance.
The same measurement serves parser findings, title repair and MER composition.

Source text is never normalized. Combining sequences, CJK, joined emoji,
flags, keycaps, variation selectors and text emoji presentation get explicit
cases. Tabs have column-dependent expansion and controls have escaped terminal
representations: retain them and explain why that title cannot yet receive a
portable rule. Replacing them with spaces or deleting them would be a separate
source choice. Unsupported titles stay editable; they are not invalid notes.

Unicode UAX #11 explicitly says East Asian Width alone is not a ready-made
terminal algorithm and that actual glyph width comes from fonts/layout. This
is ERPDERT's Windows-first display policy, not universal terminal conformance.
Reference: https://www.unicode.org/reports/tr11/tr11-44.html and
https://www.unicode.org/reports/tr29/.

## Native rendering and ownership

Notes keeps normal text shaping and fallback fonts. When the title fits on one
visual line, each source dash receives the title's measured width divided by the
required number of cells. The actual number of authored dashes is preserved, so
a mismatched rule cannot appear repaired. Source positions, partial selection,
copying and Undo remain available. Wrapped titles retain their source rule and
explain the wrap; no source reflow or extra rules are inserted. Changing zoom,
font or window size must not modify the buffer.

The source-rule prefix stays `// ` and no indentation is introduced. Complete
the title's ordinary source edit first; highlighting/layout never saves text.
Existing automatic rename, intentional deletion, preview staleness, Undo and
guarded Save/reopen remain part of acceptance.

## Evidence and deferred work

Test-owned fixtures cover real parser/repair/MER integration, native source
input and selection, fallback glyphs, exact dash advance widths, narrow/wide
frames, zoom/font changes and reopened UTF-8. Inspect the actual frames and
published executable, then run the repository's source-only suites.
Cross-terminal font configurations, OS IME/clipboard, accessibility tools and
clean-account delivery remain separate evidence; no claim of universal glyph
availability or complete Unicode conformance follows from cell arithmetic.

## Delivered and inspected, 2026-09-09

Implemented the shared policy in `Dert.Text`, used by parser diagnostics,
`TitleRuleRepair` and MER. The input sources are
[EastAsianWidth.txt](https://www.unicode.org/Public/17.0.0/ucd/EastAsianWidth.txt)
and [emoji-data.txt](https://www.unicode.org/Public/17.0.0/ucd/emoji/emoji-data.txt).
`src/Generate-UnicodeWidth.ps1` pins their SHA-256 hashes and the captured Unicode
license hash. It only accepts local inputs. Generated tables and license are
checked in; the license is copied into the CLI and Notes publications.

The native renderer preserves normal title shaping and measures its actual
glyph width. One visual dash remains one source dash, so partial selection,
editing and Undo work at the original offsets. Measured font changes reflow the
rule without dirtying the document. The renderer deliberately divides by the
required count; a half-length imported rule stays half-length until Apply.
Wrapped titles retain normal source rendering and show a plain explanation.
No language source artifacts were edited and no indentation syntax was added.

The initial native CJK case missed the title's width by 5.08 DIP; joined-emoji
cases also missed the intended measurement tolerance. After native fitting,
the tested title/rule advance differences stay within 0.25 DIP. Tests exercise
Consolas, Cascadia Mono and Segoe UI, including actual fallback. Wide/narrow WPF
frames at 1180 and 780 pixels were inspected in
`.artifacts/unicode-host-proof/unicode-title-1180.png` and
`unicode-title-780.png`: source, matching rule and readable context remain
visible in the existing theme. Emoji uses the host's monochrome fallback glyphs.
These are offscreen rendered WPF controls, not desktop screenshots.

Tests also cover Hebrew and Arabic, combining sequences, supplementary CJK,
family/profession emoji, modifiers, flags, keycaps and text/emoji presentation.
Tabs, formatting controls, orphan marks/joiners and an unfinished UTF-16 buffer
are retained with specific explanations. An invalid surrogate must be constructed
at runtime in the test: attribute metadata replaces it before parsing.

The actual CLI repair path retains joined emoji through preview, verified Apply
and exact-byte Undo. Display escaping is now shared with MER: supplementary
hidden format characters use a scalar `\U000E0001` escape, and never execute as
terminal formatting. Published MER was inspected with the new notes-only
`examples/world-notes.DERT`; its 14-mark title passes without findings.

All 710 tests (662 core/CLI, 48 native) and all ten executable suites passed
from a fresh copy containing only source and test inputs. This increment adds
26 core and 13 native cases. Published checks include 52 MER and 13 Notes cases;
the refreshed root Windows package also passed its 13 Notes checks. Evidence:
`.artifacts/unicode-source-only-final.log`,
`.artifacts/source-only-proof/6130870814cd484d8050686910ba5c04`,
`.artifacts/unicode-publish-final.log` and
`.artifacts/unicode-notes-published-final.log`.

Independent review found no actionable correctness issues. Additional read-only
native probes covered mixed bidirectional text, Arabic diacritics, Tamil,
ligatures and emoji presentation in Consolas/Segoe UI, with at most 0.033 DIP
title/rule difference. These probes supplement the named tests; they do not
establish all-script or all-font conformance. The deferred evidence above remains.
