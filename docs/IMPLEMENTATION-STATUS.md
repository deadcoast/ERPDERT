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
message instead of replacement characters or an automatic conversion. `inspect`
and `lint` are read-only. No command dispatches DERT operations or package actions.

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
source repair's undo, across title and note repairs, not an arbitrary multi-level
edit history. An already-undone latest record does not expose an older one.

Current limits: strict UTF-8, printable ASCII title width, regular local Windows
files. Non-ASCII/control title width is explicitly unavailable; no transliteration
or width guess occurs. Existing LF/CRLF/CR, BOM, and EOF style are preserved. A
title at EOF with no local newline gets a disclosed CRLF before its new rule,
without a new final newline. Links, redirected paths, alternate streams and UNC
mutation are refused. Writes are recoverable, not power-loss atomic. Active
hostile filesystem races are outside the guarantee. Timestamp restoration is
not promised. See the [full repair contract](superpowers/specs/2026-09-06-title-repair-design.md).

## Note-boundary repair: show the ownership decision

`repair <file> --at <line>` selects an unfinished multiline reasoning block
through its opener, entry, or spacing inside the recovered block. It displays
that current grouping explicitly as recovery, not as confirmed author intent.
Choices are before each entry's original source line and at the recovery stop,
or `eof` when the block reaches EOF. There is no automatically preferred ending,
arbitrary cut, separate blank-line cut, or duplicate EOF choice.

The interactive choice defaults to `leave`. Choosing a boundary opens a second
rehearsal showing:

- The existing opener and local insertion context, with a single added `//`.
- Every retained entry, including its original comma and annotation.
- Each released entry's original/result line, actual new role, actual owner,
  and resulting semantic highlight. A released opener can own later entries;
  those lines are not indiscriminately labeled ordinary notes.
- Findings from the reparsed proposal, without claiming complete validity.
- Local newline/EOF behavior, the local original-copy location, and a separate
  default-no confirmation. Ownership changes are not called presentation-only.

`--close-before <line|eof>` directly selects one offered boundary and opens the
same rehearsal. It requires `--at`. Redirected choice output prints complete
PowerShell selection commands without approval tokens; a concrete rehearsal
can print its action-bound `--apply <token>`. That token also requires the cut.
Selection uses the originally captured source and physical identity. Changed
source while choosing or confirming withdraws the edit; it is not silently
refreshed to a different plan.

The core inserts one standalone marker plus its required line ending. It
reuses the nearest preceding newline, or discloses Windows CRLF when none exists.
Existing characters, UTF-8 BOM, mixed newlines and final-newline presence remain
intact. The reparse verifies the selected block's closer and every retained
entry's value, annotation, comma and source location. Unicode note prose is
supported independently of title-width limitations. Parser recognition/recovery
was not changed in this increment.

`undo` dispatches the correct diff for either repair kind. Version-1 title JSON
receipts remain unchanged; version-2 note receipts store the kind, opener line
and selected cut. Recovery reconstructs only a supported selection against
the saved original. Invalid latest receipts cannot fall back to an older one.
Apply rechecks the latest undo receipt directory under exclusive target access,
so an old preview cannot revive after a later repair/undo returns identical text.
Undo restores the unfinished note and its prior diagnostic, not an invented
clean original. Shared file guards, backup verification and rollback still apply.

Inline reasoning notes (DERT1002), completed blocks, unoffered cuts and active
lists are not eligible for this repair. No commas, targets, operational meaning,
indentation or additional grammar are automatically changed. Source rows now
separate wrapping from the line-number gutter across inspect, lint and repair.
See the [note-boundary contract](superpowers/specs/2026-09-06-note-boundary-design.md).

## Lint-to-rehearsal handoff: the finding carries its next action

`lint` now displays findings in source order, each with its own explanation,
source row and capability-aware next step. DERT2003 offers a title rehearsal only
when the actual title planner is ready: a missing underline on a non-ASCII title
can produce that warning while repair remains unavailable. DERT1001 resolves its
exact unfinished-block opener and offers its boundary-choice view without choosing
a cut. DERT1002, DERT3003 and DERT3004 explain the unsupported repair capability.
The parser, diagnostic list, severities and lint exit meanings are unchanged.

The command under a supported finding is `repair <absolute-file>` with `--at`
only for a note opener, plus `--from-lint <SHA256-of-linted-bytes>`. It uses the
launched executable (including the assembly when hosted by dotnet) and existing
literal-safe PowerShell handling. Commands remain complete logical lines at a
32-column output width. Plain mode is carried as an option, not assumed from
the terminal environment. The file is resolved before lint reads it, so copying
the command from a different working directory still refers to that file.

`--from-lint` is repair-only, requires one exact 64-character source revision,
and cannot combine with `--apply`. The file service checks the captured raw bytes
before parsing/locating the title or note. Changed bytes receive DERT4002 and a
request to lint again; the old location cannot silently select a different block.
The source revision is not a physical-file identity, authorization or approval.
A byte-identical replacement can open the same read-only rehearsal; any eventual
approval is separately bound to the repair snapshot's physical file identity.
No write-access guarantee is made by displaying a rehearsal link; existing
platform/path guards still govern mutation.

