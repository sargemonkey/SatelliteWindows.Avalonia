using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;

namespace SatelliteWindows.Avalonia;

/// <summary>
/// Single owner of a panel's lifecycle and placement across two surfaces:
/// inside the main window (docked) and outside (satellite window).
///
/// <para>
/// Each panel has a single <see cref="PanelMode"/> at any moment —
/// <see cref="PanelMode.Hidden"/>, <see cref="PanelMode.Docked"/>, or
/// <see cref="PanelMode.Satellite"/> — and all transitions route through
/// <see cref="SetMode"/>.
/// </para>
///
/// <para>
/// Inside: delegates to the host-provided <see cref="ISatelliteDockBridge"/>
/// (which talks to Dock.Avalonia, AvalonDock, or a custom layout).
/// Outside: creates a <see cref="SatelliteWindow"/> and delegates to
/// <see cref="SatelliteManager"/> for magnetic edge snap, follow-on-resize,
/// minimize-with-main, and close-with-main.
/// </para>
///
/// <para>
/// The manager is host-agnostic: it knows nothing about the host's tool
/// classes, theming, or plugins. App-specific styling on satellite creation
/// can be applied via <see cref="SatelliteCreated"/>.
/// </para>
/// </summary>
public sealed class SatelliteDockManager : IDisposable
{
    /// <summary>Where a panel currently lives.</summary>
    public enum PanelMode
    {
        /// <summary>Not visible anywhere.</summary>
        Hidden,
        /// <summary>Visible inside the main window via the dock bridge.</summary>
        Docked,
        /// <summary>Visible outside the main window as a standalone (non-snapping) window.</summary>
        Floating,
        /// <summary>Visible outside the main window as a snapped satellite.</summary>
        Satellite,
    }

    private readonly Window _mainWindow;
    private readonly ISatelliteDockBridge _bridge;
    private readonly SatelliteManager _satelliteManager;
    private readonly Dictionary<string, SatelliteEntry> _externals = new();

    /// <summary>
    /// Per-panel <c>Closed</c> handlers attached to each popped-out satellite,
    /// so we can unsubscribe on programmatic dock-back / float-close instead of
    /// leaking the closure (and the SatelliteDockManager via it) for the
    /// satellite's whole lifetime.
    /// </summary>
    private readonly Dictionary<string, EventHandler> _closedHandlers = new();

    /// <summary>
    /// True while <see cref="DockBackInternal"/> or <see cref="CloseFloatingInternal"/>
    /// is programmatically closing a satellite. Used by <see cref="OnSatelliteClosed"/>
    /// to suppress <see cref="SatelliteClosedByUser"/> firing — otherwise hosts
    /// would think the user dismissed the panel every time the manager docked
    /// it back into the dock slot.
    /// </summary>
    private bool _isDockingBack;

    private sealed record SatelliteEntry(
        SatelliteWindow Window,
        ISatellitePanel Panel,
        SnapEdge Edge,
        PanelMode Mode);

    /// <summary>
    /// Fired when a satellite is closed via the OS close button (X). Listeners
    /// should typically re-insert the panel into its dock so the user doesn't
    /// lose visibility of it.
    /// </summary>
    public event Action<string>? SatelliteClosedByUser;

    /// <summary>
    /// Optional callback invoked after a satellite window is created and attached.
    /// Use for host-specific styling — background brush, title-bar DWM theming, etc.
    /// </summary>
    public Action<SatelliteWindow, ISatellitePanel>? SatelliteCreated { get; set; }

    /// <summary>
    /// Optional factory invoked when popping a panel out into a new window.
    /// When <c>null</c> the manager constructs a plain <see cref="SatelliteWindow"/>
    /// directly. Assign a factory (typically <see cref="DefaultHostWindowFactory"/>
    /// or your own implementation) to customise chrome, theming, or window
    /// behaviour for every popped-out window.
    /// </summary>
    /// <remarks>
    /// The factory is invoked for both <see cref="PanelMode.Satellite"/> and
    /// <see cref="PanelMode.Floating"/> pop-outs. The returned window has its
    /// <see cref="Window.Title"/>, <see cref="Layoutable.Width"/>,
    /// <see cref="Layoutable.Height"/>, <see cref="ContentControl.Content"/>,
    /// and <see cref="Window.ShowInTaskbar"/> populated from the
    /// <see cref="ISatellitePanel"/> descriptor before
    /// <see cref="SatelliteCreated"/> fires.
    /// </remarks>
    public IHostWindowFactory? HostWindowFactory { get; set; }

