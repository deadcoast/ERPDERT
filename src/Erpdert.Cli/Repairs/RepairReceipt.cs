namespace Erpdert.Cli.Repairs;

internal sealed record RepairReceipt(int Version, string DocumentPath, string FileIdentity, string BeforeHash,
    string AfterHash, DateTimeOffset CreatedAt);
