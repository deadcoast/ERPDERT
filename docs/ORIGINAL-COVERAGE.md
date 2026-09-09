# Original examples: what the current build actually reads

Assessment, 2026-09-09. This closes the coverage **map**, not the language design.
The [original notes](archive/original.md), [original corpus](../RERDMERR.DERT),
later user decisions and [repository instructions](../AGENTS.md) retain authority.
The syntax lab's C/T identifiers are traceability labels, not grammar productions.

Original files were read for this reconciliation and remain unchanged. Builds,
tests and smoke checks use independent files under `tests/Fixtures` or literal
test-owned input. The mixed fixture represents the corpus's composition; it is
not a checksum lock on the author's editable notes. No test reads this document.

## The thirteen source families

Each C row has a named semantic assertion in
[OriginalExampleCoverageTests](../tests/Dert.Tests/OriginalExampleCoverageTests.cs).
Its `Every_family_retains_exact_spelling_tokens_and_field_ranges` theory also
checks every listed fixture: token reproduction, exact field ranges and unchanged
source through application-plan construction. A passing reading is not a claim
that every family is an executable operation.

| Family | Test-owned fixture / semantic test prefix | Current reading and limit |
| --- | --- | --- |
| C01 title/rule | `C01-title-rule.DERT` / `C01_title_rule` | Original long title and short TITLE each match their rule. A bare or wrong-width ruler remains a separate title repair. Unicode width is covered under T19. |
| C02 adjacent reasoning and annotated nest | `contextual-notes.DERT` / `C02_original_reasoning` | Two closed notes retain raw targets and prose; the block owns three entries, each annotation and its standalone closer. The original final comma stays present. Its notes never request an application operation. |
| C03 compact invocation | `C03-compact.DERT` / `C03_original_compact` | One ChainNode, original ERPDERT target, codex payload, no authored closer. The later user decision maps this retained spelling to `DERT update codex`. EOF capture is visible; no closer is invented. |
| C04 `STERT ;`, active doctoring list, `fin.` | `C04-decorated-opener.DERT` / `C04_decorated_opener` | STERT opens a chain; the hook has an empty target. The active list has three annotated entries, including the final comma notice. Typed fin. owns the ending and qualifies for the florin convenience. Running this sample is refused: the request is incomplete and the doctoring statement has no handler. Acceptance as source is not a canonical decorated-opener decision. |
| C05 DERCS/help and two-field command | `C05-dercs-help.DERT` / `C05_DERCS` | DERCS is retained as the chain's target. `command option` stays one head phrase; `variable` is its hook target, with no line or payload. Original help intent is explained. This build has no DERCS binding; runner advice points to `DERT --help`. |
| C06 historical `START ;` | `C06-historical-hook.DERT` / `C06_historical_START` | START with a hook is a StatementNode, followed by a separate two-field statement. It opens no chain and is not rewritten to STERT. The views identify the historical spelling. |
| C07 historical START and line-only header | `C07-historical-line.DERT` / `C07_historical_START` | Bare START is TextNode. `command option variable` is one head before the line; there is no hook target. Its one active sinker retains a terminal comma and the contradictory annotation saying the last item has none. The notice reports continuation; it does not choose which original wording to replace. |
| C08 loose names then a doctoring header | `C08-loose-fragments.DERT` / `C08_loose_names` | Four top-level text fragments retain commas/annotations and stay outside the preceding reasoning note. The later explicit statement owns only its three following active sinkers. No application is inferred from the loose names. |
| C09 empty updateme action | `C09-empty-action.DERT` / `C09_empty_action` | The original START excerpt contains an empty statement payload but no bound operation. Separately, `commands ; updateme ;;` is a supported zero-payload action inside a bound update request. Grammar does not make every empty payload incomplete or every named action executable. |
| C10 hook/line and one active sinker | `C10-active-sinker.DERT` / `C10_hook_line` | Bare STERT owns the chain. `command option` is the head, `variable` the target, and the empty line payload owns one expanded string with its annotation. Its lack of a final comma ends that list. It is distinct from C05 and C07. |
| C11 free hook versus closed reasoning | `C11-free-hook.DERT` / `C11_a_hook_alone` | `// STERT ; starts...` stays NoteNode; a hook alone cannot promote it into reasoning. The DERT/hook/line/closer example is a ReasoningNoteNode. The current first-free-note title heuristic may give the first note title presentation; that separate heuristic is not a command interpretation. |
| C12 identity phrases and prose | `C12-identity-prose.DERT` / `C12_original_identity` | The Ermahgerd phrase remains the full reasoning head; ERKER and DERT remain raw fields. Long prose, internal punctuation and voice stay intact. No reserved operation is inferred from these names. |
| C13 multiple chains and trailing fragments | `mixed-structures.DERT` / `C13_mixed_family` | Three explicitly ended chains retain their own children. Four trailing text fragments and the later active statement remain top-level. Unsupported requests prevent running the composition; it is a readable source collection. C01-C12 cover the original families individually, while this fixture verifies their coexistence. |

