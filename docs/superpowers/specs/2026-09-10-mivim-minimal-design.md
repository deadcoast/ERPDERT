# MIVIM — minimal

Authority: the user's explicit corrections on 2026-09-10, after inspecting the
installation build. These supersede previous working recommendations about the
default authoring surface and palette. Original language source remains intact.
The user rejected seafoam, a teaching workbench as the main application, and
minimalism that removes design rather than refining it.

## Person and visible experience

1. The everyday surface is a small, rectangular notepad terminal. A person may
   simply write, or invoke supported DERT operations without adopting a large
   teaching application. The existing expanded editor and explanation panels
   become a separately launchable Help and troubleshooting workbench.
2. The working initial size is 640 × 420 device-independent pixels, resizable.
   This dimension is an implementation recommendation for the user's post-it-sized
   description, not a user-specified measurement. The content stays opaque and
   readable. Chrome fades; the document never does. The empty document contains
   no compulsory lesson, example or onboarding panel.
3. The same title rules, hook, line, sinkers, contextual reasoning, folds, exact
   source ranges, source-preserving Save and undo remain. Example:

   ```dert
   // A LITTLE ROOM FOR THE WEEKEND
   // -----------------------------

   packing ; overnight ;;
   notebook,
   headphones,
   coffee
   ```

## Visual construction

4. Retain WPF/AvalonEdit and shared authoring services. A separate rewritten
   editor would risk diverging ownership and undo behavior; shrinking the old
   dashboard would leave the explanatory layout dominant. Recommend a compact
   composition over the existing editor/session, with deliberate menus and
   temporary working panels, plus the retained expanded Help surface.

   | Token | Working value | Purpose |
   | --- | --- | --- |
   | Ink | `#090A0E` | Quiet, deep background |
   | Raised ink | `#14151E` | Slight separation for transient panels |
   | Paper light | `#E4E2EA` | Readable authored text |
   | Quiet type | `#90909F` | Comments and secondary text |
   | Violet light | `#B5A7EF` | Reasoning, line and selection emphasis |
   | Amber light | `#E7B17D` | Hook, boundary and correction evidence |

   All everyday text uses Cascadia Mono with Consolas fallback. The composition
   uses generous writing space, a compact menu/caption edge and a quiet status
   line. Rounded corners receive thin, asymmetrical highlights which diminish
   along their curves. Dark structural layers precede the highlights; no broad
   green fill, glossy sheet, simulated CRT noise or imitation Liquid Glass.
   Related native panels and terminal semantic accents follow the same palette.

## Menu behavior and ownership

5. Clicking the window reveals the root menu. With fading enabled (default),
   unanchored chrome fades after five seconds. Typing does not refresh that
   deadline. Pointer movement across the editor does not reveal the menu.
   Direct hover anywhere inside the root menu anchors the entire root strip.
   Opening a menu/submenu requires a click; hover never opens another branch.
   Each submenu anchors only while the pointer is inside that submenu's actual
   bounds. An ancestor panel fades when the pointer moves into its child.
   Leaving a hovered panel allows its existing deadline to expire; it does not
   grant another five seconds just for leaving. Clicking an item does.

   Alt/F10 provides keyboard entry; keyboard focus anchors the current menu
   level. Escape backs out and returns to the editor without changing its
   selection. Transparent/faded panels do not block document interactions.
   Root hit targets remain discoverable at their actual edge. Settings includes
   **Windows menu fade**, initially on, with persistent off behavior. Reduced
   motion uses the same states/deadlines with no animated transition.

   File, edit, note structure, operations, settings and Help remain available.
   Pending changes and failures remain visible in working panels; the chrome's
   fade must never hide a pending repair choice, operation result or error.
   A visible close/return control always returns a working panel to the document.

## Operational and Help relationship

