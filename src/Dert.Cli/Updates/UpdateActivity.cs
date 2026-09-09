namespace Dert.Cli.Updates;

internal enum UpdatePhase { Prefix, Inventory, Release, DryRun }
internal sealed record UpdateActivity(UpdatePhase Phase, bool Confirmed, string Detail, bool IsPath = false);
