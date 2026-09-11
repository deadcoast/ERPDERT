using System.Text;
using Dert.Cli.Authoring;
using Dert.Cli.Repairs;
using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class ContextExportTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "dert-context-" + Guid.NewGuid().ToString("N"));
    public ContextExportTests() => Directory.CreateDirectory(folder);
    public void Dispose() => Directory.Delete(folder, true);

    [Fact]
    public void Export_creates_exact_reviewed_UTF8_bytes_without_a_new_BOM_and_leaves_source_untouched()
    {
        var sourcePath = Path.Combine(folder, "notes.DERT");
        const string source = "\uFEFF// HOW ; 日本語 ;; café 👩‍💻, intact. //\r\n";
        var sourceBytes = new UTF8Encoding(false, true).GetBytes(source);
        File.WriteAllBytes(sourcePath, sourceBytes);
        var packet = ContextSelection.Capture(DertParser.Parse(source), 1, sourcePath, ContextSourceKind.FileSnapshot).Build();
        var destination = Path.Combine(folder, "chosen context.txt");
        Assert.Equal(Path.GetFullPath(destination), ContextExport.WriteNew(packet, destination));
        var exported = File.ReadAllBytes(destination);
        Assert.Equal(new UTF8Encoding(false, true).GetBytes(packet.Report), exported);
        Assert.False(exported.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
        using var exclusive = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(exported.Length, exclusive.Length);
    }

    [Fact]
    public void Existing_destination_and_source_are_never_overwritten()
    {
        var sourcePath = Path.Combine(folder, "notes.DERT");
        const string source = "// HOW ; music ;; Keep the words. //";
        File.WriteAllText(sourcePath, source);
        var packet = ContextSelection.Capture(DertParser.Parse(source), 1, sourcePath, ContextSourceKind.FileSnapshot).Build();
        var destination = Path.Combine(folder, "existing.txt");
        File.WriteAllText(destination, "Previous export");
        Assert.Throws<IOException>(() => ContextExport.WriteNew(packet, destination));
        Assert.Throws<IOException>(() => ContextExport.WriteNew(packet, sourcePath));
        Assert.Equal("Previous export", File.ReadAllText(destination));
        Assert.Equal(source, File.ReadAllText(sourcePath));
    }

    [Fact]
    public void A_source_path_cannot_become_an_export_destination_after_its_file_is_deleted()
    {
        var sourcePath = Path.Combine(folder, "notes.txt");
        var packet = ContextSelection.Capture(DertParser.Parse("// HOW ; music ;; Keep it. //"), 1, sourcePath,
            ContextSourceKind.FileSnapshot).Build();
        var error = Assert.Throws<IOException>(() => ContextExport.WriteNew(packet, sourcePath));
        Assert.Contains("source", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(sourcePath));
    }

    [Fact]
    public void Export_requires_a_text_destination_and_does_not_create_parent_folders()
    {
        var packet = ContextSelection.Capture(DertParser.Parse("// HOW ; music ;; Keep it. //"), 1, "Unsaved notes",
            ContextSourceKind.EditorBuffer).Build();
        Assert.Throws<IOException>(() => ContextExport.WriteNew(packet, Path.Combine(folder, "export.DERT")));
        Assert.Throws<IOException>(() => ContextExport.WriteNew(packet, Path.Combine(folder, "absent", "export.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Interrupted_or_mismatched_writes_keep_partial_destination_and_truthfully_refuse_success(bool interrupt)
    {
        var packet = ContextSelection.Capture(DertParser.Parse("// HOW ; music ;; Keep it. //"), 1, "Unsaved notes",
            ContextSourceKind.EditorBuffer).Build();
        var destination = Path.Combine(folder, "partial.txt");
        var error = Assert.Throws<IOException>(() => ContextExport.WriteNew(packet, destination, new PartialWriter(interrupt)));
        Assert.Contains("may contain incomplete text", error.Message);
        Assert.DoesNotContain("restored", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new UTF8Encoding(false, true).GetBytes(packet.Report)[..5], File.ReadAllBytes(destination));
        Assert.Equal("// HOW ; music ;; Keep it. //", packet.Selection.Document.Source.Text);
        Assert.Throws<IOException>(() => ContextExport.WriteNew(packet, destination));
    }

    [Fact]
    public void The_new_file_stays_exclusive_during_writing_and_verification()
    {
        var packet = ContextSelection.Capture(DertParser.Parse("// HOW ; music ;; Keep it. //"), 1, "Unsaved notes",
            ContextSourceKind.EditorBuffer).Build();
        var destination = Path.Combine(folder, "exclusive.txt");
        ContextExport.WriteNew(packet, destination, new ExclusiveWriter());
        Assert.Equal(packet.Report, File.ReadAllText(destination));
    }

    [Fact]
    public void Windows_alternate_streams_cannot_turn_export_into_a_source_file_write()
    {
        if (!OperatingSystem.IsWindows()) return;
        var sourcePath = Path.Combine(folder, "source.DERT");
        const string source = "// HOW ; music ;; Keep it. //";
        File.WriteAllText(sourcePath, source);
        var packet = ContextSelection.Capture(DertParser.Parse(source), 1, sourcePath, ContextSourceKind.FileSnapshot).Build();
        var destination = sourcePath + ":export.txt";
        var error = Assert.Throws<IOException>(() => ContextExport.WriteNew(packet, destination));
        Assert.Contains("separate", error.Message);
        Assert.False(File.Exists(destination));
        Assert.Equal(source, File.ReadAllText(sourcePath));
    }

    private sealed class PartialWriter(bool interrupt) : IRepairWriter
    {
        public void Rewrite(FileStream stream, byte[] bytes)
        {
            stream.Write(bytes.AsSpan(0, 5));
            stream.Flush(flushToDisk: true);
            if (interrupt) throw new IOException("Interrupted test write");
        }
    }

    private sealed class ExclusiveWriter : IRepairWriter
    {
        public void Rewrite(FileStream stream, byte[] bytes)
        {
            Assert.Throws<IOException>(() => { using var competing = new FileStream(stream.Name, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); });
            new FileStreamRepairWriter().Rewrite(stream, bytes);
        }
    }
}
