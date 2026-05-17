using Avalonia;

namespace SatelliteWindows.Avalonia.Internal;

internal static class DpiHelper
{
    /// <summary>
    /// Convert logical (DIP) size to pixel size using the given scale factor.
    /// </summary>
    public static PixelSize LogicalToPixel(Size logicalSize, double scaling)
    {
        return new PixelSize(
            (int)Math.Round(logicalSize.Width * scaling),
            (int)Math.Round(logicalSize.Height * scaling));
    }

    /// <summary>
    /// Convert pixel size to logical (DIP) size using the given scale factor.
    /// </summary>
    public static Size PixelToLogical(PixelSize pixelSize, double scaling)
    {
        return new Size(
            pixelSize.Width / scaling,
            pixelSize.Height / scaling);
    }
}
