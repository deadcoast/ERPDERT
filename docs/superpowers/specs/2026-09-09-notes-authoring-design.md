# Write, align, keep writing

Working host and interaction recommendation, 2026-09-09. User authorization:
continue the next steps in TODO. Authority: original adaptive title rule and
contextual `//` design; WHO-WE-ARE's notes-only value; experience-design's
title/authoring journey. Preserve the accepted MER palette and restrained
source-centered composition. Host controls are not new DERT grammar.

1. Serve a person writing notes and returning to them tomorrow. The first title
   should look deliberate immediately, and uncertainty should be explained next
   to the source. Saved and unsaved work must be unmistakable.
2. `ERPDERT write [file]` opens a packaged Windows authoring window. New notes
   start empty with an unobtrusive title example. Type a title and press Enter:
   add the matching rule as one undoable buffer edit, leave the caret ready for
   the next words. Rename a title and leave its line: adjust its existing rule.
   Paste completes before title treatment. Opening an existing file only offers
   a preview; it never applies imported repairs automatically. Save, Save As,
   Open, New, Undo and Redo have visible controls and normal keyboard shortcuts.
3. Reuse DertParser, semantic highlights, title plans and exact fold spans.
   Reasoning folds at rest; selecting a handle exposes its explanation and can
   temporarily unfold it for editing. Active string payloads stay expanded.
   No indentation or operation bindings are inferred. Written commands remain
   text in this authoring host; the CLI retains its explicit operation workflow.
