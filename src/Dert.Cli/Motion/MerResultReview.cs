using Dert.Cli.Applications;
using Dert.Cli.Results;
using Dert.Mer;

namespace Dert.Cli.Motion;

internal sealed partial class MerReview
{
    private bool HandleResult(string answer)
    {
        if (!answer.Equals("keep", StringComparison.OrdinalIgnoreCase) && !answer.StartsWith("keep ", StringComparison.OrdinalIgnoreCase)) return false;
        if (snapshot.Score.Replay) { Say("Replay cannot produce a new observed result. Open the source and use probe first.", Ink.Copper); return true; }
        if (Inspection() is not { } inspection || Operation() is not { Kind: ApplicationAction.UpdateDiscovery } operation || analysis.Document is null)
        { Say("Use probe on a discovery request first. Keep retains that observation without running it again.", Ink.Muted); return true; }
        var destination = answer.Length > 5 ? answer[5..].Trim() : "";
        if (destination.Length == 0) { Say("Use keep <new.json path>. Spaces belong to the path; no shell quoting is needed here.", Ink.Muted); return true; }
        try
        {
            var observation = DiscoveryObservation.Capture(analysis.Document, path, operation.Source, inspection.Report, inspection.RecordedAt);
            var record = ObservationStore.WriteNew(observation, destination);
            renderer.ResultRetained(observation, record);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { Say(error.Message, Ink.Copper); }
        return true;
    }
}
