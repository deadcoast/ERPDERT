using Erpdert.Cli.Discovery;
using Erpdert.Mer;

namespace Erpdert.Cli.Motion;

/// <summary>A captured local inspection, separate from the source-analysis record.</summary>
internal sealed record LocalInspection(int SourceLine, DateTimeOffset RecordedAt, DiscoveryReport Report)
{
    internal int CopyCount => Report.Installations.Count;
    private int IssueCount => Report.Issues.Count + Report.Commands.SelectMany(command => command.Issues)
        .DistinctBy(issue => (issue.Code, issue.Path, issue.Explanation)).Count();

    internal DocumentAnnotation Annotation(int copy)
    {
        var lines = new List<InkLine> { InkLine.Plain($"Local inspection / {RecordedAt:HH:mm:ss}", Ink.Signal) };
        var compact = new List<InkLine> { InkLine.Plain($"Local inspection / line {SourceLine}", Ink.Signal) };
        if (CopyCount == 0)
        {
            lines.Add(InkLine.Plain("No Codex launcher found", Ink.Copper));
            lines.Add(InkLine.Plain($"{Report.DirectoryCount} PATH folders inspected.", Ink.Muted));
            lines.Add(InkLine.Plain(IssueCount == 0 ? "No matching external command route." : $"{IssueCount} inspection issues; see details.", Ink.Muted));
            lines.Add(InkLine.Plain("No programs started", Ink.Muted));
            return new(SourceLine, lines, lines);
        }

        var commands = Report.Installations[copy];
        var route = commands[0];
        var package = route.Package;
        var location = Path.GetDirectoryName(route.Path) ?? route.Path;
        var heading = $"Copy {copy + 1} of {CopyCount} / {commands.Count} launchers";
        var identity = package is null ? "Target and manager unidentified" : "@openai/codex " + package.Version;
        lines.Add(InkLine.Plain(heading, Ink.Text));
        lines.Add(InkLine.Plain(IssueCount > 0 ? $"{IssueCount} inspection issues / details" :
            CopyCount > 1 ? "Multiple copies / inspect before choosing" : "One installation located", IssueCount > 0 || CopyCount > 1 ? Ink.Copper : Ink.Muted));
        lines.Add(InkLine.Plain(copy == 0 ? Report.Issues.Count > 0 ? "First observed match / incomplete search" : "First external PATH match"
            : $"Later observed match / PATH #{route.PathPosition}", Ink.Muted));
        lines.Add(InkLine.Plain(Path.GetFileName(route.Path), Ink.Text));
        lines.Add(InkLine.Plain("  |", Ink.Muted));
        lines.Add(InkLine.Plain("  +-- " + identity, package is null ? Ink.Copper : Ink.Signal));
        if (package is not null)
        {
            lines.Add(InkLine.Plain("      |-- " + (package.NodePath is null ? "Node missing" : "node.exe found"), package.NodePath is null ? Ink.Copper : Ink.Muted));
            lines.Add(InkLine.Plain("      '-- " + (package.NpmPath is null ? "npm unverified" : "npm.cmd nearby"), Ink.Muted));
        }
        if (commands.Count > 1) lines.Add(InkLine.Plain("Dependencies shown for this launcher.", Ink.Muted));
        lines.Add(InkLine.Plain("Manager ownership is not verified.", Ink.Muted));
        lines.Add(InkLine.Plain("Location / " + location, Ink.Muted));
        lines.Add(InkLine.Plain("No programs started", Ink.Muted));

        compact.Add(InkLine.Plain(heading + (IssueCount > 0 ? $" / {IssueCount} issues" : ""), IssueCount > 0 || CopyCount > 1 ? Ink.Copper : Ink.Text));
        compact.Add(InkLine.Plain(identity, package is null ? Ink.Copper : Ink.Signal));
        compact.Add(InkLine.Plain(package?.NodePath is null ? "Node unverified; manager unverified" : "Node found; manager unverified", Ink.Muted));
        compact.Add(InkLine.Plain("details: paths / no programs started", Ink.Muted));
        return new(SourceLine, lines, compact);
    }

    internal IReadOnlyList<InkLine> Details()
    {
        var lines = new List<InkLine>
        {
            InkLine.Plain($"Local inspection recorded {RecordedAt:dd MMM yyyy HH:mm:ss zzz} / source line {SourceLine}", Ink.Signal),
            InkLine.Plain($"{CopyCount} installations / {Report.Commands.Count} launchers / {Report.DirectoryCount} PATH folders inspected"),
            InkLine.Plain("No programs started. Aliases, functions and execution policy were not resolved.", Ink.Muted),
            InkLine.Plain("Adjacent npm evidence does not establish installation ownership.", Ink.Muted)
        };
        for (var index = 0; index < CopyCount; index++)
        {
            var commands = Report.Installations[index];
            lines.Add(InkLine.Plain($"Copy {index + 1} of {CopyCount}", Ink.Copper));
            foreach (var command in commands)
            {
                lines.Add(InkLine.Plain($"PATH #{command.PathPosition} / {command.Path}"));
                lines.Add(InkLine.Plain("Resolved / " + command.Identity, Ink.Muted));
                if (command.Package is { } routePackage)
                {
                    lines.Add(InkLine.Plain("  Node / " + (routePackage.NodePath ?? "not found")));
                    lines.Add(InkLine.Plain("  Adjacent npm / " + (routePackage.NpmPath ?? "not verified")));
                }
            }
            if (commands[0].Package is { } package)
            {
                lines.Add(InkLine.Plain("@openai/codex " + package.Version, Ink.Signal));
                lines.Add(InkLine.Plain("Package / " + package.Root));
                lines.Add(InkLine.Plain("Metadata / " + package.MetadataPath, Ink.Muted));
            }
        }
        foreach (var issue in Report.Issues.Concat(Report.Commands.SelectMany(command => command.Issues)).Distinct())
        {
            lines.Add(InkLine.Plain(issue.Code + " / " + issue.Path, Ink.Copper));
            lines.Add(InkLine.Plain(issue.Explanation));
        }
        return lines;
    }
}
