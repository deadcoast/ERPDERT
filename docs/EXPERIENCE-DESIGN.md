# ERPDERT: the document participates

Working design, 2026-09-06. The product commitments come from the original
notes and user corrections. Interactions, internal contracts, and delivery
choices below are development recommendations. The storyboards describe proposed
behavior, not a shipped editor. Subsequent CLI ownership/linting, title-repair,
and author-selected note-boundary increments are
recorded in [implementation status](IMPLEMENTATION-STATUS.md); it does not
establish that the complete interactions below have been built or proven usable.

## The design thesis

An author should be able to point at an ordinary-looking piece of their own
writing and discover that the software understands what belongs to it, can
repair its presentation, and can carry a supported action through to a result.
The technical appearance is justified by real relationships underneath it.

The document is the center of this experience. Highlighting, folding, linting,
doctoring, and the CLI are ways of working with the same material. The person
need not know the names of those subsystems to feel they are using them.

The "logical shield of embarrassment" concerns social and cognitive friction.
The person can recognize a problem, choose a next action, and enjoy a technical
presentation without being exposed as a beginner. Learning may happen, but
notes-only use is a complete destination. No graduation screen or skill ladder
belongs in the default experience.

There are three plausible centers for the product. An elaborate CLI can make
operations satisfying, but the notes-only author encounters little of it. An
editor skin provides an immediate visual reward, but contributes little to
structured context or the actual CLI. The recommended center is an authored
document whose relationships, corrections, and results become visible on
demand. This uses the language's distinctive features throughout the experience.

## 1. The title performs the first repair

The user types:

```dert
// MY FIRST TITLE
```

At the completion of the line, the proposed authoring integration adds:

```dert
// MY FIRST TITLE
// --------------
```

The title gains a deliberate typographic treatment. The rule draws into place
once. A small local receipt reads `TITLE ALIGNED · Undo`. When Enter commits
the title, the generated rule occupies the next row and the insertion point
lands on the blank row after it, ready for the author's next words. A title
rename keeps its existing insertion point. There is no modal introduction to
linting.

The title and its rule behave as a pair. When the author changes the title, the
rule adjusts at the next editing boundary, not after every keystroke. A paste
finishes before formatting runs. Undo removes the last automatic edit without
immediately triggering it again. Explicitly deleting the rule leaves a useful
repair offer rather than trapping the author in a delete/reinsert loop.

Working recognition proposal: the first nonblank line, if it is a free `//`
note, occupies the title position. A structured reasoning opener in that
position remains reasoning. Later free notes do not become titles merely
because they look like headings. Existing imported documents receive a title
repair preview before any source edit.

The underline excludes the `// ` prefix and matches the title's displayed
width under DERT's chosen monospaced width policy. That policy still needs
definition for combining marks, emoji, wide characters, tabs, and fallback
fonts. The host renderer and formatter must use a compatible policy; otherwise
a rule can be mathematically correct and visibly wrong.

The first repeat visit matters. We should replay the flourish for a newly
created title, not for every file open. Reduced motion shows the same finished
rule and local receipt immediately. Plain text still contains the title and
rule, with no hidden rich-document dependency.

Acceptance: type, rename, paste, undo, delete the rule, reopen the file, and use a
non-ASCII title. Source must remain stable, editing must feel uninterrupted,
and the result must still be attractive after the novelty has worn off.

## 2. Punctuation can reveal what the document understands

Working name: the connection view. It is an inspection gesture, not a lesson.

Consider the original reasoning nest:

```dert
// support doctoring ; conflicts ;;
// possible_conflicts, // may interfere with this installation
// conflicts, // a conflict has been confirmed
// fixme // the offered repair
//
```

At rest this has the required folded reasoning presentation. Its handle shows
`support doctoring ; conflicts` and the existence of three entries. Focus on
the handle or its line marker reveals the relationship: the header, its entries,
their individual annotations, and the closing `//` share a visual trace.
The other material stays readable.

This is the proposed head-turning interaction: the same two slashes that looked
like ordinary notes visibly hold a structured object together. The author
discovers an organized shape in their own text without opening an AST panel.

