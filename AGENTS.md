# Working on ERPDERT

## Read and reconcile

Read this file, `README.md`, `docs/WHO-WE-ARE.md`, and `docs/original.md`
before substantial work. Read the design documents relevant to the task.
Inspect current files before trusting paths or state from a previous session.

Latest explicit user decisions govern. `docs/original.md` remains the primary
language source, including its scattered definitions. `docs/WHO-WE-ARE.md`
records the product ambition. Later user corrections govern both.
`RERDMERR.DERT` is an original syntax corpus. Preserve these source artifacts.

`docs/overview.md` and `docs/archive/2026-09-06-conversation-readme.md`
are historical interpretations containing unsupported assertions. Read them
for provenance and review; do not use them as normative specifications.

The current design documents contain explicit working recommendations.
Do not promote a recommendation to a user decision by repeating it in another
document. Record what changed, why, and the source of the authority.

## What the work must accomplish

ERPDERT gives ordinary authors a memorable, technically expressive environment
for notes, structured context, and supported CLI operations. The user may
want only beautiful notes forever. That is a complete, successful use case.
Learning is incidental; education and progression into programming are not
product acceptance criteria.

The "logical shield of embarrassment" is a delivery requirement: real structure,
plain-English meaning, dignified correction, technically rich presentation.
Avoid beginner labels, exams, compulsory tutorials, jokes at the user's expense,
and claims that the user understands something they have never needed to know.

User effort, identity, agency, clarity, delight, and useful depth drive decisions.
Implementation familiarity, token savings, fewer dependencies, faster delivery,
or resemblance to an established format cannot by themselves justify removing
a feature. An unusual feature deserves investigation and a worked design.
Propose alternatives that preserve its intended effect when a constraint is real.

Novelty also needs judgment. Give each unusual feature a purpose in the person's
experience. More animation, options, jargon, or software layers do not establish
quality by themselves. Design for first contact and repeated use together.

## The development loop

For a substantial design or feature, work through these questions in the
relevant design or task record. Small edits need proportionate treatment.

1. What person, situation, and desired feeling or action are we serving?
2. What do they actually type, see, select, hear, copy, or receive?
3. Which original construct or use case does this deepen? Show the example.
4. What stronger interaction can we offer beyond the obvious implementation?
   Compare concrete alternatives and make a recommendation.
5. What meaning does the system preserve? Define ownership, edit behavior,
   output, and actual effects where applicable.
6. Try to break the design with an unfinished edit, ordinary prose, a mistaken
   action, an ambiguous case, and repeated use. Address the relevant cases.
7. Build or document the smallest complete experience that can answer the
   current question. Retain the full feature's requirements and deferred work.
8. Inspect evidence in the actual delivery surface when implementation exists.
   Revise weak behavior; record what remains unproven.

Bring design judgment and worked recommendations. Do not return the user's
analogies as a manifesto and call that progress. Do not ask them to do routine
design work or repeatedly authorize already requested reversible work.
Ask only when a consequential decision remains dependent on their intent.

## Data, presentation, and correction

`;` is the hook; `;;` is the line; string elements are sinkers.
Treat the `//` family and its contextual uses as deliberate language design.
Implement complexity behind the surface before proposing more symbols.
Grammar may be formal and deterministic while the experience remains unusual.

The defined note nesting uses contextual `//` roles with source lines starting
in column one. Do not introduce leading whitespace to encode nesting, pair
closers, or make an internal hierarchy visible in serialized source. A
formatter must preserve this form. Formalize the existing relationships before
proposing changes; missing implementation detail is not missing author intent.

Keep source spelling, note text, ownership, source ranges, and unfinished
structure available to the tools that need them. Do not flatten reasoning into
discarded comments or force strings through a metadata-only representation.

Separate language structure, application meaning, and visible presentation.
Syntax highlighting does not write a file. A formatting repair is a visible,
reversible edit. Reclassifying notes as commands or changing targets is a
meaning change, requiring more than a punctuation fix.

The grammar for an unfinished editor buffer and eligibility to execute an
operation are different questions. Show a useful document while explaining
exactly what prevents an operation from proceeding.

Presentation may dramatize real events. It must not fabricate validation,
progress, repair success, or reversibility. A completed check and a pending
decision must remain distinguishable. Animation must not postpone a result.
Readable text, keyboard access, and reduced motion should preserve the same
experience and status, without a lesser "beginner" edition.

## Scope and evidence

Windows and PowerShell are first. The UDL and the targeted CLI are the initial
operational proof. The extension, note application, and broader format remain
part of the design horizon; their existence must not be claimed prematurely.
Select the stack through relevant experience and integration evidence.
Verify current package-manager behavior when adapter development begins.

Preserve source documents and user edits. Keep design corrections discoverable.
Do not create release labels, conformance claims, passing tests, integrations,
or canonical rules without evidence.

An example is not a parser test. A storyboard is not an inspected application.
A tested parser is not proof that an experience feels good. Report each
kind of evidence for what it establishes.
