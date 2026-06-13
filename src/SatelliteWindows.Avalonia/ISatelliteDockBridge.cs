namespace SatelliteWindows.Avalonia;

/// <summary>
/// Bridge between <see cref="SatelliteDockManager"/> and the host application's
/// docking framework (e.g. Dock.Avalonia, AvalonDock, or a custom layout system).
///
/// The manager owns satellite windows and their snap behaviour. The bridge owns
/// the docked side: show/hide a panel in the dock, extract it when popping out
/// to a satellite, and reinsert it when the satellite docks back (or closes).
///
/// All methods are called from the UI thread.
/// </summary>
public interface ISatelliteDockBridge
{
    /// <summary>Find a registered panel by ID. Null if unknown.</summary>
    ISatellitePanel? FindPanel(string panelId);

    /// <summary>True iff the panel is currently visible inside the dock layout.</summary>
    bool IsDocked(string panelId);

    /// <summary>Show (or re-show) the panel in its dock slot. Returns success.</summary>
    bool ShowDocked(string panelId);

    /// <summary>Hide the panel from its dock slot. Returns success.</summary>
    bool HideFromDock(string panelId);

    /// <summary>
    /// Hide the panel from the dock and hand off its model so the manager can
    /// host it as a satellite. Returns the panel, or null if the panel isn't
    /// registered or cannot be extracted.
    /// </summary>
    ISatellitePanel? ExtractForSatellite(string panelId);

    /// <summary>
    /// Reinsert the panel back into its dock slot after satellite dock-back or close.
    /// </summary>
    bool ReinsertFromSatellite(string panelId);
}
