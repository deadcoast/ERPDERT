# Choose what travels with your words

Authority: the user's explicit author-and-agent context-selection TODO; the
original's contextual notes between author and development agent; AGENTS.md's
source/ownership/agency requirements. Portable schemas remain deferred by the
user. The following is an implemented-product design recommendation, not a new
language or portable interchange specification.

## Person and interaction

An author has one explanation to share, together with its own annotations. They
need to see what an agent will receive, include another relevant or disagreeing
note deliberately, and keep the original document intact. The existing reasoning
inspector opens a dedicated **Context selection** review. The current note starts
selected. Other reasoning notes are visible, paged choices; name matches are
identified without automatically selecting them or claiming contradiction analysis.

The review shows the exact text to copy/export and a **Left out** reading of the
excluded source. Both readings retain ranges. Copy and Export use the same text;
Back/Escape returns to the original inspector and source selection. A dedicated
review gives the full text enough space and keeps the ordinary inspector focused.
No agent connection, upload, credential, model choice or installed editor is needed.

Alternatives considered: raw selection loses ownership/provenance; automatically
collecting nearby or similarly named notes invents intent; a portable data schema
would preempt deferred decisions. Choose an explicit, human-readable text review
over original source slices, with deliberately included notes and visible omissions.

Example deepened from the original: select `// support doctoring ; conflicts ;;`
with its three annotated entries and own `//`. An adjacent HOW explanation is
initially left out, available to include. Application requests in the enclosing
chain are not pulled into the selection. A missing closer remains missing and
explained in both the review and exported text.

## Meaning and exact effects

- One captured parsed revision owns all choices. The primary reasoning note
  cannot be removed; additional notes are selected by their actual opener, in
  source order. Entry/closer selection resolves to its actual reasoning owner.
  Ordinary prose has no invented owner and receives useful selection guidance.
- Excerpts retain exact source spans, punctuation, annotations, BOM when present
  in the selected range, and mixed newlines. They are framed separately, never
  joined into an invented executable chain. Whole-note spans include authored
  spacing. Unselected source is represented by complete complementary ranges.
- The human-readable text identifies file or unsaved buffer, source kind,
  SHA-256 of the captured UTF-8 text, one-based line/column ranges and zero-based
  UTF-16 offsets (end exclusive). It carries real parent/boundary descriptions,
  relevant parser findings and an explicit list of omitted ranges. This is
  source context, not evidence that an operation ran or an agent understood it.
- Dynamic code fences cannot be closed by authored backticks. Source slices stay
  literal. Metadata is safely displayed. Incomplete Unicode characters refuse
  copying/export until complete, without changing the editor buffer.
- Copy writes only the reviewed text to the clipboard, with success shown after
  the clipboard call succeeds. Export writes a new UTF-8 `.txt` file, checks the
  bytes, and refuses existing destinations. It never saves the source document.
  A failed write explains that the destination may be incomplete; no false
  success or recovery claim is made.
- Source changes, document replacement, changed identity or pending source-edit
  proposals invalidate old native actions. The review can be left and reopened
  from current source; it never rebinds old line-number choices silently.

## CLI and MER integration

`DERT context file.DERT --at N [--include N ...]` previews one explicit selection
in the established source window. `--details` reveals the complete reviewed text;
`--text` emits that text exactly for deliberate redirection/copying. Default
preview reports included/excluded counts and full revision/review identity. Plain
and reduced-motion delivery preserve meaning. No artificial progress is added.

`--output new.txt --from-preview ID` exports only if the rebuilt complete selection
matches the reviewed ID. Changed source, path or selected notes refuses the old
ID. The output path must be new. Export is an explicit local action.

MER `context` previews the selected reasoning note; `context notes N` pages all
reasoning choices; `context include N` and
`context exclude N` change deliberate note choices; `context details` prints the
complete reviewed text; `context left` reveals omitted source; `context clear`
leaves that review. The complete CLI handoff carries the source revision and
selected notes. Saved repair/undo clears captured selections. Pending comparisons
continue to own their choices. No context command invokes application operations.

## Break the design and evidence

Exercise unfinished and missing boundaries; duplicate targets; contradictory
prose; unrelated operations; blank lines; quoted commas; backtick fences; BOM,
mixed newlines and Unicode; stale revisions and source replacement; canceled
and failed clipboard/export actions; repeat visits, native selection and Undo;
many selectable notes and narrow/tall layouts. Source and test-owned fixtures
remain separate from live documentation. Verify shared projection, native
controls, CLI/MER, published integration and actual wide/narrow renders.

Portable schemas, schema versions, cross-file identity, automatic contradiction
analysis, remote agent delivery, imported receipts and changes to DERT grammar
remain separate work. Exact copying does not establish downstream agent behavior.

## Delivery ledger

