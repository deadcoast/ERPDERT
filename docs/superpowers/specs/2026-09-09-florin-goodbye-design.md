# The florin goodbye in Notes

## Authority and the person using it

The original language notes at `docs/archive/original.md:105,126` request a typed
lowercase `fin.` to be corrected and/or rendered as `ƒin.` during highlighting.
The user explicitly requested this next TODO on 2026-09-09. This increment gives
the author that distinctive goodbye with ordinary keyboard input. It does not
make a chain mandatory for notes or change the parser's existing grammar.

## Interaction and alternatives

For an actual parsed chain:

```dert
STERT
// All my words stay here.
fin. // Until tomorrow.
```

The Notes editor displays the final line as `ƒin. // Until tomorrow.`. The `f`
still occupies its original source position. Selection, copying and Save
retain `fin.`. At the ending, its context identifies the opening line and says
what is displayed versus stored. A typed-spelling toggle exposes literal text.

Three alternatives were considered: silently rewriting while typing, requiring
a correction button for every goodbye, or rendering automatically with an
optional source edit. The third is the implementation recommendation: it gives
the original immediate visual effect, preserves author input, and lets authors
choose the saved spelling without a keyboard character hunt.

`Write ƒ in source…` opens an exact source-line preview. Apply changes only the
initial character, in one native undo group. Leave changes nothing. The preview
captures one ending and its original source; moving the caret cannot retarget
it, and changing source withdraws it. Save remains a separate action. The
context reports literal spelling after Apply and Undo, even when the default
display makes both spellings look the same.

## Ownership, failure cases and repeated use

- Eligibility comes from `ChainNode.Terminator`, never a text replacement or
  a boundary color alone. A standalone `fin.` is ordinary text for this feature.
- Notes, quoted strings, URLs, paths, larger words, incomplete `fin`, uppercase
  variants and header fields do not qualify. Already-written `ƒin.` is unchanged.
- Existing annotations, whitespace, BOM, newline spellings and all other
  characters remain exact. Each of several chains has its own ending.
- Rendering adds no buffer edits, no dirty state, no undo entry and no file write.
  Deleting the dot or removing the opener immediately removes the treatment.
- The source edit is exact-revision checked, preserves caret/selection and uses
  guarded Save/reopen. It does not execute the chain or certify its validity.
- List drafts keep their existing protections; a florin preview must not hide or
  discard typed list words. Keyboard access and reduced motion retain the same
  result. No extra celebration is needed for merely displaying a glyph.

## Implementation and evidence plan

Core exposes the parsed ending and a one-character source-edit plan. An
AvalonEdit element generator substitutes one visual character with identical
document length; existing semantic coloring remains responsible for color.
The existing Notes context hosts the spelling controls and exact preview.
CLI/MER source excerpts remain literal; no new CLI repair operation is introduced.

Tests own their source examples. Verify core exclusions and source preservation,
actual native rendering, typing/deletion, selection, stale previews, Undo/Redo,
Save/reopen, and wide/narrow layout. Inspect rendered controls, then run the
source-only validation and published Notes smoke before reporting delivery.
OS clipboard ownership, IME composition, clean-account installation and the
broader note application retain their existing validation gaps.

## Delivered evidence

Implemented with shared `FlorinGoodbye` ending/plan data, a one-character native
visual element, and contextual `GoodbyeAuthoringPanel` controls. The default
display is automatic; the optional source edit remains a distinct action.

The initial core tests failed with no recognized endings; the first native glyph
test drew `fin.` instead of `ƒin.`. Both passed after implementation. Native
button tests then exercised literal view, preview, Apply, Leave, Undo/Redo and
Save/reopen. The styled preview is read as inline text ranges in control tests,
not through TextBlock's plain Text property.

Review identified an actual backward-selection caret jump. The regression failed
(expected caret 6, actual 10); retaining the native selection and caret fixed it.
Further tests caught list choices appearing alongside a pinned goodbye preview,
clipped narrow spelling controls, and an accepted edit briefly reporting itself
as stale through synchronous refresh. Each was reproduced and corrected.

Actual glyph runs verify the florin while source and native copy data still say
`fin.`. The copy event is canceled after inspecting its data, so this test never
changes the OS clipboard. Typed dot/deletion, removal of the opener, explicit
source edits, stale proposals, pending list drafts, annotations, BOM, mixed
newlines and repeated opening are covered. Reduced-motion tests use the normal
host with motion disabled, preserving the same spelling and actions.

Inspected wide/narrow WPF frames are retained at `.artifacts/florin-host-proof`.
All 671 tests (636 core/CLI and 35 native) and all ten executable suites passed
from fresh source-only inputs. This feature adds 18 core cases and 12 native
cases; published Notes now has 11 checks, including a literal-path CLI launch of
an untreated goodbye and byte preservation. Evidence is retained in
`.artifacts/florin-source-only-final.log` and
`.artifacts/source-only-proof/7c5a6e8891fe4256b4bbb730063617cf`.

These results establish the bounded Notes convenience. They do not establish
cross-application clipboard round trips, IME input, accessibility-tool behavior,
clean-account installation or general Unicode title width. The latter is the
next authoring TODO; existing broader requirements remain in the backlog.
