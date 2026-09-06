# Language workbench: source, explanation, and reversible repair

2026-09-06. This records implemented behavior, not a canonical language release.
The original language notes and subsequent user decisions retain authority.

## What runs

`ERPDERT -help`, `inspect <file>`, `lint <file>`, `repair <file>`, and `undo <file>`
run in the Windows executable.
`inspect --at <line>` connects an entry to its own explanation, containing note,
and closing boundary. Inspection folds reasoning by default; `--expand` and
`--at` are inspection views, not changes to the authored format or its folding
policy. Incomplete folds carry `missing //` on their handles.

The CLI reads UTF-8, including a UTF-8 BOM. Invalid encoding gets an English
message instead of replacement characters or an automatic conversion. Both
commands are read-only. Neither dispatches shell commands or package operations.

Exit 0 means no errors were found by the implemented checks; warnings/notices
can remain. Exit 1 means a structural error was found. Exit 2 means the request
could not be processed, such as an invalid option, file, encoding, or line number.
Exit 0 is explicitly not a claim of full grammar or operational validity.

For `repair`/`undo`, exit 0 means a completed rehearsal, cancellation, no-op or
verified edit; unrelated lint findings can remain. Exit 1 means the repair was
unavailable, refused or failed. Invalid command options exit 2. These commands
never execute DERT operations or manage packages.

## Title-rule repair: exact edit, explicit choice

`repair` derives one bounded edit from the existing source model. It measures
the detected main title, shows the actual adjacent rule and proposed replacement,
and explains their difference. A missing rule is inserted before following prose,
not written over it. A bare hyphen rule can receive its `//` prefix. Aligned rules
are left alone. Title spelling, original reasoning, commas and other source stay
unchanged; no leading indentation is generated.

An actual terminal rehearsal for a generated document shows:

```text
DERT2003 / TITLE RULE · LINE 2
The line under your title is 10 marks short.

      1 │ // MY FIRST TITLE
−     2 │ // ----
+     2 │ // --------------

14 title columns → 14 rule marks.
```

The title uses warm gold; removed/added rows use rose/green, plus literal signs.
The real confirmation defaults to leaving the file untouched. A mistaken answer
gets an English choice reminder; `--plain` includes that path without color.
The gutters are separate from the source column so wrapped diff text does not
become a false line number. No timer or pretend validation is used.

Redirected output prints a complete PowerShell approval command without inserted
line breaks. It invokes the launched executable, so no PATH install is needed.
`--apply <approval-token>` names the whole edit: operation/version, path, physical
file identity, original byte hash, proposed byte hash, and undo selection. Changed
source, a replacement file, or a different undo receipt invalidates the approval.
PowerShell filenames preserve literal apostrophes; smart single quotes and hidden
characters are represented with visible `[char]` expressions.

Before writing, the service takes exclusive Windows access, checks bytes and
identity again, and saves/flushes/verifies an original plus receipt. It writes
through that held handle and checks the result before showing `TITLE ALIGNED`.
An I/O failure attempts rollback and verifies it; failed rollback reports the
retained original, not success. `undo` rehearses restoring the latest repair's
original bytes and refuses to erase later changes. It validates the receipt,
recomputes the forward repair, and checks the backup again before committing.

Recovery is under the sibling `.erpdert/repairs/<identity-hash>/<sequence-guid>`
directory. Originals are full **unencrypted local copies**, never automatically
deleted or uploaded. Keep them private. Copying/replacing a file or moving it away
from its recovery storage can disconnect automatic undo; readable backups remain.
Ordering uses a local monotonic sequence, not the wall clock. This is the latest
title repair's undo, not an arbitrary multi-level edit history.

