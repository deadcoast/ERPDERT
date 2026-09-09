# Repair where the source lives

Working recommendation, 2026-09-08. Authority: the user's request to continue
development and polish, their source-window correction, and the existing
explicit, exact, reversible repair design. These review commands are application
controls, not DERT grammar or a new user decision.

1. An author correcting their own notes should see exactly what changes without
   losing the surrounding words. Notes-only use receives the full experience.
2. At a supported finding, `repair` opens a proposal in the existing source
   window. `before` and `after` compare captured versions; `apply` explicitly
   approves that proposal, `cancel` leaves it, and Enter exits without applying.
   `repair title` also reaches the title from another line. `undo` opens the
   latest saved original for the same explicit comparison and approval.
3. `// WEEKEND` over `// --` becomes seven rule marks. An unfinished contextual
   `// plans ; weekend ;;` offers its existing possible endings; `close <line>`
   or `close eof` selects one. No boundary or ownership decision is guessed.
4. Copying another command loses the visible source connection. A second repair
   dashboard duplicates it. Recommend changing the source window's displayed
   revision, with an attached explanation and a clearly named proposal state.
   Keep standalone commands and complete diff/ownership details accessible.
5. Reuse RepairFileService for revision/identity checks, verified recovery copy,
   guarded write and verified undo. No preview writes. The saved window comes
   from RepairResult.VerifiedSource captured under the write handle, followed by
   fresh structural analysis. Old installation inspections are discarded when
   source coordinates change. Completed journals are never rewritten as events.
6. Later external edits refuse application; a write failure preserves the actual
   recovery message and never labels captured source as verified current bytes.
   Remaining findings survive a successful repair. Undo may restore an unfinished
   note and must return its corresponding analysis status. Missing/corrupt undo,
   invalid choices, repeated apply and EOF do not invent success. Replay remains
   read-only; scripted review requires both --review and an explicit apply line.
7. Complete the supported title and contextual-note repairs using the existing
   guarded service. Broader operation/update workflows retain their current
   boundaries. Use one window, ordinary keyboard commands and equivalent plain /
   reduced-motion content; no additional animation delays a saved result.
8. Verify real writes and undo only against test-owned files, including UTF-8 BOM,
   mixed newlines, stale revisions and remaining errors. Inspect actual published
   CLI output and rendered compositor frames at wide and narrow sizes. Record
   those observations separately from tests; visual quality remains revisable.

## Observed implementation

The supported title and note journeys are implemented. Review exposed reversed
undo ownership, unguarded copied note choices, whitespace-sensitive diff labels,
EOF focus loss and ambiguous post-save receipts; each was corrected with focused
coverage. Visual inspection caught stale text behind an overflow hint and an
unnecessarily clipped undo explanation; the layout now reserves enough height
when available and draws the overflow row once.

542 tests and all nine executable suites passed in a fresh source-only build,
including 46 MER checks. The published Windows program completed comparison,
apply and undo in a PTY. The visual harness also drove the actual review adapter
and guarded writer through title and note round trips, verified exact restoration
and rendered wide/narrow source frames. This is concrete delivery evidence; the
broader visual and operational ambitions remain open to further development.
