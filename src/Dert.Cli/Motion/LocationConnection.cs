using System.Globalization;
using Dert.Cli.Discovery;
using Dert.Mer;

namespace Dert.Cli.Motion;

internal sealed record LocationConnection(int SourceLine, ApplicationLocationReport Report)
{
    internal int CopyCount => Report.Discovery.Installations.Count;
    internal bool NeedsAttention => Report.Locations.Count == 0 || Report.Issues.Count > 0 ||
        CopyCount > 1 || Report.Discovery.Commands.Any(route => route.Issues.Count > 0) ||
        Report.Locations.Any(location => location.IsPartial || location.Issues.Count > 0);

    internal LocationView View()
    {
        var summary = new List<InkLine>
        {
            InkLine.Plain($"{CopyCount} installation {(CopyCount == 1 ? "group" : "groups")} on PATH / {Report.Discovery.Commands.Count} command {(Report.Discovery.Commands.Count == 1 ? "route" : "routes")}"),
            InkLine.Plain($"{Report.Locations.Count} observed locations / {Report.Discovery.DirectoryCount} PATH folders inspected", Ink.Muted)
        };
        if (Report.Discovery.Commands.Count == 0)
            summary.Add(InkLine.Plain($"No {Report.Application.CommandName} command was found in the inspected PATH folders.", Ink.Copper));
        else
        {
            summary.Add(InkLine.Plain(Report.Discovery.Issues.Count == 0 ? "FIRST EXTERNAL PATH MATCH" : "FIRST OBSERVED MATCH / incomplete PATH search", Ink.Signal));
            summary.Add(InkLine.Plain(Report.Discovery.Commands[0].Path));
        }
        summary.Add(InkLine.Plain(CopyCount > 1 ? "Earlier PATH routes can shadow later copies. Multiple copies alone do not prove damage." :
            "Aliases and shell functions are outside this external-command search.", Ink.Muted));
        var branches = new List<LocationBranch>();
        var number = 0;
        foreach (var group in Report.Groups)
        {
            var evidence = new List<InkLine> { InkLine.Plain(group.Reason, Ink.Muted) };
            foreach (var location in group.Locations)
            {
                evidence.Add(InkLine.Plain("  +-- " + location.Label, Ink.Text));
                evidence.Add(InkLine.Plain("  |   " + location.Path));
                if (!location.Path.Equals(location.Identity, StringComparison.OrdinalIgnoreCase))
                    evidence.Add(InkLine.Plain("  |   resolves to " + location.Identity, Ink.Muted));
                evidence.Add(InkLine.Plain("  |   " + Footprint(location), location.IsPartial ? Ink.Copper : Ink.Muted));
                evidence.Add(InkLine.Plain("  |   " + location.Provenance, Ink.Muted));
            }
            var route = Report.Discovery.Commands.FirstOrDefault(command => command.Package?.Root.Equals(group.Key, StringComparison.OrdinalIgnoreCase) == true);
            if (route?.Package is { } package)
            {
                evidence.Add(InkLine.Plain("  +-- " + Report.Application.NpmPackage + " / " + package.Version, Ink.Signal));
                evidence.Add(InkLine.Plain("  |   Node: " + (package.NodePath ?? "not found"), Ink.Muted));
                evidence.Add(InkLine.Plain("  '-- npm nearby: " + (package.NpmPath ?? "not identified"), Ink.Muted));
                evidence.Add(InkLine.Plain("      Nearby npm is evidence; doctor verifies its inventory.", Ink.Muted));
            }
            branches.Add(new($"{++number:00} / {GroupName(group)}", evidence));
        }
        var issues = Report.Issues.Concat(Report.Discovery.Commands.SelectMany(route => route.Issues))
            .Concat(Report.Locations.SelectMany(location => location.Issues)).Distinct().ToArray();
        if (issues.Length > 0)
            branches.Add(new("EVIDENCE GAPS", issues.SelectMany(issue => new[] {
                InkLine.Plain(issue.Code + " / " + issue.Path, Ink.Copper), InkLine.Plain(issue.Explanation) }).ToArray()));
        return new(Report.Application.DisplayName, Report.CapturedAt, summary, branches,
            Report.Locations.Count == 0 ? "No locations observed / review the search evidence" : NeedsAttention ? "Located / review the copies and evidence gaps" : "Located / local evidence recorded",
            "No programs started or files changed. Sizes are observed file lengths; nested totals overlap.");
    }

    internal DocumentAnnotation Annotation(int copy)
    {
        var lines = new List<InkLine> { InkLine.Plain($"Where / {Report.Application.DisplayName} / {Report.CapturedAt.ToLocalTime():HH:mm:ss}", Ink.Signal) };
        if (CopyCount > 0)
        {
            var route = Report.Discovery.Installations[Math.Clamp(copy, 0, CopyCount - 1)][0];
            lines.Add(InkLine.Plain($"Copy {copy + 1} of {CopyCount} / PATH #{route.PathPosition}"));
            lines.Add(InkLine.Plain(route.Path));
            if (route.Package is { } package)
            {
                lines.Add(InkLine.Plain("  +-- " + Report.Application.NpmPackage + " " + package.Version, Ink.Signal));
                var root = Report.Locations.FirstOrDefault(location => location.Identity.Equals(package.Root, StringComparison.OrdinalIgnoreCase));
                if (root is not null) lines.Add(InkLine.Plain("  '-- " + Footprint(root), root.IsPartial ? Ink.Copper : Ink.Muted));
            }
        }
        else lines.Add(InkLine.Plain("No command route located", Ink.Copper));
        lines.Add(InkLine.Plain($"{Report.Locations.Count} locations / details keeps every path", Ink.Muted));
        if (Report.Issues.Count > 0) lines.Add(InkLine.Plain($"{Report.Issues.Count} evidence gaps / details", Ink.Copper));
        lines.Add(InkLine.Plain("No programs started or files changed", Ink.Muted));
        return new(SourceLine, lines, lines.Take(3).Concat(lines.TakeLast(2)).ToArray())
        { Action = new("Application location", NeedsAttention ? StageState.Attention : StageState.Passed, "no file changes") };
    }

    private static string GroupName(ApplicationLocationGroup group) =>
        group.Locations.Any(location => location.Kind == ApplicationLocationKind.PackageRoot) ? "PACKAGE + COMMAND ROUTES" :
        group.Locations.All(location => location.Kind == ApplicationLocationKind.AdjacentFolder) ? "ADJACENT FOLDER" :
        group.Locations.Any(location => location.Kind is ApplicationLocationKind.RegistryAppPath or ApplicationLocationKind.RegistryInstallLocation) ? "REGISTERED LOCATION" : "COMMAND ROUTE";

    private static string Footprint(ApplicationLocation location)
    {
        if (!location.Exists) return "Not accessible / " + location.PartialReason;
        if (location.Size is not { } size) return "Size not measured";
        var units = new[] { "B", "KiB", "MiB", "GiB", "TiB" };
        double amount = size.Bytes; var unit = 0;
        while (amount >= 1024 && unit < units.Length - 1) { amount /= 1024; unit++; }
        var value = amount.ToString(unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[unit];
        return (size.IsPartial ? "At least " : "") + value + $" / {size.Bytes:N0} bytes / {size.Files} {(size.Files == 1 ? "file" : "files")}" +
            (size.IsPartial ? " / partial: " + size.Explanation : "");
    }
}
