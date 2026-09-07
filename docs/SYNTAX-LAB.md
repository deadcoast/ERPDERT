# DERT syntax lab

Working analysis and design recommendations, 2026-09-06.
This is not a canonical specification. A subset now has a working implementation;
see [implementation status](IMPLEMENTATION-STATUS.md) for exact coverage and limits.
It preserves the source's competing forms so decisions can be made with evidence.

## Established intent and proposed mechanics

The user has defined the names hook (`;`), line (`;;`), and sinker (string
element). The source requires contextual note forms, distinct reasoning
metadata, deliberate presentation, supported CLI/configuration use, and strong
tooling. It explicitly prioritizes an easily remembered authoring surface.

The original already defines the contextual nesting demonstrated below. Its
implementation must recognize those relationships and preserve the source form.
An unfinished parser specification does not make the author's syntax undefined.
Several earlier assistant proposals, including structural indentation, changed
that syntax instead of formalizing it.

The recommendation is to model a small surface vocabulary with explicit
contexts. An English phrase before a hook can stay one phrase. We do not need
to declare every space-separated word a command or option.

| Surface | Structural reading proposed | Application meaning |
| --- | --- | --- |
| `support doctoring ; conflicts ;;` | Header phrase, hook, target field, line opening payload | A supported application schema can interpret the fields |
| `// content ; target ;; reason //` | Closed reasoning note with three source fields | Content and target can identify a metadata schema; unknown schemas remain readable notes |
| `// free prose` | Free contextual note | No command implied |
| `value, // explanation` | Sinker with continuation marker and annotation, inside a recognized payload | Meaning of the value comes from its owning structure |
| `STERT ; ERPDERT ;; codex` | Candidate compact invocation explicitly demonstrated in original | Original maps it to `ERPDERT codex` |

The CLI's supported command catalog should assign operational meaning to
recognized structures. The parser must not guess application semantics from
English-sounding phrases or execute reasoning text as instructions.

## Original example and data relationships

The original example, reproduced without changing its text or punctuation:

```dert
STERT
// similarities ; AI ;; claudecode, claude, codex, openai-desktop-app //
// HOW ; codex, claude ;; I dont care if it calls claudes installer, i just want it to be correct. //

// pip, and python inherently to manage pip?
// npm, and node or no?

// support doctoring ; conflicts ;;
// possible_conflicts, // something thats known to override it, block it, or break it.
// conflicts, // something that is almost surely conflicting
// fixme, // the confirmation command to fix the conflict
//

commands ; updateme ;;
ƒin.
```

The closed reasoning notes carry their content, targets, and payloads. The
pip/npm lines provide free context. The doctoring block carries entries with
their own context notes. Its standalone closer returns to the surrounding
chain, where `commands ; updateme ;;` remains a separate active statement.
`ƒin.` closes that chain.

The relationships inside the doctoring block are already expressed by its
delimiters and context:

| Source fragment | Node or role | Owner |
| --- | --- | --- |
| First `//` | Reasoning-nest opener marker | Nest |
| `support doctoring` | Content/header phrase | Nest |
| `;` and `conflicts` | Hook and target field | Nest |
| `;;` | Line opening the nest payload | Nest |
| `possible_conflicts` | Sinker 1 | Nest payload |
| `something thats known to override it, block it, or break it.` | Context annotation | Sinker 1 |
| `conflicts` | Sinker 2 | Nest payload |
| `something that is almost surely conflicting` | Context annotation | Sinker 2 |
| `fixme` | Sinker 3 | Nest payload |
| `the confirmation command to fix the conflict` | Context annotation | Sinker 3 |
| Bare final `//` | Nest closer | Nest |

An annotation belongs to the preceding element on its own physical line.
It does not become a new sinker or attach to the next command.
The final closer belongs to the nest, not to the last annotation.
This structural ownership is independent of whether the note targets an
application object also called `conflicts`.

Changing an annotation must preserve the sinker value. Reformatting a nest
must preserve its item order and ownership. Copying the nest must preserve
its meaning without an external database of editor decorations.

## Why a flat precedence list fails

Consider these original shapes:

```dert
// similarities ; AI ;; claudecode, claude, codex, openai-desktop-app //
// HOW ; codex, claude ;; I dont care if it calls claudes installer, i just want it to be correct. //
```

