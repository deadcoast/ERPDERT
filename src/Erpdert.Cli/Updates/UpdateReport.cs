using Erpdert.Cli.Discovery;
using Erpdert.Cli.Operations;

namespace Erpdert.Cli.Updates;

internal enum UpdateState { Unavailable, ChooseCopy, Ready, Current, Ahead, Interrupted }
internal sealed record UpdateCheck(string Name, string Detail, bool IsPath = false);

internal sealed class UpdateReport(DiscoveryReport discovery)
{
    public DiscoveryReport Discovery { get; } = discovery;
    public UpdateState State { get; set; }
    public CommandMatch? Selected { get; set; }
    public string? Prefix { get; set; }
    public string? Node { get; set; }
    public string? NpmCli { get; set; }
    public string? Available { get; set; }
    public List<UpdateCheck> Checks { get; } = [];
    public List<string> ProposedChanges { get; } = [];
    public List<DiscoveryIssue> Issues { get; } = [];
    public CommandRequest? Install { get; set; }
    public CommandRequest? DryRun { get; set; }
    public bool NeedsAttention => State is UpdateState.Unavailable or UpdateState.ChooseCopy or UpdateState.Ahead or UpdateState.Interrupted || Issues.Count > 0 || Selected?.Issues.Count > 0;
}
