# ERPDERT MER

An Ermahgerd language, a Windows CLI, and an unusually attentive place to put
your words. DERT is being designed so a person can enjoy technically expressive
notes, structured context, and supported operations with a small, memorable
surface vocabulary.

`;` is the **hook**. `;;` is the **line**. String elements are **sinkers**.
`//` changes its role with context: ordinary notes, structured reasoning,
annotations, and explicit note boundaries share the same familiar marker.

The visible absurdity deserves substantial engineering. Syntax highlighting,
linting, formatting, folding, explanations, and command results should make the
person's own work feel coherent and impressive. Someone who only wants fancy
notes is already a complete user. Education is incidental.

## Current state

The language workbench works: a source-preserving lexer/parser and a Windows
CLI for inspecting, linting, and rehearsing reversible title-rule and note-boundary
repairs. It recognizes the original example without
rewriting its syntax, associates each reasoning entry with its own explanation,
and supplies semantic colors, folds, source locations, and English diagnostics.

`ERPDERT write` opens the bundled Windows notes window. Type a title, press
Enter, and its matching rule follows as an undoable buffer edit. The same parser
drives live source colors, reasoning folds and attached explanations. Save and
reopen keep your exact UTF-8 source; no external editor needs to be installed.

The first operational command also works: `ERPDERT codex` discovers Windows
PATH command routes, groups launchers that reference the same package, and
shows local version, Node, and adjacent npm evidence without running them.

`ERPDERT run my-tools.DERT` connects the language to that operation: a
`STERT ; ERPDERT ;; codex` request drives discovery, with its written context
and a result attributed to the source line. `--preview` explains the request
without searching. A runnable example ships with the executable.

`ERPDERT codex updateme` now rehearses an npm-managed update: select the copy,
confirm its manager's inventory, check the latest release, and inspect npm's
dependency dry run. The same operation is available inside DERT.

`ERPDERT mer` now keeps the authored source in an ASCII document window, with
line numbers, a short animated edge trace, and an explanation attached to the
selected line. The complete check log opens with `details` or `--details`.
From a supported DERT request, `probe` connects that line to actual local Codex
launchers, package metadata, Node and adjacent npm evidence. `copy <number>`
compares installations from the same recorded inspection.
Notes have their own navigation: `find <text>` searches captured source,
`hit N` selects a result, `owner` follows the actual parent, and `structure`
reveals the selected fields, entries and ending in that same document window.
`ERPDERT demo` provides five selectable offline cases, holds the result for
review, and opens the complete recorded detail on request. Interactive npm
rehearsals use the same engine's operation view for real checks.

This is not a complete language release. It does not yet
execute package-changing DERT operations, manage packages, install itself, or
provide an editor extension or the full planned note application. The current
authoring window is a single-document Windows proof. No canonical v1.0 grammar is claimed.

Windows and PowerShell are first. The planned initial operational use is an opinionated
search, update, and doctoring CLI that identifies supported installations and
their actual managers. DERT supplies its configuration, contextual notes, and
operational proof. The extension, dedicated note app, and eventual broader
document format are part of the longer design horizon.

## Try the working build

The [active TODO and product assessment](TODO.md) records the broader UDL,
authoring and integration work, including what remains beyond the current CLI.

From this workspace in PowerShell, after publishing:

```powershell
.\.artifacts\win-x64\ERPDERT.exe -help
.\.artifacts\win-x64\ERPDERT.exe write
.\.artifacts\win-x64\ERPDERT.exe demo
.\.artifacts\win-x64\ERPDERT.exe mer
.\.artifacts\win-x64\ERPDERT.exe codex
.\.artifacts\win-x64\ERPDERT.exe run .\.artifacts\win-x64\examples\my-tools.DERT
.\.artifacts\win-x64\ERPDERT.exe inspect .\RERDMERR.DERT --at 37
.\.artifacts\win-x64\ERPDERT.exe lint .\RERDMERR.DERT
```

Line 37 is the original `fixme` entry. The connection view shows its value,
explanation, owning `support doctoring ; conflicts` note, and closing boundary.
The line-number gutter belongs to the viewer; it is not added to DERT source.

Omit `--at` to see reasoning folded into handles. `--expand` reveals the source
for inspection, and `--plain` removes styling. These commands never modify files.
The original corpus currently produces a title-underline warning and an
unfinished-list notice; it is deliberately left untouched.

### Write and return to your notes

Double-click **`Try Notes.cmd`** in the published folder, or open a file:

