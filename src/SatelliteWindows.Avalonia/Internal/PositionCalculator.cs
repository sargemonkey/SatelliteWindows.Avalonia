using Avalonia;

namespace SatelliteWindows.Avalonia.Internal;

/// <summary>
/// Pure geometry: computes satellite position in screen pixels given parent bounds and snap edge.
/// </summary>
internal static class PositionCalculator
{
    /// <summary>
    /// Calculate the pixel position for a satellite window.
    /// All parameters are in screen pixels.
    /// </summary>
    public static PixelPoint Calculate(
        PixelPoint parentPosition,
        PixelSize parentSize,
        PixelSize satelliteSize,
        SnapEdge edge,
        int offsetAlongEdgePx = 0)
    {
        return edge switch
        {
            SnapEdge.Right => new PixelPoint(
                parentPosition.X + parentSize.Width,
                parentPosition.Y + offsetAlongEdgePx),

            SnapEdge.Left => new PixelPoint(
                parentPosition.X - satelliteSize.Width,
                parentPosition.Y + offsetAlongEdgePx),

            SnapEdge.Bottom => new PixelPoint(
                parentPosition.X + offsetAlongEdgePx,
                parentPosition.Y + parentSize.Height),

            SnapEdge.Top => new PixelPoint(
                parentPosition.X + offsetAlongEdgePx,
                parentPosition.Y - satelliteSize.Height),

            _ => parentPosition
        };
    }
}
