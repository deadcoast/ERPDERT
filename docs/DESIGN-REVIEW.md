# Critical design review

Date: 2026-09-06. Scope: all five source/workspace documents and the design
conversation through the user's request for independent, critical initiative,
including the subsequent correction rejecting structural indentation.
This is a source and design review. No implementation existed when this review
was written. A subsequent first language build is recorded in
[implementation status](IMPLEMENTATION-STATUS.md); its scope does not validate
every proposal below.

## The important correction

The earlier responses extracted vocabulary more successfully than they designed
an experience. They then filled gaps with familiar conventions and presented
those conventions as settled facts. That is the same loss of intent the original
review found in `overview.md`.

The product asks us to carry enough complexity that a person can feel fluent in
an expressive technical environment. The person's own document, choices, and
results must be central to that experience. A spectacular status display can
contribute; it cannot alone create that relationship.

The proposed design now connects three things previously treated separately:
what the author meant, where that meaning lives in DERT, and what a tool actually
did with it. See [the experience design](EXPERIENCE-DESIGN.md) for concrete
interactions and [the syntax lab](SYNTAX-LAB.md) for the hard language cases.

## Problems in our own proposals

| Finding | Evidence | Consequence | Review disposition |
| --- | --- | --- | --- |
| Educational success was imposed on the product | Archived README principle 10; the user's explicit correction follows it | Authors who only enjoy notes become incomplete users in our design | Replace with immediate fluency, agency, enjoyment, and useful results; learning optional |
| Metaphor was mistaken for grammar | WHO-WE-ARE opens with hook/line/sinker names; archived README invents nearest-open ownership | A useful teaching metaphor does not resolve which block owns a string | Define ownership with examples and counterexamples in the syntax lab |
| Optional `ƒin.` was declared too broadly | WHO-WE-ARE says it is not fundamentally necessary; original lines 126–128 give it intentional closure | Philosophical necessity was confused with the chosen syntax of an operational chain | Keep explicit closure in worked chains; decide omission per context |
| A second `STERT` silently closed a chain | Archived README's chain lifecycle has no supporting original definition | Missing closure can reframe an operation without an author's decision | Remove the claim from the current design; propose a closure diagnostic |
| Active sinkers became freely inline | Archived README allows inline/vertical interchange; original line 8 requires expanded strings, while line 65 has a compact invocation | A single normalization policy may erase the intended visual distinction | Treat compact invocation as a distinct candidate form; preserve expanded ordinary payloads |
| Two potentially meaningful edits were called safe | Archived README removes `STERT ;` and terminal commas automatically | An unfinished target or missing next item becomes falsely complete | Classify incomplete input before offering a repair; preserve alternatives |
| `//` recognition was a flat precedence list | Archived README checks inline annotations before understanding the enclosing note | The closing sentinel in a structured note can be consumed as an annotation | Specify context, token position, and full shape together |
| Existing note nesting was redesigned around whitespace | Original lines 19 and 31–35 define contextual nesting with flush-left `//` forms; the user explicitly rejected leading whitespace | The syntax lab invented another block and shifted its disambiguation burden into the author's formatting | Withdraw the indentation proposal, its recognition rules, and its depth-based probe; implement the original relationships |
| First eligible note became title without a lifecycle | Archived README's title rule; original line 9 requests adaptive title presentation | Pasted notes can gain unwanted titles; deleting the rule can trigger an edit fight | Propose title recognition, edit timing, ownership, undo, and repair behavior together |
| Folding was treated as if its meaning were obvious | Original lines 7–8 say always folded/expanded; the closed examples are physically single-line | Source layout and an editor's collapsed presentation can be conflated | Record both dimensions and propose an inspection surface that preserves the fold |
| A conventional grammar was dismissed | Archived README's architecture comparison | Familiar engineering was rejected rhetorically, while no actual parser decision was demonstrated | Evaluate parsing methods against this language; formal grammar is compatible with it |
| A lossless syntax tree was treated as sufficient architecture | Archived pipeline diagram | Note attachment, operation eligibility, presentation events, and edits still have no contract | Define their responsibilities around the worked experience before selecting libraries |
| Ceremony was divided only into success and failure | Archived README's delivery contrast | Waiting for a choice, skipped checks, partial completion, and external uncertainty disappear | Define distinct presentation states with truthful local explanations |
| No first-run delivery was designed | WHO-WE-ARE's imaginary conversation assumes highlighting and CLI already work | The emotional entry point can be blocked by installer/runtime/editor setup | Specify a packaged experience and test it from a clean Windows account |
| Novelty was asserted but never compared | Earlier chat says memorable, impressive, professional repeatedly | Quality becomes an adjective immune to falsification | Choose concrete interactions and record their visible benefits and failure cases |

The archived file is [the previous README](archive/2026-09-06-conversation-readme.md).
It includes the user's later correction verbatim in its HTML comment. It is
retained as provenance rather than erased by the new summary.

## What overview.md lost or invented

The earlier source review remains valid, but it was incomplete without reviewing
our own replacement.

