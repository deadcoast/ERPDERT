using System.Text;
using Dert.Cli.Authoring;
using Dert.Cli.Repairs;
using Xunit;

namespace Dert.Tests;

public class DraftFileTests
{
    [Fact]
    public void Save_retains_encoding_and_newlines_and_verifies_a_recovery_copy()
    {
        using var file = new ScratchDocument("");
        var original = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("// TITLE\r\n// -----\r\n")).ToArray();
        File.WriteAllBytes(file.Path, original);
        var service = new DraftFileService();
        var opened = service.Open(file.Path);
        Assert.True(opened.HasBom);
        Assert.DoesNotContain('\uFEFF', opened.Text);
        var saved = service.Save(opened, opened.Text + "// my own words\r\n");
        Assert.Equal(original, File.ReadAllBytes(saved.RecoveryPath!));
        Assert.True(File.ReadAllBytes(file.Path).AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.Equal(opened.Text + "// my own words\r\n", service.Open(file.Path).Text);
        Assert.Null(service.Save(saved.Snapshot, saved.Snapshot.Text).RecoveryPath);
    }

    [Fact]
    public void Save_rejects_external_edits_and_save_as_never_replaces_an_existing_path()
    {
        using var file = new ScratchDocument("original");
        var service = new DraftFileService();
        var opened = service.Open(file.Path);
        File.WriteAllText(file.Path, "external");
        Assert.Throws<RepairException>(() => service.Save(opened, "my buffer"));
        Assert.Throws<IOException>(() => service.Create(file.Path, "new buffer"));
        Assert.Equal("external", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Save_as_creates_a_new_utf8_file_and_keeps_all_punctuation()
    {
        using var file = new ScratchDocument("");
        var path = Path.Combine(file.DirectoryPath, "my [notes] ' new.DERT");
        var text = "// café\n// why ; music ;; C:\\notes https://example.test //\n";
        var result = new DraftFileService().Create(path, text);
        Assert.Equal(text, File.ReadAllText(path));
        Assert.Equal(text, result.Text);
        Assert.False(result.HasBom);
    }

    [Fact]
    public void Failed_write_rolls_back_and_keeps_the_verified_original()
    {
        using var file = new ScratchDocument("original");
        var service = new DraftFileService(new FailFirstWriter());
        var error = Assert.Throws<RepairException>(() => service.Save(service.Open(file.Path), "changed"));
        Assert.Equal("DERT4102", error.Code);
        Assert.Equal("original", File.ReadAllText(file.Path));
        Assert.Equal("original", File.ReadAllText(error.RecoveryPath!));
    }

    [Fact]
    public void Equal_bytes_at_a_replaced_file_cannot_authorize_a_save()
    {
        using var file = new ScratchDocument("original");
        var service = new DraftFileService();
        var opened = service.Open(file.Path);
        File.Move(file.Path, file.Path + ".previous");
        File.WriteAllText(file.Path, "original");
        Assert.Equal("DERT4002", Assert.Throws<RepairException>(() => service.Save(opened, "my buffer")).Code);
        Assert.Equal("original", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Failed_rollback_retains_an_independently_verified_recovery_copy()
    {
        using var file = new ScratchDocument("original");
        var service = new DraftFileService(new FailEveryWriter());
        var error = Assert.Throws<RepairException>(() => service.Save(service.Open(file.Path), "changed"));
        Assert.Equal("DERT4103", error.Code);
        Assert.Equal("original", File.ReadAllText(error.RecoveryPath!));
        Assert.NotEqual("original", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Failed_first_save_never_claims_the_requested_name()
    {
        using var file = new ScratchDocument("");
        var path = Path.Combine(file.DirectoryPath, "new.DERT");
        Assert.Throws<IOException>(() => new DraftFileService(new FailEveryWriter()).Create(path, "my words"));
        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(file.DirectoryPath, ".erpdert-new-*"));
    }

    [Fact]
    public void Invalid_utf8_cannot_be_opened_with_replacement_characters()
    {
        using var file = new ScratchDocument("");
        File.WriteAllBytes(file.Path, [0xff, 0xfe]);
        Assert.Throws<DecoderFallbackException>(() => new DraftFileService().Open(file.Path));
    }

    private sealed class FailFirstWriter : IRepairWriter
    {
        private bool failed;
        public void Rewrite(FileStream file, byte[] bytes)
        {
            if (!failed) { failed = true; file.SetLength(0); throw new IOException("Injected write failure"); }
            new FileStreamRepairWriter().Rewrite(file, bytes);
        }
    }

    private sealed class FailEveryWriter : IRepairWriter
    {
        public void Rewrite(FileStream file, byte[] bytes) { file.SetLength(0); throw new IOException("Injected persistent write failure"); }
    }
}
