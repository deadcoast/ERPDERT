using Dert.Cli.Updates;
using Dert.Mer;

namespace Dert.Cli.Motion;

internal sealed class UpdateMotion(Action<MotionEvent> publish) : IDisposable
{
    private MotionSession? session;
    internal UpdateMotion(MotionSession session) : this(session.Publish) => this.session = session;
    private string? active;
    private readonly HashSet<string> settled = [];
    internal static MotionScore Score(MotionSource? source) => new("Codex / update rehearsal", "npm queries + dry run · cache/logs may change · packages stay untouched", [
        new("Prefix", "Locate this copy", "Ask this npm where its global packages live."),
        new("Inventory", "Match the inventory", "Check the package, version and folder agree."),
        new("Release", "Read the registry", "Ask the configured registry for its latest stable release."),
        new("DryRun", "Rehearse the change", "Let npm calculate the change with package scripts disabled.")], source);

    internal void Observe(UpdateActivity activity)
    {
        var id = activity.Phase.ToString();
        if (!activity.Confirmed)
        {
            active = id;
            publish(new StartStage(id));
        }
        else
        {
            publish(new AddEvidence(id, new("Observed", activity.Detail, activity.IsPath ? EvidenceKind.Path : EvidenceKind.Text)));
            publish(new SettleStage(id, StageState.Passed, activity.IsPath ? "The target folder matches the selected copy." : activity.Detail));
            settled.Add(id);
            active = null;
        }
    }

    internal void Finish(UpdateReport report)
    {
        if (active is not null)
        {
            foreach (var issue in report.Issues)
                publish(new AddEvidence(active, new(issue.Code, issue.Explanation)));
            publish(new SettleStage(active, report.State == UpdateState.Interrupted ? StageState.Interrupted : StageState.Failed,
                report.State == UpdateState.Interrupted ? "Stopped at your request. The incomplete result was not accepted." : "This check did not establish the evidence needed to continue."));
            settled.Add(active);
        }
        var detail = report.State switch
        {
            UpdateState.Ready => "Rehearsed, not applied. The proposed command and its limits follow.",
            UpdateState.Current => "Already current. No install rehearsal was needed.",
            UpdateState.ChooseCopy => "Choose a copy below. No npm was queried.",
            UpdateState.Ahead => "Installed version is ahead of latest. No downgrade proposed.",
            UpdateState.Interrupted => "Stopped at your request. No package-changing command was run.",
            _ => "Rehearsal could not finish. The evidence and next steps follow."
        };
        foreach (var phase in Enum.GetValues<UpdatePhase>().Select(phase => phase.ToString()).Where(id => !settled.Contains(id)))
            publish(new SettleStage(phase, StageState.Skipped, report.State == UpdateState.Current
                ? "Already current; no install rehearsal was needed." : "Not run. " + detail));
        publish(new FinishMotion(report.State == UpdateState.Interrupted ? MotionOutcome.Interrupted : report.NeedsAttention ? MotionOutcome.Attention : MotionOutcome.Complete, detail));
    }

    public void Dispose() => session?.Dispose();
}
