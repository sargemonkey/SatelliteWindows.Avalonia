namespace SatelliteWindows.Avalonia;

/// <summary>
/// Interface for main windows that support docking satellites as internal panels.
/// The host controls WHERE and HOW content is embedded in its layout.
/// The manager controls lifecycle, state, and gesture detection.
/// </summary>
public interface ISatelliteDockHost
{
    /// <summary>
    /// Attempt to dock a satellite's content as an internal panel.
    /// The host should extract satellite.Content, embed it in its layout,
    /// and return true on success. The manager will hide the satellite window.
    /// </summary>
    bool TryDockSatellite(SatelliteWindow satellite, SnapEdge edge);

    /// <summary>
    /// Attempt to undock a satellite's content from the internal layout.
    /// The host should extract the content from its layout and restore it
    /// to satellite.Content, then return true. The manager will re-show the window.
    /// </summary>
    bool TryUndockSatellite(SatelliteWindow satellite);
}
