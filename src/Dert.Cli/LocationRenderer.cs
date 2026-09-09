using Dert.Core;
using Dert.Cli.Applications;
using Dert.Cli.Discovery;
using Dert.Cli.Doctoring;
using Dert.Cli.Motion;
using Dert.Mer;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer
{
    public void Locations(ApplicationLocationReport report)
    {
        using var terminal = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
        terminal.Finish(LocationCompositor.Receipt(new LocationConnection(0, report).View(), MerHost.Size(console).Columns));
    }

    public void Doctor(DoctorReport report, string application = "codex", string? sourcePath = null)
    {
        Line("");
        Line("DOCTOR / " + application, SemanticRole.Reasoning);
        if (report.Update is { } update)
            foreach (var check in update.Checks)
            { Line(check.Name, SemanticRole.ContextNote); Line(check.Detail); }
        foreach (var issue in report.Discovery.Issues.Concat(report.Update?.Selected?.Issues ?? []).Concat(report.Issues).Distinct()) DiscoveryFinding(issue);
        if (report.State == DoctorState.ChooseCopy)
        {
            Line("Choose the exact copy before checking its manager. No uninstall or install ran.", SemanticRole.Hook);
            foreach (var group in report.Discovery.Installations)
                CopyCommand(DoctorCommand(application, group[0].Path, sourcePath));
        }
        else if (report.Proposal is { } proposal && report.State == DoctorState.Ready)
        {
            Line("REINSTALL PROPOSAL / awaiting your choice", SemanticRole.Title);
            EvidencePath(proposal.Prefix);
            Line("Uninstall @openai/codex from this prefix, then install the reviewed version " + proposal.Version + ".", SemanticRole.Text);
            Line("Package scripts are disabled. npm may change this package and its dependencies. Your adjacent settings/data folders are not deletion targets.", SemanticRole.ContextNote);
            Line("This is not atomic: a failed reinstall can leave this copy absent or partial. The operation receipt records the last verified phase.", SemanticRole.ContextNote);
            Line("EXACT MANAGER REQUESTS", SemanticRole.Reasoning);
            CopyCommand(ManagerCommand(proposal.Uninstall));
            CopyCommand(ManagerCommand(proposal.Install));
            Line("APPLY THIS REVIEWED REPAIR", SemanticRole.Hook);
            CopyCommand(DoctorCommand(application, proposal.SelectedPath, proposal.Source?.Path ?? sourcePath) + " --apply " + proposal.Token);
        }
        else Line(report.State == DoctorState.Interrupted ? "Doctor checks stopped. No repair was approved." :
            report.Update?.State == Updates.UpdateState.Ahead ? "This installed version is newer than the registry's latest stable release. No downgrade is proposed." :
            "A verified reinstall proposal is unavailable for this copy.", SemanticRole.Hook);
        Line("No uninstall or reinstall has run during this review.", SemanticRole.ContextNote);
    }

    public void DoctorResult(DoctorApplyResult result)
    {
        Line("");
        Line(result.State == DoctorApplyState.Succeeded ? "DOCTOR / REINSTALL VERIFIED" : "DOCTOR / " + result.State.ToString().ToUpperInvariant(),
            result.State == DoctorApplyState.Succeeded ? SemanticRole.Line : SemanticRole.Hook);
        foreach (var phase in result.Phases)
            Line(phase.Name + " / " + (phase.Succeeded ? "checked" : "needs attention / " + phase.Detail));
        Line(result.Message);
        if (result.ReceiptPath is { } receipt) EvidencePath("Operation receipt / " + receipt);
        if (result.Recovery is { } recovery)
        {
            Line("RECOVERY REQUEST / changes packages if you run it", SemanticRole.Hook);
            CopyCommand(ManagerCommand(recovery));
        }
    }

    public void DoctorProposalStored(string path) => EvidencePath("Reviewed proposal saved locally / " + path);

    private string DoctorCommand(string application, string path, string? source) =>
        $"{Invocation()} where {PowerShellLiteral(application)} --doctor --path {PowerShellLiteral(path)}" +
        (source is null ? "" : " --from-source " + PowerShellLiteral(source)) + (plain ? " --plain" : "") + (still ? " --still" : "");

    private static string ManagerCommand(Operations.CommandRequest request) =>
        "& " + PowerShellLiteral(request.Program) + " " + string.Join(" ", request.Arguments.Select(PowerShellLiteral));
}