    /// <summary>
    /// The underlying <see cref="SatelliteManager"/> — exposed for advanced
    /// consumers that need to inspect attachments or tweak snap behaviour at runtime.
    /// </summary>
    public SatelliteManager SatelliteManager => _satelliteManager;

    public SatelliteDockManager(
        Window mainWindow,
        ISatelliteDockBridge bridge,
        SnapBehavior? behavior = null)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _satelliteManager = new SatelliteManager(mainWindow, behavior ?? DefaultBehavior());
    }

    private static SnapBehavior DefaultBehavior() => new()
    {
        FollowOnResize = true,
        MinimizeWithMain = true,
        CloseWithMain = true,
        MinVisiblePixels = 50,
        AutoSnapOnDrag = true,
        AutoDetachOnDrag = true,
        MagneticThresholdPx = 40,
        DetachThresholdPx = 30,
    };

    // ── Mode queries / transitions ──────────────────────────────────

    /// <summary>True iff the panel is currently popped out as a satellite (snapped).</summary>
    public bool IsSatellite(string panelId) =>
        _externals.TryGetValue(panelId, out var e) && e.Mode == PanelMode.Satellite;

    /// <summary>True iff the panel is currently popped out as a floating window (no snap).</summary>
    public bool IsFloating(string panelId) =>
        _externals.TryGetValue(panelId, out var e) && e.Mode == PanelMode.Floating;

    /// <summary>Where is this panel right now?</summary>
    public PanelMode GetMode(string panelId)
    {
        if (_externals.TryGetValue(panelId, out var e)) return e.Mode;
        if (_bridge.IsDocked(panelId)) return PanelMode.Docked;
        return PanelMode.Hidden;
    }

    /// <summary>
    /// Move a panel to a target mode. Idempotent: returns true if the panel is
    /// already in the target mode. Otherwise leaves the current mode then enters
    /// the target. Returns whether the entering step succeeded.
    /// </summary>
    public bool SetMode(string panelId, PanelMode mode)
    {
        var current = GetMode(panelId);
        if (current == mode) return true;

        // Leave current mode
        switch (current)
        {
            case PanelMode.Satellite: DockBackInternal(panelId); break;
            case PanelMode.Floating:  CloseFloatingInternal(panelId, reinsert: false); break;
            case PanelMode.Docked:    _bridge.HideFromDock(panelId); break;
        }

        // Enter new mode
        return mode switch
        {
            PanelMode.Docked    => _bridge.ShowDocked(panelId),
            PanelMode.Satellite => PopOutInternal(panelId, PanelMode.Satellite),
            PanelMode.Floating  => PopOutInternal(panelId, PanelMode.Floating),
            PanelMode.Hidden    => true,
            _ => false,
        };
    }

    /// <summary>
    /// Toggle through Hidden → Docked → Hidden (and Satellite → Docked).
    /// Suitable for "show/hide panel" menu items where the docked layout is the default.
    /// </summary>
    public bool Toggle(string panelId) => GetMode(panelId) switch
    {
        PanelMode.Satellite => SetMode(panelId, PanelMode.Docked),
        PanelMode.Docked => SetMode(panelId, PanelMode.Hidden),
        PanelMode.Hidden => SetMode(panelId, PanelMode.Docked),
        _ => false,
    };

    /// <summary>
    /// Toggle through Satellite ↔ Docked. Suitable for "pop out as satellite" /
    /// "dock back" actions, and for plugin-style panels that default to satellite.
    /// </summary>
    public bool ToggleSatellite(string panelId) =>
        GetMode(panelId) == PanelMode.Satellite
            ? SetMode(panelId, PanelMode.Docked)
            : SetMode(panelId, PanelMode.Satellite);

    /// <summary>
    /// Toggle through Floating ↔ Docked. Suitable for "pop out as floating window"
    /// where the user wants a standalone non-snapping window.
    /// </summary>
    public bool ToggleFloating(string panelId) =>
        GetMode(panelId) == PanelMode.Floating
            ? SetMode(panelId, PanelMode.Docked)
            : SetMode(panelId, PanelMode.Floating);

    /// <summary>Snapshot of currently-open external windows (panelId → window).</summary>
    public IReadOnlyDictionary<string, SatelliteWindow> ActiveSatellites =>
        _externals.ToDictionary(kv => kv.Key, kv => kv.Value.Window);

    // ── Internal: pop-out / dock-back ───────────────────────────────

    private bool PopOutInternal(string panelId, PanelMode mode)
    {
        var panel = _bridge.ExtractForSatellite(panelId);
        if (panel is null) return false;

        var edge = panel.DefaultSnapEdge;
        var width = panel.DefaultSatelliteWidth > 0 ? panel.DefaultSatelliteWidth : 350;
        var height = panel.DefaultSatelliteHeight > 0 ? panel.DefaultSatelliteHeight : _mainWindow.Height;

        // Honour HostWindowFactory if the host wired one up; otherwise fall back
        // to a plain SatelliteWindow. The factory hook lets hosts substitute a
        // themed / chromed window class for every pop-out without having to
        // subscribe to SatelliteCreated.
        var satellite = HostWindowFactory?.CreateHostWindow() ?? new SatelliteWindow();
        satellite.Title = panel.Title ?? panelId;
        satellite.Width = width;
        satellite.Height = height;
        satellite.Content = panel.Content;
        satellite.ShowInTaskbar = false;

        _externals[panelId] = new SatelliteEntry(satellite, panel, edge, mode);

        // Store the handler so DockBackInternal / CloseFloatingInternal / Dispose
        // can unsubscribe it — otherwise the closure pins SatelliteDockManager
        // alive for the satellite's whole lifetime.
        EventHandler onClosed = (_, _) => OnSatelliteClosed(panelId);
        satellite.Closed += onClosed;
        _closedHandlers[panelId] = onClosed;

        if (mode == PanelMode.Satellite)
        {
            _satelliteManager.Attach(satellite, edge);
        }
        else
        {
            // Floating: own window, no snap. Lifecycle still tied to the main
            // window through the AttachFloating helper (close-with-main, minimize-with-main).
            satellite.Role = WindowRole.Floating;
            _satelliteManager.AttachFloating(satellite);
        }

        SatelliteCreated?.Invoke(satellite, panel);
        return true;
    }

    /// <summary>
    /// Unsubscribe and forget the Closed handler we registered for a panel in
    /// <see cref="PopOutInternal"/>. Safe to call when the panel has no
    /// recorded handler (e.g. it was never popped out).
    /// </summary>
    private void UnsubscribeClosedHandler(string panelId, SatelliteWindow satellite)
    {
        if (_closedHandlers.Remove(panelId, out var handler))
            satellite.Closed -= handler;
    }

    private bool DockBackInternal(string panelId)
    {
        if (!_externals.TryGetValue(panelId, out var entry)) return false;
        _externals.Remove(panelId);

        var window = entry.Window;
        UnsubscribeClosedHandler(panelId, window);

        // Order matters: flush the satellite's content + layout BEFORE re-inserting
        // the panel into the dock slot, then close the satellite. Otherwise either:
        //  - reinsert sees the panel still parented to the satellite ("already has a
        //    visual parent"), or
        //  - the satellite closes before reinsert and Avalonia raises "InvalidateArrange
        //    on wrong LayoutManager" on the next render tick.
        SatelliteManager.FlushContentForReparent(window);
        var ok = _bridge.ReinsertFromSatellite(panelId);

        _isDockingBack = true;
        try
        {
            if (entry.Mode == PanelMode.Satellite)
                _satelliteManager.Detach(window, closeSatellite: true);
            else
                _satelliteManager.DetachFloating(window, closeSatellite: true);
        }
        finally { _isDockingBack = false; }

        return ok;
    }

    private void CloseFloatingInternal(string panelId, bool reinsert)
    {
        if (!_externals.TryGetValue(panelId, out var entry)) return;
        if (entry.Mode != PanelMode.Floating) return;
        _externals.Remove(panelId);

        var window = entry.Window;
        UnsubscribeClosedHandler(panelId, window);

        if (reinsert)
        {
            SatelliteManager.FlushContentForReparent(window);
            _bridge.ReinsertFromSatellite(panelId);
        }

        _isDockingBack = true;
        try { _satelliteManager.DetachFloating(window, closeSatellite: true); }
        finally { _isDockingBack = false; }
    }

    private void OnSatelliteClosed(string panelId)
    {
        // _isDockingBack: see field doc. Suppresses user-close event when WE
        // closed the satellite as part of dock-back / float-close.
        if (_isDockingBack) return;
        _closedHandlers.Remove(panelId);
        if (_externals.Remove(panelId))
            SatelliteClosedByUser?.Invoke(panelId);
    }

    public void Dispose()
    {
        // Unsubscribe every per-satellite Closed handler before tearing down
        // SatelliteManager; otherwise the closures pin this dock manager alive
        // until each satellite is garbage-collected.
        foreach (var (panelId, entry) in _externals)
        {
            if (_closedHandlers.TryGetValue(panelId, out var h))
                entry.Window.Closed -= h;
        }
        _closedHandlers.Clear();

        _satelliteManager.Dispose();
        _externals.Clear();
    }
}
