# Original DERT language core implementation plan

> For agentic workers: execute task by task using the executing-plans workflow.
> This session continues locally under the user's explicit instruction to develop.

Goal: recognize the original DERT example without changing its source and make
its note relationships inspectable through a working Windows CLI.

Architecture: a lossless source/lexical layer feeds a context-aware parser.
The parser produces source-addressable notes, reasoning blocks and entries,
chains, statements, presentation spans, folds, and diagnostics. A separate
terminal application consumes that model; it does not execute DERT operations.

Tech stack: .NET 10/C#, Spectre.Console 0.49.1 for terminal rendering,
xUnit 2.9.3 for tests. SDK 10.0.204 is installed in this workspace environment.
Versions are pinned for reproducibility, not claimed to be the newest.

Spec: ../../SYNTAX-LAB.md and ../../EXPERIENCE-DESIGN.md, especially the original
example and the correction requiring contextual nesting with flush-left source.

## Global constraints

- Preserve the exact original example, including commas, English, and slashes.
- Leading whitespace never encodes ownership or recursion.
- An entry's explanation belongs to that entry; the bare closer ends the block.
- Source text, line endings, offsets, and punctuation remain available unchanged.
- Metadata fields preserve their complete raw text. Typed schemas remain later work.
- Preserve uncertain forms; do not silently remove hooks, commas, or terminators.
- First increment inspects/lints only. No package modification or DERT execution.
- All terminal source text is literal, with terminal control characters made visible.
- No canonical v1.0, full language conformance, or editor product is claimed.

## Task 1: source and original relationships

Files: src/Dert.Core/SourceText.cs, Syntax.cs, DertLexer.cs, DertParser.cs,
HeaderReader.cs; tests/Dert.Tests/OriginalSyntaxTests.cs.

Interfaces: DertParser.Parse(string text) returns DertDocument with Source,
Tokens, Nodes, Diagnostics, Highlights, and Folds. Nodes retain TextSpan
(UTF-16 offset/length), line number, and typed children.

- [x] Write tests that extract the original code block directly from original.md.
- [x] Run and record failure before implementing the parser.
- [x] Implement source lines and a lossless token stream.
- [x] Implement contextual recognition for the original closed notes, free
  notes, annotated block, statement, STERT, and florin terminator.
- [x] Run the original-source tests and inspect every ownership assertion.

Core acceptance shape:

```csharp
var document = DertParser.Parse(originalExample);
var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
var block = Assert.Single(chain.Children.OfType<ReasoningBlockNode>());
Assert.Equal("support doctoring", block.Header.Head.Text);
Assert.Equal("conflicts", block.Header.Target!.Text);
Assert.Equal(3, block.Entries.Count);
Assert.Equal("fixme", block.Entries[2].Value.Text);
Assert.Equal("the confirmation command to fix the conflict",
    block.Entries[2].Annotation!.Text);
Assert.Single(chain.Children.OfType<StatementNode>());
```

## Task 2: recovery and presentation data

Files: tests/Dert.Tests/RecoveryTests.cs, SourceTests.cs;
src/Dert.Core/DertParser.cs and Syntax.cs. Diagnostic/presentation records are
kept with the source model in Syntax.cs rather than separate tiny files.

- [x] Write failing cases for a missing block closer, URL slashes, English
  commas, source positions, and leading spaces that do not create nesting.
- [x] Implement diagnostics without editing or discarding source.
- [x] Recognize source header forms and expanded statement payloads from
  RERDMERR; preserve fragments and missing fields without inventing semantics.
- [x] Produce semantic highlighting roles and reasoning fold regions.
- [x] Verify LF/CRLF/BOM preservation, original corpus parsing, and recovery.

Recovery acceptance:

```csharp
var document = DertParser.Parse(
    "// support doctoring ; conflicts ;;\n// fixme, // repair\ncommands ; updateme ;;");
Assert.Contains(document.Diagnostics, d => d.Code == "DERT1001");
Assert.Contains(document.Nodes, n => n is StatementNode);
Assert.Equal(document.Source.Text,
    string.Concat(document.Tokens.Select(t => document.Source.Slice(t.Span))));
```

The recovered statement remains inspectable; it is not executed. A warning for
an uncertain shape is not a claim that the original language forbids that shape.

## Task 3: usable inspect/lint host

Files: src/Erpdert.Cli/Program.cs, CliApplication.cs, DocumentRenderer.cs;
tests/Dert.Tests/CliTests.cs.

- [x] Write failing command tests for default help, literal file content,
  focused entry ownership, lint exit status, missing files, and no source writes.
- [x] Implement inspect <file> with folded reasoning by default, --expand,
  --at <line>, and --plain. Focus shows original value, annotation, and owner.
- [x] Implement lint <file> with source-cited English diagnostics and explicit
  check/uncertainty status; errors return 1, usage/IO errors return 2.
- [x] Inspect real output for the original example and RERDMERR, including a
  damaged copy. Verify display does not add source indentation.
- [x] Run release build/tests and a Windows publish smoke check.
- [x] Update README, source implementation status, changelog, and evidence.

Verification commands:

```powershell
dotnet test ERPDERT.slnx
dotnet build ERPDERT.slnx -c Release
dotnet run --project src/Erpdert.Cli -- inspect RERDMERR.DERT --at 37 --plain
dotnet run --project src/Erpdert.Cli -- lint RERDMERR.DERT --plain
dotnet publish src/Erpdert.Cli -c Release -r win-x64 --self-contained true -o .artifacts/win-x64
```

## Stack decision and boundaries

C# supplies an explicit model usable by a Windows executable and later hosts.
Spectre handles terminal capability/rendering concerns while DERT owns source
roles and presentation. The core has no terminal dependency.

Microsoft documents self-contained distribution with the runtime included:
[single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).
This increment first verifies a self-contained folder; an installer and full
authoring surface remain separate deliverables. JSON interchange remains deferred.

This folder is not currently a Git repository. No branch, worktree, or commit
is created as part of this implementation.

## Execution evidence

The initial four original-example tests failed against an empty parser, then
passed after contextual parsing was implemented. Recovery and CLI batches
likewise exposed missing behavior before implementation. Preservation tests
that already passed were retained as characterization checks, not falsely
reported as previously failing tests.

Terminal inspection led to regression tests and fixes for unclosed fold status,
the entry-opening slash role, unfinished-quote closure, and singular counts.
The final verification at this checkpoint passed 46 release tests, a release
build with zero warnings/errors, format verification, Windows self-contained
publishing, and seven published-executable smoke scenarios. Runtime downloads
and a refreshed restore required approved network access in the sandbox.

The smoke check is repeatable through tests/Invoke-PublishedSmoke.ps1. It extracts
the original fence itself, retains a damaged copy and terminal evidence beneath
.artifacts/proof, and verifies protected source and fixture hashes.

Independent review exposed three additional issues: a file named help bypassing
validation, repeated copying of expanded values, and hidden supplementary Unicode
format characters. Failing regressions were added, each fix verified, and the
fixes re-reviewed. The source interpretation for active-list continuation across
structured reasoning remains documented as provisional, not newly canonical.

The implementation scope, provisional interpretation choices, and next repair
interaction are recorded in ../../IMPLEMENTATION-STATUS.md. Completion of this
plan does not complete the larger ERPDERT product or freeze its grammar.
