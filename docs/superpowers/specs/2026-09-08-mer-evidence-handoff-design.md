# MER: keep the evidence, carry the next action

Development recommendation and implementation record, 2026-09-08.
The later user screenshot rejected the default receipt's lack of source UI and
legibility. The [document-window correction](2026-09-08-mer-document-window-design.md)
supersedes this record's default presentation and automatic printing of repair
links. Its source-bound handoff remains available through explicit detail/repair.
Authority: the user's instruction to continue development, AGENTS.md's delivery
and correction requirements, the original title-rule and contextual-note cases,
and the worked repair journey in `docs/EXPERIENCE-DESIGN.md`. This increment does
not establish new language rules or turn earlier recommendations into user decisions.

## Person and visible interaction

An author checks their notes with `ERPDERT mer 'weekend.DERT'`. They should be
able to recognize the source, see what a completed check established, and follow
a finding without reconstructing a path or guessing what the tool can repair.
Repeated checking should be calm enough to read after the initial trace settles.

The general evidence view keeps current work at a fixed row and recent settled
results below it. The source stays anchored to the last observed line until new
evidence selects another. Recent results use literal state labels; shortened
text is marked and retained in full in the receipt. The source-focused title
and ownership views continue to take precedence where applicable; this increment
does not replace them with the general evidence view.

The original title construct is still the material being checked:

```dert
// MY FIRST TITLE
// ----
```

MER shows the proposed `// --------------`, then `DERT2003 / line 2` and
`REHEARSE / TITLE RULE`, followed by a complete command using the launched
executable, absolute source path, and `--from-lint` byte revision. For an
unfinished `// support doctoring ; conflicts ;;` block, `CHOOSE / NOTE BOUNDARY`
links to the original opener with `--at`. It does not insert a closer or infer
where the author's reasoning ends.

## Alternatives and recommendation

A list of stage names loses the explanation as the next stage starts. A growing
log retains everything but moves the author's source throughout the inspection.
The selected general view reserves a stable current position and a bounded area
for settled results; the complete receipt preserves everything outside that area.
A short trace responds to a new observation once, rather than looping while
the same evidence waits. A pending state remains visible without a timer.

Generic instructions to run lint would make the person repeat the check merely
to obtain its next action. An unbound repair command could select a different
note after an edit. The selected handoff uses the already implemented capability
advice and byte-bound preview flow. Embedding new repair prompts in MER would
require the full selection, approval, write, and verification journey; that
remains a separate increment rather than a partial duplicate here.

## Meaning, ownership, and effects

MER captures raw file bytes once and retains the resulting parsed document.
After live rendering has released its rows, it prints the supported actions
from that same analysis and hashes those same bytes. Replay uses the same
capture; it does not reread the path at completion. The shared lint advice
determines availability, including title-width limits and the exact unfinished
block opener. Plain mode and literal-safe PowerShell paths follow the link.

The command opens the existing preview-only `--from-lint` flow. Selecting a note
boundary and authorizing a source write remain separate operations with their
existing ownership diff and file guards. There are no source edits, recovery
copies, package operations, or new approvals during MER analysis or handoff.
The in-memory demo cases do not advertise repairs of nonexistent files.

## Adverse and repeated use

- Unfinished edits retain their source and diagnostic. Missing endings lead to
  choices; unfinished inline notes and lists explain unsupported repair capability.
- Ordinary prose and clear notes do not advertise a repair merely to fill space.
- A file changed while results are printed still receives only the old revision's
  link. Following it refuses the stale source before selecting any repair.
- BOM, CRLF, Unicode notes, unusual paths, and narrow terminals preserve the
  revision and a complete logical command line.
- Failed reads never acquire repair actions. Broken output stays an output
  failure, without manufacturing a failed source check or a successful handoff.
- Failure and interruption stop the trace before the overall finish event.
  Reduced motion retains the same evidence and states without timed changes.

## Scope and evidence

The existing five failing margin regressions reproduced the missing completed
explanations, moving active row, repeated trace, and ticking failure state.
Additional tests exercise source continuity, shortened result text, file changes
during both live/replay delivery, capability checks, and exact copied commands.
The executable suite follows title and note commands from another directory,
opens a selected boundary preview, refuses stale input, and checks unchanged bytes.

Rendered wide unfinished-note and narrow failure frames were inspected, followed
by the packaged Windows demo's paused/held/detail views in a PTY. Inspection and
independent review found a doubled omission mark; the compositor now uses the
text clipper's own mark. Final narrow-frame inspection also exposed a duplicated
prioritized finding and its misleading overflow notice. A failing regression
confirmed the issue; the live detail now presents the explanation once, and
the refreshed frame was inspected. These are layout and execution observations, not proof
that authors find the experience delightful or that a clean-account install works.
Full verification results are recorded in the changelog. Broader MER repair
interaction, user judgments, editor integration, and note application remain open.
