# Doctor actual conflicts

Authority: the user's 10 September request to implement this TODO; the original
`support doctoring`, `possible_conflicts`, `conflicts`, and `fixme` descriptions;
the existing ecosystem and update-transaction designs. This document records a
bounded implementation recommendation, not new UDL grammar. Result adapters,
portable schemas and automatic agent delivery remain deferred.

## Person, situation and interaction

An author has several Codex installations and a command that fails even though
another copy appears intact. They need to see the interference, distinguish it
from suspicion, and choose the correction with their written reason still nearby.

The first complete case is a generated npm PowerShell launcher earlier on PATH
whose declared JavaScript entry point is definitely missing, ahead of another
identified Codex installation with a readable entry point. The conflict is scoped
to external PATH resolution. Parent-shell aliases, functions, cmdlets and execution
policy are not inspected and must not be implied by the result.

`DERT where codex --doctor` diagnoses the routes before asking for a copy.
Multiple copies still require a deliberate selection. The concrete correction
command selects the broken prefix for existing guarded doctor rehearsal. Its
separate `--apply` approval binds the source, route evidence and environment.
Reinstall then verifies the package and the diagnosed external-route condition.
The other installation and PATH are not modification targets.

The same journey remains available through the existing language:

```dert
STERT ; where ;; codex
// support doctoring ; conflicts ;;
// possible_conflicts, // Several copies deserve a closer look.
// conflicts, // Show the route and the missing file before calling it broken.
// fixme // Keep the reason beside the exact correction I choose.
//
commands ; doctor ;;
ƒin.
```

Those entries remain the author's notes. Their names do not assert a diagnosis
or approve an operation. Runtime evidence attaches to the actual doctor request;
the original source, ownership, lines and note wording remain available.

## Alternatives considered

1. Label duplicate versions as conflicts: rejected; coexistence is not damage.
2. Repair a foreign file occupying an npm launcher slot: useful further work,
   but needs an explicit preservation/restore and ownership treatment. Existing
   refusal protects the unidentified file and is retained.
3. Diagnose a known broken external route hiding another copy, then repair its
   identified npm installation: selected. It fits existing manager verification
   and permits a complete before/after proof without deleting an unknown target.

## Evidence and ownership

Confirmation requires a complete supported external PATH scan, a recognized
generated npm PowerShell launcher shape (not merely a target substring), an
identified local package, definite entry-point absence, and a distinct later copy
with inspected entry-point evidence. Unreadable is not missing. Custom wrappers,
redirected/ambiguous inputs and malformed environment entries cannot earn the
confirmed label. A single damaged installation remains damage, not this conflict.

Keep possible/confirmed/unresolved distinctions as structured data behind plain
explanations. Show the earlier route, missing file and later route together in one
targeted ASCII relationship view. Use `possible_conflicts`, `conflicts` and `fixme`
as purposeful connections to the original vocabulary, with English meaning first.
MER keeps the source window and line numbers; its compact margin retains status
and directs detailed evidence to `details`. Motion reports real inspection and
transaction boundaries. Plain and still output preserve the same facts/actions.

A conflict-bearing proposal must retain PATH/PATHEXT, earlier/later route identity,
relevant original bytes, absent candidates that could become earlier matches, and
the existing exact npm/source snapshot. Verify unchanged evidence before manager
execution and mutation. Preserve unrelated-route evidence across uninstall/install
and verify the repaired selected route again before recording resolution. The
approval cannot quietly become a repair of the later copy. Reinstall of another
copy must not claim that this conflict was fixed.

No application is launched to guess its identity or health. Verification proves
the selected package and inspected external-route condition; native Codex runtime,
authentication and the user's active PowerShell session remain outside that claim.
Interruption or failed verification retains the existing honest partial receipt,
never a resolved conflict. No automatic rollback or PATH rewrite is introduced.

## Adverse cases and repeated use

- Two intact copies: possible overlap, no confirmed failure.
- Missing/unreadable/corrupt metadata or a custom wrapper: evidence gap, no
  manufactured certainty. Unknown launcher slots remain protected.
- An earlier executable, relative/quoted PATH entry or changed PATHEXT: do not
  extrapolate the supported first-route conclusion.
- A new earlier launcher, changed later copy, retargeted junction, changed source
  or reordered PATH invalidates the conflict approval before npm runs.
- Cancellation and npm failure: retain last actual phase and unresolved scope.
- Successful repair followed by another diagnosis: no stale conflict remains;
  multiple installations may still deserve attention without being called broken.
- Unfinished/notes-only source: existing planner eligibility applies. No new
  request is inferred from the author's `support doctoring` note.

## Verification plan

