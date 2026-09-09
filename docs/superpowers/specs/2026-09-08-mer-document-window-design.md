# MER: the document needs its own surface

2026-09-08. Corrective implementation record, not a language specification.

## Authority and failure

The user's screenshot of the packaged `ERPDERT mer` command showed source
introduced by a bare colored path, followed by a long alternating-color report.
The user explicitly requires a distinct, slightly animated ASCII UI around the
DERT source, retention of line numbers, and restraint elsewhere. The previous
completion claim relied on engine/tests evidence that did not establish this
delivery requirement. This correction governs the earlier evidence-handoff record.

The file-tab shape, sizing, annotation placement and review commands below are
implementation recommendations made under that request. They are not additional
user decisions. Original source artifacts and grammar remain unchanged.

## Person and interaction

An author opening their note should recognize their own words immediately and
understand which line an explanation concerns. Reading notes is a complete use;
there is no required repair or progression into commands.

`ERPDERT mer` leaves one ASCII document window in the terminal. It contains a
file tab, original line numbers, blank source lines, syntax coloring, and a hook
in a separate margin selecting the relevant line. Explanations attach outside
the source border at wide widths and below it at narrow widths. The check log
does not replace this window at completion.

Interactive input stays small: `at <line>` inspects captured source, `details`
opens the complete record, `repair` shows available preview commands, `back`
restores the default focus, and Enter ends the review. Redirected output has no
input prompt. `--details` prints the full record and repair commands directly.

## Construct and worked example

The original title/rule construct remains the source of the title experience:

```dert
// WEEKEND
// --
```

The window retains those exact two lines. Its line-2 annotation says `5 marks
short.`, labels the edit `Preview only / not applied`, and shows the original
`// --` alongside proposed `// -------`. Neither preview changes the document.

Selecting a contextual entry such as `// music // Keep the playlist.` shows its
observed owning note and actual closing `//` line, or the missing closer. All
serialized source stays in column one; the border and gutter are presentation.

## Alternatives and recommendation

A colored path plus a full log failed the user's explicit source-UI requirement.
A framed panel around every check would multiply the competing regions the
user rejected. A full-screen editor would introduce navigation and lifecycle
requirements beyond this correction. The selected experience gives the authored
document one persistent boundary and attaches one explanation at a time. Full
evidence and existing repair choices remain available when requested.

## Meaning and actual effects

Raw styled source lines and their original numbers travel separately from the
legacy display gutter. Wrapping preserves source whitespace and graphemes; the
visible continuation row has no invented number. No formatter or source edit is
involved. Source ranges, diagnostics, parsed ownership, original bytes, and the
timestamp all belong to the captured analysis. Review does not reread the file.
Repair links retain the captured byte hash and refuse changed source.

Motion is driven by observed journal evidence. One edge mark traces toward the
selected line for up to 480 ms. It does not animate the source punctuation,
invent validation, loop after settling, or delay a live result. Recorded replay
retains its explicit label and pacing. Failure stops the trace immediately;
reduced motion preserves the same document and status. The small status footer
distinguishes attention, failure, work in progress, and completed analysis.

## Adverse cases and repeated use

- Unfinished notes remain source with an explanation. No guessed ending or
  operation is inserted. Failed reads show source unavailable and the failure.
- Ordinary prose stays readable, without an invented operation or repair offer.
- A mistaken review command leaves the analysis unchanged and explains inputs.
- The title preview stays separate from authored text; warnings remain attention
  after analysis finishes. A settled read never claims pending checks completed.
- Small windows identify excerpts. Long lines wrap; omitted continuation rows
  still count as an excerpt even when their source line number is visible.
- At extremely small sizes the status view explains that the source window needs
  more room and points to `--details`. Large gutters stay separate from source.
- Large documents wrap only the source near the viewport on each frame; explicit
  details still prints everything. This avoids a measured multi-second rendering
  delay on a 500-note live analysis.
- Enter/EOF releases the prompt. Cancellation retains the existing completed
  replay or partial live receipt. No repair or operation runs during review.

## Evidence and limits

Regression coverage includes compact default output versus explicit detail,
review after an external file change, empty documents, bounded source access at
5,000 lines, viewport/line-number bounds, the one-shot edge trace, and the
read-settled transition. Executable checks cover actual shipped source, Unicode,
unchanged BOM/CRLF bytes, damaged/unfinished input, and copied repair commands.

Wide and narrow actual compositor frames were rendered and inspected. A Windows
PTY was used for the animated command and recorded-line review. The harness
defaults to `TERM=dumb` and `NO_COLOR=1`; animation inspection explicitly used a
capable terminal environment. Exported PNGs are rendered frames, not OS screenshots.

Independent code review found premature completion wording, a narrow footer
overlap, and excessive source wrapping; all were corrected and rechecked. In
the same warmed in-process benchmark, 500 inline notes fell from about 5,265 ms
to 609 ms of live rendering with output discarded. This measures renderer cost,
not perceived terminal quality or package-manager speed.

This implements the source-window correction. The broader motion language,
demo/update presentation, complete repair interaction, editor and note application
remain separate work. Test success and these inspections do not establish that
the user accepts the design. Verification counts and artifacts are recorded in
the changelog after the final build.
