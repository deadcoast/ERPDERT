using System.Windows;

namespace Erpdert.Notes;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(argument => argument.StartsWith("--", StringComparison.Ordinal) && argument != "--still") || args.Count(argument => argument != "--still") > 1)
        { MessageBox.Show("Use ERPDERT write [file.DERT] [--still].", "ERPDERT"); return 2; }
        var app = new Application();
        return app.Run(new NotesWindow(args.FirstOrDefault(argument => argument != "--still"), args.Contains("--still")));
    }
}
