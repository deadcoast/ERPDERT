# Update transaction implementation plan

Spec: `docs/superpowers/specs/2026-09-09-update-transaction-design.md`.

1. Extract the shared npm transaction underneath doctor and update. Add distinct
   update proposals, update-only execution, shared locks and focused adversarial
   tests. Preserve the doctor facade and existing behavior.
2. Integrate CLI approval and source-bound proposals through DERT run and MER.
   Replace the raw mutation handoff with the saved proposal and exact DERT command.
   Keep real-event transaction presentation, source relationships and plain output.
3. Extend executable and real-npm disposable-fixture checks. Review the complete
   change independently, fix findings, run source-only verification, inspect the
   delivery surface, refresh the Windows package, and reconcile TODO/documentation.

## Execution record

- Design reconciled with existing doctor and update implementation. No new UDL
  spelling or automatic package mutation is introduced.
- Ruling: continue in the existing user-authorized development workspace and
  preserve all prior edits. A clean checkout would discard the required integrated
  baseline; no commit or remote publication is part of this task.
- Shared transaction extraction implemented; focused backend verification and
  adversarial review are in progress. Doctor remains a compatibility facade.
- CLI, DERT run and MER now create and retain saved update proposals. Approval
  dispatches through the shared engine. The source window and line numbers remain.
- Review found and fixed source loss in multi-copy selection commands, and linked
  cancellation across discovery/rehearsal. Regressions added for both paths.
- Disposable real-npm harness prepared and run against the previous executable:
  expected failure at the missing approval token, after successful loopback
  rehearsal. Final verification below supersedes this initial red result.
- Task 1 complete: shared backend plus 28 transaction tests; six additional stale
  evidence failures reproduced and fixed. The doctor facade retains its existing
  behavior and uses the shared evidence implementation.
- Task 2 complete: CLI/run/MER integration plus 12 new CLI cases; source choice
  and cancellation findings resolved. An older interactive test now isolates its
  state directory rather than attempting to write into user application data.
- Task 3 complete: independent review has no remaining material findings;
  source-only **1,191 tests and all eleven executable suites passed**, including
  29 update checks. All 265 current source/test inputs match the verified copy.
  Real npm 11.13.0 on Node v25.8.1 verified a disposable 1.0.0 → 2.0.0 update,
  one install without uninstall/scripts/Codex launch, and stale approval refusal.
  Actual MER adapter frames at 110/58 columns and published result transcripts
  were inspected. Windows package refreshed; full paths and limits are in the spec.
- TODO reconciled: next recommendation is one reproducible doctor conflict.
  The explicitly deferred result adapters, portable schemas and automatic agent
  delivery remain further work. No user tool installation changed.
