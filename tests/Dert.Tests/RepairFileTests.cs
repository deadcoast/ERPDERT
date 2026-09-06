using System.Text;
using System.Runtime.InteropServices;
using Erpdert.Cli.Repairs;
using Xunit;

namespace Dert.Tests;

public class RepairFileTests
{
    [Fact]
    public void Named_streams_are_not_treated_as_independent_regular_documents()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var streamPath = file.Path + ":notes";
        File.WriteAllText(streamPath, "// TITLE\n// ---");
        var service = new RepairFileService();
        Assert.Throws<RepairException>(() => service.Apply(service.Preview(streamPath)));
        Assert.Equal("// TITLE\n// --", File.ReadAllText(file.Path));
        Assert.Equal("// TITLE\n// ---", File.ReadAllText(streamPath));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Hard_linked_documents_are_not_written_through_an_alias()
    {
        using var file = new ScratchDocument("// TITLE");
        var alias = System.IO.Path.Combine(file.DirectoryPath, "alias.DERT");
        Assert.True(CreateHardLink(alias, file.Path, IntPtr.Zero), $"CreateHardLink failed: {Marshal.GetLastWin32Error()}");
        var service = new RepairFileService();
        var error = Assert.Throws<RepairException>(() => service.Apply(service.Preview(file.Path)));
        Assert.Equal("DERT4101", error.Code);
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
        Assert.Equal("// TITLE", File.ReadAllText(alias));
    }

