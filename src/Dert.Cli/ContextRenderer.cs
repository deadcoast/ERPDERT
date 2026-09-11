using Dert.Core;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer
{
    internal void ContextReview(ContextPacket packet, bool details = false)
    {
        Line("Context review / " + packet.ReviewId, SemanticRole.Reasoning);
        Line("Source revision / " + packet.Selection.SourceRevision, SemanticRole.ContextNote);
        Line("Review every selected word with --details; --text emits the exact text for deliberate copying. No agent receives it automatically.", SemanticRole.Text);
        if (details) ContextText(packet.Report);
        var command = $"{Invocation()} context {PowerShellLiteral(packet.Selection.SourceLabel)} --at {packet.Selection.Primary.LineNumber}";
        foreach (var note in packet.Included.Where(note => note.LineNumber != packet.Selection.Primary.LineNumber)) command += $" --include {note.LineNumber}";
        Line("Review this selection from the current file:", SemanticRole.Text);
        CopyCommand(command + " --details" + (plain ? " --plain" : ""));
        Line("Export this reviewed text to a new file; choose another .txt path if this one exists:", SemanticRole.Text);
        CopyCommand(command + " --output " + PowerShellLiteral(packet.Selection.SourceLabel + ".context.txt") +
            " --from-preview " + packet.ReviewId + (plain ? " --plain" : ""));
    }

    internal void ContextText(string text)
    {
        // Terminal display escapes control characters. --text and export retain
        // the exact reviewed text without this display transformation.
        foreach (var line in new SourceText(text).Lines) Line(text.Substring(line.Start, line.ContentEnd - line.Start));
    }

    internal void ContextSaved(string path, string review)
    {
        Line("Export saved / bytes verified", SemanticRole.Sinker);
        Line(path, SemanticRole.Text);
        Line("Reviewed text / " + review, SemanticRole.ContextNote);
        Line("Source unchanged. The text has not been sent to an agent.", SemanticRole.Text);
    }
}