An indiscriminate rule that `//` after text starts an annotation will consume
the closing delimiter. An indiscriminate comma split will break the HOW
sentence into two values. An indiscriminate semicolon split will damage prose
that uses semicolons without claiming structured-note form.

Working recognition proposal:

1. Determine the enclosing context from the opening and closing forms already
   read. Leading whitespace does not establish nesting depth.
2. Protect quoted spans and ordinary URL/path text when finding delimiters.
3. Recognize the complete closed-note shape before considering its closer as
   an inline annotation. Require its structural hook and line.
4. Recognize a nest opener only from its complete header shape and terminal
   line marker.
5. Inside a reasoning nest, recognize its standalone `//` closer and the
   prefixed entries with their own same-line context notes.
6. Recognize inline annotations in the element context that can own them.
7. Preserve remaining prefixed prose as a free note.
8. Give title/rule recognition its own document-position rule.

These are responsibilities for the recognizer, not a sufficient implementation
algorithm. Each needs fixtures, exact token rules, and recovery behavior.

Candidate delimiter rule: structural `//` occurs at the appropriate line
prefix, as a bare closer, as a terminal closed-note sentinel, or as a
whitespace-separated annotation marker. The slashes inside `https://` do not
qualify. Syntax tokens inside explicitly quoted literals are not structural.
Whitespace around `;` and `;;` can be normalized only after the field
structure is known. Formatting preference and lexical recognition are distinct.

Candidate literal mechanism: quoted text for the rare value containing reserved
delimiter sequences. Windows backslashes should remain literal by default.
The exact quote/escape grammar needs definition; the highlighter and repair
surface must help author a literal without requiring a separate lesson first.
Do not invent a large escape language before testing the actual source cases.

## English, lists, and data density

Free notes preserve their complete prose. Closed reasoning notes preserve raw
content, target, and payload fields, including punctuation and source ranges.
They are structured data even when each field is a string.

Recommendation: a declared metadata schema can add a typed interpretation while
retaining the original fields. For example, a schema for the source's
`similarities` note may interpret the payload as a list, whereas `HOW`
preserves its reason as prose. Unknown note kinds remain usable and lossless.
Typed meaning must not depend on an undocumented heuristic that counts commas
or guesses whether a sentence "looks like" English.

Schema selection, versioning, and portable typed interpretation remain a design
decision before machine interchange. The authoring surface should show the
current interpretation on inspection. A schema mismatch should explain what
the receiving operation needs without turning arbitrary personal notes invalid.

For active vertical payloads, the original "no comma on last string item"
provides a stronger clue: a terminal comma indicates another item is expected.
A comma inside an ordinary string is a separate question. We recommend
recognizing terminal structural commas and preserving internal prose punctuation,
with quoted literals available where a trailing literal comma is necessary.

Blank lines and free notes can separate existing items without becoming values.
An explicit new header must not be swallowed as another sinker merely because
the preceding comma promised continuation. Show the unresolved list boundary
before the header instead.

## Layout and folding have two dimensions

The original explicitly says reasoning is always folded and UDL strings always
expanded. It demonstrates single-line closed reasoning notes, multiline
reasoning nests, vertical active strings, and a compact invocation. It does
not fully define whether "folded" names serialized layout, visual collapse, or
both in every case.

Working recommendation:

| Construct | Candidate canonical source layout | Candidate presentation |
| --- | --- | --- |
| Free note | Author's physical lines | Readable, subdued |
| Closed reasoning note | Its closed single-line form | Compact reasoning handle |
| Reasoning nest | Header, entries, and explicit closer | Folded handle with temporary inspection |
| Ordinary active sinkers | One item per line | Expanded |
| Compact STERT invocation | The original one-line form, treated separately | Complete invocation visible |

This preserves the source's apparent distinctions without declaring arbitrary
inline and vertical forms interchangeable. The final canonical rule still
needs to settle what can be reformatted across physical lines.

Proposed rich-host behavior: the reasoning handle remains folded while a
temporary inspection surface exposes its contents and source. This provides
inspection and editing without silently changing the prescribed resting
presentation. A plain editor continues to show the actual source.

## Nesting through the defined `//` roles

The author has already provided the nesting mechanism. All source lines in
the example start in column one. The structured opener establishes the
reasoning context; each prefixed entry belongs to that context; a second
`//` on an entry introduces that entry's explanatory note; the standalone
`//` closes the enclosing reasoning context.

