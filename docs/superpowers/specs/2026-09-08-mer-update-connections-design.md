# Follow an update request through its evidence

Working recommendation, 2026-09-08. Authority: continued user direction for
substantial development and polish; original `STERT ; ERPDERT ;; codex` with
`commands ; updateme ;;`; the source-window correction and restraint requirement.
Review controls here are application choices, not DERT syntax decisions.

1. A person has written an update request and wants to identify the affected
   installation, see its actual manager evidence and understand the proposed
   change while retaining the source and surrounding reasoning.
2. At that request, `rehearse` discovers local copies. `copy N` selects a captured
   installation. `check` explicitly starts npm inventory, registry and dry-run
   queries after the view explains their network/cache effects. The result stays
   attached to the request. `details` retains complete paths, checks, warnings,
   proposed dependency changes and the existing separately labeled manual command.
3. This deepens the original Codex/updateme chain without executing prose, note
   entries or unsupported chains. Both chain and statement selection lead to the
   actual statement's source line. Ordinary notes remain complete on their own.
4. A copied command breaks source continuity; embedding the full audit buries
   it. Choose a source-attached route from installed version through confirmed
   inventory and registry to the proposed version. Real activity drives the
   transition. All underlying evidence remains available in details.
5. Reuse the existing binder, discovery, UpdateMotion event adapter, command
   runner and npm rehearsal. No npm query before explicit check. Multiple copies
   and incomplete searches require an explicit selection. Recheck source and
   selected route before queries; reject source changes during queries. Repaired
   source invalidates captured rehearsal choices/results. Do not mutate a settled
   source-analysis journal to pretend later external checks were part of it.
6. Unknown routes, stale source/installation, unsupported versions, registry
   failures, warning output and cancellation retain their observed meaning.
   Current/ahead versions do not get fabricated dry-run success. Repeated check
   is a new query with its own timestamp; merely revisiting a copy reuses evidence.
   Failed/interrupted work must not be covered by an earlier ready result. Apply
   remains a source-repair command; it never installs a rehearsed update. Replay
   can retain local discovery but cannot start npm queries.
7. Complete source-bound update rehearsal and copy comparison in MER. Integrated
   package-changing approval/execution/post-install verification remains separate
   work; the existing manual command is clearly labeled and does not claim those
   guarantees. npm may access its configured registry and write cache/logs.
8. Verify all state transitions with actual discovery/guard logic and a process
   fixture; exercise the published executable. Use a disposable prefix with real
   Node/npm for opt-in registry/dry-run verification, and inspect wide/narrow
   compositor frames. Documentation remains outside executable test inputs.

The existing adapter uses `--dry-run` and `--ignore-scripts`; current
[npm install documentation](https://docs.npmjs.com/cli/install/) describes those
controls. The runtime experiment must establish the behavior of the installed
npm version; documentation alone does not establish integration success.

## Observed implementation

MER now uses the existing operation binder, local discovery, npm rehearsal and
process runner. A separate update journal feeds the source-attached annotation;
the source analysis retains its own time and meaning. Full details retain the
original local inspection and each subsequent rehearsal as distinct records.

Focused regressions cover explicit copy selection, current/ahead releases,
inventory and engine failures, source and installation changes, repeated checks,
repair invalidation, fresh discovery warnings and cancellation. Review exposed
three retention problems: a stale repair could leave an accepted update attached,
cancelling discovery could leave an earlier ready result visible, and fresh
discovery issues could disappear behind the original scan. Each now has a
regression and a corrected settled result.

The published Windows executable was exercised in a PTY through source selection,
local rehearsal, explicit check and return to the prompt. The child-process
fixture validated npm arguments and refused actual installation. Wide 132x36 and
narrow 80x24 compositor frames were inspected from that same adapter and fixture:
the request remains selected, the wide view connects its evidence beside it, and
the narrow view preserves a compact result below the source. These are rendered
engine frames, not OS screenshots; fixture versions are controlled examples.

Real Node/npm was also exercised through `mer --review` against a disposable
prefix, with empty user/global npm configuration and a dedicated cache. The
public registry resolved 0.153.4 for this run; inventory and the dependency dry
run succeeded. Checksums confirmed all 1,941 installation files were unchanged.
No manual update command was executed. A preceding registry-access failure
settled as stopped without an accepted proposal. Evidence:

- `.artifacts/mer-live-npm-verified.log`
- `.artifacts/live-npm-proof/28077a424b5b4b5483af728ac7a6fb77`
- `.artifacts/mer-visual/1fd5590ec7034b259260d61df71d2b2c/update-02-check.png`
- `.artifacts/mer-visual/88843822ade44871bf2917e409f52ebd/update-02-check.png`

Final verification passed 563 tests and all nine executable suites from a fresh
copy of code, build settings and test inputs, including 20 update checks and 46
MER checks. The workspace Windows publish is refreshed. The source-only record
is `.artifacts/mer-update-source-only-final.log`, with full retained output under
`.artifacts/source-only-proof/0eb6a32ae2564771ab0c1d1daf4cadbe`.

This proves the connected rehearsal and its observed npm behavior. Integrated
package-changing approval, execution, post-install verification and any recovery
contract remain unimplemented. The inspection does not certify the user's
subjective standard for the full product.