6. The compact window and CLI share settings, argument validation and command
   execution. A command runs through the actual DERT CLI with literal argument
   boundaries, captured output/exit status and cancellation where supported.
   No arbitrary shell command execution or approval-token manufacture is added.
   Doctor/update retain their existing previews, explicit saved approvals and
   final verification. Changing the document invalidates any source-dependent
   operation handoff. Saved-file operations require Save; an unfinished editor
   buffer remains useful while eligibility is explained.

   Help launches independently, offers syntax examples/explanations, the current
   expanded workbench, CLI help and troubleshooting using the same operational
   path. It is a destination when the compact window's doctoring needs deeper
   inspection. Learning is appropriate in Help and optional in everyday use.
   Start, the installer, direct file opening and `DERT write` all reach the
   compact default. Help has its own discoverable launch route.

## Challenges and proof

7. Verify ordinary prose, unfinished titles/reasoning, edits while a panel is
   open, wrong actions, repeat visits, settings reload, off/disabled motion,
   keyboard traversal, moving between root/parent/child menus, and pointer
   movement outside exact hit regions. Preserve pending proposals on attempted
   dismissal and normal save/reopen behavior. Keep the selected text available
   when menu focus would otherwise complete an automatic edit.
8. Build and inspect the native window at default/minimum sizes, faded and
   anchored states, with realistic source and an operation result. Test actual
   input, not just opacity setters. Retain screenshots plus exact test evidence.
   Tests own fixtures; documentation never becomes a runtime/test data source.
   Visual acceptance is separate from parser/CLI passing checks.

## Implemented evidence, 2026-09-10

The compact host uses the existing editor and controllers. The Help workbench
is independently launchable through Start, `DERT guide`, `--guide` and
`Try Help.cmd`. Context selection uses full-width Choose notes/Preview pages;
result review retains its observation, source comparison and insertion readings.
Both secondary reviews fit 640 × 420 and 460 × 320 without discarding actions.

Verification recorded in the workspace:

- **1,375 current tests passed**: 1,168 core/CLI/MER and 207 native. This adds
  70 core and 53 native cases to the preceding increment. Settings/capture tests
  cover real subprocess output, literal arguments, malformed files, effects and
  cancellation. Source binding includes option-first write/guide, saved
  `--from-source` doctor/update approvals and a result record's implicit origin.
- **21 menu-state cases** check the exact deadline and independent anchors.
  Native routed clicks, keyboard commands, rendered hit rectangles, branch-edge
  arrow navigation, settings reload and F10/Escape exercise the actual host.
  Native UI Automation invocation refuses disabled original review buttons
  while a real CLI `inspect` runs. Save/Undo are blocked for that source; output,
  exit status and editing resume after completion. A small running-status footer
  remains visible when the command panel is closed.
- **13 compact review cases** cover default/minimum size, later paged notes,
  keyboard choice, complete copy/new-file export, source/proposal/file identity
  changes, insertion, duplicate refusal and one-step Undo. Existing expanded
  review tests continue to pass for Help.
- Fresh source-only runs `c4eb42a9b3464bc19fea8436a16a835b` and
  `79b4581b933d4e95972c37267219552e` passed **all eleven executable suites**
  without documents as inputs. The latter used 304 source/test inputs and
  includes the completed-command message correction. A final running-footer
  refinement and its regression followed that snapshot: the entire current
  solution then passed again, and the refreshed published Notes passed **24/24**
  checks in `notes-publish-proof/bf5533a67c0e4ca68dae780cbf1fd38f`.
- The first source-only run's installation proof
  `installation/d65e0f30077347248a6b9b6eae8d3ffc` passed **91 delivery assertions
  and six native interaction checks**. Actual owned HWNDs measured Notes at
  640 × 420 DIP and Help at 1180 × 780 DIP, at 96 DPI. Help opened independently
  while the literal-file compact note stayed open; closing Notes left Help
  alive. Setup/removal preserved the existing lifecycle and connection guards.
  This installation run precedes the two status-display refinements above;
  those refinements do not change installation or launch routes.

