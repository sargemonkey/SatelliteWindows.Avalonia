using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

/// <summary>
/// Regression coverage for the dock / detach / re-dock bug chain we hit during
/// the tri-role demo:
///
///   1. "already has a visual parent" when reinserting a panel still parented
///      to the about-to-close satellite,
///   2. "Attempt to call InvalidateArrange on wrong LayoutManager" when the
///      satellite's layout queue isn't flushed before close, and
///   3. The satellite window staying visible after dock-back because DetachCore
///      early-returned when the snap-chain drag detector had already removed
///      the attachment from _allAttachments.
/// </summary>
public class DockBackTests
{
    [AvaloniaFact]
    public void FlushContentForReparent_ClearsContentAndUnparentsChild()
    {
        var border = new Border();
        var sat = new SatelliteWindow { Width = 100, Height = 100, Content = border };
        sat.Show();
        Assert.NotNull(border.GetVisualParent());

        SatelliteManager.FlushContentForReparent(sat);

        Assert.Null(sat.Content);
        Assert.Null(border.GetVisualParent());
        sat.Close();
    }

    [AvaloniaFact]
    public void FlushContentForReparent_NoOpOnNullContent_DoesNotThrow()
    {
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();
        SatelliteManager.FlushContentForReparent(sat);
        Assert.Null(sat.Content);
        sat.Close();
    }

    [AvaloniaFact]
    public void FlushContentForReparent_AllowsImmediateReparentIntoAnotherWindow()
    {
        var border = new Border();
        var sat = new SatelliteWindow { Width = 100, Height = 100, Content = border };
        sat.Show();
        var other = new Window { Width = 100, Height = 100 };
        other.Show();

        SatelliteManager.FlushContentForReparent(sat);
        other.Content = border;
        other.UpdateLayout();

        Assert.Same(border, other.Content);
        sat.Close();
        other.Close();
    }

    [AvaloniaFact]
    public void FlushContentForReparent_ThrowsOnNullArgument()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            SatelliteManager.FlushContentForReparent(null!));
    }

    [AvaloniaFact]
    public void InitialSetModeDocked_PlacesPanelInHost()
    {
        var h = TestHelpers.BuildHost();
        Assert.Same(h.Panel.Content, h.Host.Content);
        Assert.Equal(SatelliteDockManager.PanelMode.Docked, h.Manager.GetMode(h.Panel.Id));
        h.Cleanup();
    }

    [AvaloniaFact]
    public void SetModeSatellite_RemovesPanelFromHostAndCreatesSatellite()
    {
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);

        Assert.Equal(SatelliteDockManager.PanelMode.Satellite, h.Manager.GetMode(h.Panel.Id));
        Assert.Null(h.Host.Content);
        Assert.Single(h.Manager.ActiveSatellites);
        var sat = h.Manager.ActiveSatellites[h.Panel.Id];
        Assert.Same(h.Panel.Content, sat.Content);
        Assert.Equal(WindowRole.Satellite, sat.Role);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void SatelliteToDocked_ReparentsPanelAndClosesSatellite()
    {
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);
        var sat = h.Manager.ActiveSatellites[h.Panel.Id];

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Docked);

        Assert.Equal(SatelliteDockManager.PanelMode.Docked, h.Manager.GetMode(h.Panel.Id));
        Assert.Same(h.Panel.Content, h.Host.Content);
        Assert.Empty(h.Manager.ActiveSatellites);
        Assert.False(sat.IsVisible);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void FloatingToDocked_ReparentsAndClosesFloatingWindow()
    {
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Floating);
        var floating = h.Manager.ActiveSatellites[h.Panel.Id];
        Assert.Equal(WindowRole.Floating, floating.Role);

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Docked);

        Assert.Same(h.Panel.Content, h.Host.Content);
        Assert.Empty(h.Manager.ActiveSatellites);
        Assert.False(floating.IsVisible);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void RepeatedDockSatelliteCycles_RemainStable()
    {
        var h = TestHelpers.BuildHost();
        for (int i = 0; i < 4; i++)
        {
            h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);
            Assert.Single(h.Manager.ActiveSatellites);
            h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Docked);
            Assert.Same(h.Panel.Content, h.Host.Content);
            Assert.Empty(h.Manager.ActiveSatellites);
        }
        h.Cleanup();
    }

    [AvaloniaFact]
    public void RepeatedDockFloatingCycles_RemainStable()
    {
        var h = TestHelpers.BuildHost();
        for (int i = 0; i < 4; i++)
        {
            h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Floating);
            Assert.Single(h.Manager.ActiveSatellites);
            h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Docked);
            Assert.Same(h.Panel.Content, h.Host.Content);
            Assert.Empty(h.Manager.ActiveSatellites);
        }
        h.Cleanup();
    }

    [AvaloniaFact]
    public void DockBack_StillClosesSatellite_WhenAlreadyAutoDetachedFromSnapChain()
    {
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);
        var sat = h.Manager.ActiveSatellites[h.Panel.Id];

        // Simulate the snap-chain auto-detach: drop the satellite out of the
        // manager's attachment tree without closing it.
        h.Manager.SatelliteManager.Detach(sat, closeSatellite: false);

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Docked);

        Assert.Same(h.Panel.Content, h.Host.Content);
        Assert.False(sat.IsVisible);
        h.Cleanup();
    }
}
