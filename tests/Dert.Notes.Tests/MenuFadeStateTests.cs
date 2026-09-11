using Dert.Notes;
using Xunit;

namespace Dert.Notes.Tests;

public class MenuFadeStateTests
{
    [Fact]
    public void Window_click_reveals_root_until_exactly_five_seconds_later()
    {
        var state = new MenuFadeState();
        Assert.True(state.IsOpen("root"));
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.Zero));
        state.ClickWindow(TimeSpan.FromSeconds(10));
        Assert.Equal(1, state.SurfaceOpacity("root", TimeSpan.FromMilliseconds(14999)));
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(15)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("window")]
    [InlineData("editor")]
    [InlineData("root-padding-outside")]
    public void Pointer_outside_open_regions_never_reveals_chrome(string? region)
    {
        var state = new MenuFadeState();
        state.ClickWindow(TimeSpan.Zero);
        state.HoverRegion(region);
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(6)));
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void Hovering_a_closed_branch_cannot_open_it()
    {
        var state = new MenuFadeState();
        state.HoverRegion("file");
        Assert.False(state.IsOpen("file"));
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.Zero));
    }

    [Fact]
    public void Root_strip_is_one_anchor_without_anchoring_open_flyouts()
    {
        var state = new MenuFadeState();
        state.OpenPath(["root", "file", "recent"], TimeSpan.Zero);
        state.HoverRegion("root");
        Assert.Equal(1, state.SurfaceOpacity("root", TimeSpan.FromSeconds(30)));
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.FromSeconds(30)));
        Assert.Equal(0, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Entering_child_fades_ancestors_before_the_click_lease_expires()
    {
        var state = new MenuFadeState();
        state.OpenPath(["root", "file", "recent"], TimeSpan.Zero);
        state.HoverRegion("file");
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(1)));
        Assert.Equal(1, state.SurfaceOpacity("file", TimeSpan.FromSeconds(1)));
        state.HoverRegion("recent");
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(2)));
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.FromSeconds(2)));
        Assert.Equal(1, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(20)));
        state.HoverRegion(null);
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Hierarchy_comes_from_explicit_path_even_when_names_suggest_the_reverse()
    {
        var state = new MenuFadeState();
        state.OpenPath(["root", "file/recent", "file"], TimeSpan.Zero);
        state.HoverRegion("file");
        Assert.Equal(0, state.SurfaceOpacity("file/recent", TimeSpan.FromSeconds(1)));
        Assert.Equal(1, state.SurfaceOpacity("file", TimeSpan.FromSeconds(1)));
        Assert.Equal(0, state.SurfaceOpacity("file/other", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Leaving_after_the_existing_deadline_does_not_grant_five_more_seconds()
    {
        var state = new MenuFadeState();
        state.ClickWindow(TimeSpan.Zero);
        state.HoverRegion("root");
        Assert.Equal(1, state.SurfaceOpacity("root", TimeSpan.FromSeconds(30)));
        state.HoverRegion(null);
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Leaving_before_the_deadline_keeps_only_the_remaining_lease()
    {
        var state = new MenuFadeState();
        state.ClickWindow(TimeSpan.Zero);
        state.HoverRegion("root");
        state.HoverRegion(null);
        Assert.Equal(1, state.SurfaceOpacity("root", TimeSpan.FromSeconds(4)));
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Repeated_visibility_updates_while_authoring_do_not_renew_the_click_lease()
    {
        var state = new MenuFadeState();
        state.ClickWindow(TimeSpan.Zero);
        // The host sends clicks and exact menu anchors only. Typing is deliberately
        // absent from this model; its render updates must not act as interaction.
        for (var second = 1; second < 5; second++)
            Assert.Equal(1, state.SurfaceOpacity("root", TimeSpan.FromSeconds(second)));
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void A_new_click_reveals_suppressed_open_chrome_and_renews_the_lease()
    {
        var state = new MenuFadeState();
        state.OpenPath(["root", "file", "recent"], TimeSpan.Zero);
        state.HoverRegion("recent");
        state.HoverRegion(null);
        state.ClickWindow(TimeSpan.FromSeconds(20));
        Assert.Equal(1, state.SurfaceOpacity("root", TimeSpan.FromSeconds(24)));
        Assert.Equal(1, state.SurfaceOpacity("file", TimeSpan.FromSeconds(24)));
        Assert.Equal(1, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(24)));
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(25)));
    }

    [Fact]
    public void Clicking_an_open_path_renews_the_existing_branch_lease()
    {
        var state = new MenuFadeState();
        state.OpenPath(["root", "file"], TimeSpan.Zero);
        state.OpenPath(["root", "file", "recent"], TimeSpan.FromSeconds(10));
        Assert.Equal(1, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(14)));
        Assert.Equal(0, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void Changing_branch_removes_stale_pointer_and_keyboard_anchors()
    {
        var state = new MenuFadeState();
        state.OpenPath(["root", "file", "recent"], TimeSpan.Zero);
        state.HoverRegion("recent");
        state.SetKeyboardRegion("file");
        state.OpenPath(["root", "settings"], TimeSpan.FromSeconds(10));
        Assert.False(state.IsOpen("file"));
        Assert.False(state.IsOpen("recent"));
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.FromSeconds(10)));
        Assert.Equal(0, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(10)));
        Assert.Equal(0, state.SurfaceOpacity("settings", TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void Keyboard_entry_and_traversal_anchor_only_the_current_menu_level()
    {
        var state = new MenuFadeState();
        state.SetKeyboardRegion("root");
        Assert.Equal(1, state.SurfaceOpacity("root", TimeSpan.FromSeconds(30)));
        state.OpenPath(["root", "file", "recent"], TimeSpan.FromSeconds(30));
        state.SetKeyboardRegion("file");
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(31)));
        Assert.Equal(1, state.SurfaceOpacity("file", TimeSpan.FromSeconds(60)));
        state.SetKeyboardRegion("recent");
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.FromSeconds(31)));
        Assert.Equal(1, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(60)));
        state.SetKeyboardRegion(null);
        Assert.Equal(0, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void Keyboard_focus_cannot_open_a_closed_region()
    {
        var state = new MenuFadeState();
        state.SetKeyboardRegion("file");
        Assert.False(state.IsOpen("file"));
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Explicit_hover_still_anchors_its_region_during_keyboard_navigation()
    {
        var state = new MenuFadeState();
        state.OpenPath(["root", "file", "recent"], TimeSpan.Zero);
        state.HoverRegion("root");
        state.SetKeyboardRegion("recent");
        Assert.Equal(1, state.SurfaceOpacity("root", TimeSpan.FromSeconds(30)));
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.FromSeconds(30)));
        Assert.Equal(1, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Fade_preference_off_keeps_open_surfaces_visible_but_never_opens_others()
    {
        var state = new MenuFadeState { FadeEnabled = false };
        state.OpenPath(["root", "file", "recent"], TimeSpan.Zero);
        state.HoverRegion("recent");
        Assert.Equal(1, state.SurfaceOpacity("root", TimeSpan.FromHours(1)));
        Assert.Equal(1, state.SurfaceOpacity("file", TimeSpan.FromHours(1)));
        Assert.Equal(1, state.SurfaceOpacity("recent", TimeSpan.FromHours(1)));
        Assert.Equal(0, state.SurfaceOpacity("settings", TimeSpan.FromHours(1)));
        state.FadeEnabled = true;
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromHours(1)));
        Assert.Equal(1, state.SurfaceOpacity("recent", TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Closing_to_root_clears_flyouts_and_anchors_without_renewing_the_lease()
    {
        var state = new MenuFadeState();
        state.OpenPath(["root", "file", "recent"], TimeSpan.Zero);
        state.HoverRegion("recent");
        state.SetKeyboardRegion("root");
        state.CloseToRoot();
        Assert.True(state.IsOpen("root"));
        Assert.False(state.IsOpen("file"));
        Assert.False(state.IsOpen("recent"));
        Assert.Equal(0, state.SurfaceOpacity("root", TimeSpan.FromSeconds(6)));
        Assert.Equal(0, state.SurfaceOpacity("recent", TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void Invalid_open_paths_do_not_replace_the_existing_branch()
    {
        var state = new MenuFadeState();
        state.OpenPath(["root", "file"], TimeSpan.Zero);
        Assert.Throws<ArgumentException>(() => state.OpenPath([], TimeSpan.FromSeconds(10)));
        Assert.Throws<ArgumentException>(() => state.OpenPath(["file"], TimeSpan.FromSeconds(10)));
        Assert.Throws<ArgumentException>(() => state.OpenPath(["root", "file", "file"], TimeSpan.FromSeconds(10)));
        Assert.Throws<ArgumentException>(() => state.OpenPath(["root", " "], TimeSpan.FromSeconds(10)));
        Assert.True(state.IsOpen("file"));
        Assert.Equal(0, state.SurfaceOpacity("file", TimeSpan.FromSeconds(10)));
    }
}
