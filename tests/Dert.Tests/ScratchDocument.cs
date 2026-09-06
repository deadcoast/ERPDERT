using System.Text;

namespace Dert.Tests;

internal sealed class ScratchDocument : IDisposable
{
    private static readonly string ScratchRoot = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(AppContext.BaseDirectory, "repair-scratch"));
    public string DirectoryPath { get; } = System.IO.Path.Combine(ScratchRoot, Guid.NewGuid().ToString("N"));
    public string Path { get; }
    public ScratchDocument(string text)
    {
        Directory.CreateDirectory(DirectoryPath);
        Path = System.IO.Path.Combine(DirectoryPath, "my notes.DERT");
        File.WriteAllText(Path, text, new UTF8Encoding(false));
    }
    public void Dispose()
    {
        var target = System.IO.Path.GetFullPath(DirectoryPath);
        if (!target.StartsWith(ScratchRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Scratch cleanup escaped its dedicated directory.");
        foreach (var file in Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(target, recursive: true);
    }
}
