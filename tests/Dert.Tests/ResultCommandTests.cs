using System.Text;
using Dert.Cli;
using Dert.Cli.Authoring;
using Dert.Cli.Discovery;
using Dert.Cli.Results;
using Dert.Core;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class ResultCommandTests
{
    private const string Source = "\uFEFFSTERT ; update ;; codex\r\n// HOW ; codex ;; Keep uncertainty. //\nƒin.\r\n";

    [Fact]
    public void Real_discovery_retains_attention_and_exact_origin_then_previews_an_inert_note()
    {
        using var file = new ScratchDocument(Source); var record = file.Path + ".json";
        try
        {
            var run = Run(["run", file.Path, "--receipt", record, "--plain"]);
            Assert.Equal(1, run.Exit); Assert.Contains("Result retained", run.Output);
            var observed = ObservationStore.Read(record);
            Assert.Equal(ObservationOutcome.Attention, observed.Outcome);
            Assert.Contains("Keep uncertainty.", observed.Origin.ChainText);
            Assert.Equal(File.ReadAllBytes(file.Path), new UTF8Encoding(false, true).GetBytes(Source));
            var review = Run(["result", record, "--into", file.Path, "--details", "--plain"]);
            Assert.Equal(0, review.Exit); Assert.Contains("RESULT TO NOTE", review.Output);
            Assert.Contains("--result", review.Output); Assert.Contains("observed result", review.Output);
            Assert.Contains("Keep uncertainty.", review.Output);
            Assert.Equal(2, Run(["run", file.Path, "--receipt", record, "--plain"]).Exit);
        }
        finally { if (File.Exists(record)) File.Delete(record); }
    }

    [Theory]
    [InlineData("--preview")]
    [InlineData("--receipt", "second.json")]
    [InlineData("--at", "1")]
    public void Invalid_retention_requests_write_nothing(params string[] extra)
    {
        using var file = new ScratchDocument(Source); var record = file.Path + ".json";
        Assert.Equal(2, Run(["run", file.Path, "--receipt", record, .. extra, "--plain"]).Exit);
        Assert.False(File.Exists(record));
    }

    [Theory]
    [InlineData("STERT ; update ;; codex\ncommands ; updateme ;;\nƒin.")]
    [InlineData("STERT ; where ;; codex\nƒin.")]
    [InlineData("STERT ; update ;; codex\nƒin.\nSTERT ; update ;; codex\nƒin.")]
    public void Retention_does_not_substitute_discovery_for_another_operation(string source)
    {
        using var file = new ScratchDocument(source); var record = file.Path + ".json";
        Assert.Equal(2, Run(["run", file.Path, "--receipt", record, "--plain"]).Exit);
        Assert.False(File.Exists(record));
    }

    [Fact]
    public void MER_keep_retains_the_existing_probe_without_changing_source()
    {
        using var file = new ScratchDocument(Source); var record = file.Path + ".json";
        try
        {
            var run = Run(["mer", file.Path, "--review", "--plain"], $"at 1\nkeep {record}\nprobe\nkeep {record}\n\n");
            Assert.Contains("Use probe", run.Output); Assert.Contains("Result retained", run.Output);
            Assert.Equal(ObservationOutcome.Attention, ObservationStore.Read(record).Outcome);
            Assert.Equal(new UTF8Encoding(false, true).GetBytes(Source), File.ReadAllBytes(file.Path));
        }
        finally { if (File.Exists(record)) File.Delete(record); }
    }

    [Fact]
    public void Native_handoff_carries_literal_paths_and_never_applies_automatically()
    {
        var start = AuthoringCommand.LaunchInfo(["notes [日本語].DERT", "--result", "result's.json", "--at", "3", "--still"], AppContext.BaseDirectory);
        Assert.Contains("--result", start.ArgumentList); Assert.Contains("--at", start.ArgumentList);
        Assert.Contains(Path.GetFullPath("result's.json"), start.ArgumentList);
        Assert.DoesNotContain("--apply", start.ArgumentList); Assert.False(start.UseShellExecute);
    }

    [Fact]
    public void Kept_result_reenters_context_selection_without_becoming_an_operation()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\nƒin.");
        var observation = ObservationStoreTests.Observation(file.Path, File.ReadAllText(file.Path));
        var plan = ResultNotes.Plan(DertParser.Parse(File.ReadAllText(file.Path)), observation, file.Path);
        Assert.True(plan.IsReady);
        var document = DertParser.Parse(plan.ProposedText);
        var note = Assert.Single(document.Nodes.OfType<ReasoningBlockNode>());
        var context = ContextSelection.Capture(document, note.LineNumber, file.Path, ContextSourceKind.EditorBuffer).Build();
        Assert.Contains(observation.Id, context.Report); Assert.Contains("Verification failed", context.Report);
        Assert.Single(Dert.Cli.Operations.DertOperationPlan.Build(document).Steps);
        Assert.Single(context.Included);
    }

    [Fact]
    public void New_only_storage_retains_exact_evidence_and_refuses_invalid_data()
    {
        using var file = new ScratchDocument(Source); var record = file.Path + ".json";
        try
        {
            Run(["run", file.Path, "--receipt", record, "--plain"]);
            var observation = ObservationStore.Read(record);
            Assert.Throws<IOException>(() => ObservationStore.WriteNew(observation, record));
            File.WriteAllText(record, "{\"Format\":1,\"Observation\":null}");
            Assert.Throws<IOException>(() => ObservationStore.Read(record));
            Assert.ThrowsAny<Exception>(() => ObservationStore.WriteNew(observation, file.Path));
        }
        finally { if (File.Exists(record)) File.Delete(record); }
    }

    [Theory]
    [InlineData(58)]
    [InlineData(110)]
    public void Actual_result_frames_identify_proposed_source_before_insertion(int width)
    {
        using var file = new ScratchDocument(Source); var record = file.Path + ".json";
        try
        {
            Run(["run", file.Path, "--receipt", record, "--plain"]);
            var result = ContextCommandTests.Run(["result", record, "--into", file.Path, "--plain"], columns: width);
            Assert.Equal(0, result.Exit); Assert.Contains("Proposed / not saved", result.Output);
            Assert.Contains("RESULT TO NOTE", result.Output); Assert.Contains("source unchanged", result.Output);
        }
        finally { if (File.Exists(record)) File.Delete(record); }
    }

    private static (int Exit, string Output) Run(string[] args, string input = "")
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 110; console.Profile.Height = 30; console.Profile.Capabilities.Interactive = false;
        return (CliApplication.Run(args, console, discoveryEnvironment: new("", ".EXE;.CMD"), merInput: new StringReader(input)), writer.ToString());
    }
}
