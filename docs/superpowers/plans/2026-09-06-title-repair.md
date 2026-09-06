# Title-rule repair implementation plan

> For agentic workers: use the executing-plans skill task by task. The user has
> explicitly requested implementation of this continuation; execute locally.

Goal: preview, apply, and undo one title-rule edit against the author's exact file.
Architecture: a pure source planner feeds a revision-checked Windows file service
and a terminal rehearsal/receipt renderer. File recovery is separate from syntax.
Tech stack: existing .NET 10, Spectre.Console 0.49.1, xUnit 2.9.3; no new packages.
Spec: ../specs/2026-09-06-title-repair-design.md and ../../EXPERIENCE-DESIGN.md.

## Global constraints

- Original DERT syntax and authoritative source artifacts stay unchanged.
- Only the title rule may change; source newlines, BOM, prose and notes survive.
- Preview is read-only until explicit confirmation or action-bound --apply.
- Stale revisions and unsafe undo refuse to write.
- Retain original bytes before committing; never report an unverified success.
- Unicode width beyond printable ASCII is explicitly unavailable, not guessed.
- No package operations, editor installation, or automatic source rewriting.
- No Git repository is present; no branch or commit is created.

## 1. Exact source repair

Files: src/Dert.Core/TitleRuleRepair.cs; tests/Dert.Tests/TitleRuleRepairTests.cs.
Interfaces: TitleRuleRepair.Plan(DertDocument) -> TitleRepairPlan; plan.State,
Explanation, Edit, TitleLine, RuleLine, Width, Source, ProposedText, ApplyTo(string).
SourceEdit carries Span, Before, After. States Ready, Aligned, NoTitle, Unavailable.

- [x] Write literal before/after tests for missing, short, long and bare rules.
- [x] Run against the unimplemented planner and verify failure.
- [x] Implement one bounded edit from existing source nodes and line ranges.
- [x] Verify the edit preserves EOF, newlines, BOM and unrelated source.
- [x] Verify refusal for changed source and unavailable width; verify idempotence.

```csharp
var plan = TitleRuleRepair.Plan(DertParser.Parse("// TITLE\n// --\nwords"));
Assert.Equal("// TITLE\n// -----\nwords", plan.ProposedText);
Assert.Equal("// --", plan.Edit!.Before);
Assert.Throws<InvalidOperationException>(() => plan.ApplyTo("// changed"));
```

## 2. Guarded application and undo

Files: src/Erpdert.Cli/Repairs/RepairFileService.cs, RepairSnapshot.cs,
RepairReceipt.cs, WindowsFileGuard.cs; tests/Dert.Tests/RepairFileTests.cs,
ScratchDocument.cs. File identity/paths and recovery live here, not in the parser.
Interfaces: Preview(string) -> RepairSnapshot; Apply(RepairSnapshot) -> RepairResult;
PreviewUndo(string) -> RepairSnapshot; snapshot.Revision, ApprovalToken, Identity, Before, After, Plan,
ReceiptDirectory, IsUndo, CanApply; result.Message, ReceiptDirectory.
IRepairWriter.Rewrite(FileStream, byte[]) permits deterministic I/O-failure tests.

- [x] Write real-file tests for preview/no-write, apply, exact undo, stale apply,
  later edits and locks; observe failure. Extend with encoding/receipt checks.
- [x] Implement exclusive Windows access, guarded paths, revision checks and
  flushed receipt creation before committing; reread and compare bytes afterward.
- [x] Inject a partial write failure, verify rollback and retained recovery.
- [x] Verify hard-link refusal and no mutation on unavailable recovery storage.

```csharp
var preview = service.Preview(file.Path);
service.Apply(preview);
Assert.Equal("// TITLE\n// -----", File.ReadAllText(file.Path));
service.Apply(service.PreviewUndo(file.Path));
Assert.Equal(originalBytes, File.ReadAllBytes(file.Path));
```

## 3. The terminal rehearsal

Files: src/Erpdert.Cli/RepairCommand.cs, RepairRenderer.cs;
modify CliApplication.cs, CliOptions.cs, DocumentRenderer.cs;
tests/Dert.Tests/RepairCliTests.cs; tests/Invoke-RepairSmoke.ps1.
Share literal-safe text rendering and the existing palette; do not duplicate
injection-prone markup handling. Confirmation is a user-input boundary that tests
can supply; actual file service and rendering remain real in command tests.

- [x] Test preview output, no writes, confirmation/cancel, action-bound apply,
  undo, stale/corrupt refusal, escaping, unrelated diagnostic status and --plain.
- [x] Add repair/undo routes and explicit --apply <approval-token> validation.
- [x] Render exact local diff, width explanation, original-untouched state,
  verified completion and recoverable-original/undo commands.
- [x] Run actual 80-column terminal preview/cancel/apply/undo; test the real
  renderer at 32 columns, including unbroken copy commands.
- [x] Request independent code review, correct confirmed issues with regressions.
- [x] Build/test/format, publish and run the complete repair smoke sequence.
- [x] Update README, implementation status, changelog, design status and evidence.

```powershell
dotnet test ERPDERT.slnx
dotnet build ERPDERT.slnx -c Release --no-restore
dotnet format ERPDERT.slnx --verify-no-changes --no-restore
dotnet publish src/Erpdert.Cli -c Release -r win-x64 --self-contained true -o .artifacts/win-x64
.\tests\Invoke-PublishedSmoke.ps1
.\tests\Invoke-RepairSmoke.ps1
```

## Implementation and review record

Completed 2026-09-06. Release: 108 tests, zero build warnings/errors, clean format
verification, self-contained Windows x64 publish, seven inspection smoke checks
and twelve repair smoke checks. Protected source hashes remained unchanged.
Actual interactive runs covered default cancellation, invalid input, application
and undo; the final published plain prompt's invalid-input path was rechecked.

Review improved the design without changing DERT syntax:

- Recovery ownership now follows the opened physical file, not an uppercased
  path; different documents occupying one pathname cannot share originals.
- Recovery order is monotonic, not clock-dependent.
- Approval binds input **and output**, identity, operation, path and undo record;
  a stale command cannot approve a different undo with identical repaired bytes.
- Alternate streams are refused as mutation targets; real hard links are tested.
- PowerShell smart quotes and invisible filename characters remain literal path
  data through visible character expressions. Copy commands use the actual
  executable and are never hard-wrapped by the renderer.

The first four ownership/order/approval/stream cases and smart-quote cases were
reproduced as failures before correction. Encoding, backup-tamper and narrow-output
tests add coverage of already-implemented guards. Final independent source review
reported no remaining important concern in the bounded increment. Broader Unicode
width, hostile filesystem races, power-loss atomicity, clean-machine installation
and human usability/accessibility evaluation remain explicitly unproven.

See ../../IMPLEMENTATION-STATUS.md for the retained evidence paths and full limits.
