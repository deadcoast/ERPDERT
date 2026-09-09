namespace Dert.Core;

public sealed record LiteralQuote(string Text, IReadOnlyList<int> InsertedQuotes, bool IsAvailable, string Explanation);

public sealed record LiteralEditPlan(string Source, string ProposedText, string Explanation,
    SourceEdit? Edit, string BeforeReading, string AfterReading) : ISourceRepairPlan
{
    public string Kind => "literal-quote-v1";
    public bool IsReady => Edit is not null;
    public IReadOnlyList<int> InsertedQuotes { get; init; } = [];
    public int LineNumber { get; init; }
}

public static class LiteralText
{
    /// <summary>Adds quote boundaries using the current reader. It never escapes or decodes words.</summary>
    public static LiteralQuote Protect(string words)
    {
        if (words.Length == 0 || words.Any(c => c is '\r' or '\n' or '\0'))
            return Unavailable("Select words on one physical line. Your text is retained.");
        if (HeaderReader.HasUnclosedQuote(words, 0, words.Length))
            return Unavailable("A quote is still open. Keep writing its ending before using this convenience; no ending or escape is guessed.");

        var additions = new List<int>();
        var outsideStart = 0;
        var quoted = false;
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i] != '"' || HeaderReader.IsEscaped(words, i)) continue;
            if (!quoted) ProtectPart(outsideStart, i);
            else outsideStart = i + 1;
            quoted = !quoted;
        }
        ProtectPart(outsideStart, words.Length);
        var result = new System.Text.StringBuilder(words.Length + additions.Count);
        var inserted = new List<int>();
        var cursor = 0;
        foreach (var position in additions)
        {
            result.Append(words.AsSpan(cursor, position - cursor));
            inserted.Add(result.Length); result.Append('"'); cursor = position;
        }
        result.Append(words.AsSpan(cursor));
        var candidate = result.ToString();
        // This is the real sinker reader, including reserved boundary words,
        // annotation recognition, trimming and terminal continuation commas.
        var parsed = DertParser.Parse("items ; literal ;;\n" + candidate);
        if (parsed.Nodes is not [StatementNode { Values: [var item] }] || item.Value.Text != candidate || item.HasComma || item.Annotation is not null)
            return Unavailable("These words do not remain one complete value with quote marks alone. Keep the source for an explicit edit.");
        return new(candidate, inserted.AsReadOnly(), true, additions.Count == 0
            ? "These words already stay together. No quote marks need adding."
            : "Only the highlighted quote marks are added. Every typed character stays; the quotes also remain in saved source.");

        LiteralQuote Unavailable(string why) => new(words, [], false, why);

        void ProtectPart(int start, int end)
        {
            if (start == end) return;
            var part = words[start..end];
            var annotation = part.Contains("//", StringComparison.Ordinal) && Enumerable.Range(start, end - start - 1)
                .Any(i => words[i] == '/' && words[i + 1] == '/' && (i == 0 || char.IsWhiteSpace(words[i - 1])));
            var boundaryWord = start == 0 && end == words.Length && part is "STERT" or "fin." or "ƒin.";
            var edgeSpace = start == 0 && char.IsWhiteSpace(words[start]) || end == words.Length && char.IsWhiteSpace(words[end - 1]);
            var needsQuotes = part.Contains(';') || annotation || boundaryWord || edgeSpace || end == words.Length && part.EndsWith(',');
            if (!needsQuotes) return;
            // Leave an odd trailing backslash run outside the new ending. Adding
            // a second backslash would change the author's raw value.
            var close = end;
            if (HeaderReader.IsEscaped(words, end))
                while (close > start && words[close - 1] == '\\') close--;
            if (close <= start) return;
            additions.Add(start); additions.Add(close);
        }
    }

    public static LiteralEditPlan Plan(DertDocument document, TextSpan selection)
    {
        var source = document.Source.Text;
        if (selection.Start < 0 || selection.Length <= 0 || selection.Start > source.Length - selection.Length)
            return Unavailable("Select the words you want to keep together.");
        var index = new DertStructureIndex(document);
        var line = index.LineOf(selection.Start);
        if (selection.End > document.Source.Lines[line - 1].ContentEnd)
            return Unavailable("Select words on one physical line; note boundaries stay outside this choice.");
        var textElements = System.Globalization.StringInfo.ParseCombiningCharacters(source);
        if (Array.BinarySearch(textElements, selection.Start) < 0 || selection.End != source.Length && Array.BinarySearch(textElements, selection.End) < 0)
            return Unavailable("Select whole characters, including their accents, before adding quote marks.");
        var location = index.AtLine(line);
        if (location?.Node is ChainNode or StatementNode)
            return Unavailable("This is an application or statement field. Change its meaning directly in source; literal convenience does not change request fields.");
        var allowed = location?.Node switch
        {
            NoteEntryNode entry => EntrySpan(entry),
            ReasoningNoteNode note => note.Header.Payload?.Span,
            TextNode text => PlainValue(document, text.Content).Span,
            _ => null
        };
        if (allowed is not { } field || selection.Start < field.Start || selection.End > field.End)
            return Unavailable("Select within one value field. Its annotation, hook, line and closing marker stay outside this choice.");
        if (HeaderReader.HasUnclosedQuote(source, field.Start, selection.Start))
            return Unavailable("This selection is already inside quoted text. Its punctuation stays there without another pair of quotes.");
        var quote = Protect(source[selection.Start..selection.End]);
        if (!quote.IsAvailable || quote.InsertedQuotes.Count == 0) return Unavailable(quote.Explanation);
        var proposed = source[..selection.Start] + quote.Text + source[selection.End..];
        var revised = new DertStructureIndex(DertParser.Parse(proposed));
        if (!SameSurroundings(index, revised, line))
            return Unavailable("That quoting would change surrounding ownership or another field. Keep the source; this is more than a literal punctuation edit.");
        // A quote adjoining a backslash or an existing quote can acquire a
        // different meaning in its complete line. Check that exact context too.
        var revisedField = revised.AtLine(line)?.Node switch
        {
            NoteEntryNode entry => entry.Value,
            ReasoningNoteNode note => note.Header.Payload,
            TextNode text => PlainValue(revised.Document, text.Content),
            _ => null
        };
        if (revisedField is null || HeaderReader.HasUnclosedQuote(proposed, revisedField.Span.Start, revisedField.Span.End) ||
            revisedField.Span.Start > selection.Start || revisedField.Span.End < selection.Start + quote.Text.Length)
            return Unavailable("The nearby quote or backslash changes this boundary. Select the complete value to review it together.");
        return new(source, proposed, quote.Explanation, new(selection, source[selection.Start..selection.End], quote.Text),
            DertPunctuation.Reading(index, line), DertPunctuation.Reading(revised, line)) { InsertedQuotes = quote.InsertedQuotes, LineNumber = line };

        LiteralEditPlan Unavailable(string explanation) => new(source, source, explanation, null, "", "");
        TextSpan EntrySpan(NoteEntryNode entry)
        {
            var end = entry.Value.Span.End;
            if (entry.HasComma)
            {
                while (end < document.Source.Lines[line - 1].ContentEnd && char.IsWhiteSpace(source[end])) end++;
                if (end < source.Length && source[end] == ',') end++;
            }
            return new(entry.Value.Span.Start, end - entry.Value.Span.Start);
        }
    }

    private static TextField PlainValue(DertDocument document, TextField content)
    {
        var annotation = HeaderReader.FindAnnotation(document.Source.Text, content.Span.Start, content.Span.End);
        return annotation < 0 ? content : HeaderReader.Field(document.Source, content.Span.Start, annotation);
    }

    private static bool SameSurroundings(DertStructureIndex before, DertStructureIndex after, int changedLine)
    {
        if (before.Locations.Count != after.Locations.Count) return false;
        foreach (var (a, b) in before.Locations.Zip(after.Locations))
        {
            if (a.Node.GetType() != b.Node.GetType() || a.Node.LineNumber != b.Node.LineNumber || a.Parent?.LineNumber != b.Parent?.LineNumber ||
                ClosingLine(before, a.Node) != ClosingLine(after, b.Node)) return false;
            if (a.Node.LineNumber != changedLine)
            {
                var originalLine = before.Document.Source.Lines[a.Node.LineNumber - 1];
                var revisedLine = after.Document.Source.Lines[b.Node.LineNumber - 1];
                if (before.Document.Source.Slice(originalLine.ContentSpan) != after.Document.Source.Slice(revisedLine.ContentSpan)) return false;
            }
            else if (a.Node is NoteEntryNode ae && b.Node is NoteEntryNode be && ae.Annotation?.Text != be.Annotation?.Text) return false;
            else if (a.Node is ReasoningNoteNode an && b.Node is ReasoningNoteNode bn &&
                (an.Header.Head.Text != bn.Header.Head.Text || an.Header.Target?.Text != bn.Header.Target?.Text)) return false;
        }
        return !after.Document.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error &&
            !before.Document.Diagnostics.Any(old => old.Code == d.Code && old.LineNumber == d.LineNumber));

        static int? ClosingLine(DertStructureIndex index, DertNode node) => DertStructureIndex.Closing(node) is { } span ? index.LineOf(span.Start) : null;
    }
}
