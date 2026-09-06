# Title-rule repair rehearsal

The user explicitly approved continuing into an exact, explained, reversible
title-rule repair. This develops EXPERIENCE-DESIGN sections 1 and 3; it does not
introduce new DERT syntax or settle the remaining grammar probes.

## The experience

`ERPDERT repair <file>` shows the actual title, the existing and proposed rule,
the width difference, and a local diff. In an interactive terminal it offers an
explicit confirmation, defaulting to leaving the file alone. In a redirected
terminal it previews only, then prints a copyable apply command containing the
approval token. No tutorial, timer, or simulated validation is involved.

`ERPDERT repair <file> --apply <approval-token>` approves the shown edit.
The 64-hex SHA-256 token binds the implementation's action version, operation,
full path, physical file identity, original byte hash, proposed byte hash, and
undo receipt selection. An old undo approval cannot select a new original just
because two repairs produced identical aligned text. `ERPDERT undo <file>` rehearses
the reverse of the most recent title repair; it follows the same confirmation
and action-bound `--apply` contract. Undo never overwrites later author edits.
`--plain` retains the complete interaction without styling.

Copy commands use the actual launched executable, not an assumed PATH install.
They are emitted as one logical line without renderer-inserted line breaks.
PowerShell literal quoting preserves apostrophes; visible `[char]` expressions
handle hidden format/control characters and smart single quotes U+2018–U+201B.

The source report uses the existing warm title, violet structure, and cyan line
colors. Removed and added rule rows receive distinct red/green treatments, with
literal minus/plus signs so color is never the only signal. The title remains
visible above the small diff. A real completed application produces a local
`TITLE ALIGNED` receipt, a recovery location, and a usable undo command.

## Scope and alternatives

The chosen design keeps a complete byte-for-byte original alongside a compact
receipt. A reverse patch alone would be smaller, but would leave less useful
recovery material after a interrupted write. An editor-owned undo stack would
integrate naturally with typing but would not survive closing this CLI. The
on-disk receipt supports this standalone workflow and can feed a later editor.

Only the main title's underline is eligible. Existing first-title recognition
remains provisional and is explicitly visible in the preview. A rule made only
of hyphens, with or without its `//` prefix, can be replaced. Otherwise insert a
new rule immediately after the title; never replace prose on the following line.
Recognized correct rules are unchanged. Structured reasoning cannot become a
title merely to supply a repair.

The current ASCII display-width policy remains explicit. Non-ASCII/control
characters in a title require a future measured width policy; this action must
explain that limit and leave them unchanged. No silent transliteration, trimming
of title words, or speculative Unicode width is permitted.

Source newline style is local to the title. Insertions use its existing newline;
a title ending at EOF without a newline uses CRLF, the declared Windows-first
default. Existing final-newline presence, BOM, remaining lines, punctuation,
annotations, and command text are preserved. Generated rule text is flush-left.

## Revision and recovery contract

The core planner is pure. It returns the source range, exact old/new text, a
plain-English explanation, and the proposed document. Applying that plan to a
different source string is rejected.

The file service reads strict UTF-8 and fingerprints original bytes. It acquires
exclusive Windows access to the target before checking the fingerprint and
physical file identity again. Identity is captured through the opened handle,
not inferred from case-folding the filename.
It retains a flushed original and receipt before touching source bytes, rewrites
through the held handle, flushes and rereads the result, and reports success only
after verification. Normal competing writes/deletes are denied while that handle
is held. A write failure attempts rollback through the same handle; a failed
rollback reports the retained original's exact path, never a success message.

This is a recoverable guarded write, not a power-loss-atomic replacement. Process
termination or storage failure may leave a partial target; the retained original
is the recovery source. Automatic undo will not guess whether unexpected bytes
are an interrupted write or a later human edit. File timestamps change on edits;
the promise of exact reversal applies to file contents, not timestamps.

Receipts live beside the document under `.erpdert/repairs/<identity-hash>/<id>/`,
containing `before.DERT` and `receipt.json`. They are local, unencrypted copies of
the note, never uploaded or automatically deleted. Paths containing reparse
points, hard-linked targets, named alternate streams, UNC/device paths, and
non-Windows mutation are refused explicitly in
this increment. Preview remains available. Unsafe receipt paths are not followed.

The identity combines the Windows volume serial and file index. Receipt IDs use
an increasing local sequence plus a GUID; the recorded timestamp is descriptive,
not the ordering authority. Different files occupying the same path keep separate
originals, including when their repaired bytes are identical. The receipt retains
the original path as provenance. Renaming within the same directory can retain
automatic undo; copying/replacing the file or moving it away from its recovery
storage can disconnect it. Saved originals remain ordinary readable files.

Undo validates receipt version, file identity, original hash, and a recomputed
forward title repair. It requires the current bytes to equal that repair's
result before restoring the original. Corrupt receipts, changed files, locked
targets, and unavailable recovery storage leave the requested source unchanged.
Already restored files are identified without another write. This is undo of the
latest title repair, not a general multi-level editor history.

Path checks and sharing locks protect normal local use. They are not a security
boundary against an actively hostile process replacing filesystem ancestors.
An incomplete/corrupt latest receipt is refused, not skipped in favor of an
older repair that might restore the wrong original.

`repair`/`undo` exit 0 after a completed preview, cancellation, no-op or verified
edit; unrelated lint findings can remain. Exit 1 means repair/undo was refused
or failed, including unavailable width. Invalid command options exit 2.

## Acceptance and proof

Exercise absent/short/long/bare/correct rules; following prose and reasoning;
CR/LF/CRLF, BOM, mixed line endings and EOF; unavailable Unicode width; stale
preview; canceled prompt; applied/undone receipts; edited-after-apply refusal;
locked/read-only/hard-linked paths; corrupt receipts; failed writes and rollback.
Use actual temporary files for file behavior. Inject only the commit step to
reproduce a partial I/O failure deterministically.

Inspect actual terminal preview, cancel, apply, stale refusal, and undo output.
The original source artifacts are used read-only; changes are demonstrated on
generated copies. Rebuild, run the suite, publish, and exercise the executable.
Tests and terminal inspection do not establish clean-machine installation or
human usability; record those limits.

## Platform references

- [FileShare](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileshare?view=net-10.0)
  defines exclusive sharing behavior.
- [FileStream.Flush](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)
  documents requesting a flush through intermediate buffers.
- [GetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle)
  supplies the opened file's link count and attributes.
