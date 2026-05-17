using Avalonia;
using SatelliteWindows.Avalonia.Internal;
using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

public class DpiHelperTests
{
    // ── LogicalToPixel ──────────────────────────────────────────────

    [Fact]
    public void LogicalToPixel_At100Percent_NoChange()
    {
        var result = DpiHelper.LogicalToPixel(new Size(800, 600), 1.0);

        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
    }

    [Fact]
    public void LogicalToPixel_At150Percent_ScalesUp()
    {
        var result = DpiHelper.LogicalToPixel(new Size(800, 600), 1.5);

        Assert.Equal(1200, result.Width);
        Assert.Equal(900, result.Height);
    }

    [Fact]
    public void LogicalToPixel_At125Percent_RoundsCorrectly()
    {
        // 800 * 1.25 = 1000 (exact)
        // 600 * 1.25 = 750 (exact)
        var result = DpiHelper.LogicalToPixel(new Size(800, 600), 1.25);

        Assert.Equal(1000, result.Width);
        Assert.Equal(750, result.Height);
    }

    [Fact]
    public void LogicalToPixel_At200Percent_Doubles()
    {
        var result = DpiHelper.LogicalToPixel(new Size(400, 300), 2.0);

        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
    }

    [Fact]
    public void LogicalToPixel_FractionalRounding()
    {
        // 101 * 1.25 = 126.25 → rounds to 126
        // 99 * 1.25 = 123.75 → rounds to 124
        var result = DpiHelper.LogicalToPixel(new Size(101, 99), 1.25);

        Assert.Equal(126, result.Width);
        Assert.Equal(124, result.Height);
    }

    [Fact]
    public void LogicalToPixel_ZeroDimensions_ReturnsZero()
    {
        var result = DpiHelper.LogicalToPixel(new Size(0, 0), 1.5);

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
    }

    // ── PixelToLogical ──────────────────────────────────────────────

    [Fact]
    public void PixelToLogical_At100Percent_NoChange()
    {
        var result = DpiHelper.PixelToLogical(new PixelSize(800, 600), 1.0);

        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
    }

    [Fact]
    public void PixelToLogical_At200Percent_Halves()
    {
        var result = DpiHelper.PixelToLogical(new PixelSize(800, 600), 2.0);

        Assert.Equal(400, result.Width);
        Assert.Equal(300, result.Height);
    }

    // ── Round-trip ──────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void RoundTrip_LogicalToPixelAndBack_PreservesApproximately(double scaling)
    {
        var original = new Size(800, 600);
        var pixels = DpiHelper.LogicalToPixel(original, scaling);
        var roundTripped = DpiHelper.PixelToLogical(pixels, scaling);

        // Allow small rounding error due to int truncation in pixel conversion
        Assert.InRange(roundTripped.Width, original.Width - 1, original.Width + 1);
        Assert.InRange(roundTripped.Height, original.Height - 1, original.Height + 1);
    }
}