The entry's explanation runs to the end of its physical line. Ending that
explanation does not close the enclosing reasoning block. Conversely, the
standalone closing marker belongs to the block rather than to the last entry.
The same marker has distinct roles because its surrounding form and active
context are different.

The lexer/parser must retain those contexts, entry relationships, raw values,
annotations, and source ranges. A formatter must preserve the flush-left
source form. Internal data structures may represent ownership at multiple
levels without transferring that hierarchy into leading whitespace.

The earlier indentation proposal is withdrawn. It inserted an additional
`observed` block, then introduced a whitespace rule to distinguish the new
grouping. Neither addition was needed to represent the original data. Its
depth-based recognition, closer-alignment rule, and proposed recursive-block
comparison are removed from the current design.

If the existing block's standalone closer is missing, report that boundary
at its source location. The diagnostic and its repair must work with the
author's existing markers. Missing punctuation does not justify changing
the nesting model.

Implemented probe: `repair --at <line>` offers a missing block closer at the
current recovered entries' source starts or recovery stop/EOF. The author
selects the cut, sees retained entries and actual reparsed ownership outside
it, and approves separately. This adds one existing standalone `//`; it does
not define a new nesting rule, adopt indentation, or canonize recovery behavior.
Inline-note closure and expanded-list completion remain separate unresolved
repair cases. See [implementation status](IMPLEMENTATION-STATUS.md).

## Chain closure and compact forms

The original `ƒin.` contributes both visible identity and an explicit boundary.
The user's observation that it is not fundamentally required challenges
necessity; it does not settle all operational grammar.

Working recommendation for the first proof: use explicit `ƒin.` in expanded
operational chains, preserve the original compact invocation as its own
candidate, and allow unfinished documents to remain editable and inspectable.
At execution time explain a missing boundary if it changes the selected
operation's extent.

Do not presently assume that another `STERT` implicitly closes the prior
chain. Compare three cases before choosing the rule: a complete compact
invocation, an expanded chain at EOF, and an expanded chain interrupted by
another `STERT`. Record the interpretation of each.

Likewise, `STERT ;` may be a historical decorated opener, an unfinished hook,
or a deliberate accepted variant. Calling its semicolon meaningless and
deleting it was premature. The corpus must resolve that choice.

`fin.` to `ƒin.` is an intended authoring convenience in the original.
Implement it as an explicit formatter/editor edit only at a recognized
terminator position, or as a documented presentation treatment. Lexing and
highlighting themselves should not mutate the source. Never replace the text
inside an unrelated word, quote, or ordinary note.

## Complete disposition of the source example families

Grouped duplicates remain represented here; each exact original example should
become a fixture when implementation begins. None has been rewritten in place.

| ID | Source | Example family | Disposition and remaining issue |
| --- | --- | --- | --- |
| C01 | original 11–15; RERDMERR 1–2 | Title/rule | Prefix and adaptive length derive from original; bare RERDMERR ruler is a repair candidate; width policy required |
| C02 | original 24–38, 43–53; RERDMERR 27–41 | Adjacent closed reasoning plus annotated nest | Required first-class case; terminal comma and folded presentation require precise treatment |
| C03 | original 65 | Compact `STERT ; ERPDERT ;; codex` | Preserve as its own explicit sample; do not generalize all payloads to inline |
| C04 | original 97–105 | `STERT ;`, active doctoring list, typed `fin.` | Opener variant unresolved; comma contradiction recorded; florin normalization is intended |
| C05 | original 111–128; RERDMERR 12–18 | DERCS/help and `command option ; variable` | Help intent known; exact schema and meaning of DERCS remain undefined |
| C06 | original 137–140 | `START ;` and a two-field command | START is a likely historical spelling, not yet a silent rewrite rule; missing closure may be excerpt |
| C07 | original 143–147 | `START`, `command option variable ;;`, `string,` | Conflicts with other statement matrices and its own final-comma explanation |
| C08 | original 149–160; RERDMERR 43–54 | Loose list, blank lines, subsequent doctoring header | Ownership absent; retain as fragment, do not attach values to nearest reasoning payload |
| C09 | original 163–165; RERDMERR 40 | `commands ; updateme ;;` without sinkers | Could be zero-payload action or unfinished structure; command schema must decide |
| C10 | RERDMERR 20–25 | `command option ; variable ;;` then one string | Distinct header shape; compare against C05 and C07 instead of replacing with a generic matrix |
| C11 | original 119–120; RERDMERR 9–10 | Free note with hook; closed note with hook and line | A semicolon alone must not promote free prose to reasoning |
| C12 | original 113–115; RERDMERR 4–5 | Ermahgerd identity phrases and long prose | Preserve voice; no evidence these words all define reserved operators |
| C13 | RERDMERR as a whole | Multiple complete chains and trailing fragments | Whole document is a source corpus, not an executable integration test |

