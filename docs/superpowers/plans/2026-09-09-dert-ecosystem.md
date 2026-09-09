# DERT ecosystem implementation plan

> Execute continuously using subagent-driven-development for the independent
> location and doctor modules, with parent integration and final review.

**Goal:** Deliver normalized DERT commands and UDL bindings, a connected application
locator, and a verified manager-specific doctor flow using shared evidence.

**Architecture:** Application catalog → typed request → shared discovery → manager
adapter → connected MER presentation. Package writes require exact reviewed plans.

**Tech stack:** Existing .NET 10, C#, WPF/AvalonEdit, MER and PowerShell.

**Spec:** `docs/superpowers/specs/2026-09-09-dert-ecosystem-design.md`.

## Constraints and rulings

- Preserve previous Unicode work and original source artifacts. Work in the
  existing authorized workspace; an isolated checkout would omit those edits.
- No user application mutation during development; disposable prefixes only.
- No generic shell dispatch, global search of credential contents, or deleting
  adjacent data as doctoring. Keep byte/identity guards and visible failure states.
- Persisted `.erpdert` recovery format remains readable and writable unchanged.
- This plan's task status below is the continuation ledger. No commits requested.

## 1. Normalize identity and dispatch

Files: solution, active projects/namespaces, launchers, CliApplication,
Operations/DertOperationPlan, current command renderers, tests/EcosystemCommandTests.

- [x] Add failing tests for current help, `update codex`, canonical UDL and legacy
  UDL input. Run the focused filter before changing dispatch.
- [x] Rename active namespaces/projects and executable to Dert/DERT. Update
  references and packaged command generation. Preserve existing recovery storage.
- [x] Add shared ApplicationCatalog and typed ApplicationRequest with Update,
  Where, UpdateRehearsal and Doctor actions. Bind CLI and UDL to that same model.
- [x] Verify focused routes and existing operations; retained input aliases must
  print normalized output, and unsupported chains must run nothing.

## 2. Shared application location evidence

Files: Dert.Cli/Applications catalog/request, Dert.Cli/Discovery generic PATH
adapter and location models/scanner, tests/ApplicationLocationTests.

- [x] Test grouping, generic names, registry evidence, adjacent paths, measured
  size/partial size, cancellation and source preservation using owned fixtures.
- [x] Reuse the Codex package probe behind the catalog; add Windows location
  evidence without guessing ownership from names. Provide injectable environment
  inputs so tests never inspect the user's installed applications.
- [x] Expose `ApplicationLocator.Scan(ApplicationDefinition, DiscoveryEnvironment,
  CancellationToken)` returning command discovery, locations and issues.
- [x] Parent reviews spec/quality and verifies focused cases.

## 3. Doctor through the manager adapter

Files: Dert.Cli/Doctoring, shared Updates selection/verification, tests/DoctorTests.

- [x] Inspect npm's current documented/runtime uninstall/install behavior.
- [x] Test proposal creation and source/installation identity binding, unsupported
  and ambiguous copies, current versions, rejected approvals, failed phases,
  receipt persistence and post-install verification with disposable inputs.
- [x] Reuse NpmUpdateRehearsal verification; expose DoctorService.Inspect and
  DoctorService.Apply using typed requests and ICommandRunner. Exact selected
  npm prefix only; no scripts or credential/data deletion.
- [x] Inspect a real disposable npm transaction when locally available; record
  unproven actual-package behavior honestly. Parent reviews spec/quality.

## 4. Connected delivery

Files: application command dispatcher, DertRunCommand, application location
renderer, MER source connections, help/examples, executable smoke suites.

- [x] Integrate `where`, `--doctor`/`-dr`, exact-path choice and reviewed apply.
- [x] Keep DERT source selection/notes/owner visible around location and doctor
  evidence. Expose exact paths and complete detail in compact and wide views.
- [x] Verify source changes, rejected choices, cancellation, plain/still modes
  and repeated use. Inspect actual rendered frames and published command output.
- [x] Rename smoke infrastructure and run all source-only tests/executable suites.
- [x] Refresh Windows package, update README/TODO/status/evidence and request final
  whole-change review. Retain any uncovered scope as specific evidence gaps.

## Final verification

Completed 2026-09-09. The linked design records implementation decisions, review
findings, 806 passing tests, all eleven source-only executable suites, the
refreshed Windows package, inspected wide/narrow frames and remaining evidence
limits. Original source artifacts and prior Unicode work were preserved.
