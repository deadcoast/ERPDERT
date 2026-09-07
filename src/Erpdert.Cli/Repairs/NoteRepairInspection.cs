using Dert.Core;

namespace Erpdert.Cli.Repairs;

public sealed class NoteRepairInspection
{
    internal NoteRepairInspection(string path, string revision, string identity, NoteClosureAnalysis analysis)
    {
        Path = path; Revision = revision; Identity = identity; Analysis = analysis;
    }
    public string Path { get; }
    public string Revision { get; }
    public string Identity { get; }
    public NoteClosureAnalysis Analysis { get; }
}