```powershell
.\.artifacts\win-x64\ERPDERT.exe write 'C:\My notes\weekend.DERT'
```

Start a new title with `// MY TITLE` and press Enter. Renaming an aligned title
adjusts its rule when you leave the title line; Enter advances past that rule.
Undo reverses the automatic edit without immediately reinserting it. Deleting
the rule deliberately leaves it deleted. Opening a mismatched title offers
**Align title…**, an exact preview with Apply and Leave. Unicode words are kept;
automatic rule width currently supports ASCII titles and explains that limit.

Reasoning opens from its gutter handle or **Ctrl+Space**. Its explanation and
owned entries stay available beside the source; narrow windows place that
context below it. Active sinkers stay expanded. Colors and folding never write
decorations or indentation into the document, and written operations are text
inside this window.

For an expanded list, select its header or an active item. **Add another item…**
lets you enter the actual words and optional `//` annotation, then previews their
new source line. **Finish this list…** previews removal of only the last
continuation comma. Apply changes your buffer as one undoable edit. Repeated
additions show any needed comma on the previous item; following prose cannot be
silently absorbed. Commas in annotations and reasoning are preserved.

**Ctrl+Enter** opens Add; **Ctrl+Shift+Enter** opens Finish. Source changes withdraw
a pending proposal while retaining your typed item for **Review again**. **Leave**
discards the proposal. Save requires applying or leaving a typed proposal first;
departure asks before discarding those words. Unfinished quoted text is preserved
and explained instead of treating its comma as a list boundary.

Lint and MER's detailed findings offer a copyable `write` command for DERT3003.
It opens the **current file**, which may differ from the analysis, and does not
apply a choice. Repair commands retain their separate source-byte checks.

Type `fin.` to close a `STERT` chain: Notes automatically displays `ƒin.`.
The source still contains `fin.`; Copy and Save keep that typed spelling.
Select the ending's line to see its opening line and **Show typed spelling**.
That view choice applies to this window and never changes your words.

**Write ƒ in source…** offers the exact one-character edit if you want the florin
in the saved file too. **Apply** changes the buffer; **Ctrl+Z** restores `f`;
**Ctrl+S** saves. Notes, quotes, paths, URLs and standalone fragments are left
alone. Existing `ƒin.` is already literal source. CLI/MER source excerpts retain
the source spelling; this automatic appearance belongs to the Notes editor.

**Ctrl+S** saves; **Ctrl+Shift+S** creates a new copy; **Ctrl+Z / Ctrl+Y** undo and
redo buffer edits. New, Open and Close offer Save, Discard or Cancel when needed.
`--still`, `ERPDERT_MOTION=off`, and Windows reduced-motion settings suppress the
short accent that follows an actual title alignment, accepted list edit or
accepted florin source edit. Displaying the florin alone adds no animation or edit.

Existing-file saves check the opened file's identity and original bytes, preserve
its BOM and literal line endings, keep verified previous bytes in the adjacent
`.erpdert/drafts` folder, and verify the new bytes. External edits refuse the save
and retain your buffer. **Save as** only creates a new file; choose a new name if
one already exists. Save failure keeps the buffer and explains any recovery copy.
These recovery files are unencrypted and retained until you remove them. Buffer
undo lasts for the current session; reopening starts a new undo history. Saving
is limited to regular local Windows files; it is not a power-loss-atomic save or
a cross-session editing history.

Keep the published `notes` folder beside `ERPDERT.exe`. The authoring host ships
its own desktop runtime and editor dependency with its license. A normal-user
installer, file associations, and clean-account validation remain TODO work.

### Try it, pause it, judge it

Double-click **`Try MER.cmd`** inside the published `win-x64` folder, or run:

```powershell
.\.artifacts\win-x64\ERPDERT.exe demo
```

Press Enter to start, or choose a case by number: title alignment, contextual
ownership, unfinished notes, Unicode text, or a check that cannot finish.
Each case says what to look for and replays actual local analysis. The failure
case deliberately supplies invalid UTF-8 bytes; it must show a failed check.
Nothing is installed, written, downloaded, or read from your own notes.

After a case, the focused result stays on screen. `details` opens the complete
recorded source and checks, with the original timestamp and no second execution.
Structural errors take priority over cosmetic title previews.

Context cases list commands such as `at 5`, `at 6`, and `at 7`. Type one to
inspect that recorded entry, with nearby entry names, its owner, explanation,
and closing boundary. This does not replay the case, read the source again,
or add a second review. An unavailable line leaves the record untouched.