Current limits: strict UTF-8, printable ASCII title width, regular local Windows
files. Non-ASCII/control title width is explicitly unavailable; no transliteration
or width guess occurs. Existing LF/CRLF/CR, BOM, and EOF style are preserved. A
title at EOF with no local newline gets a disclosed CRLF before its new rule,
without a new final newline. Links, redirected paths, alternate streams and UNC
mutation are refused. Writes are recoverable, not power-loss atomic. Active
hostile filesystem races are outside the guarantee. Timestamp restoration is
not promised. See the [full repair contract](superpowers/specs/2026-09-06-title-repair-design.md).

## Original example acceptance

The tests extract the first DERT fence directly from `original.md`. Its spelling,
English commas, final annotated-entry comma, flush-left lines, and florin are
not repaired or normalized to make the tests pass.

That example produces:

- One `STERT` chain with its explicit `ƒin.` boundary.
- Two closed reasoning notes, preserving complete raw targets and payloads.
- Two free notes about pip/python and npm/node.
- One `support doctoring ; conflicts` reasoning block with three entries.
- Each entry's own annotation, not a flattened block-wide comment.
- A bare `//` closer followed by `commands ; updateme ;;` outside the note.

The terminal connection for its `fixme` entry includes:

```text
BELONGS HERE
Belongs to  support doctoring ; conflicts
Value       fixme
Context     the confirmation command to fix the conflict
Comma       present in the source; preserved
Boundary    standalone // on line 12 closes this reasoning note
```

The same entry is line 37 in `RERDMERR.DERT`; source positions are document-local.
Console gutters and fold handles are presentation, never serialized indentation.
No depth or ownership is inferred from leading whitespace. Existing whitespace
in imperfect input is preserved, not introduced as a new authoring rule.

## Implementation boundaries

| Layer | Implemented responsibility | Deliberately not inferred |
| --- | --- | --- |
| `SourceText` / `DertLexer` | Original characters, BOM, LF/CRLF/CR, source lines, punctuation candidates | A slash token is not automatically an annotation or boundary |
| `HeaderReader` / `DertParser` | Contextual note roles, entries and explanations, chain/statement shapes, source spans, recovery | No package command schema or arbitrary recursive blocks |
| Semantic presentation | Title/rule roles, distinct hook/line/target/sinker/note roles, fold extents and closure state | No file edits or editor installation |
| CLI renderer | Folded/expanded inspection, ownership view, English diagnostics, timestamp, literal content | No fabricated checks, repair success, or operation receipts |
| `TitleRuleRepair` | Pure exact source edit, local rule/width explanation, source-bound application | No metadata changes, recursive syntax changes, or guessed Unicode width |
| Repair file service / rehearsal renderer | Action-bound approval, guarded Windows writes, retained originals, undo, exact diff and verified results | No general formatter, command execution, power-loss atomicity or unrestricted history |

The core has no terminal dependency. The CLI uses Spectre.Console 0.49.1;
the core and CLI target .NET 10. Tests use xUnit 2.9.3. Versions are pinned,
not claimed as the newest available packages.

All source offsets and lengths are UTF-16 code-unit ranges, not byte offsets
or display columns. `DertDocument.Source` remains the exact input string, and
concatenating token slices reconstructs it. Semantic fields trim surrounding
whitespace for interpretation while retaining their spans into that source.
Line endings stay in source and line spans; rendering does not round-trip them
through a formatter.

Highlights are semantic overlays; later matching roles take precedence. The
opening `//` of a block entry differs from the second `//` introducing that
entry's explanation. Quoted markup such as `[red]` is rendered literally.
Terminal control and Unicode format characters are displayed as `\uXXXX`, not
sent as executable terminal controls. This display policy is separate from
preserving their original characters in the source model.

## Implemented diagnostics

These identifiers belong to this implementation, not a frozen public rule set.

