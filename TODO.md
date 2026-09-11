# DERT: the work still worth doing

Assessment, 2026-09-10. This is a working development order, not a new language
specification or an exhaustive limit on the product. The user has accepted the
current MER balance of source window, restrained ASCII composition and theme.
Preserve that reference while developing the UDL's usefulness beyond operations.

Authority: [original language notes](docs/archive/original.md),
[product ambition](docs/WHO-WE-ARE.md), [repository standards](AGENTS.md), and the
latest user directions. The [syntax lab](docs/SYNTAX-LAB.md) and
[experience design](docs/EXPERIENCE-DESIGN.md) contain working recommendations;
their proposals are not promoted here to user decisions.

## Assessment of the current gap

The engine, source model and guarded repairs now support considerably more than
the author's entry experience exposes. Package discovery and update rehearsal
have received deep integrations. The preceding assessment identified a missing
authoring experience: notes-only users needed an external editor and had no live
typing or first-save flow. The native authoring increment below addresses that
gap without making package operations a requirement for useful notes.

This pass adds literal search, source ownership navigation and field/entry
inspection for ordinary statements, sinkers, free notes and reasoning. It also
separates notes-only viewing from the runner's unsupported-operation findings.
These are navigation and interpretation tools for captured source, not editing.

## Implemented: source navigation

- [x] One source index for exact parsed locations, parents, boundary lines and
  literal search spans; used by MER and command-line source selection.
- [x] `find`, paged `hits`, `hit N`, `owner` and `structure` in MER review.
  Entire explanations stay available in `details`; search never executes text.
- [x] Attached header/hook/line, expanded entries and actual ending information,
  with a compact narrow view. Missing boundaries and unrelated interleaved
  notes cannot acquire a misleading closed ownership trace.
- [x] Search hit numbers are discarded after saved repair/undo; the index is
  rebuilt from verified bytes. Pending comparisons block navigation.
- [x] A packaged `weekend.DERT` that needs no operational chain or package manager.

See [the interaction and evidence record](docs/superpowers/specs/2026-09-09-udl-connections-design.md).

## Implemented next: native notes authoring

- [x] Compared embedded WPF/AvalonEdit, native RichEdit and installed-editor
  integration against actual typing, folding and distribution needs. The bundled
  Windows host opens with `DERT write [file]` or `Try Notes.cmd`.
- [x] New-title Enter, rename, paste-boundary treatment, deliberate rule deletion,
  imported-title preview and Undo/Redo use the actual editor buffer and preserve
  caret/selection. Renamed titles retain one rule; unrelated Undo does not disable
  future title treatment. The Unicode increment below extends title width.
- [x] Live parser colors, reasoning folded on load, keyboard unfolding/editing,
  owned-entry navigation and narrow-window context. Active sinkers remain expanded;
  literal source remains available to selection and saving.
- [x] Save, Save as, reopen and second-session editing; original-byte/identity
  checks, verified recovery copies, write verification and failure retention.
  No external editor installation is assumed.

See [the host decision and evidence](docs/superpowers/specs/2026-09-09-notes-authoring-design.md).
This completes the bounded title/free-note/reasoning host increment. It does not
complete the full note application, clean-account installation, clipboard/IME
coverage or a general formatter. The temporary inspection increment below adds
automatic return to the reasoning's folded resting view.

## Next: make authorship feel complete

The deliberate list-completion increment is implemented: select an expanded
list's header or active item to add actual words or finish its final continuation
comma. Both choices preview the exact source before Apply, use one native undo
step, and share guarded Save/reopen. New-item drafts survive source changes and
need Apply or explicit Leave before saving. Lint and MER offer a clearly labeled
current-file authoring command. Notes, prose and unfinished quoted text cannot
silently become list repairs. See [the design and evidence](docs/superpowers/specs/2026-09-09-list-completion-design.md).

