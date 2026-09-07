# Changelog

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