Native renders under `.artifacts/mivim-minimal-proof/final` were inspected:
`minimal-writing-root.png`, `minimal-writing-faded.png`,
`minimal-child-anchored.png`, `minimal-reasoning.png`, `minimal-cli-result.png`,
`standalone-help.png`, and the compact context/result images at both widths.
These are WPF control renders. Inactive-desktop PrintWindow client images do
not establish visual acceptance. Pointer geometry and routed native actions are
tested; a human's physical mouse/keyboard experience has not been approved by
those tests. Cross-monitor DPI, alternative fonts, IME and long-note performance
remain separate evidence work.

The before/after review records remain in `.artifacts/mivim-minimal-proof`:
`minimal-review-before.trx` (five reproduced UI failures),
`minimal-review-after.trx`, `source-binding-before.trx` (three missing route
bindings), `source-binding-after.trx`, and `compact-review-before.trx` versus
`compact-review-final.trx`. Later passing native runs include the corrections.
The final whole-solution stdout is retained in `current/solution-output.txt`;
the runner reused one TRX filename, so the TRX alone retains only the last test
project's result. This logging limitation is separate from both passing totals.

The unsigned workspace installer is `.artifacts/installer/DERT-Setup.exe`.
SHA-256 of the initial minimal build, before the edge refinement below:
`9C4599413FD46942B2BC69AB55A928493C21292A61951E0DDFD5EB2E50A4C7D4`.
The portable folder is `.artifacts/win-x64`, with separate Try Notes and Try Help
launchers. No public release or canonical language version is claimed. Clean
standard-account installation and downloaded-file trust prompts remain on TODO.

## Outer-edge correction from five screenshots

The user's screenshots `230543`, `230540`, `230534`, `230531` and `230526`
(2026-09-10, supplied from their Screenshots folder) show the thin Windows
outline separated from the app's inset violet contour. The top-left and
bottom-right corner strokes exaggerate that separation. The user explicitly
wants to retain the system outline for desktop contrast and match the app's
highlights to it.

Pixel inspection found the upper outer edge around `#39393A` and lower edges
around `#373738`–`#383839`. A hue change alone would leave the offset silhouette.
The correction removes the app's perimeter stroke and two-pixel content inset,
aligns the rounded fill/menu with an eight-DIP curve, and places short neutral
highlights directly beside the native edge. Their peak color is `#39393A`;
alpha tapers them into the existing dark background. The previous violet/blue
corner strokes and thirteen-DIP inner contour are gone. The native outline's
color and rendering policy are not overridden.

Verification: **207/207 native tests passed** in
`.artifacts/window-edge-proof/edge-native-final.trx`. The first run exposed
concurrent WindowChrome initialization in tests on separate STA threads; the
compact-window tests now share an xUnit collection. The failed run remains in
`edge-refinement.trx`. Production windows already share their UI thread.
No new tests mirror the decorative geometry; the existing source, menu,
keyboard, operations and compact-review cases verify the retained interactions.

Native renders were inspected under `.artifacts/window-edge-proof/final`.
The packaged HWND probe in `native-frame.txt` confirms system non-client
rendering enabled, a one-pixel visible Windows border at 96 DPI, rounded-corner
preference 2, initial size 640 × 420, native resize to 560 × 360 and normal close.
The probe waits for WPF's initial layout before measuring. This OS rejects
reading the border-color attribute; the absence of an override is established
by the app source, not an invented returned color. Microsoft's
[DWM attribute reference](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)
documents the system-border and corner controls. Native renders still do not
capture the composed desktop frame; the user's physical visual acceptance is
separate from these checks.

The user's existing portable Notes process kept its DLL open, so publishing
over that folder was refused. The successful refreshed payload is
`.artifacts/win-x64-edge`, including Try Notes and Try Help; the open process
and its note were retained. `.artifacts/installer/DERT-Setup.exe` was rebuilt
from this new payload. Its SHA-256 is
`AF1CA94EF61F1009C0D1CF77D96B0AE55B16A8AFEBE1C88FED356957FF4238FC`.