`prev` and `next` move between entries without wrapping at the ends. `source`
switches the selected connection to its original source excerpt and back. The
opener, selected line, and known closer keep their actual punctuation and line
numbers; omitted lines are marked. Long source lines wrap at words without
discarding spaces. Control characters appear as explained escapes, not terminal
commands. This is a displayed excerpt, not a byte-for-byte copy/export command.

`still` and `plain` compare the current selection without replaying or rereading
the source. `repeat` plays the same recorded analysis again and returns directly
to your selected view. `step` pauses at evidence and results: Enter advances, `back`
revisits the previous step, and `next` goes to the case review. These controls
use ordinary typed lines, not a full-screen interface.

Use `keep` or `change <your note>` to record your judgment, or Enter to leave it
unreviewed. `q` ends the demo and leaves a copyable review card with the cases,
view modes and comparisons, starting terminal sizes, results, and your notes. Unseen cases are
never marked approved. The card is not saved or uploaded; copy it from scrollback.

For a focused review or a noninteractive transcript:

```powershell
ERPDERT demo --case context --step
ERPDERT demo --all --plain
```

The latter needs no input. Redirected demos also run without prompts unless
`--review` or `--step` is explicitly requested. Expected attention/failure cases
do not make the demo command fail; an unexpected case result does. Use `--help`
for the available names and options.

### Watch MER move on your own notes

```powershell
ERPDERT mer
ERPDERT mer .\my-notes.DERT
ERPDERT mer .\my-notes.DERT --replay
ERPDERT mer .\my-notes.DERT --still
ERPDERT mer .\my-notes.DERT --details
```

The bare command analyzes the shipped `examples/mer.DERT`, then deliberately
paces its recorded events so the connections can be watched. It is labeled
**Replay** throughout. Supplying your own file performs live local analysis
without artificial delays; `--replay` lets you watch that analysis afterward.
Analysis does not edit the source or execute operations written inside it.
An explicit `probe` during review performs the supported local discovery described
below; it starts no programs and makes no network request.

The source has a file tab, an ASCII border, and a separate line-number gutter.
Blank lines remain visible; wrapped source continues without a second line
number. A hook in the margin selects the line being explained. In a wide terminal
the explanation connects to that line outside the document; in a narrow terminal
it sits below the window. The footer names the visible source range and labels
excerpts. Failed checks and structural errors take precedence over a title preview.

A short trace moves along the document edge when a new relationship or
measurement is observed. The source characters stay unchanged. The trace settles
without postponing completion, and the document remains visible when analysis ends.
The default result does not expand into the full check log.

At the terminal prompt, `at 10` inspects recorded line 10, `details` prints the
complete source and evidence, `back` returns to the default focus, and Enter
finishes. `next` and `prev` follow the document's real findings, contextual notes,
and supported requests, with the next destination named in the prompt. The margin
connects a selected entry to its real owner and closing `//`; title previews
emphasize the exact marks added or removed. `repair`, when offered, opens the
selected repair in this same window; `repair title` reaches the title rule from
another line. An unfinished note offers `close <line>` or `close eof`, with the
number of entries kept and released. Choosing an ending opens a proposal and
shows the resulting ownership; it never guesses a boundary.

`before` and `after` compare captured source with the exact proposal. **`apply`
saves that displayed edit**, using the existing guarded writer and verified local
undo copy. `cancel` leaves the preview; Enter exits without applying it. The
window labels proposals as **not saved**, then displays **bytes verified** only
after a successful write and read-back. Fresh analysis retains remaining findings
and clears installation inspections attached to the previous source revision.
`undo` opens the saved original for the same comparison and explicit `apply`.
The saved copies are complete, unencrypted files beside the document under
`.erpdert/repairs`; `details` gives their location. Later external edits block
application and undo. Replay, including the bare bundled demonstration, remains
read-only. Open an actual file without `--replay` to save an approved repair.

Copied PowerShell previews in `details` check the exact bytes MER captured;
changed source requires a new check. They remain preview-only handoffs.
Use `--details` to print the complete record and repair commands directly,
including when redirecting output. Unsupported repairs explain their limits.

To follow the bundled note into the installation on this PC:

```text
next
probe
details
```

`next` selects its `STERT ; ERPDERT ;; codex` request. `probe` inspects local
external command routes and attaches their package/dependency evidence to that
line. If several installations are found, `copy 2` views the second captured
copy. Dependencies are attributed to their launcher; nearby npm does not prove
installation ownership. Incomplete searches and unidentified routes stay explicit.
Full detail retains exact paths and all inspection issues. The inspection has
its own timestamp, separate from the source-analysis replay.