In a rich host, a temporary inspection surface fans out the entries beside the
folded source. It follows source order and anchors each explanation to its
entry. It must support keyboard focus and editable text, not a hover-only
tooltip. Selecting an entry can jump to its exact source range.

In a terminal, the same selection produces a brief source excerpt with line
numbers and clear labels outside the source text. The source retains its
flush-left form, including when copied. Both surfaces answer the same
questions: what is this, what belongs to it, and where is the original?

The interaction must distinguish syntactic ownership from a reasoning note's
claimed target. `// HOW ; codex, claude ;; ... //` names two targets, but names
alone do not establish binding to two particular operations. Show unresolved
target references as such. A beautiful connecting line must never invent a
relationship that the language has not established.

When a closing marker is missing, show the unresolved boundary. Do not draw a
confident trace around unrelated following commands. The same mechanism that
makes valid structure impressive must make uncertainty locally understandable.

Acceptance: inspect one nest, adjacent closed notes, duplicate target names,
an annotated sinker, and a missing closer. Keyboard and pointer access must
expose the same information. No decorative connector may claim an unproven
attachment.

## 3. Rehearse a correction on the actual note

A generic error list separates the author's intention from the repair. DERT
can use the exact source location as its repair surface.

For a title-width mismatch, the proposed diagnostic looks like a developer's
source report while saying something directly usable:

```text
06 SEP 2026 · 14:37:08 local                 TITLE-RULE
weekend.DERT:2

The line under your title is two marks short.

  1 | // MY FIRST TITLE
- 2 | // ------------
+ 2 | // --------------

[Align title]  [Leave for now]
```

This is an illustrative diagnostic, not a log from a working tool. Exact
diagnostic identifiers and timestamp formatting are still design choices.

In an editor, focusing `Align title` previews that small change in place.
Applying it commits the same edit and leaves a short-lived undo receipt at
the affected text. The cursor and nearby explanation remain anchored. There
is no screen-wide celebration for two hyphens.

A comma case must offer a different kind of interaction:

```dert
items ; weekend ;;
snacks,
```

The author could be finished, or could be about to type another item. The system
should say, "That comma leaves room for another item." While typing, it is a
quiet incomplete state. When checking the document, offer `Add another item`
and `Finish this list`. Preview each result. Do not call one an automatic
correction until the author has chosen the intended outcome.

Do not equate a clean document with a document the tool aggressively rewrote.
The impressive effect is that the tool understood the choice and made it easy.

Repair records need the affected source range, original text, proposed text,
meaning impact, explanation, and document revision. If the source changed while
the preview was open, update or withdraw the preview. Applying a stale repair
to the wrong line would break the central promise.

Acceptance: preview without writing; apply; undo; edit while the preview is
open; reopen an incomplete list; dismiss a suggestion. No automatic edit may
fight an intentional edit or claim to resolve missing intent.

Implemented CLI slice: `repair --at <line>` now shows an unfinished reasoning
block's recovered grouping, asks for an offered source-line/EOF boundary, then
rehearses its exact insertion and reparsed ownership. Every retained entry and
each released line's new role/owner are visible before separate approval.
Both prompts default to leaving. This is an author-selected closure using the
existing `//`, not the proposed list-completion interaction or an editor feature.
The [boundary contract](superpowers/specs/2026-09-06-note-boundary-design.md)
and implementation status record its actual scope and evidence.

Implemented handoff: each supported lint finding now carries its own complete
PowerShell rehearsal command. Generic help would make the author reconstruct
the path and context; an unbound line command could outlive the source it cites.
The selected delivery ties the command to the checked bytes and opens a preview
without write prompts. Note choices retain that link through selected rehearsal,
and plain mode follows the copied action. A separate approval is still needed.
Unsupported findings describe their missing capability rather than offering a
command that cannot perform the promised repair. The terminal view walks source
locations in order; semantic diagnostics themselves are not reordered or rewritten.

## 4. Let a command return to the note that asked for it

This proposal is where the CLI becomes more than a second application bundled
with the highlighter.

The original compact expression remains recognizable:

```dert
STERT ; ERPDERT ;; codex
```

