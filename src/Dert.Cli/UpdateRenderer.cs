using Dert.Core;
using Dert.Cli.Updates;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer
{
    public void UpdateHeading(bool embedded = false)
    {
        if (embedded) Line("CODEX / UPDATE / REHEARSAL", SemanticRole.Reasoning);
        else Banner("CODEX / UPDATE / REHEARSAL");
        Line("Choose the copy. Check the route. See the change.", SemanticRole.Reasoning);
        Line("This checks local npm and registry metadata. npm may write its cache and logs; no packages or notes are changed.", SemanticRole.ContextNote);
    }

    public void UpdateProgress(string stage) => Line($"CHECK / {stage}", SemanticRole.Reasoning);

    public void UpdateRehearsal(UpdateReport report, bool embedded = false, bool includeChecks = true)
    {
        if (report.Selected is { } selected)
        {
            Line("THIS COPY", SemanticRole.Title);
            EvidencePath(selected.Path);
            if (report.Prefix is not null) EvidencePath($"TARGET / {report.Prefix}");
            if (report.Node is not null) EvidencePath($"NODE   / {report.Node}");
            if (report.NpmCli is not null) EvidencePath($"NPM    / {report.NpmCli}");
            if (report.Available is not null) Line($"VERSION / {selected.Package!.Version} → {report.Available}", SemanticRole.Target);
        }
        foreach (var check in includeChecks ? report.Checks : [])
        {
            Line($"✓ {check.Name}", SemanticRole.Line);
            if (check.IsPath) EvidencePath($"  {check.Detail}");
            else Line($"  {check.Detail}");
        }
        if (report.State == UpdateState.ChooseCopy)
        {
            Line("CHOOSE THE COPY", SemanticRole.Title);
            Line("Multiple routes or an incomplete search prevent an automatic choice. Select the exact installed copy to rehearse; none has been queried yet.");
            foreach (var group in report.Discovery.Installations)
            {
                var route = group[0];
                EvidencePath(route.Path);
                Line($"  VERSION / {route.Package?.Version ?? "unidentified"}", SemanticRole.Target);
                CopyCommand($"{Invocation()} update codex updateme --path {PowerShellLiteral(route.Path)}" + (plain ? " --plain" : "") + (still ? " --still" : ""));
            }
        }
        foreach (var issue in report.Discovery.Issues.Concat(report.Selected?.Issues ?? []).Concat(report.Issues))
            DiscoveryFinding(issue);
        if (report.State == UpdateState.Ready)
        {
            Line("REHEARSED / NOT APPLIED", SemanticRole.Title);
            Line("npm accepted this dry run with engine checks and optional dependencies included. It did not run package scripts or test whether Codex can launch.");
            if (report.ProposedChanges.Count > 0)
            {
                Line("NPM / PROPOSED CHANGES", SemanticRole.Reasoning);
                foreach (var change in report.ProposedChanges)
                {
                    if (change.StartsWith("add ", StringComparison.Ordinal)) Line("+ " + change[4..], SemanticRole.Line);
                    else if (change.StartsWith("remove ", StringComparison.Ordinal)) Line("− " + change[7..], SemanticRole.Hook);
                    else Line("~ " + change[7..], SemanticRole.Target);
                }
            }
            Line("MANUAL UPDATE / CHANGES PACKAGES", SemanticRole.Hook);
            Line("If you choose to run the command below, npm changes this Codex copy and its dependencies. It may run package scripts. This is not a DERT approval token or an automatic rollback.");
            CopyCommand("& " + PowerShellLiteral(report.Install!.Program) + " " + string.Join(" ", report.Install.Arguments.Select(PowerShellLiteral)));
            Line("Rehearse again if the installation, registry settings, or environment changes. A copied npm command does not recheck this snapshot.", SemanticRole.ContextNote);
            Line("VERIFY AFTERWARD / read the installed result", SemanticRole.Reasoning);
            CopyCommand($"{Invocation()} update codex" + (plain ? " --plain" : ""));
        }
        else if (report.State == UpdateState.Current)
        {
            Line("ALREADY CURRENT", SemanticRole.Title);
            Line("The installed version equals the registry's latest stable version. No install rehearsal was needed; this is not a package-health or launch check.");
        }
        else if (report.State == UpdateState.Ahead)
        {
            Line("NEWER THAN LATEST", SemanticRole.Hook);
            Line("The installed version is newer than the registry's latest tag. This could be a different release channel or registry. No downgrade is proposed.");
        }
        else if (report.State == UpdateState.Interrupted)
            Line("INTERRUPTED / stopped at your request; the incomplete result was not accepted.", SemanticRole.Hook);
        Line("The manual update command was not executed.", SemanticRole.ContextNote);
        if (!embedded) Line("ƒin.", SemanticRole.Boundary);
    }
}
