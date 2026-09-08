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

The first operational command also works: `ERPDERT codex` discovers Windows
PATH command routes, groups launchers that reference the same package, and
shows local version, Node, and adjacent npm evidence without running them.

`ERPDERT run my-tools.DERT` connects the language to that operation: a
`STERT ; ERPDERT ;; codex` request drives discovery, with its written context
and a result attributed to the source line. `--preview` explains the request
without searching. A runnable example ships with the executable.

This is not a complete language release. It does not yet
execute package-changing DERT operations, manage packages, install itself, or
provide an editor extension or note app. No canonical v1.0 grammar is claimed.

Windows and PowerShell are first. The planned initial operational use is an opinionated
search, update, and doctoring CLI that identifies supported installations and
their actual managers. DERT supplies its configuration, contextual notes, and
operational proof. The extension, dedicated note app, and eventual broader
document format are part of the longer design horizon.

## Try the working build

From this workspace in PowerShell, after publishing:

```powershell
.\.artifacts\win-x64\ERPDERT.exe -help
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

Development uses the .NET 10 SDK pinned in `global.json`; the published app does
not use Node or npm. Run these commands from the workspace root:

```powershell
dotnet test ERPDERT.slnx
dotnet build ERPDERT.slnx -c Release
dotnet publish src/Erpdert.Cli -c Release -r win-x64 --self-contained true -o .artifacts/win-x64
.\tests\Invoke-PublishedSmoke.ps1
.\tests\Invoke-RepairSmoke.ps1
.\tests\Invoke-NoteRepairSmoke.ps1
.\tests\Invoke-LintHandoffSmoke.ps1
.\tests\Invoke-RepairFollowthroughSmoke.ps1
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
The source-only check builds, tests, publishes, and runs all five suites in a fresh
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

These are working proposals developed in the experience design. They connect
appearance to source structure and real effects. Their success must be
demonstrated in a usable surface, including imperfect input and repeated use.

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
DERT requests. The next operational work is
installation ownership and a supported update rehearsal: identify the manager,
exact target, and intended effect before offering any execution.

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
