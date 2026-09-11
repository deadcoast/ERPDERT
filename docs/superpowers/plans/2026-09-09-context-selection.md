# Context selection implementation plan

**Goal:** Let an author choose reasoning context, review included and omitted
source, then copy or export the exact reviewed text without changing the notes.
**Architecture:** A shared revision-bound core projection owns choices/ranges and
human-readable reports. A CLI authoring service owns guarded new-file export.
Native and CLI/MER adapters own interaction, status and presentation.
**Stack:** Existing .NET, WPF/AvalonEdit and MER; no new dependency.
**Spec:** [Choose what travels with your words](../specs/2026-09-09-context-selection-design.md).

## Constraints and workflow

Preserve existing user changes and original artifacts. No documentation as test
input; no indentation, inferred operations or portable schema. User authorization
covers continuing this working checkout and local publish. Use focused failing
regressions before implementation, independent review and source-only final proof.
The subagent-driven-development skill supplies independently scoped shared-core
implementation and review; the coordinator implements native and CLI/MER adapters
and integrates delivery. No commit, remote publish or installer action is required.

## Shared interfaces

Create in `Dert.Core`:

```csharp
public enum ContextSourceKind { FileSnapshot, EditorBuffer }
public sealed record ContextNote(DertNode Node, DertNode? Parent, bool IsRelated)
{
    public int LineNumber => Node.LineNumber;
    public string Label => DertStructureIndex.Label(Node);
    public TextSpan Span => Node.Span;
    public bool IsClosed => DertStructureIndex.Closing(Node) is not null;
}
public sealed record ContextRange(TextSpan Span, int FirstLine, int LastLine, string Text);
// ContextSelection.Capture(document, atLine, sourceLabel, kind) throws
// ArgumentException with plain guidance when no reasoning owns the line.
// Properties: Document, Primary (ContextNote), Notes (IReadOnlyList<ContextNote>),
// SourceLabel, Kind, SourceRevision.
// Build(IEnumerable<int>? includedLines = null) always retains Primary and
// returns ContextPacket. Additional lines must be actual reasoning openers.
// ContextPacket properties: Selection, Included, Excluded (ContextRange lists),
// Report, LeftOutReport, ReviewId. Included is IReadOnlyList<ContextNote>.
```

Create `Dert.Cli.Authoring.ContextExport.WriteNew(ContextPacket, string path)`.
Return verified full path; throw IOException/UnauthorizedAccessException with
truthful failure and no overwrite. Write strict UTF-8 without a new BOM, verify
through the same exclusive handle. Source remains untouched.

## 1. Shared selection and export

- [x] Create `tests/Dert.Tests/ContextSelectionTests.cs` and
  `ContextExportTests.cs`: exact spans/annotations, complete excluded complement,
  multiple notes in source order, primary retained, foreign/non-note choices,
  unknown names, missing closer/findings, hash changes, raw fences/newlines,
  strict Unicode and new-only verified export. Observe failing behavior first.
- [x] Implement `src/Dert.Core/ContextSelection.cs`, report builder in a separate
  focused file, and `src/Dert.Cli/Authoring/ContextExport.cs` using interfaces above.
- [x] Run focused core/export regressions; review against spec before integration.

## 2. Native review

- [x] Add `ContextSelectionWindow.cs` and focused native tests. Extend
  `ReasoningPanel` with one Context selection action and `NotesWindow` with a
  guarded create/open path. Actual command opens an owned review window; tests
  can create and show it offscreen without modal blocking.
- [x] Show exact Report in a read-only text surface; Left out switches to complete
  excluded text. Page note choices, retain primary, identify shared names, keep
  copy/export/back visible at 1180/780. Export uses the shared service. Clipboard
  writer can be injected for real button tests without replacing the OS clipboard.
- [x] Test stale source/pending actions, explicit choices, clipboard failure,
  new-only export, return/selection/Undo and repeated visits; capture actual views.

## 3. CLI and MER

- [x] Write `ContextCommandTests` and `MerContextTests` for explicit --at,
  --include, exact --text, --details, new-file --output with --from-preview,
  changed-source/selection refusal, incomplete notes, MER choices/reset/pending.
- [x] Implement `ContextCommand.cs`, `ContextPresentation.cs` and
  `Motion/MerContextReview.cs`; route through CliApplication and existing MER
  source composition. Keep full packet details separate from bounded annotations.
- [x] Verify wide/narrow actual frames with retained choice/status information.

## 4. Delivery

- [x] Bundle `context-selection.DERT`, help and README usage. Extend existing
  executable suites with test-owned fixtures; do not make docs test inputs.
- [x] Independent review and reproduced regression fixes. Inspect native and MER
  renders. Run fresh source-only solution/publish/all eleven executable suites.
- [x] Refresh Windows package; exercise published example, copy/export handoff
  and unchanged source bytes. Compare final source/test inputs to proof.
- [x] Record exact evidence in spec/status/changelog/TODO; keep portable schemas
  deferred and describe the next worked direction separately.
