using Dert.Core;
using System.Globalization;
using System.Text;
using Erpdert.Cli.Repairs;
using Spectre.Console;

namespace Erpdert.Cli;

internal sealed partial class DocumentRenderer
{
    public void RepairPreview(RepairSnapshot snapshot)
    {
        Banner(snapshot.IsUndo ? "SERNTERX / UNDO REHEARSAL" : "SERNTERX / REPAIR REHEARSAL");
        Line(snapshot.Path, SemanticRole.ContextNote);
        Line("");
        if (!snapshot.CanApply)
        {
            Line("UNCHANGED", SemanticRole.Title);
            Line(snapshot.IsUndo ? "These original bytes are already restored." : snapshot.Plan.Explanation);
            Line("No source bytes were written. No new recovery copy was created.", SemanticRole.ContextNote);
            return;
        }

        var plan = snapshot.Plan;
        var before = DertParser.Parse(snapshot.Before);
        var after = DertParser.Parse(snapshot.After);
        Line(snapshot.IsUndo ? "RESTORE / EXACT ORIGINAL" : $"DERT2003 / TITLE RULE · LINE {plan.RuleLine}", SemanticRole.Hook);
        Line(snapshot.IsUndo
            ? "Reverse the latest title-rule edit. Restore the original bytes, including their line endings."
            : plan.Explanation);
        Line("");
        DiffRow(" ", plan.TitleLine, before.Source.Slice(before.Source.Lines[plan.TitleLine - 1].ContentSpan),
            Style(SemanticRole.Title));
        var insertion = plan.Edit!.Before.Length == 0;
        if (!insertion || snapshot.IsUndo)
            DiffRow("−", plan.RuleLine, before.Source.Slice(before.Source.Lines[plan.RuleLine - 1].ContentSpan), RemovedStyle());
        if (!insertion || !snapshot.IsUndo)
            DiffRow("+", plan.RuleLine, after.Source.Slice(after.Source.Lines[plan.RuleLine - 1].ContentSpan), AddedStyle());
        Line("");
        Line("− removed   + added   ·   signs and line numbers are display-only", SemanticRole.ContextNote);
        if (!snapshot.IsUndo)
        {
            Line($"{plan.Width} title columns → {plan.Width} rule marks. Title words and following content are unchanged.", SemanticRole.Line);
            var originalNewline = before.Source.Text[before.Source.Lines[plan.TitleLine - 1].ContentEnd..before.Source.Lines[plan.TitleLine - 1].End];
            if (insertion && originalNewline.Length == 0)
                Line("This title ends at EOF with no line ending to reuse. The new line uses Windows CRLF; no final newline is added.", SemanticRole.ContextNote);
            else
                Line("Existing line endings, UTF-8 BOM, and end-of-file style are preserved.", SemanticRole.ContextNote);
            Line("Before writing: save and verify a full, unencrypted local undo copy beside this file in .erpdert/repairs.", SemanticRole.ContextNote);
        }
        else
            Line("The saved original stays available after undo. Later edits are never silently replaced.", SemanticRole.ContextNote);
        Line("Your choice belongs to this file and this exact edit. Changed source, file identity, or undo selection cancels it.", SemanticRole.ContextNote);
        Line("");
    }

    public void RepairWaiting(RepairSnapshot snapshot)
    {
        Line("PREVIEW ONLY · nothing written", SemanticRole.Hook);
        Line("To approve this exact edit in PowerShell, copy the complete command:");
        CopyCommand(ApplyCommand(snapshot));
        Line("Or run the same command without --apply in an interactive terminal to choose there.", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    public void RepairLeft()
    {
        Line("LEFT UNCHANGED · your source and recovery storage were not touched", SemanticRole.Title);
        Line("ƒin.", SemanticRole.Boundary);
    }

    public void RepairCompleted(RepairSnapshot snapshot, RepairResult result)
    {
        console.Write(new Text(snapshot.IsUndo ? "✓ ORIGINAL RESTORED" : "✓ TITLE ALIGNED", AddedStyle()));
        console.WriteLine();
        Line(result.Message);
        Line("Saved original  " + Path.Combine(result.ReceiptDirectory, "before.DERT"), SemanticRole.ContextNote);
        if (!snapshot.IsUndo)
        {
            Line("Undo rehearsal:", SemanticRole.ContextNote);
            CopyCommand(Invocation() + " undo " + PowerShellLiteral(snapshot.Path));
            Line("Keep the .erpdert folder with this document to retain undo. It contains a full local copy of the original.", SemanticRole.ContextNote);
        }
        var document = DertParser.Parse(snapshot.After);
        if (document.Diagnostics.Count > 0)
        {
            Line("");
            Line(snapshot.IsUndo
                ? $"{Count(document.Diagnostics.Count, "finding")} in the restored original · undo also restores its previous rule"
                : $"{Count(document.Diagnostics.Count, "finding")} still present · this action only changed the title rule", SemanticRole.Hook);
            foreach (var diagnostic in document.Diagnostics)
            {
                Line($"{diagnostic.Code} · LINE {diagnostic.LineNumber} · {diagnostic.Message}");
                SourceLine(document, document.Source.Lines[diagnostic.LineNumber - 1], true);
            }
        }
        Line("ƒin.", SemanticRole.Boundary);
    }

    public void RepairFailure(RepairException error)
    {
        Line("");
        Line($"{error.Code} / REPAIR STOPPED", SemanticRole.Hook);
        Line(error.Message);
        if (error.RecoveryPath is not null)
            Line("Saved original for comparison or recovery  " + error.RecoveryPath, SemanticRole.Sinker);
        Line("ƒin.", SemanticRole.Boundary);
    }

    private void DiffRow(string sign, int number, string content, Style style)
    {
        // Separate gutter and source columns keep wrapped text out of the line-number gutter.
        var grid = new Grid().AddColumn(new GridColumn().Width(9).NoWrap().PadRight(1))
            .AddColumn(new GridColumn());
        grid.AddRow(new Text($"{sign} {number,5} │", style), new Text(Safe(content.TrimStart('\uFEFF')), style));
        console.Write(grid);
    }

    private Style AddedStyle() => plain ? Spectre.Console.Style.Plain : new(new Color(141, 218, 172));
    private Style RemovedStyle() => plain ? Spectre.Console.Style.Plain : new(new Color(245, 146, 157));
    private void CopyCommand(string command)
    {
        // Do not insert Spectre hard-wrap newlines into an executable command.
        // The terminal may soft-wrap it; redirected output retains one complete line.
        console.Profile.Out.Writer.WriteLine(command);
    }

    private static string PowerShellLiteral(string value)
    {
        var result = new StringBuilder("'");
        var expression = false;
        foreach (var rune in value.EnumerateRunes())
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format ||
                rune.Value is >= 0x2018 and <= 0x201B)
            {
                expression = true;
                foreach (var unit in rune.ToString()) result.Append($"' + [char]0x{(int)unit:X4} + '");
            }
            else result.Append(rune.ToString().Replace("'", "''", StringComparison.Ordinal));
        result.Append('\'');
        return expression ? "(" + result + ")" : result.ToString();
    }
    private string Invocation() => invocation is { Length: > 0 }
        ? "& " + string.Join(' ', invocation.Select(PowerShellLiteral)) : "ERPDERT";
    private string ApplyCommand(RepairSnapshot snapshot) =>
        $"{Invocation()} {(snapshot.IsUndo ? "undo" : "repair")} {PowerShellLiteral(snapshot.Path)} --apply {snapshot.ApprovalToken}";
}
