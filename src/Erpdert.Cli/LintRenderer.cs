using Dert.Core;

namespace Erpdert.Cli;

internal sealed partial class DocumentRenderer
{
    public void Lint(DertDocument document, string path, string revision)
    {
        Banner("SERNTERX / LINT");
        Line(path, SemanticRole.ContextNote);
        var offered = false;
        foreach (var diagnostic in document.Diagnostics.OrderBy(d => d.LineNumber).ThenBy(d => d.Span.Start))
        {
            Line("");
            Line($"{diagnostic.Code} · {diagnostic.Severity.ToString().ToUpperInvariant()} · {path}:{diagnostic.LineNumber}",
                diagnostic.Severity == DiagnosticSeverity.Error ? SemanticRole.Hook : SemanticRole.Target);
            Line(diagnostic.Message);
            SourceLine(document, document.Source.Lines[diagnostic.LineNumber - 1], true);
            var advice = LintRepairAdvice.For(document, diagnostic);
            Line(advice.Rehearsal switch
            {
                LintRehearsal.TitleRule => "REHEARSE / TITLE RULE",
                LintRehearsal.NoteBoundary => "CHOOSE / NOTE BOUNDARY",
                _ => "NO REPAIR OFFERED"
            }, advice.Rehearsal == LintRehearsal.None ? SemanticRole.ContextNote : SemanticRole.Reasoning);
            Line(advice.Explanation);
            if (advice.Rehearsal == LintRehearsal.None) continue;
            offered = true;
            var scope = advice.AtLine is int line ? $" --at {line}" : "";
            CopyCommand($"{Invocation()} repair {PowerShellLiteral(path)}{scope} --from-lint {revision}" + (plain ? " --plain" : ""));
        }
        Line("");
        if (offered)
        {
            Line("Copy a complete PowerShell command above to open its preview. It will not ask to write your file.", SemanticRole.Reasoning);
            Line("Each link belongs to the bytes checked here. After an edit, run lint again for current links.", SemanticRole.ContextNote);
        }
        Summary(document);
    }

    private void LintSourceMatched() =>
        Line("FROM LINT / source still matches · preview only, no write prompt", SemanticRole.Reasoning);
}
