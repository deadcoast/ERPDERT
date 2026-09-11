namespace Dert.Core;

public enum ObservationOutcome { Completed, Attention, Failed, Interrupted }

public sealed record ResultEvidence(string Label, string Detail);
public sealed record ResultOrigin(string Path, int Line, string SourceRevision, string ChainText);

/// <summary>A captured historical observation. Evidence is snapshotted on construction and replacement.</summary>
public sealed record ResultObservation(string Id, DateTimeOffset ObservedAt, string Action,
    string Target, ObservationOutcome Outcome, string Summary, string Effects,
    ResultOrigin Origin, IReadOnlyList<ResultEvidence> Evidence)
{
    private IReadOnlyList<ResultEvidence> evidence = Array.AsReadOnly((Evidence ?? throw new ArgumentNullException(nameof(Evidence))).ToArray());
    public IReadOnlyList<ResultEvidence> Evidence
    {
        get => evidence;
        init => evidence = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(Evidence))).ToArray());
    }
}

public sealed record ResultSourceComparison(string State, string Message, int? Line,
    string CapturedText, string? CurrentText);
