using Avalonia;
using SatelliteWindows.Avalonia.Internal;
using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

public class ScreenHelperTests
{
    // Standard test screens
    private static readonly PixelRect PrimaryScreen = new(0, 0, 1920, 1080);
    private static readonly PixelRect RightScreen = new(1920, 0, 1920, 1080);
    private static readonly PixelRect LeftScreen = new(-1920, 0, 1920, 1080);
    private static readonly PixelRect AboveScreen = new(0, -1080, 1920, 1080);

    private static readonly PixelSize WindowSize = new(300, 200);

    // ── GetVirtualDesktopBounds ─────────────────────────────────────

    [Fact]
    public void VirtualDesktop_EmptyScreens_ReturnsDefault()
    {
        var result = ScreenHelper.GetVirtualDesktopBounds(Array.Empty<PixelRect>());

        Assert.Equal(default, result);
    }

    [Fact]
    public void VirtualDesktop_SingleScreen_ReturnsThatScreen()
    {
        var result = ScreenHelper.GetVirtualDesktopBounds(new[] { PrimaryScreen });

        Assert.Equal(PrimaryScreen, result);
    }

    [Fact]
    public void VirtualDesktop_TwoScreensSideBySide_ReturnsUnion()
    {
        var result = ScreenHelper.GetVirtualDesktopBounds(new[] { PrimaryScreen, RightScreen });

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(3840, result.Width);  // 1920 + 1920
        Assert.Equal(1080, result.Height);
    }

    [Fact]
    public void VirtualDesktop_ScreenToLeft_HandlesNegativeCoordinates()
    {
        var result = ScreenHelper.GetVirtualDesktopBounds(new[] { PrimaryScreen, LeftScreen });

        Assert.Equal(-1920, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(3840, result.Width);
        Assert.Equal(1080, result.Height);
    }

    [Fact]
    public void VirtualDesktop_ThreeScreens_UnionsAll()
    {
        var screens = new[] { LeftScreen, PrimaryScreen, RightScreen };
        var result = ScreenHelper.GetVirtualDesktopBounds(screens);

        Assert.Equal(-1920, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(5760, result.Width); // 1920 * 3
        Assert.Equal(1080, result.Height);
    }

    [Fact]
    public void VirtualDesktop_ScreenAbove_ExtendsVertically()
    {
        var result = ScreenHelper.GetVirtualDesktopBounds(new[] { PrimaryScreen, AboveScreen });

        Assert.Equal(0, result.X);
        Assert.Equal(-1080, result.Y);
        Assert.Equal(1920, result.Width);
        Assert.Equal(2160, result.Height); // 1080 * 2
    }

    // ── ClampToVisibleArea — window fully visible ───────────────────

    [Fact]
    public void Clamp_FullyVisible_ReturnsUnchanged()
    {
        var pos = new PixelPoint(100, 100);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, new[] { PrimaryScreen });

        Assert.Equal(pos, result);
    }

    [Fact]
    public void Clamp_EmptyScreenList_ReturnsUnchanged()
    {
        var pos = new PixelPoint(9999, 9999);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, Array.Empty<PixelRect>());

        Assert.Equal(pos, result);
    }

    // ── ClampToVisibleArea — partially visible (should NOT clamp) ───

    [Fact]
    public void Clamp_PartiallyOffRightEdge_StillVisible_ReturnsUnchanged()
    {
        // Window at right edge: 300px wide, starting at 1800 → extends to 2100
        // That's 120px overlap (1920 - 1800), which is > 50px minimum
        var pos = new PixelPoint(1800, 100);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, new[] { PrimaryScreen });

