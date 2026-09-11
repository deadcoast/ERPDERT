using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dert.Notes;

public sealed partial class NotesWindow
{
    private bool isHelpWorkbench;
    public void EnableHelp(bool troubleshoot = false)
    {
        if (IsCompact) throw new InvalidOperationException("Help uses its separate workbench surface.");
        if (Content is not DockPanel root) return;
        isHelpWorkbench = true;
        if (root.Children[0] is DockPanel brand && brand.Children[1] is TextBlock role) role.Text = "  /  help";
        var navigation = new WrapPanel { Margin = new(0, 0, 0, 12) };
        navigation.Children.Add(Action("Titles", () => LoadLesson("// MY FIRST TITLE\r\n// --------------\r\n\r\n// Change the title, then press Enter. Undo brings your earlier words back.\r\n")));
        navigation.Children.Add(Action("Hook, line, sinkers", () => LoadLesson("// A LITTLE LIST\r\n// -------------\r\n\r\npacking ; overnight ;;\r\nnotebook, // ; is the hook, ;; is the line. These items are sinkers.\r\nheadphones,\r\ncoffee\r\n")));
        navigation.Children.Add(Action("Reasoning", () => LoadLesson("// NOTES WITH REASONS\r\n// ------------------\r\n\r\n// HOW ; weekend ;; Keep the useful explanation with its note. //\r\n\r\n// little plans ; Saturday ;;\r\n// morning, // Coffee and a walk.\r\n// afternoon, // Leave room for a change of mind.\r\n//\r\n")));
        navigation.Children.Add(Action("Troubleshoot", () => new TroubleshootingWindow().Show()));
        navigation.Children.Add(Action("Open a compact note", () => new NotesWindow(compact: true).Show()));
        DockPanel.SetDock(navigation, Dock.Top); root.Children.Insert(2, navigation);
        if (!HasOpenedDocument && !needsAttention) LoadLesson("// DERT HELP\r\n// ---------\r\n\r\n// Choose a topic above, or open a note to inspect its syntax.\r\n// This workbench is yours to explore. The compact window is your everyday notepad.\r\n\r\n// why ; help ;; Try a change and inspect its meaning. Undo keeps exploration reversible. //\r\n");
        if (!needsAttention) notice.Text = "Help workbench / examples are editable; Save creates your own copy.";
        Update();
        if (troubleshoot) Loaded += (_, _) => new TroubleshootingWindow().Show();
    }

    private void LoadLesson(string source)
    {
        if (!ConfirmDeparture()) return;
        opened = null; preview = null; Lists.Reset(); Goodbye.Reset(); Literals.Reset(); needsAttention = false;
        Session.Load(source); Editor.CaretOffset = 0; Update();
    }

    private void OpenHelp(bool troubleshoot = false)
    {
        var help = new NotesWindow(); help.EnableHelp(troubleshoot); help.Show();
    }
}

/// <summary>A Help destination using the same compact operational bridge.</summary>
internal sealed class TroubleshootingWindow : Window
{
    public TroubleshootingWindow()
    {
        Title = "Troubleshooting / DERT Help"; Width = 620; Height = 420; MinWidth = 460; MinHeight = 320;
        Background = Palette.Background; Foreground = Palette.Text; FontFamily = new("Cascadia Mono, Consolas");
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/DERT.Notes;component/Theme.xaml", UriKind.Relative) });
        var stack = new StackPanel { Margin = new(26) };
        stack.Children.Add(NotesWindow.Label("Follow the evidence", Palette.Text, 19));
        stack.Children.Add(NotesWindow.Label("The compact window's Tools menu runs the same DERT doctor and saved approvals as the CLI. Start with a check; keep its complete output when it needs more attention.", Palette.Muted));
        stack.Children.Add(NotesWindow.Action("Open doctoring", () =>
        {
            var note = new NotesWindow(compact: true); note.Show(); note.OpenCommandPanel("where codex --doctor");
        }));
        stack.Children.Add(NotesWindow.Label("1. Find the installation: DERT where codex\n\n2. Inspect a repair: DERT where codex --doctor\n\n3. Review the exact copy and offered approval. Apply only the command bound to that proposal. Changed evidence refuses an old approval.", Palette.Text));
        stack.Children.Add(NotesWindow.Label("For note syntax: open the file in the Help workbench. Unfinished notes remain readable; previews explain a correction before changing words.", Palette.Muted));
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
