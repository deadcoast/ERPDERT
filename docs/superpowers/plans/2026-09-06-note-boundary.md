# Note-boundary rehearsal implementation plan

> For agentic workers: execute task-by-task with test-driven development and
> review checkpoints. Use executing-plans for inline execution. The user has
> requested continued implementation of the documented next interaction.

Goal: let an author select the end of an unfinished reasoning block, inspect
the resulting ownership, and apply/undo that exact source edit.
Architecture: pure analysis and source-edit planning feed the existing guarded
file service; a two-stage terminal interaction separates boundary selection
from approval. Reparse proposed source to explain actual ownership changes.
Tech stack: existing .NET 10, Spectre.Console 0.49.1, xUnit 2.9.3; no new packages.
Spec: ../specs/2026-09-06-note-boundary-design.md.

## Global constraints

- Keep docs/original.md, docs/WHO-WE-ARE.md and RERDMERR.DERT unchanged.
- Keep the parser's recognition and recovery rules unchanged in this increment.
- Only an author-selected standalone `//` insertion is eligible.
- Never remove author text, commas, prefixes, or insert structural indentation.
- Selection is not approval; leaving is the default at both interaction stages.
- Preserve exact source bytes outside the insertion, local newlines, BOM and EOF.
- Bind the selected action and undo to the captured source and physical file.
- Preserve version-1 title-repair receipts; replay version-2 note selections.
- Retain guarded writes, original backups, verification and stale-edit refusal.
- No DERT command execution, package mutation, grammar canonization or uploads.

## 1. Core boundary analysis and exact edit

Files: new src/Dert.Core/SourceRepair.cs and NoteClosureRepair.cs;
adapt TitleRuleRepair.cs to ISourceRepairPlan; new
tests/Dert.Tests/NoteClosureRepairTests.cs.

Interfaces:

```csharp
public interface ISourceRepairPlan
{
    string Source { get; }
    string ProposedText { get; }
    string Explanation { get; }
    string Kind { get; }
    bool IsReady { get; }
    SourceEdit? Edit { get; }
}
public sealed record NoteBoundary(string Key, int? BeforeLine, int Offset,
    int KeptCount, int ReleasedCount);
// NoteClosureAnalysis: Document, Block, Boundaries.
// NoteClosurePlan implements ISourceRepairPlan and exposes Analysis, Boundary,
// ResultDocument, OwnershipChanges, and ApplyTo(string).
public static class NoteClosureRepair
{
    public static NoteClosureAnalysis Analyze(DertDocument document, int atLine);
    public static NoteClosurePlan Plan(NoteClosureAnalysis analysis, string boundaryKey);
}
```

- [x] Write tests against a compilable unimplemented API and observe failures.
  Required literal case:

```csharp
const string input = "// context ; notes ;;\n// first, // its reason\n// later\ncommands ; updateme ;;";
var analysis = NoteClosureRepair.Analyze(DertParser.Parse(input), 2);
var plan = NoteClosureRepair.Plan(analysis, "3");
Assert.Equal("// context ; notes ;;\n// first, // its reason\n//\n// later\ncommands ; updateme ;;", plan.ProposedText);
Assert.Equal(1, plan.Boundary.KeptCount);
Assert.Equal(1, plan.Boundary.ReleasedCount);
Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(input + "changed"));
```

- [x] Implement selection only among the current unclosed block's entry starts
  and recovery stop. Reject closed/inline/non-block selections and invalid cuts.
- [x] Construct one exact SourceEdit insertion using local newline/EOF policy;
  reparse and verify the selected original block's closure and retained entries.
- [x] Build released-entry role/owner observations from resulting nodes and
  source-line mapping. Test a released structured opener owning later entries.
- [x] Verify original-example closer restoration, all newline/BOM/EOF variants,
  empty blocks, Unicode prose, multiple blocks, exact unchanged source and spans.

Run: `dotnet test ERPDERT.slnx --no-restore --filter FullyQualifiedName~NoteClosureRepairTests`.

## 2. Shared guarded repair and typed recovery