| Priority | Person's action and intended payoff | Existing support and remaining work | Evidence needed before calling it delivered |
| --- | --- | --- | --- |
| 1 — bounded increment delivered | Type a title and immediately enjoy its matching rule, then keep writing | Native host, title behavior and guarded save/reopen now work. Continue real clipboard/IME, dialog and clean-account validation. | Actual control/input tests and rendered wide/narrow views exist. OS clipboard, input-method composition and clean-user first launch remain unproven. |
| 2 — bounded inspection and return delivered | Open a reasoning handle and follow an explanation without losing the note | Gutter, Inspect and Ctrl+Space expose a connected reading view; entry actions edit exact source. Return/Escape restores anchored selection and viewport, retaining edits. Navigation away refolds reasoning while active selections remain visible. | Native controls cover long-note repeated use, reverse/rectangular selections, deleted adjacent owners, missing boundaries, pending proposals and Save/reopen. Wide/narrow rendered views inspected. Human long-note usability and real clipboard/IME remain unproven. |
| 3 — delivered in Notes | Finish an unfinished list deliberately | Separate Add another item and Finish this list choices now preview literal words, required punctuation and the resulting ending. Undo and guarded Save/reopen are integrated. | Core and native host cases cover annotations, blank lines, headers, quoted punctuation, stale drafts, repeated additions and following prose. Unfinished quotes are refused; no unattended CLI list repair is claimed. |
| 4 — delivered in Notes | Get the florin goodbye without rewriting unrelated words | Recognized chain endings display `ƒin.` automatically, with literal source retained for Copy and Save. Context exposes typed spelling and an optional exact one-character source-edit preview with native Undo. | 18 core and 12 native cases cover exclusions, actual glyphs, native copy data, typing/deletion, selection direction, stale previews, list drafts and Save/reopen. Wide/narrow controls inspected; highlighting creates no source edit. |
| 5 — bounded increment delivered | Give a title its own language without a crooked rule | Shared Unicode width data connects parser, repair and MER. Notes fits each authored dash to actual title glyph width; incorrect counts remain visibly short or long. Source spelling, normalization, Undo and guarded Save remain intact. | Core and native cases cover combining marks, CJK, RTL, joined emoji, flags, font changes, partial selection and exact UTF-8 reopen. Wide/narrow WPF frames inspected. Tabs/controls and incomplete characters explain unavailable width; wrapped titles retain their rule. Cross-terminal fonts and IME remain unproven. |

The florin [interaction and evidence](docs/superpowers/specs/2026-09-09-florin-goodbye-design.md)
records its source-preserving appearance and separate author-selected edit.
CLI/MER excerpts remain literal. Copy data was checked through the native copy
command without altering the OS clipboard; a cross-application clipboard round
trip and IME remain unproven.

The Unicode [interaction and evidence](docs/superpowers/specs/2026-09-09-unicode-title-width-design.md)
records the shared cell policy, native font measurements and precise limits.
`examples/world-notes.DERT` offers a notes-only starting point with a mixed title.

Temporary reasoning inspection and return now have a [worked interaction and
evidence record](docs/superpowers/specs/2026-09-09-reasoning-return-design.md).
Reading keeps source folded; editing uses the original buffer. Return is a
navigation action, with source Undo and Save retaining their existing meanings.

The [original example coverage map](docs/ORIGINAL-COVERAGE.md) now accounts for
C01-C13 and T01-T24 with independent fixtures, assertions, observed readings and
explicit limits. Viewer explanations distinguish captured chain extent from an
authored closer; historical START, unfinished hooks and DERCS keep their identity.

Literal punctuation now has source-selection and new-sinker previews. Exact
quote insertions preserve paths, existing quotes and typed characters; the parser
checks ownership and annotations before accepting a source convenience edit.
The [delivery record](docs/superpowers/specs/2026-09-09-literal-authoring-design.md)
keeps its current quote behavior separate from a portable escape grammar.

Structured context now has a [bounded reading and authoring integration](docs/superpowers/specs/2026-09-09-context-reading-design.md).
HOW retains its complete explanation; similarities exposes ordered source items.
Raw fields, unknown note kinds, duplicate occurrences and unfinished words remain
available. Selecting an item edits its exact range; a named reading never binds
an application operation.

