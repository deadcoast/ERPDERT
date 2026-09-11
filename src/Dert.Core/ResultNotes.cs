using System.Globalization;
using System.Text;

namespace Dert.Core;

public sealed class ResultNotePlan : ISourceRepairPlan
{
    public string Source { get; }
    public string ProposedText { get; }
    public string Explanation { get; }
    public string Kind => "result-note";
    public bool IsReady => Edit is not null;
    public SourceEdit? Edit { get; }
    public ResultObservation Observation { get; }
    public ResultSourceComparison Comparison { get; }
    public string NoteText { get; }
    public TextSpan AddedSpan { get; }

    internal ResultNotePlan(DertDocument document, ResultObservation observation,
        ResultSourceComparison comparison, string explanation, string noteText = "", string separator = "")
    {
        Source = document.Source.Text;
        Observation = observation;
        Comparison = comparison;
        Explanation = explanation;
        NoteText = noteText;
        ProposedText = Source + separator + noteText;
        AddedSpan = new(Source.Length + separator.Length, noteText.Length);
        if (noteText.Length > 0) Edit = new(new(Source.Length, 0), "", separator + noteText);
    }
}

public static class ResultNotes
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private const string DisplayLegend = "Line endings are labeled as \\r, \\n or \\r\\n. BOM and control characters use Unicode escapes; literal backslashes are doubled. These are display marks; source and record strings remain unchanged.\n";

    public static void Validate(ResultObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        Required(observation.Id, "record identifier");
        if (observation.Id.Length > 128 || observation.Id.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.')))
            throw new ArgumentException("The result record identifier must use letters, numbers, dots, underscores or hyphens, up to 128 characters.");
        if (observation.ObservedAt == default) throw new ArgumentException("The result needs its original observation time.");
        if (!Enum.IsDefined(observation.Outcome)) throw new ArgumentException("The result has an unknown outcome.");
        Required(observation.Action, "action");
        Required(observation.Target, "target");
        Required(observation.Summary, "summary");
        Required(observation.Effects, "effects");
        if (observation.Origin is null) throw new ArgumentException("The result needs its captured source origin.");
        Required(observation.Origin.Path, "source path");
        if (observation.Origin.Line < 1) throw new ArgumentException("The result's source line must start at one.");
        var revision = observation.Origin.SourceRevision;
        if (revision is null || revision.Length != 64 || revision.Any(character => !char.IsAsciiHexDigit(character)))
            throw new ArgumentException("The result needs the captured source's 64-character SHA-256 revision.");
        Required(observation.Origin.ChainText, "captured chain text");
        var captured = DertParser.Parse(observation.Origin.ChainText);
        if (captured.Nodes.Count != 1 || captured.Nodes[0] is not ChainNode)
            throw new ArgumentException("The result's original source must contain one captured STERT chain.");
        foreach (var item in observation.Evidence)
        {
            if (item is null) throw new ArgumentException("The result contains an empty evidence entry.");
            Required(item.Label, "evidence label");
            Text(item.Detail, "evidence detail");
        }

        static void Required(string? value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"The result needs its {name}.");
            Text(value, name);
        }
        static void Text(string? value, string name)
        {
            if (value is null) throw new ArgumentException($"The result needs its {name}.");
            try { Utf8.GetByteCount(value); }
            catch (EncoderFallbackException error)
            { throw new ArgumentException($"The result's {name} contains an incomplete Unicode character.", error); }
        }
    }

    public static ResultSourceComparison Compare(DertDocument document, ResultObservation observation,
        string destinationPath, int? atLine = null, bool sourceHasBom = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        Validate(observation);
        var origin = observation.Origin;
        var chains = document.Nodes.OfType<ChainNode>().ToArray();
        var selected = atLine is { } line ? chains.FirstOrDefault(chain =>
            line >= chain.LineNumber && line <= LastLine(document, chain)) : null;
        if (!SamePath(origin.Path, destinationPath))
            return Result("different-document", "This destination is a different document. The historical origin stays with the captured source; any selected chain is only a comparison candidate.", selected);
        if (atLine is not null)
            return selected is null ? Result("missing", "The selected line has no current chain. Choose a chain to compare; the captured origin is unchanged.", null)
                : Match(selected, explicitSelection: true);
        var matches = chains.Where(SameChain).ToArray();
        if (matches.Length > 1)
            return Result("ambiguous", "More than one chain has the captured text. Select the current chain explicitly to compare it; no origin was guessed.", null);
        if (matches.Length == 1) return Match(matches[0], explicitSelection: false);
        var candidate = chains.FirstOrDefault(chain => chain.LineNumber == origin.Line);
        return candidate is null ? Result("missing", "The captured chain was not found in this source. The saved observation still retains its original words.", null)
            : Result("changed", "The chain at the captured line has changed. It is a comparison candidate, not a confirmed link to the historical origin.", candidate);

        ResultSourceComparison Match(ChainNode chain, bool explicitSelection)
        {
            if (!SameChain(chain))
                return Result("changed", "The explicitly selected chain differs from the captured words. It is a comparison candidate; the historical origin has not been reassigned.", chain);
            var bomReading = document.Source.Slice(chain.Span) == origin.ChainText ? ""
                : " The initial file BOM is accounted for separately from the chain words.";
            if (chain.LineNumber != origin.Line)
                return Result("moved", $"The exact captured chain is now at line {Number(chain.LineNumber)}." +
                    (explicitSelection ? " This occurrence was explicitly selected." : " This is the only exact text match.") + bomReading, chain);
            // Notes stores its opened file's BOM separately from the editor text.
            // Reconstitute it only from current metadata, never from the old record.
            var currentSource = sourceHasBom ? "\uFEFF" + document.Source.Text : document.Source.Text;
            var sameRevision = string.Equals(ContextSelection.Hash(currentSource), origin.SourceRevision, StringComparison.OrdinalIgnoreCase);
            return Result("exact", (sameRevision ? "The destination text matches the full captured source revision and chain. This remains a historical observation."
                : "The chain text and line match exactly, while the full source revision has changed. This remains a historical observation.") + bomReading, chain);
        }
        bool SameChain(ChainNode chain) => WithoutInitialBom(document.Source.Slice(chain.Span)).SequenceEqual(WithoutInitialBom(origin.ChainText));
        ResultSourceComparison Result(string state, string message, ChainNode? chain) =>
            new(state, message, chain?.LineNumber, origin.ChainText, chain is null ? null : document.Source.Slice(chain.Span));
    }

    public static ResultNotePlan Plan(DertDocument document, ResultObservation observation,
        string destinationPath, int? atLine = null, bool sourceHasBom = false)
    {
        var comparison = Compare(document, observation, destinationPath, atLine, sourceHasBom);
        var source = document.Source.Text;
        var index = new DertStructureIndex(document);
        if (index.Locations.Any(location => location.Node is ReasoningBlockNode block &&
            block.Header.Head.Text == "observed result" && block.Entries.Any(entry =>
                entry.Value.Text == "record" && entry.Annotation?.Text == observation.Id)))
            return Refuse("This result record is already present in a result note. Keep that existing note or choose another observation.");
        if (index.Locations.Any(location => location.Node is ChainNode or ReasoningBlockNode or ReasoningNoteNode &&
            DertStructureIndex.Closing(location.Node) is null && source.AsSpan(location.Node.Span.End).Trim().Length == 0))
            return Refuse("A result appended here would become part of the final chain or unfinished note. Give that owner an explicit ending before appending a standalone result; no closer was added.");

        // Continue the latest written ending. In particular, never join a final CR
        // to an appended LF and silently turn the author's ending into CRLF.
        var newline = document.Source.Lines.Select(line => source[line.ContentEnd..line.End]).LastOrDefault(ending => ending.Length > 0) ?? "\n";
        var separator = source.Length == 0 ? "" : source[^1] is '\r' or '\n' ? newline : newline + newline;
        var note = new StringBuilder();
        note.Append("// observed result ; observation ;;").Append(newline);
        Entry("action", observation.Action);
        Entry("target", observation.Target);
        Entry("observed", observation.ObservedAt.ToString("O", CultureInfo.InvariantCulture));
        Entry("outcome", observation.Outcome.ToString());
        Entry("summary", observation.Summary);
        Entry("effects", observation.Effects);
        Entry("source", observation.Origin.Path);
        Entry("source line", Number(observation.Origin.Line));
        Entry("revision", observation.Origin.SourceRevision);
        Entry("record", observation.Id);
        Entry("history", "Recorded observation reported by its producer. Keeping this note does not repeat or verify the operation.");
        Entry("comparison", comparison.State + " / " + comparison.Message);
        foreach (var evidence in observation.Evidence)
        {
            Entry("evidence label", evidence.Label);
            Entry("evidence", evidence.Detail);
        }
        note.Append("//").Append(newline);
        var plan = new ResultNotePlan(document, observation, comparison,
            "Append this standalone result note after your existing source. Apply inserts the displayed text as one undoable edit; Save keeps it in the file.", note.ToString(), separator);
        var parsed = DertParser.Parse(plan.ProposedText);
        if (parsed.Nodes.LastOrDefault() is not ReasoningBlockNode { Closing: not null } added || added.Span != plan.AddedSpan ||
            !PreservesOriginal(document, parsed) ||
            parsed.Diagnostics.Any(finding => finding.Span.Start >= plan.AddedSpan.Start))
            return Refuse("The added note would change existing ownership or could not be read as a separate reasoning note. Finish the surrounding source and review again; your source is unchanged.");
        return plan;

        ResultNotePlan Refuse(string explanation) => new(document, observation, comparison, explanation);
        void Entry(string label, string value)
        {
            foreach (var line in new SourceText(value).Lines)
                note.Append("// ").Append(label).Append(", // ").Append(Safe(value[line.Start..line.ContentEnd])).Append(newline);
        }
    }

    public static string DescribeComparison(ResultSourceComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        var report = new StringBuilder("DERT source comparison\n\n");
        AppendField(report, "State", comparison.State);
        AppendField(report, "Meaning", comparison.Message);
        if (comparison.Line is { } line) AppendField(report, "Current chain line", Number(line));
        report.Append('\n');
        AppendField(report, "Captured chain text", comparison.CapturedText);
        report.Append('\n');
        if (comparison.CurrentText is { } current)
            AppendField(report, "Current chain text", current);
        else AppendField(report, "Current chain text / unavailable", "No current chain is selected or matched. The captured source remains available above.");
        report.Append('\n').Append(DisplayLegend);
        return report.ToString();
    }

    public static string Describe(ResultObservation observation)
    {
        Validate(observation);
        var report = new StringBuilder("DERT recorded result\nHistorical observation reported by its producer; this review performs no new check.\n\n");
        Field("Record", observation.Id);
        Field("Observed", observation.ObservedAt.ToString("O", CultureInfo.InvariantCulture));
        Field("Action", observation.Action);
        Field("Target", observation.Target);
        Field("Outcome", observation.Outcome.ToString());
        Field("Summary", observation.Summary);
        Field("Effects", observation.Effects);
        Field("Captured source", observation.Origin.Path);
        Field("Captured chain line", Number(observation.Origin.Line));
        Field("Captured source SHA-256", observation.Origin.SourceRevision);
        report.Append("\nEvidence / ").Append(Number(observation.Evidence.Count)).Append(" entries\n");
        if (observation.Evidence.Count == 0) report.Append("No evidence entries were retained.\n");
        for (var index = 0; index < observation.Evidence.Count; index++)
        {
            Field("Evidence " + Number(index + 1) + " label", observation.Evidence[index].Label);
            Field("Evidence " + Number(index + 1) + " detail", observation.Evidence[index].Detail);
        }
        report.Append('\n');
        Field("Captured chain text", observation.Origin.ChainText);
        report.Append(DisplayLegend).Append("Imported records are not authenticated.\n");
        return report.ToString();
        void Field(string label, string value) => AppendField(report, label, value);
    }

    public static string CompareObservations(ResultObservation before, ResultObservation after)
    {
        Validate(before);
        Validate(after);
        var report = new StringBuilder("DERT observation comparison\nTwo retained historical observations; no operation has been repeated.\n\n");
        Difference("Record", before.Id, after.Id);
        Difference("Observed", before.ObservedAt.ToString("O", CultureInfo.InvariantCulture), after.ObservedAt.ToString("O", CultureInfo.InvariantCulture));
        Difference("Action", before.Action, after.Action);
        Difference("Target", before.Target, after.Target);
        Difference("Outcome", before.Outcome.ToString(), after.Outcome.ToString());
        Difference("Summary", before.Summary, after.Summary);
        Difference("Effects", before.Effects, after.Effects);
        report.Append("\nEvidence comparison\n");
        var remaining = after.Evidence.ToList();
        var removed = new List<ResultEvidence>();
        foreach (var entry in before.Evidence)
        {
            var match = remaining.IndexOf(entry);
            if (match >= 0) remaining.RemoveAt(match);
            else removed.Add(entry);
        }
        foreach (var entry in removed) Evidence("Removed", entry);
        foreach (var entry in remaining) Evidence("Added", entry);
        if (removed.Count == 0 && remaining.Count == 0)
            report.Append(before.Evidence.SequenceEqual(after.Evidence) ? "Evidence entries are unchanged, including their order and repeated entries.\n"
                : "The same evidence entries are retained, but their order changed.\n");
        report.Append("\nSource comparison / historical origins\n");
        Difference("Source path", before.Origin.Path, after.Origin.Path);
        Difference("Source line", Number(before.Origin.Line), Number(after.Origin.Line));
        Difference("Source SHA-256", before.Origin.SourceRevision, after.Origin.SourceRevision);
        Difference("Captured chain text", before.Origin.ChainText, after.Origin.ChainText);
        report.Append("These source differences do not reassign either observation to a current chain.\n").Append(DisplayLegend);
        return report.ToString();

        void Difference(string label, string left, string right)
        {
            if (left == right) AppendField(report, label + " / unchanged", left);
            else { AppendField(report, label + " / before", left); AppendField(report, label + " / after", right); }
        }
        void Evidence(string change, ResultEvidence entry)
        {
            AppendField(report, change + " evidence label", entry.Label);
            AppendField(report, change + " evidence detail", entry.Detail);
        }
    }

    private static bool PreservesOriginal(DertDocument original, DertDocument proposed)
    {
        var before = new DertStructureIndex(original).Locations;
        var after = new DertStructureIndex(proposed).Locations.Where(location => location.Node.Span.Start < original.Source.Text.Length).ToArray();
        if (before.Count != after.Length) return false;
        for (var index = 0; index < before.Count; index++)
        {
            var left = before[index];
            var right = after[index];
            if (left.Node.GetType() != right.Node.GetType() || left.Node.LineNumber != right.Node.LineNumber ||
                left.Node.Span.Start != right.Node.Span.Start || left.Node.Span.End != Math.Min(right.Node.Span.End, original.Source.Text.Length) ||
                left.Parent?.Span.Start != right.Parent?.Span.Start || left.Parent?.GetType() != right.Parent?.GetType() ||
                DertStructureIndex.Closing(left.Node) != DertStructureIndex.Closing(right.Node) ||
                DertStructureIndex.Label(left.Node) != DertStructureIndex.Label(right.Node)) return false;
        }
        return true;
    }

    private static int LastLine(DertDocument document, DertNode node) => new DertStructureIndex(document).LineOf(Math.Max(node.Span.Start, node.Span.End - 1));
    private static ReadOnlySpan<char> WithoutInitialBom(string text) => text.AsSpan(text.StartsWith('\uFEFF') ? 1 : 0);
    private static bool SamePath(string left, string right)
    {
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
    }
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static void AppendField(StringBuilder report, string label, string value)
    {
        report.Append(label).Append(':').Append('\n');
        foreach (var line in new SourceText(value).Lines)
        {
            report.Append("  | ").Append(Safe(value[line.Start..line.ContentEnd], escapeBackslashes: true));
            if (line.End > line.ContentEnd)
                report.Append(" [line ending: ").Append(value[line.ContentEnd..line.End].Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)).Append(']');
            report.Append('\n');
        }
    }
    private static string Safe(string value, bool escapeBackslashes = false)
    {
        var result = new StringBuilder();
        foreach (var character in value)
            if (escapeBackslashes && character == '\\') result.Append("\\\\");
            else if (char.IsControl(character) || character is '\ufeff' or '\u2028' or '\u2029' or '\u202a' or '\u202b' or '\u202c' or '\u202d' or '\u202e' or '\u2066' or '\u2067' or '\u2068' or '\u2069')
                result.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
            else result.Append(character);
        return result.ToString();
    }
}
