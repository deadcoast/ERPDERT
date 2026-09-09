# ERPDERT: the work still worth doing

Assessment, 2026-09-09. This is a working development order, not a new language
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
  Windows host opens with `ERPDERT write [file]` or `Try Notes.cmd`.
- [x] New-title Enter, rename, paste-boundary treatment, deliberate rule deletion,
  imported-title preview and Undo/Redo use the actual editor buffer and preserve
  caret/selection. Renamed titles retain one rule; unrelated Undo does not disable
  future title treatment. Non-ASCII width is explained rather than guessed.
- [x] Live parser colors, reasoning folded on load, keyboard unfolding/editing,
  owned-entry navigation and narrow-window context. Active sinkers remain expanded;
  literal source remains available to selection and saving.
- [x] Save, Save as, reopen and second-session editing; original-byte/identity
  checks, verified recovery copies, write verification and failure retention.
  No external editor installation is assumed.

See [the host decision and evidence](docs/superpowers/specs/2026-09-09-notes-authoring-design.md).
This completes the bounded title/free-note/reasoning host increment. It does not
complete the full note application, clean-account installation, clipboard/IME
coverage or a general formatter. Expanded reasoning remains open until folded;
automatic refolding after temporary inspection is retained for further design.

## Next: make authorship feel complete

| Priority | Person's action and intended payoff | Existing support and remaining work | Evidence needed before calling it delivered |
| --- | --- | --- | --- |
| 1 — bounded increment delivered | Type a title and immediately enjoy its matching rule, then keep writing | Native host, title behavior and guarded save/reopen now work. Continue real clipboard/IME, dialog and clean-account validation. | Actual control/input tests and rendered wide/narrow views exist. OS clipboard, input-method composition and clean-user first launch remain unproven. |
| 2 — initial authoring integration delivered | Open a reasoning handle and follow an explanation without losing the note | Folding, keyboard toggling and owned entries now share the authoring source. Define automatic refolding after inspection without hiding an active edit. | Adjacent/missing boundaries and exact selection are covered. Long-note usability, real clipboard round trips and automatic resting-state restoration remain. |
| 3 | Finish an unfinished list deliberately | Parser retains terminal commas and explains DERT3003. Implement separate `Add another item` and `Finish this list` choices, with an exact preview and guarded undo. A comma may represent ongoing typing. | Distinguish a promised next item from a finished list. Preserve commas in prose, annotations, blank lines and subsequent headers; do not silently infer intent. |
| 4 | Get the florin goodbye without rewriting unrelated words | Both `fin.` and `ƒin.` are recognized. Implement the original convenience as a clearly defined presentation treatment or explicit reversible edit at a recognized terminator. | Quoted text, notes, URL/path strings, ordinary `fin.` fragments, real chain endings and undo. Highlighting alone must not write a file. |
| 5 | Give a title any language without a crooked rule | MER measures grapheme/cell widths; title repair currently limits checked widths to ASCII. Establish one compatible display-width policy and exercise the renderer and repair together. | Combining marks, CJK, emoji/ZWJ, tabs, fallback fonts and both line-ending forms in the delivery host. A passed arithmetic test is insufficient visual evidence. |

Recommended next complete increment: deliberate list completion (priority 3),
with distinct Add another item and Finish this list actions, source ownership,
exact preview and undo. Carry the authoring validation gaps above alongside it;
the eventual full note application remains a larger scope.

## Deepen the language without inventing a dialect

- [ ] **Close the original example coverage map.** Reconcile C01-C13 and T01-T24
  in the syntax lab against current test-owned fixtures. Record the exact
  implemented reading of `STERT ;`, historical `START`, compact invocation,
  DERCS/help and loose fragments. A parser recovery choice is not a canonical
  grammar decision. Ask for intent only when competing meanings remain consequential.
- [ ] **Make literal punctuation comfortable.** Work through authoring `//`,
  `;`, `;;`, quotes, Windows paths and trailing literal commas. Current quote
  protection is not a settled escape-language specification. A literal edit
  should expose the preserved text and boundary meaning before changing it.
- [ ] **Retain structured context as useful data.** Keep HOW prose intact and
  distinguish it from an explicitly supported list interpretation such as
  similarities. Prototype schema-aware inspection while retaining raw fields,
  spans and unknown note kinds; do not infer typing from comma counts.
- [ ] **Offer useful cross-note references.** The original names targets;
  search now locates matching text. Explore showing exact named occurrences
  alongside actual structural parents, with duplicate names and unresolved
  references visible. A drawn connector must say whether it means ownership,
  a name match or a resolved application reference.

## Connect the ecosystem around the author's work

These are worked next directions or retained ambitions, not shipped integrations.

- [ ] **An author-and-agent context selection.** Original use includes notes
  shared with a development agent. Design selecting one reasoning block and
  its own annotations for a copy/export preview, carrying file/revision/ranges
  and unresolved boundaries. Keep excluded material visible as an explicit
  choice; never upload or silently omit a contradicting note. This is a proposed
  human-facing handoff, not permission to flatten the whole language into JSON.
- [ ] **Results return to useful notes.** Develop the operation-receipt journey
  in experience design: observed action/time/result/provenance and source origin,
  then explicit insertion preview and later comparison. Imported receipts remain
  observations, not executable requests or proof that current state is unchanged.
- [ ] **Complete the update transaction.** Source-bound choice and rehearsal now
  work. Build separate approval, snapshot rechecks, execution, process interruption,
  post-install verification and a truthful recovery contract before calling
  package management complete. The copied manual command does not provide these.
- [ ] **Doctor actual conflicts.** Turn the original possible_conflicts,
  conflicts and fixme distinction into observed evidence and a chosen action.
  Begin with one reproducible Windows conflict. Mere duplicate PATH routes or
  uncertain ownership are not proof of a conflict; verify manager behavior when
  implementing each adapter.
- [ ] **A usable first launch and durable installation.** Publish now bundles
  CLI, authoring host, runtimes, license and examples together. Help, paths with
  spaces and reopening are exercised. Complete normal-user installation,
  file associations and removal. Test from a clean Windows
  account. Current publish/smoke results do not establish that experience.
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
  work rather than declaring the current plan the boundary of ERPDERT.
