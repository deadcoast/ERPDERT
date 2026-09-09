using Erpdert.Mer;
using Spectre.Console;

namespace Erpdert.Cli.Motion;

internal static class MerHost
{
    internal static bool CanAnimate(IAnsiConsole console, bool plain, bool still) =>
        CanDrawLive(console, plain) && !still &&
        Environment.GetEnvironmentVariable("ERPDERT_MOTION") != "off";

    internal static bool CanDrawLive(IAnsiConsole console, bool plain) => !plain &&
        console.Profile.Capabilities.Ansi && console.Profile.Capabilities.Interactive && Environment.GetEnvironmentVariable("TERM") != "dumb";

    internal static MotionSession Open(MotionScore score, IAnsiConsole console, bool plain, bool still, bool detailed = false) =>
        new(score, console.Profile.Out.Writer, () => Size(console), Colors(console, plain), CanAnimate(console, plain, still), CanDrawLive(console, plain), detailed);

    internal static TerminalSize Size(IAnsiConsole console)
    {
        try
        {
            // Use the real viewport when attached; test/redirected writers have no console handle.
            if (console.Profile.Capabilities.Interactive && !Console.IsOutputRedirected)
                return new(Console.WindowWidth, Console.WindowHeight);
        }
        catch (IOException) { }
        return new(console.Profile.Width, console.Profile.Height);
    }

    internal static MerColorMode Colors(IAnsiConsole console, bool plain) =>
        plain || !console.Profile.Capabilities.Ansi || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
        ? MerColorMode.None
        : console.Profile.Capabilities.ColorSystem == ColorSystem.TrueColor ? MerColorMode.TrueColor : MerColorMode.Ansi16;
}
