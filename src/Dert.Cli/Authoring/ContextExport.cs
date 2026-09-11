using Dert.Core;
using System.Text;
using Dert.Cli.Repairs;

namespace Dert.Cli.Authoring;

public static class ContextExport
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static string WriteNew(ContextPacket packet, string path) => WriteNew(packet, path, new FileStreamRepairWriter());

    internal static string WriteNew(ContextPacket packet, string path, IRepairWriter writer)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var fullPath = Path.GetFullPath(path);
        if (SameSource(packet.Selection, fullPath))
            throw new IOException("Choose a different path for the context export. The captured source path cannot be used as an export destination.");
        if (OperatingSystem.IsWindows() && fullPath[(Path.GetPathRoot(fullPath)?.Length ?? 0)..].Contains(':'))
            throw new IOException("Choose a separate .txt file for context. An alternate file stream would write inside another file.");
        if (!Path.GetExtension(fullPath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a new .txt file for the reviewed context.");
        var bytes = Utf8.GetBytes(packet.Report);
        var created = false;
        try
        {
            // CreateNew also refuses a destination that appeared after review.
            // Retain one exclusive handle through the write and byte comparison.
            using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            created = true;
            writer.Rewrite(output, bytes);
            output.Position = 0;
            using var actual = new MemoryStream();
            output.CopyTo(actual);
            if (!actual.GetBuffer().AsSpan(0, checked((int)actual.Length)).SequenceEqual(bytes))
                throw new IOException("The destination bytes did not match the reviewed context.");
            return fullPath;
        }
        catch (IOException error)
        {
            throw new IOException(created ?
                "The context export could not be completed and verified. The destination may contain incomplete text; inspect it before trying a new path. Your source was not saved or changed." :
                "The context export could not create a new file. Choose an unused .txt path in an existing writable folder. Existing files are never overwritten.", error);
        }
        catch (UnauthorizedAccessException error)
        {
            throw new UnauthorizedAccessException(created ?
                "The context export lost access before it could be verified. The destination may contain incomplete text; inspect it before trying a new path. Your source was not saved or changed." :
                "The context export could not create a new file here. Choose an unused .txt path in a writable folder. Existing files are never overwritten.", error);
        }
    }

    private static bool SameSource(ContextSelection selection, string destination)
    {
        // Saved editor buffers also identify their source by path; an unsaved
        // buffer label cannot accidentally authorize replacing a source file.
        try { return Path.GetFullPath(selection.SourceLabel).Equals(destination, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
}
