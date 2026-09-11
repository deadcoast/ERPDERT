# Follow a name, keep your place

Authority: the user's explicit cross-note TODO request, AGENTS.md, the original
HOW/similarities examples, and the experience design's distinction between claimed
targets and structural ownership. Portable schemas remain deferred by the user.
The following matching policy is a bounded implementation recommendation, not a
new canonical reference grammar.

1. Serve an author who remembers a name but needs the other note that discusses
   it. They should recognize why each destination is offered and return without
   losing their original place, even after editing the destination.
2. In Inspect reasoning, choose References. Each authored name shows other named
   places, their source line/column, field role and actual structural parent.
   Follow one to select those exact words. Return/Escape restores the original
   selection/viewport across multiple visits. MER offers refs, paged refs, ref N
   and ref back; inspect gains an explicit --references view with --at.
3. Deepen `// HOW ; codex, claude ;; Keep either explanation, intact. //` beside
   `// similarities ; AI ;; codex, claude, codex //`. A match to a listed item is
   labeled as that, not as an application or a parent. Unknown note heads work.
4. Alternatives: substring search already exists but cannot distinguish roles;
   automatic binding would invent identity; explicit whole-name matches provide
   useful navigation with visible ambiguity. Choose the last within the current
   single-document host. Cross-document identity and file navigation remain
   separate requirements, with no directory scan or hidden file attachment.
5. Index reasoning content names, targets, and explicitly supported similarities
   items. Split target names at commas outside existing quotes; preserve each raw
   field, occurrence range, quotes, spelling and source order. Whole-name matching
   is ordinal and case-insensitive, without quote decoding, Unicode normalization,
   substring matching or aliases. Ignore the originating note when finding other
   places. Multiple occurrences in another note remain separate destinations.
   Free prose, annotations, HOW payload sentences and application commands are
   not inferred to be named-note destinations. Existing literal search remains
   available for them. No name match resolves or executes an operation.
6. Empty and unfinished names remain visible without guessed destinations or new
   parser errors. No matches is a useful outcome. Every result belongs to one
   parsed revision: stale source actions refuse navigation; pending proposals
   retain their own choices. Source changes refresh the index. MER labels captured
   source and clears numbered references after saved changes. Missing closers
   remain visible; ownership comes only from the structural index.
7. Deliver a shared note-name index, native inspector navigation, explicit CLI
   inspection and MER reference navigation. Keep the established source window,
   line numbers, palette, keyboard behavior and restrained composition. Only the
   reference view adds destination rows; ordinary reading stays focused.
8. Inspect actual native and MER renders at wide/narrow sizes, including duplicates,
   no match and missing boundaries. Verify exact spans, native return/edit/Undo,
   stale and pending actions, CLI arguments, MER paging/back and source-only
   publish. Record OS accessibility and human usability gaps separately.

## Implementation and validation sequence

- [x] Shared immutable note-name projection and independent source-span tests.
- [x] Native References view, exact visits and anchored return with pending guards.
- [x] CLI --references and MER refs/ref/back using the shared projection.
- [x] Bundled example, help, native/published journeys and visual inspection.
- [x] Independent review, full source-only checks, package refresh and TODO record.

Cross-document paths, portable identity, schema versioning, application-reference
resolution, rename-all and persistent backlinks remain outside this increment.
They require their own meaning and editing decisions; this feature must not imply
that those decisions have been settled.

## Implemented interaction

`NoteReferenceIndex` retains the parsed document, each raw name field, exact value
range, occurrence role and actual structural parent. The quote-aware comma
scanner is shared with `ContextReading`; no second punctuation grammar was added.
A shared structure index avoids rebuilding the whole document for every note.
Foreign equal-looking nodes cannot borrow another revision's matches.

Notes References pages six result rows at a time. Each origin identifies its
role and match count; each destination offers its exact position, role and
actual parent. Unknown kinds are eligible names. Empty and unfinished places
are retained with an explanation and no guessed destination. A visited note's
missing closer stays above the scrolling results. Paging retains keyboard focus.

Following a match verifies the revision, originating owner and offered occurrence,
then selects its exact source words. Repeated visits preserve the initial
selection/viewport anchors. Return/Escape restores that place while retaining
edits and native Undo. If the visited opener is deleted or becomes prose, the
inspector keeps a separate Return affordance rather than adopting a neighboring
note. New/Open resets the trail. Pending proposals and stale buttons cannot
redirect source selection; guarded Save/reopen preserves original encoding.

