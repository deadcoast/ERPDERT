using Dert.Core;

namespace Erpdert.Cli;

internal sealed record NodeLocation(DertNode Node, DertNode? Parent);

internal static class NodeWalk
{
    public static IEnumerable<NodeLocation> All(IEnumerable<DertNode> nodes, DertNode? parent = null)
    {
        foreach (var node in nodes)
        {
            yield return new(node, parent);
            IEnumerable<DertNode> children = node switch
            {
                ChainNode chain => chain.Children,
                ReasoningBlockNode block => block.Entries,
                StatementNode statement => statement.Values,
                _ => []
            };
            foreach (var child in All(children, node)) yield return child;
        }
    }

    public static NodeLocation? At(DertDocument document, int line) =>
        All(document.Nodes).Where(n => n.Node.Span.Contains(document.Source.Lines[line - 1].Start))
            .OrderBy(n => n.Node.Span.Length).FirstOrDefault();
}
