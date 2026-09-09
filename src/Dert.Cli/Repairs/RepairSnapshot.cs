using Dert.Core;
using System.Security.Cryptography;
using System.Text;

namespace Dert.Cli.Repairs;

public sealed class RepairSnapshot
{
    internal RepairSnapshot(string path, string before, string after, string revision,
        ISourceRepairPlan plan, string identity, bool isUndo = false, string? receiptDirectory = null)
    {
        Path = path; Before = before; After = after; Revision = revision;
        Plan = plan; IsUndo = isUndo; ReceiptDirectory = receiptDirectory;
        Identity = identity;
        // Approval names the whole action, not just input bytes: two undo records can
        // begin with identical repaired source yet restore different originals.
        ApprovalToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
            plan.Kind, isUndo ? "undo" : "repair", path, identity, revision,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(after))), receiptDirectory ?? ""))));
    }
    public string Path { get; }
    public string Before { get; }
    public string After { get; }
    public string Revision { get; }
    public string ApprovalToken { get; }
    public string Identity { get; }
    public ISourceRepairPlan Plan { get; }
    public bool IsUndo { get; }
    public string? ReceiptDirectory { get; }
    public bool CanApply => !string.Equals(Before, After, StringComparison.Ordinal);
}

public sealed class RepairResult
{
    internal RepairResult(string message, string receiptDirectory, byte[] verifiedBytes)
    {
        Message = message;
        ReceiptDirectory = receiptDirectory;
        // Capture immutable source and its raw-byte revision while Apply still
        // holds the verified file. Presentation must not reopen a changing path.
        VerifiedSource = new UTF8Encoding(false, true).GetString(verifiedBytes);
        Revision = Convert.ToHexString(SHA256.HashData(verifiedBytes));
    }
    public string Message { get; }
    public string ReceiptDirectory { get; }
    public string VerifiedSource { get; }
    public string Revision { get; }
}

public sealed class RepairException(string code, string message, string? recoveryPath = null,
    Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
    public string? RecoveryPath { get; } = recoveryPath;
}

public interface IRepairWriter
{
    void Rewrite(FileStream stream, byte[] bytes);
}

public sealed class FileStreamRepairWriter : IRepairWriter
{
    public void Rewrite(FileStream stream, byte[] bytes)
    {
        stream.Position = 0;
        stream.Write(bytes);
        stream.SetLength(bytes.Length);
        stream.Flush(flushToDisk: true);
    }
}
