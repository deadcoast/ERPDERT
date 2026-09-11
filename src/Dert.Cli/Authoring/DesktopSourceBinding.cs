using Dert.Cli.Results;

namespace Dert.Cli.Authoring;

/// <summary>Finds the source a literal CLI command reads or proposes to change.</summary>
public static class DesktopSourceBinding
{
    public static bool UsesDocument(IReadOnlyList<string> arguments, string path)
    {
        var fullPath = Path.GetFullPath(path);
        bool Matches(string candidate) => string.Equals(Path.GetFullPath(candidate), fullPath, StringComparison.OrdinalIgnoreCase);
        if (arguments.Count > 0 && arguments[0].ToLowerInvariant() is "write" or "guide")
        {
            var launch = AuthoringCommand.LaunchInfo(arguments.Skip(1).ToArray(), AppContext.BaseDirectory);
            return launch.ArgumentList.FirstOrDefault() is { } source && !source.StartsWith("--", StringComparison.Ordinal) && Matches(source);
        }
        if (arguments.Count > 1 && arguments[0].ToLowerInvariant() is
            "run" or "repair" or "undo" or "lint" or "inspect" or "context")
            if (!arguments[1].StartsWith("--", StringComparison.Ordinal) && Matches(arguments[1])) return true;
        for (var i = 1; i + 1 < arguments.Count; i++)
            if (arguments[i] is "--from-source" or "--into" && Matches(arguments[i + 1])) return true;
        if (arguments.Count > 1 && arguments[0].Equals("result", StringComparison.OrdinalIgnoreCase) && !arguments.Contains("--into"))
            return Matches(ObservationStore.Read(arguments[1]).Origin.Path);
        return false;
    }
}
