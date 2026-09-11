# Bring the result back to the words

Authority: the user's explicit results-return-to-useful-notes TODO, the original
annotated reasoning nests, and the operation-receipt journey in
`docs/EXPERIENCE-DESIGN.md`. Portable schemas and automatic agent delivery remain
deferred. Decisions below are working implementation policy, not new grammar.

## Person, action and desired feeling

An author has used a source request to find an application. The output matters
tomorrow as well as now. They should be able to keep the observed result with
their explanations, understand what was checked, and compare the old source with
today's words without confusing history with another successful check.

The complete first slice is source-bound Codex discovery. `DERT run file.DERT
--receipt new.json` deliberately retains one observed discovery in a new local
record. MER `keep new.json` retains its selected real probe without repeating it.
The offered `DERT result record.json --into file.DERT` shows the result and source
comparison; `--write` opens the adjacent Notes host at an insertion review.
Notes also offers **Keep result…** to choose a local record. No package mutation
or external agent is needed to exercise this entire journey.

## Worked choice

Pasting terminal output loses ownership and may contain executable-looking text.
Automatically rewriting the source erases author choice. The earlier identity
note proposal adds source bookkeeping and cross-copy decisions before they are
needed for this slice. Use an explicit local observation with captured chain text
and source revision, then a previewed, ordinary annotated reasoning note. Exact
chain matching survives a line move; duplicate matches remain ambiguous. An
explicit current-chain selection can support comparison after edits, while the
saved observation continues to identify its original source. Persistent identity
notes and automatic rebinding remain future work, not silently invented rules.

Example shape using the existing grammar (illustrative data):

```dert
// observed result ; observation ;;
// action, // DERT update codex / local discovery
// target, // codex
// observed, // 2026-09-09T12:00:00-07:00
// outcome, // Attention / no matching command found
// effects, // Read local PATH and package metadata. No programs started.
// source, // tools.DERT / captured chain at line 4
// revision, // captured source SHA-256
// record, // local observation identifier
// evidence, // No Codex route appeared in the inspected PATH directories.
//
```

Field names are application readings of an existing annotated nest. No structured
child-header grammar, indentation or receipt execution is introduced. Every line
of external evidence is prefixed into an annotation; punctuation cannot escape
into commands. Original chain text, exact timestamps, findings, evidence and
effects remain available in the local record and full review.

The fixed `observation` target keeps the generated header independent of imported
punctuation. The observed application's exact target is an annotation. This is a
working application convention, not a new reserved language construct.

## Ownership, edits and comparisons

The observation captures action, target, time, outcome, summary, effects, exact
evidence and origin (file, full-source revision, chain line and exact chain text).
Capture does not assert authenticity of later imported records. A record is
historical evidence reported by its producer, never a command or present-state
check. Read-only discovery retains uncertain ownership and missing evidence.

Insertion appends a standalone folded reasoning note after the existing source.
The exact addition is visible before Apply. Reparse it to prove the added note
is outside existing chains/nests and does not absorb or reclassify authored text.
An unfinished final owner that would swallow the addition refuses insertion with
guidance. Duplicate record insertion is refused. No automatic closer is added.

Native Apply checks the captured buffer and file identity, changes one undo group,
and selects the inserted note. Save/reopen uses the existing guarded file service.
Back/Escape changes no source or selection. Pending source proposals keep their
own choices. Review shows observed result, source comparison and exact insertion
as distinct readings, with actions fixed above scrolling content at 1180/780.

Comparison distinguishes exact source, moved exact chain, changed chain, missing
origin, ambiguous duplicates and a different destination document. Any selected
changed chain is a comparison candidate, not a rewritten historical origin.
Two saved observations can also be compared explicitly: time, action, outcomes,
effects and evidence changes remain separate from their source comparison.

## Delivery and retained scope

Test source movement, duplicates, changed requests, unfinished final structures,
ordinary prose, quoted paths, Unicode, mixed newlines/BOM, multiline hostile-looking
evidence, duplicate insertion, failed/new-only record writes, stale previews,
native Undo and guarded Save/reopen. Inspect actual native and MER delivery, run
source-only suites and refresh the local Windows package. Tests own their fixtures;
documentation and original user notes are not build/test inputs.

This slice records read-only discovery. Retaining update rehearsals, importing the
doctor transaction journal (including failed verification), stable authored origin
keys, cross-document identity and edited-receipt reconciliation remain explicit
follow-on work. The shared observation model must preserve outcome distinctions
needed for those adapters. No portable schema, authenticated receipt, automatic
agent delivery or assertion of current installation health is claimed.

## Delivery ledger

- [x] Shared observation, inert note insertion and source/result comparison.
- [x] New-only local record persistence and source-bound discovery producers.
- [x] CLI/MER review and native insertion/Undo/Save journey.
- [x] Independent review, actual renders, source-only verification and publish.
- [x] Evidence, scope and next development direction recorded.

