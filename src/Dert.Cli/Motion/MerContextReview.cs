using System.Globalization;
using Dert.Core;
using Dert.Cli.Authoring;
using Dert.Mer;

namespace Dert.Cli.Motion;

internal sealed partial class MerReview
{
    private ContextPacket? contextPacket;
    private DocumentAnnotation? contextAnnotation;

    private bool HandleContext(string answer)
    {
        var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || !words[0].Equals("context", StringComparison.OrdinalIgnoreCase)) return false;
        if (structureIndex is null) { Say("No captured source is available for a context selection.", Ink.Muted); return true; }
        try
        {
            if (words.Length == 1)
            {
                if (selected is null) throw new ArgumentException("Use at <line> to choose a reasoning note first.");
                contextPacket = ContextSelection.Capture(structureIndex.Document, selected.Value, Path.GetFullPath(path), ContextSourceKind.FileSnapshot).Build();
                ShowContext(); ContextNotes(1);
                Say("context include <line> / context exclude <line> / context notes <page> / context left / context details / context clear", Ink.Muted);
            }
            else if (words.Length == 2 && words[1].Equals("clear", StringComparison.OrdinalIgnoreCase))
            { contextPacket = null; contextAnnotation = null; Say("Context selection left. Source unchanged.", Ink.Muted); Show(); }
            else if (contextPacket is null) Say("Use context on a reasoning note first. No context selection is captured.", Ink.Muted);
            else if (words.Length == 2 && words[1].Equals("details", StringComparison.OrdinalIgnoreCase)) renderer.ContextReview(contextPacket, true);
            else if (words.Length == 2 && words[1].Equals("left", StringComparison.OrdinalIgnoreCase)) renderer.ContextText(contextPacket.LeftOutReport);
            else if (words.Length is 2 or 3 && words[1].Equals("notes", StringComparison.OrdinalIgnoreCase)) ContextNotes(words.Length == 2 ? 1 : Number(words[2]));
            else if (words.Length == 3 && words[1].ToLowerInvariant() is "include" or "exclude")
            {
                var line = Number(words[2]);
                if (!contextPacket.Selection.Notes.Any(note => note.LineNumber == line)) throw new ArgumentException($"Line {line} is not a reasoning note opener. Use context notes to see the choices.");
                if (words[1].Equals("exclude", StringComparison.OrdinalIgnoreCase) && line == contextPacket.Selection.Primary.LineNumber)
                    throw new ArgumentException("The starting note stays included. Use at <line>, then context to start from another note.");
                var included = contextPacket.Included.Select(note => note.LineNumber).ToHashSet();
                if (words[1].Equals("include", StringComparison.OrdinalIgnoreCase)) included.Add(line); else included.Remove(line);
                contextPacket = contextPacket.Selection.Build(included); ShowContext();
            }
            else Say("Use context, context include/exclude <line>, context notes <page>, context left, context details or context clear.", Ink.Muted);
        }
        catch (ArgumentException error) { Say(error.Message + " Your existing selection is retained.", Ink.Copper); }
        return true;

        static int Number(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number : throw new ArgumentException("Choose a positive source line or offered page number.");
    }

    private void ShowContext() => Select(contextPacket!.Selection.Primary.LineNumber, true, context: ContextPresentation.Annotation(contextPacket));

    private void ContextNotes(int page)
    {
        const int size = 6;
        var packet = contextPacket!;
        var notes = packet.Selection.Notes;
        var pages = Math.Max(1, (notes.Count + size - 1) / size);
        if (page > pages) { Say($"Choose context notes 1-{pages}. Your selection is unchanged.", Ink.Muted); return; }
        Say($"Notes {(page - 1) * size + 1}-{Math.Min(page * size, notes.Count)} of {notes.Count} / context notes 1-{pages}", Ink.Signal);
        foreach (var note in notes.Skip((page - 1) * size).Take(size))
        {
            var state = packet.Included.Any(chosen => chosen.LineNumber == note.LineNumber) ? "included" : "left out";
            Say($"{note.LineNumber} / {state} / {note.Label}" + (note.IsRelated ? " / name match" : ""), state == "included" ? Ink.Signal : Ink.Muted);
        }
        Say("Other notes remain your choice. Name matches do not establish agreement.", Ink.Muted);
    }

    private void ClearContextAfterEdit()
    {
        if (contextPacket is not null) Say("Context selection cleared after the source changed. Use context again on the verified text.", Ink.Muted);
        contextPacket = null; contextAnnotation = null;
    }
}