Before any package change, the supported command view should make the intended
action legible in the same place as the source. For the initial read-only
discovery phase, the plain explanation could be:

> Find the installed Codex command and identify what manages it.

The familiar CLI spelling, `ERPDERT codex`, is available in that view.
The actual manager-specific invocation can be inspected after discovery.
The example asserts no current Codex installer or package manager.

The operation model must distinguish discovery, explanation, plan, action,
and verification. An executable found on disk does not alone establish its
owner. "Installed twice" and "these installations conflict" require different
evidence. If ownership remains uncertain, the operation explains that uncertainty
and offers the next supported inspection.

The proposed signature completion is `Keep result in this note`. After a real
operation, the tool can preview a structured reasoning note carrying the
observed result, time, relation to the originating chain, and available evidence.
The author's source remains the host; the tool has brought something back.

Candidate receipt shape, with fictional fixture data:

```dert
// checked ; codex ;;
// source ; chain ;; chain-weekend-01
// result ; command lookup ;; one matching command
// changed ; this computer ;; nothing
// observed ; local time ;; 06 SEP 2026 14:37:08
//
```

This shape extends the source's annotated nest with structured child entries.
It is a proposal for the syntax lab, not an established grammar or executed
check. Location alone is insufficient when a document is edited.

Working attachment proposal: keeping the first result for an expanded chain
offers one source transaction that adds the receipt and, when needed, a folded
identity note inside that chain:

```dert
// identity ; chain ;; chain-weekend-01 //
```

The example key is illustrative; the tool generates a document-local unique
key. Its use is an application meaning for an existing reasoning shape.
The chain's identity and the receipt's source field use the same key. The key
travels in the readable source, so a line move does not break the relation.
The preview includes both edits; the tool does not add hidden bookkeeping to
every personal note.

The command schema must recognize this identity note in the containing chain.
Copying a chain within the same document can duplicate a key: offer a new key
for the copied chain and keep existing receipts attached to the original.
Do not guess when an imported document already contains duplicates. Record
the source revision with each observation so changing a chain preserves its
history without presenting the old result as a check of the new text.
Cross-document links require a separate reference design and are outside this
first proof.

The saved receipt records what was observed then. It must not pretend to be
permanently current. Rechecking creates a comparison with the prior observation:
what changed and what stayed the same. It must preserve authored reasoning.
A file opened in a basic text editor still communicates its result.

Copying a result for a friend can use this readable excerpt and its technical
styling. Keep copied text useful without ERPDERT installed. Provide a preview
of any machine-specific paths or names included in an excerpt; sending or
uploading is not part of the default action.

Acceptance: discovery without changes; uncertain ownership; a supported action
and verified outcome; failed verification; keeping a result; moving the source
chain; rechecking later; copying an excerpt. A pretty receipt cannot turn an
attempt into a verified success.

## The experience needs more than success and failure

| State | Visible delivery | Available meaning |
| --- | --- | --- |
| Ready | A clear local action with an understandable scope | What will happen |
| Reading or working | Actual stages become visible as events occur | What the tool is doing now |
| Waiting for a choice | Motion settles; the question and choices take precedence | Why the tool cannot choose on the author's behalf |
| Successful check | A satisfying completion mark tied to a real check | What was established |
| Applied, verification pending | Change and pending check have separate statuses | What changed, what has not yet been established |
| Partial or skipped | Plain status beside the affected stage | What was left out and why |
| Failed | Exact location or operation, plain cause, evidence, concrete next action | What prevents progress |
| Uncertain | Honest unresolved state with available inspection | What is known and unknown |

These events drive presentation. They must not be derived from a timer scripted
to look like progress. Batch several fast checks into a legible completion,
rather than delaying the result to finish an animation. An interruption should
settle immediately into an explanation.

Expert detail can be present without dominating. A source position, a status
identifier, a timestamp, and a diff make a report look technical and give it
real precision. The sentence beside them must still explain the consequence.
Allow exact time/zone detail on inspection while the primary timestamp remains
readable. Time, color, and status wording must agree across the CLI and editor.

Humor belongs in naming, transitions, and optional detail where it does not
delay the person's next action. It must not make an error report harder to read
or turn a struggling author into the joke.

