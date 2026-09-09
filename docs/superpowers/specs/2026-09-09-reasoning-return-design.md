# Read the reasoning, return to the note

Authority: the user's request to continue the next TODO; the original reasoning
folded-at-rest and active-sinkers-expanded requirements; the accepted Notes/MER
theme. The temporary rich-host inspection in EXPERIENCE-DESIGN and SYNTAX-LAB
is a working interaction recommendation, not an added grammar rule.

1. Serve an author following an explanation while keeping their place in a long
   note. Reading should feel immediate; editing should remain the real document.
2. Ctrl+Space or Inspect reasoning opens a temporary context view while closed
   source stays folded. It shows the header, named target, ordered entries and
   each entry's own annotation, with its actual closing line or missing closer.
   Edit source or an entry button reveals/selects its exact original range.
   Return to note / Escape restores the anchored origin and viewport. Moving the
   source caret outside the reasoning returns it to rest without moving that
   new caret. Related context controls may retain focus without closing it.
3. Deepen the existing `// plans ; Saturday ;;` block with annotated `// morning,`
   and `// evening` entries and its own `//`. Free notes and ordinary sinkers do
   not acquire reasoning folds. Named targets remain names, not operation bindings.
4. Alternatives: leave opened source permanently expanded (current gap); collapse
   on any focus loss (would interrupt work in context controls); or give reading
   a temporary connected surface and keep editing in the original buffer. Choose
   the third. Automatic resting follows source navigation, not a hover timeout.
5. Inspection changes no source, saved state or undo history. Native text anchors
   track the original place through edits. If the inspected opener is removed or
   ceases to be reasoning, withdraw that inspection instead of borrowing a nearby
   note's identity. An active source selection must not be hidden by automatic
   folding. Explicit Return retains all buffer edits; it is never a rollback.
6. Verify adjacent identical notes, missing closers, changed headers, removed
   structures, cross-boundary selections, edits before the origin, undo, save,
   new/open and repeated visits. List draft focus is part of editing and cannot
   cause its owner to disappear. Reading and folding never trigger CLI operations.
7. Implement the complete native Notes interaction with the existing parser,
   folding manager, source buffer, undo and guarded save. Keep CLI/MER reading
   behavior intact. Screen-reader evaluation, IME and clean-account installation
   remain separate evidence gaps; this is not the full note application.
8. Use actual offscreen WPF controls and routed input for regressions. Inspect
   rendered wide/narrow controls, including long reasoning and a return journey;
   refresh the Windows package and execute the source-only suites. Record those
   evidence types separately from human usability and OS input-method tests.

Implementation is authorized by the current user request and AGENTS.md's
development loop. No repeated design-approval gate is added for this reversible
authoring increment. The delivered ecosystem and Unicode behavior is retained.

## Work and verification

- [x] Native inspection lifecycle, anchors and fold-rest behavior.
- [x] Connected context view, source editing, keyboard/pointer controls.
- [x] Adverse-case/native tests and independent code review.
- [x] Wide/narrow visual inspection, publish, source-only validation and docs.

## Delivered interaction

The Notes host opens a temporary reading surface through its reasoning gutter,
Inspect reasoning button or Ctrl+Space. It retains the current source selection
and leaves a closed fold closed. The actual header, named target, ordered entries,
individual annotations and owned closing line share one restrained visual trace.
Missing closers and empty reasoning have explicit text. Named targets remain
names; inspection creates no application binding or execution eligibility.

The wide view sits beside the source. At narrow widths, reading controls occupy
the left of the context below the note, with entries on the right. An inspected
first layout spent its entire compact height on controls and hid the entries;
the delivered layout exposes both sample explanations and their closing line.
Longer contents remain scrollable. Buttons retain keyboard focus across caret
and layout refreshes; Escape is available from the source or reading controls.

Each entry selects its exact value range in the existing buffer. Edit source
selects the note's explanation or block header. Source spelling and annotations
remain available there, with existing Undo, Redo and guarded Save. Return/Escape
restores the anchored origin, native selection shape/direction and scroll
position after the compact layout settles. It keeps buffer edits. If inspection
began inside an active expanded selection, Return keeps that selection visible;
source navigation away restores the fold. A cross-boundary selection also keeps
the selected reasoning visible until released.

Opening a reading session is explicit. Ordinary navigation and edits cannot
silently start inspecting a neighboring expanded note. Deleting either original
opening slash withdraws inspection before document anchors move; this prevents
an adjacent opener from inheriting it even when deletion joins their boundaries.
Renaming the header preserves the existing owner. A stale entry action cannot
select a replacement parsed entry. Source edits never gain an automatic Undo.

Pending list, goodbye and title proposals retain their Apply/Leave decisions.
An inspection request cannot displace them, and a refused gutter action consumes
the click instead of falling through to a native fold toggle. Resting goodbye
context yields to an explicitly inspected reasoning block elsewhere in the note.
Native inline unfolding remains editable and refolds on source departure; it
does not implicitly start a separate reading session.

## Evidence and limits, 2026-09-09

- **24 additional native cases; 72 native tests passed.** Real offscreen WPF
  controls exercise routed commands and button handlers, exact selection/editing,
  native Undo, reverse and rectangular selection, proposal retention, single/empty/
  unfinished notes, original-opener deletion, renamed headers and adjacent owners.
  Wide/narrow long-note journeys repeat three times and verify caret, viewport,
  exact source, unchanged Undo history and saved/reopened UTF-8 bytes with BOM and
  mixed newlines. `.artifacts/reasoning-native.log` records the full native run.
- **830 tests and all eleven executable suites passed** from fresh source/test
  inputs, without documentation or original source artifacts as build inputs:
  758 core/CLI/MER and 72 native. Evidence is
  `.artifacts/reasoning-source-only-final.log` and
  `.artifacts/source-only-proof/4bd63f9a61dc40a39be049d687c20c1e/output.txt`.
- Inspected actual rendered WPF frames at 1180 and 780 pixels, including return
  to the resting source, under `.artifacts/reasoning-host-proof/`. These are
  application control renders, not OS desktop screenshots or human usability
  results. The four frames are `reasoning-read-1180.png`,
  `reasoning-read-780.png`, `reasoning-return-1180.png` and
  `reasoning-return-780.png`.
- Independent review reproduced next-line boundary, context competition and two
  adjacent-owner identity bugs. All four retained regressions pass after fixes;
  final review found no concrete residual blocker. The review also checked the
  pointer-coordinate contract and refusal path; OS pointer events were not
  simulated. Native rectangle-selection preservation was separately corrected.
- Refreshed `.artifacts/win-x64/DERT.exe` and its bundled Notes host through
  `src/Publish-Windows.ps1`. The shipped host passes all 13 existing published
  Notes checks. Logs: `.artifacts/reasoning-publish-final.log` and
  `.artifacts/reasoning-notes-published-final.log`. Use
  `DERT write examples/weekend.DERT` in that folder to try the existing notes-only
  example; the example is a product asset, not this increment's test fixture.

No OS clipboard round trip, IME composition, screen-reader evaluation, real
pointer-input run, clean-account installation or human long-note study is claimed.
Reduced motion retains the same controls and source meaning; this increment
adds no timing-dependent presentation. The full note application and unresolved
grammar interpretations remain separate work on TODO.
