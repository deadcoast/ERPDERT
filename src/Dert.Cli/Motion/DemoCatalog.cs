using System.Text;

namespace Dert.Cli.Motion;

internal sealed record DemoCase(string Id, string Title, string LookFor, Func<string> Read, int ExpectedExit = 0);

internal static class DemoCatalog
{
    // Product-owned, offline examples. These go through the real parser and
    // repair planner; no package manager, user note, or reference doc is read.
    internal static IReadOnlyList<DemoCase> Cases { get; } = [
        new("title", "Title alignment",
            "The title stays put. Only its short underline appears in the proposed edit; nothing is applied.",
            () => "// MY FIRST TITLE\n// ---\n\nKeep my words exactly as I wrote them.\n"),
        new("context", "Context that belongs together",
            "Follow a word to its explanation. After playback, prev and next move between entries; source reveals the original note.",
            () => "// FRIDAY NIGHT\n// ------------\nSTERT\n// plans ; friends ;;\n// music, // Bring the playlist everyone keeps asking for.\n// food, // Book the corner table for six. Two of us finish late, so ask the kitchen to hold their order. Keep the booking number with this note.\n// home // Last train at 23:40. Keep this with the plan.\n//\nƒin.\n"),
        new("unfinished", "Unfinished is not broken",
            "The missing closing // stays unresolved. You should see what needs a choice, without a green claim that it was fixed.",
            () => "// STILL THINKING\n// --------------\nSTERT\n// plans ; weekend ;;\n// maybe, // I have not finished this thought.\n// tomorrow // Keep this explanation with its entry.\n", 1),
        new("unicode", "Words beyond ASCII",
            "Check café, 日本語, and 👩‍💻 at narrow widths. Words and symbols should stay readable; the full receipt retains the text.",
            () => "// WORLD TOUR\n// ----------\nSTERT\n// places ; next ;;\n// café, // One coffee before the train.\n// 日本語, // Keep the wide characters together.\n// 👩‍💻 // A joined emoji is one visible symbol.\n//\nƒin.\n"),
        new("failure", "When a check cannot finish",
            "This case deliberately supplies invalid UTF-8 bytes. The real decoder must fail; later checks must not claim they ran.",
            () => new UTF8Encoding(false, true).GetString([0xC3, 0x28]), 2)
    ];
}
