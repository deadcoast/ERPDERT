using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dert.Core;
using Dert.Cli.Repairs;
using Xunit;

namespace Dert.Tests;

public class NoteRepairFileTests
{
    private const string Original = "// n ; t ;;\n// first, // reason\n// second\ncommands ; x ;;";

    private static RepairSnapshot Select(RepairFileService service, string path, string boundary = "4", int at = 1)
    {
        var snapshot = service.PreviewNote(service.InspectNote(path, at), boundary);
        Assert.True(snapshot.CanApply, "The selected note boundary must produce an edit.");
        return snapshot;
    }

    [Fact]
    public void Selection_preserves_source_and_has_no_recovery_side_effect()
    {
        using var file = new ScratchDocument(Original);
        var snapshot = Select(new RepairFileService(), file.Path, "3");
        Assert.Equal("// n ; t ;;\n// first, // reason\n//\n// second\ncommands ; x ;;", snapshot.After);
        Assert.Equal(Original, File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Theory]
    [InlineData("// n ; t ;;", "eof", "// n ; t ;;\r\n//")]
    [InlineData("\uFEFF// n ; t ;;\r\n// café\n", "eof", "\uFEFF// n ; t ;;\r\n// café\n//\n")]
    [InlineData(Original, "3", "// n ; t ;;\n// first, // reason\n//\n// second\ncommands ; x ;;")]
    public void Chosen_note_edit_and_undo_are_byte_exact(string original, string boundary, string expected)
    {
        using var file = new ScratchDocument(original);
        var bytes = File.ReadAllBytes(file.Path);
        var service = new RepairFileService();
        var result = service.Apply(Select(service, file.Path, boundary));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(file.Path));
        Assert.Equal(bytes, File.ReadAllBytes(System.IO.Path.Combine(result.ReceiptDirectory, "before.DERT")));
        service.Apply(service.PreviewUndo(file.Path));
        Assert.Equal(bytes, File.ReadAllBytes(file.Path));
        Assert.False(service.PreviewUndo(file.Path).CanApply);
    }