Initial handoff and copied note-boundary choices always return previews without
selection/confirmation prompts, including interactive terminals. A banner confirms
the matched source and preview-only state. The choice commands retain the same
revision and plain mode through the selected preview. That preview emits the
existing separate `--apply` token, without `--from-lint`, and retains plain mode.
Ordinary direct `repair`/`undo` behavior is unchanged. Lint and all handoff previews
create no recovery history and write no source bytes.

This is a bounded extension of the existing diagnostic/repair flow. Generic help,
unbound path/line commands and source-bound previews were compared. The selected
approach carries the context for the author and refuses stale locations without
turning missing intent into an automatic repair. No new grammar or repair type,
operational adapter, runtime dependency or installation step was introduced.

## Verified repair follow-through

After an approved title repair, note-boundary repair, or undo, `VERIFIED RESULT`
shows the remaining findings with the same source-adjacent capability explanations
and `--from-lint` rehearsal links as lint. Insertion-shifted line numbers and newly
released note structures come from reparsing the verified result, not the old input.
Unsupported findings do not advertise a repair. No-op, preview, cancellation, and
failed operations do not receive a verified-success report or follow-up actions.

`RepairFileService` retains its guarded write and exact readback comparison.
Its result now captures immutable source text and the raw-byte hash from that
readback while the file is still held. Completion does not reopen the path; later
edits therefore cannot be misreported as the verified repair. A copied link checks
those bytes again before selecting a note. No choice or approval is carried over.

The action's successful exit remains separate from unrelated lint errors. Undo
can restore an unfinished document and reports it as such. A result without findings
claims only the supported structural checks, never validated command meanings or
executed DERT operations. Plain mode now follows all copied approval and undo
commands, not only approvals reached through lint.

## Contextual-note coverage

The tests read the independent `tests/Fixtures/contextual-notes.DERT` input.
It retains the demonstrated note relationships, English commas, final annotated-entry
comma, flush-left lines, and florin. No Markdown extraction or dependency on the
reference documents remains. Those documents are human reference material, not
executable contracts; changing or moving them does not change the test inputs.

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
| DERT4005 | No eligible unfinished reasoning block at the selected line, or the selected cut is not supported. |
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

- 218 tests pass in the release suite, including 300 deterministic random input
  fragments checked for lexical preservation and in-bounds highlight spans.
- Contextual-note ownership and a standalone mixed-structure fixture run
  in the suite. CR, LF, CRLF, BOM, invalid UTF-8, URLs, English commas, quoted
  markers, missing closers, expanded values, and no-write behavior have checks.
  A 10,000-entry list also has a generous allocation-bound regression to catch
  copying all preceding entries on every addition, without a timing benchmark.
- The release solution build passed with zero warnings/errors, and format
  verification passed. Compiler warnings are treated as errors.
- `tests/Invoke-PublishedSmoke.ps1` exercises the actual published executable:
  help; folded reasoning notes; entry ownership; a removed block closer;
  mixed-structure lint; a nonexistent file; and a real file literally named `help`.
  It checks expected output, exit codes and unchanged test inputs, and retains
  terminal output. It does not read or hash reference documents.
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
- `tests/Invoke-RepairSmoke.ps1` passes 12 actual-executable checks: mixed-structure
  preview, copied application, aligned no-op, undo preview, copied undo, restored
  no-op, fresh preview, stale refusal, and four quoted-filename/EOF/BOM checks.
  It executes the emitted PowerShell commands using the actual executable path,
  and compares the generated test file's before/restored hashes.
- Actual interactive runs exercised styled preview/default cancellation, plain
  invalid input and approval, styled undo, and exact original restoration. The
  final published build's plain invalid-answer path was checked again after fixing
  a default prompt color leak. No terminal capability/profile changes were persisted.
- Note-boundary coverage adds 25 core, 18 real-file and 23 CLI cases. These cover
  exact original-example edits, CR/LF/CRLF and EOF variants, Unicode/BOM, actual
  released AST ownership despite overlapping statement spans, no-write choices,
  two-stage permission, stale selections, legacy and typed receipts, mixed
  latest-history ordering, tamper refusal and injected partial-write rollback.
- Independent note review produced failing regressions before fixes for stale
  undo selection after an identical-byte round trip, inaccurate inline-note
  labeling, and omitted retained entries in direct selected previews. The core
  EOF regression also preserves semantic fields when the inserted newline extends
  the last physical entry span. Review was read-only, not a substitute for tests.
- `tests/Invoke-NoteRepairSmoke.ps1` passes 13 actual-executable checks: original
  choices, copied selection/application/undo, restored no-op, released ownership,
  changed-source refusal, unoffered-cut refusal, and quoted Unicode/BOM/EOF
  reversal. An initial smoke assertion was corrected to allow terminal wrapping
  within an English refusal; the refusal itself was already working.
