using Dert.Cli.Authoring;
using Dert.Cli.Results;
using Dert.Core;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Dert.Tests;

public sealed class DesktopSourceBindingTests
{
    [Theory]
    [InlineData("write")]
    [InlineData("guide")]
    public void Authoring_options_before_the_filename_keep_the_same_binding(string verb)
    {
        var path = Path.GetFullPath("My note.DERT");
        Assert.True(DesktopSourceBinding.UsesDocument([verb, "--still", path], path));
        Assert.True(DesktopSourceBinding.UsesDocument([verb, "--result", "record.json", path], path));
        Assert.False(DesktopSourceBinding.UsesDocument([verb, "--still"], path));
    }

    [Fact]
    public void A_result_receipt_uses_its_recorded_destination_unless_overridden()
    {
        var path = Path.GetFullPath("My note.DERT");
        var record = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        const string source = "STERT ; update ;; codex\nƒin.";
        var observation = new ResultObservation("desktop-binding", DateTimeOffset.Now, "DERT update codex / discovery", "codex",
            ObservationOutcome.Attention, "No confirmed installation.", "Read local PATH. No programs started.",
            new(path, 1, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(), source),
            [new("discovery", "No route found.")]);
        try
        {
            ObservationStore.WriteNew(observation, record);
            Assert.True(DesktopSourceBinding.UsesDocument(["result", record, "--write"], path));
            Assert.False(DesktopSourceBinding.UsesDocument(["result", record, "--into", "another.DERT", "--write"], path));
        }
        finally { File.Delete(record); }
    }
    [Theory]
    [InlineData("run")]
    [InlineData("repair")]
    [InlineData("undo")]
    [InlineData("lint")]
    [InlineData("inspect")]
    [InlineData("context")]
    public void File_commands_bind_the_document_using_Windows_path_identity(string verb)
    {
        var path = Path.GetFullPath("My note.DERT");
        Assert.True(DesktopSourceBinding.UsesDocument([verb, path.ToUpperInvariant()], path));
        Assert.False(DesktopSourceBinding.UsesDocument([verb, "another.DERT"], path));
    }

    [Theory]
    [InlineData("where", "--from-source")]
    [InlineData("update", "--from-source")]
    [InlineData("result", "--into")]
    public void Saved_handoffs_bind_the_named_source_even_after_other_arguments(string verb, string option)
    {
        var path = Path.GetFullPath("My note.DERT");
        Assert.True(DesktopSourceBinding.UsesDocument([verb, "codex", "--apply", "old-token", option, path.ToUpperInvariant()], path));
    }

    [Fact]
    public void Discovery_and_literal_non_source_arguments_do_not_bind_the_editor()
    {
        var path = Path.GetFullPath("My note.DERT");
        Assert.False(DesktopSourceBinding.UsesDocument(["where", "codex"], path));
        Assert.False(DesktopSourceBinding.UsesDocument(["where", "codex", "--path", path], path));
        Assert.False(DesktopSourceBinding.UsesDocument(["update", "codex", "--from-source"], path));
    }
}
