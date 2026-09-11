using Dert.Core;
using Dert.Cli.Motion;
using Dert.Mer;
using Spectre.Console;

namespace Dert.Cli.Authoring;

internal static class ContextPresentation
{
    internal static DocumentAnnotation Annotation(ContextPacket packet)
    {
        var selection = packet.Selection;
        var primary = selection.Primary;
        var structure = new DertStructureIndex(selection.Document);
        var missing = packet.Included.Count(note => !note.IsClosed);
        var lines = new List<InkLine>
        {
            InkLine.Plain("SELECTED CONTEXT", Ink.Signal),
            InkLine.Plain(missing > 0 ? $"Closing // missing in {missing} selected notes" : "Selected notes have their own //", missing > 0 ? Ink.Copper : Ink.Muted),
            InkLine.Plain($"{packet.Included.Count} {(packet.Included.Count == 1 ? "note" : "notes")} / own annotations retained"),
            InkLine.Plain("Selected / lines " + string.Join(", ", packet.Included.Take(3).Select(note => note.LineNumber)) + (packet.Included.Count > 3 ? $" +{packet.Included.Count - 3} / details" : ""), Ink.Signal),
            InkLine.Plain($"Left out / {packet.Excluded.Count} source ranges", Ink.Copper),
            InkLine.Plain("Names do not select other notes", Ink.Muted),
            InkLine.Plain("Preview / exact text in details", Ink.Muted)
        };
        return new(primary.LineNumber, lines, lines)
        {
            CompactRowBudget = 7,
            Ownership = StructureConnection.Describe(structure, primary.LineNumber).Ownership,
            Action = new("Context review", StageState.Waiting, "source unchanged")
        };
    }

    internal static void Draw(ContextPacket packet, IAnsiConsole console, bool plain)
    {
        var selection = packet.Selection;
        var analysis = NoteScore.Analyze(() => selection.Document.Source.Text, selection.SourceLabel, false);
        var journal = new MotionJournal(analysis.Score, DateTimeOffset.Now);
        foreach (var observed in analysis.Events) journal.Apply(observed, TimeSpan.Zero);
        using var terminal = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
        var size = MerHost.Size(console);
        terminal.Finish(MerCompositor.Document(journal.Snapshot(TimeSpan.Zero), size.Columns, size.Rows,
            selection.Primary.LineNumber, Annotation(packet)).Lines);
    }
}