    [Fact]
    public void Invalid_utf8_is_not_replaced_with_lossy_decoding()
    {
        using var file = new ScratchDocument("");
        byte[] bytes = [0x2f, 0x2f, 0x20, 0xff];
        File.WriteAllBytes(file.Path, bytes);
        Assert.Throws<RepairException>(() => new RepairFileService().Preview(file.Path));
        Assert.Equal(bytes, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Backup_changed_after_undo_preview_cannot_be_applied()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService();
        var result = service.Apply(service.Preview(file.Path));
        var undo = service.PreviewUndo(file.Path);
        var before = File.ReadAllBytes(file.Path);
        File.WriteAllText(System.IO.Path.Combine(result.ReceiptDirectory, "before.DERT"), "different");
        Assert.Throws<RepairException>(() => service.Apply(undo));
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Malformed_receipt_is_explained_without_writing_source()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService();
        var result = service.Apply(service.Preview(file.Path));
        File.WriteAllText(System.IO.Path.Combine(result.ReceiptDirectory, "receipt.json"), "{");
        var before = File.ReadAllBytes(file.Path);
        Assert.Throws<RepairException>(() => service.PreviewUndo(file.Path));
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newName, string existingName, IntPtr security);

    [Fact]
    public void Preview_leaves_bytes_and_directory_contents_unchanged()
    {
        using var file = new ScratchDocument("\uFEFF// TITLE\r\n---\r\nwords");
        var before = File.ReadAllBytes(file.Path);
        var preview = new RepairFileService().Preview(file.Path);
        Assert.True(preview.CanApply);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Theory]
    [InlineData("// TITLE")]
    [InlineData("\uFEFF// TITLE\r\n---\r\nwords\n")]
    [InlineData("// TITLE\n// --\nwords")]
    public void Apply_then_undo_restores_the_exact_original_bytes(string text)
    {
        using var file = new ScratchDocument(text);
        var before = File.ReadAllBytes(file.Path);
        var service = new RepairFileService();
        var preview = service.Preview(file.Path);
        var result = service.Apply(preview);
        Assert.Equal(new UTF8Encoding(false).GetBytes(preview.After), File.ReadAllBytes(file.Path));
        Assert.Equal(before, File.ReadAllBytes(System.IO.Path.Combine(result.ReceiptDirectory, "before.DERT")));
        var undo = service.PreviewUndo(file.Path);
        Assert.True(undo.IsUndo);
        service.Apply(undo);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
        Assert.False(service.PreviewUndo(file.Path).CanApply);
    }

    [Fact]
    public void A_document_edited_after_preview_cannot_be_overwritten()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService();
        var preview = service.Preview(file.Path);
        File.WriteAllText(file.Path, "// CHANGED");
        var error = Assert.Throws<RepairException>(() => service.Apply(preview));
        Assert.Equal("DERT4002", error.Code);
        Assert.Equal("// CHANGED", File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void A_replaced_file_with_identical_bytes_invalidates_the_preview()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService();
        var preview = service.Preview(file.Path);
        var replacement = System.IO.Path.Combine(file.DirectoryPath, "replacement.DERT");
        File.WriteAllText(replacement, "// TITLE");
        File.Move(replacement, file.Path, overwrite: true);
        Assert.Throws<RepairException>(() => service.Apply(preview));
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Different_files_that_occupied_the_same_path_keep_separate_originals()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var service = new RepairFileService();
        service.Apply(service.Preview(file.Path));
        var parked = System.IO.Path.Combine(file.DirectoryPath, "parked.DERT");
        File.Move(file.Path, parked);
        File.WriteAllText(file.Path, "// TITLE\n// ---");
        service.Apply(service.Preview(file.Path));
        File.Move(file.Path, System.IO.Path.Combine(file.DirectoryPath, "second.DERT"));
        File.Move(parked, file.Path);
        service.Apply(service.PreviewUndo(file.Path));
        Assert.Equal("// TITLE\n// --", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Latest_receipt_order_does_not_depend_on_the_wall_clock()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var service = new RepairFileService();
        var first = service.Apply(service.Preview(file.Path));
        var futureName = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(first.ReceiptDirectory)!,
            "0999999999999999999-" + Guid.NewGuid().ToString("N"));
        Assert.StartsWith(file.DirectoryPath + System.IO.Path.DirectorySeparatorChar, futureName);
        Directory.Move(first.ReceiptDirectory, futureName);
        File.WriteAllText(file.Path, "// TITLE\n// ---");
        service.Apply(service.Preview(file.Path));
        service.Apply(service.PreviewUndo(file.Path));
        Assert.Equal("// TITLE\n// ---", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Undo_refuses_to_erase_an_edit_made_after_repair()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService();
        service.Apply(service.Preview(file.Path));
        File.AppendAllText(file.Path, "\nnew words");
        var before = File.ReadAllBytes(file.Path);
        Assert.Throws<RepairException>(() => service.PreviewUndo(file.Path));
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Undo_checks_again_after_its_own_preview()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService();
        service.Apply(service.Preview(file.Path));
        var undo = service.PreviewUndo(file.Path);
        File.AppendAllText(file.Path, "\nnew words");
        var before = File.ReadAllBytes(file.Path);
        Assert.Throws<RepairException>(() => service.Apply(undo));
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void A_locked_target_is_left_unchanged()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService();
        var preview = service.Preview(file.Path);
        using (var competingReader = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<RepairException>(() => service.Apply(preview));
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
    }

    [Fact]
    public void A_readonly_target_is_left_unchanged()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService();
        var preview = service.Preview(file.Path);
        File.SetAttributes(file.Path, FileAttributes.ReadOnly);
        Assert.Throws<RepairException>(() => service.Apply(preview));
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Unavailable_recovery_storage_prevents_the_source_write()
    {
        using var file = new ScratchDocument("// TITLE");
        File.WriteAllText(System.IO.Path.Combine(file.DirectoryPath, ".erpdert"), "not a directory");
        var service = new RepairFileService();
        var preview = service.Preview(file.Path);
        Assert.Throws<RepairException>(() => service.Apply(preview));
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Corrupt_backup_cannot_be_restored_over_the_document()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService();
        var result = service.Apply(service.Preview(file.Path));
        File.WriteAllText(System.IO.Path.Combine(result.ReceiptDirectory, "before.DERT"), "wrong words");
        var before = File.ReadAllBytes(file.Path);
        Assert.Throws<RepairException>(() => service.PreviewUndo(file.Path));
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Partial_write_failure_rolls_back_and_retains_the_original()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService(new FailedWrite());
        var error = Assert.Throws<RepairException>(() => service.Apply(service.Preview(file.Path)));
        Assert.Equal("DERT4102", error.Code);
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
        Assert.Equal("// TITLE", File.ReadAllText(error.RecoveryPath!));
    }

    [Fact]
    public void Failed_rollback_reports_the_retained_original_instead_of_success()
    {
        using var file = new ScratchDocument("// TITLE");
        var service = new RepairFileService(new FailedWrite(always: true));
        var error = Assert.Throws<RepairException>(() => service.Apply(service.Preview(file.Path)));
        Assert.Equal("DERT4103", error.Code);
        Assert.Equal("// TITLE", File.ReadAllText(error.RecoveryPath!));
        Assert.Contains("recovery", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FailedWrite(bool always = false) : IRepairWriter
    {
        private bool failed;
        public void Rewrite(FileStream stream, byte[] bytes)
        {
            if (always || !failed)
            {
                failed = true;
                stream.Position = 0;
                stream.WriteByte((byte)'?');
                stream.Flush(true);
                throw new IOException("Injected partial write failure.");
            }
            new FileStreamRepairWriter().Rewrite(stream, bytes);
        }
    }
}
