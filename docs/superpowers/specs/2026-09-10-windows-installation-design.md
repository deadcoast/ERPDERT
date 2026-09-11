# Arrive, write, return, remove

Working installation recommendation, 2026-09-10. Authority: the user's request
to continue the next TODO, its normal-user installation requirement, the original
title/note constructs, and WHO-WE-ARE's complete notes-only outcome. This adds
Windows delivery behavior, without changing the language or the accepted MER view.

## Experience and choice

1. Serve someone who wants to try the notes and return tomorrow. They download
   one setup executable, see the actual destination and integration choices,
   install for their own Windows account, and open an editable notes example.
   No SDK, Node, external editor, account, or tutorial is required.
2. Setup installs the bundled CLI and Notes runtimes under
   `%LOCALAPPDATA%\Programs\DERT`. Start offers **DERT Notes** and **Your DERT
   example**. Finish can open `Documents\DERT examples\weekend.DERT`. Its title,
   free notes and folded reasoning use the existing authoring experience. The
   author can Save, close and reopen the same file. Future starts of Notes open
   the existing empty-note surface; there is no recurring welcome sequence.
3. The original `// TITLE`, matching rule, and contextual reasoning gain a
   discoverable entry point. Installation never runs a written operation or
   repairs an example merely because it opens it.
4. Compared approaches:

   | Approach | Person's benefit | Delivery constraint |
   | --- | --- | --- |
   | Portable folder | Try immediately, retain an explicit movable copy | Does not supply durable Start entries, file opening registration or ordinary removal |
   | Per-user Inno Setup | One offline executable, visible choices, engine rollback for payload failures and registered removal | Requires direct Windows integration tests; late custom integration failures need explicit partial-completion reporting |
   | New custom WPF installer | Complete control of visual composition | Must first establish extraction, rollback, locked-file and removal behavior already provided by the installer engine |

   Recommend Inno Setup 6.7.3 for this increment. Its native keyboard-accessible
   dark wizard carries concise DERT copy; the existing Notes window provides the
   language's distinctive first interaction. Retain the portable publish output.
   This is an unsigned development package, not a public or canonical release.

## Meaning and ownership

5. Setup owns application files, its Start entries, its uninstall registration,
   its application-specific ProgID, and only the PATH entry it actually adds.
   The editable examples are copied outside the application folder, only when
   absent, and never entered into the removal list. Reinstall preserves changed
   examples and unrelated files. Recovery copies and saved notes remain owned by
   the author. The application directory is for installed binaries.

   **Command line** optionally appends the exact installation directory to user
   PATH. Existing entries and order stay intact; duplicates are not added. A
   new terminal receives the change. Removal deletes only a matching entry that
   setup recorded adding, preserving subsequent unrelated PATH edits and value
   type. An existing entry is not adopted. Reinstall retains that ownership.

   **Open with DERT Notes** registers `.DERT` as a supported file type, with a
   quoted direct Notes executable and quoted file argument. Opening a file never
   runs the CLI runner. Windows owns the default-app choice: use Open with / Always
   to choose Notes. Setup does not alter another application's default or the
   protected UserChoice key. Removal withdraws only DERT's registration.

   Setup is per-user and requests no elevation. App identity and directory stay
   stable across repeat installation; moving a registered installation requires
   removal first. Close Notes and finish CLI work before install/remove. No
   process is killed, no reboot is forced, and no in-use result is called success.

   Deselection on reinstall withdraws the connections previously added by DERT.
   Unreadable or non-text PATH storage refuses before copying. A later failure
   reports that application files are installed, names the connection needing
   attention, returns a failure exit code and suppresses automatic launch. It
   does not claim to roll back installed files or successful connections.

   Before copying, setup writes and flushes a small ownership marker tied to its
   application identity and exact destination. Retry reuses a valid marker
   unchanged. This makes an interrupted payload retryable without accepting an
   arbitrary occupied folder. Ordinary marker-creation failure removes only a
   newly created incomplete marker; existing evidence is preserved. Old partial
   uninstall artifacts can remain after recovery: setup never infers ownership
   from an `unins*` filename or deletes unknown files to make removal look tidy.

6. Challenges: an unfinished note remains valid to open; prose never executes;
   cancellation before installation leaves no application; another default app
   remains chosen; existing PATH spellings and other programs survive removal;
   an edited example survives repeated installation and removal; an open unsaved
   note blocks replacement; a foreign occupied destination is refused. Paths
   containing spaces and Unicode are exercised. Semicolons and percent signs in
   installation paths are refused because PATH/installer expansion cannot preserve
   those destinations reliably. This does not restrict document filenames.

## Delivery and evidence

7. Build a setup recipe, a repeatable compiler entry point, shared running-instance
   guard and executable integration tests. Test code owns its fixtures. Neither
   source notes nor design documents become package or test inputs. Keep compiler
   acquisition explicit, pinned and signature-checked; builds never install tools
   or download them implicitly.
8. Verify the actual installer against disposable Windows registrations and paths,
   including repeat installation, optional integration, failures and removal.
   Inspect wizard controls and installed Notes. Record which account and privileges
   were used. A namespaced test build on this PC is not a clean Windows account.
   Retain clean-account, downloaded-file trust prompts, cross-application default
   selection and human first-contact validation until observed directly.

