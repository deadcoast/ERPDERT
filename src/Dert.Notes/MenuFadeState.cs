namespace Dert.Notes;

/// <summary>
/// Visibility policy for the root strip and an explicitly opened menu branch.
/// The host owns hit testing, click-only expansion, focus, and rendering. All
/// times come from the host's monotonic clock; querying opacity changes no state.
/// </summary>
public sealed class MenuFadeState
{
    public const string RootRegionId = "root";
    private static readonly TimeSpan ClickLease = TimeSpan.FromSeconds(5);
    private string[] openPath = [RootRegionId];
    private readonly HashSet<string> fadedAncestors = new(StringComparer.Ordinal);
    private TimeSpan? visibleUntil;
    private string? hoveredRegion, keyboardRegion;

    public bool FadeEnabled { get; set; } = true;

    /// <summary>
    /// Reveals existing chrome for five seconds. Only a click grants this lease:
    /// typing, pointer movement, focus changes, and leaving a menu do not renew it.
    /// </summary>
    public void ClickWindow(TimeSpan now)
    {
        visibleUntil = now + ClickLease;
        fadedAncestors.Clear();
    }

    /// <summary>
    /// Replaces the open branch after an explicit menu activation. The path starts
    /// with "root" and lists each successive flyout; names imply no hierarchy.
    /// Opening grants the same lease as a click, including keyboard activation.
    /// </summary>
    public void OpenPath(IEnumerable<string> regionIds, TimeSpan now)
    {
        ArgumentNullException.ThrowIfNull(regionIds);
        var nextPath = regionIds.ToArray();
        if (nextPath.Length == 0 || nextPath[0] != RootRegionId ||
            nextPath.Any(string.IsNullOrWhiteSpace) ||
            nextPath.Distinct(StringComparer.Ordinal).Count() != nextPath.Length)
            throw new ArgumentException("An open menu path must start at root and contain distinct, nonempty region IDs.", nameof(regionIds));

        openPath = nextPath;
        if (hoveredRegion is not null && !IsOpen(hoveredRegion)) hoveredRegion = null;
        if (keyboardRegion is not null && !IsOpen(keyboardRegion)) keyboardRegion = null;
        ClickWindow(now);
    }

    /// <summary>
    /// Anchors one actual hit region. The whole root strip is supplied as "root";
    /// an unknown ID is outside open chrome and cannot reveal or open anything.
    /// </summary>
    public void HoverRegion(string? id) => hoveredRegion = Anchor(id);

    /// <summary>Anchors the current keyboard menu level; null leaves menu focus.</summary>
    public void SetKeyboardRegion(string? id) => keyboardRegion = Anchor(id);

    /// <summary>Closes flyouts and releases menu anchors without granting a lease.</summary>
    public void CloseToRoot()
    {
        openPath = [RootRegionId];
        hoveredRegion = keyboardRegion = null;
        fadedAncestors.Clear();
    }

    public bool IsOpen(string id) => Array.IndexOf(openPath, id) >= 0;

    /// <summary>
    /// Returns the target opacity, independent of animation or reduced motion.
    /// An exact pointer or keyboard anchor wins over ancestor fading. Disabling
    /// fading shows opened regions only; it never expands a closed branch.
    /// </summary>
    public double SurfaceOpacity(string id, TimeSpan now)
    {
        if (!IsOpen(id)) return 0;
        if (!FadeEnabled || id == hoveredRegion || id == keyboardRegion) return 1;
        if (fadedAncestors.Contains(id)) return 0;
        return visibleUntil is { } deadline && now < deadline ? 1 : 0;
    }

    private string? Anchor(string? id)
    {
        var index = Array.IndexOf(openPath, id);
        if (index < 0) return null;

        // Moving into a child dismisses its unanchored ancestors immediately,
        // even during their click lease. Keep that dismissal when leaving the
        // child, so the parent does not flash back on before the lease expires.
        // Directly returning to a region restores its remaining lease only.
        fadedAncestors.Remove(openPath[index]);
        for (var ancestor = 0; ancestor < index; ancestor++)
            fadedAncestors.Add(openPath[ancestor]);
        return openPath[index];
    }
}
