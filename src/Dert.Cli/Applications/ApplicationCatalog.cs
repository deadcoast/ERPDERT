using System.Text;

namespace Dert.Cli.Applications;

public sealed record ApplicationDefinition(
    string Id,
    string DisplayName,
    string CommandName,
    string? NpmPackage = null)
{
    public bool CanUpdate => Id.Equals("codex", StringComparison.OrdinalIgnoreCase) &&
        NpmPackage?.Equals("@openai/codex", StringComparison.OrdinalIgnoreCase) == true;
    public bool CanDoctor => CanUpdate;
}

public static class ApplicationCatalog
{
    public static ApplicationDefinition Codex { get; } =
        new("codex", "Codex", "codex", "@openai/codex");

    public static bool TryResolve(string input, out ApplicationDefinition application, out string reason)
    {
        application = null!;
        reason = "";
        if (string.IsNullOrWhiteSpace(input))
        {
            reason = "Give DERT one application command name to locate.";
            return false;
        }

        var name = input;
        if (name.Equals(Codex.Id, StringComparison.OrdinalIgnoreCase))
        {
            application = Codex;
            return true;
        }

        if (!IsSafeCommandName(name))
        {
            reason = "Application names may use letters, numbers, ordinary spaces, periods, underscores, hyphens, plus signs and parentheses. Paths, wildcards, control characters and leading option names are not accepted.";
            return false;
        }

        application = new(name.ToLowerInvariant(), name, name);
        return true;
    }

    private static bool IsSafeCommandName(string name)
    {
        if (name.Length is < 1 or > 128 || name != name.Trim() || name.StartsWith('-') ||
            name.EndsWith('.') || name is "." or "..") return false;
        var runes = name.EnumerateRunes().ToArray();
        if (!runes.Any(Rune.IsLetterOrDigit) || !runes.All(rune =>
                Rune.IsLetterOrDigit(rune) || rune.Value is ' ' or '.' or '_' or '-' or '+' or '(' or ')'))
            return false;
        var stem = name.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)) return false;
        return !(stem.Length == 4 && stem[3] is >= '1' and <= '9' &&
            (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
             stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)));
    }
}