Platform references checked for this design:
[per-user mode](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm),
[file-type alternatives](https://learn.microsoft.com/en-us/windows/win32/shell/fa-file-types),
[compiler 6.7.3](https://jrsoftware.org/isdl.php), and
[installer command-line controls](https://jrsoftware.org/ishelp/topic_setupcmdline.htm).

## Evidence record

Baseline: `dotnet test DERT.slnx --no-restore` passed 1,098 core/CLI/MER and
154 native Notes tests. This baseline did not establish installation behavior.

The implementation was reviewed against language ownership, the published
package, file-opening argument validation and native application lifetimes.
Independent review corrected late integration failures being swallowed by the
installer engine, a missing pre-copy lifetime recheck, unreadable PATH handling,
and unreconciled integration deselection. Inno 6.7.3 also checks its saved
`AppMutex` after uninstall confirmation and before our PATH-removal callback;
see the [pinned engine ordering](https://raw.githubusercontent.com/jrsoftware/issrc/is-6_7_3/Projects/Src/Setup.Uninstall.pas).

An actual power interruption left proof `b7314aedf7ae489f8be7cb138fa80186`
partially copied with no installed registry state. That before-fix folder is
retained under `.artifacts/installation-proof`. It motivated the durable marker;
setup does not retroactively adopt the markerless folder. A subsequent review
caught retry truncating valid evidence. The regression failed in proof
`a959031b3f544b829bee10aa54c5e72e` before the fix. Reinstall and interruption
checks now require the valid marker's modification time to remain unchanged.

Verification uses GUID-specific AppIds, file classes, synthetic environment
values, Start groups and editable-example destinations. Production never gains
the proof build's explicit late-PATH-failure switch. Installation checks run on
an isolated Windows desktop; they do not take focus from the user's work.

The fresh source copy at
`.artifacts/source-only-proof/670a1c9b0c41428c8f54765bf11dc897` passed **1,252
tests** (1,098 core/CLI/MER and 154 native) and **all eleven existing executable
suites**, including 60 MER and 24 published Notes checks. It contained **285
source/test inputs**, matching the working tree when copied. No design documents
or original notes were copied into the build. Restore used available local
packages; this is not a fresh dependency vulnerability audit.

Two harness failures remain separate from those passing suites. The first
isolated source-run wrapper supplied no usable stdin, making a repair smoke test
prompt; a closed input pipe fixed the wrapper. Its log remains
`.artifacts/full-validation-invalid-input-handle.log`. The next full run's new
installation suite exposed `.NET Process.CloseMainWindow()` selecting an unnamed
WPF window instead of the already inspected Notes window. The actual Notes
window closed normally through its own handle. Only the three installation test
helpers changed afterward: they verify the observed window's process ownership,
post a normal close, retain window-state diagnostics and distinguish posting,
timeout and application-exit failures. No product code changed for this harness
correction. The full run's failed installation result is retained, not relabeled
as passing.

Final targeted installation proof
`.artifacts/installation-proof/5273d56e457c4fe19fdf7766ace8510a` passed **80
delivery assertions and six native interaction checks** with the reviewed
installer sources. Its `output.txt`, `interaction.txt`, setup/removal logs and
`notes-window-state.txt` retain the evidence. The latter reproduces the helper
issue: the inspected example HWND differs from .NET's unnamed main-window
selection, and closing the inspected HWND exits normally. The interrupted-copy
case kills only its own setup process during payload copying, then verifies
retry, unchanged marker, working CLI, preserved authored files and removal.

Inspected native wizard captures cover welcome, destination, connections, ready,
running-work refusal, finish and removal confirmation. Start shortcut targets
were read through `IShellLinkW`; Windows itself resolved the quoted Notes handler
through `ShellExecuteEx`, preserving a literal filename containing Unicode,
brackets, apostrophe, ampersand, percent and semicolon. The installed runtimes
opened the unfinished note and retained its exact bytes without external runtime
or editor lookup. Finish opened the retained editable example in the installed
Notes process, which closed normally.

The finish checkbox caption is visible in the earlier complete capture under
`.artifacts/installation-proof/0c746abc1cbd42c7814df8b94acf54a4`; the final
proof's immediate finish-page capture omits that caption. That final image alone
does not establish its rendering, despite the successfully exercised handoff.

The WPF client area is black in `PrintWindow` captures from the inactive test
desktop; these images do **not** prove the installed editor's visual rendering.
The native control render `.artifacts/installation-authoring-proof/notes-1180.png`
was separately inspected: it shows the title, source, folded reasoning and
inspector with readable controls. That is control-render evidence, distinct from
the verified packaged window/filename/process handoff and from human usability.

The production workspace executable and companion hash are at
`.artifacts/installer/DERT-Setup.exe` and `.artifacts/installer/DERT-Setup.exe.sha256`,
copied from the fresh source build. SHA-256:
`349ECF22B36A0FF42DF7BC484B7ED6E0C33D13BF0395AE7BEC82D4052AD9FE5C`.
The pinned compiler download's Authenticode signature was valid for Pyrsys B.V.;
the resulting DERT package is unsigned. No release label was created.

Tests used the existing developer account with a **non-elevated token**. It is
an administrator account with its administrator group disabled in that token,
not a fresh standard account. Windows Sandbox is not installed here. The TODO
therefore remains open for a clean standard-account first launch, downloaded-file
trust prompts, user default-app selection, first Save, second visit, and human
keyboard/screen-reader inspection. Those results cannot be inferred from this
account, an isolated desktop or self-contained runtime checks.

Failed disposable installations were removed through their exact registered
uninstallers; GUID-specific test connections were cleared. The final cleanup
verified no test state keys, Start groups or Notes processes remained, and the
actual user PATH value and type were unchanged. Logs and the interrupted-copy
evidence remain. `.artifacts/installation-cleanup.txt` records that cleanup.
