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
| `Attach(satellite, edge, offset)` | Attach to the main window's edge |
| `Attach(satellite, parent, edge, offset)` | Chain to another satellite's edge |
| `Detach(satellite, closeSatellite)` | Detach (default: DetachChain) |
| `Detach(satellite, mode, closeSatellite)` | Detach with explicit `DetachMode` |
| `DetachAll()` | Close and detach everything |
| `Dock(satellite, edge)` | Dock inside main window (requires `ISatelliteDockHost`) |
| `Undock(satellite, edge)` | Pop back out as external satellite |
| `GetChildren(parent)` | Get direct children of a window in the tree |
| `IsDocked(satellite)` | Check if a satellite is docked internally |
| `SaveState()` / `RestoreState(...)` | Persist and restore the attachment tree |
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

## Project Structure

```
SatelliteWindows.Avalonia/
├── src/
│   └── SatelliteWindows.Avalonia/       # Core library (net8.0 + net10.0)
│       ├── SatelliteManager.cs          # Main manager — tree model, positioning, snap/detach
│       ├── SatelliteWindow.cs           # Window subclass with snap awareness
│       ├── SatelliteAttachment.cs       # Attachment descriptor (edge, offset, parent)
│       ├── SnapEdge.cs                  # Edge enum
│       ├── SnapBehavior.cs              # Configuration
│       ├── DetachMode.cs                # Detach chain handling
│       ├── AttachmentState.cs           # Persist/restore record
│       ├── ISatelliteDockHost.cs        # Dual-mode docking interface
│       └── Internal/
│           ├── PositionCalculator.cs    # Geometry math
│           ├── ScreenHelper.cs          # Multi-monitor clamping
│           └── DpiHelper.cs             # DIP↔pixel conversion
├── samples/
│   └── SatelliteDemo/                   # Interactive demo app
├── tests/
│   └── SatelliteWindows.Avalonia.Tests/ # Unit tests (46 tests)
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
| **3 — Dual-mode docking** | ✅ | ISatelliteDockHost, Dock/Undock, content transfer between external and internal mode |
| **4 — Polish** | ✅ | Snap animation, multi-target net8.0+net10.0, NuGet packaging |

## License

MIT
