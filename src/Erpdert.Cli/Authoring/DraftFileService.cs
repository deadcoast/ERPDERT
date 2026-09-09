using System.Text;
using Erpdert.Cli.Repairs;

namespace Erpdert.Cli.Authoring;

public sealed class DraftFileSnapshot
{
    public string Path { get; }
    public string Text { get; }
    public bool HasBom { get; }
    internal string Identity { get; }
    internal byte[] Original { get; }
    internal DraftFileSnapshot(string path, string text, bool hasBom, string identity, byte[] original)
        => (Path, Text, HasBom, Identity, Original) = (path, text, hasBom, identity, original);
}

public sealed record DraftSaveResult(DraftFileSnapshot Snapshot, string? RecoveryPath);

/// <summary>Explicit editor saves, distinct from source-repair approval and undo receipts.</summary>
public sealed class DraftFileService(IRepairWriter? writer = null)
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly IRepairWriter writer = writer ?? new FileStreamRepairWriter();

    public DraftFileSnapshot Open(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Capture(file, fullPath);
    }

    public DraftFileSnapshot Create(string path, string text, bool hasBom = false)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        LocalPath(fullPath);
        // Write and verify a sibling before giving it the requested name. Move
        // without overwrite prevents even a late-arriving file from being replaced.
        var temporary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fullPath)!, ".erpdert-new-" + Guid.NewGuid().ToString("N"));
        try
        {
            var bytes = Encode(text, hasBom);
            string identity;
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                writer.Rewrite(file, bytes);
                Verify(file, bytes);
                identity = WindowsFileGuard.Identity(file);
            }
            File.Move(temporary, fullPath, false);
            using var saved = OpenForSave(fullPath);
            if (WindowsFileGuard.Identity(saved) != identity)
                throw new RepairException("DERT4002", "The new file was replaced before saving could be verified. Your buffer is retained; inspect that path before trying again.");
            Verify(saved, bytes);
            return new(fullPath, text, hasBom, identity, bytes);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public DraftSaveResult Save(DraftFileSnapshot opened, string text)
    {
        using var target = OpenForSave(opened.Path);
        var original = Read(target);
        if (WindowsFileGuard.Identity(target) != opened.Identity || !original.AsSpan().SequenceEqual(opened.Original))
            throw new RepairException("DERT4002", "This file changed outside the authoring window. Your buffer is retained. Save a new copy, or reopen the current file before replacing anything.");
        var proposed = Encode(text, opened.HasBom);
        if (original.AsSpan().SequenceEqual(proposed)) return new(opened, null);
        var folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(opened.Path)!, ".erpdert", "drafts");
        CheckSavePath(folder);
        Directory.CreateDirectory(folder);
        CheckSavePath(folder);
        var recovery = System.IO.Path.Combine(folder, Guid.NewGuid().ToString("N") + ".DERT");
        using var backup = new FileStream(recovery, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        new FileStreamRepairWriter().Rewrite(backup, original);
        Verify(backup, original);
        try
        {
            writer.Rewrite(target, proposed);
            Verify(target, proposed);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { writer.Rewrite(target, original); Verify(target, original); }
            catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
            { throw new RepairException("DERT4103", "Saving and restoring the previous bytes could not finish. Your buffer is retained; recover the file from the saved original.", recovery, rollback); }
            throw new RepairException("DERT4102", "Saving failed. The previous bytes were restored and verified. Your edited buffer is still here.", recovery, error);
        }
        return new(new(opened.Path, text, opened.HasBom, opened.Identity, proposed), recovery);
    }

    private static void LocalPath(string path)
    {
        if (!OperatingSystem.IsWindows() || path.StartsWith(@"\\", StringComparison.Ordinal) || path[(System.IO.Path.GetPathRoot(path)?.Length ?? 0)..].Contains(':'))
            throw new RepairException("DERT4101", "Choose a regular local Windows file for this authoring save.");
        CheckSavePath(path);
    }

    private static void CheckSavePath(string path)
    {
        try { WindowsFileGuard.CheckPath(path); }
        catch (RepairException error) { throw SavePathError(error); }
    }

    private static FileStream OpenForSave(string path)
    {
        try { return WindowsFileGuard.OpenExclusive(path); }
        catch (RepairException error) { throw SavePathError(error); }
        catch (IOException error)
        { throw new IOException("This file could not be opened exclusively for saving. Check its read-only status and other programs using it. Your buffer is retained.", error); }
    }

    private static RepairException SavePathError(RepairException error) => new("DERT4101",
        "This save needs a regular local Windows file without links or redirected folders. Use Save as to choose a new local copy. Your buffer is retained.", inner: error);

    private static DraftFileSnapshot Capture(FileStream file, string path)
    {
        var bytes = Read(file);
        var hasBom = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf });
        var text = Utf8.GetString(bytes.AsSpan(hasBom ? 3 : 0));
        return new(path, text, hasBom, WindowsFileGuard.Identity(file), bytes);
    }

    private static byte[] Encode(string text, bool bom) => bom ? [0xef, 0xbb, 0xbf, .. Utf8.GetBytes(text)] : Utf8.GetBytes(text);
    private static byte[] Read(FileStream file) { file.Position = 0; using var copy = new MemoryStream(); file.CopyTo(copy); return copy.ToArray(); }
    private static void Verify(FileStream file, byte[] expected)
    {
        if (!Read(file).AsSpan().SequenceEqual(expected)) throw new IOException("The written bytes did not match the buffer.");
    }
}
