using System.Globalization;
using Dert.Core;
using Erpdert.Mer;

namespace Erpdert.Cli.Motion;

internal sealed partial class MerReview
{
    private DertStructureIndex? structureIndex = analysis.Document is { } parsed ? new(parsed) : null;
    private bool showStructure;
    private IReadOnlyList<SourceHit> searchHits = [];
    private string? searchText;

    private bool InOperationContext(int line)
    {
        var node = structureIndex?.AtLine(line);
        while (node is not null)
        {
            if (node.Node is ChainNode chain) return chain.Header.Hook is not null || chain.Header.Line is not null;
            node = node.Parent is { } parent ? structureIndex!.AtLine(parent.LineNumber) : null;
        }
        return false;
    }

    private void ResetStructure()
    {
        structureIndex = analysis.Document is { } parsed ? new(parsed) : null;
        showStructure = false;
        if (searchText is not null) Say("Search cleared after the source changed. Use find again on the verified text.", Ink.Muted);
        searchHits = [];
        searchText = null;
    }

    private DocumentAnnotation? StructureAnnotation()
    {
        if (structureIndex is null || selected is not int line) return null;
        var annotation = StructureConnection.Describe(structureIndex, line);
        var hit = searchHits.Select((value, index) => (value, index)).FirstOrDefault(item => item.value.LineNumber == line);
        if (hit.value is null) return annotation;
        var label = InkLine.Plain($"Hit {hit.index + 1} of {searchHits.Count} / line {line}", Ink.Copper);
        return annotation with
        {
            Lines = [label, .. annotation.Lines],
            CompactLines = [label, .. (annotation.CompactLines ?? annotation.Lines).Take(4)]
        };
    }

    private bool HandleStructure(string answer)
    {
        var command = answer.Split(' ', 2)[0].ToLowerInvariant();
        if (command is not ("find" or "hit" or "hits" or "owner" or "structure")) return false;
        if (structureIndex is null) { Say("No captured source is available to inspect.", Ink.Muted); return true; }
        switch (command)
        {
            case "structure":
                if (!answer.Equals("structure", StringComparison.OrdinalIgnoreCase)) Say("Use structure to inspect the selected source line.", Ink.Muted);
                else if (selected is null) Select(structureIndex.Locations.FirstOrDefault()?.Node.LineNumber ?? 1, true);
                else { showStructure = true; repairAnnotation = null; Show(); }
                break;
            case "owner":
                if (answer.Equals("owner", StringComparison.OrdinalIgnoreCase) && selected is int line && structureIndex.OwnerLine(line) is int parent)
                    Select(parent, true);
                else Say("This selection belongs directly to the document, or has no selected owner. Your selection is unchanged.", Ink.Muted);
                break;
            case "find":
                if (answer.Length <= 5 || string.IsNullOrWhiteSpace(answer[5..]))
                { Say("Use find <text> to search the captured source literally. Your search is unchanged.", Ink.Muted); break; }
                var query = answer[5..];
                var found = structureIndex.Find(query);
                if (found.Count == 0) { Say("No literal matches in the captured source. Your selection and previous search are unchanged.", Ink.Muted); break; }
                searchHits = found;
                searchText = query;
                Say($"{found.Count} matching lines / captured source / literal, case-insensitive", Ink.Signal);
                ShowHits(1);
                Select(found[0].LineNumber, true);
                break;
            case "hit":
                if (TryNumber(answer, 4, out var choice) && choice <= searchHits.Count) Select(searchHits[choice - 1].LineNumber, true);
                else Say("No search hit has that number. Your selection is unchanged.", Ink.Muted);
                break;
            case "hits":
                if (answer.Equals("hits", StringComparison.OrdinalIgnoreCase)) ShowHits(1);
                else if (TryNumber(answer, 5, out var page)) ShowHits(page);
                else Say("Use hits <page> with a positive page number. Your selection is unchanged.", Ink.Muted);
                break;
        }
        return true;

        static bool TryNumber(string text, int offset, out int number)
        {
            number = 0;
            return text.Length > offset && int.TryParse(text.AsSpan(offset), NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;
        }
    }

    private void ShowHits(int page)
    {
        const int perPage = 8;
        var pages = (searchHits.Count + perPage - 1) / perPage;
        if (searchHits.Count == 0) { Say("No captured search yet. Use find <text>.", Ink.Muted); return; }
        if (page < 1 || page > pages) { Say($"Choose hits 1-{pages}. Your selection is unchanged.", Ink.Muted); return; }
        var start = (page - 1) * perPage;
        Say($"Hits {start + 1}-{Math.Min(start + perPage, searchHits.Count)} of {searchHits.Count} / {searchText}", Ink.Signal);
        var width = Math.Max(1, MerHost.Size(console).Columns - 1);
        for (var i = start; i < Math.Min(start + perPage, searchHits.Count); i++)
        {
            var hit = searchHits[i];
            Say($"hit {i + 1} / line {hit.LineNumber} / {structureIndex!.DescribeLine(hit.LineNumber)}", Ink.Muted);
            Say(TerminalText.Clip(hit.Text.TrimStart('\uFEFF'), width), Ink.Text);
        }
        Say($"hit 1-{searchHits.Count} selects source" + (pages > 1 ? $" / hits 1-{pages} turns pages" : ""), Ink.Muted);
    }

    private void StructureDetails()
    {
        if (selected is null || structureIndex is null) return;
        Say("SELECTED SOURCE / captured structure", Ink.Signal);
        if (searchText is not null) Say($"Search / {searchText} / {searchHits.Count} matching lines", Ink.Muted);
        foreach (var line in StructureConnection.Describe(structureIndex, selected.Value).Lines) Say(line.Text, line.Runs.FirstOrDefault()?.Ink ?? Ink.Text);
    }
}
