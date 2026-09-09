using System.Globalization;

namespace Dert.Cli;

internal sealed record CliOptions(string Command, string Path, bool Expand, int? At, string? ApplyToken = null,
    bool Plain = false, string? CloseBefore = null, string? FromLint = null, bool Preview = false)
{
    public static bool TryRead(string[] args, out CliOptions? options, out string message)
    {
        options = null;
        message = "Use DERT run, inspect, lint, repair, or undo with a file. DERT -help shows examples.";
        if (args.Length < 2 || args[0] is not ("run" or "inspect" or "lint" or "repair" or "undo")) return false;
        if (args[0] == "run" && args[1].StartsWith("--", StringComparison.Ordinal))
        {
            message = "Choose a DERT file: DERT run 'my-tools.DERT' [--preview] [--plain].";
            return false;
        }
        var expand = false;
        var preview = false;
        int? at = null;
        string? revision = null;
        string? closeBefore = null;
        string? fromLint = null;
        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--plain":
                    break;
                case "--still" when args[0] == "run":
                    break;
                case "--preview" when args[0] == "run":
                    preview = true;
                    break;
                case "--expand" when args[0] == "inspect":
                    expand = true;
                    break;
                case "--at" when args[0] is "inspect" or "repair":
                    if (at is not null || ++i >= args.Length || !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
                    {
                        message = "--at needs a positive line number, for example: DERT inspect notes.DERT --at 12";
                        return false;
                    }
                    at = number;
                    break;
                case "--close-before" when args[0] == "repair":
                    if (closeBefore is not null || ++i >= args.Length)
                    {
                        message = "Choose --close-before once, with an offered source line or eof.";
                        return false;
                    }
                    if (args[i].Equals("eof", StringComparison.OrdinalIgnoreCase)) closeBefore = "eof";
                    else if (int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var boundary) && boundary > 0)
                        closeBefore = boundary.ToString(CultureInfo.InvariantCulture);
                    else
                    {
                        message = "--close-before needs an offered positive source line, or eof when offered. Run repair with --at first to see the choices.";
                        return false;
                    }
                    break;
                case "--from-lint" when args[0] == "repair":
                    if (fromLint is not null || ++i >= args.Length || args[i].Length != 64 || !args[i].All(char.IsAsciiHexDigit))
                    {
                        message = "--from-lint needs the exact 64-character source revision from lint. Copy a complete rehearsal command from that report.";
                        return false;
                    }
                    fromLint = args[i].ToUpperInvariant();
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
                    message = $"DERT does not recognize the option '{args[i]}' here. Use -help to see the available options.";
                    return false;
            }
        }
        if (closeBefore is not null && at is null || args[0] == "repair" && at is not null && revision is not null && closeBefore is null)
        {
            message = "A note repair needs --at to identify the unfinished block, and --close-before to select a boundary before --apply can approve it.";
            return false;
        }
        if (fromLint is not null && revision is not null)
        {
            message = "A --from-lint handoff opens a preview only. It cannot be combined with --apply. Open the rehearsal first, then choose its separate approval command.";
            return false;
        }
        options = new(args[0], args[1], expand, at, revision, args.Contains("--plain"), closeBefore, fromLint, preview);
        return true;
    }
}
