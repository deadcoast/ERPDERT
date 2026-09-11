using System.Security.Cryptography;
using System.Text;
using Dert.Cli.Discovery;
using Dert.Core;

namespace Dert.Cli.Results;

internal static class DiscoveryObservation
{
    internal static ResultObservation Capture(DertDocument document, string path, ChainNode chain,
        DiscoveryReport report, DateTimeOffset observedAt)
    {
        var evidence = new List<ResultEvidence>
        {
            new("Search scope", $"{report.DirectoryCount} PATH directories inspected; {report.Commands.Count} command routes; {report.Installations.Count} installation groups."),
            new("Limits", "Aliases, shell functions and execution policy were not resolved. Adjacent npm does not establish manager ownership. Codex was not launched.")
        };
        foreach (var command in report.Commands)
        {
            evidence.Add(new("Command route", $"PATH #{command.PathPosition}: {command.Path}"));
            evidence.Add(new("Resolved identity", command.Identity));
            if (command.Package is { } package)
            {
                evidence.Add(new("Package", $"@openai/codex {package.Version}: {package.Root}"));
                evidence.Add(new("Metadata", package.MetadataPath));
                evidence.Add(new("Node", package.NodePath ?? "Not identified"));
                evidence.Add(new("Adjacent npm", package.NpmPath ?? "Not identified"));
            }
            else evidence.Add(new("Package", "Package and manager not identified for this route."));
            foreach (var issue in command.Issues) evidence.Add(new(issue.Code, issue.Path + " / " + issue.Explanation));
        }
        foreach (var issue in report.Issues) evidence.Add(new(issue.Code, issue.Path + " / " + issue.Explanation));
        return new(Guid.NewGuid().ToString("N"), observedAt, "DERT update codex / local discovery", "codex",
            report.NeedsAttention ? ObservationOutcome.Attention : ObservationOutcome.Completed,
            report.Commands.Count == 0 ? "No Codex command route found in the inspected PATH directories."
                : $"Observed {report.Commands.Count} command routes in {report.Installations.Count} installation groups. Manager ownership remains unverified.",
            "Discovery read local PATH and package metadata. No programs started or packages changed.",
            new(Path.GetFullPath(path), chain.LineNumber,
                Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false, true).GetBytes(document.Source.Text))),
                document.Source.Slice(chain.Span)), evidence.AsReadOnly());
    }
}