CLI `inspect <file> --at N --references` reports the same names and ownership.
The flag requires an explicit source line. MER `refs` captures a numbered set
from a reasoning opener, entry or closer. `refs N` offers four destinations per
page; `ref N` visits one and `ref back` returns to the captured source line.
Both wide and narrow source windows show every choice on the offered page;
`details` retains complete names, roles and ownership. The margin traces actual
ownership only. Ordinary source navigation clears the displayed reference
annotation while retaining the numbered set for explicit return. Verified
repair/undo rebuilds the source index and clears that set; canceled repair leaves
display and navigation on the same source line.

The bundled `examples/referenced-notes.DERT` demonstrates personal notes,
duplicates, unknown heads and useful references without an operational chain.
Reading names does not scan directories, start programs, edit source, or add
portable reference syntax.

## Review and inspected evidence

- Added 13 core reference, 13 CLI/MER and 14 native cases. Coverage includes exact
  duplicate spans, raw quotes and commas, Unicode spelling, unknown kinds,
  incomplete names, foreign revisions, actual parents, paging, multi-hop visits,
  pending proposals, stale controls, deleted/reclassified owners, native typing,
  Undo, repeated visits, Save/reopen and BOM/mixed-newline retention.
- Independent review reproduced a quadratic indexing path and a deleted visited
  note losing its return anchors. Both received failing regressions and fixes.
  The 10,000-note/20,000-name case previously exceeded 14 seconds; it now passes
  the five-second regression bound. This is a local responsiveness check, not
  a universal performance guarantee or an incremental indexing implementation.
- Final review reproduced hidden choices in wide MER frames and mismatched
  selection after canceling repair. Failing regressions led to bounded window
  annotations with full detail retained, and clearing reference display when a
  repair changes selection. All four offered choices are asserted at 58, 110 and
  140 columns. Follow-up read-only review found no remaining material issues.
- Inspected actual WPF control renders at 1180/780 pixels: duplicates, no matches,
  deleted visited owner and an unfinished destination. Return stays visible;
  result rows scroll inside the inspector. Missing-boundary viewport assertions
  cover both widths. Inspected MER engine frames at 110/58 columns, including
  exact duplicate choices and a visited note with a missing closer.
- Published executable journeys use test-owned reference examples with literal
  paths, UTF-8 BOM and mixed newlines. CLI inspection and MER visit/back preserve
  bytes. The Notes smoke launches the adjacent native host through DERT and
  closes normally; it verifies launch/file retention, while native control tests
  establish reference button and edit behavior.

Final source-only verification passed **1,014 tests** (890 core/CLI/MER, 124 native)
and **all eleven executable suites**, including **54 MER** and **19 published Notes
checks**. All 229 current source/test inputs match that fresh proof. The copy
contains source and test-owned inputs only, excluding documentation and original
user notes. The root Windows package was refreshed and its Notes smoke passed.
Published lint and inspection of `referenced-notes.DERT` report zero diagnostics;
MER paging, visit, details and return preserve its bytes.

Local visual and regression evidence:

- `.artifacts/source-only-proof/9e52df551e404700bce6b053c07e15ee/output.txt`
- `.artifacts/references-root-publish.log`, `.artifacts/references-root-notes-smoke.log`
- `.artifacts/references-example-lint.log`, `.artifacts/references-example-inspect.log`, `.artifacts/references-example-mer.log`
- `.artifacts/reference-host-proof/references-1180.png`
- `.artifacts/reference-host-proof/references-780.png`
- `.artifacts/reference-host-proof/references-personal-780.png`
- `.artifacts/reference-host-proof/references-removed-False.png`
- `.artifacts/reference-host-proof/references-unfinished-780.png`
- `.artifacts/mer-visual/e88bd1a8afea4950a1b9e0e669dc19df/held.png`
- `.artifacts/mer-visual/737e8fc70fbe47979580da5394c6cf5d/held.png`
- `.artifacts/mer-visual/f2f5f93b0abc49b1a8bd5c78b0425091/held.png`
- `.artifacts/references-final-review-red.log`, `.artifacts/references-final-review-green.log`
- `.artifacts/references-native-delivery.log`

These are native-control and engine render inspections, not an OS screen-reader,
clipboard/IME or human usability certification. Single-document whole-name
matching remains a working implementation policy. Cross-document identity,
portable schemas, incremental indexing, application resolution, persistent
backlinks and rename-all remain separate work. The next recommended TODO is an
author-and-agent context selection with an explicit copy/export preview.