| Code | Level | Explanation and current behavior |
| --- | --- | --- |
| DERT1001 | Error | A reasoning block lacks its standalone `//`. Retain entries and point at the opener. An unprefixed following line is recovered outside the incomplete block for inspection only. |
| DERT1002 | Error | A same-line reasoning note lacks its closing `//`. Retain its raw payload. |
| DERT2003 | Warning | The detected main title lacks its adjacent `//` rule, or its ASCII rule width does not match. Inspection/lint never writes; `repair` rehearses an explicitly approved correction. |
| DERT3003 | Notice | An expanded active payload ends with a continuation comma. Preserve it and leave the author's completion decision open. This does not reject the original reasoning block's final comma. |
| DERT3004 | Notice | A title needs a non-ASCII display-width check that this build does not perform. Do not pretend its rule was fully checked. |

`RERDMERR.DERT` currently yields DERT2003 on its bare rule at line 2 and DERT3003
on its final active entry at line 54: zero errors, one warning, one notice.
Neither original artifact is corrected to suppress those results.

Repair-specific codes are separate from grammar diagnostics:

| Code | Meaning |
| --- | --- |
| DERT4001 | No proposed edit is available to apply. |
| DERT4002 | Source, file identity, approval or undo eligibility changed; refuse the edit. |
| DERT4003 | No saved repair is available for this file identity. |
| DERT4004 | Recovery validation failed, backup changed, or recovery sequence cannot be extended. |
| DERT4100 | File/encoding/recovery storage could not be used safely, including locks and malformed receipt JSON. |
| DERT4101 | Mutation targets a linked, redirected, stream, nonlocal or unsupported platform path. |
| DERT4102 | Write failed; the prior bytes were restored and verified. |
| DERT4103 | Write and rollback failed; manual recovery from the retained original is needed. |

## Provisional behavior and remaining interpretation work

These implementation choices keep the source inspectable. They do not become
canonical syntax merely because code now embodies them.

- Main-title detection currently uses the first nonempty free note before any
  other document node. This position policy still needs explicit language review.
- Double-quoted regions protect embedded markers from structural recognition;
  an odd preceding backslash run protects a quote. The full quoting/escaping
  grammar is not settled. Quotes and escapes are not decoded or rewritten.
  An unfinished quote cannot supply a false same-line `//` closer.
- Annotation recognition currently requires whitespace before `//` (or the
  beginning of the field), outside the protected quote region. Ordinary URL
  slashes are retained. No-whitespace and further literal cases remain probes.
- Expanded active values currently remain pending across blank lines, free
  notes, and structured reasoning notes. Those reasoning notes retain their own
  entries and ownership; following unprefixed values can resume the pending
  active list. This is provisional inspection behavior, not a canonical attachment
  decision. A final value without a comma, a new active header/chain boundary, or
  EOF ends that list. Semicolon-bearing unquoted literal values still need an
  explicit policy.
- `STERT`, including its demonstrated header-bearing forms, is structurally
  recognized. `fin.` and `ƒin.` are recognized without modifying spelling.
  Missing terminators remain absent in the model; no required-terminator rule
  or implicit closure semantics are asserted. A new `STERT` separates recovered
  source regions, not proof that an omitted terminator is canonically valid.
- Historical `START` is not silently promoted to `STERT`. Unknown text stays
  present, but there is no exhaustive unknown-syntax diagnostic yet. A compact
  invocation's header is retained; its operational meaning is not executed.
- Reasoning targets and payloads remain raw fields. The code does not turn
  every English comma into a metadata array or infer package-support facts from
  note prose. Typed metadata, normalization, and JSON interchange remain later work.

## Verification evidence

The automated suite exercises the real core and CLI, without mocked parser
results. Test batches were run against missing behavior before implementation.
Inspection-led regressions cover incomplete fold status, slash color roles,
unfinished quotes, and readable singular counts. An independent read-only review
found a help-path validation bypass, repeated copying of expanded lists, and
supplementary hidden Unicode characters escaping the display policy. Each received
a failing regression before correction; the focused re-review found no remaining
important issue in those fixes.

- 108 tests pass in the release suite, including 300 deterministic random input
  fragments checked for lexical preservation and in-bounds highlight spans.
