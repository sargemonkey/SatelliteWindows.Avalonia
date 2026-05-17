namespace SatelliteWindows.Avalonia;

/// <summary>
/// Configuration for satellite window snapping behavior.
/// </summary>
public sealed class SnapBehavior
{
    /// <summary>Distance in pixels within which magnetic snapping activates (Phase 2).</summary>
    public int MagneticThresholdPx { get; set; } = 20;

    /// <summary>Whether to automatically snap satellites when dragged near an edge (Phase 2).</summary>
    public bool AutoSnapOnDrag { get; set; }

    /// <summary>Whether satellites reposition when the main window is resized.</summary>
    public bool FollowOnResize { get; set; } = true;

    /// <summary>Whether satellites hide when the main window is minimized.</summary>
    public bool MinimizeWithMain { get; set; } = true;

    /// <summary>Whether satellites close when the main window closes.</summary>
    public bool CloseWithMain { get; set; } = true;

    /// <summary>Maximum chain depth for satellite-to-satellite snapping (-1 = unlimited). Phase 2.</summary>
    public int ChainDepthLimit { get; set; } = -1;

    /// <summary>Minimum pixels that must remain visible on any screen.</summary>
    public int MinVisiblePixels { get; set; } = 50;
}