        Assert.Equal(pos, result);
    }

    [Fact]
    public void Clamp_SpanningTwoScreens_ReturnsUnchanged()
    {
        // Window sitting across the boundary of two side-by-side screens
        var pos = new PixelPoint(1800, 100);
        var screens = new[] { PrimaryScreen, RightScreen };

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, screens);

        Assert.Equal(pos, result);
    }

    // ── ClampToVisibleArea — entirely off screen (should clamp) ─────

    [Fact]
    public void Clamp_EntirelyOffRight_ClampedBackToVisible()
    {
        var pos = new PixelPoint(5000, 100);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, new[] { PrimaryScreen }, minVisiblePx: 50);

        // Should be clamped so at least 50px is visible
        Assert.True(result.X < 5000, "X should have been clamped left");
        Assert.True(result.X + WindowSize.Width > PrimaryScreen.X, "Some window should be visible on screen");
        // Specifically: X ≤ 1920 - 50 = 1870
        Assert.Equal(1870, result.X);
    }

    [Fact]
    public void Clamp_EntirelyOffLeft_ClampedBackToVisible()
    {
        var pos = new PixelPoint(-5000, 100);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, new[] { PrimaryScreen }, minVisiblePx: 50);

        // Should be clamped so at least 50px is visible
        // X ≥ 0 - 300 + 50 = -250
        Assert.Equal(-250, result.X);
    }

    [Fact]
    public void Clamp_EntirelyOffTop_ClampedBackToVisible()
    {
        var pos = new PixelPoint(100, -5000);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, new[] { PrimaryScreen }, minVisiblePx: 50);

        // Y ≥ 0 - 200 + 50 = -150
        Assert.Equal(-150, result.Y);
    }

    [Fact]
    public void Clamp_EntirelyOffBottom_ClampedBackToVisible()
    {
        var pos = new PixelPoint(100, 5000);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, new[] { PrimaryScreen }, minVisiblePx: 50);

        // Y ≤ 1080 - 50 = 1030
        Assert.Equal(1030, result.Y);
    }

    // ── ClampToVisibleArea — L-shaped layout ────────────────────────

    [Fact]
    public void Clamp_LShapedLayout_WindowInDeadZone_Clamped()
    {
        // L-shaped: primary at (0,0) and a screen below-right at (1920, 1080)
        // Dead zone at (1920, 0) — no screen there
        var bottomRight = new PixelRect(1920, 1080, 1920, 1080);
        var screens = new[] { PrimaryScreen, bottomRight };

        // Place window in the dead zone at (2000, 500) — not on any screen
        var pos = new PixelPoint(2000, 500);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, screens, minVisiblePx: 50);

        // Should be clamped — verify it's on at least one screen
        bool isOnSomeScreen = false;
        foreach (var screen in screens)
        {
            int overlapX = Math.Max(0, Math.Min(result.X + WindowSize.Width, screen.X + screen.Width) - Math.Max(result.X, screen.X));
            int overlapY = Math.Max(0, Math.Min(result.Y + WindowSize.Height, screen.Y + screen.Height) - Math.Max(result.Y, screen.Y));
            if (overlapX > 0 && overlapY > 0)
            {
                isOnSomeScreen = true;
                break;
            }
        }
        Assert.True(isOnSomeScreen, "Clamped window should be visible on some screen");
    }

    // ── ClampToVisibleArea — minVisiblePx threshold ─────────────────

    [Fact]
    public void Clamp_BarelyBelowThreshold_GetsAdjusted()
    {
        // Window is only 40px overlapping, threshold is 50
        // 300px window at X=1880 → overlaps 40px (1920-1880) on X
        // But 200px tall fully visible → overlaps 200px on Y which is > 50
        var pos = new PixelPoint(1880, 100);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, new[] { PrimaryScreen }, minVisiblePx: 50);

        // overlapX = 40 (<50) but overlapY = 200 (≥50) → condition is ||, so visible
        Assert.Equal(pos, result);
    }

    [Fact]
    public void Clamp_BothAxesBelowThreshold_GetsAdjusted()
    {
        // Window barely overlapping: only 30px on X and 30px on Y
        // 300px window at X=1890, 200px window at Y=1050
        // overlapX = 1920-1890 = 30, overlapY = 1080-1050 = 30, both < 50
        var pos = new PixelPoint(1890, 1050);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, new[] { PrimaryScreen }, minVisiblePx: 50);

        Assert.NotEqual(pos, result); // Should have been clamped
    }

    // ── Negative coordinate screens ─────────────────────────────────

    [Fact]
    public void Clamp_NegativeScreenCoords_WorksCorrectly()
    {
        var pos = new PixelPoint(-1800, 100);

        var result = ScreenHelper.ClampToVisibleArea(pos, WindowSize, new[] { LeftScreen });

        Assert.Equal(pos, result); // Should be visible on left screen
    }
}