4. Compare hosts against this experience:

   | Candidate | Author benefit | Constraint to verify |
   | --- | --- | --- |
   | Installed VS Code integration | Familiar existing editing and extension APIs | It is present on this development PC, but unavailable editors must not strand first launch; extension delivery is another product boundary. |
   | Native RichEdit host | Windows text input and local file dialogs | Does not directly supply the needed code-folding and semantic text-edit model; styling must not disturb native undo. |
   | WPF + AvalonEdit | Native Windows input, undo groups, source offsets, semantic rendering and folding driven by the same C# parser | Verify actual title undo, caret mapping, paste, folding and packaging before accepting the host. |

   Recommend embedded WPF/AvalonEdit for this bounded proof. Local WindowsDesktop
   targeting/runtime packs exist; [AvalonEdit 6.3.1.120](https://www.nuget.org/packages/AvalonEdit/6.3.1.120)
   and its [source](https://github.com/icsharpcode/AvalonEdit/tree/v6.3.1)
   provide the editing/folding mechanisms. This choice is based on authorship
   and distribution needs, not the absence of an existing editor on this PC.

5. Keep text, selection, undo and display separate. Colorizing and folding never
   rewrite source. Automatic title treatment follows authored title changes at
   a completed editing boundary. Undo and explicit rule deletion suppress an
   immediate repeat; an explicit Align title action remains. Imported mismatches
   require a preview and Apply. UTF-8 BOM and existing line endings are retained.
   Saving is explicit and checks the originally opened file's bytes and identity.
   Keep a verified previous-byte recovery copy before rewriting an existing file;
   verify writes, attempt rollback on failure, and never claim saved on failure.
   Save As creates a new file exclusively; it cannot silently replace a path.
6. Exercise new title, rename, multi-line paste, undo/redo, deliberate rule
   deletion, reopened mismatches, non-ASCII width limits, partial reasoning,
   folded source editing, invalid UTF-8, stale/replaced files and failed writes.
   New/Open/Close with unsaved work requires Save/Discard/Cancel. Failed save
   keeps the buffer and blocks departure. Plain-English messages retain details.
7. Deliver one packaged authoring window and CLI entry point, plus shared guarded
   save and actual host tests. Full note app, installer, operation execution,
   generalized formatting, Unicode title-width policy and multi-document work
   remain separate TODO items. Do not mark them delivered by this proof.
8. Run source model/save tests and actual Windows editor-control journeys. Inspect
   rendered host images at normal and narrow dimensions, exercise keyboard events
   and repeated sessions, and keep evidence boundaries explicit. A rendered WPF
   frame is not a screenshot of a clean Windows installation.

Visual tokens: MER paper `#E9E7DF`, background `#141A1E`, muted `#8C97A3`,
hook copper `#E6A57E`, line teal `#76C9C1`, result `#A5C685`. Consolas carries
the source and punctuation; Segoe UI carries actions and explanations. One
document area with a line gutter; a quiet attached context area and a persistent
save state. A short title-rule accent follows a real alignment; reduced motion
shows the settled edit immediately. No welcome animation or nested panel grid.

## Implemented experience and inspected evidence

The embedded host is implemented in `src/Erpdert.Notes`, with the pinned editor
dependency, WPF resources, DertParser-driven colors/folds and exact title plans.
`AuthoringSession` owns buffer treatment; `DraftFileService` handles explicit
saves separately from CLI repair approvals and repair undo receipts. `write`
launches the fixed packaged host with literal arguments; it does not resolve an
editor through PATH. `src/Publish-Windows.ps1` publishes both runtimes and the
dependency license, plus `Try Notes.cmd`.

Review reproduced two repeated-use defects: Enter after renaming an aligned
title created a detached old rule, and unrelated Undo disabled later title
treatment. Regression tests failed before the fixes and passed afterward. Enter
now adjusts the existing rule and advances past it; Undo suppresses an immediate
repeat while subsequent title edits retain their treatment. A save failure cannot
leave a green saved status. Creating a new file verifies identity and bytes again
after assigning its final name, before marking the buffer saved.

Actual WPF control renders were inspected at 1180×780 and 780×720, with motion
off. The first frames exposed default Windows button colors and a selected line
outside the narrow viewport. The revised theme retains the MER colors in buttons,
fold handles and scrollbars; resizing brings the caret's line back into view.
The source gutter, expanded active sinkers and attached owned entries remain
visible. Frames: `.artifacts/notes-host-proof/notes-1180.png` and `notes-780.png`.
These are rendered controls, not photographs or screenshots of the Windows desktop.

Verification: 590 core/CLI tests and 13 native host tests passed in fresh
source-only inputs. All ten published suites passed, including six new checks
for the packaged notes host, runtime/dependency/license/launcher, argument
refusal, direct open, CLI handoff, normal close and unchanged source on reopen.
The published host was launched from another directory with empty PATH and
installed-runtime lookup disabled. The filename includes spaces and punctuation;
the mismatched title and UTF-8 BOM remain untouched. Recovery tests include a
same-byte replacement file, failed write, failed rollback and failed first save.

The larger run exposed a test-fixture readiness race: File.Exists was not proof
that Windows had released the child PID marker for reading. The fixture now
publishes the completed marker and waits for a readable PID before cancellation.
The runner's production behavior was not changed for this test correction.

Remaining evidence limits and requirements:

- Paste scheduling is exercised with a complete inserted buffer and a routed
  paste event; OS clipboard contents are not changed by the harness. Full native
  clipboard and IME journeys need separate delivery inspection.
- Native text input, Enter and Undo routes are exercised. Windows file picker
  and Save/Discard/Cancel dialogs are implemented but not automatically driven.
- Reasoning loads folded and keyboard inspection/editing works. Expanded notes
  stay open until explicitly folded; automatic refolding after temporary
  inspection remains a worked design question, not a delivered behavior.
- Unicode title width, deliberate list completion, florin treatment, long-note
  performance, source comparison after external edits and clean-account
  installation remain in TODO. No formatter, installer or full note app is claimed.
- Recovery copies are unencrypted, retained previous bytes. Buffer undo does
  not survive reopening. Existing-file saves verify and attempt rollback but
  are not atomic against power loss; recovery storage is not a backup service.
