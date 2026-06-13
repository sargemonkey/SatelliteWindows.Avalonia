using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

public class WindowRoleTests
{
    [Fact]
    public void Enum_HasThreeRoles_WithFloatingDefault()
    {
        // Floating is the implicit default (value 0) so newly-constructed
        // SatelliteWindows start in the rest state.
        Assert.Equal(0, (int)WindowRole.Floating);
        Assert.Equal(WindowRole.Floating, default(WindowRole));
        Assert.True(System.Enum.IsDefined(typeof(WindowRole), WindowRole.Satellite));
        Assert.True(System.Enum.IsDefined(typeof(WindowRole), WindowRole.DockHost));
    }

    [Fact]
    public void SnapBehavior_EnabledDefaultsTrue()
    {
        Assert.True(new SnapBehavior().Enabled);
    }

    [Fact]
    public void SnapBehavior_EnabledCanBeDisabled()
    {
        var b = new SnapBehavior { Enabled = false };
        Assert.False(b.Enabled);
    }
}
