namespace Erpdert.Cli;

internal sealed record CliOptions(string Command, string Path, bool Expand, int? At, string? ApplyToken = null, bool Plain = false)
{
    public static bool TryRead(string[] args, out CliOptions? options, out string message)
    {
        options = null;
        message = "Use ERPDERT inspect, lint, repair, or undo with a file. ERPDERT -help shows examples.";
        if (args.Length < 2 || args[0] is not ("inspect" or "lint" or "repair" or "undo")) return false;
        var expand = false;
        int? at = null;
        string? revision = null;
        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--plain":
                    break;
                case "--expand" when args[0] == "inspect":
                    expand = true;
                    break;
                case "--at" when args[0] == "inspect":
                    if (++i >= args.Length || !int.TryParse(args[i], out var number) || number < 1)
                    {
                        message = "--at needs a positive line number, for example: ERPDERT inspect notes.DERT --at 12";
                        return false;
                    }
                    at = number;
                    break;
                case "--apply" when args[0] is "repair" or "undo":
                    if (revision is not null || ++i >= args.Length || args[i].Length != 64 ||
                        !args[i].All(char.IsAsciiHexDigit))
                    {
                        message = "--apply needs the exact 64-character approval token from your preview. Run repair or undo without --apply to see it first.";
                        return false;
                    }
                    revision = args[i].ToUpperInvariant();
                    break;
                default:
                    message = $"ERPDERT does not recognize the option '{args[i]}' here. Use -help to see the available options.";
                    return false;
            }
        }
        options = new(args[0], args[1], expand, at, revision, args.Contains("--plain"));
        return true;
    }
}