## Review decisions and revisions

The record snapshots evidence and validates outcome, origin, exact Unicode and
one captured chain. Local persistence uses a versioned internal envelope, strict
UTF-8, bounded reads and new-file-only creation through one exclusive handle.
Bytes are verified before reporting retention. Existing paths, source paths and
Windows alternate streams are refused; incomplete writes retain an explanation.
Imported content remains producer-reported history, with no authenticity claim.

Exact chain words support local movement comparisons; duplicate matches remain
ambiguous. Missing or different origins can still be kept as explicitly described
standalone history. The new note retains the original origin and comparison
status. It never adopts a neighboring operation as the original source.

Independent review reproduced a BOM mismatch across the real CLI-to-Notes path.
Core comparison now treats the initial encoding marker separately from chain
words; full-source revision uses current BOM metadata, never the old receipt's
assumption. Removal of the BOM remains a changed full revision. Both raw source
strings are retained. Line endings and controls are labeled in shared source
comparison readings so invisible changes can be inspected.

Native review found long imported targets could push actions below the viewport,
and toolbar focus could align a pending title before cancellation. The header now
trims one line while full text remains in Observation. Boundary alignment is held
through the actual toolbar/chooser route. Apply checks both the captured buffer
and the opened file's identity/original bytes; Save uses its existing independent
guard. A re-entrant or stale button cannot apply twice.

The MER insertion frame now explicitly labels proposed source as unsaved. Selected
source, result state, comparison and pending action use the existing document
window. No animation delays the result or claims a second operation occurred.

The last review reproduced an actual NUL or BOM displaying like its literal
escape spelling. Shared human-readable fields now double literal backslashes,
while actual control characters use a Unicode escape. The legend explains that
display treatment. Raw record strings and inserted note annotations are unchanged;
path preservation and both escape collisions have regression coverage.

## Verified delivery

Final source-only verification passed **1,151 tests** (997 core/CLI/MER, 154 native)
and **all eleven executable suites**, including **17 DERT-to-operation**, **60 MER**
and **24 published Notes checks**. The increment adds 55 core/CLI/MER and 17 native
cases. All **258** current source/test inputs match the final proof copy. Builds
and tests exclude documentation and original user notes. Network restore was
unavailable, so existing cached dependency packages were used with audit disabled
for those process invocations; no dependency declarations or default project
audit settings changed. This evidence does not establish a fresh vulnerability
audit.

The refreshed Windows package passes the full source-only published suites.
The root package's new result journey also passes capture/review and native
handoff checks. Bundled `observed-results.DERT` passes lint and real discovery
against disposable fixture package metadata; captured evidence remains clearly
fixture-derived in the proof. Full review and copied commands preserve source
bytes, including quoted Unicode paths, BOM and mixed newlines. No npm or Codex
process was started to obtain that discovery evidence.

Inspected actual native control renders at 1180/780 pixels for insertion,
observation and source comparison, including changed source, line-ending-only
changes and stale disabled Apply. Narrow/wide MER engine frames show proposed
unsaved source with the result annotation and unchanged-source footer. Native
controls verify deliberate Apply, duplicate/stale refusal, one-step Undo, original
selection on cancel, guarded Save/reopen, and real producer-to-editor BOM handling.
Published Notes checks establish the adjacent host and owned review open and
close without applying. Follow-up independent review found no remaining material
issues after the recorded fixes.

Local evidence:

- `.artifacts/source-only-proof/f829232814854d5988830aaa4c99c01a/output.txt`
- `.artifacts/results-root-publish-verified.log`
- `.artifacts/results-run-smoke.log`, `.artifacts/results-notes-smoke.log`
- `.artifacts/results-example-lint.log`, `.artifacts/results-example-capture.log`, `.artifacts/results-example-review-verified.log`
- `.artifacts/results-native-visual-final.log`
- `.artifacts/result-native/insertion-1180.png`, `.artifacts/result-native/insertion-780.png`
- `.artifacts/result-native/comparison-changed-780.png`, `.artifacts/result-native/comparison-line-endings-780.png`
- `.artifacts/result-native/stale-780.png`
- `.artifacts/mer-visual/359f7c64a7d04b46b98e31ac13adff91/held.png`
- `.artifacts/mer-visual/971852bd15434b62a2862a7562b63c7c/held.png`
- `.artifacts/results-core-bom-red.log`, `.artifacts/results-core-bom-green.log`
- `.artifacts/results-core-escape-red.log`, `.artifacts/results-core-escape-green.log`

These are source, real-control, engine-render and executable checks. They do not
certify screen-reader, IME, OS clipboard or human usability acceptance. This
increment does not implement every result adapter or stable origin key policy.
Update rehearsals and doctor journals, including failed verification, remain
explicit follow-on work. The next recommended bounded TODO is completing the
update transaction. Portable schemas and automatic agent delivery stay deferred.
