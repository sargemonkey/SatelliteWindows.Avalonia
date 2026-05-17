using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

public class SnapBehaviorTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var behavior = new SnapBehavior();

        Assert.Equal(20, behavior.MagneticThresholdPx);
        Assert.True(behavior.AutoDetachOnDrag);
        Assert.Equal(30, behavior.DetachThresholdPx);
        Assert.True(behavior.AutoSnapOnDrag);
        Assert.True(behavior.FollowOnResize);
        Assert.True(behavior.MinimizeWithMain);
        Assert.True(behavior.CloseWithMain);
        Assert.Equal(-1, behavior.ChainDepthLimit);
        Assert.Equal(50, behavior.MinVisiblePixels);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var behavior = new SnapBehavior
        {
            MagneticThresholdPx = 30,
            AutoDetachOnDrag = false,
            DetachThresholdPx = 50,
            AutoSnapOnDrag = true,
            FollowOnResize = false,
            MinimizeWithMain = false,
            CloseWithMain = false,
            ChainDepthLimit = 5,
            MinVisiblePixels = 100
        };

        Assert.Equal(30, behavior.MagneticThresholdPx);
        Assert.False(behavior.AutoDetachOnDrag);
        Assert.Equal(50, behavior.DetachThresholdPx);
        Assert.True(behavior.AutoSnapOnDrag);
        Assert.False(behavior.FollowOnResize);
        Assert.False(behavior.MinimizeWithMain);
        Assert.False(behavior.CloseWithMain);
        Assert.Equal(5, behavior.ChainDepthLimit);
        Assert.Equal(100, behavior.MinVisiblePixels);
    }
}
