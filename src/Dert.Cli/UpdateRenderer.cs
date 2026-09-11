using Dert.Core;
using Dert.Cli.Updates;
using Dert.Cli.Transactions;

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

    public void UpdateRehearsal(UpdateReport report, bool embedded = false, bool includeChecks = true, string? sourcePath = null)
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
                CopyCommand($"{Invocation()} update codex updateme --path {PowerShellLiteral(route.Path)}" +
                    (sourcePath is null ? "" : " --from-source " + PowerShellLiteral(sourcePath)) +
                    (plain ? " --plain" : "") + (still ? " --still" : ""));
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
            if (report.Proposal is { } proposal && report.ProposalPath is { } stored)
            {
                Line("UPDATE PROPOSAL / awaiting your choice", SemanticRole.Title);
                Line("  |-- Recheck this source, installation and manager", SemanticRole.ContextNote);
                Line($"  |-- Install @openai/codex {proposal.Version} into this prefix", SemanticRole.Target);
                Line("  '-- Verify the installed version, launcher and npm inventory", SemanticRole.ContextNote);
                Line("This updates the selected copy without uninstalling first. Package scripts stay disabled. An interrupted install can leave it partial; no rollback is promised.");
                if (proposal.Source is { } source) EvidencePath("Source snapshot / " + source.Path);
                EvidencePath("Reviewed proposal saved locally / " + stored);
                Line("APPLY THIS REVIEWED UPDATE", SemanticRole.Hook);
                CopyCommand($"{Invocation()} update codex updateme --path {PowerShellLiteral(proposal.SelectedPath)}" +
                    (proposal.Source is null ? "" : " --from-source " + PowerShellLiteral(proposal.Source.Path)) +
                    " --apply " + proposal.Token + (plain ? " --plain" : "") + (still ? " --still" : ""));
                Line("Changed evidence refuses this approval. DERT records each phase and the verified result in a local operation receipt.", SemanticRole.ContextNote);
            }
            else Line("No saved update approval is available. Rehearse again to create a reviewed proposal.", SemanticRole.Hook);
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
        Line("The update command was not executed.", SemanticRole.ContextNote);
        if (!embedded) Line("ƒin.", SemanticRole.Boundary);
    }

    public void UpdateApproval(UpdateReport report)
    {
        var proposal = report.Proposal!;
        Banner("CODEX / UPDATE / APPROVED REQUEST");
        Line($"@openai/codex / {proposal.InstalledVersion} → {proposal.Version}", SemanticRole.Target);
        EvidencePath("Selected copy / " + proposal.SelectedPath);
        EvidencePath("Target folder / " + proposal.Prefix);
        if (proposal.Source is { } source) EvidencePath("Source snapshot / " + source.Path);
        Line("Rechecking the saved proposal before changing packages. Install only; scripts disabled. Verification and interruption are recorded.", SemanticRole.ContextNote);
    }

    public void UpdateResult(NpmApplyResult result)
    {
        Line("");
        var success = result.State == NpmApplyState.Succeeded;
        Line(success ? "UPDATE / VERIFIED" : "UPDATE / " + result.State.ToString().ToUpperInvariant(), success ? SemanticRole.Line : SemanticRole.Hook);
        foreach (var phase in result.Phases)
        {
            Line("  |-- " + phase.Name + " / " + (phase.Succeeded ? "checked" : "needs attention"),
                phase.Succeeded ? SemanticRole.ContextNote : SemanticRole.Hook);
            if (!phase.Succeeded) Line("      " + phase.Detail);
        }
        Line("  ';; " + result.Message, success ? SemanticRole.Line : SemanticRole.Hook);
        if (result.ReceiptPath is { } receipt) EvidencePath("Operation receipt / " + receipt);
        if (result.Recovery is { } recovery)
        {
            Line("RECOVERY REQUEST / changes packages if you run it", SemanticRole.Hook);
            Line("Inspect the receipt and current installation first. This exact manager request is manual recovery; it does not recheck the saved approval.", SemanticRole.ContextNote);
            CopyCommand(ManagerCommand(recovery));
        }
        Line("ƒin.", SemanticRole.Boundary);
    }
}
