using Dert.Core;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer
{
    public void Lint(DertDocument document, string path, string revision)
    {
        Banner("SERNTERX / LINT");
        Line(path, SemanticRole.ContextNote);
        var offered = Findings(document, path, revision);
        Line("");
        if (offered) RehearsalLinksHint();
        Summary(document);
    }

    private bool Findings(DertDocument document, string path, string revision)
    {
        var offered = false;
        foreach (var diagnostic in document.Diagnostics.OrderBy(d => d.LineNumber).ThenBy(d => d.Span.Start))
        {
            Line("");
            Line($"{diagnostic.Code} · {diagnostic.Severity.ToString().ToUpperInvariant()} · {path}:{diagnostic.LineNumber}",
                diagnostic.Severity == DiagnosticSeverity.Error ? SemanticRole.Hook : SemanticRole.Target);
            Line(diagnostic.Message);
            SourceLine(document, document.Source.Lines[diagnostic.LineNumber - 1], true);
            offered |= FindingAdvice(document, diagnostic, path, revision);
        }
        return offered;
    }

    public void MerRehearsals(DertDocument document, string path, string revision)
    {
        if (document.Diagnostics.Count == 0) return;
        try
        {
            Line("");
            Line("FROM THIS ANALYSIS / NEXT STEPS", SemanticRole.Reasoning);
            var offered = false;
            foreach (var diagnostic in document.Diagnostics.OrderBy(finding => finding.LineNumber).ThenBy(finding => finding.Span.Start))
            {
                Line("");
                Line($"{diagnostic.Code} / line {diagnostic.LineNumber}", SemanticRole.Target);
                offered |= FindingAdvice(document, diagnostic, path, revision);
            }
            if (offered)
            {
                Line("");
                RehearsalLinksHint();
            }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            throw new Dert.Mer.MerOutputException(error);
        }
    }

    private bool FindingAdvice(DertDocument document, Diagnostic diagnostic, string path, string revision)
    {
        var advice = LintRepairAdvice.For(document, diagnostic);
        Line(advice.Rehearsal switch
        {
            LintRehearsal.TitleRule => "REHEARSE / TITLE RULE",
            LintRehearsal.NoteBoundary => "CHOOSE / NOTE BOUNDARY",
            LintRehearsal.AuthoringList => "CHOOSE / IN NOTES",
            _ => "NO REPAIR OFFERED"
        }, advice.Rehearsal == LintRehearsal.None ? SemanticRole.ContextNote : SemanticRole.Reasoning);
        Line(advice.Explanation);
        if (advice.Rehearsal == LintRehearsal.AuthoringList)
        {
            Line($"This opens the current file, which may differ from this analysis. Select the list at line {advice.AtLine} and review its current words.", SemanticRole.ContextNote);
            CopyCommand($"{Invocation()} write {PowerShellLiteral(path)}");
            return false;
        }
        if (advice.Rehearsal == LintRehearsal.None) return false;
        var scope = advice.AtLine is int line ? $" --at {line}" : "";
        CopyCommand($"{Invocation()} repair {PowerShellLiteral(path)}{scope} --from-lint {revision}" + (plain ? " --plain" : ""));
        return true;
    }

    private void RehearsalLinksHint()
    {
        Line("Copy a complete PowerShell repair command above to open its preview. It will not ask to write your file.", SemanticRole.Reasoning);
        Line("Repair links check these exact bytes again. If the file has changed, run lint for current links. write opens the current file for authoring.", SemanticRole.ContextNote);
    }

    private void LintSourceMatched() =>
        Line("FROM LINT / source still matches · preview only, no write prompt", SemanticRole.Reasoning);
}