For a written `commands ; updateme ;;` request, select its line and use:

```text
rehearse
copy 1
check
details
```

`rehearse` finds local copies without starting npm. A single unambiguous copy is
selected automatically; `copy N` chooses among multiple installations. The view
explains the effects before `check` starts that copy's npm inventory, configured
registry query and dependency dry run. These queries may write npm cache/logs.
The source stays visible as actual results connect its request to the installed
version, confirmed target folder, registry release and proposed dependency changes.
Current and ahead versions explain why no install dry run is needed. `details`
retains every check and the separately labeled manual update command. No package
update is applied inside MER; `apply` remains a source-repair control.

The file must still match the captured bytes before a new probe or rehearsal
starts. Source checks between npm calls and after the result reject intervening
edits. Repairs discard prior installation choices and update results. Repeating
`check` queries again; selecting a previously checked copy shows its timestamped
record. Failed or interrupted checks cannot leave an older ready result in view.
Replay cannot start npm queries. Notes that merely mention Codex never acquire
operational meaning, and an ineligible plan blocks external checks.
`--review` explicitly accepts these commands, including `apply`, from redirected input. Without it,
redirected output never reads review commands. `--details` and `--review` are
alternative output modes.

This is inline CLI output, not a full-screen application. The custom
`Erpdert.Mer` library owns its cell layout, motion, row-difference rendering,
source focus, and final receipt. It has no UI-framework package dependency.
The existing CLI still uses Spectre.Console for other reports and host
capabilities; those surfaces have not all been migrated.

For a complete notes-only example:

```powershell
.\.artifacts\win-x64\ERPDERT.exe mer .\.artifacts\win-x64\examples\weekend.DERT
```

Type `find quiet`, `hit 2`, then `owner`. Search includes original values,
annotations and prose; punctuation is literal and case is ignored. `hits` lists
the first eight matching lines; `hits 2` turns the page. Each hit retains its
source line, and multiple occurrences on one line share a destination. Failed
searches and invalid choices leave the current selection and search intact.

`structure` reveals the current line's head, hook, line, entries and explicit
ending. It can also inspect a line currently showing an operation or diagnostic;
the analysis status still retains any findings. `owner` follows the parsed
parent, or returns from a closing marker to its opener. Names written in a
target field are shown as names, without inventing an operation binding.
`details` includes the full selected structure. None of these controls runs
programs or edits source. A saved repair/undo rebuilds the index and clears old
hit numbers; external edits require reopening MER for a fresh captured view.

Resize the terminal to switch between connected side-by-side and stacked
layouts. Very short or narrow windows show the status and explain how to obtain
the full record. `--still` keeps the document window without timer animation;
`--plain` removes color too. Redirected output and `TERM=dumb` do not animate. Set
`ERPDERT_MOTION=off` to disable motion across these surfaces; `NO_COLOR` controls
color separately. Ctrl+C returns `130`. Stopping a replay preserves its already
completed analysis, including findings not yet shown; the receipt explicitly
says playback stopped. Stopping a running operation instead retains the observed
partial result and marks unfinished work. Both require available output.

`codex updateme` and its DERT equivalent now publish real npm checks into MER
in an interactive terminal. There is no deliberate pacing on those operations.
`--still` is also available on `codex updateme` and `run`.

The engine is Windows-first. It handles common combining, emoji, and CJK
clusters without splitting them at a clip boundary; exact glyph width still
depends on the terminal and font. Universal terminal/font conformance is not
claimed. Full explanations and source survive in the final receipt even when
the live viewport can show only a focused excerpt.

### Let your DERT request an operation

Save this as `my-tools.DERT`:

```dert
STERT ; ERPDERT ;; codex
// why ; codex ;; Find the installation my terminal can see. //
ƒin.
```

Run `ERPDERT run my-tools.DERT`. The result comes from the same discovery
engine as `ERPDERT codex`, with the requesting source and its notes shown first.
The one-line form without `ƒin.` works too. Neither form launches Codex itself.

Add `--preview` to see the interpretation without searching, then use the
complete run command printed beneath it. `--plain` follows that command.
Formatting warnings remain visible without stopping discovery. Unsupported
operations and incomplete structure are cited before dispatch; the runner
does not quietly execute only the recognized part of a mixed request.
Multiple supported chains run in source order with separately attributed results.
A notes-only file has nothing to run and is left alone.

