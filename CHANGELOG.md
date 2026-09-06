# Changelog

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