- The original example's ownership assertions and the full original corpus run
  in the suite. CR, LF, CRLF, BOM, invalid UTF-8, URLs, English commas, quoted
  markers, missing closers, expanded values, and no-write behavior have checks.
  A 10,000-entry list also has a generous allocation-bound regression to catch
  copying all preceding entries on every addition, without a timing benchmark.
- The release solution build passed with zero warnings/errors, and format
  verification passed. Compiler warnings are treated as errors.
- `tests/Invoke-PublishedSmoke.ps1` exercises the actual published executable:
  help; the original folded example; entry ownership; a removed block closer;
  original corpus lint; a nonexistent file; and a real file literally named `help`.
  It checks expected output and
  exit codes, hashes protected sources and fixtures, and retains terminal output.
- Self-contained Windows x64 publishing includes the .NET 10.0.8 runtime in
  `.artifacts/win-x64`; the executable has been run directly in this environment.
- Real terminal output has been inspected in plain mode and in a color-capable
  PTY. The latter used process-local capability settings because the tool host
  normally sets `NO_COLOR=1` and `TERM=dumb`. User profile settings were not changed.
- Repair coverage includes literal before/after spans, missing/short/long/bare
  rules, unchanged prose/reasoning, local line endings, BOM/EOF, unavailable width,
  stale approvals, replaced files, real locks/read-only/hard-link/alternate-stream
  refusal, damaged receipts, backup tampering, and injected partial-write/rollback
  failures. The 32-column renderer test checks an intact copy-command line.
- Independent repair review led to failing regressions for file/undo ownership,
  clock-independent ordering, whole-action approval, named streams and smart
  PowerShell quotes before their corrections. Final focused review found no
  further important issue in those changes; it was source review, not a test run.
- `tests/Invoke-RepairSmoke.ps1` passes 12 actual-executable checks: corpus-copy
  preview, copied application, aligned no-op, undo preview, copied undo, restored
  no-op, fresh preview, stale refusal, and four quoted-filename/EOF/BOM checks.
  It executes the emitted PowerShell commands using the actual executable path,
  compares original/restored hashes, and verifies protected originals are unchanged.
- Actual interactive runs exercised styled preview/default cancellation, plain
  invalid input and approval, styled undo, and exact original restoration. The
  final published build's plain invalid-answer path was checked again after fixing
  a default prompt color leak. No terminal capability/profile changes were persisted.

Retained final published smoke output from this increment:

- `.artifacts/proof/88ce344e55a047de800f5cf6a64f50f7/terminal-output.txt`
- `.artifacts/repair-proof/a48e934bb5e346e299f62799edc3ab5c/terminal-output.txt`

These generated artifacts are local evidence, not required source files.

Reproduce from the workspace root:

```powershell
dotnet format ERPDERT.slnx --verify-no-changes --no-restore
dotnet build ERPDERT.slnx -c Release
dotnet test ERPDERT.slnx -c Release --no-build --no-restore
dotnet publish src/Erpdert.Cli -c Release -r win-x64 --self-contained true -o .artifacts/win-x64
.\tests\Invoke-PublishedSmoke.ps1
.\tests\Invoke-RepairSmoke.ps1
```

This evidence is not a usability study, screenshot review, clean-machine install
test, full language conformance test, or verification of package-manager behavior.
The repair diff separates wrapped content from its gutter. Older inspect/lint
source rows still use the earlier wrapping behavior and deserve the same treatment.
Neither automated renderer checks nor inspected terminal output prove human delight
or accessibility with screen readers; those need direct evaluation.

## Next complete interaction

Title-rule rehearsal is implemented. Next: an unfinished edit handled in context,
showing the existing source ownership and the exact choices where author intent
is required. Do not auto-close a note if that silently changes what it owns.
Non-ASCII width still needs its own measured policy before automatic correction.

This work does not authorize speculative comma removal, metadata reinterpretation,
indentation, or package operations. The eventual adapters, editor integration,
installation experience, and note application remain separate product milestones.
