namespace Erpdert.Mer;

public readonly record struct ScenePoint(int X, int Y);
public readonly record struct SceneRegion(int X, int Y, int Width, int Height);

/// <summary>Named text regions and event-timed routes on an inline cell surface.
/// Sampling is deterministic; motion cannot change content or claim a result.</summary>
public sealed class MerScene
{
    private sealed record TextRegion(SceneRegion Bounds, IReadOnlyList<InkLine> Lines);
    private sealed record Signal(ScenePoint[] Path, TimeSpan Observed);
    private readonly int width;
    private readonly int height;
    private readonly HashSet<string> ids = new(StringComparer.Ordinal);
    private readonly List<TextRegion> regions = [];
    private readonly List<Signal> signals = [];

    public MerScene(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        this.width = width;
        this.height = height;
    }

    public void Text(string id, SceneRegion region, IReadOnlyList<InkLine> lines)
    {
        if (region.X < 0 || region.Y < 0 || region.Width < 1 || region.Height < 1 ||
            region.X > width - region.Width || region.Y > height - region.Height)
            throw new ArgumentOutOfRangeException(nameof(region));
        Identity(id);
        regions.Add(new(region, Array.AsReadOnly(lines.Take(region.Height).ToArray())));
    }

    public void Connect(string id, IReadOnlyList<ScenePoint> route, TimeSpan observed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(observed, TimeSpan.Zero);
        if (route.Count < 2) throw new ArgumentException("A route needs two endpoints.", nameof(route));
        if (route.Any(point => point.X < 0 || point.X >= width || point.Y < 0 || point.Y >= height))
            throw new ArgumentOutOfRangeException(nameof(route));
        var path = new List<ScenePoint> { route[0] };
        foreach (var next in route.Skip(1))
        {
            var current = path[^1];
            if (current.X != next.X && current.Y != next.Y) throw new ArgumentException("Route segments must follow rows or columns.", nameof(route));
            while (current != next)
            {
                current = new(current.X + Math.Sign(next.X - current.X), current.Y + Math.Sign(next.Y - current.Y));
                path.Add(current);
            }
        }
        Identity(id);
        signals.Add(new(path.ToArray(), observed));
    }

    public IReadOnlyList<InkLine> Sample(TimeSpan elapsed, bool still = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        var canvas = new CellCanvas(width, height);
        var joins = new Dictionary<ScenePoint, int>();
        foreach (var signal in signals)
            for (var i = 0; i < signal.Path.Length; i++)
            {
                var point = signal.Path[i];
                var directions = (i > 0 ? Direction(point, signal.Path[i - 1]) : 0) |
                    (i + 1 < signal.Path.Length ? Direction(point, signal.Path[i + 1]) : 0);
                joins[point] = joins.GetValueOrDefault(point) | directions;
            }
        foreach (var (point, directions) in joins)
            if (!Protected(point)) canvas.Put(point.X, point.Y, Glyph(directions), Ink.Muted);
        if (!still)
            foreach (var signal in signals)
            {
                var age = (elapsed - signal.Observed).TotalMilliseconds;
                if (age < 0 || age >= 480) continue;
                var progress = 1 - Math.Pow(1 - age / 480, 3);
                var point = signal.Path[Math.Min(signal.Path.Length - 1, (int)(progress * signal.Path.Length))];
                if (!Protected(point)) canvas.Put(point.X, point.Y, ";", Ink.Signal, true);
            }
        foreach (var region in regions)
            for (var i = 0; i < region.Lines.Count; i++)
                canvas.Put(region.Bounds.X, region.Bounds.Y + i, region.Lines[i], region.Bounds.Width);
        return canvas.Lines();

        bool Protected(ScenePoint point) => regions.Any(region => point.X >= region.Bounds.X &&
            point.X < region.Bounds.X + region.Bounds.Width && point.Y >= region.Bounds.Y && point.Y < region.Bounds.Y + region.Bounds.Height);
    }

    private void Identity(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new ArgumentException("Each scene element needs a unique nonempty identity.", nameof(id));
    }

    private static int Direction(ScenePoint from, ScenePoint to) => to.X > from.X ? 1 : to.X < from.X ? 2 : to.Y > from.Y ? 4 : to.Y < from.Y ? 8 : 0;
    private static string Glyph(int directions) => directions switch
    {
        1 or 2 or 3 => "─", 4 or 8 or 12 => "│", 5 => "╭", 6 => "╮", 9 => "╰", 10 => "╯",
        7 => "┬", 11 => "┴", 13 => "├", 14 => "┤", 15 => "┼", _ => "·"
    };
}