These are the runner's current implemented capabilities, not the limits of
DERT's evolving design. No source is rewritten and no package changes are made.

### Rehearse the update for this copy

Run `ERPDERT codex updateme`, or use the shipped `examples/update-my-tools.DERT`:

```dert
STERT ; ERPDERT ;; codex
// why ; codex ;; Check this copy before changing anything. //
commands ; updateme ;;
ƒin.
```

The rehearsal uses the identified Node and npm entrypoint, not whichever `npm`
another PATH search happens to find. It checks npm's global folder and detailed
inventory against the discovered package path/version, resolves `latest`, then
runs a version-pinned dependency dry run with engine checks, optional dependencies,
and package scripts disabled. npm's proposed additions/removals/changes remain visible.

Multiple copies or an incomplete PATH search produce complete `--path` choice
commands before any npm query. An identified damaged copy retains its findings;
rehearsing an update does not certify its health. Already-current copies do not
receive an installation command, and an older `latest` tag never proposes a downgrade.
Unrecognized release channels, unavailable managers, failed queries, unexpected
output, and changed source installations are explained instead of guessed.

When ready, **MANUAL UPDATE / CHANGES PACKAGES** contains the exact absolute
Node/npm invocation, selected prefix, and pinned package version. ERPDERT does
not execute it. Running it yourself can change packages and run package scripts;
it is not an ERPDERT approval, snapshot guard, or reversible repair. Rehearse again
if the installation or environment changes. Integrated update approval/execution
and verified post-update results remain future work.

Unlike discovery, a rehearsal launches npm and contacts its configured registry;
npm may write cache/logs. It does not launch Codex, change packages, or rewrite notes.
`run ... --preview` remains interpretation-only: no npm or network queries.
Stable three-part versions and npm's legacy-object/current singleton-array registry
responses are supported. Historical installer ownership is not claimed.

Exit codes for `updateme`: `0` = ready rehearsal or already current; `1` = a choice,
unavailable check, ahead-of-latest version, or selected-copy finding needs attention;
`2` = unsupported usage/platform or unavailable output; `130` = interrupted.
A selected copy can be checked successfully while
unrelated PATH warnings remain visible.