## One experience at several chosen depths

Someone enjoying colored notes should receive the complete title, inspection,
and repair experience without a CLI lesson. Someone working with metadata
should be able to inspect exact fields and relationships. Someone invoking
supported operations should be able to inspect actual command arguments,
observations, and outcomes.

These are choices at the current point of work, not accounts labeled beginner,
intermediate, and expert. Explanations open where needed. A person who never
opens them is still using the product correctly.

At the hundredth use, the experience should respect familiarity. Keep a stable
document layout, repeatable shortcuts, local acknowledgments, and predictable
undo. Elaborate welcome sequences belong to first contact or deliberate replay.
Quiet use should keep DERT's identity in typography, punctuation, and structure.

## Distribution is part of this design

The current workspace contains documents, not an installer or application.
The intended Windows package should carry the runtime needed by its own
features, install the CLI entry point, and open a usable DERT example in the
supported authoring surface. No Node/npm/package.json setup belongs in that
author's first interaction.

An editor extension cannot be assumed to exist merely because the CLI is
installed. A bundled authoring demonstration can precede the full note app.
If an integration uses VS Code, detect its availability and make the actual
installation state clear. An absent editor must not strand the person at
"now configure a development environment."

Initial delivery recommendation: a packaged Windows CLI plus one deliberately
small authoring surface for the title, note, and repair sequence. This is a
proof surface for the UDL/CLI, not the full general note application. Evaluate
whether an embedded editor or an installed-editor integration best preserves
that first-run experience. Full extension and app work remain later scopes.

Test the chosen delivery from a clean Windows account, including a space in the
path, ordinary user privileges, reopening after installation, and an absent
optional editor. Verify that the installed help command and the authoring
surface expose the same actual capabilities.

## What the engineering must provide

| Capability | Required data or behavior | User-visible reason |
| --- | --- | --- |
| Source model | Exact text, spans, newlines, punctuation, recoverable incomplete nodes | Edits and explanations point at the author's actual words |
| Context model | Node kinds, ownership edges, claimed targets, resolved references kept distinct | Connection view can show real structure |
| Presentation model | Semantic roles, folds, active inspection, title/rule relationship | Distinct appearance remains consistent across surfaces |
| Repair model | Revision-aware edits, explanation, meaning impact, undo information | Rehearsal and correction stay reliable |
| Operation model | Supported command schema, source reference, typed arguments, observations, outcomes | English explanation and actual operation agree |
| Event model | Real phase/status/evidence events | Ceremony and diagnostics tell the same truth |
| Receipt model | Observed time, action, result, provenance, reference to origin | Results return to useful notes and can be compared later |

This does not dictate seven services, packages, or processes. These are
responsibilities that must remain distinguishable in the design. One process
may implement several. Select a parser strategy and implementation language
after testing source preservation, editing behavior, and the Windows delivery.
There is no inherent conflict between a formal grammar and this experience.

JSON export remains later work as requested in the original notes. The current
design preserves the information it will need without making serialization the
center of the author's experience.

## The first proof and its review

Build toward one complete sequence: open a DERT document, create its title,
inspect an original reasoning nest, preview a real ambiguity, repair it,
rehearse a supported CLI invocation, perform read-only discovery, and return
an observation to the source. Package it so another person can try it.

Use the original source cases in this proof. Do not construct only convenient
examples that avoid overloaded slashes, incomplete delimiters, ordinary English
punctuation, or conflicting ownership.

Implementation order can be incremental: source/repair behavior, title and note
surface, command discovery adapter, then linked results. Each increment keeps
the full proof's requirements visible. The targeted package update and doctor
features follow with their own real platform evidence; a read-only discovery
demonstration does not stand in for them.

Review actual frames at rest, focus, preview, failure, and completion. Try the
sequence using keyboard only and with motion disabled. Observe whether a person
can tell what changed and choose an available action without a vocabulary test.
Ask what they would show a friend, and why. Those observations assess the
experience; passing parser tests establish a different kind of correctness.

Current evidence: these interactions have been designed and challenged against
the source. Their aesthetic effect, comprehension, installation quality, and
memorable value remain to be demonstrated in a working surface.
