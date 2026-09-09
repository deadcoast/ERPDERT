# One more item, or finished here

Working interaction recommendation, 2026-09-09. Authorized by the user's request
to continue the next TODO. Original authority: the active-string example says
there is no comma on the last string item. The original reasoning example keeps
its final comma; it must not inherit active-list completion rules. The experience
design explicitly proposes separate Add another item and Finish this list choices.
No grammar decision is added by this interaction.

1. Serve someone making a list who pauses after `snacks,`. A pause is not a
   mistake. Give them an easy way to continue or deliberately finish their thought.
2. Select the statement header or one of its active items in the notes window.
   Its context offers Add another item and, for a trailing continuation comma,
   Finish this list. Finish previews one removed comma. Add opens a literal item
   field and previews the actual new line before Apply. No placeholder is saved.
   Apply changes the buffer; Ctrl+Z undoes it; existing Ctrl+S saves explicitly.
3. Deepen the existing expanded sinker form:

   ```dert
   items ; weekend ;;
   snacks, // Enough for the walk.
   ```

   Finish keeps the annotation and removes only the comma after `snacks`.
   Adding `thermos // Bring tea.` inserts that exact item immediately afterward,
   before existing blank lines or notes. Adding again to a completed list first
   previews its needed continuation comma. Reasoning entries remain untouched.
4. Automatic comma removal guesses intent. Inserting an empty line merely moves
   the caret and does not finish an addition. Recommend entering the actual item
   in a source-bound preview: the person sees its owner, literal text and resulting
   ending before accepting it. Retain the existing quiet incomplete diagnostic.
5. Plans capture the full source, selected statement and exact edits. Reparse the
   proposed document to ensure the new text is one additional active item of that
   statement and does not absorb following prose. Preserve all other text, commas,
   annotations, blank lines, line endings and ownership. Native buffer application
   groups edits as one undo step, retaining existing selection for Finish and
   placing the caret at the new item for Add. Disk saving retains existing guards.
6. Reject empty or multiline additions and text that would become a header,
   terminator or free note. Explain the actual boundary instead of treating it as
   a valid item. A typed trailing comma may keep the new item unfinished, provided
   it does not absorb existing text. Source changes withdraw an open proposal.
   Leave cancels a proposal; unapplied typed words require explicit discard before
   leaving the document and cannot silently disappear through Save/New/Open/Close.
   Previewing, navigating or reopening an incomplete list never removes its comma.
7. Deliver shared list-edit plans, the native authoring choices, exact preview,
   undo/save/reopen tests, and a discoverable CLI lint handoff to the notes window.
   This does not add unattended list repair, change reasoning grammar, or complete
   general formatting, literal-language design, Unicode width or package operations.
8. Inspect actual WPF frames for both choices at normal and narrow dimensions;
   exercise native focus/buttons, stale previews, second additions and verified
   saves. Run source-only tests and published checks. Record clipboard/IME and
   clean-account limits separately from these control and executable results.

## Delivery and review evidence

Implemented shared `ListCompletion` plans, native `ListAuthoringPanel` choices,
grouped buffer edits, existing guarded Save/reopen, and DERT3003 authoring handoffs
from lint and MER. The handoff opens the current file; it explicitly does not
inherit captured-revision checks or approval from repair links. The editor's
proposal is then bound to the actual buffer revision and selected owner.

Read-only review found two concrete defects. Unfinished quoted text could expose
an internal comma as a list separator; the new guard uses the existing escaped
quote recognition and leaves such text unchanged. Four regression cases failed
before the guard and passed after. Add could retain an earlier source selection
after moving the caret; the next input could then replace the old item. Add now
clears that selection, verified by a failing-then-passing native text-input case.
Finish still preserves its mapped selection.

Wide and narrow WPF frames were inspected for both choices. The first compact
addition clipped its comparison and buttons. Revised space allocation, compact
spacing, input styling and removal of duplicate comma advice leave the source,
typed item, full comparison and Apply/Leave visible together at 780×780. Source
and context use the accepted MER palette. These are rendered controls, not OS
screenshots. Evidence: `.artifacts/list-host-proof/list-add-1180.png`,
`list-add-780.png`, `list-finish-1180.png`, and `list-finish-780.png`.

Fresh source-only verification passed 618 core/CLI tests and 23 native host tests
(641 total), then all ten published executable suites. This includes 28 list-plan
cases, ten added native list cases, nine published Notes checks, 50 MER checks
and 20 update checks. Native list cases cover draft retention, source invalidation,
button dispatch, exact Finish selection, Add then typing, grouped undo, repeated
addition and guarded save/reopen. Published checks verify literal lint commands,
actual CLI-to-window launch, and unchanged incomplete-list/BOM bytes on opening.
The working Windows build is republished separately.

Evidence is retained in `.artifacts/list-source-only-final.log` and
`.artifacts/source-only-proof/758acc8b76034aa89f864324bce4e22c`.
The normal guarded-writer recovery limits still apply. OS clipboard/IME, modal
departure dialogs, clean-account installation and long-note usability have not
been established by these tests. The next recommendation remains the original
florin goodbye; these list choices do not settle new literal or nesting grammar.
