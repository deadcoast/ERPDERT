using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dert.Cli.Repairs;
using Dert.Core;

namespace Dert.Cli.Results;

/// <summary>Local observation persistence. This is not a portable interchange contract.</summary>
public static class ObservationStore
{
    private const int MaximumBytes = 8 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private sealed record Stored(int Format, ResultObservation Observation);

    public static ResultObservation Read(string path)
    {
        using var input = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaximumBytes) throw new IOException("This local result exceeds the supported 8 MiB review size. The file is unchanged.");
        using var content = new MemoryStream(); input.CopyTo(content);
        try
        {
            var text = Utf8.GetString(content.ToArray());
            using var document = JsonDocument.Parse(text);
            CheckNames(document.RootElement);
            var saved = JsonSerializer.Deserialize<Stored>(text, Json);
            if (saved is not { Format: 1, Observation: not null }) throw new ArgumentException("Unrecognized local result record.");
            ResultNotes.Validate(saved.Observation);
            return saved.Observation;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or OverflowException)
        { throw new IOException("This file is not a complete supported local result record. Its text was retained; no result was inserted.", error); }
    }

    public static string WriteNew(ResultObservation observation, string path) => WriteNew(observation, path, new FileStreamRepairWriter());

    internal static string WriteNew(ResultObservation observation, string path, IRepairWriter writer)
    {
        ResultNotes.Validate(observation);
        var fullPath = Destination(path, observation.Origin.Path);
        // Validate all strings before JSON serialization can replace invalid Unicode.
        _ = Utf8.GetBytes(ResultNotes.Describe(observation));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Stored(1, observation), Json);
        if (bytes.Length > MaximumBytes) throw new IOException("The result exceeds the supported 8 MiB local record size. No file was created.");
        var created = false;
        try
        {
            using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            created = true; writer.Rewrite(output, bytes); output.Position = 0;
            using var actual = new MemoryStream(); output.CopyTo(actual);
            if (!actual.ToArray().AsSpan().SequenceEqual(bytes)) throw new IOException("Result bytes could not be verified.");
            return fullPath;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new IOException(created
                ? "The result file could not be completed and verified. It may contain incomplete data; inspect it before choosing a new path. Source is unchanged."
                : "The result could not be retained at that path. Choose a new .json file in an existing writable folder; existing files are never overwritten.", error);
        }
    }

    internal static string Destination(string path, string sourcePath)
    {
        var fullPath = Path.GetFullPath(path);
        if (fullPath.Equals(Path.GetFullPath(sourcePath), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Choose a separate result file. The source path cannot be used for a result record.");
        if (OperatingSystem.IsWindows() && fullPath[(Path.GetPathRoot(fullPath)?.Length ?? 0)..].Contains(':'))
            throw new IOException("Choose a separate result file; alternate file streams are not supported.");
        if (!Path.GetExtension(fullPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a new .json file for this local result record.");
        if (File.Exists(fullPath) || Directory.Exists(fullPath)) throw new IOException("That result path already exists. Choose a new .json file; nothing is overwritten.");
        return fullPath;
    }

    private static void CheckNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Repeated field in local result.");
                CheckNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) CheckNames(item);
    }
}
