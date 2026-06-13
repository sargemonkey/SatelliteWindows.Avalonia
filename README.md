# SatelliteWindows.Avalonia

Multi-window snapping library for [Avalonia UI](https://avaloniaui.net/) — magnetic satellite windows that attach to edges of a main window and move as one unified surface.

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-purple)]()
[![Avalonia](https://img.shields.io/badge/Avalonia-12.x-blueviolet)]()
[![License](https://img.shields.io/badge/license-MIT-green)]()
[![NuGet](https://img.shields.io/badge/NuGet-1.0.0-blue)]()

```
┌──────────┐┌──────────┐┌──────────┐
│  Left    ││          ││  Right   │
│  Panel   ││   Main   ││  Panel   │
│(attached)││  Window  ││(attached)│
└──────────┘│          │├──────────┤
            └──────────┘│ Chained  │
                        │ (bottom) │
                        └──────────┘
```

## Features

- **Edge snapping** — attach satellite windows to Left, Right, Top, or Bottom edges
- **Snap chaining** — satellites snap to other satellites, forming trees
- **Edge stacking** — multiple satellites per edge, auto-stacked
- **Magnetic re-snap** — drag a detached satellite near any window edge to reattach
- **Edge sliding** — drag snapped satellites along their edge to reposition
- **Drag-away detach** — perpendicular drag detaches; configurable threshold
- **Dual-mode docking** — satellites can dock inside the main window as internal panels via `ISatelliteDockHost`
- **Tri-role panels** — high-level `SatelliteDockManager` moves a panel between `Hidden` / `Docked` / `Floating` / `Satellite` modes through a single `SetMode` API
- **Safe cross-window re-parent** — `SatelliteManager.FlushContentForReparent` (called automatically inside the close paths) handles the Avalonia visual-tree pitfalls when moving the same `Control` between windows
- **Follow-on-move/resize** — satellites reposition in lockstep when parent moves or resizes
- **Minimize/restore sync** — satellites hide and restore with the main window
- **Multi-monitor aware** — per-screen clamping handles L-shaped layouts correctly
- **DPI-aware** — pixel-space positioning with frame border compensation
- **Persist/restore** — serialize and restore the full attachment tree
- **Snap animation** — visual opacity flash on magnetic re-snap
- **Zero dependencies** beyond Avalonia itself
- **Configurable** — snap behavior, thresholds, and overlap ratios tunable via `SnapBehavior`

## Quick Start

```csharp
using SatelliteWindows.Avalonia;

// In your main window's Opened handler:
var manager = new SatelliteManager(mainWindow);

// Attach a satellite to the main window's right edge
var toolPanel = new SatelliteWindow
{
    Title = "Tools",
    Width = 250,
    Height = 400,
    Content = new TextBlock { Text = "I follow the main window!" }
};
manager.Attach(toolPanel, SnapEdge.Right);

// Chain another satellite to the first one's bottom edge
var details = new SatelliteWindow { Title = "Details", Width = 250, Height = 200 };
manager.Attach(details, toolPanel, SnapEdge.Bottom);

// Dock a satellite inside the main window (requires ISatelliteDockHost)
manager.Dock(toolPanel, SnapEdge.Right);
manager.Undock(toolPanel); // Pop back out as external satellite

// Detach
manager.Detach(toolPanel, closeSatellite: true);
manager.DetachAll();
```

## API Reference

### `SatelliteManager`

The core manager — attach it to your main `Window` to control satellite lifecycle.

```csharp
var manager = new SatelliteManager(mainWindow);                 // default behavior
var manager = new SatelliteManager(mainWindow, new SnapBehavior  // custom behavior
{
    MagneticThresholdPx = 40,
    DetachThresholdPx = 30,
    SnapOverlapRatio = 0.3,
    MinimizeWithMain = true,
    CloseWithMain = true
});
```

| Method | Description |
|--------|-------------|
| `Attach(satellite, edge, offset)` | Attach to the main window's edge (sets `Role = Satellite`) |
| `Attach(satellite, parent, edge, offset)` | Chain to another satellite's edge |
| `AttachFloating(satellite)` | Track lifecycle (close/minimize-with-main) without snap; leaves `Role = Floating` |
| `Detach(satellite, closeSatellite)` | Detach (default: DetachChain). When `closeSatellite: true`, automatically calls `FlushContentForReparent` and closes the window even if the satellite was already auto-detached from the snap chain |
| `Detach(satellite, mode, closeSatellite)` | Detach with explicit `DetachMode` |
| `DetachFloating(satellite, closeSatellite)` | Stop tracking a floating window |
| `DetachAll()` | Close and detach everything (satellites, docked, and floating-tracked) |
| `Dock(satellite, edge)` | Dock inside main window (requires `ISatelliteDockHost`) |
| `Undock(satellite, edge)` | Pop back out as external satellite |
| `GetChildren(parent)` | Get direct children of a window in the tree |
| `IsDocked(satellite)` | Check if a satellite is docked internally |
| `SaveState()` / `RestoreState(...)` | Persist and restore the attachment tree |
| `FlushContentForReparent(satellite)` *(static)* | Null the satellite's `Content` and run a synchronous layout pass. Required before re-parenting the same control into another window — see [Cross-window content re-parent](#cross-window-content-re-parent) |
| `Attachments` | Read-only list of all `SatelliteAttachment`s |
| `AttachmentChanged` | Event fired on any topology change |

### `SatelliteWindow`

A `Window` subclass with snap awareness. `ShowInTaskbar` is `false` by default. Set content, size, and window properties before attaching. Set `SatelliteId` for persist/restore.

### `ISatelliteDockHost`

Implement on your main window to enable dual-mode docking:

```csharp
public class MainWindow : Window, ISatelliteDockHost
{
    public bool TryDockSatellite(SatelliteWindow satellite, SnapEdge edge) { /* embed content */ }
    public bool TryUndockSatellite(SatelliteWindow satellite) { /* extract content */ }
}
```

### `SnapEdge`

```csharp
public enum SnapEdge { Left, Right, Top, Bottom }
```

### `DetachMode`

```csharp
public enum DetachMode { ReparentChildren, DetachChain }
```

### `SnapBehavior`

| Property | Default | Description |
|----------|---------|-------------|
| `MagneticThresholdPx` | `40` | Pixel distance for magnetic snap detection |
| `DetachThresholdPx` | `30` | Perpendicular drag distance to detach |
| `AutoDetachOnDrag` | `true` | Enable drag-away detach |
| `AutoSnapOnDrag` | `true` | Enable magnetic re-snap after detach |
| `SnapOverlapRatio` | `0.3` | Min alignment overlap (0–1) for snap to trigger |
| `FollowOnResize` | `true` | Reposition on parent resize |
| `MinimizeWithMain` | `true` | Hide satellites on minimize |
| `CloseWithMain` | `true` | Close satellites on main close |
| `ChainDepthLimit` | `-1` | Max chain depth (-1 = unlimited) |
| `MinVisiblePixels` | `50` | Min pixels visible on any screen |
| `Enabled` | `true` | Master snap switch — set `false` for floating-tracked windows |

## Window class hierarchy

The library vendors a fork of [Dock.Avalonia](https://github.com/wieslawsoltes/Dock)
(under `src/SatelliteWindows.Avalonia.Dock/`, projects prefixed
`SatelliteWindows.Dock.*`) so it can flip the upstream `sealed class HostWindow`
into a non-sealed base class and unify the window hierarchy:

```
Avalonia.Controls.Window
        ▲
        │
SatelliteWindows.Dock.Avalonia.Controls.HostWindow    ← vendored Dock host window
        ▲
        │
SatelliteWindows.Avalonia.SatelliteWindow             ← snap-aware, carries Role
```

The practical consequence:

- Every `SatelliteWindow` instance **is** a Dock `HostWindow`, so dragged-out
  tabs from a Dock framework `DockControl` land in a snap-aware window that
  the host can later promote to `WindowRole.Satellite` by attaching it to a
  `SatelliteManager`.
- A plain `HostWindow` (constructed directly by Dock framework code that
  hasn't been given a custom factory) is still just an Avalonia `Window` and
  carries no satellite behaviour.
- Hosts that already use Dock don't need a separate adapter — they just
  reference the vendored `SatelliteWindows.Dock.*` packages instead of
  upstream `Dock.*` and replace the Dock factory with one that returns
  `SatelliteWindow` instances.

## Window roles

A `SatelliteWindow` has a `Role` property (`StyledProperty<WindowRole>`) that
selects one of three behaviours. Roles can be switched at runtime and the
library wires the necessary plumbing.

| Role | Snap | Lifecycle owner | Use case |
|------|------|-----------------|----------|
| `WindowRole.Floating` | no | host (or manager via `AttachFloating`) | standalone tool / preview / dock-popout without snap |
| `WindowRole.Satellite` | yes | `SatelliteManager` | edge-snapped panel that follows the main window |
| `WindowRole.DockHost` | no (until promoted) | dock framework via `IHostWindowFactory` | host class for a Dock framework's dragged-out tabs (since `SatelliteWindow` IS a `HostWindow`, the same instance can later be promoted to `Satellite` by attaching it to a manager) |

`SatelliteManager` enforces the rule **Role = `Satellite` iff tracked by a
manager via `Attach`**: `Attach` flips a fresh window from `Floating` →
`Satellite`; `Detach` (or close) flips it back. `AttachFloating` tracks a
window for lifecycle (close-with-main, minimize-with-main) without applying
snap behaviour.

```csharp
var w = new SatelliteWindow();                  // Role = Floating
manager.Attach(w, SnapEdge.Right);              // Role = Satellite
manager.Detach(w);                              // Role = Floating

manager.AttachFloating(new SatelliteWindow());  // tracked but free-floating

// Dock-host replacement (host wires this into its dock framework):
var factory = new DefaultHostWindowFactory(host => host.Background = ...);
var dockHost = factory.CreateHostWindow();      // Role = DockHost
manager.Attach(dockHost, SnapEdge.Left);        // promotes to Satellite
```

`SatelliteDockManager.PanelMode` mirrors the three external modes:
`Hidden`, `Docked`, `Floating`, `Satellite`. Use `ToggleSatellite`,
`ToggleFloating`, or `SetMode` to move panels between them.

## `SatelliteDockManager` — high-level tri-role API

For applications that want a single panel to live in one of `Docked` /
`Floating` / `Satellite` / `Hidden` at a time, `SatelliteDockManager` is the
recommended entry point. It owns the transitions, wires in a
`SatelliteManager` for snap behavior, and delegates the in-window placement to
a host-supplied `ISatelliteDockBridge` (so it stays agnostic of Dock.Avalonia,
AvalonDock, or your custom layout).

```csharp
var bridge  = new MyDockBridge(...);  // ISatelliteDockBridge — host-specific
var manager = new SatelliteDockManager(mainWindow, bridge);

manager.SetMode("tools", SatelliteDockManager.PanelMode.Docked);
manager.SetMode("tools", SatelliteDockManager.PanelMode.Satellite);  // pops out
manager.SetMode("tools", SatelliteDockManager.PanelMode.Floating);   // free window
manager.SetMode("tools", SatelliteDockManager.PanelMode.Hidden);

manager.ToggleSatellite("tools");   // Satellite ↔ Docked
manager.ToggleFloating("tools");    // Floating ↔ Docked
```

Customize the satellite window per pop-out via the `SatelliteCreated` event
or by supplying a `HostWindowFactory` (`IHostWindowFactory`). The
`HostWindowFactory` property defaults to **`null`** — in that case the
manager creates plain `SatelliteWindow` instances directly. To customise the
chrome of every popped-out window, assign your own factory (typically
`DefaultHostWindowFactory`, which returns a `SatelliteWindow` with
`Role = DockHost` and accepts an optional `Action<SatelliteWindow>` for
host-specific styling):

```csharp
manager.HostWindowFactory = new DefaultHostWindowFactory(w => {
    w.Background = Brushes.Black;
    w.SystemDecorations = SystemDecorations.BorderOnly;
});
```

### `ISatelliteDockBridge`

Implement on the host to plug into your dock framework. Every method is
keyed by `panelId`, so the dock manager can drive multiple panels through one
bridge.

| Method | Purpose |
|--------|---------|
| `FindPanel(id)` | Lookup the `ISatellitePanel` descriptor |
| `IsDocked(id)` | Report current dock state |
| `ShowDocked(id)` / `HideFromDock(id)` | Attach/detach the panel content to/from your in-window slot |
| `ExtractForSatellite(id)` | Hand the panel off when popping out |
| `ReinsertFromSatellite(id)` | Re-attach the panel content when docking back |

## Cross-window content re-parent

Avalonia raises `InvalidOperationException` if a `Control` already has a
visual parent when you assign it to a new one, and
`ArgumentException: "Attempt to call InvalidateArrange on wrong LayoutManager"`
on the next render tick if a re-parented control still has a dirty entry in
its old window's layout queue.

`SatelliteManager.FlushContentForReparent(window)` handles both issues:

```csharp
SatelliteManager.FlushContentForReparent(satellite);  // null content + UpdateLayout
host.Content = panelControl;                           // safe re-parent
manager.Detach(satellite, closeSatellite: true);       // close the (now-empty) satellite
```

You only need to call this directly when you want to re-parent the
satellite's content into a new visual parent **before** closing the
satellite. `Detach(closeSatellite: true)`, `DetachFloating(closeSatellite: true)`,
`DetachAll`, and the internal close-subtree paths already invoke it for you,
so raw users of `SatelliteManager` who just want to dispose a satellite get
the safe behaviour by default. `SatelliteDockManager` uses the explicit
ordering (`FlushContentForReparent → ReinsertFromSatellite → Detach`)
internally as part of its dock-back flow.

## Project Structure

```
SatelliteWindows.Avalonia/
├── src/
│   ├── SatelliteWindows.Avalonia/       # Core library (net8.0 + net10.0)
│   │   ├── SatelliteManager.cs          # Main manager — tree model, positioning, snap/detach
│   │   ├── SatelliteDockManager.cs      # High-level tri-role facade (Docked/Floating/Satellite/Hidden)
│   │   ├── SatelliteWindow.cs           # Inherits HostWindow (vendored Dock); snap-aware, carries Role
│   │   ├── SatelliteAttachment.cs       # Attachment descriptor (edge, offset, parent)
│   │   ├── WindowRole.cs                # Floating / Satellite / DockHost enum
│   │   ├── SnapEdge.cs                  # Edge enum
│   │   ├── SnapBehavior.cs              # Configuration (incl. master Enabled switch)
│   │   ├── DetachMode.cs                # Detach chain handling
│   │   ├── AttachmentState.cs           # Persist/restore record
│   │   ├── ISatelliteDockHost.cs        # Dual-mode docking (in-window slot for a SatelliteWindow)
│   │   ├── ISatelliteDockBridge.cs      # Host plug-in for SatelliteDockManager
│   │   ├── ISatellitePanel.cs           # Panel descriptor (id, title, content, default edge/size)
│   │   ├── IHostWindowFactory.cs        # Factory + DefaultHostWindowFactory for DockHost windows
│   │   └── Internal/
│   │       ├── PositionCalculator.cs    # Geometry math
│   │       ├── ScreenHelper.cs          # Multi-monitor clamping
│   │       └── DpiHelper.cs             # DIP↔pixel conversion
│   └── SatelliteWindows.Avalonia.Dock/  # Vendored Dock.Avalonia (12 projects, prefix SatelliteWindows.Dock.*)
│       └── src/                          # ↳ HostWindow unsealed so SatelliteWindow can inherit from it
├── samples/
│   └── SatelliteDemo/                   # Drag-driven tri-role demo (grip → satellite → re-dock)
├── tests/
│   └── SatelliteWindows.Avalonia.Tests/ # Unit + headless tests (97 tests, xunit v3 + Avalonia.Headless)
└── SatelliteWindows.slnx
```

## Building

```bash
dotnet build
dotnet test
dotnet run --project samples/SatelliteDemo
dotnet pack src/SatelliteWindows.Avalonia -c Release  # Create NuGet package
```

## Roadmap

| Phase | Status | Features |
|-------|--------|----------|
| **1 — Core snapping** | ✅ | Edge snap, follow-on-move/resize, minimize/close sync, multi-monitor, DPI-aware |
| **2 — Chaining** | ✅ | Satellite-to-satellite trees, stacking, snap-to-any, edge sliding, persist/restore |
| **3 — Dual-mode docking** | ✅ | `ISatelliteDockHost`, `Dock`/`Undock`, content transfer between external and internal mode |
| **4 — Polish** | ✅ | Snap animation, multi-target net8.0+net10.0, NuGet packaging |
| **5 — Tri-role + facade** | ✅ | `WindowRole`, `SatelliteDockManager`, `PanelMode`, `IHostWindowFactory`, `FlushContentForReparent` |
| **6 — Vendored Dock + unified window** | ✅ | `Dock.Avalonia` vendored under `SatelliteWindows.Dock.*`; `HostWindow` unsealed so `SatelliteWindow : HostWindow : Window` |

## License

This library is released under the [MIT License](LICENSE).

### Third-party software

SatelliteWindows.Avalonia bundles a renamed fork of
[Dock.Avalonia](https://github.com/wieslawsoltes/Dock) (Copyright © Wiesław Šoltés,
MIT, v12.0.0.2) under `src/SatelliteWindows.Avalonia.Dock/`. The only
source-level modification is the removal of `sealed` from `HostWindow` so
`SatelliteWindow` can inherit from it; all other source files are unchanged
apart from `Dock.*` → `SatelliteWindows.Dock.*` project/namespace renames.

Full third-party notices: see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)
(also packaged into the NuGet). Per-project details in
[`src/SatelliteWindows.Avalonia.Dock/README.md`](src/SatelliteWindows.Avalonia.Dock/README.md).
