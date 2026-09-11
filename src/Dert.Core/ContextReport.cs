using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Dert.Core;

/// <summary>Human-readable framing around exact source slices, not a portable schema.</summary>
internal static class ContextReport
{
    public static string Build(ContextPacket packet, bool leftOut)
    {
        var selection = packet.Selection;
        var document = selection.Document;
        var index = new DertStructureIndex(document);
        var report = new StringBuilder();
        Line(leftOut ? "DERT context selection / Left out" : "DERT context selection");
        Line("Source: " + Safe(selection.SourceLabel));
        Line("Source kind: " + (selection.Kind == ContextSourceKind.FileSnapshot ? "file snapshot" : "editor buffer") +
            " / Primary reasoning note: line " + Number(selection.Primary.LineNumber));
        Line("Included notes: " + Number(packet.Included.Count) + " / Omitted ranges: " + Number(packet.Excluded.Count));
        Line();
        if (leftOut)
        {
            if (packet.Excluded.Count == 0) Line("No source was left out.");
            foreach (var range in packet.Excluded)
            {
                Line("Omitted source / lines " + Number(range.FirstLine) + "-" + Number(range.LastLine));
                Source(range.Text);
                Line("Source range: " + Range(range.Span));
                Line();
            }
            Line("These ranges were left out of the reviewed text. Their source is shown only in this Left out reading.");
        }
        else
        {
            foreach (var note in packet.Included)
            {
                Line("Reasoning note at line " + Number(note.LineNumber) + (ReferenceEquals(note, selection.Primary) ? " (primary)" : " (explicitly included)"));
                Line(DertStructureIndex.Closing(note.Node) is { } closing ? "Closing //: " + Position(closing.Start)
                    : "Closing //: missing; the captured unfinished structure is retained.");
                Source(document.Source.Slice(note.Span));
                Line("Written name: " + Safe(note.Label));
                Line("Source range: " + Range(note.Span));
                Line(note.Parent is null ? "Parent: document" :
                    "Parent: " + DertStructureIndex.Kind(new(note.Parent, null)) + " at line " + Number(note.Parent.LineNumber) + ". Parent source is not implicitly included.");
                var findings = document.Diagnostics.Where(finding => Touches(note.Span, finding.Span)).ToArray();
                Line("Parser findings: " + (findings.Length == 0 ? "none within this note's range." : Number(findings.Length)));
                foreach (var finding in findings)
                    Line("- " + Safe(finding.Code) + " / " + finding.Severity + " / " + Range(finding.Span) + " / " + Safe(finding.Message));
                if (ContextReading.Read(index, note.Node) is { } reading)
                    foreach (var issue in reading.Issues)
                        Line("Reading detail: " + Range(issue.Span) + " / " + Safe(issue.Message));
                Line();
            }
            Line("Omitted ranges (contents not included)");
            if (packet.Excluded.Count == 0) Line("No source was left out.");
            foreach (var range in packet.Excluded) Line("- " + Range(range.Span));
        }
        Line();
        Line("Captured UTF-8 SHA-256: " + selection.SourceRevision);
        if (leftOut) Line("Review SHA-256: " + packet.ReviewId);
        Line("Coordinates use one-based source lines/columns and zero-based UTF-16 offsets; range ends are exclusive.");
        Line("The source excerpts are exact. Their enclosing fences and any separator newlines are presentation, outside the stated source ranges.");
        Line("Effects of this context creation: Nothing was executed or sent to an agent. No agent check was requested.");
        return report.ToString();

        void Line(string value = "") => report.Append(value).Append('\n');
        string Position(int offset)
        {
            var line = index.LineOf(offset);
            var column = offset - document.Source.Lines[line - 1].Start + 1;
            return "line " + Number(line) + ", column " + Number(column);
        }
        string Range(TextSpan span) => Position(span.Start) + " to " + Position(span.End) + " (end exclusive); UTF-16 [" + Number(span.Start) + ", " + Number(span.End) + ")";
        void Source(string text)
        {
            var longest = 0;
            var current = 0;
            foreach (var character in text)
            {
                current = character == '`' ? current + 1 : 0;
                longest = Math.Max(longest, current);
            }
            var fence = new string('`', Math.Max(3, longest + 1));
            Line(fence + "dert");
            report.Append(text);
            // A delimiter needs its own line. The stated range identifies the
            // source exactly even when the author has not written a final newline.
            if (text.Length > 0 && text[^1] is not ('\r' or '\n')) report.Append('\n');
            Line(fence);
        }
    }

    private static string Safe(string text) => JsonSerializer.Serialize(text);
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static bool Touches(TextSpan source, TextSpan finding) => finding.Length == 0
        ? finding.Start >= source.Start && finding.Start <= source.End
        : finding.Start < source.End && finding.End > source.Start;
}
