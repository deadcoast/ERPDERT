namespace Erpdert.Mer;

public enum Ink { Text, Muted, Copper, Signal, Good, Bad }
public sealed record InkRun(string Text, Ink Ink = Ink.Text, bool Bold = false);

/// <summary>Styled text, never terminal markup or executable escape sequences.</summary>
public sealed class InkLine(params InkRun[] runs)
{
    public IReadOnlyList<InkRun> Runs { get; } = Array.AsReadOnly(runs.ToArray());
    public string Text => string.Concat(Runs.Select(run => run.Text));
    public static InkLine Plain(string text, Ink ink = Ink.Text) => new(new InkRun(text, ink));
}
