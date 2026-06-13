using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

/// <summary>
/// Tests for the high-level <see cref="SatelliteDockManager"/> facade:
/// mode transitions, toggles, events, multi-panel scenarios, and the
/// <c>HostWindowFactory</c> hook.
/// </summary>
public class SatelliteDockManagerTests
{
    // ── Hidden mode ──────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SetModeHidden_ClearsDockSlot()
    {
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Hidden);

        Assert.Equal(SatelliteDockManager.PanelMode.Hidden, h.Manager.GetMode(h.Panel.Id));
        Assert.Null(h.Host.Content);
        Assert.Empty(h.Manager.ActiveSatellites);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void SetMode_NoOpWhenAlreadyInTargetMode()
    {
        var h = TestHelpers.BuildHost();
        // Already docked from BuildHost — re-setting must be a no-op (and idempotent).
        Assert.True(h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Docked));
        Assert.Same(h.Panel.Content, h.Host.Content);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void HiddenToSatellite_CreatesSatelliteWithoutTouchingDockSlot()
    {
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Hidden);
        Assert.Null(h.Host.Content);

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);

        Assert.Single(h.Manager.ActiveSatellites);
        Assert.Null(h.Host.Content); // host stays empty
        h.Cleanup();
    }

    // ── Toggle helpers ──────────────────────────────────────────────────

    [AvaloniaFact]
    public void Toggle_HiddenDockedHiddenCycle()
    {
        var h = TestHelpers.BuildHost();
        // Starting state: Docked. Toggle goes Docked → Hidden → Docked.
        Assert.True(h.Manager.Toggle(h.Panel.Id));
        Assert.Equal(SatelliteDockManager.PanelMode.Hidden, h.Manager.GetMode(h.Panel.Id));
        Assert.True(h.Manager.Toggle(h.Panel.Id));
        Assert.Equal(SatelliteDockManager.PanelMode.Docked, h.Manager.GetMode(h.Panel.Id));
        h.Cleanup();
    }

    [AvaloniaFact]
    public void Toggle_FromSatelliteReturnsToDocked()
    {
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);

        Assert.True(h.Manager.Toggle(h.Panel.Id));

        Assert.Equal(SatelliteDockManager.PanelMode.Docked, h.Manager.GetMode(h.Panel.Id));
        Assert.Empty(h.Manager.ActiveSatellites);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void ToggleSatellite_AlternatesDockedAndSatellite()
    {
        var h = TestHelpers.BuildHost();

        Assert.True(h.Manager.ToggleSatellite(h.Panel.Id));
        Assert.Equal(SatelliteDockManager.PanelMode.Satellite, h.Manager.GetMode(h.Panel.Id));

        Assert.True(h.Manager.ToggleSatellite(h.Panel.Id));
        Assert.Equal(SatelliteDockManager.PanelMode.Docked, h.Manager.GetMode(h.Panel.Id));
        h.Cleanup();
    }

    [AvaloniaFact]
    public void ToggleFloating_AlternatesDockedAndFloating()
    {
        var h = TestHelpers.BuildHost();

        Assert.True(h.Manager.ToggleFloating(h.Panel.Id));
        Assert.Equal(SatelliteDockManager.PanelMode.Floating, h.Manager.GetMode(h.Panel.Id));

        Assert.True(h.Manager.ToggleFloating(h.Panel.Id));
        Assert.Equal(SatelliteDockManager.PanelMode.Docked, h.Manager.GetMode(h.Panel.Id));
        h.Cleanup();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void IsSatellite_AndIsFloating_ReflectCurrentMode()
    {
        var h = TestHelpers.BuildHost();
        Assert.False(h.Manager.IsSatellite(h.Panel.Id));
        Assert.False(h.Manager.IsFloating(h.Panel.Id));

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);
        Assert.True(h.Manager.IsSatellite(h.Panel.Id));
        Assert.False(h.Manager.IsFloating(h.Panel.Id));

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Floating);
        Assert.False(h.Manager.IsSatellite(h.Panel.Id));
        Assert.True(h.Manager.IsFloating(h.Panel.Id));
        h.Cleanup();
    }

    [AvaloniaFact]
    public void GetMode_UnknownPanel_ReturnsHidden()
    {
        var h = TestHelpers.BuildHost();
        Assert.Equal(SatelliteDockManager.PanelMode.Hidden,
            h.Manager.GetMode("does-not-exist"));
        h.Cleanup();
    }

    [AvaloniaFact]
    public void ActiveSatellites_KeyedByPanelId()
    {
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);

        Assert.Contains(h.Panel.Id, h.Manager.ActiveSatellites.Keys);
        h.Cleanup();
    }

    // ── Events ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SatelliteCreated_FiresOnPopOut()
    {
        var h = TestHelpers.BuildHost();
        SatelliteWindow? created = null;
        ISatellitePanel? createdPanel = null;
        h.Manager.SatelliteCreated += (w, p) => { created = w; createdPanel = p; };

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);

        Assert.NotNull(created);
        Assert.Same(h.Panel, createdPanel);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void SatelliteCreated_FiresForFloatingPopOutToo()
    {
        var h = TestHelpers.BuildHost();
        int calls = 0;
        h.Manager.SatelliteCreated += (_, _) => calls++;

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Floating);

        Assert.Equal(1, calls);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void SatelliteClosedByUser_FiresWhenUserClosesSatellite()
    {
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);
        var sat = h.Manager.ActiveSatellites[h.Panel.Id];
        string? closedId = null;
        h.Manager.SatelliteClosedByUser += id => closedId = id;

        sat.Close(); // user-initiated close (not a dock-back)

        Assert.Equal(h.Panel.Id, closedId);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void SatelliteClosedByUser_DoesNotFireOnDockBack()
    {
        // Dock-back closes the satellite internally — the event must not fire,
        // otherwise hosts would think the user dismissed the panel.
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);
        int calls = 0;
        h.Manager.SatelliteClosedByUser += _ => calls++;

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Docked);

        Assert.Equal(0, calls);
        h.Cleanup();
    }

    // ── HostWindowFactory hook ───────────────────────────────────────────

    private sealed class StubFactory : IHostWindowFactory
    {
        public int CreateCalls;
        public SatelliteWindow CreateHostWindow()
        {
            CreateCalls++;
            return new SatelliteWindow { Width = 123, Height = 234 };
        }
    }

    [AvaloniaFact]
    public void HostWindowFactory_DefaultsToNull()
    {
        // Null = SatelliteDockManager constructs plain SatelliteWindow instances
        // internally. Hosts opt into a factory (e.g. DefaultHostWindowFactory or
        // their own) only when they need to customise the satellite chrome.
        var h = TestHelpers.BuildHost();
        Assert.Null(h.Manager.HostWindowFactory);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void HostWindowFactory_CanBeReplaced()
    {
        var h = TestHelpers.BuildHost();
        var stub = new StubFactory();
        h.Manager.HostWindowFactory = stub;

        Assert.Same(stub, h.Manager.HostWindowFactory);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void DefaultHostWindowFactory_CreatesDockHostRoleWindow()
    {
        var f = new DefaultHostWindowFactory();
        var w = f.CreateHostWindow();
        Assert.Equal(WindowRole.DockHost, w.Role);
        w.Close();
    }

    [AvaloniaFact]
    public void DefaultHostWindowFactory_InvokesConfigureCallback()
    {
        int callbackInvocations = 0;
        var f = new DefaultHostWindowFactory(w => { callbackInvocations++; w.Title = "configured"; });
        var w = f.CreateHostWindow();
        Assert.Equal(1, callbackInvocations);
        Assert.Equal("configured", w.Title);
        w.Close();
    }

    // ── Multi-panel scenarios ───────────────────────────────────────────

    [AvaloniaFact]
    public void TwoPanels_IndependentMode()
    {
        var hostA = new ContentControl();
        var hostB = new ContentControl();
        var panelA = new TestHelpers.FakePanel(new Border(), "a");
        var panelB = new TestHelpers.FakePanel(new Border(), "b");
        var bridge = new TestHelpers.FakeBridge();
        bridge.Register(panelA, hostA);
        bridge.Register(panelB, hostB);
        var stack = new StackPanel();
        stack.Children.Add(hostA);
        stack.Children.Add(hostB);
        var main = new Window { Width = 400, Height = 300, Content = stack };
        main.Show();
        var mgr = new SatelliteDockManager(main, bridge);

        mgr.SetMode("a", SatelliteDockManager.PanelMode.Docked);
        mgr.SetMode("b", SatelliteDockManager.PanelMode.Satellite);

        Assert.Same(panelA.Content, hostA.Content);
        Assert.Null(hostB.Content);
        Assert.Single(mgr.ActiveSatellites);
        Assert.Equal("b", System.Linq.Enumerable.First(mgr.ActiveSatellites.Keys));

        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void TwoPanels_BothPoppedOut_BothClosedOnDockBack()
    {
        var hostA = new ContentControl();
        var hostB = new ContentControl();
        var panelA = new TestHelpers.FakePanel(new Border(), "a");
        var panelB = new TestHelpers.FakePanel(new Border(), "b");
        var bridge = new TestHelpers.FakeBridge();
        bridge.Register(panelA, hostA);
        bridge.Register(panelB, hostB);
        var stack = new StackPanel();
        stack.Children.Add(hostA);
        stack.Children.Add(hostB);
        var main = new Window { Width = 400, Height = 300, Content = stack };
        main.Show();
        var mgr = new SatelliteDockManager(main, bridge);
        mgr.SetMode("a", SatelliteDockManager.PanelMode.Docked);
        mgr.SetMode("b", SatelliteDockManager.PanelMode.Docked);
        mgr.SetMode("a", SatelliteDockManager.PanelMode.Satellite);
        mgr.SetMode("b", SatelliteDockManager.PanelMode.Floating);
        var satA = mgr.ActiveSatellites["a"];
        var satB = mgr.ActiveSatellites["b"];

        mgr.SetMode("a", SatelliteDockManager.PanelMode.Docked);
        mgr.SetMode("b", SatelliteDockManager.PanelMode.Docked);

        Assert.False(satA.IsVisible);
        Assert.False(satB.IsVisible);
        Assert.Empty(mgr.ActiveSatellites);
        Assert.Same(panelA.Content, hostA.Content);
        Assert.Same(panelB.Content, hostB.Content);

        mgr.Dispose();
        main.Close();
    }
}