Fixture paths above are under [tests/Fixtures](../tests/Fixtures). The original
corpus is not passed into tests to enforce its prose, location or current edits.

## The recognizer and interaction challenges

**Verified** describes the stated current behavior. **Bounded** identifies a
narrower implemented part and keeps the larger requested behavior visible.
**Deferred** is design work with no implementation claim. Test class links below
lead to the assertions; UI evidence is recorded separately in the delivery record.

| Probe | Disposition | Evidence and exact scope |
| --- | --- | --- |
| T01 URL in closed reasoning | Verified | [RecoveryTests](../tests/Dert.Tests/RecoveryTests.cs): `Url_slashes_do_not_become_context_annotations`; URL retained and terminal closer recognized. |
| T02 original HOW comma | Verified | [ContextualNoteTests](../tests/Dert.Tests/ContextualNoteTests.cs) and C02 retain the entire sentence and multiple named targets as raw fields. No comma-based metadata schema is inferred. |
| T03 free-note hook | Verified | `Semicolon_in_a_free_note_does_not_open_structured_reasoning` in RecoveryTests; C11 also tests the original pair. |
| T04 adjacent closed notes | Verified | C02 and ContextualNoteTests retain two owners and exact field ranges. [ReasoningBoundaryTests](../tests/Dert.Notes.Tests/ReasoningBoundaryTests.cs) covers removal beside another note. |
| T05 annotated entry | Verified | ContextualNoteTests separates value, continuation and annotation. [SourceTests](../tests/Dert.Tests/SourceTests.cs) verifies distinct opening/annotation color roles. |
| T06 missing closer before active text | Verified | RecoveryTests retains the recovered block and following statement with DERT1001. [DertRunTests](../tests/Dert.Tests/DertRunTests.cs) verifies that parser errors prevent partial execution. A recovery stop remains an unresolved boundary. |
| T07 terminal comma at EOF | Verified | C04/C07 retain the comma and DERT3003 notice. [ListCompletionTests](../tests/Dert.Tests/ListCompletionTests.cs) and native [ListAuthoringTests](../tests/Dert.Notes.Tests/ListAuthoringTests.cs) offer author-selected Add/Finish previews. |
| T08 comma before a new header | Verified | RecoveryTests `New_header_after_continuation_comma_is_not_consumed_as_a_sinker`; ListCompletionTests preserves a separate following header. |
| T09 comma inside English text | Verified for raw fields | C02, ContextualNoteTests and SourceInterpretationTests preserve internal prose punctuation. Typed HOW/similarities schema interpretation remains deferred. |
| T10 literal markers and trailing comma | Bounded | RecoveryTests preserves quoted markers and unfinished quotes. ListCompletionTests `Quoted_punctuation_and_literal_commas_survive_completion` retains literal punctuation. This is the current double-quote/backslash recognition behavior, not a settled portable escape grammar. Work through authoring convenience next. |
| T11 cursor after `STERT ;` | Verified | [SourceReadingAuthoringTests](../tests/Dert.Notes.Tests/SourceReadingAuthoringTests.cs) retains the hook through boundary handling, accepts continued native typing, updates its explanation and restores it with Undo. C04 records its runner restriction. |
| T12 note/quote/owned `fin.` | Verified | [FlorinGoodbyeTests](../tests/Dert.Tests/FlorinGoodbyeTests.cs) and native [FlorinAuthoringTests](../tests/Dert.Notes.Tests/FlorinAuthoringTests.cs) distinguish actual endings, prose and quotes. [SourceReadingTests](../tests/Dert.Tests/SourceReadingTests.cs) also prevents loose goodbyes receiving boundary color. |
| T13 spacing/free notes between sinkers | Verified | RecoveryTests `Expanded_sinker_values_keep_order_and_inline_annotations` and [StructureIndexTests](../tests/Dert.Tests/StructureIndexTests.cs) retain source order and actual parents. Free notes/blank lines do not become list values. |
| T14 duplicate named targets | Verified as unresolved names | [SourceInterpretationTests](../tests/Dert.Tests/SourceInterpretationTests.cs) retains two source ranges, intact reasons and no operation steps. [MerStructureTests](../tests/Dert.Tests/MerStructureTests.cs) shows claimed names without a resolved binding. Cross-note resolution remains a separate feature. |
| T15 flush-left annotated doctoring nest | Verified | C02, ContextualNoteTests and SourceTests preserve entries, annotations and their own closer in column one. The preceding reasoning inspection increment verifies folded reading and exact source editing. |
| T16 ordinary prose without STERT | Verified | StructureIndexTests, DertRunTests and MerStructureTests retain useful text/notes with no inferred application request. No chain is required for notes-only use. |
| T17 empty payload before goodbye | Verified for supported schemas | C09 and [EcosystemCommandTests](../tests/Dert.Tests/EcosystemCommandTests.cs) separate supported zero-payload updateme/doctor actions from incomplete application requests and unsupported statements. |
| T18 rule repair/undo/deletion | Verified in separate flows | [RepairFileTests](../tests/Dert.Tests/RepairFileTests.cs) verifies exact repair/undo bytes. Native [AuthoringTests](../tests/Dert.Notes.Tests/AuthoringTests.cs) verifies title Undo and deliberate rule deletion without reinsertion, while a new explicit preview remains available. No general formatter oscillation claim is made. |
| T19 non-ASCII title/newlines | Verified within declared policy | [UnicodeTitleTests](../tests/Dert.Tests/UnicodeTitleTests.cs), [UnicodeTitleAuthoringTests](../tests/Dert.Notes.Tests/UnicodeTitleAuthoringTests.cs) and SourceTests cover width, font-shaped rules, grapheme spelling, BOM and newline retention. Cross-terminal font equivalence and IME remain unproven. |
| T20 stale repair | Verified | [TitleRuleRepairTests](../tests/Dert.Tests/TitleRuleRepairTests.cs) rejects changed source; RepairFileTests rejects external edits, identity replacement and changed Undo previews. Native previews also refresh against source changes. |
| T21 reformat/reparse a valid nest | Bounded | SourceInterpretationTests `T21_title_repair_does_not_reformat_an_already_valid_annotated_nest` verifies a supported edit leaves its bytes, values, order, annotations, ownership and flush-left layout intact. [NoteClosureRepairTests](../tests/Dert.Tests/NoteClosureRepairTests.cs) verifies explicit boundary repair effects. A general nest formatter does not exist. |
| T22 parse/print without formatting | Verified source retention | Every C family reconstructs exact source from tokens and checks field spans. [CliTests](../tests/Dert.Tests/CliTests.cs) verifies inspect/lint preserve file bytes. Inspection adds display UI; it is not a source serializer or formatter. |
| T23 full corpus coverage | Verified family accounting | C01-C13 above account for the source's current families. The mixed fixture tests complete chains beside loose fragments. No test consumes or freezes the live RERDMERR corpus, and no whole-corpus executability is claimed. |
| T24 proposed operation receipt import | Bounded | SourceInterpretationTests parses the proposed example into an inert reasoning block; its semicolon-bearing child text stays raw entry values and produces no operations. Receipt import/export, provenance validation and attachment are not implemented. This does not validate the fictional observation as a real result. |
| T25 proposed receipt identity | Deferred beyond this TODO | Chain identity generation, portable attachment on move/copy and duplicate identity resolution have no implementation here. The experience-design shape remains a proposal. |

## Decisions remain separate from passing tests

- `STERT ;` is a preserved chain header with an empty target. The runner needs an
  actual application request. Whether it is a canonical decorated opener remains
  unresolved; no formatter removes its hook.
- START remains the observed historical spelling. The parser currently retains
  it as text or a statement, including its annotation. No alias or spelling edit
  is introduced. Quoted START, prose and named note targets get no command advice.
- The parser captures a chain to its authored ending, the next STERT or EOF.
  Only the first supplies an explicit closing span. Viewers now say where capture
  stopped. Existing operation eligibility is preserved; these messages establish
  neither an implicit-closure grammar nor a new required-closer rule.
- DERCS has explicit help intent in the original but no implemented field schema
  or binding. The runner explains that and points to `DERT --help`. It does not
  convert a help request into update or where.
- Unknown reasoning retains raw data. The proposed receipt's internal semicolons
  are entry text under the current nest grammar, not newly typed child headers.
- General formatting, quote/escape canonicalization, first-title recognition,
  optional expanded-chain closure and cross-note identity remain decisions with
  concrete source cases. The next bounded TODO is literal punctuation authoring.

Implementation, review, visual and executable evidence are recorded in the
[delivery record](superpowers/specs/2026-09-09-original-coverage-design.md).
