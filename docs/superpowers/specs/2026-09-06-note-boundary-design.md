# Note-boundary rehearsal

Working implementation design for the next interaction recorded in README and
IMPLEMENTATION-STATUS. The user requested continued development of that plan.
Original language authority remains docs/original.md and subsequent corrections;
this adds authoring behavior, not canonical grammar or new DERT punctuation.

## The person and the moment

An author has an unfinished reasoning block. The familiar `//` marker is doing
several contextual jobs, and the author wants to see which words belong together.
The response must make a real structural choice visible without demanding parser
vocabulary or presenting a missing marker as a personal failure.

The source's doctoring block supplies the required case. Remove only its final
standalone `//` on a generated copy: its three annotated entries remain visible,
and the following `commands ; updateme ;;` must not acquire invented ownership.
Also examine an earlier boundary: a following prefixed line might be an entry,
an ordinary note, or a new reasoning note once the author closes the first block.

## Alternatives and decision

1. Insert a closer at the parser's recovery stop. This would encode a recovery
   heuristic as author intent; it cannot handle an earlier intended boundary.
2. Diagnose and direct the author to manually edit punctuation. This preserves
   intent but does not provide the planned exact rehearsal and recovery.
3. Offer source-position choices, show their ownership consequences, then use
   the existing guarded apply/undo pipeline. This is the chosen design.

There is no automatically preferred boundary. Leaving the file unchanged is the
default. Scope choices refer to existing source lines; source remains flush-left.
No repeated punctuation, indentation, brace, metadata key or new closure rule
is introduced.

## What the author types and sees

`ERPDERT repair <file>` continues to mean title-rule repair.

`ERPDERT repair <file> --at <line>` selects an unfinished reasoning block through
its opener, an entry, or intervening spacing within its recovered source extent.
The first screen is a boundary rehearsal: actual opener, recovered entries with
their own annotations, and the recovery stop (next unprefixed line or EOF).
It explicitly labels that current grouping as recovery, not confirmed intent.

Choices are `before <source line>` for each entry, plus the recovery-stop line
or EOF. Each states how many current entries stay in the block and how many leave
it. A line number is not a menu ordinal. Blank-only cuts with identical ownership
are not separately offered in this increment. The author can leave or select one.

`ERPDERT repair <file> --at <line> --close-before <line|eof>` directly selects
one of those offered positions. Selection alone is not permission to write.
It opens the exact edit rehearsal and the existing default-no confirmation.

The selected rehearsal shows:

- The actual block header and neighboring source at the insertion point.
- One added standalone `//`, with line-number and diff furniture outside source.
- Entries kept within this block and released from it.
- For released entries, the actual reparsed role and owner, with old/new source
  line numbers. A following structured note must not be called an ordinary note.
- The next source line outside the selected block, or end of file.
- Remaining diagnostics, separated from success of this selected action.

Interactive selection uses an English prompt accepting an offered source line,
`eof`, or `leave` (default). The subsequent confirmation remains explicit.
Plain mode preserves every choice and explanation. Redirected output never asks
for input: it prints valid selection commands, then an action-bound approval
command after a position is selected. Commands use the launched executable and
the existing tested literal-safe PowerShell path handling.

## Meaning, source, and repair model

The parser is unchanged. Its current recovery ends an unfinished block before
the first unprefixed nonblank line, or at EOF. That is inspectable recovery,
not a claim that the missing closer necessarily belongs there.

Core analysis reads that block and lists finite insertion points. A selected
plan inserts only one standalone marker. It never removes prefixes, commas,
quotes, existing lines or author text, nor changes targets or runs operations.
The chosen plan reparses its proposed text and verifies that the original opener
now has the selected closing marker and the expected retained entries.

Insertion before an existing line uses the preceding physical line's newline.
At EOF, preserve final-newline presence; reuse the nearest preceding newline,
or disclose Windows CRLF when none exists. Keep BOM and all other bytes intact.
Unicode note content is retained; this action does not measure title width.

The scope change is deliberate and visible, unlike title-rule presentation repair.
The author chooses which current entries remain owned by this block. A current
recovered relationship is not labeled canonical merely because it is in the AST.
Role descriptions are observations of the proposed parse, not command semantics.

## Guarded writes and backward-compatible undo

A shared source-repair interface separates exact edits from terminal delivery.
The existing title planner implements it without changing title behavior.
The file snapshot carries the selected plan; file identity, original/proposed
bytes, operation kind and undo record remain bound to the approval token.

Boundary selection and final confirmation use the same captured document and
physical identity. A changed/replaced file withdraws the action when applied.
Choosing a boundary must not silently reread a new file and transfer approval.

Title receipts retain the existing version-1 format and replay path. Note closure
receipts use version 2 with a typed selection (repair kind, original block opener
line, chosen boundary). Undo recomputes that exact supported plan from its saved
original, verifies the result hash, and retains the existing backup checks.
Unknown kinds, invalid positions and malformed records cannot act as arbitrary
text patches. Latest repair ordering spans both supported kinds; undo restores
the latest repair only, not an inferred older state.

All existing Windows file guards, locks, pre-write backup verification, readback,
rollback, and action-bound approval remain in force. Recovery copies are local
and unencrypted. Writes are recoverable, not power-loss atomic. Active hostile
filesystem races are not claimed to be prevented. No package action is executed.

## Challenge cases and acceptance

- Original example unchanged: no boundary offer for a completed block. Its final
  annotated-entry comma is still valid source and is never removed.
- Generated copy missing only its closer: offer the recovered stop explicitly;
  chosen correction restores the defined nesting, preserving English exactly.
- Close before the final prefixed line: report it outside this note, using its
  reparsed role. Choosing another boundary changes both output and approval.
- A released structured opener can own later lines; report that observed owner
  rather than pretending all released entries become unrelated free prose.
- Blank lines, CR/LF/CRLF, mixed newlines, BOM, no final newline and empty block:
  exact one-marker insertion and byte-exact undo.
- Multiple blocks: repair only the selected unfinished block, never a neighbor.
- Inline DERT1002 and unfinished lists: retain diagnostics, explain that this
  action is for block boundaries, and do not invent an inline/literal/list fix.
- Invalid line or position, leave/default, redirected input, stale selection,
  stale confirmation, different-boundary token, old title receipts, changed
  backup and corrupt note receipts: no unintended source write.
- Existing title repair/undo and all current tests remain operational.

Run real core/file/CLI tests, then inspect actual colored and plain terminal
choices, scope preview, cancellation, apply and undo. Execute copied commands
against generated corpus copies using the published executable. Hash protected
originals before and after. Tests are not a usability or screen-reader study.

## Explicit remaining scope

No automatic block closing, inline quote repair, comma interpretation, arbitrary
recursive nesting, general-purpose formatter, editor extension, installer,
package adapter, or JSON language export is added. Those retain their own source
decisions and future proof requirements.