- [x] Shared source selection, exact report and guarded new-file export.
- [x] Native review, explicit inclusion/exclusion, copy/export and return.
- [x] CLI/MER selection and revision-bound export handoff.
- [x] Independent review, visual checks, source-only suites and package refresh.
- [x] Documentation and next TODO updated with evidence and remaining scope.

## Implementation decisions and revisions

The shared `ContextSelection` owns a parsed revision and real reasoning-note
choices. `ContextPacket` builds the selected report and a separate Left out
reading. Whole source ranges preserve annotations, blank lines and mixed newlines;
omitted ranges form the exact complement. The primary note remains selected.
`ContextExport` writes strict UTF-8 to a new `.txt` through one exclusive handle,
then verifies the bytes. Existing paths, the captured source path and Windows
alternate streams are refused. A failed/interrupted write retains its incomplete
destination with an explanation, not a claimed successful export.

The native review is an owned modal window, reached through one action in the
reasoning inspector. Paging bounds the choice controls; Preview and Left out
show their complete text in a scrolling read-only source surface. Copy/Export/Back
stay above that body at both inspected widths. Left out requires returning to
Preview before delivery. Source, identity and pending proposals are rechecked at
the actual action; old choices are never rebound to newly occupied line numbers.

Actual render inspection found the first source excerpt buried under metadata.
The report now places the source within nine lines, with its boundary immediately
above. Ranges, parents, findings, revision and the coordinate guide remain intact
after the excerpt. This is a presentation decision, not a grammar change.

Independent review reproduced two clipboard issues without changing the OS
clipboard. Embedded NUL survived the UTF-8 model but truncated native UnicodeText;
Copy now refuses it with exact-file export guidance. AvalonEdit's focused inner
TextArea also handled Copy before the outer control's binding. Tunneling command
handlers now intercept the actual route. Regressions target that TextArea and
cancel native DataObject copying, verifying full-report copy, Left out refusal,
NUL refusal and the common action guard. Follow-up review confirmed the fixes.

CLI default output reuses MER's source window; the full report remains available
through --details/--text. The source file stays read-locked through capture/export;
the review ID covers the entire report, including source identity, full-source
revision, selected notes and omitted ranges. MER names selected opener lines
separately from its actual ownership trace. Its narrow footer retains `source
unchanged`. A copied export command was executed against disposable quoted and
Unicode paths; the resulting file equals --text output exactly.

No report or animation claims that an agent received, understood or agreed with
the words. This increment did not add a portable schema or reference grammar.

## Final evidence and limits

Fresh source-only verification passed **1,079 tests** (942 core/CLI/MER, 137 native)
and **all eleven executable suites**, including **60 MER** and **21 published Notes
checks**. This increment adds 52 core/CLI/MER and 13 native cases. All **243**
current source/test inputs match the proof copy. Documentation and original user
notes were excluded from the build and test inputs.

Inspected actual native control renders at 1180/780 pixels, plus Left out,
unfinished source and a fourth page of choices. Actions remain above the scrolling
body; missing boundaries appear before excerpts. Inspected actual MER engine
frames at 110/58 columns: selected opener lines, omissions and source-unchanged
status remain visible. These inspections led to the source-first report and
shorter footer described above. Independent follow-up review confirmed the real
TextArea copy route and common guards with no remaining material findings.

The root Windows package was refreshed and passed 21 published Notes checks.
Bundled `context-selection.DERT` passes lint and context selection with identical
source SHA-256 before and after. Executable tests run the generated review-bound
export command using a quoted Unicode path, compare its bytes with exact `--text`
output, and refuse existing destinations and stale choices. Native control tests
exercise the actual review controls; published Notes smoke establishes adjacent
host launch, normal close and source-byte retention.

Local evidence:

- `.artifacts/source-only-proof/f03ea67463694529b83a306f26f053a9/output.txt`
- `.artifacts/selection-root-publish.log`, `.artifacts/selection-root-notes-smoke.log`
- `.artifacts/selection-example-lint.log`, `.artifacts/selection-example-context.log`
- `.artifacts/selection-native-final.log`
- `.artifacts/context-selection-native/context-selection-1180.png`
- `.artifacts/context-selection-native/context-selection-780.png`
- `.artifacts/context-selection-native/context-selection-left-out.png`
- `.artifacts/context-selection-native/context-selection-paged-780.png`
- `.artifacts/context-selection-native/context-selection-unfinished.png`
- `.artifacts/mer-visual/29b43fd0f4384db59cd57f98e517e0af/held.png`
- `.artifacts/mer-visual/1d6fd7a8c7ae43fdaa45b2e73c02f4e3/held.png`

Clipboard tests inject the final writer and intercept native serialization without
changing the OS clipboard. Cross-application clipboard round trips, screen-reader,
IME and human usability acceptance remain unproven. No external agent was
contacted. Results returning to useful notes are the next recommended TODO;
portable schemas, cross-file identity and automatic agent delivery remain deferred.