Cross-note references now connect explicit named occurrences within one document.
Notes selects exact destination words and keeps an anchored return across visits
and edits. CLI inspection and MER share the name index; duplicates, no matches
and unfinished names remain visible. The [delivery record](docs/superpowers/specs/2026-09-09-note-references-design.md)
distinguishes name matches from structural ownership and application bindings.
Portable schemas and cross-document identity remain deferred.

Author-and-agent context selection now has a [dedicated copy/export review](docs/superpowers/specs/2026-09-09-context-selection-design.md).
The primary note and its own annotations start included; other notes and all
omitted source remain visible choices. Native Copy/Export and CLI/MER share exact
source excerpts, actual parents, ranges and revision identity. Source changes
refuse old export reviews. Portable schemas remain deferred.

The update transaction is complete for the supported npm adapter, with exact
review, rechecks, interruption handling and a verified observed result.
The first reproduced doctor conflict now connects a broken earlier npm PowerShell
route, its missing entry point and a later identified copy to guarded correction.
The next installation increment now has a per-user setup implementation and
executable delivery checks; clean-account validation remains open below.
Rehearsal/doctor result adapters remain further work;
portable schemas and automatic agent delivery stay deferred.

## Deepen the language without inventing a dialect

- [x] **Close the original example coverage map.** C01-C13 have test-owned
  fixtures and semantic assertions; T01-T24 have verified, bounded or deferred
  dispositions. Compact extent, unfinished hooks, historical START, DERCS/help
  and loose fragments have explicit current readings. The [map](docs/ORIGINAL-COVERAGE.md)
  retains canonical grammar, general formatter and receipt-integration limits;
  this completes the accounting, not those deferred features.
- [x] **Make literal punctuation comfortable — bounded authoring delivered.**
  Source selection and list composition now preview exact quote insertions for
  `//`, hooks, lines, paths and literal commas. Existing quotes stay verbatim;
  no-op, unfinished quote, annotation, ownership, selection and stale-source
  cases have direct coverage. Apply/Undo/Save and CLI/MER readings stay connected.
  Portable escapes, decoded values, automatic paste treatment and changing a
  parsed application's meaning remain separate work, not implied by this check.
- [x] **Retain structured context as useful data — bounded inspection delivered.**
  Named HOW/similarities readings preserve raw fields, source ranges, quotes,
  annotations, duplicates, empty places and unknown kinds. Notes exposes a
  reading/raw-field switch and exact source selection with Return/Undo/Save.
  CLI/MER use the shared reading; crowded views retain missing-boundary status.
  This prototype does not define portable schemas, versioning, decoded values
  or application relationships. Typing is not inferred from comma counts.
- [x] **Offer useful cross-note references — bounded navigation delivered.**
  Explicit content/target/similarities names find other named places in the same
  document, with each duplicate's exact range and actual structural parent.
  Notes References supports visits, anchored Return, edits, Undo and Save/reopen;
  CLI `inspect --at N --references` and MER `refs`/`ref N`/`ref back` use the shared
  index. Paged results preserve choices; stale and pending actions cannot redirect
  them. Empty, unfinished and unmatched names stay useful. Name equality does not
  bind an operation. Cross-document identity, persistent backlinks, rename-all
  and portable schemas remain separate work.

## Connect the ecosystem around the author's work

Delivered from the user's 2026-09-09 ecosystem direction:

- [x] DERT executable/projects/CLI identity, normalized `update` command, shared
  application catalog and typed CLI/UDL binding; legacy original source retained.
- [x] `where` with exact PATH/registry names, package and adjacent-folder evidence,
  bounded size measurement and connected wide/narrow presentation.
- [x] `where codex --doctor` / `-dr`, shared npm checks, saved source-bound review,
  exact approval, guarded uninstall/reinstall, interruption and verified receipts.
  NVM prefix links remain bound to their reviewed targets across phases.
- [x] Source-selected MER probe/copy/doctor/details and a separate CLI approval
  handoff, with stale source/routes and incomplete discovery requiring attention.

See the [implementation and evidence](docs/superpowers/specs/2026-09-09-dert-ecosystem-design.md).
Native Codex launch/auth health, other manager adapters, full-machine application
indexing and additional conflict kinds remain additional work.