| Area | Source intent | Secondary artifact problem |
| --- | --- | --- |
| Authority | Original is explicitly unfinished | `overview.md` announces an official v1.0 |
| Notes | Free context, structured reasoning, entries with their own annotations, explicit closing safeguard | Only two note forms are actually described despite announcing three levels |
| Layout | Reasoning folded, ordinary strings expanded, adaptive title rule | These constraints are omitted |
| Delimiters | Competing examples require analysis | Mandatory `STERT ;` and one statement matrix are declared |
| Words | Ermahgerd is deliberately placed where it supports identity | Prose phrases become reserved operators without evidence |
| Errors | Defined errors, warnings, problems, and useful lint support | A trailing comma supposedly crashes the linter/compiler |
| Color | Free notes subdued; reasoning brighter and not the Python green referenced by the author | A specific cyan/teal palette is declared without design work |
| Product | Windows-first CLI, configuration as proof, contextual notes for author and agent | A written example is called an exact integration test |

## Coverage of the authoritative material

This table is a preservation map, not a statement that everything is implemented
or every ambiguity resolved. References use the original line numbers in the
source files inspected on 2026-09-06.

| Source | Intent carried forward | Current location |
| --- | --- | --- |
| original 4–8 | Contextual `//`, structured metadata, folding distinction, user convenience over parser convenience | Syntax lab: note states and layout; experience: relation inspection |
| original 9–15 | Adaptive title underline as deliberately excessive visual identity | Experience: title interaction and authoring lifecycle |
| original 19, 24–38, 43–53 | Reasoning can contain annotated data; adjacent notes; safeguard closer | Syntax lab: worked original nest, ownership, hostile examples |
| original 58–60 | Targeted search/update CLI, limited configuration language, full support for chosen examples, basic help, author/agent notes | README scope; experience: command rehearsal and operational slice |
| original 62–84 | Windows/PowerShell first, correct package ownership, dependencies, conflicting installations, considered CLI delivery | Experience: package operation path; verification deferred until adapters exist |
| original 88–94 | Note appearance, STERT, extension, RERDMERR identity/icon association | Syntax lab inventory; experience: delivery and visual roles |
| original 97–105 | Doctoring structure, values and annotations, typed fin | Syntax lab corpus and repairs |
| original 111–128 | Documentation use, restrained Ermahgerd placement, DERCS/help sample, florin farewell | Syntax lab ambiguity register; experience voice and source editing |
| original 133–165 | Strong linter treatment, competing START/command forms, comma contradictions | Syntax lab conflict dispositions and counterexamples |
| WHO-WE-ARE 3–16 | Hook/line/sinker terms; simple author surface with substantial machinery | AGENTS and syntax lab |
| WHO-WE-ARE 22–42 | Broad participation, visible technical confidence, notes-only value | Experience: audience without mandatory progression |
| WHO-WE-ARE 43–64 | Identity, authoring entry point, later extension/app, eventual broader format | README horizon and experience delivery |
| WHO-WE-ARE 89–108 | Avoid generic AI defaults; prioritize creative human benefit over ease of delivery | AGENTS decision loop and experience alternatives |
| WHO-WE-ARE 109–140 | Integrated first-run experience, contextual help, recovery, active creative leadership | Experience acceptance sequences and AGENTS |
| Later user correction | No educational requirement; strategic technicality; timestamps; real validation ceremony and plain-English errors | Experience delivery states and current README |
| Latest user direction | Analogies guide invention; lead with critical judgment and tangible work | This review, worked designs, and repository development loop |

The historical claims about other products and markets express the user's
motivation. No market statistics, competitor history, or predicted adoption
have been established by this review, and none is needed to preserve the
requested product experience.

## Judgment calls made in this review

The strongest first experience is a small document that recognizes its structure,
helps its author shape it, and can carry a real supported operation. Its strength
comes from continuity: the same source remains recognizable before and after
inspection, repair, and execution.

The design should therefore make relations and changes visible at the point of
authorship. It should let people enjoy technical presentation at their chosen
depth. It should let someone keep using notes forever without prompts to become
an advanced user.

The first proof must also include an imperfect input and a second visit. A
perfect one-time demonstration cannot establish authoring quality. Conversely,
we should not discard an unfamiliar interaction merely because it needs a more
capable editor or a harder state model. Those are implementation questions to
investigate with the experience intact.

These are recommendations made by the development lead, derived from the
source. They remain reviewable choices; they are not attributed to the user.

## Evidence for this pass

Read all five existing workspace files completely. The earlier root paths
`original.md` and `overview.md` had moved into `docs/`; the review follows
the current layout.

The previous README is retained in the archive with its text preserved.
The source notes and original DERT corpus are retained separately.
Document verification found no broken local Markdown links and no unclosed
code fences across the nine Markdown documents. The archived README matches
the prior text after line-ending normalization. SHA-256 comparisons confirmed
that original.md, WHO-WE-ARE.md, overview.md, and RERDMERR.DERT retained their
original bytes. These checks do not establish grammar conformance.

No parser, rendered interface, installation, package-manager check, or user
observation is claimed.
