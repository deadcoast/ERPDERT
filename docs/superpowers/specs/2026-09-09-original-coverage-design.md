# Original examples, actual readings

Authority: the next TODO requested by the user, original language notes and corpus,
the later DERT/update/where decision, and AGENTS.md. This is a bounded audit and
correction of existing behavior. It does not decide a canonical grammar by test.

1. Serve an author or developer checking whether their written forms retain the
   meaning, identity and relationships shown in the original. Inspection should
   explain the current reading without quietly changing their note.
2. Open original-shaped test-owned examples in inspect, MER and Notes. Show the
   real captured extent of an unterminated chain, distinguish historical START
   and an empty STERT hook, and give DERCS its actual help-oriented explanation.
   The operation preview must separately state what can run in this build.
3. Reconcile C01-C13 and T01-T24 from the syntax lab. Retain T25's proposed receipt
   identity as deferred design. Preserve original documents; fixtures are manually
   maintained independent inputs, never extracts read by builds or tests.
4. Alternatives: only mark old table cells complete; expand the grammar to make
   every excerpt runnable; or publish an evidence map and correct mismatched
   readings in the existing surfaces. Choose the third. Unsupported forms deserve
   useful explanations, while historical ambiguity remains visible.
5. Keep exact spelling, tokens, fields, annotations, source ownership and all
   unknown text. No source edit follows from inspection. A captured chain ending
   at EOF or another STERT is a parser extent, not an authored closer. DERCS stays
   an unimplemented application binding with usable CLI help guidance. START
   remains preserved as the parser currently reads it; no silent alias is added.
6. Challenge compact requests, adjacent chains, EOF, a hook still being typed,
   a quoted/annotated START, standalone fin., missing reasoning closers, comma
   contradictions, unknown metadata and detached application names. Bind no note
   target to a command. Keep zero-payload action validity in the application.
7. Add family fixtures with semantic assertions; map existing adversarial and
   native evidence; share source-reading explanations across the three viewers.
   Correct concrete presentation/binding advice gaps. Record unsupported DERCS,
   general formatting, receipt import and literal grammar decisions explicitly.
8. Exercise focused regressions, independent review and source-only validation.
   Inspect actual MER and native frames at wide/narrow sizes. Refresh the package
   and distinguish rendered/control evidence from real OS input and usability.

## Delivery checklist

- [x] C01-C13 fixture and assertion coverage; T01-T24 evidence disposition.
- [x] Consistent captured-boundary, historical/unfinished-form explanations.
- [x] Correct DERCS help advice and standalone goodbye highlighting.
- [x] Review, inspected views, source-only checks, publish and documentation.

## Implemented and inspected

[The complete map](../../ORIGINAL-COVERAGE.md) ties each source family to a fixture
and named assertion, and each challenge to evidence with its precise limit.
Eleven added C-family fixtures join the existing contextual/mixed fixtures. Each
family has semantic assertions and exact token/field-range checks. The mix is a
test-owned composition of the original shapes, not the live original corpus.

`DertSourceReading` shares source-form and captured-chain explanations across
the inspect command, MER's attached source context and Notes. The grammar and
application eligibility remain unchanged. The parser correction only removes
boundary color from goodbyes with no owning chain. The application correction
gives unsupported DERCS a help-specific explanation and `DERT --help` guidance.
No source rewriting, silent START alias or operational receipt replay is added.

The existing source window, line-number gutter, restrained connection trace and
Notes palette are retained. Inspection of 110/58-column MER frames and 1180/780-
pixel native frames confirmed the new explanations in their actual composition.
The new `tests/Export-MerVisualProof.ps1 -DocumentPath ... -DocumentWindow
-StructureView -AtLine ...` path captures the real source adapter and compositor;
it remains a render harness, not an OS screenshot or pointer-input test.

Independent review identified an annotated bare START that lacked its historical
explanation and a narrow DERCS view whose crowded compact fields displaced its
extent. Regressions failed before the fixes, then passed. Exact matching now
uses quote-aware annotation boundaries. Render inspection then exposed that
retaining an extent row in the data was insufficient: wrapping still pushed it
out of the 58-column view. Actual compositor regressions at 58 and 80 columns
now verify the displayed meaning. Shared form readings provide a full explanation
and a concise summary; compact context places extent immediately after its title.
All source fields remain in the document and full detail. Final review inspected
the corrected 58-column frame and found no concrete remaining blocker in scope.

## Verification, 2026-09-09

- **888 tests passed** from fresh source/test inputs: 807 core/CLI/MER and 81
  native; **all eleven executable suites passed** after fresh publish in that
  isolated copy. This increment adds 49 core/CLI and 9 native cases. No original
  notes or documentation were present as verification inputs.
- Full evidence: `.artifacts/coverage-source-only-final.log` and
  `.artifacts/source-only-proof/7a7f37c32ff340c1b9afee1cdcc63646/output.txt`.
  Focused regression evidence: `.artifacts/coverage-red.log`,
  `.artifacts/coverage-annotation-red.log`, `.artifacts/coverage-narrow-red.log`,
  `.artifacts/coverage-rendered-red.log` and `.artifacts/coverage-focused.log`
  (49 passing cases).
- Actual rendered native controls: `.artifacts/source-reading-host-proof/`
  `source-reading-1180.png` and `source-reading-780.png`. Actual MER frames and
  ANSI transcripts: `.artifacts/mer-visual/a11d12b1e115476682bbadb815d9ee60/`
  and `.artifacts/mer-visual/76c3b608ccd34941a08b40cbb14ac5f2/`. The final
  narrow DERCS frame is `.artifacts/mer-visual/6eaae9d7e9414389ac1478b077eae938/held.png`.
- The root Windows package was refreshed: `.artifacts/coverage-publish-final.log`.
  Its existing 13 Notes launch/byte-retention checks passed in
  `.artifacts/coverage-notes-published-final.log`. The published inspect and run
  preview were exercised on the new `examples/as-written.DERT`: correct source
  extent, DERCS help advice, expected refusal of unsupported sample requests and
  unchanged SHA-256. Transcripts: `.artifacts/coverage-inspect-published.log` and
  `.artifacts/coverage-run-published.log`.

Test results establish current implementation, not canonical status for START,
decorated openers, implicit closure, quoting or arbitrary zero-payload actions.
Receipt-shaped data is verified as inert raw reasoning; import, attachment and
identity resolution remain unimplemented. General formatting also remains absent.
Native tests use real controls, routed commands and text input. OS clipboard/IME,
real pointer-input runs, accessibility tooling, clean-account installation and
human usability remain separate evidence gaps. No stronger claim follows from
the rendered frames. The next bounded TODO is literal punctuation authoring.