## Cases that must challenge the recognizer

These are specified probes, not executed tests. Their purpose is to expose where
the current proposal still needs a decision or would violate established intent.

| Probe | Input or action | Required evidence or result |
| --- | --- | --- |
| T01 | Closed reasoning with a URL | URL slashes preserved; actual closer recognized |
| T02 | The original HOW sentence with its internal comma | English reason retained intact in raw metadata |
| T03 | `// STERT ; starts the chain of command` | Free note; no inferred command or open reasoning block |
| T04 | Two adjacent closed reasoning notes | Two distinct nodes and source ranges |
| T05 | Nest entry with inline annotation | Value, continuation marker, and annotation distinguished |
| T06 | Missing reasoning closer before active text | Unresolved boundary visible; no guessed execution |
| T07 | Terminal comma at EOF while typing | Incomplete state; no automatic declaration of author intent |
| T08 | Terminal comma followed by a new header | New header preserved; missing list boundary diagnosed |
| T09 | A comma inside English text | Raw text preserved; structural interpretation explained by context/schema |
| T10 | A literal containing `//`, `;`, `;;`, or a trailing comma | Chosen literal mechanism round-trips without changing text |
| T11 | `STERT ;` with cursor after hook | No destructive cleanup while target may still be entered |
| T12 | `fin.` in note, quoted string, and terminator position | Only recognized terminator eligible for florin edit |
| T13 | Blank lines and free notes between sinkers | Item ownership/order unchanged |
| T14 | Duplicate named targets and a HOW note | Claimed target and resolved object reference remain distinct |
| T15 | Original doctoring nest with every prefix in column one | Each same-line context note stays attached to its entry; standalone `//` closes the block; formatting introduces no indentation |
| T16 | Plain prose in a document with no STERT | Useful document presentation; no unsolicited execution requirement |
| T17 | Empty payload before `ƒin.` | Schema declares zero-payload validity or diagnostic; no blanket guess |
| T18 | Rule repair, undo, then intentional deletion | No formatter oscillation; title repair remains available |
| T19 | Non-ASCII title and line endings from another editor | Declared display-width policy; source newline policy preserved |
| T20 | Repair preview followed by another source edit | Stale edits rejected/recomputed against document revision |
| T21 | Reformat then reparse a valid annotated nest | Same values, order, annotations, and ownership |
| T22 | Parse/print without requested formatting | Source text preserved, including unrecognized constructs |
| T23 | Full RERDMERR corpus | Every C01–C13 family accounted for; fragments distinguished from validated chains |
| T24 | Import a proposed operation receipt | Data remains readable; recorded observation is not replayed as a command |
| T25 | Move or copy a chain carrying the proposed identity note | Moves retain receipt attachment; duplicate identities require a visible resolution |

## Decisions to resolve through the first proof

The recommended interaction design can be reviewed now. These language choices
must become explicit before a canonical grammar is declared:

- Whether folded/expanded governs serialization, presentation, or both for each
  form; treatment of compact invocation as a separate form.
- Explicit versus implicit closure for expanded chains and compact invocations.
- Recognition and canonical status of `STERT ;` and historical `START`.
- Literal quotation, terminal comma behavior, and metadata schema interpretation.
- Reasoning attachment to content below, named targets, and repeated targets;
  validate the experience design's proposed identity note and receipt reference.
- Zero-payload actions and the concrete DERCS, doctoring, and updateme schemas.
- Main-title detection, displayed width, and the scope of automated source edits.

These are bounded design tasks with source cases attached. They should be
worked by the development lead with recommendations and inspected prototypes,
not returned to the user as an unexplained questionnaire.
