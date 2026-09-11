using Dert.Cli;
using Dert.Cli.Discovery;
using Dert.Cli.Doctoring;
using Dert.Cli.Motion;
using Dert.Cli.Operations;
using Dert.Mer;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class DoctorDeliveryTests
{
    private static DoctorReport Confirmed() => new(new([], [], 2))
    {
        State = DoctorState.ChooseCopy,
        Diagnosis = new(DoctorDiagnosisState.Confirmed, "A broken first route hides another identified copy.",
            "C:\\earlier  copy\\codex.ps1", "C:\\earlier  copy\\node_modules\\@openai\\codex\\bin\\codex.js",
            "C:\\later copy\\codex.ps1", [])
    };

    [Theory]
    [InlineData(32)]
    [InlineData(58)]
    [InlineData(110)]
    public void Diagnosis_keeps_all_three_facts_and_scope_at_small_and_large_widths(int columns)
    {
        var lines = new DoctorConnection(Confirmed()).Receipt(columns);
        Assert.All(lines, line => Assert.True(TerminalText.Width(line.Text) < columns, line.Text));
        var output = string.Join('\n', lines.Select(line => line.Text));
        Assert.Contains("First external route", output);
        Assert.Contains("Missing entry point", output);
        Assert.Contains("Later identified copy", output);
        Assert.Contains("external PATH", output);
        Assert.Contains("codex.ps1", output);
        Assert.Contains("codex.js", output);
        Assert.Contains("earlier  copy", output);
    }

    [Fact]
    public void Compact_mer_diagnosis_retains_confirmed_scope_and_a_pending_choice()
    {
        var annotation = new DoctorConnection(Confirmed()).Annotation(8, "Codex");
        Assert.Equal(8, annotation.SourceLine);
        var text = string.Join('\n', annotation.CompactLines!.Select(line => line.Text));
        Assert.Contains("Conflict confirmed / external PATH", text);
        Assert.Contains("Choose a copy", text);
        Assert.Contains("No package changes", text);
        Assert.Equal(StageState.Attention, annotation.Action!.State);
    }

    [Fact]
    public void Compact_mer_retains_interruption_alongside_the_observed_conflict()
    {
        var report = Confirmed();
        report.State = DoctorState.Interrupted;
        var annotation = new DoctorConnection(report).Annotation(8, "Codex");
        Assert.Equal(StageState.Interrupted, annotation.Action!.State);
        Assert.Contains(annotation.CompactLines!, line => line.Text == "Doctor stopped / interrupted");
        Assert.Contains(annotation.CompactLines!, line => line.Text == "Conflict confirmed / external PATH");
        Assert.DoesNotContain(annotation.CompactLines!, line => line.Text.Contains("awaiting choice", StringComparison.Ordinal));
    }

    [Fact]
    public void Long_paths_do_not_displace_the_choice_and_details_hint_from_the_wide_source_window()
    {
        var report = Confirmed();
        var root = "C:\\" + new string('x', 160);
        report.Diagnosis = report.Diagnosis with { LeadingPath = root + "\\first folder\\codex.ps1", LaterPath = root + "\\later folder\\codex.ps1" };
        var content = new[] { InkLine.Plain("STERT ; where ;; codex"), InkLine.Plain("commands ; doctor ;;"), InkLine.Plain("ƒin.") };
        var source = new MotionSource("my notes.DERT", content, [1, 2, 3]) { ContentLines = content };
        var journal = new MotionJournal(new("Notes", "Original source", [new("read", "Read", "Read source")], source), DateTimeOffset.UnixEpoch);
        var frame = MerCompositor.Document(journal.Snapshot(TimeSpan.Zero), 110, 28, 2, new DoctorConnection(report).Annotation(2, "Codex"));
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Contains("commands ; doctor ;;", text);
        Assert.Contains("details: exact paths", text);
        Assert.Contains("No package changes", text);
        Assert.Contains("first folder", text);
        Assert.Contains("later folder", text);
        Assert.All(frame.Lines, line => Assert.True(TerminalText.Width(line.Text) < 110));
    }

    [Fact]
    public void Confirmed_diagnosis_offers_a_source_bound_rehearsal_without_an_apply_token()
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 160;
        new DocumentRenderer(console, plain: true).Doctor(Confirmed(), "codex", "C:\\author's notes\\reason.DERT");
        var text = writer.ToString();
        Assert.Contains("FIXME / review the broken first copy", text);
        Assert.Contains("--path 'C:\\earlier  copy\\codex.ps1'", text);
        Assert.Contains("--from-source 'C:\\author''s notes\\reason.DERT'", text);
        Assert.DoesNotContain("--apply", text);
        Assert.Contains("No uninstall or reinstall has run", text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_successful_reinstall_needs_the_recorded_route_phase_to_claim_conflict_resolution(bool routeVerified)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 160;
        var phases = new List<DoctorPhase> { new("verified", true, "Package verified") };
        if (routeVerified) phases.Add(new("external-route-condition-verified", true, "External condition verified"));
        new DocumentRenderer(console, plain: true).DoctorResult(new(DoctorApplyState.Succeeded, "Reinstall verified", "receipt.jsonl", null, phases));
        Assert.Equal(routeVerified, writer.ToString().Contains("EXTERNAL ROUTE / REPAIR VERIFIED", StringComparison.Ordinal));
    }

    [Fact]
    public void A_proposal_for_the_later_copy_keeps_the_first_route_unresolved_and_shows_version_change()
    {
        var report = Confirmed();
        report.State = DoctorState.Ready;
        var request = new CommandRequest("C:\\later copy\\node.exe", ["npm-cli.js", "install", "@openai/codex@2.0.0"], "C:\\later copy", TimeSpan.FromMinutes(5));
        report.Proposal = new("C:\\later copy\\codex.ps1", "C:\\later copy", "2.0.0", request, request, [], null)
        { InstalledVersion = "1.0.0", Token = "reviewed" };
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 160;
        new DocumentRenderer(console, plain: true).Doctor(report);
        Assert.Contains("Its reinstall does not repair the diagnosed conflict", writer.ToString());
        Assert.Contains("1.0.0 -> 2.0.0", writer.ToString());
        Assert.Contains("FIXME / review the broken first copy", writer.ToString());
        Assert.DoesNotContain("& 'C:\\later copy\\node.exe'", writer.ToString());
    }
}
