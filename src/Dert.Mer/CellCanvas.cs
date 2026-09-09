using System.Text;

namespace Dert.Mer;

/// <summary>A small retained cell surface. Continuation cells never split a grapheme.</summary>
internal sealed class CellCanvas(int width, int height)
{
    private sealed record Cell(string Glyph, Ink Ink, bool Bold);
    private readonly Cell?[,] cells = new Cell?[height, width];

    public void Put(int x, int y, string text, Ink ink = Ink.Text, bool bold = false, int? limit = null) =>
        Put(x, y, new InkLine(new InkRun(text, ink, bold)), limit);

    public void Put(int x, int y, InkLine line, int? limit = null)
    {
        if (y < 0 || y >= height || x < 0 || x >= width) return;
        var available = Math.Min(width - x, limit ?? width);
        if (available < 1) return;
        var elements = line.Runs.SelectMany(run => TerminalText.Elements(run.Text)
            .Select(element => (element.Text, element.Width, run.Ink, run.Bold))).ToArray();
        var clipped = elements.Sum(element => element.Width) > available;
        var end = x + available - (clipped ? 1 : 0);
        foreach (var element in elements)
        {
            if (x + element.Width > end) break;
            cells[y, x++] = new(element.Text, element.Ink, element.Bold);
            if (element.Width == 2) cells[y, x++] = new("", element.Ink, element.Bold);
        }
        if (clipped) cells[y, x] = new("…", Ink.Muted, false);
    }

    public IReadOnlyList<InkLine> Lines()
    {
        var result = new List<InkLine>();
        for (var y = 0; y < height; y++)
        {
            var runs = new List<InkRun>();
            var buffer = new StringBuilder();
            var ink = Ink.Text;
            var bold = false;
            var end = width - 1;
            while (end >= 0 && cells[y, end] is null) end--;
            for (var x = 0; x <= end; x++)
            {
                var cell = cells[y, x] ?? new(" ", Ink.Text, false);
                if (cell.Ink != ink || cell.Bold != bold)
                {
                    if (buffer.Length > 0) runs.Add(new(buffer.ToString(), ink, bold));
                    buffer.Clear();
                    ink = cell.Ink;
                    bold = cell.Bold;
                }
                buffer.Append(cell.Glyph);
            }
            if (buffer.Length > 0) runs.Add(new(buffer.ToString(), ink, bold));
            result.Add(new(runs.ToArray()));
        }
        return result;
    }
}
