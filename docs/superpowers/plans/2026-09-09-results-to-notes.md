# Results to notes implementation plan

**Goal:** Retain a real source-bound observation and deliberately insert its inert
result note, with historical source comparison and native Undo/Save.
**Architecture:** Dert.Core owns immutable observation data, comparison and source
insertion. Dert.Cli/Results owns local storage, producer adapters and CLI review.
Notes owns the dedicated review and existing guarded authoring actions. MER keeps
its real captured probe. No new dependency or grammar.
**Spec:** [Bring the result back](../specs/2026-09-09-results-to-notes-design.md).

Use the subagent-driven-development and test-driven-development skills. Continue
the authorized working checkout, preserve earlier edits and original artifacts,
and use independent review. No commit, remote publish or installer is required.

## Shared contract

New public Core types in `ResultObservation.cs`, `ResultNotes.cs`:

```csharp
enum ObservationOutcome { Completed, Attention, Failed, Interrupted }
record ResultEvidence(string Label, string Detail);
record ResultOrigin(string Path, int Line, string SourceRevision, string ChainText);
record ResultObservation(string Id, DateTimeOffset ObservedAt, string Action,
    string Target, ObservationOutcome Outcome, string Summary, string Effects,
    ResultOrigin Origin, IReadOnlyList<ResultEvidence> Evidence);
record ResultSourceComparison(string State, string Message, int? Line,
    string CapturedText, string? CurrentText);
// ResultNotes.Compare(DertDocument, ResultObservation, string destinationPath,
//     int? atLine = null, bool sourceHasBom = false) -> ResultSourceComparison
// ResultNotes.Plan(same arguments) -> ResultNotePlan : ISourceRepairPlan
// Plan includes Observation, Comparison, NoteText, AddedSpan; append only.
// ResultNotes.Describe(ResultObservation) -> full human-readable review.
// ResultNotes.DescribeComparison(ResultSourceComparison) -> visible source differences.
// ResultNotes.CompareObservations(before, after) -> human-readable differences.
```

Public CLI `Results.ObservationStore.WriteNew(ResultObservation, string path)`
returns verified path; `Read(string path)` returns validated record. Strict UTF-8,
new `.json` only, max 8 MiB, no overwrite, source-path/ADS refusal. This is private
local persistence, not a promised portable interchange contract.

## Tasks

- [x] Core delegate: failing `ResultNotesTests` for exact/moved/changed/duplicate
  origins, inert annotation serialization, whole-source preservation, unfinished
  owner refusal, duplicate ID, exact comparison and outcome retention; implement
  shared contract, self-review and focused tests.
- [x] Coordinator: failing `ResultCommandTests`/storage tests; implement local
  store and discovery adapter, explicit `run --receipt` (one discovery only),
  `result record [--into file] [--at N] [--compare record] [--write]`, MER keep.
  Generated commands use existing PowerShell literal/Invocation helpers.
- [x] Native delegate: `ResultReviewWindow` and `NotesWindow` entry/handoff,
  session apply method, real-control tests of preview/Apply/cancel/stale/pending,
  Undo/Save/reopen and width constraints. Source comparison and exact insertion
  are separate readings; no source mutation until Apply.
- [x] Coordinator: help, shipped example and executable test-owned smoke cases;
  independent review and regression fixes; inspect actual native/MER frames.
- [x] Run `tests/Invoke-SourceOnlySmoke.ps1`, local `src/Publish-Windows.ps1`,
  published smoke/example journeys and source/proof hash parity. Record exact
  evidence and retained adapters in spec, status, changelog, README and TODO.

Ruling: use existing content and explicit selection for local source comparisons;
defer the earlier recommended identity-note grammar policy. This avoids silent
identity edits while preserving a complete deliberately reviewed history path.

Final verification and review evidence is recorded in the spec: 1,151 tests,
all eleven executable suites, and exact parity for 258 source/test inputs.
Source artifacts and preceding workspace changes were preserved.
