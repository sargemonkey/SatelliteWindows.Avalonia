using Avalonia;
using Avalonia.Controls;

namespace SatelliteWindows.Avalonia;

/// <summary>
/// Describes how a satellite window is attached to its parent.
/// </summary>
public sealed class SatelliteAttachment
{
    /// <summary>The satellite window.</summary>
    public SatelliteWindow Satellite { get; }

    /// <summary>The parent window this satellite is attached to.</summary>
    public Window Parent { get; internal set; }

    /// <summary>Which edge of the parent the satellite is attached to.</summary>
    public SnapEdge Edge { get; }

    /// <summary>Offset along the edge in logical units (DIPs). 0 = aligned to edge start.</summary>
    public double OffsetAlongEdge { get; set; }

    /// <summary>Last position set programmatically by the manager (screen pixels).</summary>
    internal PixelPoint ExpectedPosition { get; set; }

    /// <summary>When this attachment was created (for snap cooldown).</summary>
    internal DateTime AttachedAt { get; set; }

    internal SatelliteAttachment(SatelliteWindow satellite, Window parent, SnapEdge edge, double offsetAlongEdge = 0)
    {
        Satellite = satellite;
        Parent = parent;
        Edge = edge;
        OffsetAlongEdge = offsetAlongEdge;
    }
}
