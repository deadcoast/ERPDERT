using Erpdert.Cli;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class MerDemoTests
{
    [Fact]
    public void The_source_lens_follows_selection_and_keeps_the_original_punctuation_through_a_mode_change()
    {
        var (exit, output) = Invoke(["demo", "--case", "context", "--review", "--plain"], "\nat 5\nsource\nnext\nplain\nsource\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("Recorded source / line 5", output);
        Assert.Contains("// music, // Bring the playlist", output);
        Assert.Equal(2, output.Split("Recorded source / line 6", StringSplitOptions.None).Length - 1);
        Assert.Contains("// food, // Book the corner table", output);
        Assert.Contains("booking number", output);
        Assert.Contains("Belongs to plans", output[(output.LastIndexOf("Recorded source / line 6", StringComparison.Ordinal))..]);
        Assert.Equal(1, output.Split("context / not reviewed /", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void A_case_without_entries_explains_the_source_control_without_losing_its_error()
    {
        var (exit, output) = Invoke(["demo", "--case", "failure", "--review", "--plain"], "\nsource\nnext\ndetails\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("no recorded entries", output);
        Assert.Contains("not valid UTF-8", output);
        Assert.DoesNotContain("Recorded source / line", output);
    }

    [Fact]
    public void Neighbor_navigation_keeps_one_record_and_stops_at_the_end_instead_of_wrapping()
    {
        var (exit, output) = Invoke(["demo", "--case", "context", "--review", "--plain"], "\nprev\nprev\nprev\nnext\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("Inspecting recorded line 6", output);
        Assert.Contains("Inspecting recorded line 5", output);
        Assert.Contains("Already at the first recorded entry", output);
        Assert.Equal(2, output.Split("Inspecting recorded line 6", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, output.Split("context / not reviewed /", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Presentation_comparisons_keep_the_selected_entry_and_do_not_restart_the_case()
    {
        var (exit, output) = Invoke(["demo", "--case", "context", "--review", "--plain"], "\nat 5\nstill\nplain\nq\n");
        Assert.Equal(0, exit);
        Assert.Equal(3, output.Split("Inspecting recorded line 5", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, output.Split("Look for /", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, output.Split("context / not reviewed /", StringSplitOptions.None).Length - 1);
        Assert.Contains("compared:", output);
    }

    [Fact]
    public void Replaying_is_a_presentation_of_the_same_record_not_another_analysis_or_review()
    {
        var (exit, output) = Invoke(["demo", "--case", "context", "--review", "--plain"], "\nat 5\nrepeat\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("Replaying the same record", output);
        Assert.Equal(1, output.Split("Look for /", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, output.Split("context / not reviewed /", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, output.Split("Inspecting recorded line 5", StringSplitOptions.None).Length - 1);
        var replayed = output[output.IndexOf("Replaying the same record", StringComparison.Ordinal)..];
        Assert.Equal(1, replayed.Split("Context that belongs together", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void A_recorded_entry_can_be_inspected_directly_without_replaying_or_adding_a_review()
    {
        var (exit, output) = Invoke(["demo", "--case", "context", "--review", "--plain"], "\nat 5\nat 6\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("at 5", output);
        Assert.Contains("Inspecting recorded line 5", output);
        Assert.Contains("Bring the playlist everyone keeps asking for.", output);
        Assert.Contains("Inspecting recorded line 6", output);
        Assert.Contains("Belongs to plans", output);
        Assert.Equal(1, output.Split("context / not reviewed /", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, output.Split("Look for /", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("Read the source / checked", output);
    }

    [Theory]
    [InlineData("at 999")]
    [InlineData("at -1")]
    [InlineData("at banana")]
    public void An_unavailable_entry_leaves_the_record_and_review_untouched(string choice)
    {
        var (exit, output) = Invoke(["demo", "--case", "context", "--review", "--plain"], "\n" + choice + "\nat 5\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("No recorded connection at that line", output);
        Assert.Contains("Inspecting recorded line 5", output);
        Assert.Equal(1, output.Split("context / not reviewed /", StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData("title", "details")]
    [InlineData("context", "at 5")]
    public void Opening_recorded_detail_or_an_entry_retains_the_time_of_the_record(string selected, string command)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        var exit = CliApplication.Run(["demo", "--case", selected, "--review", "--plain"], console, demoInput: new PausedReview(command));
        Assert.Equal(0, exit);
        var times = System.Text.RegularExpressions.Regex.Matches(writer.ToString(), @"\b\d{2}:\d{2}:\d{2}\b").Select(match => match.Value).ToArray();
        Assert.True(times.Length >= 2);
        Assert.Single(times.Distinct());
    }

    private sealed class PausedReview(string command) : TextReader
    {
        private int reads;
        public override string? ReadLine()
        {
            reads++;
            if (reads == 1) return "";
            if (reads == 2) { Thread.Sleep(1100); return command; }
            return "q";
        }
    }

    [Fact]
    public void A_review_holds_the_title_comparison_instead_of_burying_it_under_the_audit()
    {
        var (exit, output) = Invoke(["demo", "--case", "title", "--review", "--plain"], "\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("Measured against your title", output);
        Assert.Contains("// MY FIRST TITLE", output);
        Assert.Contains("+ // --------------", output);
        Assert.Contains("Preview only / not applied", output);
        Assert.DoesNotContain("Read the source / checked", output);
    }

    [Fact]
    public void The_held_context_view_names_the_owner_and_its_actual_closing_line()
    {
        var (exit, output) = Invoke(["demo", "--case", "context", "--review", "--plain"], "\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("Belongs to plans", output);
        Assert.Contains("Last train at 23:40", output);
        Assert.Contains("Closing // at line 8", output);
        Assert.DoesNotContain("Read the source / checked", output);
    }

    [Fact]
    public void Details_opens_the_complete_record_without_replaying_the_case()
    {
        var (exit, output) = Invoke(["demo", "--case", "title", "--review", "--plain"], "\ndetails\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("Full recorded detail", output);
        Assert.Contains("Read the source / checked", output);
        Assert.Contains("Keep my words exactly as I wrote them.", output);
        Assert.Equal(1, output.Split("title / not reviewed /", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Encoding_failure_explains_the_problem_before_the_decoder_details()
    {
        var (exit, output) = Invoke(["demo", "--case", "failure", "--plain"]);
        Assert.Equal(0, exit);
        var explanation = output.IndexOf("This source is not valid UTF-8 text.", StringComparison.Ordinal);
        var technical = output.IndexOf("Decoder detail /", StringComparison.Ordinal);
        Assert.True(explanation >= 0 && technical > explanation);
        Assert.Contains("No later check ran", output);
        Assert.Contains("keep the original", output);
    }

    [Fact]
    public void Explicit_step_mode_accepts_line_input_without_a_second_review_flag()
    {
        var (exit, output) = Invoke(["demo", "--case", "title", "--step", "--plain"], "\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("Paused replay", output);
        Assert.Contains("Enter starts Title alignment", output);
        Assert.Contains("view: partial", output);
    }

    [Fact]
    public void Quitting_mid_step_records_a_partial_view_not_an_unseen_case()
    {
        var (exit, output) = Invoke(["demo", "--case", "title", "--step", "--review", "--plain"], "\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("title / not reviewed", output);
        Assert.Contains("view: partial", output);
        Assert.DoesNotContain("title / not reviewed / not shown", output);
        Assert.Contains("Replay stopped", output);
    }

    [Fact]
    public void A_stepped_review_can_go_back_then_record_the_persons_own_judgment()
    {
        var (exit, output) = Invoke(["demo", "--case", "title", "--step", "--review", "--plain"], "\n\nback\nnext\nchange Too much text\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("Paused replay", output);
        Assert.Contains("title / needs work", output);
        Assert.Contains("Too much text", output);
        Assert.DoesNotContain("title / looks good", output);
        Assert.Contains("context / not reviewed", output);
    }

    [Fact]
    public void Review_choices_do_not_mark_skipped_cases_as_approved()
    {
        var (exit, output) = Invoke(["demo", "--review", "--plain"], "\nkeep\nq\n");
        Assert.Equal(0, exit);
        Assert.Contains("title / looks good", output);
        Assert.Contains("context / not reviewed", output);
        Assert.Contains("failure / not reviewed / not shown", output);
    }
    [Fact]
    public void The_demo_runs_its_cases_offline_without_input_or_certifying_the_design()
    {
        var (exit, output) = Invoke(["demo", "--all", "--plain"]);
        Assert.Equal(0, exit);
        Assert.Contains("Title alignment", output);
        Assert.Contains("Context that belongs together", output);
        Assert.Contains("Unfinished is not broken", output);
        Assert.Contains("Words beyond ASCII", output);
        Assert.Contains("When a check cannot finish", output);
        Assert.Contains("REPLAY", output);
        Assert.Contains("not reviewed", output);
        Assert.Contains("No files", output);
        Assert.DoesNotContain('\u001b', output);
        Assert.DoesNotContain("MANUAL UPDATE", output);
    }

    [Theory]
    [InlineData("title", "+ // --------------")]
    [InlineData("context", "belongs to line 4")]
    [InlineData("unfinished", "missing")]
    [InlineData("unicode", "日本語")]
    [InlineData("failure", "Read the source / failed")]
    public void A_demo_case_uses_real_analysis_and_explains_what_to_judge(string name, string expected)
    {
        var (exit, output) = Invoke(["demo", "--case", name, "--plain"]);
        Assert.Equal(0, exit);
        Assert.Contains(expected, output);
        Assert.Contains("Look for", output);
        Assert.Contains("Your review", output);
    }

    [Theory]
    [InlineData("--case", "missing-case")]
    [InlineData("--speed", "banana")]
    public void Invalid_demo_options_explain_the_available_cases(string first, string second)
    {
        var (exit, output) = Invoke(["demo", first, second, "--plain"]);
        Assert.Equal(2, exit);
        Assert.Contains("--case", output);
        Assert.Contains("title", output);
        Assert.DoesNotContain("Your review", output);
    }

    private static (int Exit, string Output) Invoke(string[] args, string input = "")
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        return (CliApplication.Run(args, console, demoInput: new StringReader(input)), writer.ToString());
    }
}
