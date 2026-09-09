namespace Dert.Core;

public enum ListAction { Add, Finish }

public sealed class ListEditPlan : ISourceRepairPlan
{
    public string Source { get; }
    public string ProposedText { get; }
    public string Explanation { get; }
    public string Kind => "active-list-edit-v1";
    public ListAction Action { get; }
    public bool IsReady => Edits.Count > 0;
    public SourceEdit? Edit { get; }
    public IReadOnlyList<SourceEdit> Edits { get; }
    public int StatementLine { get; }
    public int LastLine { get; }
    public int? NewItemCaret { get; }
    public string BeforeLine { get; }
    public IReadOnlyList<string> AfterLines { get; }

    internal ListEditPlan(DertDocument document, ListAction action, string explanation,
        StatementNode? statement = null, IReadOnlyList<SourceEdit>? edits = null, int? newItemCaret = null)
    {
        Source = document.Source.Text; Action = action; Explanation = explanation;
        StatementLine = statement?.LineNumber ?? 0;
        LastLine = statement?.Values.LastOrDefault()?.LineNumber ?? 0;
        NewItemCaret = newItemCaret;
        Edits = Array.AsReadOnly((edits ?? []).OrderBy(edit => edit.Span.Start).ToArray());
        ProposedText = Source;
        foreach (var edit in Edits.Reverse()) ProposedText = ProposedText[..edit.Span.Start] + edit.After + ProposedText[edit.Span.End..];
        if (Edits.Count > 0)
        {
            var start = Edits[0].Span.Start;
            var end = Edits[^1].Span.End;
            var delta = ProposedText.Length - Source.Length;
            Edit = new(new(start, end - start), Source[start..end], ProposedText[start..(end + delta)]);
        }
        BeforeLine = LastLine == 0 ? "" : document.Source.Slice(document.Source.Lines[LastLine - 1].ContentSpan);
        var after = new SourceText(ProposedText);
        AfterLines = Array.AsReadOnly(LastLine == 0 ? [] : after.Lines.Skip(LastLine - 1)
            .Take(IsReady && action == ListAction.Add ? 2 : 1).Select(line => after.Slice(line.ContentSpan)).ToArray());
    }
}

public static class ListCompletion
{
    public static StatementNode? StatementAt(DertDocument document, int line)
    {
        var at = new DertStructureIndex(document).AtLine(line);
        var statement = at?.Node as StatementNode ?? at?.Parent as StatementNode;
        return statement is { Header.Line: not null, Header.Payload.Text.Length: 0, Values.Count: > 0 } ? statement : null;
    }

    public static ListEditPlan Plan(DertDocument document, int line, ListAction action, string? item = null)
    {
        var statement = StatementAt(document, line);
        if (statement is null) return new(document, action, "Select an expanded list's header or one of its active items. Notes and ordinary prose are left alone.");
        var last = statement.Values[^1];
        if (last.Value.Text.Length == 0) return Unavailable("The last item has no words yet. Write that item before completing this list.");
        var source = document.Source;
        var lastLine = source.Lines[last.LineNumber - 1];
        var annotation = HeaderReader.FindAnnotation(source.Text, lastLine.Start, lastLine.ContentEnd);
        if (HeaderReader.HasUnclosedQuote(source.Text, lastLine.Start, annotation < 0 ? lastLine.ContentEnd : annotation))
            return Unavailable("This item's quoted text is unfinished. Close its quote before changing the list punctuation; your words are retained.");
        var edits = new List<SourceEdit>();
        if (action == ListAction.Finish)
        {
            if (!last.HasComma) return Unavailable("This list already ends with its last item. Nothing needs removing.");
            var comma = last.Value.Span.End;
            while (comma < lastLine.ContentEnd && char.IsWhiteSpace(source.Text[comma])) comma++;
            if (comma >= lastLine.ContentEnd || source.Text[comma] != ',') return Unavailable("The continuation comma could not be located in this source. Leave the text and inspect the item.");
            edits.Add(new(new(comma, 1), ",", ""));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(item)) return Unavailable("Write the next item to see its exact preview. Nothing is added until Apply.");
            if (item.Any(character => character is '\r' or '\n' or '\0')) return Unavailable("Add one item on one physical line. Your original list is unchanged.");
            var itemAnnotation = HeaderReader.FindAnnotation(item, 0, item.Length);
            if (HeaderReader.HasUnclosedQuote(item, 0, itemAnnotation < 0 ? item.Length : itemAnnotation))
                return Unavailable("The proposed item's quoted text is unfinished. Close its quote to review where the item ends.");
            if (!last.HasComma) edits.Add(new(new(last.Value.Span.End, 0), "", ","));
            var newline = source.Text[lastLine.ContentEnd..lastLine.End];
            if (newline.Length == 0)
            {
                newline = source.Lines.Take(last.LineNumber).Reverse().Select(previous => source.Text[previous.ContentEnd..previous.End])
                    .FirstOrDefault(ending => ending.Length > 0) ?? "\r\n";
                edits.Add(new(new(lastLine.End, 0), "", newline + item));
            }
            else edits.Add(new(new(lastLine.End, 0), "", item + newline));
        }

        var candidate = new ListEditPlan(document, action, "", statement, edits);
        var parsed = DertParser.Parse(candidate.ProposedText);
        var revised = new DertStructureIndex(parsed).AtLine(statement.LineNumber)?.Node as StatementNode;
        if (revised is null || revised.Values.Count != statement.Values.Count + (action == ListAction.Add ? 1 : 0))
            return Unavailable(action == ListAction.Add
                ? "That addition would become another structure or pull following text into the list. Enter one item; an annotation may follow //, and its final comma can be left out."
                : "Removing this comma would change the list's ownership. Leave it and inspect the source.");
        for (var index = 0; index < statement.Values.Count; index++)
        {
            var before = statement.Values[index];
            var after = revised.Values[index];
            var expectedComma = index == statement.Values.Count - 1 ? action == ListAction.Add : before.HasComma;
            if (before.Value.Text != after.Value.Text || before.Annotation?.Text != after.Annotation?.Text || after.HasComma != expectedComma)
                return Unavailable("That comma also touches the item's literal text. This choice cannot preserve those words exactly; leave the source for a deliberate edit.");
        }
        if (action == ListAction.Finish)
            return new(document, action, $"Finish the {statement.Values.Count}-item list on line {statement.LineNumber}. Only the final continuation comma is removed; its annotation stays with the item.", statement, edits);

        var added = revised.Values[^1];
        if (added.LineNumber != last.LineNumber + 1 || added.Value.Text.Length == 0)
            return Unavailable("That text does not become one new list item. Enter its words first; an annotation may follow //.");
        return new(document, action, $"Add item {revised.Values.Count} to the list on line {statement.LineNumber}. " +
            (added.HasComma ? "Its comma still leaves room for another item." : "The new last item ends the list."), statement, edits, added.Value.Span.End);

        ListEditPlan Unavailable(string explanation) => new(document, action, explanation, statement);
    }
}
