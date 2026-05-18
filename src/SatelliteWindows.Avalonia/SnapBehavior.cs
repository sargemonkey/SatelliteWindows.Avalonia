namespace SatelliteWindows.Avalonia;

/// <summary>
/// Configuration for satellite window snapping behavior.
/// </summary>
public sealed class SnapBehavior
{
    /// <summary>Distance in pixels within which magnetic snapping activates.</summary>
    public int MagneticThresholdPx { get; set; } = 40;

    /// <summary>Whether dragging a satellite away from its snap position auto-detaches it.</summary>
    public bool AutoDetachOnDrag { get; set; } = true;

    /// <summary>Pixel distance a satellite must be dragged before it auto-detaches.</summary>
    public int DetachThresholdPx { get; set; } = 30;

    /// <summary>Whether to automatically snap satellites when dragged near an edge.</summary>
    public bool AutoSnapOnDrag { get; set; } = true;

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

    /// <summary>
    /// Minimum overlap ratio (0.0–1.0) along the shared axis for magnetic snap.
    /// E.g., 0.3 means at least 30% of the smaller window must vertically overlap
    /// the target for a Left/Right snap to trigger. Prevents snapping when windows
    /// are barely aligned.
    /// </summary>
    public double SnapOverlapRatio { get; set; } = 0.3;
}
