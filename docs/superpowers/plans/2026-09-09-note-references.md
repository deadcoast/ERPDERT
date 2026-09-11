# Note references implementation plan

**Goal:** Follow explicitly named places between reasoning notes without inventing
ownership or application bindings, and return to the author's original position.

**Architecture:** A revision-bound core index produces raw name occurrences and
matches. Native and CLI/MER adapters own navigation and presentation. Existing
source, ownership, pending-choice and save models retain their responsibilities.

**Stack:** Current .NET, WPF/AvalonEdit, and MER; no new dependency.
**Design:** [Follow a name, keep your place](../specs/2026-09-09-note-references-design.md).

**Constraints:** Original artifacts unchanged; tests own examples; no indentation
syntax; no source writes from reading; no operation binding from name matches;
portable schemas and cross-document identity deferred. Preserve in-progress work.

## 1. Shared projection

- [x] Write `NoteReferenceTests` for exact target/item spans, duplicate destinations,
  unknown heads, excluded prose/annotations, quotes, empties and foreign revisions.
- [x] Observe missing behavior; implement `Dert.Core/NoteReferenceIndex.cs` and
  shared `SourceFieldParts.cs`. Reuse comma scanning in `ContextReading.cs`.
- [x] Run reader/reference tests; retain raw fields and actual structural parents.

## 2. Native visit and return

- [x] Write native tests for References actions, multi-hop Return, typing/Undo,
  stale buttons, pending proposals, repeated visits and Save/reopen.
- [x] Add `ReasoningReferences.cs` as a focused partial of `ReasoningPanel`; extend
  `ReasoningInspection` with revision-checked visits preserving original anchors.
- [x] Run native reasoning/context/reference tests; inspect 1180/780-pixel frames.

## 3. CLI and MER

- [x] Write published-adapter tests for `inspect --at N --references`, validation,
  MER refs/ref selection, paging, ref back and invalidated/pending references.
- [x] Extend `CliOptions` and `DocumentRenderer`; add `MerReferenceReview.cs` and
  a reference annotation builder. Keep structural traces based on real parents.
- [x] Run reference/MER tests; inspect 110/58-column frames including open notes.

## 4. Delivery

- [x] Bundle `referenced-notes.DERT` and help; extend published smoke using its own
  fixtures. Run independent read-only review and fix reproduced findings.
- [x] Run fresh source-only solution tests, publish and all eleven executable
  suites. Refresh root package and verify the example and unchanged source bytes.
- [x] Update README, TODO, status and evidence; verify source/test inputs match
  the final proof. Record remaining scope without declaring portable semantics.
