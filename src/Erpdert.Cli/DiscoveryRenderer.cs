using Dert.Core;
using Erpdert.Cli.Discovery;

namespace Erpdert.Cli;

internal sealed partial class DocumentRenderer
{
    public void Discovery(DiscoveryReport report, bool embedded = false)
    {
        if (embedded) Line("CODEX / DISCOVERY", SemanticRole.Reasoning);
        else Banner("CODEX / DISCOVERY");
        Line("WHICH CODEX?", SemanticRole.Title);
        if (report.Commands.Count == 0)
        {
            Line("No codex command was found in the inspected PATH folders.", SemanticRole.Hook);
            Line("That does not prove Codex is uninstalled. It may be outside PATH or available through a shell alias.");
        }
        else
        {
            Line(report.Issues.Count == 0 ? "First PowerShell PATH match" : "First observed PATH match · search incomplete", SemanticRole.Line);
            EvidencePath(report.Commands[0].Path);
            Line(report.Installations.Count == 1
                ? "One installation group in this search."
                : $"{report.Installations.Count} installation groups share this command name. The earlier PATH match comes first; a newer package does not override it.",
                report.Installations.Count == 1 ? SemanticRole.Text : SemanticRole.Hook);
        }
        Line("");
        var number = 0;
        foreach (var installation in report.Installations)
        {
            var first = installation[0];
            Line($"{++number:00} / {(first == report.Commands[0] ? "FIRST ROUTE" : "ALTERNATE ROUTE")}", SemanticRole.Reasoning);
            foreach (var command in installation)
                EvidencePath($"  PATH {command.PathPosition:00} · {command.Path}");
            if (first.Package is { } package)
            {
                Line($"  PACKAGE  @openai/codex · {package.Version}", SemanticRole.Target);
                EvidencePath($"  EVIDENCE {package.MetadataPath}");
                Line("  Launcher text references this package. Version is from local metadata.");
                if (installation.Count > 1)
                    Line($"  {installation.Count} launcher paths refer to the same package; these are not {installation.Count} separate installs.");
                Line("  Dependencies below belong to the first listed launcher; other routes can differ.", SemanticRole.ContextNote);
                EvidencePath($"  NODE     {package.NodePath ?? "not found"}");
                EvidencePath($"  NPM      {package.NpmPath ?? "no matching npm package found beside this launcher"}");
                Line("  OWNER    npm-style layout; the original installer is not proven.", SemanticRole.ContextNote);
            }
            else Line("  OWNER    unknown · no updater selected", SemanticRole.Hook);
            foreach (var issue in installation.SelectMany(c => c.Issues).Distinct()) DiscoveryFinding(issue);
            Line("");
        }
        foreach (var issue in report.Issues) DiscoveryFinding(issue);
        Line("SEARCH BOUNDARY", SemanticRole.Line);
        Line($"{Count(report.DirectoryCount, "PATH folder")} · {Count(report.Commands.Count, "command match", "command matches")} · local files only");
        Line("This is a file-discovery snapshot, not a launch test. Shell aliases/functions, execution policy, native payloads, and runtime compatibility were not validated.", SemanticRole.ContextNote);
        Line("To check overrides in your current PowerShell session (does not run Codex):", SemanticRole.Text);
        CopyCommand("Get-Command codex -All | Select-Object CommandType, Name, Source, Definition");
        Line("No whole-computer, registry, Store, or WSL inventory was performed. No updater was selected.", SemanticRole.ContextNote);
        if (!embedded)
        {
            Line("Nothing was executed, installed, updated, or changed.", SemanticRole.Boundary);
            Line("ƒin.", SemanticRole.Boundary);
        }
    }

    private void DiscoveryFinding(DiscoveryIssue issue)
    {
        Line($"  {issue.Code}", SemanticRole.Hook);
        EvidencePath($"  {issue.Path}");
        Line($"  {issue.Explanation}");
    }

    // Let the terminal soft-wrap a path. Inserting hard newlines inside one
    // makes copying the evidence unreliable, especially from redirected output.
    private void EvidencePath(string value) => console.Profile.Out.Writer.WriteLine(Safe(value));
}
