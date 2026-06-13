using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

/// <summary>
/// Tests for <see cref="SatelliteManager"/> — the lower-level snap/lifecycle
/// primitive. Covers role transitions, floating tracking, the regression where
/// Detach with closeSatellite:true would skip Close() on already-auto-detached
/// satellites, and DetachAll cleanup.
/// </summary>
public class SatelliteManagerTests
{
    private static (Window main, SatelliteManager mgr) BuildBase()
    {
        var main = new Window { Width = 200, Height = 200 };
        main.Show();
        var mgr = new SatelliteManager(main);
        return (main, mgr);
    }

    // ── WindowRole transitions on Attach / Detach ──────────────────────

    [AvaloniaFact]
    public void Attach_PromotesFloatingToSatellite()
    {
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();
        Assert.Equal(WindowRole.Floating, sat.Role); // default

        mgr.Attach(sat, SnapEdge.Right);

        Assert.Equal(WindowRole.Satellite, sat.Role);
        Assert.Same(mgr, sat.Manager);

        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void Detach_DemotesSatelliteBackToFloating()
    {
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();
        mgr.Attach(sat, SnapEdge.Right);

        mgr.Detach(sat);

        Assert.Equal(WindowRole.Floating, sat.Role);
        Assert.Null(sat.Manager);

        sat.Close();
        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void Attach_ToAlreadyAttachedManager_Throws()
    {
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();
        mgr.Attach(sat, SnapEdge.Right);

        // Re-attaching the same satellite to a different manager should throw.
        var main2 = new Window { Width = 200, Height = 200 };
        main2.Show();
        var mgr2 = new SatelliteManager(main2);
        Assert.Throws<System.InvalidOperationException>(() =>
            mgr2.AttachFloating(sat));

        mgr.Dispose();
        mgr2.Dispose();
        main.Close();
        main2.Close();
    }

    // ── Detach(closeSatellite: true) regression coverage ──────────────

    [AvaloniaFact]
    public void Detach_WithCloseSatelliteTrue_FlushesContentAndClosesWindow()
    {
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();
        var border = new Border();
        sat.Content = border;
        mgr.Attach(sat, SnapEdge.Right);

        mgr.Detach(sat, closeSatellite: true);

        Assert.Null(border.GetVisualParent());
        Assert.False(sat.IsVisible);
        Assert.Equal(WindowRole.Floating, sat.Role);

        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void Detach_WithCloseSatelliteTrue_AfterPriorAutoDetach_StillClosesWindow()
    {
        // The regression: calling Detach(closeSatellite:true) on a satellite
        // that's no longer in _allAttachments (because a prior drag-aware
        // Detach already removed it) used to early-return and leak the window.
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();
        mgr.Attach(sat, SnapEdge.Right);
        mgr.Detach(sat, closeSatellite: false); // simulate drag-out auto-detach
        Assert.True(sat.IsVisible);

        mgr.Detach(sat, closeSatellite: true);

        Assert.False(sat.IsVisible);

        mgr.Dispose();
        main.Close();
    }

    // ── AttachFloating / DetachFloating ────────────────────────────────

    [AvaloniaFact]
    public void AttachFloating_TracksLifecycleWithoutAttachingToSnapTree()
    {
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();

        mgr.AttachFloating(sat);

        Assert.Equal(WindowRole.Floating, sat.Role);
        Assert.Same(mgr, sat.Manager);
        Assert.Empty(mgr.GetChildren(main));

        mgr.DetachFloating(sat, closeSatellite: true);
        Assert.False(sat.IsVisible);
        Assert.Null(sat.Manager);

        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void AttachFloating_OnAlreadyAttachedSatellite_Throws()
    {
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();
        mgr.Attach(sat, SnapEdge.Right);

        Assert.Throws<System.InvalidOperationException>(() =>
            mgr.AttachFloating(sat));

        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void DetachFloating_UnknownSatellite_IsNoOp()
    {
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();

        // Was never AttachFloating'd — must not throw.
        mgr.DetachFloating(sat, closeSatellite: false);

        sat.Close();
        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void FloatingClose_TriggersAttachmentChanged()
    {
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();
        mgr.AttachFloating(sat);

        int events = 0;
        mgr.AttachmentChanged += () => events++;

        sat.Close();

        Assert.True(events >= 1, "AttachmentChanged should fire when the floating-tracked window closes");

        mgr.Dispose();
        main.Close();
    }

    // ── DetachAll covers all three tracking pools ──────────────────────

    [AvaloniaFact]
    public void DetachAll_ClosesSatellitesAndFloatingTracked()
    {
        var (main, mgr) = BuildBase();
        var sat1 = new SatelliteWindow { Width = 100, Height = 100 };
        var sat2 = new SatelliteWindow { Width = 100, Height = 100 };
        var floating = new SatelliteWindow { Width = 100, Height = 100 };
        sat1.Show(); sat2.Show(); floating.Show();

        mgr.Attach(sat1, SnapEdge.Right);
        mgr.Attach(sat2, SnapEdge.Left);
        mgr.AttachFloating(floating);

        mgr.DetachAll();

        Assert.False(sat1.IsVisible);
        Assert.False(sat2.IsVisible);
        Assert.False(floating.IsVisible);

        mgr.Dispose();
        main.Close();
    }

    // ── Chained satellites ─────────────────────────────────────────────

    [AvaloniaFact]
    public void Attach_ChainedSatellite_IsTrackedAsChildOfParent()
    {
        var (main, mgr) = BuildBase();
        var parent = new SatelliteWindow { Width = 100, Height = 100 };
        var child = new SatelliteWindow { Width = 100, Height = 100 };
        parent.Show(); child.Show();

        mgr.Attach(parent, SnapEdge.Right);
        mgr.Attach(child, parent, SnapEdge.Bottom);

        Assert.Single(mgr.GetChildren(main));
        Assert.Single(mgr.GetChildren(parent));
        Assert.Equal(WindowRole.Satellite, parent.Role);
        Assert.Equal(WindowRole.Satellite, child.Role);

        mgr.Dispose();
        main.Close();
    }

    [AvaloniaFact]
    public void DetachAll_DemotesAllSatellitesToFloating()
    {
        var (main, mgr) = BuildBase();
        var s1 = new SatelliteWindow { Width = 100, Height = 100 };
        var s2 = new SatelliteWindow { Width = 100, Height = 100 };
        s1.Show(); s2.Show();
        mgr.Attach(s1, SnapEdge.Right);
        mgr.Attach(s2, SnapEdge.Left);

        mgr.DetachAll();

        Assert.Equal(WindowRole.Floating, s1.Role);
        Assert.Equal(WindowRole.Floating, s2.Role);

        mgr.Dispose();
        main.Close();
    }

    // ── AttachmentChanged firing ───────────────────────────────────────

    [AvaloniaFact]
    public void AttachmentChanged_FiresOnAttachAndDetach()
    {
        var (main, mgr) = BuildBase();
        var sat = new SatelliteWindow { Width = 100, Height = 100 };
        sat.Show();
        int events = 0;
        mgr.AttachmentChanged += () => events++;

        mgr.Attach(sat, SnapEdge.Right);
        Assert.True(events >= 1);

        int afterAttach = events;
        mgr.Detach(sat);
        Assert.True(events > afterAttach);

        sat.Close();
        mgr.Dispose();
        main.Close();
    }
}
