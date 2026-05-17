using Avalonia;
using SatelliteWindows.Avalonia.Internal;
using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

public class PositionCalculatorTests
{
    private static readonly PixelPoint Origin = new(100, 200);
    private static readonly PixelSize ParentSize = new(800, 600);
    private static readonly PixelSize SatelliteSize = new(250, 400);

    // ── Right edge ──────────────────────────────────────────────────

    [Fact]
    public void Right_PositionsSatelliteAtParentRightEdge()
    {
        var result = PositionCalculator.Calculate(Origin, ParentSize, SatelliteSize, SnapEdge.Right);

        Assert.Equal(100 + 800, result.X); // parent.X + parent.Width
        Assert.Equal(200, result.Y);       // aligned to parent top
    }

    [Fact]
    public void Right_WithOffset_ShiftsVertically()
    {
        var result = PositionCalculator.Calculate(Origin, ParentSize, SatelliteSize, SnapEdge.Right, offsetAlongEdgePx: 50);

        Assert.Equal(900, result.X);
        Assert.Equal(250, result.Y); // 200 + 50
    }

    // ── Left edge ───────────────────────────────────────────────────

    [Fact]
    public void Left_PositionsSatelliteToLeftOfParent()
    {
        var result = PositionCalculator.Calculate(Origin, ParentSize, SatelliteSize, SnapEdge.Left);

        Assert.Equal(100 - 250, result.X); // parent.X - satellite.Width
        Assert.Equal(200, result.Y);
    }

    [Fact]
    public void Left_WithNegativeOffset_ShiftsUp()
    {
        var result = PositionCalculator.Calculate(Origin, ParentSize, SatelliteSize, SnapEdge.Left, offsetAlongEdgePx: -30);

        Assert.Equal(-150, result.X);
        Assert.Equal(170, result.Y); // 200 - 30
    }

    // ── Bottom edge ─────────────────────────────────────────────────

    [Fact]
    public void Bottom_PositionsSatelliteBelowParent()
    {
        var result = PositionCalculator.Calculate(Origin, ParentSize, SatelliteSize, SnapEdge.Bottom);

        Assert.Equal(100, result.X);
        Assert.Equal(200 + 600, result.Y); // parent.Y + parent.Height
    }

    [Fact]
    public void Bottom_WithOffset_ShiftsHorizontally()
    {
        var result = PositionCalculator.Calculate(Origin, ParentSize, SatelliteSize, SnapEdge.Bottom, offsetAlongEdgePx: 100);

        Assert.Equal(200, result.X); // 100 + 100
        Assert.Equal(800, result.Y);
    }

    // ── Top edge ────────────────────────────────────────────────────

    [Fact]
    public void Top_PositionsSatelliteAboveParent()
    {
        var result = PositionCalculator.Calculate(Origin, ParentSize, SatelliteSize, SnapEdge.Top);

        Assert.Equal(100, result.X);
        Assert.Equal(200 - 400, result.Y); // parent.Y - satellite.Height
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ZeroSizeParent_StillCalculatesCorrectly()
    {
        var result = PositionCalculator.Calculate(Origin, new PixelSize(0, 0), SatelliteSize, SnapEdge.Right);

        Assert.Equal(100, result.X); // parent.X + 0
        Assert.Equal(200, result.Y);
    }

    [Fact]
    public void ZeroSizeSatellite_LeftEdge_PositionsAtParentX()
    {
        var result = PositionCalculator.Calculate(Origin, ParentSize, new PixelSize(0, 0), SnapEdge.Left);

        Assert.Equal(100, result.X); // parent.X - 0
        Assert.Equal(200, result.Y);
    }

    [Fact]
    public void NegativeParentPosition_CalculatesCorrectly()
    {
        var negativeOrigin = new PixelPoint(-500, -300);
        var result = PositionCalculator.Calculate(negativeOrigin, ParentSize, SatelliteSize, SnapEdge.Right);

        Assert.Equal(-500 + 800, result.X);
        Assert.Equal(-300, result.Y);
    }

    [Theory]
    [InlineData(SnapEdge.Right, 900, 200)]
    [InlineData(SnapEdge.Left, -150, 200)]
    [InlineData(SnapEdge.Bottom, 100, 800)]
    [InlineData(SnapEdge.Top, 100, -200)]
    public void AllEdges_NoOffset_ProduceExpectedPositions(SnapEdge edge, int expectedX, int expectedY)
    {
        var result = PositionCalculator.Calculate(Origin, ParentSize, SatelliteSize, edge);

        Assert.Equal(expectedX, result.X);
        Assert.Equal(expectedY, result.Y);
    }
}
