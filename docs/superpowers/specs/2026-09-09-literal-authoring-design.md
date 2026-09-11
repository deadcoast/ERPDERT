# Literal punctuation, authored deliberately

Authority: the user's request to begin the literal-punctuation TODO, AGENTS.md,
the original hook/line/sinker and contextual `//` forms. The syntax lab's quote
mechanism remains a working recommendation. This increment uses existing quote
recognition; it does not establish a portable escape grammar or decoded strings.

1. Serve someone writing a folder, quoted phrase or punctuation example in their
   own notes. They should keep their words and see why a character becomes a
   boundary, with a useful choice at that exact location.
2. Select source and choose **Literal text…** (Ctrl+Shift+L), or use **Keep
   punctuation as text** in Add another item. A source-bound preview shows the
   inserted quote marks and actual parsed reading. Apply changes the buffer in
   one Undo step; Save retains the existing guarded file behavior.
3. Deepen active sinkers and reasoning values, for example an item containing
   `C:\tools; old\`, `//`, `;;` or a final literal comma. Source spellings, note
   prefixes, annotations and flush-left ownership remain available and intact.
4. Alternatives: require manual quoting; auto-escape all punctuation on paste;
   or offer an explicit quote preview checked by the parser. Choose the third.
   It joins the existing list composer and source inspector, with no new global
   mode or new language marker. Ordinary paths need no added syntax. Quotes
   already present stay verbatim. When protection is needed, quote unquoted
   portions; a trailing backslash remains outside the added quote so it cannot
   escape the new ending. Every inserted character is shown before Apply.
5. Protection inserts only quote marks within the selected words. The existing
   Add flow also inserts the new item and any required preceding continuation
   comma, all shown in its source diff. No slash doubling, decoding, whitespace
   normalization or source removal. Existing source edits must remain within a
   readable value and preserve surrounding structure, annotations and application
   fields after reparsing. An intentional final-comma change cannot detach other
   list items. New-item protection treats the entire draft as its words; choosing
   this action explicitly includes any typed `//` in those words.
6. Preserve unfinished quotes with an explanation; do not guess their intended
   ending. Reject multiline/rectangular or split-character selections, structural
   header selections and stale previews. Quote-bearing prose, UNC/final-slash
   paths, annotations, literal commas and repeated use need direct regressions.
   No-op protection must not add an Undo entry. Pending list words keep their
   existing Save/departure protections. Source previews never silently retarget.
7. Deliver shared core protection and source plans, native selection/list
   integration, exact before/after readings and a bundled notes-only example.
   General escape grammar, application decoding and automatic paste treatment
   remain separate work with no implementation claim.
8. Inspect actual native controls at wide/narrow sizes; exercise parser, native
   input/Undo, stale choices, save/reopen, and fresh source-only publishing.
   Record rendered evidence separately from OS clipboard/IME and usability.

## Delivery

- [x] Core quote-only protection, exact edits and parser ownership checks.
- [x] Source selection and existing list composer integration.
- [x] Native and CLI/MER shared punctuation readings.
- [x] Reviewed regressions, inspected renders, fresh package and documentation.

## Implemented behavior and corrections

`LiteralText.Protect` inserts existing quote boundaries only where the input
needs protection. It retains existing quote spans verbatim, checks that the
result becomes one complete sinker, and verifies no annotation or continuation
comma escaped that reading. Plain paths and already protected words have no
extra quote edit. Adjacent quoted/unquoted portions remain visible raw source;
for example `say "hello" // literally,` previews as
`say "hello"" // literally,"`. No reader unescapes these fields.

`LiteralText.Plan` binds a selection to its source and permits only a value,
reason or plain-text field. It reparses the complete candidate and compares
node kinds, ownership, authored closings, remaining fields and error findings.
A final comma can become part of an item's text only if the following source
keeps its ownership. Annotation prefixes, request fields, whole-character
boundaries and existing quote context are checked separately.

The native source preview records the original line and selection direction;
Apply makes one buffer edit, and stale source requires a fresh selection.
The list composer keeps the actual draft text while its mode changes only the
proposal. A new quote edit uses the existing short accepted-edit accent, with
the same settled text in reduced motion. Inspection itself creates no motion
or source write. CLI and MER retain exact source excerpts and share the core
quote/path explanation; full detail carries that explanation in narrow MER.

Read-only review found two annotation mistakes: a plain-text selection could
initially absorb its annotation, and the revised-value check included an
unrelated unfinished annotation quote. Both now use the actual annotation
boundary and have passing regressions. A reverse-selection regression failed
before its direction was retained through Apply.

Rendered inspection found a separate narrow-window issue: an unchanged previous
line appeared twice in a new-item diff, pushing Apply out of the viewport.
The diff now shows only changed lines. The regression measures the button's
actual bottom against its scroll viewport; `IsVisible` alone was insufficient.
The final 780-pixel frame keeps the source, exact addition and Apply/Leave in view.

## Verification, 2026-09-09

- **932 tests passed**: 838 core/CLI/MER and 94 native. The increment adds
  31 core/CLI and 13 native tests; one core case checks 576 punctuation
  combinations for exact character retention and stable repeated protection.
- **All eleven executable suites passed** from a fresh source/test copy,
  including **15 Notes launch and byte-retention checks**. No original notes or
  documentation were present as build, test or smoke inputs. Evidence:
  `.artifacts/literal-source-only-final.log` and
  `.artifacts/source-only-proof/086ca73c05984740b0aa4aec41e43f6e/output.txt`.
- Failed-before-fix evidence: `.artifacts/literal-core-red.log`,
  `.artifacts/literal-reading-red.log`, `.artifacts/literal-review-red.log` and
  `.artifacts/literal-viewport-red.log`. Passing focused/render evidence:
  `.artifacts/literal-core-adversarial.log`, `.artifacts/literal-focused.log`,
  `.artifacts/literal-render-final.log` and the final full source-only record.
- Actual native control renders were inspected in
  `.artifacts/literal-host-proof/literal-source-1180.png`,
  `literal-source-780.png`, `literal-add-1180.png` and `literal-add-780.png`.
  These are WPF renders, not OS desktop screenshots or a physical-input study.
- Actual MER compositor frames and ANSI transcripts were inspected at 110 and
  58 columns: `.artifacts/mer-visual/83fb0ca7e47c42e190e085e12063a158/` and
  `.artifacts/mer-visual/43a41646c4b742cdb6d7bc7db411ead1/`. The narrow view
  preserves literal source and its separate annotation; full explanation is
  available in detail, without adding another compact context row.
- Root Windows package refreshed (`.artifacts/literal-publish.log`) and its
  15 Notes checks passed (`.artifacts/literal-notes-published.log`). Published
  inspect and lint exercised `examples/literal-words.DERT`, reported zero
  findings and retained its SHA-256. Transcripts:
  `.artifacts/literal-inspect-published.log`, `.artifacts/literal-lint-published.log`.

Native evidence covers real controls, routed commands, selections, buffer edits,
Undo, Save/reopen and viewport bounds. Physical keyboard/clipboard round trips,
IME, accessibility-tool testing, clean-account installation and human usability
remain unproven. Very long proposals can still use the context scroll area.
This increment does not introduce arbitrary literal quote escapes, decoded
values, automatic paste transformation, or a meaning-change workflow for an
already parsed application field. These limits remain explicit design work.
The next bounded TODO is structured-context inspection with raw fields retained.
