using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dert.Cli.Authoring;

public sealed record DesktopSettings(bool WindowsMenuFade = true, bool WordWrap = true,
    bool ShowLineNumbers = true, bool ReducedMotion = false, double FontSize = 14);

public sealed class DesktopSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public DesktopSettingsStore(string? path = null) => Path = System.IO.Path.GetFullPath(path ??
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DERT", "settings.json"));

    public string Path { get; }

    public DesktopSettings Read()
    {
        try
        {
            RefuseLink();
            using var file = File.OpenRead(Path);
            using var json = JsonDocument.Parse(file);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Expected a settings object.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in json.RootElement.EnumerateObject())
                if (!names.Add(property.Name)) throw new JsonException($"The setting '{property.Name}' appears more than once.");
            var settings = json.RootElement.Deserialize<DesktopSettings>(JsonOptions)!;
            Validate(settings);
            return settings;
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        { return new(); }
        catch (Exception error) when (error is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            throw new IOException($"The settings file '{Path}' could not be read. Check its contents and access before saving settings again. The file was not changed. {error.Message}", error);
        }
    }

    public void Save(DesktopSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        // Refuse to replace malformed or newer settings with guessed defaults.
        Read();
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(file, settings, JsonOptions);
                file.Flush(flushToDisk: true);
            }
            Read();
            if (File.Exists(Path)) File.Replace(temporary, Path, null);
            else File.Move(temporary, Path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"The settings file '{Path}' could not be saved. Check folder access and close programs holding the file, then try again. Existing settings were retained. {error.Message}", error);
        }
        finally
        {
            if (temporary is not null)
                try { File.Delete(temporary); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    private void RefuseLink()
    {
        if ((File.GetAttributes(Path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Choose a regular settings file; this path is a link.");
    }

    private static void Validate(DesktopSettings settings)
    {
        if (!double.IsFinite(settings.FontSize) || settings.FontSize is < 8 or > 48)
            throw new ArgumentException("font-size must be a number from 8 to 48, such as 14 or 17.5.");
    }
}

internal static class DesktopSettingsCommand
{
    internal static int Run(string[] args, DocumentRenderer renderer, string? path)
    {
        try
        {
            var values = args.Skip(1).Where(value => value is not ("--plain" or "--still")).ToArray();
            if (values.Length is not (0 or 2))
                throw new ArgumentException("Use DERT settings to read preferences, or DERT settings <name> <value> to change one.");
            var store = new DesktopSettingsStore(path);
            var settings = store.Read();
            if (values.Length == 2)
            {
                settings = values[0].ToLowerInvariant() switch
                {
                    "windows-menu-fade" => settings with { WindowsMenuFade = Boolean(values[1]) },
                    "word-wrap" => settings with { WordWrap = Boolean(values[1]) },
                    "show-line-numbers" => settings with { ShowLineNumbers = Boolean(values[1]) },
                    "reduced-motion" => settings with { ReducedMotion = Boolean(values[1]) },
                    "font-size" when double.TryParse(values[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var size)
                        => settings with { FontSize = size },
                    "font-size" => throw new ArgumentException("font-size must be a number from 8 to 48, such as 14 or 17.5."),
                    _ => throw new ArgumentException("Choose windows-menu-fade, word-wrap, show-line-numbers, reduced-motion or font-size.")
                };
                store.Save(settings);
            }
            renderer.DesktopPreferences(settings, store.Path, values.Length == 2);
            return 0;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        { renderer.Failure(error.Message); return 2; }
    }

    private static bool Boolean(string value) => value.ToLowerInvariant() switch
    {
        "on" => true,
        "off" => false,
        _ => throw new ArgumentException("Choose on or off for this setting.")
    };
}
