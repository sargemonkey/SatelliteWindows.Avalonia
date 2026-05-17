# SatelliteWindows.Avalonia

Multi-window snapping library for [Avalonia UI](https://avaloniaui.net/) — magnetic satellite windows that attach to edges of a main window and move as one unified surface.

[![Build](https://img.shields.io/badge/phase-1%20core%20snapping-blue)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)]()
[![Avalonia](https://img.shields.io/badge/Avalonia-11.2-blueviolet)]()
[![License](https://img.shields.io/badge/license-MIT-green)]()

```
┌──────────┐┌──────────┐
│  Left    ││          │┌──────────┐
│  Panel   ││   Main   ││  Right   │
│(attached)││  Window  ││  Panel   │
└──────────┘│          │└──────────┘
            └──────────┘
```

## Features (Phase 1)

- **Edge snapping** — attach satellite windows to Left, Right, Top, or Bottom edges
- **Follow-on-move** — satellites reposition in lockstep when the main window moves
- **Follow-on-resize** — satellites reposition when the main window is resized
- **Minimize/restore sync** — satellites hide and restore with the main window
- **Close propagation** — satellites close when the main window closes
- **Multi-monitor aware** — satellites can extend across display boundaries; clamping uses per-screen intersection (handles L-shaped layouts correctly)
- **DPI-aware** — positioning uses pixel-space math with logical↔pixel conversion
- **Configurable** — snap behavior tunable via `SnapBehavior`
- **Zero dependencies** beyond Avalonia itself

## Quick Start

```csharp
using SatelliteWindows.Avalonia;

// In your main window's Opened handler:
var manager = new SatelliteManager(mainWindow);

// Create and attach a satellite
var toolPanel = new SatelliteWindow
{
    Title = "Tools",
    Width = 250,
    Height = 400,
    Content = new TextBlock { Text = "I follow the main window!" }
};

manager.Attach(toolPanel, SnapEdge.Right);

// Later — detach or close
manager.Detach(toolPanel, closeSatellite: true);

// Or close all satellites at once
manager.DetachAll();
```

## API Reference

### `SatelliteManager`

The core manager — attach it to your main `Window` to control satellite lifecycle.

```csharp
var manager = new SatelliteManager(mainWindow);                 // default behavior
var manager = new SatelliteManager(mainWindow, new SnapBehavior  // custom behavior
{
    FollowOnResize = true,
    MinimizeWithMain = true,
    CloseWithMain = true,
    MinVisiblePixels = 50
});
```

| Method | Description |
|--------|-------------|
| `Attach(satellite, edge, offset)` | Attach and show a satellite on the given edge |
| `Detach(satellite, closeSatellite)` | Detach a satellite; optionally close its window |
| `DetachAll()` | Close and detach all satellites |
| `Attachments` | Read-only list of current `SatelliteAttachment`s |
| `Dispose()` | Unsubscribe from main window events and close all satellites |

### `SatelliteWindow`

A `Window` subclass with snap awareness. `ShowInTaskbar` is `false` by default. Set your content, size, and any window properties before attaching.

### `SnapEdge`

```csharp
public enum SnapEdge { Left, Right, Top, Bottom }
```

### `SnapBehavior`

| Property | Default | Description |
|----------|---------|-------------|
| `FollowOnResize` | `true` | Reposition satellites when main window resizes |
| `MinimizeWithMain` | `true` | Hide satellites when main window minimizes |
| `CloseWithMain` | `true` | Close satellites when main window closes |
| `MinVisiblePixels` | `50` | Min pixels visible on any screen before clamping |
| `MagneticThresholdPx` | `20` | *(Phase 2)* Magnetic snap distance |
| `AutoSnapOnDrag` | `false` | *(Phase 2)* Auto-snap on drag near edge |
| `ChainDepthLimit` | `-1` | *(Phase 2)* Max chain depth (-1 = unlimited) |

## Project Structure

```
SatelliteWindows.Avalonia/
├── src/
│   └── SatelliteWindows.Avalonia/       # Core library
│       ├── SatelliteManager.cs          # Main manager
│       ├── SatelliteWindow.cs           # Window subclass
│       ├── SatelliteAttachment.cs       # Attachment descriptor
│       ├── SnapEdge.cs                  # Edge enum
│       ├── SnapBehavior.cs              # Configuration
│       ├── ISatelliteDockHost.cs        # Phase 3 stub
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
```

## Roadmap

| Phase | Status | Features |
|-------|--------|----------|
| **1 — Core snapping** | ✅ Done | Edge attachment, follow-on-move/resize, minimize/close sync, multi-monitor, DPI-aware |
| **2 — Chaining + magnetic snap** | Planned | Satellite-to-satellite chaining, magnetic drag snap, drag-away detach, stacking, persist/restore |
| **3 — Dual mode + Dock.Avalonia** | Planned | Dock inside main window, drag-in/out, `ISatelliteDockHost`, Dock.Avalonia integration package |
| **4 — Polish** | Planned | Custom chrome, resize handles, animations, cross-platform testing, NuGet packaging |

## License

MIT