    [Fact]
    public void Changing_the_file_while_choosing_cannot_refresh_authority_implicitly()
    {
        using var file = new ScratchDocument(Original);
        var service = new RepairFileService();
        var inspection = service.InspectNote(file.Path, 1);
        File.AppendAllText(file.Path, "\n// new words");
        var snapshot = service.PreviewNote(inspection, "3");
        Assert.Equal(Original, snapshot.Before);
        Assert.True(snapshot.CanApply);
        Assert.Throws<RepairException>(() => service.Apply(snapshot));
        Assert.Equal(Original + "\n// new words", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Different_boundaries_produce_different_approvals()
    {
        using var file = new ScratchDocument(Original);
        var service = new RepairFileService();
        var inspection = service.InspectNote(file.Path, 1);
        Assert.NotEqual(service.PreviewNote(inspection, "3").ApprovalToken,
            service.PreviewNote(inspection, "4").ApprovalToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mixed_title_and_note_history_restores_only_the_latest_repair(bool titleFirst)
    {
        using var file = new ScratchDocument("// TITLE\n// --\n// n ; t ;;\n// item");
        var service = new RepairFileService();
        if (titleFirst) service.Apply(service.Preview(file.Path));
        var note = Select(service, file.Path, "eof", 3);
        service.Apply(note);
        if (!titleFirst) service.Apply(service.Preview(file.Path));
        service.Apply(service.PreviewUndo(file.Path));
        Assert.Equal(titleFirst ? "// TITLE\n// -----\n// n ; t ;;\n// item"
            : "// TITLE\n// --\n// n ; t ;;\n// item\n//", File.ReadAllText(file.Path));
        Assert.False(service.PreviewUndo(file.Path).CanApply);
    }

    [Fact]
    public void An_open_undo_preview_is_withdrawn_when_latest_history_changes_even_if_bytes_return()
    {
        using var file = new ScratchDocument("// TITLE\n// --\n// n ; t ;;\n// item");
        var service = new RepairFileService();
        service.Apply(service.Preview(file.Path));
        var staleUndo = service.PreviewUndo(file.Path);
        service.Apply(Select(service, file.Path, "eof", 3));
        service.Apply(service.PreviewUndo(file.Path));
        Assert.Equal(staleUndo.Before, File.ReadAllText(file.Path));
        var error = Assert.Throws<RepairException>(() => service.Apply(staleUndo));
        Assert.Equal("DERT4002", error.Code);
        Assert.Equal(staleUndo.Before, File.ReadAllText(file.Path));
    }

    [Fact]
    public void Legacy_v1_receipts_without_selection_still_restore_original_title_bytes()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var service = new RepairFileService();
        var preview = service.Preview(file.Path);
        var result = service.Apply(preview);
        var path = System.IO.Path.Combine(result.ReceiptDirectory, "receipt.json");
        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.False(written.ContainsKey("Selection"));
        var legacy = new JsonObject
        {
            ["Version"] = 1,
            ["DocumentPath"] = file.Path,
            ["FileIdentity"] = preview.Identity,
            ["BeforeHash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("// TITLE\n// --"))),
            ["AfterHash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("// TITLE\n// -----"))),
            ["CreatedAt"] = "2026-09-06T10:00:00+00:00"
        };
        File.WriteAllText(path, legacy.ToJsonString());
        service.Apply(service.PreviewUndo(file.Path));
        Assert.Equal("// TITLE\n// --", File.ReadAllText(file.Path));
    }

    [Theory]
    [InlineData("Kind", "arbitrary-patch")]
    [InlineData("AtLine", "invalid")]
    [InlineData("CloseBefore", "999")]
    [InlineData("CloseBefore", "1")]
    [InlineData("Selection", "missing")]
    public void Malformed_note_selection_cannot_replay_as_an_arbitrary_patch(string field, string value)
    {
        using var file = new ScratchDocument(Original);
        var service = new RepairFileService();
        var result = service.Apply(Select(service, file.Path));
        var path = System.IO.Path.Combine(result.ReceiptDirectory, "receipt.json");
        var receipt = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (field == "Selection") receipt.Remove(field);
        else receipt["Selection"]![field] = value;
        File.WriteAllText(path, receipt.ToJsonString());
        var before = File.ReadAllBytes(file.Path);
        Assert.Throws<RepairException>(() => service.PreviewUndo(file.Path));
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void An_invalid_latest_note_receipt_does_not_skip_back_to_a_title_original()
    {
        using var file = new ScratchDocument("// TITLE\n// --\n// n ; t ;;\n// item");
        var service = new RepairFileService();
        service.Apply(service.Preview(file.Path));
        var note = service.Apply(Select(service, file.Path, "eof", 3));
        File.WriteAllText(System.IO.Path.Combine(note.ReceiptDirectory, "receipt.json"), "{}");
        var before = File.ReadAllBytes(file.Path);
        Assert.Throws<RepairException>(() => service.PreviewUndo(file.Path));
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Changed_note_backup_after_undo_preview_is_refused()
    {
        using var file = new ScratchDocument(Original);
        var service = new RepairFileService();
        var result = service.Apply(Select(service, file.Path));
        var undo = service.PreviewUndo(file.Path);
        File.WriteAllText(System.IO.Path.Combine(result.ReceiptDirectory, "before.DERT"), "different");
        var before = File.ReadAllBytes(file.Path);
        Assert.Throws<RepairException>(() => service.Apply(undo));
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void A_partial_note_write_uses_the_shared_rollback_path()
    {
        using var file = new ScratchDocument(Original);
        var service = new RepairFileService(new FailOnce());
        var error = Assert.Throws<RepairException>(() => service.Apply(Select(service, file.Path)));
        Assert.Equal("DERT4102", error.Code);
        Assert.Equal(Original, File.ReadAllText(file.Path));
        Assert.Equal(Original, File.ReadAllText(error.RecoveryPath!));
    }

    private sealed class FailOnce : IRepairWriter
    {
        private bool failed;
        public void Rewrite(FileStream stream, byte[] bytes)
        {
            if (!failed)
            {
                failed = true;
                stream.WriteByte((byte)'?');
                stream.Flush(true);
                throw new IOException("Injected write failure.");
            }
            new FileStreamRepairWriter().Rewrite(stream, bytes);
        }
    }
}
