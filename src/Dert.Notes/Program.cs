using System.IO;
using System.Windows;
using Dert.Cli.Authoring;
using Dert.Cli.Results;
using Dert.Core;

namespace Dert.Notes;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            // The CLI and direct host use one validator. Building launch info
            // only normalizes arguments; it does not start another process.
            var normalized = AuthoringCommand.LaunchInfo(args, AppContext.BaseDirectory).ArgumentList.ToArray();
            var path = normalized.FirstOrDefault() is { } first && first != "--still" ? first : null;
            var resultIndex = Array.IndexOf(normalized, "--result");
            var atIndex = Array.IndexOf(normalized, "--at");
            var atLine = atIndex < 0 ? (int?)null : int.Parse(normalized[atIndex + 1], System.Globalization.CultureInfo.InvariantCulture);
            ResultObservation? observation = resultIndex < 0 ? null : ObservationStore.Read(normalized[resultIndex + 1]);
            var app = new Application();
            var window = new NotesWindow(path, normalized.Contains("--still"));
            if (observation is not null)
            {
                if (!window.HasOpenedDocument)
                {
                    window.Close();
                    MessageBox.Show("The destination note could not be opened. Check that file and reopen the result review; nothing was inserted.", "DERT");
                    return 2;
                }
                var reviewed = false;
                window.ContentRendered += (_, _) =>
                {
                    if (reviewed) return;
                    reviewed = true;
                    window.CreateResultReview(observation, atLine)?.ShowDialog();
                };
            }
            return app.Run(window);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { MessageBox.Show(error.Message, "DERT"); return 2; }
    }
}
