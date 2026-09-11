# MIVIM minimal implementation plan

User authorization: the explicit correction requests implementation of a new
default compact window and a separate Help role. Work continues in the shared
checkout; preserve the user's staged installation documentation and commits.
No additional approval gate is needed for these reversible changes.

Spec: [MIVIM minimal](../specs/2026-09-10-mivim-minimal-design.md).

## Tasks and interfaces

- [x] **1. Shared settings and CLI/desktop execution.** Add a persistent
  `Dert.Cli.Authoring.DesktopSettings` record and `DesktopSettingsStore` with
  injected path for tests; expose equivalent `DERT settings` read/set commands.
  Add a safe desktop command bridge which runs the actual CLI with literal
  argument vectors, captured output/exit code, no shell, honest busy/cancel state
  and complete approved commands. Tests cover persistence, malformed settings,
  quoting, refusal, command output and effect boundaries. This independent task
  is delegated; agree public interfaces before GUI wiring.
- [x] **2. Compact composition and palette.** Split NotesWindow into partial
  files, keeping its shared authoring/session and expanded Workbench composition.
  Add compact composition, rounded native chrome, fine highlight details,
  unobtrusive status and working-panel overlays. Replace seafoam in native and
  terminal defaults. Use actual rendering to refine spacing/contrast.
- [x] **3. Precise fading menu.** Add a deterministic fade-state model plus a
  native menu host. Tests first cover click deadline, strict root/child hover,
  parent fading, typing, focus, fade setting and reduced motion. Connect complete
  File/Edit/Note/Operations/Settings/Help actions to existing implementations.
- [x] **4. Help and delivery routes.** Add an independent Help launch, syntax
  examples and troubleshooting destination. Retain the expanded workbench here.
  Default direct launch, CLI write and installer entries open compact Notes.
  Keep application lifetimes/installation protections for both roles.
- [x] **5. Integrated verification and reconciliation.** Test CLI/settings and
  native behavior, inspect screenshots and actual input, obtain independent
  review, run full solution/published checks, refresh package/setup, update
  README/TODO/status/changelog with the user's correction and evidence limits.

## Execution record

- Preflight: installation work is committed; its later documentation edits are
  staged by the user. This task will preserve those staged changes.
- Recommendation: start at 640 × 420, resizable, because no exact pixel
  measurement was supplied. Shared editor semantics stay beneath both surfaces.
- Completed shared settings/CLI capture, precise menu state, compact composition,
  native context/result reviews, independent Help routes and installer/portable
  entries. The current source has 1,168 core/CLI/MER and 207 native passing tests.
- Independent review found source-handoff omissions, an incomplete edit lock,
  invisible keyboard proposals, lost initial open errors, and menu focus defects.
  Regression cases reproduced these and passed after correction. Native command
  capture also led to removing a stale busy message and retaining a running-status
  footer when the person returns to writing.
- Final verification: all 1,375 tests passed after the last UI status change.
  Fresh source-only builds passed all eleven executable suites; the final
  published Notes package passed 24 checks. Installation passed 91 delivery
  assertions and six native interaction checks, including simultaneous compact
  Notes and expanded Help at their intended dimensions. Exact evidence and
  the sequence of small final status corrections are in the design record.
- README, AGENTS, TODO, implementation status and changelog now record the user's
  correction. Original language/product source artifacts remain untouched.
- Clean standard-account first launch, cross-monitor/font/IME coverage and the
  person's visual acceptance remain unproven. These retain their backlog scope;
  they are not grounds for returning the default to the teaching workbench.
