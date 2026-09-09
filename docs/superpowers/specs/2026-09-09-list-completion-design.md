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