The completed context-selection increment and remaining development directions
are recorded below. Unchecked entries retain their unimplemented scope.

- [x] **An author-and-agent context selection — bounded handoff delivered.**
  One reasoning note with its own annotations opens an exact source preview.
  Other notes remain explicit, paged choices; Left out reveals every omitted
  source range without inferring agreement or contradictions. File/buffer identity,
  revision, ranges, actual parents and unresolved boundaries travel with the
  reviewed text. Native Copy/Export and CLI/MER share it; stale source, pending
  choices, new-file-only export and keyboard clipboard handling are guarded.
  Exported text is inert context. Portable schemas and remote delivery remain
  deferred; no whole-document JSON conversion or automatic upload is implied.
- [x] **Results return to useful notes — bounded discovery history delivered.**
  Source-bound discovery and MER's existing probe can retain observed action,
  time, result, effects, evidence and source origin. CLI and Notes review the
  captured/current source and exact standalone result note. Native Apply, Undo,
  guarded Save/reopen and explicit comparison of two records are integrated.
  Imported records remain historical producer reports; they do not execute or
  verify current state. [Design and evidence](docs/superpowers/specs/2026-09-09-results-to-notes-design.md).
  Retain follow-on adapters for update rehearsal and doctor journals, including
  interruption and failed verification; stable origin keys and reconciliation of
  author-edited receipts also remain work. Portable schemas and automatic agent
  delivery stay deferred.
- [x] **Complete the update transaction — supported npm adapter.** CLI, DERT run
  and MER retain source-bound saved proposals. Exact DERT approval replaces the
  raw npm handoff. Update and doctor share evidence checks, a canonical-prefix
  lock, durable journals, interruption handling and final verification. Update
  installs the pinned version without uninstalling first. Real npm verified a
  disposable 1.0.0 → 2.0.0 update; 1,191 tests and all eleven executable suites
  passed, including 29 update checks. [Design and evidence](docs/superpowers/specs/2026-09-09-update-transaction-design.md).
- [x] **Doctor actual conflicts — first Windows case.** A supported generated
  npm PowerShell launcher with a missing entry point hides a later identified copy.
  CLI, DERT run and MER distinguish confirmed evidence, possible overlap and
  incomplete reads; authored possible_conflicts, conflicts and fixme remain notes.
  The selected repair binds source, PATH order, launcher slots and later-copy
  evidence through reinstall and durable verification. Foreign files appearing
  after uninstall stop installation. Real npm/PowerShell reproduced failure and
  repair; 1,252 tests and all eleven executable suites passed, including 18
  location/doctor checks. [Design and evidence](docs/superpowers/specs/2026-09-10-doctor-conflicts-design.md).
  Other conflict kinds and managers remain work; duplicates alone prove no damage.
- [ ] **A usable first launch and durable installation — clean-account proof remains.**
  Per-user setup now bundles CLI, Notes, runtimes and licenses, supplies Start
  entries, optional user PATH and an Open with handler, and registers ordinary
  Windows removal. Editable examples live in Documents and survive reinstall
  and removal. Changed integration choices reconcile owned connections; failures,
  cancellation and running work retain their actual status. A recovery marker
  permits retry after interrupted copying, with unknown old uninstall artifacts
  retained. See the [design and evidence](docs/superpowers/specs/2026-09-10-windows-installation-design.md).
  Test on a clean standard Windows account, including downloaded-file trust
  prompts, choosing the default app, a first Save and a second visit. The
  non-administrator development-account checks do not establish that experience.
- [ ] **Repeated-use polish.** Retain focused selection through safe refresh,
  explain external edits with a source comparison, and preserve concise everyday
  navigation. Measure long notes and large searches, interrupt them, and inspect
  terminal resize and keyboard-only paths. Avoid making every visit a demo.

## Keep evidence and decisions honest

- Original source documents remain editable notes, outside all build/test inputs.
- Every new integration needs a demonstrated imperfect case and repeat visit.
- Keep raw source, structural ownership, application meaning and presentation
  distinct across CLI, authoring host and any data interchange.
- Update this backlog as concrete experiences land; preserve newly discovered
  work rather than declaring the current plan the boundary of DERT.
