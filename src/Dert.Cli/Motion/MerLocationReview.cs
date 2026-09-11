using Dert.Cli.Applications;
using Dert.Cli.Discovery;
using Dert.Cli.Doctoring;
using Dert.Cli.Operations;
using Dert.Mer;

namespace Dert.Cli.Motion;

internal sealed partial class MerReview
{
    private readonly Dictionary<int, LocationConnection> locations = [];
    private readonly Dictionary<int, int> locationCopies = [];
    private readonly Dictionary<int, DoctorReport> doctors = [];
    private readonly Dictionary<int, DocumentAnnotation> locationProblems = [];
    private bool IsLocation() => Operation()?.Kind is ApplicationAction.Where or ApplicationAction.Doctor;
    private bool LocationNeedsAttention() => locationProblems.Count > 0 || locations.Values.Any(location => location.NeedsAttention) ||
        doctors.Values.Any(report => report.State != DoctorState.Ready);
    private string LocationCommands() => !IsLocation() ? "" : " / probe" +
        (Operation() is { } operation && locations.TryGetValue(operation.RequestLine, out var location) ?
            (location.CopyCount > 0 && (location.CopyCount > 1 || location.Report.Discovery.Issues.Count > 0) ? $" / copy 1-{location.CopyCount}" : "") + (operation.Request.Application.CanDoctor ? " / doctor" : "") : "");

    private DocumentAnnotation? LocationAnnotation()
    {
        if (!IsLocation() || Operation() is not { } operation) return null;
        var line = operation.RequestLine;
        if (locationProblems.TryGetValue(line, out var problem)) return problem;
        if (doctors.TryGetValue(line, out var doctor))
            return new DoctorConnection(doctor).Annotation(line, operation.Request.Application.DisplayName);
        if (locations.TryGetValue(line, out var found)) return found.Annotation(locationCopies.GetValueOrDefault(line));
        return new(line, [InkLine.Plain("Where / " + operation.Request.Application.DisplayName, Ink.Signal),
            InkLine.Plain("Source -> command routes -> related folders"),
            InkLine.Plain("probe reads local evidence and folder sizes.", Ink.Muted),
            InkLine.Plain(operation.Kind == ApplicationAction.Doctor ? "Then doctor verifies the selected manager." : "No programs started or files changed", Ink.Muted)]);
    }

