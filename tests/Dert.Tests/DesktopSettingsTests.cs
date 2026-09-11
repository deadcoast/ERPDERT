using Dert.Cli;
using Dert.Cli.Authoring;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public sealed class DesktopSettingsTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dert-settings-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => System.IO.Path.Combine(directory, "settings.json");

    [Fact]
    public void Missing_settings_are_readable_without_creating_a_file()
    {
        Assert.Equal(new DesktopSettings(), new DesktopSettingsStore(SettingsPath).Read());
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Native_save_and_cli_read_share_all_values_and_cli_changes_preserve_other_preferences()
    {
        var store = new DesktopSettingsStore(SettingsPath);
        store.Save(new(false, false, false, true, 17.5));
        var shown = Run("settings");
        Assert.Equal(0, shown.Exit);
        Assert.Contains(SettingsPath, shown.Output);
        Assert.Contains("windows-menu-fade: off", shown.Output);
        Assert.Contains("word-wrap: off", shown.Output);
        Assert.Contains("show-line-numbers: off", shown.Output);
        Assert.Contains("reduced-motion: on", shown.Output);
        Assert.Contains("font-size: 17.5", shown.Output);
        Assert.Equal(0, Run("settings", "windows-menu-fade", "on").Exit);
        Assert.Equal(new(true, false, false, true, 17.5), store.Read());
    }

    [Theory]
    [InlineData("word-wrap", "off")]
    [InlineData("show-line-numbers", "off")]
    [InlineData("reduced-motion", "on")]
    [InlineData("font-size", "21.5")]
    public void Every_desktop_setting_can_be_changed_from_the_cli(string name, string value)
    {
        var result = Run("settings", name, value, "--plain");
        Assert.Equal(0, result.Exit);
        Assert.Contains(name + ": " + value, result.Output);
        Assert.Contains(name + ": " + value, Run("settings").Output);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"windowsMenuFade\":\"off\"}")]
    [InlineData("{\"fontSize\":999}")]
    [InlineData("{\"futureSetting\":true}")]
    [InlineData("{\"wordWrap\":true,\"wordWrap\":false}")]
    public void Invalid_or_unrecognized_settings_are_explained_and_never_overwritten(string original)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, original);
        var error = Assert.Throws<IOException>(() => new DesktopSettingsStore(SettingsPath).Read());
        Assert.Contains(SettingsPath, error.Message);
        Assert.Contains("settings", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<IOException>(() => new DesktopSettingsStore(SettingsPath).Save(new()));
        Assert.Equal(2, Run("settings", "word-wrap", "off").Exit);
        Assert.Equal(original, File.ReadAllText(SettingsPath));
        Assert.Single(Directory.EnumerateFileSystemEntries(directory));
    }

    [Theory]
    [InlineData("windows-menu-fade", "yes")]
    [InlineData("word-wrap", "")]
    [InlineData("font-size", "NaN")]
    [InlineData("font-size", "7")]
    [InlineData("font-size", "49")]
    [InlineData("font-size", "12,5")]
    [InlineData("made-up", "on")]
    public void Invalid_cli_values_leave_the_previous_file_exactly_intact(string name, string value)
    {
        new DesktopSettingsStore(SettingsPath).Save(new());
        var original = File.ReadAllBytes(SettingsPath);
        Assert.Equal(2, Run("settings", name, value).Exit);
        Assert.Equal(original, File.ReadAllBytes(SettingsPath));
    }

    [Fact]
    public void An_unreplaceable_file_retains_its_previous_settings_and_leaves_no_temporary_file()
    {
        var store = new DesktopSettingsStore(SettingsPath);
        store.Save(new());
        var original = File.ReadAllBytes(SettingsPath);
        using (var held = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => store.Save(new(WordWrap: false)));
        Assert.Equal(original, File.ReadAllBytes(SettingsPath));
        Assert.Single(Directory.EnumerateFileSystemEntries(directory));
    }

    [Fact]
    public void Sparse_settings_keep_defaults_for_preferences_not_yet_saved()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, "{\"windowsMenuFade\":false}");
        Assert.Equal(new(WindowsMenuFade: false), new DesktopSettingsStore(SettingsPath).Read());
    }

    private (int Exit, string Output) Run(params string[] args)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 300;
        return (CliApplication.Run(args, console, desktopSettingsPath: SettingsPath), writer.ToString());
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
