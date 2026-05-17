namespace SatelliteWindows.Avalonia;

/// <summary>
/// Interface for main windows that support docking satellites as internal panels.
/// Phase 3 feature — stub for forward compatibility.
/// </summary>
public interface ISatelliteDockHost
{
    /// <summary>Dock a satellite as an internal panel on the specified edge.</summary>
    void DockSatellite(SatelliteWindow satellite, SnapEdge edge);

    /// <summary>Undock a satellite, returning it to external satellite mode.</summary>
    void UndockSatellite(SatelliteWindow satellite);
}