    private bool HandleLocation(string answer, CancellationToken cancellation)
    {
        if (!IsLocation() || Operation() is not { } operation) return false;
        var line = operation.RequestLine;
        if (answer.StartsWith("copy ", StringComparison.OrdinalIgnoreCase) && locations.TryGetValue(line, out var found))
        {
            if (int.TryParse(answer.AsSpan(5), out var number) && number >= 1 && number <= found.CopyCount)
            { locationCopies[line] = number - 1; doctors.Remove(line); locationProblems.Remove(line); showStructure = false; Show(); }
            else Say("No located copy has that number. Your selection is unchanged.", Ink.Muted);
            return true;
        }
        var probe = answer.Equals("probe", StringComparison.OrdinalIgnoreCase);
        if (!probe && !answer.Equals("doctor", StringComparison.OrdinalIgnoreCase)) return false;
        using var locationTerminal = probe ? new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain)) : null;
        showStructure = false;
        if (plan is not { CanRun: true }) { Show(); return true; }
        if (!MatchingSource(out var problem)) { Problem(problem); return true; }
        if (!OperatingSystem.IsWindows()) { Problem("Application location currently requires Windows."); return true; }
        doctors.Remove(line); locationProblems.Remove(line);
        try
        {
            if (probe)
            {
                var terminal = locationTerminal!;
                var report = ApplicationLocator.Scan(operation.Request.Application, environment ?? DiscoveryEnvironment.Capture(), cancellation, activity =>
                {
                    if (activity.Completed || !MerHost.CanDrawLive(console, plain)) return;
                    var size = MerHost.Size(console);
                    if (size.Columns < 4 || size.Rows < 4) return;
                    var pending = new DocumentAnnotation(line, [InkLine.Plain("Locating " + operation.Request.Application.DisplayName, Ink.Signal),
                        InkLine.Plain(activity.Detail), InkLine.Plain("No programs started", Ink.Muted)])
                    { Action = new("Application location", StageState.Working, "reading local evidence") };
                    terminal.Draw(MerCompositor.Document(snapshot, size.Columns, size.Rows, selected, pending));
                });
                if (!MatchingSource(out problem)) { Problem(problem); return true; }
                locations[line] = new(line, report); locationCopies.Remove(line);
                var finalSize = MerHost.Size(console);
                terminal.Finish(MerCompositor.Document(snapshot, finalSize.Columns, finalSize.Rows, selected, LocationAnnotation()).Lines);
                return true;
            }
            if (snapshot.Score.Replay) { Problem("Replay cannot start doctor queries. Open the file in a live MER session."); return true; }
            if (!operation.Request.Application.CanDoctor) { Problem("This application has no doctor adapter. Its location evidence remains available."); return true; }
            if (!locations.TryGetValue(line, out var location))
            { Say("Use probe to locate this application's copies before requesting doctor checks.", Ink.Muted); return true; }
            var explicitCopy = locationCopies.TryGetValue(line, out var chosen);
            var oldRoute = location.CopyCount > 0 && (explicitCopy || location.CopyCount == 1)
                ? location.Report.Discovery.Installations[explicitCopy ? chosen : 0][0] : null;
            var scanEnvironment = environment ?? DiscoveryEnvironment.Capture();
            Func<DiscoveryEnvironment>? environmentReader = environment is null ? null : () => environment;
            var fresh = CodexDiscovery.Scan(scanEnvironment, cancellation);
            if (!explicitCopy && fresh.Issues.Count > 0)
            { Problem("The PATH search is incomplete. Probe again and explicitly choose a copy before checking its manager."); return true; }
            if (!explicitCopy && fresh.Installations.Count != location.CopyCount)
            { Problem("The available copies changed since they were located. Probe again before choosing a manager."); return true; }
            if (oldRoute is not null && !fresh.Commands.Any(route => route.Path.Equals(oldRoute.Path, StringComparison.OrdinalIgnoreCase) && SameRoute(oldRoute, route)))
            { Problem("The selected copy changed since it was located. Probe again before checking its manager."); return true; }
            Say(oldRoute is null ? "Doctor reads the route relationship before a copy is chosen. No npm queries run." :
                "Doctor queries this copy's npm and registry, then saves a local reviewed proposal. npm may write cache/logs; no uninstall or reinstall runs.", Ink.Muted);
            var reportDoctor = DoctorService.Inspect(fresh, oldRoute?.Path,
                new SourceCheckedRunner(commandRunner ?? new SystemCommandRunner(cancellation), () => MatchingSource(out var issue) ? null : issue, cancellation),
                cancellation: cancellation, source: DoctorSource.Capture(path), environment: scanEnvironment, environmentReader: environmentReader);
            if (!MatchingSource(out problem)) { Problem(problem); return true; }
            if (reportDoctor.State == DoctorState.Ready) renderer.DoctorProposalStored(DoctorService.SaveProposal(reportDoctor,
                doctorStateDirectory is null ? null : Path.Combine(doctorStateDirectory, "doctor-proposals"), environmentReader));
            doctors[line] = reportDoctor;
            Show();
        }
        catch (OperationCanceledException) { Problem("Application checks stopped. No new result was accepted.", StageState.Interrupted); throw; }
        catch (Exception error) when (error is not MerOutputException && error is IOException or UnauthorizedAccessException or ArgumentException)
        { Problem("Application evidence could not be retained. " + error.Message); }
        return true;

        void Problem(string message, StageState state = StageState.Failed)
        {
            doctors.Remove(line);
            locationProblems[line] = new(line, [InkLine.Plain("Application checks stopped", Ink.Copper), InkLine.Plain(message)])
            { Action = new("Application checks", state, "no package changes") };
            if (locationTerminal is null) Show();
            else
            {
                var size = MerHost.Size(console);
                locationTerminal.Finish(MerCompositor.Document(snapshot, size.Columns, size.Rows, selected, LocationAnnotation()).Lines);
            }
            Say(message, Ink.Copper);
        }
    }

    private void LocationDetails()
    {
        foreach (var location in locations.Values)
        {
            Say($"Location evidence / source line {location.SourceLine}", Ink.Signal);
            renderer.Locations(location.Report);
            if (doctors.TryGetValue(location.SourceLine, out var doctor)) renderer.Doctor(doctor, location.Report.Application.Id, path);
            if (locationProblems.TryGetValue(location.SourceLine, out var problem)) Print(problem.Lines);
        }
    }

    private void ClearLocations() { locations.Clear(); locationCopies.Clear(); doctors.Clear(); locationProblems.Clear(); }
}