Files: modify src/Erpdert.Cli/Repairs/RepairSnapshot.cs, RepairFileService.cs,
RepairReceipt.cs; create NoteRepairInspection.cs; add
tests/Dert.Tests/NoteRepairFileTests.cs. Adapt current title-only renderer casts
and readiness checks without changing visible title behavior.

Interfaces:

```csharp
// Snapshot.Plan becomes ISourceRepairPlan. Kind comes from the plan.
public NoteRepairInspection InspectNote(string path, int atLine);
public RepairSnapshot PreviewNote(NoteRepairInspection inspection, string boundaryKey);
// Inspection exposes Path, Revision, Identity, Analysis captured together.
// RepairSelection carries Kind, AtLine, CloseBefore for version-2 receipt replay.
// Existing Preview(path), Apply(snapshot), PreviewUndo(path) remain available.
```

- [x] Write real-file tests and observe failure: analysis/selection is read-only;
  chosen apply changes only the marker; undo restores exact original bytes.

```csharp
using var file = new ScratchDocument("// context ; notes ;;\n// item\ncommands ; updateme ;;");
var service = new RepairFileService();
var inspection = service.InspectNote(file.Path, 1);
var snapshot = service.PreviewNote(inspection, "3");
service.Apply(snapshot);
Assert.Equal("// context ; notes ;;\n// item\n//\ncommands ; updateme ;;", File.ReadAllText(file.Path));
service.Apply(service.PreviewUndo(file.Path));
Assert.Equal("// context ; notes ;;\n// item\ncommands ; updateme ;;", File.ReadAllText(file.Path));
```

- [x] Capture source, identity and revision once for selection; do not refresh
  after the author chooses. Reuse Apply's held-handle validation and commit path.
- [x] Keep title receipts version 1; write note receipts version 2 with typed
  selection. Replay only a supported valid selection against saved original.
- [x] Test legacy title undo, mixed latest-repair ordering, invalid/unknown
  receipt selections, changed files during selection, stale approvals, backup
  tampering, original retention, and partial-write rollback through this path.

Run: `dotnet test ERPDERT.slnx --no-restore --filter 'FullyQualifiedName~RepairFileTests|FullyQualifiedName~NoteRepairFileTests'`.

## 3. Boundary choice, ownership rehearsal and published proof

Files: modify CliOptions.cs, CliApplication.cs, RepairCommand.cs,
DocumentRenderer.cs, RepairRenderer.cs; create NoteRepairRenderer.cs and
NoteRepairCommand.cs; add tests/Dert.Tests/NoteRepairCliTests.cs and
tests/Invoke-NoteRepairSmoke.ps1. Update README, CHANGELOG, implementation status,
experience/syntax-lab implementation references, and this plan's evidence.

Interfaces:

- `repair --at N`: inspect the unfinished block at that source line.
- `--close-before N|eof`: select an offered boundary; requires `repair --at N`.
- `--apply <token>`: requires a selected plan; no implicit boundary selection.
- Optional CLI input callback receives `IReadOnlyList<NoteBoundary>` and returns
  a chosen key or null to leave. Existing confirmation callback remains separate.
- `NoteRepairRenderer` uses the existing literal-safe output and copy commands;
  shows current recovery, explicit choices, exact inserted marker, and resulting
  scope. All source gutters remain display-only.

- [x] Write real CLI tests before routes: no-write choice report, selected
  rehearsal, actual choice/cancel/confirm, arbitrary cuts/arguments refused,
  precise ownership changes, stale choice, action-specific token, undo and plain
  output. Capture output at wide and narrow widths with real source/file service.

```csharp
// No --close-before means no approval token and no default automatic choice.
var result = Run(["repair", path, "--at", "1", "--plain"]);
Assert.Contains("BOUNDARY", result.Output);
Assert.Contains("--close-before", result.Output);
Assert.DoesNotContain("--apply", result.Output);
Assert.Equal(originalBytes, File.ReadAllBytes(path));
```

