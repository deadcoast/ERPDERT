# Complete the update transaction

Authority: the user's 9 September request to complete this TODO, the existing
DERT ecosystem design, and the original update intent. This is an implementation
design, not a new language decision. Rehearsal/doctor result-to-notes adapters,
portable schemas, and automatic agent delivery remain deferred.

## Person and experience

An author has found the intended Codex copy and rehearsed its update. They need
the next action to retain that evidence, explain what will change, and return a
trustworthy outcome. A raw npm command currently drops DERT's reviewed snapshot
exactly when packages can change.

`DERT update codex` remains discovery. `DERT update codex updateme` rehearses and
saves a local proposal. Its receipt offers one exact command:

```powershell
DERT update codex updateme --path '<reviewed launcher>' --apply <proposal token>
```

A proposal made from a note also includes `--from-source '<document>'`. The
existing construct remains unchanged:

```dert
STERT ; update ;; codex
// Keep the copy I selected and the reason I wrote here.
commands ; updateme ;;
ƒin.
```

Running that note, or choosing `check` in MER, creates a rehearsal and proposal;
neither action approves package mutation. Approval is a deliberate separate CLI
invocation. Plain, still, and animated output retain the selected route, version
transition, unapplied/applied distinction, and exact next action. MER retains its
source relationship and line numbers; transaction phases use real process events.

## Alternatives and recommendation

1. Keep the raw npm handoff: small, but loses source and installation binding.
2. Rerun rehearsal when applying: convenient, but silently approves newer evidence.
3. Persist an operation-specific proposal and recheck it before applying: chosen.
   It costs local state and explicit stale-proposal handling; it preserves choice.

Update and doctor share transaction evidence, locking, journal, cancellation, and
verification. Update executes one pinned install without a preceding uninstall.
Doctor retains its explicit uninstall/reinstall sequence. Approval tokens bind the
operation kind so neither can authorize the other. Shared code belongs beneath
both application actions, with compatibility facades where existing callers need
them; two copies of the transaction engine are not acceptable.

## Meaning and actual effects

The reviewed snapshot includes selected/canonical routes, launcher slots (including
absence), package metadata and entry point, Node/npm evidence, installed and target
versions, exact structured requests, and source identity/hash when supplied.
Recheck before manager queries and mutation, then verify the pinned installed
package, launcher/entry point, manager identity and inventory afterwards. An
installation lock must coordinate update and doctor for the same canonical prefix,
even when their receipt folders differ. A durable journal precedes mutation and
records command completion, verification, interruption and partial failure.

Install uses an exact version, explicit prefix, optional dependencies, strict
engines, disabled package scripts, explicit non-dry-run execution, and no force.
Rehearsal remains a dry run with scripts disabled. A zero exit code alone cannot
establish success. No rollback, Codex launch, authentication or native-runtime
health is implied. A stopped npm install can leave the selected copy partial;
recovery is an explicit reviewed request, not an automatic second mutation.

Saved records are private local operational state. They are not portable schemas,
cryptographic authorization against a hostile local account, or results-to-notes
adapters. Configuration/registry resolution remains npm's responsibility; exact
top-level version is pinned, not a frozen dependency tree or offline artifact set.

## Adversarial and repeated use

- Unfinished/blocked source: operation planner blocks rehearsal as before.
- Ordinary prose: notes stay notes; no new grammar or command inference.
- Wrong action, route, source or token: refuse before npm execution.
- Changed source, package, manager or redirected prefix: invalidate old approval.
- Already current/ahead: no install proposal and no downgrade guess.
- Repeated approval after success: old package snapshot is stale; re-inspect.
- Concurrent doctor/update: only one transaction enters the installation.
- Cancellation before mutation: no package-changing process starts. Cancellation
  after it begins: truthful partial status and retained journal.
- Missing/failed receipt: do not begin mutation without durable evidence; do not
  claim verified completion when the result cannot be recorded.

## Evidence required