- Real note interaction: styled default-leave selection; plain invalid cut,
  valid early cut, invalid confirmation and default cancellation; styled early
  cut approval and undo. Source-row semantic roles visibly change with the
  proposed ownership. Original/restored interactive fixture SHA256 is
  `D7788CB17666315F989135311AAEBE191F349A0AB8AEABDD2B29D71AADA4279D`.
- Lint-handoff coverage adds 26 CLI/real-file cases. The initial 16 behavior
  cases failed before implementation; seven existing no-action/refusal cases
  already passed. Two later regressions first failed for source/plain continuity
  through copied choices/approval, and another for exposing parser check order.
  Checks use real parser/planner/file behavior, including interactive consoles
  with callbacks that throw if a handoff asks to choose or write.
- `tests/Invoke-LintHandoffSmoke.ps1` passes 14 published-executable checks:
  corpus lint, copied title link from another directory, original missing-closer
  lint, copied note handoff/selection, unsupported width, unusual filename/BOM
  lint and copied handoff, two stale-link refusals, fresh rehearsal, separate
  approval, undo rehearsal and byte-exact undo. Plain output and no-write/no-history
  handoffs are checked; the approval/undo use owned generated copies only.
- Real PTY inspection covered styled lint/title handoff and plain note lint,
  boundary choices and selected early-cut rehearsal. Handoffs exited without
  prompts; checked fixture hashes remained unchanged. The terminal review led
  to source-order display, keeping each explanation/action with its own finding.
- Independent read-only review confirmed the no-prompt/raw-byte-check paths and
  identified loss of source/plain continuity after the first copied note link.
  The stronger continuous handoff received failing regressions before correction;
  focused re-review found no remaining important issue. No commits, branch changes
  or unrelated `.serena/` edits were made.

Retained published smoke output from this increment:

- `.artifacts/proof/fbd9bd5295ab4d7cadaf89e562d009ca/terminal-output.txt`
- `.artifacts/repair-proof/ff8c1f4e328e4e80b91dcb96f95d2b2c/terminal-output.txt`
- `.artifacts/note-proof/484f8cc5c89b4cffbf04f93749789873/terminal-output.txt`
- `.artifacts/lint-proof/89100d59a6dc408bbd2e4a58cac53886/terminal-output.txt`

These generated artifacts are local evidence, not required source files.

The verified-result increment adds 18 test cases and 12 executable checks. The
tests cover shifted lines, BOM/CRLF revisions, newly released blocks, restored
findings, source order, unsupported repairs, no-op/cancel/stale paths, literal
paths, plain-mode continuity, and a file changed while success is being printed.
The last case verifies both snapshot reporting and refusal of the now-stale link.
New behaviors were exercised as failing regressions before implementation.
Independent read-only review found no remaining important issue.

All 218 tests and all 58 executable checks across five suites passed in a fresh
source-only copy, with no documentation or old build output present. The published
CLI also completed a styled interactive undo and opened its result's note choices
in a real terminal without requesting selection or write approval.

Current evidence:

- `.artifacts/followthrough-proof/5f900a4237c2417f9383889a3aa979ec/terminal-output.txt`
- `.artifacts/source-only-proof/64d330f79e3e42ba892d2f8bb532dab4/output.txt`

Reproduce from the workspace root:

```powershell
dotnet format ERPDERT.slnx --verify-no-changes --no-restore
dotnet build ERPDERT.slnx -c Release
dotnet test ERPDERT.slnx -c Release --no-build --no-restore
dotnet publish src/Erpdert.Cli -c Release -r win-x64 --self-contained true -o .artifacts/win-x64
.\tests\Invoke-PublishedSmoke.ps1
.\tests\Invoke-RepairSmoke.ps1
.\tests\Invoke-NoteRepairSmoke.ps1
.\tests\Invoke-LintHandoffSmoke.ps1
.\tests\Invoke-RepairFollowthroughSmoke.ps1
.\tests\Invoke-SourceOnlySmoke.ps1
```

This evidence is not a usability study, screenshot review, clean-machine install
test, full language conformance test, or verification of package-manager behavior.
The repair diff and inspect/lint source rows separate wrapped content from their gutters.
Neither automated renderer checks nor inspected terminal output prove human delight
or accessibility with screen readers; those need direct evaluation.

## Next complete interaction

Title-rule and author-selected unfinished-note rehearsals are implemented.
Supported lint findings and verified repair results carry their source-bound
preview commands. Development has moved to the actual tool-management use case:
`ERPDERT codex` now performs read-only Windows PATH discovery, groups referenced
packages, and reports competing routes and local dependency evidence. See the
README's "Which Codex?" section for boundaries and the executable smoke check.
Next operational work: establish installation ownership and rehearse an exact
supported update before adding execution. Non-ASCII title width remains separate.

This work does not authorize speculative comma removal, metadata reinterpretation,
indentation, or package operations. The eventual adapters, editor integration,
installation experience, and note application remain separate product milestones.