- [x] Implement parsing and separate boundary selection from approval. Both
  interactive defaults leave unchanged; redirected output emits commands only.
- [x] Render released lines using their actual proposed roles/owners, not an
  assumed free-note label. Show outside boundary and remaining diagnostics.
- [x] Reuse generic undo with kind-appropriate diff and success descriptions.
- [x] Request independent review; reproduce confirmed defects before fixing.
- [x] Build/test/format, publish, run all three smoke suites on owned generated
  fixtures. Execute emitted PowerShell selection/apply/undo commands and compare
  exact restored hashes; verify all protected source hashes unchanged.
- [x] Exercise the actual styled/plain interactive terminal, including early
  boundary choice, leaving, invalid input, approval, undo and copied commands.
- [x] Record evidence, provisional behavior and genuine remaining limitations.

```powershell
dotnet test ERPDERT.slnx --no-restore
dotnet build ERPDERT.slnx -c Release --no-restore
dotnet test ERPDERT.slnx -c Release --no-build --no-restore
dotnet format ERPDERT.slnx --verify-no-changes --no-restore
dotnet publish src/Erpdert.Cli -c Release -r win-x64 --self-contained true -o .artifacts/win-x64 --no-restore
.\tests\Invoke-PublishedSmoke.ps1
.\tests\Invoke-RepairSmoke.ps1
.\tests\Invoke-NoteRepairSmoke.ps1
```

## Starting evidence and workspace

Baseline: clean Git checkout on main at 7cd7d60; 108 tests pass. The repository
now exists, unlike the previous increment. Workspace-isolation preference was
requested before implementation. Do not invent permission for branch/worktree
changes or claim a commit that was not made.

## Completed evidence

Implemented in the shared checkout without changing branches, creating worktrees
or committing. The optional isolation question received no answer; continuation
in place was explicitly announced. An unrelated untracked `.serena/` appeared
during the task and was preserved untouched.

- Core: 25 cases first failed against the unimplemented API, then passed. EOF
  planning initially rejected a retained final entry because its physical span
  gained a line ending. Validation now compares preserved semantic fields and
  source starts while allowing that exact physical-span extension.
- File service: 16 new cases initially failed; a genuine legacy-receipt test
  already passed. All passed after the shared implementation. A later regression
  reproduced an old undo preview becoming eligible after a newer repair/undo
  restored identical text. Latest-receipt revalidation under exclusive access
  closes that gap. Total note-file cases: 18.
- CLI: 15 behavior cases initially failed and six refusal checks passed against
  the pre-route CLI. Independent review then reproduced two further failures:
  unfinished inline notes labeled closed-form, and retained entries omitted from
  direct selected rehearsals. Both were corrected. Total note-CLI cases: 23.
- Release suite: 174 passing tests. Build: zero warnings/errors. Format check
  passes. No new dependencies. Source/parser grammar unchanged.
- Self-contained published executable: 7 inspection checks, 12 title-repair
  checks and 13 note-boundary checks pass. Printed PowerShell commands are
  executed against generated fixtures, including unusual filenames.
- Real PTY: styled default-leave; plain invalid cut and confirmation, early
  selection and default cancellation; styled early-cut approval and undo.
  Source/ownership color changes and source gutters were inspected in output.
  The original/restored fixture hash is recorded in implementation status.
- The three protected original artifact hashes match the starting values.
  Source artifacts and unrelated workspace files were not rewritten.
- Final focused read-only review found no remaining important issue in the
  corrected ownership presentation, held-handle undo check or documentation.

Retained output:

- `.artifacts/proof/38cfd7bd865b448490dd46304f006c2c/terminal-output.txt`
- `.artifacts/repair-proof/8d365b6ce8094f179804274603c1d122/terminal-output.txt`
- `.artifacts/note-proof/bdff21aa904d483ab7ab4e2447138094/terminal-output.txt`

No human usability study, screen-reader evaluation, clean-machine install,
canonical grammar decision or operational adapter validation is claimed.
Follow-on delivery: source-cited lint-to-rehearsal commands, without broadening
the supported repairs or inferring author intent.
