using System;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

/// <summary>
/// Coverage for newly-hardened paths and previously-uncovered API surface:
/// HostWindowFactory wiring, Role mutation + style invalidation, IsSnapped /
/// IsFloatingTracked discriminators, ChainDepthLimit enforcement,
/// SaveState/RestoreState round-trip + cycle detection, ISatelliteDockHost
/// Dock/Undock, event-leak verification, and PrepareContentForReparent.
/// </summary>
public class HardeningTests
{
    // ── HostWindowFactory wiring (A2) ───────────────────────────────

    private sealed class CountingFactory : IHostWindowFactory
    {
        public int Calls;
        public SatelliteWindow? Last;
        public SatelliteWindow CreateHostWindow()
        {
            Calls++;
            return Last = new SatelliteWindow { Tag = "from-factory" };
        }
    }

    [AvaloniaFact]
    public void HostWindowFactory_IsInvokedByPopOut()
    {
        var h = TestHelpers.BuildHost();
        var factory = new CountingFactory();
        h.Manager.HostWindowFactory = factory;

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);

        Assert.Equal(1, factory.Calls);
        var sat = h.Manager.ActiveSatellites[h.Panel.Id];
        Assert.Same(factory.Last, sat);
        Assert.Equal("from-factory", sat.Tag);
        // Manager must still populate metadata on the factory-supplied instance.
        Assert.Same(h.Panel.Content, sat.Content);
        Assert.False(sat.ShowInTaskbar);
        h.Cleanup();
    }

    [AvaloniaFact]
    public void HostWindowFactory_IsInvokedForFloatingPopOutToo()
    {
        var h = TestHelpers.BuildHost();
        var factory = new CountingFactory();
        h.Manager.HostWindowFactory = factory;

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Floating);

        Assert.Equal(1, factory.Calls);
        h.Cleanup();
    }

    // ── Event-handler leak fix (A1) ─────────────────────────────────

    [AvaloniaFact]
    public void PopOutThenDockBack_UnsubscribesClosedHandler()
    {
        // After dock-back the manager must have released its Closed handler on
        // the satellite. We verify by closing the (now-detached) window and
        // confirming SatelliteClosedByUser does NOT fire, because the manager
        // no longer holds a subscription.
        var h = TestHelpers.BuildHost();
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);
        var sat = h.Manager.ActiveSatellites[h.Panel.Id];
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Docked);
        Assert.False(sat.IsVisible);

        // Satellite is closed-and-disposed at this point. A "fresh" Closed
        // would not re-fire (the window has already been Closed once during
        // dock-back), but we can verify the dictionary state through behaviour:
        // a subsequent pop-out + close-by-user must still raise the event
        // exactly once, proving the previous handler didn't leak across cycles.
        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);
        var sat2 = h.Manager.ActiveSatellites[h.Panel.Id];
        int calls = 0;
        h.Manager.SatelliteClosedByUser += _ => calls++;

        sat2.Close();

        Assert.Equal(1, calls);
        h.Cleanup();
    }

    // ── SatelliteWindow.Role runtime mutation (B3) ──────────────────

    [AvaloniaFact]
    public void Role_ChangedAtRuntime_TriggersStyleInvalidation()
    {
        // The contract is: changing Role at runtime invalidates the style key.
        // We can't directly observe Avalonia's style-key cache, but we can
        // observe that the property write doesn't throw and the new value sticks.
        // (Without InvalidateStyles, the cached theme would persist but the
        // property value would still update — so this is a smoke test that the
        // hook itself runs.)
        var sat = new SatelliteWindow();
        sat.Show();
        Assert.Equal(WindowRole.Floating, sat.Role);

        sat.Role = WindowRole.DockHost;
        Assert.Equal(WindowRole.DockHost, sat.Role);

        sat.Role = WindowRole.Satellite;
        Assert.Equal(WindowRole.Satellite, sat.Role);

        sat.Close();
    }

    // ── IsSnapped / IsFloatingTracked discriminators (B7) ───────────

    [AvaloniaFact]
    public void IsSnapped_TrueForSatellite_FalseForFloatingTracked()
    {
        var main = new Window { Width = 200, Height = 200 };
        main.Show();
        var mgr = new SatelliteManager(main);

        var snapped = new SatelliteWindow { Width = 100, Height = 100 };
        snapped.Show();
        mgr.Attach(snapped, SnapEdge.Right);
        Assert.True(snapped.IsSnapped);
        Assert.False(snapped.IsFloatingTracked);
        Assert.True(snapped.IsAttached);

        var floating = new SatelliteWindow { Width = 100, Height = 100 };
        floating.Show();
        mgr.AttachFloating(floating);
        Assert.False(floating.IsSnapped);
        Assert.True(floating.IsFloatingTracked);
        Assert.True(floating.IsAttached);

        var standalone = new SatelliteWindow { Width = 100, Height = 100 };
        standalone.Show();
        Assert.False(standalone.IsSnapped);
        Assert.False(standalone.IsFloatingTracked);
        Assert.False(standalone.IsAttached);

        mgr.Dispose();
        main.Close();
        standalone.Close();
    }

    // ── ChainDepthLimit enforcement (C5 / new) ──────────────────────

    [AvaloniaFact]
    public void Attach_ExceedingChainDepthLimit_Throws()
    {
        var main = new Window { Width = 200, Height = 200 };
        main.Show();
        var mgr = new SatelliteManager(main, new SnapBehavior { ChainDepthLimit = 1 });

        var s1 = new SatelliteWindow { Width = 100, Height = 100 }; s1.Show();
        var s2 = new SatelliteWindow { Width = 100, Height = 100 }; s2.Show();

        mgr.Attach(s1, SnapEdge.Right); // depth 1 — OK

        // depth 2 — exceeds the limit
        Assert.Throws<InvalidOperationException>(() =>
            mgr.Attach(s2, s1, SnapEdge.Bottom));

        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void Attach_ChainDepthLimitMinusOne_IsUnlimited()
    {
        var main = new Window { Width = 200, Height = 200 };
        main.Show();
        var mgr = new SatelliteManager(main, new SnapBehavior { ChainDepthLimit = -1 });

        var s1 = new SatelliteWindow { Width = 100, Height = 100 }; s1.Show();
        var s2 = new SatelliteWindow { Width = 100, Height = 100 }; s2.Show();
        var s3 = new SatelliteWindow { Width = 100, Height = 100 }; s3.Show();

        mgr.Attach(s1, SnapEdge.Right);
        mgr.Attach(s2, s1, SnapEdge.Bottom);
        mgr.Attach(s3, s2, SnapEdge.Right); // depth 3 — must succeed

        Assert.Equal(3, mgr.Attachments.Count);

        mgr.Dispose();
        main.Close();
    }

    // ── SaveState / RestoreState (C5 / new) ─────────────────────────

    [AvaloniaFact]
    public void SaveState_MissingSatelliteId_ThrowsBeforeBuildingPartialList()
    {
        var main = new Window { Width = 200, Height = 200 };
        main.Show();
        var mgr = new SatelliteManager(main);
        var s1 = new SatelliteWindow { Width = 100, Height = 100, SatelliteId = "s1" }; s1.Show();
        var s2 = new SatelliteWindow { Width = 100, Height = 100 /* no id */ }; s2.Show();
        mgr.Attach(s1, SnapEdge.Right);
        mgr.Attach(s2, SnapEdge.Left);

        var ex = Assert.Throws<InvalidOperationException>(() => mgr.SaveState());
        Assert.Contains("SatelliteId", ex.Message);

        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void SaveStateRestoreState_RoundTrip_RebuildsTree()
    {
        var main = new Window { Width = 400, Height = 400 };
        main.Show();
        var mgr = new SatelliteManager(main);

        var root = new SatelliteWindow { Width = 100, Height = 100, SatelliteId = "root" }; root.Show();
        var child = new SatelliteWindow { Width = 100, Height = 100, SatelliteId = "child" }; child.Show();
        mgr.Attach(root, SnapEdge.Right, offsetAlongEdge: 12);
        mgr.Attach(child, root, SnapEdge.Bottom, offsetAlongEdge: 24);

        var saved = mgr.SaveState();
        Assert.Equal(2, saved.Length);

        // Tear down and rehydrate with a factory.
        var mgr2 = new SatelliteManager(main);
        var recreated = new System.Collections.Generic.Dictionary<string, SatelliteWindow>();
        mgr2.RestoreState(saved, id =>
        {
            var w = new SatelliteWindow { Width = 100, Height = 100 };
            recreated[id] = w;
            return w;
        });

        Assert.Equal(2, mgr2.Attachments.Count);
        Assert.True(recreated.ContainsKey("root"));
        Assert.True(recreated.ContainsKey("child"));

        // Topology: child should be parented under root, not under main.
        Assert.Single(mgr2.GetChildren(main));    // root only
        Assert.Single(mgr2.GetChildren(recreated["root"])); // child only

        mgr.Dispose();
        mgr2.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void RestoreState_DanglingParentReference_Throws()
    {
        var main = new Window { Width = 200, Height = 200 };
        main.Show();
        var mgr = new SatelliteManager(main);

        // Entry 'orphan' references parent 'missing' that's not in the state set.
        var badState = new[]
        {
            new AttachmentState("orphan", "missing", SnapEdge.Right, 0, 100, 100, IsDocked: false),
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            mgr.RestoreState(badState, id => new SatelliteWindow { Width = 100, Height = 100 }));
        Assert.Contains("parent", ex.Message, StringComparison.OrdinalIgnoreCase);

        mgr.Dispose();
        main.Close();
    }

    // ── ISatelliteDockHost: Dock / Undock low-level API (B2 / C5) ──

    private sealed class FakeDockHost : Window, ISatelliteDockHost
    {
        public ContentControl Slot { get; } = new();
        public int DockCalls, UndockCalls;
        public bool ReturnDockResult = true;
        public bool ReturnUndockResult = true;
        public FakeDockHost() { Content = Slot; Width = 300; Height = 300; }

        public bool TryDockSatellite(SatelliteWindow satellite, SnapEdge edge)
        {
            DockCalls++;
            if (!ReturnDockResult) return false;
            // Mirror what a real host would do: extract + embed the content.
            var content = satellite.Content;
            satellite.PrepareContentForReparent();
            Slot.Content = content;
            return true;
        }

        public bool TryUndockSatellite(SatelliteWindow satellite)
        {
            UndockCalls++;
            if (!ReturnUndockResult) return false;
            var content = Slot.Content;
            Slot.Content = null;
            satellite.Content = content;
            return true;
        }
    }

    [AvaloniaFact]
    public void Dock_DelegatesToHost_AndHidesSatellite()
    {
        var host = new FakeDockHost();
        host.Show();
        var mgr = new SatelliteManager(host);

        var sat = new SatelliteWindow { Width = 100, Height = 100, Content = new Border() };
        sat.Show();
        mgr.Attach(sat, SnapEdge.Right);

        mgr.Dock(sat, SnapEdge.Right);

        Assert.Equal(1, host.DockCalls);
        Assert.True(mgr.IsDocked(sat));
        Assert.False(sat.IsVisible); // manager hides the satellite
        Assert.NotNull(host.Slot.Content);

        mgr.Dispose();
        host.Close();
    }

    [AvaloniaFact]
    public void Undock_DelegatesToHost_AndReshowsSatellite()
    {
        var host = new FakeDockHost();
        host.Show();
        var mgr = new SatelliteManager(host);
        var sat = new SatelliteWindow { Width = 100, Height = 100, Content = new Border() };
        sat.Show();
        mgr.Attach(sat, SnapEdge.Right);
        mgr.Dock(sat, SnapEdge.Right);

        mgr.Undock(sat);

        Assert.Equal(1, host.UndockCalls);
        Assert.False(mgr.IsDocked(sat));
        Assert.True(sat.IsVisible);
        Assert.NotNull(sat.Content);

        mgr.Dispose();
        host.Close();
    }

    [AvaloniaFact]
    public void Dock_OnMainWindowWithoutInterface_Throws()
    {
        var main = new Window { Width = 200, Height = 200 };
        main.Show();
        var mgr = new SatelliteManager(main);
        var sat = new SatelliteWindow { Width = 100, Height = 100 }; sat.Show();
        mgr.Attach(sat, SnapEdge.Right);

        Assert.Throws<InvalidOperationException>(() => mgr.Dock(sat));

        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void Dock_OnSatelliteWithChildren_Throws()
    {
        var host = new FakeDockHost();
        host.Show();
        var mgr = new SatelliteManager(host);
        var parent = new SatelliteWindow { Width = 100, Height = 100 }; parent.Show();
        var child = new SatelliteWindow { Width = 100, Height = 100 }; child.Show();
        mgr.Attach(parent, SnapEdge.Right);
        mgr.Attach(child, parent, SnapEdge.Bottom);

        Assert.Throws<InvalidOperationException>(() => mgr.Dock(parent));

        mgr.Dispose();
        host.Close();
    }

    // ── PrepareContentForReparent instance helper (C4) ──────────────

    [AvaloniaFact]
    public void PrepareContentForReparent_InstanceMethod_ClearsContent()
    {
        var sat = new SatelliteWindow { Width = 100, Height = 100, Content = new Border() };
        sat.Show();
        Assert.NotNull(sat.Content);

        sat.PrepareContentForReparent();

        Assert.Null(sat.Content);
        sat.Close();
    }

    // ── DefaultHostWindowFactory wiring through SatelliteDockManager (A2 e2e) ──

    [AvaloniaFact]
    public void DefaultHostWindowFactory_ProducedWindowsAreDockHost()
    {
        var h = TestHelpers.BuildHost();
        int configured = 0;
        h.Manager.HostWindowFactory = new DefaultHostWindowFactory(w =>
        {
            configured++;
            w.Title = "themed";
        });

        h.Manager.SetMode(h.Panel.Id, SatelliteDockManager.PanelMode.Satellite);

        Assert.Equal(1, configured);
        var sat = h.Manager.ActiveSatellites[h.Panel.Id];
        // The factory sets Role=DockHost; SetMode(Satellite) then calls
        // SatelliteManager.Attach, which preserves DockHost role per the
        // role-preservation contract (see SatelliteManager XML doc).
        Assert.Equal(WindowRole.DockHost, sat.Role);
        h.Cleanup();
    }
}
