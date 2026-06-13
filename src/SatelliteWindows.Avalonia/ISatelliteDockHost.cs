namespace SatelliteWindows.Avalonia;

/// <summary>
/// Implemented by a main window that can host a satellite's content as an
/// internal panel embedded directly in its own layout.
///
/// <para>This is the <em>low-level</em> docking contract — paired with
/// <see cref="SatelliteManager.Dock"/> / <see cref="SatelliteManager.Undock"/>.
/// The main window is given a <see cref="SatelliteWindow"/> instance and is
/// responsible for extracting its <see cref="Avalonia.Controls.ContentControl.Content"/>
/// and placing it somewhere in its own visual tree. The manager handles
/// lifecycle / gesture detection but knows nothing about <em>where</em> in the
/// host's layout the content ends up.</para>
///
/// <para>Compare with <see cref="ISatelliteDockBridge"/> + <see cref="SatelliteDockManager"/>:
/// that is the <em>panel-mode facade</em> for hosts that already have a panel
/// concept (id, title, descriptor) and want a single <c>SetMode</c> API to move
/// the panel between Docked / Floating / Satellite / Hidden. Most applications
/// should reach for <see cref="ISatelliteDockBridge"/> first;
/// <see cref="ISatelliteDockHost"/> is the right pick when you have a single
/// pre-existing dock slot and want raw <c>Dock</c>/<c>Undock</c> calls.</para>
/// </summary>
public interface ISatelliteDockHost
{
    /// <summary>
    /// Embed <paramref name="satellite"/>'s content as an internal panel in
    /// this host. Implementations typically extract <c>satellite.Content</c>
    /// (via <see cref="SatelliteWindow.PrepareContentForReparent"/> first) and
    /// place it in their own layout. Return <c>true</c> if the embed succeeded;
    /// the manager will then hide the satellite window.
    /// </summary>
    bool TryDockSatellite(SatelliteWindow satellite, SnapEdge edge);

    /// <summary>
    /// Reverse <see cref="TryDockSatellite"/>: remove the content from the
    /// host's layout and restore it to <paramref name="satellite"/>.Content,
    /// then return <c>true</c>. The manager will re-show the window.
    /// </summary>
    bool TryUndockSatellite(SatelliteWindow satellite);
}
