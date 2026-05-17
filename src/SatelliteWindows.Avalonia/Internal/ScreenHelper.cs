using Avalonia;
using Avalonia.Controls;

namespace SatelliteWindows.Avalonia.Internal;

/// <summary>
/// Multi-monitor helpers: virtual desktop bounds and visibility clamping.
/// All coordinates are in screen pixels.
/// </summary>
internal static class ScreenHelper
{
    // ── Overloads accepting Avalonia Screens (used by SatelliteManager) ──

    public static PixelRect GetVirtualDesktopBounds(Screens screens)
    {
        return GetVirtualDesktopBounds(ExtractBounds(screens));
    }

    public static PixelPoint ClampToVisibleArea(
        PixelPoint position, PixelSize windowSize, Screens screens, int minVisiblePx = 50)
    {
        return ClampToVisibleArea(position, windowSize, ExtractBounds(screens), minVisiblePx);
    }

    // ── Core implementations accepting IReadOnlyList<PixelRect> (testable) ──

    /// <summary>
    /// Compute the virtual desktop rect as the union of all screen bounds.
    /// </summary>
    public static PixelRect GetVirtualDesktopBounds(IReadOnlyList<PixelRect> screenBounds)
    {
        if (screenBounds.Count == 0)
            return default;

        int left = int.MaxValue, top = int.MaxValue;
        int right = int.MinValue, bottom = int.MinValue;

        foreach (var b in screenBounds)
        {
            left = Math.Min(left, b.X);
            top = Math.Min(top, b.Y);
            right = Math.Max(right, b.X + b.Width);
            bottom = Math.Max(bottom, b.Y + b.Height);
        }

        return new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Clamp a proposed window position so that at least <paramref name="minVisiblePx"/>
    /// pixels remain visible on at least one screen.
    /// Checks per-screen intersection — handles L-shaped layouts correctly.
    /// </summary>
    public static PixelPoint ClampToVisibleArea(
        PixelPoint position,
        PixelSize windowSize,
        IReadOnlyList<PixelRect> screenBounds,
        int minVisiblePx = 50)
    {
        if (screenBounds.Count == 0)
            return position;

        var winRight = position.X + windowSize.Width;
        var winBottom = position.Y + windowSize.Height;

        foreach (var sb in screenBounds)
        {
            int overlapX = Math.Max(0, Math.Min(winRight, sb.X + sb.Width) - Math.Max(position.X, sb.X));
            int overlapY = Math.Max(0, Math.Min(winBottom, sb.Y + sb.Height) - Math.Max(position.Y, sb.Y));

            if (overlapX > 0 && overlapY > 0
                && (overlapX >= minVisiblePx || overlapY >= minVisiblePx))
            {
                return position;
            }
        }

        // Not sufficiently visible — clamp to the nearest individual screen
        var nearestScreen = FindNearestScreen(position, windowSize, screenBounds);
        int x = Math.Clamp(position.X,
            nearestScreen.X - windowSize.Width + minVisiblePx,
            nearestScreen.X + nearestScreen.Width - minVisiblePx);
        int y = Math.Clamp(position.Y,
            nearestScreen.Y - windowSize.Height + minVisiblePx,
            nearestScreen.Y + nearestScreen.Height - minVisiblePx);

        return new PixelPoint(x, y);
    }

    /// <summary>
    /// Find the screen whose center is closest to the window's center.
    /// Used as the clamping target when the window isn't visible on any screen.
    /// </summary>
    private static PixelRect FindNearestScreen(
        PixelPoint windowPos, PixelSize windowSize, IReadOnlyList<PixelRect> screenBounds)
    {
        double winCx = windowPos.X + windowSize.Width / 2.0;
        double winCy = windowPos.Y + windowSize.Height / 2.0;

        var nearest = screenBounds[0];
        double minDist = double.MaxValue;

        foreach (var sb in screenBounds)
        {
            double scx = sb.X + sb.Width / 2.0;
            double scy = sb.Y + sb.Height / 2.0;
            double dist = (winCx - scx) * (winCx - scx) + (winCy - scy) * (winCy - scy);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = sb;
            }
        }

        return nearest;
    }

    private static IReadOnlyList<PixelRect> ExtractBounds(Screens screens)
    {
        var all = screens.All;
        var bounds = new PixelRect[all.Count];
        for (int i = 0; i < all.Count; i++)
            bounds[i] = all[i].Bounds;
        return bounds;
    }
}
