using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dert.Core;

namespace Erpdert.Cli.Repairs;

public sealed class RepairFileService
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly IRepairWriter writer;
    public RepairFileService(IRepairWriter? writer = null) => this.writer = writer ?? new FileStreamRepairWriter();

    public NoteRepairInspection InspectNote(string path, int atLine, string? expectedRevision = null) => Guard(() =>
    {
        var fullPath = Path.GetFullPath(path);
        using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bytes = Read(file);
        CheckLintRevision(expectedRevision, bytes);
        var identity = WindowsFileGuard.Identity(file);
        var document = DertParser.Parse(Utf8.GetString(bytes));
        var analysis = SelectNote(() => NoteClosureRepair.Analyze(document, atLine));
        return new NoteRepairInspection(fullPath, Hash(bytes), identity, analysis);
    });

    public RepairSnapshot PreviewNote(NoteRepairInspection inspection, string boundaryKey)
    {
        var plan = SelectNote(() => NoteClosureRepair.Plan(inspection.Analysis, boundaryKey));
        return new(inspection.Path, plan.Source, plan.ProposedText, inspection.Revision, plan, inspection.Identity);
    }

    public RepairSnapshot Preview(string path, string? expectedRevision = null) => Guard(() =>
    {
        var fullPath = Path.GetFullPath(path);
        using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bytes = Read(file);
        CheckLintRevision(expectedRevision, bytes);
        var identity = WindowsFileGuard.Identity(file);
        var text = Utf8.GetString(bytes);
        var plan = TitleRuleRepair.Plan(DertParser.Parse(text));
        return new RepairSnapshot(fullPath, text, plan.ProposedText, Hash(bytes), plan, identity);
    });

    public RepairSnapshot PreviewUndo(string path) => Guard(() =>
    {
        var fullPath = Path.GetFullPath(path);
        using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var current = Read(file);
        var identity = WindowsFileGuard.Identity(file);
        var directory = LatestReceipt(fullPath, identity);
        if (directory is null)
            throw new RepairException("DERT4003", "There is no saved source repair for this document. Nothing was changed.");
        WindowsFileGuard.CheckPath(directory);
        var backupPath = Path.Combine(directory, "before.DERT");
        var receiptPath = Path.Combine(directory, "receipt.json");
        WindowsFileGuard.CheckPath(backupPath);
        WindowsFileGuard.CheckPath(receiptPath);
        var receipt = JsonSerializer.Deserialize<RepairReceipt>(File.ReadAllBytes(receiptPath));
        var original = File.ReadAllBytes(backupPath);
        var originalText = Utf8.GetString(original);
        if (receipt is null || receipt.FileIdentity != identity || receipt.BeforeHash != Hash(original))
            throw new RepairException("DERT4004", "This recovery record does not match this file and its saved original. The document was left unchanged.", backupPath);
        var plan = Replay(receipt, originalText, backupPath);
        if (!plan.IsReady ||
            receipt.AfterHash != Hash(Utf8.GetBytes(plan.ProposedText)))
            throw new RepairException("DERT4004", "This recovery record does not match a verified source repair. The document was left unchanged.", backupPath);
        var revision = Hash(current);
        if (revision != receipt.AfterHash && revision != receipt.BeforeHash)
            throw new RepairException("DERT4002", "This document changed after the repair. Undo would replace later edits, so it was stopped. The saved original is available for comparison.", backupPath);
        return new RepairSnapshot(fullPath, Utf8.GetString(current), originalText, revision, plan, identity, true, directory);
    });

    public RepairResult Apply(RepairSnapshot snapshot) => Guard(() =>
    {
        if (!snapshot.CanApply)
            throw new RepairException("DERT4001", "There is no proposed edit to apply. The document was left unchanged.");
        using var target = WindowsFileGuard.OpenExclusive(snapshot.Path);
        var original = Read(target);
        if (Hash(original) != snapshot.Revision || WindowsFileGuard.Identity(target) != snapshot.Identity)
            throw new RepairException("DERT4002", "The document changed after this preview. Preview it again before applying; no source bytes were written.");
        var proposed = Utf8.GetBytes(snapshot.After);
        if (snapshot.IsUndo)
        {
            if (!string.Equals(LatestReceipt(snapshot.Path, snapshot.Identity), snapshot.ReceiptDirectory, StringComparison.Ordinal))
                throw new RepairException("DERT4002", "The latest recovery record changed after this undo preview. Preview undo again; no source bytes were written.");
            var backupPath = Path.Combine(snapshot.ReceiptDirectory!, "before.DERT");
            WindowsFileGuard.CheckPath(backupPath);
            using var backup = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!Read(backup).AsSpan().SequenceEqual(proposed))
                throw new RepairException("DERT4004", "The recovery copy changed after the undo preview. Nothing was written.");
            Commit(target, original, proposed, backupPath);
            return new RepairResult("The original document bytes were restored and verified.", snapshot.ReceiptDirectory!);
        }

        var root = ReceiptRoot(snapshot.Path, snapshot.Identity);
        WindowsFileGuard.CheckPath(root);
        var directory = Path.Combine(root, $"{NextSequence(root):D19}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        WindowsFileGuard.CheckPath(directory);
        var savedPath = Path.Combine(directory, "before.DERT");
        using var saved = new FileStream(savedPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        new FileStreamRepairWriter().Rewrite(saved, original);
        Verify(saved, original);
        var selection = snapshot.Plan is NoteClosurePlan note
            ? new RepairSelection(note.Kind, note.Analysis.Block.LineNumber, note.Boundary.Key) : null;
        var receipt = new RepairReceipt(selection is null ? 1 : 2, snapshot.Path, snapshot.Identity,
            snapshot.Revision, Hash(proposed), DateTimeOffset.UtcNow, selection);
        using var record = new FileStream(Path.Combine(directory, "receipt.json"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var recordBytes = JsonSerializer.SerializeToUtf8Bytes(receipt);
        new FileStreamRepairWriter().Rewrite(record, recordBytes);
        Verify(record, recordBytes);
        Commit(target, original, proposed, savedPath);
        return new RepairResult(snapshot.Plan is NoteClosurePlan
            ? "The selected note boundary was added and its saved bytes verified."
            : "The title rule was aligned and its saved bytes verified.", directory);
    });

    private void Commit(FileStream target, byte[] original, byte[] proposed, string recoveryPath)
    {
        try
        {
            writer.Rewrite(target, proposed);
            Verify(target, proposed);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try
            {
                writer.Rewrite(target, original);
                Verify(target, original);
            }
            catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
            {
                throw new RepairException("DERT4103", "The write and automatic rollback could not finish. The file needs recovery from the retained original; no success is claimed.", recoveryPath, rollback);
            }
            throw new RepairException("DERT4102", "The write did not finish. The previous bytes were restored and verified; the recovery copy was retained.", recoveryPath, error);
        }
    }

    private static string ReceiptRoot(string path, string identity) => Path.Combine(Path.GetDirectoryName(path)!, ".erpdert", "repairs",
        Hash(Utf8.GetBytes(identity)));
    private static string? LatestReceipt(string path, string identity)
    {
        var root = ReceiptRoot(path, identity);
        WindowsFileGuard.CheckPath(root);
        var directory = Directory.Exists(root)
            ? Directory.EnumerateDirectories(root).OrderByDescending(Path.GetFileName, StringComparer.Ordinal).FirstOrDefault()
            : null;
        if (directory is not null) WindowsFileGuard.CheckPath(directory);
        return directory;
    }
    private static long NextSequence(string root)
    {
        long greatest = 0;
        if (Directory.Exists(root))
            foreach (var directory in Directory.EnumerateDirectories(root))
                if (long.TryParse(Path.GetFileName(directory).Split('-')[0], out var value))
                    greatest = Math.Max(greatest, value);
        if (greatest == long.MaxValue)
            throw new RepairException("DERT4004", "The recovery sequence cannot be extended safely. No source bytes were written.");
        return greatest + 1;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void CheckLintRevision(string? expected, byte[] bytes)
    {
        if (expected is not null && !Hash(bytes).Equals(expected, StringComparison.Ordinal))
            throw new RepairException("DERT4002", "This document changed since lint. Run lint again to get current source locations and rehearsal commands; nothing was written.");
    }
    private static ISourceRepairPlan Replay(RepairReceipt receipt, string source, string backupPath)
    {
        try
        {
            return receipt switch
            {
                { Version: 1, Selection: null } => TitleRuleRepair.Plan(DertParser.Parse(source)),
                { Version: 2, Selection: { Kind: NoteClosurePlan.RepairKind, AtLine: int at, CloseBefore: string boundary } } =>
                    NoteClosureRepair.Plan(NoteClosureRepair.Analyze(DertParser.Parse(source), at), boundary),
                _ => throw new RepairException("DERT4004", "This recovery record names an unsupported repair or is missing its selection. Nothing was written.", backupPath)
            };
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            throw new RepairException("DERT4004", "The saved repair selection cannot be reconstructed from its original. Nothing was written.", backupPath, error);
        }
    }
    private static T SelectNote<T>(Func<T> action)
    {
        try { return action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            throw new RepairException("DERT4005", error.Message, inner: error);
        }
    }
    private static byte[] Read(FileStream stream)
    {
        stream.Position = 0;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
    private static void Verify(FileStream stream, byte[] expected)
    {
        if (!Read(stream).AsSpan().SequenceEqual(expected))
            throw new IOException("The bytes read back did not match the intended document.");
    }
    private static T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch (DecoderFallbackException error)
        {
            throw new RepairException("DERT4100", "This file is not readable as UTF-8. Save a UTF-8 copy first; no repair was applied.", inner: error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            throw new RepairException("DERT4100", $"The file or its recovery storage could not be used safely. {error.Message}", inner: error);
        }
    }
}