Unit/CLI/MER cases cover classification, source handoffs, environment/evidence
changes, chosen correction, interruption and post-verification. A disposable
Windows fixture uses real npm-generated launchers and two test-owned packages.
Fresh PowerShell external command lookup and invocation reproduce the missing-file
failure before DERT repair and successful fixture invocation afterward; this is
test-owned code, never the user's Codex. Verify the later installation unchanged.
Inspect actual CLI transcripts and MER frames at wide/narrow widths, then run the
source-only tests and executable suites and refresh the local Windows package.

References checked during design: Microsoft's
[command precedence](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_command_precedence?view=powershell-7.5),
npm's [install controls](https://docs.npmjs.com/cli/v11/commands/npm-install/), and
the installed npm 11.13.0 `cmd-shim/lib/index.js` generator. These are adapter
evidence; they do not replace the disposable before/after reproduction.

## Implementation and inspected evidence, 10 September

Implemented `DoctorDiagnosis`, the complete npm PowerShell template recognizer,
and immutable `NpmRouteGuard` evidence in the shared transaction. Private proposal
and journal schema 3 binds that evidence to the existing exact manager/source
snapshot. Older approvals require a new review; this is not a portable schema.
The confirmed slice requires supported local generated npm launchers and retained
manager dependencies for both copies. Unknown generator shapes remain incomplete.

CLI, DERT run and MER use the same diagnosis. Notes remain notes; a new packaged
`doctor-conflicts.DERT` shows the existing language. Equivalent path spellings and
same-prefix sibling launchers bind the same correction. Repairing the later prefix
does not earn the external-route verification phase. Manager arguments are shown
as evidence, while the copyable repair retains DERT's approval and source binding.

Observed regressions drove corrections: stale diagnosis after a query, frozen
production environment during DERT run, lost compact interruption, non-directory
entrypoint parents, evidence drift during final route verification, and foreign
shim files appearing after uninstall. The final slot check permits only absence
or the original bytes/identity before install, retaining a truthful partial receipt
when a new occupant prevents continuation. No foreign-shim repair was introduced.
Final scoped review confirmed the last verification and slot fixes.

- **Source-only:** `tests/Invoke-SourceOnlySmoke.ps1` passed **1,252 tests**
  (1,098 core/CLI/MER and 154 native) and **all eleven executable suites**,
  including 18 location/doctor, 29 update, 17 DERT-to-operation, 60 MER and
  24 published Notes checks. Evidence:
  `.artifacts/doctor-conflicts-source-only-final.log` and
  `.artifacts/source-only-proof/f5c3a02d3f664c90b07a11fd4ae2f0e3/`.
  All **278** current source/test inputs match the fresh copy. Documentation and
  original notes were not build/test inputs. Restore used the existing cache;
  it does not establish a fresh dependency vulnerability audit.
- **Real npm and PowerShell:**
  `tests/Invoke-LiveNpmDoctorConflict.ps1 -ToolPrefix C:\nvm4w\nodejs` passed
  **15 checks** against the final published build, using npm **11.13.0**, Node
  **v25.8.1** and PowerShell **7.6.6**. Test-owned `@openai/codex` 2.0.0 packages
  in two disposable prefixes generated real npm shims. Fresh PowerShell chose
  the earlier `.ps1` and failed with `MODULE_NOT_FOUND`; direct later invocation
  worked. DERT diagnosed, rehearsed and applied the chosen earlier repair.
  The same earlier route then invoked the benign fixture successfully, with
  `external-route-condition-verified` and `Succeeded` in the durable journal.
  The entire later prefix and nominated source-tool hashes stayed unchanged;
  no package scripts ran. Final evidence:
  `.artifacts/live-npm-doctor-conflict/b21941398284443a9e1e9754d7b39d83/result.json`.
- **Delivery:** final actual MER engine frames were inspected at **110×28** and
  **58×28**, including both source margins and full evidence rails:
  `.artifacts/mer-visual/573921063d7f454190e0b3816ba01c98/` and
  `.artifacts/mer-visual/75c47ad7c4e841dca6d5253fe2671418/`.
  Initial wide output let long paths crowd out choices. Bounded folder labels now
  retain the relationship, pending action and details hint; exact paths remain in
  details. These are engine renders, not terminal screenshots or a usability study.
- **Local delivery:** `.artifacts/win-x64/DERT.exe` and the bundled Notes host
  were refreshed. No user Codex installation was launched, uninstalled or changed.

Parent-shell aliases/functions/cmdlets, execution policy and native Codex runtime
or authentication remain outside the verified condition. Custom launchers, other
managers and other conflict kinds remain further work. Result-to-notes adapters,
portable schemas and automatic agent delivery stay deferred. The next working
recommendation is a usable first launch and durable installation.
