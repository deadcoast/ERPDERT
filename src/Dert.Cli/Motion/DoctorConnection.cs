using Dert.Cli.Doctoring;
using Dert.Mer;

namespace Dert.Cli.Motion;

// One relationship, shown beside its source in MER and unfolded in CLI scrollback.
// Inspection facts remain separate from the selected repair and its pending approval.
internal sealed class DoctorConnection(DoctorReport report)
{
    internal IReadOnlyList<InkLine> Receipt(int columns)
    {
        var diagnosis = report.Diagnosis;
        var width = Math.Clamp(columns - 1, 1, 132);
        var rail = width >= 38;
        var lines = new List<InkLine>();
        var heading = diagnosis.State switch
        {
            DoctorDiagnosisState.Confirmed => "CONFLICT CONFIRMED / external PATH",
            DoctorDiagnosisState.Possible => "POSSIBLE CONFLICT / evidence only",
            DoctorDiagnosisState.Incomplete => "CONFLICT CHECK INCOMPLETE",
            _ => "EXTERNAL ROUTE CHECK"
        };
        Add(heading, Ink.Copper, rail ? " .-- " : "");
        Add(diagnosis.Summary);
        if (diagnosis.State == DoctorDiagnosisState.Confirmed)
        {
            Fact("First external route", diagnosis.LeadingPath!);
            Fact("Missing entry point", diagnosis.MissingEntrypoint!);
            Add("This route hides the later copy in external PATH order.", Ink.Copper);
            Fact("Later identified copy", diagnosis.LaterPath!);
        }
        foreach (var reason in diagnosis.Reasons) Add(reason, Ink.Muted);
        Add(diagnosis.Scope, Ink.Muted);
        if (rail) lines.Add(InkLine.Plain(" '-- ;;", Ink.Muted));
        return lines;

        void Fact(string label, string path)
        {
            Add(label, Ink.Signal, rail ? " |-- " : "");
            Add(path, Ink.Text, rail ? " |   " : "", exact: true);
        }
        void Add(string text, Ink ink = Ink.Text, string? prefix = null, bool exact = false)
        {
            prefix ??= rail ? " |   " : "";
            var contentWidth = Math.Max(1, width - prefix.Length);
            var parts = exact ? WrapPath(text, contentWidth) : TerminalText.Wrap(text, contentWidth);
            var first = true;
            foreach (var part in parts)
            {
                lines.Add(new(new(first ? prefix : rail ? " |   " : "", Ink.Muted), new(part, ink)));
                first = false;
            }
        }
    }

    internal DocumentAnnotation Annotation(int sourceLine, string application)
    {
        var diagnosis = report.Diagnosis;
        var ready = report.State == DoctorState.Ready && report.Proposal is not null;
        var confirmed = diagnosis.State == DoctorDiagnosisState.Confirmed;
        var choice = report.State == DoctorState.Interrupted ? "Doctor stopped / interrupted" :
            report.State == DoctorState.ChooseCopy ? "Choose a copy / then review its repair" :
            ready ? "Reinstall proposal / awaiting choice" : "Doctor checks need attention";
        var heading = diagnosis.State == DoctorDiagnosisState.None ? "Doctor / " + application : Heading(diagnosis.State);
        var lines = new List<InkLine> { InkLine.Plain(heading, Ink.Copper), InkLine.Plain(choice, Ink.Signal) };
        if (confirmed)
        {
            lines.Add(InkLine.Plain("First route -> missing entry point", Ink.Copper));
            lines.Add(InkLine.Plain(diagnosis.LeadingPath!));
            lines.Add(InkLine.Plain("  '-- hides later identified copy", Ink.Muted));
            lines.Add(InkLine.Plain(diagnosis.LaterPath!));
        }
        else if (diagnosis.State != DoctorDiagnosisState.None) lines.Add(InkLine.Plain(diagnosis.Summary));
        if (report.Proposal is { } proposal)
        {
            lines.Add(InkLine.Plain("Selected / " + proposal.SelectedPath));
            lines.Add(InkLine.Plain("npm / " + proposal.InstalledVersion + " -> " + proposal.Version, Ink.Signal));
            if (confirmed && proposal.RouteGuard is null)
                lines.Add(InkLine.Plain("This choice does not repair the first route", Ink.Copper));
        }
        else if (report.Issues.FirstOrDefault() is { } issue) lines.Add(InkLine.Plain(issue.Explanation));
        lines.Add(InkLine.Plain("details: evidence and exact next command", Ink.Muted));
        lines.Add(InkLine.Plain("No package changes / source retained", Ink.Muted));
        var compact = new[] { lines[0], lines[1],
            InkLine.Plain(confirmed ? "First route -> missing entry point" : diagnosis.Summary, Ink.Muted),
            lines[^2], lines[^1] };
        var window = new List<InkLine> { lines[0], lines[1] };
        if (confirmed)
        {
            window.Add(InkLine.Plain("First folder", Ink.Signal));
            window.Add(InkLine.Plain(FolderLabel(diagnosis.LeadingPath!), Ink.Text));
            window.Add(InkLine.Plain("  '-- missing bin/codex.js", Ink.Copper));
            window.Add(InkLine.Plain("Later folder", Ink.Signal));
            window.Add(InkLine.Plain(FolderLabel(diagnosis.LaterPath!), Ink.Text));
        }
        else if (diagnosis.State != DoctorDiagnosisState.None) window.Add(InkLine.Plain(diagnosis.Summary));
        if (report.Proposal is { } chosen)
            window.Add(InkLine.Plain(confirmed ? chosen.RouteGuard is null ? "Selected copy leaves first route broken" :
                "Review repairs the first copy" : "npm / " + chosen.InstalledVersion + " -> " + chosen.Version, Ink.Signal));
        window.Add(InkLine.Plain("details: exact paths and next command", Ink.Muted));
        window.Add(lines[^1]);
        var state = report.State == DoctorState.Interrupted ? StageState.Interrupted :
            ready ? StageState.Waiting : report.State == DoctorState.ChooseCopy ? StageState.Attention : StageState.Failed;
        return new(sourceLine, lines, compact)
        { WindowLines = window, Action = new("Doctor review", state, "no package changes") };
    }

    private static string FolderLabel(string path) => TerminalText.Clip(Path.GetFileName(Path.GetDirectoryName(path)) ?? path, 28);

    private static string Heading(DoctorDiagnosisState state) => state switch
    {
        DoctorDiagnosisState.Confirmed => "Conflict confirmed / external PATH",
        DoctorDiagnosisState.Possible => "Possible conflict / evidence only",
        DoctorDiagnosisState.Incomplete => "Conflict check incomplete",
        _ => "External route check"
    };

    // Preserve every path character, including repeated spaces and punctuation.
    private static IEnumerable<string> WrapPath(string path, int width)
    {
        var elements = Dert.Text.DisplayCells.Elements(path).ToArray();
        for (var start = 0; start < elements.Length;)
        {
            var end = start;
            var used = 0;
            var separator = -1;
            while (end < elements.Length && used + elements[end].Width <= width)
            {
                used += elements[end].Width;
                if (elements[end].Text is "\\" or "/") separator = end + 1;
                end++;
            }
            if (end == start) { yield return "…"; start++; continue; }
            if (end < elements.Length && separator > start) end = separator;
            yield return string.Concat(elements[start..end].Select(element => element.Text));
            start = end;
        }
    }
}
