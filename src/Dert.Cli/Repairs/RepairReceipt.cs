using System.Text.Json.Serialization;

namespace Dert.Cli.Repairs;

internal sealed record RepairReceipt(int Version, string DocumentPath, string FileIdentity, string BeforeHash,
    string AfterHash, DateTimeOffset CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RepairSelection? Selection = null);

internal sealed record RepairSelection(string Kind, int? AtLine, string? CloseBefore);
