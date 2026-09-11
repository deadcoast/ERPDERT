using System.Globalization;
using Dert.Mer;

namespace Dert.Cli.Motion;

internal sealed partial class MerReview
{
    private ReferenceConnection? referenceSet;
    private DocumentAnnotation? referenceAnnotation;

    private bool HandleReferences(string answer)
    {
        var words = answer.Split(' ', 2);
        if (!words[0].Equals("refs", StringComparison.OrdinalIgnoreCase) && !words[0].Equals("ref", StringComparison.OrdinalIgnoreCase)) return false;
        if (structureIndex is null) { Say("No captured source is available to inspect.", Ink.Muted); return true; }
        if (answer.Equals("refs", StringComparison.OrdinalIgnoreCase))
        {
            if (selected is not { } line || ReferenceConnection.Read(structureIndex, line) is not { } found)
            { Say("This line has no reasoning-note names. Use at <line> or find <text>.", Ink.Muted); return true; }
            referenceSet = found;
            ShowReferences(1);
        }
        else if (words[0].Equals("refs", StringComparison.OrdinalIgnoreCase))
        {
            if (Number(out var page) && referenceSet is { } current && page <= current.Pages) ShowReferences(page);
            else Say("Use refs on a note, then refs <page> with an offered page number. Your selection is unchanged.", Ink.Muted);
        }
        else if (answer.Equals("ref back", StringComparison.OrdinalIgnoreCase) && referenceSet is { } origin)
        {
            Say($"Reference origin / line {origin.SourceLine}", Ink.Signal);
            ShowReferences(1);
        }
        else if (Number(out var number) && referenceSet is { } choices && number <= choices.Choices.Count)
        {
            var choice = choices.Choices[number - 1];
            Select(choice.Match.LineNumber, true, choices.Visit(choice));
        }
        else Say("No reference has that number. Use refs on a note first; your selection is unchanged.", Ink.Muted);
        return true;

        bool Number(out int number)
        {
            number = 0;
            return words.Length == 2 && int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;
        }
    }

    private void ShowReferences(int page)
    {
        var annotation = referenceSet!.Describe(page);
        Select(referenceSet.SourceLine, true, annotation);
    }

    private void ReferenceDetails()
    {
        if (referenceAnnotation is null) return;
        foreach (var line in referenceAnnotation.Lines) Say(line.Text, line.Runs.FirstOrDefault()?.Ink ?? Ink.Text);
    }
}
