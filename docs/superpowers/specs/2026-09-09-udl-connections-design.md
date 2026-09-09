# Give the written language its own connections

Working implementation recommendation, 2026-09-09. The user's latest correction
accepts MER's aesthetic balance and asks for broader UDL assessment, development
and integration. Keep that source window and palette as the visual reference.
Original authority: `docs/archive/original.md` contextual notes, expanded strings,
hook/line fields and notes shared with an agent; `docs/WHO-WE-ARE.md` notes-only
value; `docs/EXPERIENCE-DESIGN.md` section 2 connection-view recommendation.
Review commands and the proposed backlog are application choices, not grammar.

1. Serve someone returning to their own notes, looking for a remembered phrase
   and wanting to see which entry, explanation or structure contains it. This
   experience must be useful without an operational chain or package manager.
2. `find <text>` searches the captured document literally, without case matching;
   `hit N` selects a matching source line. Results name source line and structure,
   page rather than flood the terminal, and retain repeated matches on one line
   as one destination. `owner` follows the actual parsed parent. `structure`
   reveals fields/entries/boundaries beside the current numbered source, including
   at a line otherwise occupied by an operation or diagnostic. `details` retains
   the complete selected structure and search context. No source or process writes.
3. Deepen the original `support doctoring ; conflicts ;;` with its expanded
   annotated entries, and `// HOW ; codex, claude ;; ... //`. Preserve the full
   target field and prose; matching words do not establish an operation binding.
   Both active sinkers and reasoning entries need useful ownership inspection.
4. Alternatives: another parser dump exposes implementation without helping
   navigation; another operation integration delays notes-only value. Recommend
   a shared source index with one attached explanation and literal search. Keep
   the existing margin trace and explicit full-detail escape hatch. Do not add
   nested UI boxes or animated search progress for immediate local work.
5. The parser owns structure. The index projects exact spans, direct parents,
   fields, children and explicit closers; it does not reinterpret grammar. Blank
   spacing has no entry of its own. A closer selects its block/chain and `owner`
   takes it to that opener. Ordinary free notes between active sinkers retain
   their actual parsed parent, not the overlapping statement's extent. Source
   repairs rebuild the index and discard old hit numbers. External edits leave
   explicitly captured search evidence until a fresh MER session is opened.
6. Check unfinished blocks, ordinary prose, duplicate target names, literal URL
   slashes, annotation matches, terminal control characters, invalid hit/page
   numbers, empty searches, repeated use, BOM/CRLF and source repair/undo. Search
   never executes text. Missing closers remain missing; no confident closed trace.
   Pending repairs must be applied/cancelled before navigation can change focus.
7. Build index, presentation, review commands and a shipped notes-only example.
   Keep editor authoring, reversible florin edits, list completion, machine-facing
   contextual interchange and package-changing execution visible in TODO.md.
8. Verify source ownership and exact spans in core tests, commands with real MER
   review, published Windows input, and wide/narrow actual compositor frames.
   Tests own their examples; original documents remain read-only design sources.

## Observed result

The complete review journey runs with a shipped notes-only example. At
`find quiet`, two matches lead to an active sinker's annotation and a reasoning
entry's annotation; `owner` exposes each distinct header and its entries. The
wide view retains the source and an attached field/entry diagram. The 80-column
view retains selected source, hit number, entry, explanation and ownership in a
compact view. Full fields and annotations remain reachable through details.

During review, an interleaved free note exposed why source-span containment is
not ownership. The index follows actual parent edges. The renderer now receives
member lines and the recovered end so it cannot trace through unrelated text;
offscreen markers use eligible members too. The same bounded ownership is
emitted by NoteScore for live/replay events. Title inspection checks for an actual
adjacent RuleNode before describing it. No original grammar/source artifact was
changed to obtain these results.

Evidence inspected:

- Wide frame: `.artifacts/mer-visual/f6de0e3462c74b3385770a266a183b1a/notes-03-owner.png`
- Narrow frame: `.artifacts/mer-visual/1249f0450c88438e9d496bb8d9db970f/notes-00-find-quiet.png`
- Their `notes-review.txt` files retain actual review output. Images are rendered
  compositor frames, not screenshots of an OS terminal.
- The published Windows executable was exercised in a PTY with `find quiet`,
  `hit 2`, `owner`, `structure`, and Enter; it exited 0.
- Fresh source-only verification passed 578 tests and all nine executable suites,
  including 50 MER and 20 update checks. Log: `.artifacts/udl-source-only-final.log`;
  full output: `.artifacts/source-only-proof/700452429d3d444d8aa4a2cd946fcdcd`.

An earlier workspace test run encountered the existing child-cancellation test
failure; its isolated rerun and the final fresh full run passed. No change to the
process runner was made in this increment. All bounded review findings above
were corrected and independently checked. The broader assessment and next
authoring recommendation live in [TODO.md](../../../TODO.md).