Focused tests cover update-only execution, operation-separated approvals, source
binding, stale inputs, failures/interruption, shared lock and post-verification.
Exercise CLI, DERT run and MER with test-owned fixtures. Inspect settled output and
actual npm behavior in a disposable local prefix. Existing user installations must
not be changed. Run the source-only suite, refresh the local Windows package and
record exact evidence and remaining limits here after implementation.

Adapter references checked during this step: npm's official
[install documentation](https://docs.npmjs.com/cli/v11/commands/npm-install/) and
[configuration reference](https://docs.npmjs.com/cli/v11/using-npm/config/).
These describe explicit versions, global prefix, scripts and dry-run controls;
live disposable-fixture verification is required in addition to documentation.

## Implemented and inspected, 9 September 2026

`NpmTransaction` now owns evidence capture, deterministic request validation,
operation-specific proposals, persistence, locking, journal, execution and final
verification. `DoctorService` adapts its existing callers; `UpdateTransaction`
exposes the update operation. Schema 2 binds the action into the local approval
identity. Old doctor proposals require a fresh review; no old token is silently
converted. One persistent `.dert-npm-transaction.lock` in the canonical prefix
coordinates both operations independently of their state/receipt folders.

CLI, DERT run and MER use the same update service. Every offered copy-selection
command from a note retains `--from-source`; saved approvals retain that source's
identity and bytes. Cancellation is linked across discovery and rehearsal. The
MER margin now points to the saved approval in `details`, including its compact
reading. The source frame, line numbers and context remain intact.

Independent review and adversarial tests exposed stale discovery metadata,
source changes during an already-current response, and four changes to package,
launcher, entry point or an absent launcher slot during final inventory. These
were reproduced before the fixes. Capture now reprobes discovery before the first
query, all completed inspection outcomes recheck captured inputs, and fresh
installed evidence is verified again after the final inventory query. An old
success cannot remain over those changes. The proposal list/request collections
are copied and frozen; checksums do not permit arbitrary process requests.

Evidence retained in this workspace:

- Fresh source-only tests: **1,191 passed** (1,037 core/CLI/MER, 154 native).
  **All eleven executable suites passed**, including 29 update, 8 location/doctor,
  17 DERT-to-operation, 60 MER and 24 published Notes checks.
  The current **265 source/test inputs** match the verified project under
  `.artifacts/source-only-proof/a75a25ca62fc4b428d743c8785e4560e/project`.
- Update executable checks: **29 passed**, including copied PowerShell approval,
  source/stale refusal, update-only mutation, failure and interruption receipts.
  Evidence: `.artifacts/update-proof/dfc406988e39484fa4fb8467cf24d392/output.txt`.
- Real DERT update through **Node v25.8.1 / npm 11.13.0**: test-owned package
  **1.0.0 → 2.0.0**, target tarball retrieved from the loopback registry, one
  install, no uninstall, scripts or Codex launch; final independent npm inventory
  and DERT journal agree. Reused approval returned 1 without new npm logs,
  registry requests or transaction receipts. Nine invoked steps are recorded at
  `.artifacts/live-npm-update/9ccafbaaa10b4e6baea0a070be5b3ca9/result.json`.
- Legacy real-npm install/uninstall/install compatibility remains verified in
  `.artifacts/npm-doctor-proof/4d2c09c602fb44e0a60ee6c81921b2dd/result.json`.
  This older harness tests raw npm, not the DERT doctor service; that service is
  covered by its unit and published process-fixture checks.
- Inspected actual MER adapter frames at **110 × 32** and **58 × 26**; retained
  final renders are `.artifacts/mer-visual/62e10506b2b145209bc091592fe0b772` and
  `.artifacts/mer-visual/a7977ddfee2e446c8592190f0216f6e0`. These are rendered
  engine frames, not OS-terminal screenshots or a human usability study. Published
  CLI success/failure transcripts were also inspected. The Windows package was
  refreshed at `.artifacts/win-x64`.

The source documents are unchanged. Dependency restore used the existing local
cache, not a fresh online vulnerability audit. No user Codex installation was
updated. Native runtime health, clean-account installation, additional managers
and a reproduced conflict diagnosis remain separate work. Rehearsal/doctor
result-to-notes adapters, portable schemas and automatic agent delivery remain
deferred exactly as requested.
