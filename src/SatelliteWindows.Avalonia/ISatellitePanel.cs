namespace SatelliteWindows.Avalonia;

/// <summary>
/// Describes a panel that can live either docked inside the main window or
/// popped out as a <see cref="SatelliteWindow"/>.
///
/// Hosts implement this on their tool/panel model classes (e.g. a Dock.Avalonia
/// <c>Tool</c> subclass) so <see cref="SatelliteDockManager"/> can host them
/// without taking a dependency on any particular docking library.
/// </summary>
public interface ISatellitePanel
{
    /// <summary>Stable identifier — same across dock and satellite lifetimes.</summary>
    string Id { get; }

    /// <summary>Window title and tab title when satellited.</summary>
    string? Title { get; }

    /// <summary>The visual content (typically a ViewModel resolved via DataTemplates).</summary>
    object Content { get; }

    /// <summary>Default edge to snap to on first pop-out.</summary>
    SnapEdge DefaultSnapEdge { get; }

    /// <summary>
    /// Preferred width for the satellite window. 0 = manager picks a sensible default.
    /// </summary>
    double DefaultSatelliteWidth => 0;

    /// <summary>
    /// Preferred height for the satellite window. 0 = manager matches main window.
    /// </summary>
    double DefaultSatelliteHeight => 0;
}