The adapter follows [npm inventory](https://docs.npmjs.com/cli/v11/commands/npm-ls/),
[dry-run behavior](https://docs.npmjs.com/cli/v11/commands/npm-install/#dry-run), and
[npm 12's JSON change](https://github.com/npm/cli/blob/latest/CHANGELOG.md).

### Which Codex?

Run `ERPDERT codex` (or the executable path above). No document is required.
The first PowerShell PATH match stays first even when a later package is newer
or easier to identify. Three npm launchers referencing one package are shown
together, not diagnosed as three conflicting installs. Competing groups,
missing entrypoints or Node, unreadable folders, and unidentified launchers
receive plain-English findings with their actual paths.

The npm-style adapter reads launcher references and package metadata. An adjacent
npm launcher is supporting evidence, not proof of who installed Codex. A package's
development `packageManager` field does not choose the user's updater. Unknown
ownership remains unknown; no install, update, repair, or discovered program runs.

This searches local PATH folders, not the whole computer. Shell aliases/functions,
execution policy, native payloads, and runtime compatibility are not validated.
The report includes a read-only command to inspect overrides in your own PowerShell
session. Relative, quoted, inaccessible, and network PATH entries are explained
instead of silently reinterpreted. `--plain` preserves all information without color.
Exit codes: `0` = one group with no inspection findings; `1` = missing, competing,
unknown, broken, or incomplete discovery; `2` = unsupported usage/platform.
Neither `0` nor a file being found certifies that Codex can launch.

The adapter was grounded in [npm's Windows layout](https://docs.npmjs.com/cli/v11/configuring-npm/folders/),
[PowerShell's external-command lookup](https://github.com/PowerShell/PowerShell/blob/master/src/System.Management.Automation/engine/CommandSearcher.cs),
and [Codex's package launcher](https://github.com/openai/codex/blob/main/codex-cli/bin/codex.js).
`tests/Invoke-DiscoverySmoke.ps1` compares the published program with actual
PowerShell discovery in isolated fixture environments, without changing your PATH.

### Go from a finding to its rehearsal

Run `lint` on your file. Each finding appears with its source line and either
a supported action or a specific explanation of what cannot yet be repaired.
There is no command to reconstruct or rule number to look up:

- **REHEARSE / TITLE RULE** opens the exact underline edit when its width is supported.
- **CHOOSE / NOTE BOUNDARY** opens the unfinished block's entries and possible endings.
- **NO REPAIR OFFERED** explains unsupported inline closure, list completion, or title width.

Copy the complete PowerShell command printed under the finding. It uses this
executable's path, the file's absolute path, and a `--from-lint` source revision.
You do not need to type that revision. It is **not an approval token**: the link
checks the linted bytes and only opens a preview, without any write prompt.
If the file changed, it asks you to lint again instead of using an old line number.

For note boundaries, subsequent copied choices retain the source check and your
`--plain` preference until the selected preview. Only that exact edit's separate
approval command can write. The ordinary interactive `repair` commands below
remain available. Lint never creates a recovery copy or changes source.

After an approved repair or undo, **VERIFIED RESULT** shows findings in the bytes
actually saved and checked. Each supported finding has a fresh rehearsal command
at its new line number. Unsupported repairs stay explained; completing one edit
never claims to fix everything. Following a result command only opens a preview,
and plain mode stays with copied approvals, undo, and subsequent actions.

### Rehearse a title repair

Use a note of your own, or a copy of the corpus:

```powershell
.\.artifacts\win-x64\ERPDERT.exe repair '.\my notes.DERT'
.\.artifacts\win-x64\ERPDERT.exe undo '.\my notes.DERT'
```

The rehearsal shows the actual title, an exact `−`/`+` rule diff, and an English
explanation. Press Enter to leave the file alone; choose `y` to apply. Undo has
its own preview and confirmation. Following prose, note ownership, punctuation,
line endings, and any UTF-8 BOM stay intact.

Redirected output previews only. It prints a complete PowerShell command with
`--apply <approval-token>` bound to that file and that exact edit. It uses the
launched executable's path; installing ERPDERT on `PATH` is not required.
Changed bytes, a replacement file, or a different undo selection invalidate it.

Before editing, ERPDERT saves and verifies a full original under the sibling
`.erpdert/repairs` folder. These are **unencrypted local copies** of your notes;
keep them private and retain them for recovery. Writes are recoverable, not
power-loss atomic. Undo refuses to overwrite later edits. Copying or replacing
the file can disconnect automatic undo; the saved originals remain readable.

This increment measures printable ASCII titles in strict UTF-8 documents and
applies only to regular local Windows files. Unsupported title widths are
explained, never guessed. Title repair does not change commas, targets, or note closures.
See the [repair contract](docs/superpowers/specs/2026-09-06-title-repair-design.md).

### Choose where an unfinished note ends

Point at the opener or an entry of an unfinished reasoning block:

```powershell
.\.artifacts\win-x64\ERPDERT.exe repair '.\my notes.DERT' --at 12
```

The CLI shows the current recovered grouping and offers source-line boundaries.
Choose one to see the exact `//` insertion, every entry that stays inside, and
the actual new role and owner of each released entry. Press Enter to leave.
Selection opens the rehearsal; a separate `y` approves the write.

For a boundary offered by that report, you can go directly to its rehearsal:

```powershell
.\.artifacts\win-x64\ERPDERT.exe repair '.\my notes.DERT' --at 12 --close-before 16
```

Use `eof` instead of a line number only when offered. Redirected choice reports
print selection commands; only a concrete edit preview prints an approval token.
No boundary is automatically preferred. The same `undo` command restores the
latest title or note repair, including the original unfinished state.

The edit adds one standalone `//` and its required line ending. Words, commas,
BOM, existing newlines, and final-newline style are preserved. Ownership changes
are explicitly shown; this is not described as decoration-only. Unicode note
text is supported. Inline-note closure and unfinished active lists are not
repaired by this command, and no parser grammar was changed to implement it.
See the [note-boundary contract](docs/superpowers/specs/2026-09-06-note-boundary-design.md).

The published `win-x64` folder includes its .NET runtime. Keep the **whole folder**
together, not just the executable. This workspace build is not a signed installer
or a public download, and clean-machine installation remains untested.

### Build and verify

Development uses the .NET 10 SDK pinned in `global.json`; the published app
includes its .NET runtime. Notes and discovery need no Node/npm runtime of their
own; npm update rehearsals use the selected tool installation's Node/npm.
Run these commands from the workspace root:

```powershell
dotnet test ERPDERT.slnx
dotnet build ERPDERT.slnx -c Release
.\src\Publish-Windows.ps1
.\tests\Invoke-PublishedSmoke.ps1
.\tests\Invoke-RepairSmoke.ps1
.\tests\Invoke-NoteRepairSmoke.ps1
.\tests\Invoke-LintHandoffSmoke.ps1
.\tests\Invoke-RepairFollowthroughSmoke.ps1
.\tests\Invoke-DiscoverySmoke.ps1
.\tests\Invoke-DertRunSmoke.ps1
.\tests\Invoke-UpdateSmoke.ps1
.\tests\Invoke-MerSmoke.ps1
.\tests\Invoke-NotesSmoke.ps1
.\tests\Invoke-SourceOnlySmoke.ps1
```

The executable checks use standalone DERT test inputs under `tests/Fixtures`,
verify ownership, exercise damaged input and error exits, and check that inspected
inputs stay unchanged. They do not read or validate the project's documentation.
Generated evidence stays in `.artifacts/proof`.
The repair smoke suite applies and undoes edits on generated copies, executes the
printed PowerShell commands, and checks exact reversal and stale-approval refusal.
Its evidence and recovery copies stay in `.artifacts/repair-proof`.
The note-boundary suite checks reasoning-note choices, copied selection/apply/
undo commands, actual released ownership, refusal paths, and Unicode/BOM/EOF
reversal. Its evidence stays in `.artifacts/note-proof`.
The lint-handoff suite executes copied links from a different directory, checks
source-bound note choices and unsupported findings, preserves unusual filenames,
rejects stale links, and verifies separate approval/undo. Its evidence stays in
`.artifacts/lint-proof`.
The repair-followthrough suite checks copied actions after title repair, note
closure, and undo, including shifted lines and stale links. Evidence stays in
`.artifacts/followthrough-proof`.
The update suite uses a separate test-only executable to exercise real child-process
dispatch and copied commands; that fixture refuses installation. A separate opt-in
`tests/Invoke-LiveNpmRehearsal.ps1 -NodePrefix <existing-Node-folder>` checks genuine
npm against a disposable copied installation and public-registry metadata. It never
invokes the manual update and retains its cache and evidence under `.artifacts`.
Add `-Mer` to exercise the source-window review commands through that same real
npm rehearsal and verify installation file hashes afterward.
The MER suite checks the shipped example, actual user input, damaged input,
unchanged UTF-8/BOM/CRLF bytes, reduced-motion receipts, all five offline demo
cases, held-result/detail review, step input, and the packaged launcher without
PATH dependencies. The engine tests
also cover row ownership, resize races, grapheme clipping, event integrity,
interruption, and broken output. `tests/Export-MerVisualProof.ps1` captures
actual compositor frames and an ANSI recording under `.artifacts/mer-visual`;
these are rendered engine frames, not screenshots of an OS terminal. Add
`-DocumentWindow` to capture the current `mer` source window, optionally with
`-AtLine 10`. `-DemoCase title` or `-DemoCase context` selects a demo input.
`-DocumentWindow -AtLine 4 -ProbeLocal` records the bundled request's actual local
discovery and renders its attached evidence; `local-inspection.txt` retains the
full report beside the frame. `-InspectionPath` can select controlled test routes.
`-RepairJourney title` or `-RepairJourney note` exercises comparison, guarded save,
and undo on a harness-owned file, renders every state, and checks exact restoration.
Use `-BuildFolder .artifacts/win-x64` to inspect the published build. The generated
`repair-review.txt` retains its actual review output beside the frames.
`-NotesJourney` exercises literal search, hit selection, ownership and structure
against a disposable copy of the shipped weekend notes. It captures each actual
review frame and retains `notes-review.txt`, checking that the source stayed intact.
`-UpdatePrefix <test-Node-folder>` runs the source-window update review against a
chosen test installation and renders its copy-selection and checked-result states.
It invokes that copy's npm queries/dry run; use the process fixture or a disposable
installation. `update-review.txt` retains the actual review output.
The demo and standalone update views retain their own layouts.
Add `-AtLine 6` to capture a direct recorded-entry inspection.
Add `-SourceView` with `-AtLine` to render that entry's source excerpt.
The authoring tests exercise actual WPF editor controls on STA threads, including
text-input/Enter/Undo routing, caret and selection, folding, title correction and
save/reopen. Set `ERPDERT_AUTHORING_PROOF` to an output directory to capture wide
and narrow rendered WPF frames. These are control renders, not OS screenshots.
The published notes suite opens and closes the packaged window directly and
through `write`, with an empty PATH, disabled installed-runtime lookup, and
literal space/punctuation-containing paths. It checks unchanged source bytes
across reopening. It does not automate clipboard contents or Windows file dialogs.
The source-only check builds, tests, publishes, and runs all ten suites in a fresh
copy containing only code, build settings, and tests. No reference documents or
old build outputs are copied; evidence stays in `.artifacts/source-only-proof`.
The [implementation record](docs/IMPLEMENTATION-STATUS.md) separates tested
behavior, provisional parser decisions, and unfinished product work.

## Read the sources and the current work

| Document | Role |
| --- | --- |
| [Original language notes](docs/archive/original.md) | Primary semantic source; intentionally scattered and unresolved |
| [Who we are](docs/WHO-WE-ARE.md) | The user's full product ambition and MIVIM philosophy |
| [RERDMERR.DERT](RERDMERR.DERT) | Original authoring/syntax corpus; includes conflicting examples |
| [Critical design review](docs/DESIGN-REVIEW.md) | What the previous summaries lost, what our own proposals invented, and source coverage |
| [Experience design](docs/EXPERIENCE-DESIGN.md) | Worked interactions, delivery choices, internal needs, and observable acceptance criteria |
| [Syntax lab](docs/SYNTAX-LAB.md) | Context/ownership model, original example dispositions, and probes for unresolved grammar |
| [Implementation status](docs/IMPLEMENTATION-STATUS.md) | Working commands, architecture, diagnostics, evidence, and limits |
| [Changelog](CHANGELOG.md) | Implemented increments, separate from design proposals |
| [Development instructions](AGENTS.md) | The concrete loop contributors and agents must use |

Latest explicit user decisions govern the source material. The current design
documents distinguish those decisions from development recommendations.

The [old overview](docs/archive/overview.md) and
[previous conversation README](docs/archive/2026-09-06-conversation-readme.md)
are preserved historical interpretations. Both contain unsupported claims.
The latter also preserves the user's correction to the educational framing.
Neither is a normative grammar.

## The experience being designed

The author types a title and watches its rule align. They inspect a pair of
slashes and see which entries and explanations belong together. A diagnostic
points to their actual words, explains the problem in English, and rehearses a
repair before committing it. A supported command can bring its observed result
back into the note that requested it.

Title authoring and source ownership now have working surfaces. Returning an
observed operation result to a note remains a working proposal in the experience
design. Each part connects appearance to source structure and actual effects;
its success needs imperfect input and repeated use in the delivery surface.

Technical presentation should feel rich and readable. Real checks can receive
ceremony. Errors need exact locations, clear consequences, and useful choices.
Waiting, uncertainty, partial completion, and verification must remain visible.
The "logical shield of embarrassment" means the person can act confidently
without a compulsory tutorial or an announcement that they are a beginner.

## The next proof

Recognition, ownership inspection, read-only linting, title-rule rehearsal, and
author-selected unfinished-note closure now have working implementations.
Supported lint findings and verified repair results now link directly to their
source-bound rehearsals, without write prompts or guessed repairs. Operational
development now has read-only Codex discovery driven from both CLI commands and
DERT requests. npm inventory verification and supported update rehearsal now
identify the manager route, exact target and proposed effect. The custom animated
MER engine now carries live source/evidence connections, responsive delivery,
and recorded receipts in the CLI. MER's source window also
carries comparison, explicit repair approval, verified saved results and undo,
sharing the existing guarded repair service. Written update requests connect to
copy selection, npm checks and a retained rehearsal in that same source window.
The native authoring window now brings title treatment, reasoning inspection,
deliberate list completion and the original florin goodbye to writing, with
guarded save/reopen. The next proposed UDL increment is a shared, visually checked
title-width policy for non-ASCII text. Remaining host
validation and wider operation workflows stay in [TODO](TODO.md).

Integrated update approval, execution and verified results remain operational
work ahead, followed by additional managers and doctoring actions; the manual
command is not a substitute for that full flow.

The broader Windows proof still includes an ambiguous edit, supported command
rehearsal and an optional result kept in the document.
The original cases remain in that proof; difficult punctuation and note nesting
are central.

Before declaring a canonical grammar, resolve the syntax lab's closure,
folding, quotation, attachment, and payload cases through explicit decisions
and fixtures. Extend the implementation against the required authoring and
Windows delivery behavior.

Novel features may require unusual implementation work. Preserve the intended
experience while investigating that work, and make every claim about quality
answerable to what the author can actually see and do.
