using Erpdert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerSceneTests
{
    [Fact]
    public void A_signal_travels_once_without_hiding_or_overwriting_evidence()
    {
        var scene = new MerScene(35, 8);
        scene.Text("evidence", new(8, 2, 20, 2), [InkLine.Plain("No edit was applied.")]);
        scene.Connect("observed", [new(1, 2), new(30, 2)], TimeSpan.Zero);
        var early = scene.Sample(TimeSpan.Zero);
        var moving = scene.Sample(TimeSpan.FromMilliseconds(120));
        var settled = scene.Sample(TimeSpan.FromSeconds(1));
        Assert.NotEqual(Text(early), Text(moving));
        Assert.Equal(Text(settled), Text(scene.Sample(TimeSpan.FromSeconds(10))));
        Assert.All(new[] { early, moving, settled }, frame => Assert.Contains("No edit was applied.", Text(frame)));
        Assert.Contains('─', settled[2].Text);
    }

    [Fact]
    public void Reduced_motion_keeps_the_complete_route_without_timer_changes()
    {
        var scene = new MerScene(30, 8);
        scene.Connect("route", [new(1, 1), new(1, 5), new(20, 5)], TimeSpan.Zero);
        var still = scene.Sample(TimeSpan.Zero, true);
        Assert.Equal(Text(still), Text(scene.Sample(TimeSpan.FromSeconds(10), true)));
        Assert.Contains('╰', still[5].Text);
        Assert.DoesNotContain(';', Text(still));
    }

    [Fact]
    public void Regions_clip_at_their_own_edge_and_do_not_leak_into_neighbors()
    {
        var scene = new MerScene(25, 4);
        scene.Text("left", new(1, 1, 8, 1), [InkLine.Plain("日本語👩‍💻 more text"), InkLine.Plain("Hidden row")]);
        scene.Text("right", new(11, 1, 10, 1), [InkLine.Plain("Keep this")]);
        var frame = scene.Sample(TimeSpan.Zero);
        Assert.Contains("日本語…", frame[1].Text);
        Assert.Contains("Keep this", frame[1].Text);
        Assert.DoesNotContain("Hidden row", Text(frame));
        Assert.All(frame, line => Assert.InRange(TerminalText.Width(line.Text), 0, 25));
    }

    [Fact]
    public void Scene_identity_geometry_and_signal_time_are_validated_before_rendering()
    {
        var scene = new MerScene(25, 5);
        scene.Text("one", new(1, 1, 10, 1), [InkLine.Plain("Text")]);
        Assert.Throws<ArgumentException>(() => scene.Text("one", new(1, 2, 10, 1), []));
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.Text("outside", new(24, 0, 2, 1), []));
        Assert.Throws<ArgumentException>(() => scene.Connect("diagonal", [new(0, 0), new(2, 2)], TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.Connect("negative", [new(0, 0), new(2, 0)], TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.Sample(TimeSpan.FromSeconds(-1)));
    }

    private static string Text(IReadOnlyList<InkLine> lines) => string.Join('\n', lines.Select(line => line.Text));
}
