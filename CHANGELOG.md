# Changelog

## 2026-09-07 — DERT drives the operation

- Added `ERPDERT run <file>`: bind `STERT ; ERPDERT ;; codex` to the existing
  discovery engine, display the source and contextual notes, and attribute each
  result to its requesting chain. The one-line form and optional terminator work.
- Added `--preview` with a complete, literal-safe command to run the request.
  Preview does not search. Formatting warnings do not stop discovery;
  unavailable operations and incomplete structure are source-cited before dispatch.
- Ship `examples/my-tools.DERT` beside the executable as a runnable example.
  Neither run nor preview changes the file, runs discovered programs, or updates packages.
- Added end-to-end regressions and a published-executable suite covering source
  binding, context, multiple chains, copied commands, and unchanged source bytes.
- Verified all 295 tests and 84 executable checks in a fresh build, plus the
  rebuilt app's styled terminal output against local Codex installation evidence.

## 2026-09-07 — operational Codex discovery

- Added `ERPDERT codex [--plain]`: read-only Windows PATH discovery with a
  first-route answer, grouped package launchers, version and dependency evidence,
  competing routes, and cited English findings for missing or unreadable files.
- Preserve PowerShell lookup order, including quoted PATH behavior; recognize
  junction aliases without inventing another installation. Do not infer the
  installer from package layout or a development `packageManager` field.
- No discovered programs or package operations execute. Existing DERT commands
  and the original reference documents remain unchanged by discovery.
- Added 45 unit/integration cases and 14 published-executable checks, including
  comparisons with actual PowerShell lookup and unchanged-file/execution traps.
- Verified all 263 tests and 72 executable checks in a fresh source-only build.

## 2026-09-07 — verified repairs carry the next action

- Added source-cited rehearsal links to remaining findings after title repair,
  note closure, and undo. The completed edit stays distinct from unrelated issues.
- Derive the result and revision from verified readback bytes, with current line
  numbers and rechecked repair capabilities. Later file changes invalidate links.
- Keep every further repair a separate preview and approval. Unsupported findings
  remain explained, and a clean result only claims supported structural checks.
- Preserve plain mode through copied approvals, undo, and subsequent result actions.
- Added 18 regression cases and 12 actual-executable checks. All 218 tests and
  58 executable checks passed in a fresh source-only copy without documentation.

## 2026-09-07 — remove documentation dependencies

- Removed reference-document copying, Markdown example extraction, and reference
  document hash checks from the build, tests, and executable verification scripts.
- Kept language coverage in standalone DERT test inputs. No parser or CLI behavior
  changed, and the user's original notes were not edited.
- Added a source-only check that builds, runs all 200 tests, publishes, and runs
  all 46 executable checks without the documentation or old build outputs present.

## 2026-09-06 — lint findings carry their own rehearsal

- Added source-adjacent, copyable PowerShell handoffs for supported title-rule
  and unfinished-note findings. Lint display follows source order, not parser
  check order; diagnostic meaning, severity and exit codes are unchanged.
- Check actual title-repair readiness before offering an action. Unicode title
  warnings cannot incorrectly advertise unsupported width repair. Inline-note
  and list-completion findings receive explicit capability explanations.
- Added `repair --from-lint <source-revision>`: compare the exact linted bytes
  before interpreting a title/block, then open a preview without selection or
  write prompts, even in an interactive terminal. It cannot combine with `--apply`.
- Carry absolute file paths, the launched executable, literal-safe quoting and
  plain-mode preference into copied commands. Note choices retain the source
  revision through selected preview; approval is a separate action-bound token.
- Added real-file/CLI regressions and a published-executable handoff suite for
  changed directories, stale links, unusual filenames, no-write previews and
  separately approved repair/undo. Original syntax artifacts remain unchanged.

No new repair kind, parser rule, automatic mutation, dependency or installer.
All work remains uncommitted in the existing checkout.

## 2026-09-06 — author-selected note-boundary rehearsal

- Added `repair --at <line>`: show an unfinished reasoning block and explicit
  source-line/EOF choices. Leaving is the default; no ending is guessed.
- Added `--close-before <line|eof>` for an offered boundary. Selection opens a
  rehearsal, not permission to write. Approval remains a separate decision.
- Preview the exact standalone `//` insertion, every retained entry, and each
  released line's actual reparsed role and owner. Show remaining findings.
- Preserve words, annotations, commas, flush-left source, BOM and newline/EOF
  style. No parser recognition or recovery grammar changes were introduced.
- Share guarded apply/undo with title repair. Keep legacy title receipts and
  replay typed note selections from saved originals. Reject stale undo choices
  even when a later repair-and-undo returns the source to identical bytes.
- Separate source gutters from wrapped content throughout inspect/lint/repair.
- Added core, real-file, CLI and published-executable checks, including copied
  PowerShell commands and real styled/plain interactive choice/approval/undo.

This supports unfinished multiline reasoning blocks only. Inline-note closure,
active-list completion, operational execution and installation remain separate.
Recovery remains latest-repair-only, unencrypted locally and not power-loss atomic.

## 2026-09-06 — title-rule repair rehearsal

- Added `repair` and `undo`: source-cited explanation, exact colored/sign-marked
  diff, default-leave confirmation, verified application, and byte-exact reversal.
- Added a pure source-range planner. It changes only the title rule, preserving
  prose, original reasoning, mixed newlines, UTF-8 BOM, and EOF style.
- Added retained local originals, checked recovery receipts, exclusive Windows
  writes, stale-file/approval rejection, readback verification, and rollback.
- Bound copied approvals to the file identity, source, proposed edit, operation,
  and undo selection. Recovery ordering does not depend on the wall clock.
- Made generated PowerShell commands executable without a PATH installation,
  without inserted wrapping, and with literal-safe unusual filenames.
- Added repair tests and a published-executable smoke suite using generated
  corpus copies. Authoritative source artifacts remain unchanged.

Printable ASCII title width and regular local Windows files are the current
repair boundary. Recovery is not power-loss atomic; saved originals are local,
unencrypted document copies. No broader formatting or command execution is added.

## 2026-09-06 — first working language build

- Added a source-preserving .NET language core with context-aware `//` parsing,
  original reasoning-entry ownership, expanded active values, and source ranges.
- Added semantic highlighting roles and folded reasoning presentation; missing
  closers remain visible even while the note is folded.
- Added `ERPDERT -help`, `inspect`, and `lint`, including line-focused ownership,
  English diagnostics, plain output, literal terminal text, and useful error exits.
- Added original-source, recovery, preservation, and CLI tests, plus a repeatable
  smoke check against the published Windows executable.
- Published and exercised a self-contained Windows x64 folder locally.
- Preserved `original.md`, `WHO-WE-ARE.md`, and `RERDMERR.DERT` without rewriting
  their examples to suit the implementation.

This build inspects only: no source repair, command execution, package adapters,
installer, editor extension, or canonical v1.0 grammar is claimed.
See [implementation status](docs/IMPLEMENTATION-STATUS.md) for coverage and limits.
