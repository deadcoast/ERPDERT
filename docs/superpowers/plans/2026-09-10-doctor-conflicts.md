# Doctor conflicts implementation plan

Use the subagent development workflow for the independent backend, delivery
integration and disposable process verification tasks, followed by scoped review.

Goal: diagnose one reproduced Windows external-route conflict and carry its chosen
repair through the existing guarded npm transaction with verified resolution.
Spec: `docs/superpowers/specs/2026-09-10-doctor-conflicts-design.md`.
Stack: existing .NET CLI/MER, PowerShell and test-owned npm fixtures.

- [x] Add typed conflict diagnosis and constrained generated-shim recognition.
  Bind route/environment evidence into the shared transaction; verify before
  queries, across mutation and after repair. Add focused adversarial tests.
- [x] Integrate diagnosis, exact choices and resolved/unresolved receipts through
  CLI, DERT run and MER. Preserve source binding and authored context. Test all
  three entry points and compact/plain presentation.
- [x] Reproduce failure and repair with real npm/PowerShell in disposable prefixes;
  inspect delivered output, review independently, run source-only verification,
  refresh Windows package and reconcile TODO/status with exact evidence.

## Execution record

- Ruling: extend the current user-authorized workspace and preserve all previous
  edits. No commit, remote publication or user application mutation is required.
- Rejected duplicate-count diagnosis and foreign-shim removal as shortcuts. The
  selected case has a definite missing file and a narrow observed route contract.
- Implemented typed diagnosis, persisted route guards and mandatory phase checks;
  CLI, source-bound run and MER share the evidence and separate approval path.
- Review corrected equivalent-path and same-prefix sibling selection, final
  diagnosis freshness, production UDL environment refresh and compact interruption.
  Manager arguments remain inspection evidence; the primary action keeps DERT's
  saved approval. The version transition is visible because repair can upgrade.
- Final review also reproduced and fixed a non-directory entrypoint parent,
  source/package changes during the final route scan and new foreign launcher
  occupants after uninstall. The final scoped review found no remaining blocker.
- Final source-only validation: 1,252 tests and all eleven executable suites
  passed; 278 source/test inputs match. The final published real npm/PowerShell
  proof passed 15 checks, including before/after invocation of the same test-owned
  route and an unchanged later prefix. Wide/narrow engine frames were inspected;
  the wide margin was revised so long paths cannot displace the next action.
- Local Windows package refreshed. Exact artifacts and verification commands are
  recorded in the linked design. Next recommendation: first launch and durable
  installation; other conflict kinds remain scoped future work.
- Result adapters, portable schemas and automatic agent delivery remain deferred;
  no new UDL spelling or indentation rule is introduced.
