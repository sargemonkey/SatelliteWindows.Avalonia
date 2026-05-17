# Plan: Satellite Window System — Standalone Avalonia Library

## Location
`C:\root\SatelliteWindows` — standalone project, separate from MuxiMuxi.
Published as NuGet: `SatelliteWindows.Avalonia`

## Problem
Avalonia has no built-in support for multi-window snapping. IDE-like apps need
auxiliary windows that magnetically attach to the main window and move as one
unified surface. This library provides that behavior generically for any
Avalonia application.

## Library API Surface (public)

### Core Types

**`SatelliteManager`** — attached to a main `Window`, manages satellite lifecycle
```
var manager = new SatelliteManager(mainWindow);

// Attach externally to main window edge
manager.Attach(panelA, SnapEdge.Right);

// Chain: attach to another satellite
manager.Attach(panelC, panelB, SnapEdge.Bottom);

// Dock inside the main window (becomes an internal panel)
manager.Dock(panelA);

// Undock from main window back to external satellite
manager.Undock(panelA, SnapEdge.Right);

// Detach (children re-parent or detach too)
manager.Detach(panelB, DetachMode.ReparentChildren);
manager.Detach(panelB, DetachMode.DetachChain);

manager.DetachAll();
```

**`SatelliteWindow`** — a `Window` subclass with snap awareness
```
var satellite = new SatelliteWindow { Content = myPanel };
manager.Attach(satellite, SnapEdge.Right);
```

**`SnapEdge`** — Left, Right, Top, Bottom

**`SatelliteAttachment`** — data record: edge, offset along edge, size, parent reference

**`SnapBehavior`** — configuration: magnetic threshold (px), auto-snap on drag,
follow on resize, minimize/restore behavior, chain depth limit

### Key behaviors
- Main window move → all satellites reposition in lockstep (including chains)
- Main window resize → satellites on resized edges reposition
- Main minimize → hide satellites; restore → show
- Main close → close all satellites
- Drag satellite away → detach (optional, configurable)
- Drag satellite near edge → magnetic snap (optional, configurable)
- Multiple satellites per edge (stacked with offsets)
- DPI-aware: pixel-space positioning with logical→pixel conversion

### Multi-monitor behavior
- Satellites can extend across display boundaries — no per-display clamping
- Screen-clamping only prevents windows from going entirely off ALL displays
- Virtual desktop rect = union of all `Screens.All` working areas
- A satellite snapped to the right edge of the main window naturally extends
  onto the adjacent monitor if one is present

### Dual mode: external satellite OR internal dock
A satellite window has two states — it can live **outside** as a snapped
satellite, or be **docked inside** the main window as an embedded panel:
```
External (satellite):              Internal (docked):
┌──────────┐┌──────────┐          ┌─────────────────────┐
│  Main    ││  Panel   │   ←→     │  Main     │  Panel  │
│  Window  ││(snapped) │          │  Window   │(docked) │
└──────────┘└──────────┘          └─────────────────────┘
```
- Drag a satellite INTO the main window → it docks as an internal panel
- Drag an internal panel OUT of the main window → it becomes a satellite
- Same content, just different hosting mode
- API: `manager.Dock(satellite)` / `manager.Undock(panel)` or drag-based
- When docked internally, the satellite's content is injected into the main
  window's layout (e.g. as a split panel or dock tool)

### Snap Chaining
Satellites can snap to other satellites, forming chains:
```
┌──────────┐┌──────────┐┌──────────┐
│  Panel A ││  Main    ││  Panel B │
│ (chained)││  Window  ││(attached)│
└──────────┘└──────────┘└──────────┘
                              │
                        ┌──────────┐
                        │  Panel C │
                        │(chained  │
                        │ to B)    │
                        └──────────┘
```
- Any satellite can be a snap target for another satellite
- Moving the main window propagates through the entire chain tree
- Detaching a satellite also detaches its children (or re-parents them)
- Chain depth is configurable (default: unlimited)
- API: `manager.Attach(panelC, panelB, SnapEdge.Bottom)` — attach C to B's bottom edge
- Internal model: tree of `SatelliteAttachment` nodes rooted at the main window
- Position calculation walks the tree recursively: parent position + edge offset

### Integration with Dock.Avalonia (optional extension)
Separate package: `SatelliteWindows.Avalonia.Dock`
- `SatelliteHostWindow : HostWindow` — Dock.Avalonia floating windows that can snap
- Hooks into `HostWindowLocator` for automatic integration

## Project Structure
```
C:\root\SatelliteWindows\
├── src\
│   ├── SatelliteWindows.Avalonia\          # Core library
│   │   ├── SatelliteManager.cs
│   │   ├── SatelliteWindow.cs
│   │   ├── SnapEdge.cs
│   │   ├── SatelliteAttachment.cs
│   │   ├── SnapBehavior.cs
│   │   ├── ISatelliteDockHost.cs           # Interface for internal docking
│   │   └── Internal\
│   │       ├── PositionCalculator.cs       # Geometry math (tree-recursive)
│   │       ├── ScreenHelper.cs             # Multi-monitor union + clamping
│   │       └── DpiHelper.cs                # Pixel↔DIP conversion
│   └── SatelliteWindows.Avalonia.Dock\     # Dock.Avalonia integration
│       └── SatelliteHostWindow.cs
├── samples\
│   └── SatelliteDemo\                      # Minimal demo app
├── nuget.config
└── SatelliteWindows.sln
```

## Implementation Phases

### Phase 1: Core snapping (1 week)
- SatelliteManager + SatelliteWindow
- Right-edge and left-edge attachment
- Main window move → satellites follow
- Main window resize → satellites reposition
- Main minimize/close → satellites follow
- Multi-monitor aware — satellites can extend across displays
- Clamp only to virtual desktop bounds (union of all screens)
- Windows-first, cross-platform best-effort
- Demo app

### Phase 2: Full edges + chaining + magnetic snap (1 week)
- Top and bottom edges
- **Snap chaining** — satellites snap to other satellites, forming trees
- Chain-aware detach (reparent children or detach entire sub-tree)
- Multiple satellites per edge (stacking)
- Magnetic snap threshold on drag (to any snappable window, not just main)
- Drag-away detach
- DPI-aware positioning
- Persist/restore attachment state (including chain topology)

### Phase 3: Dual mode + Dock.Avalonia integration (1 week)
- **Dual mode** — satellites can dock inside the main window as internal panels
- Drag satellite into main window → docks internally
- Drag internal panel out → becomes satellite
- `ISatelliteDockHost` interface for apps to provide internal dock target
- SatelliteHostWindow extending Dock.Avalonia HostWindow
- Integration package (`SatelliteWindows.Avalonia.Dock`)

### Phase 4: Polish (1 week)
- Custom minimal chrome option
- Resize handles on shared edges
- Animation for snap/detach/dock transitions
- Cross-platform testing + fixes
- NuGet packaging

## Scope
- **Phase 1 alone** is a usable library and enough for MuxiMuxi MVP
- Library targets `net8.0` + `net10.0` (broad Avalonia compat)
- Avalonia 12.x dependency
- Zero other dependencies in core package

## MuxiMuxi Integration
After Phase 1, MuxiMuxi adds a project reference to `SatelliteWindows.Avalonia`
and uses it to allow tool panels to pop out as satellite windows. The integration
point is `MuxiMuxiDockFactory.HostWindowLocator` returning `SatelliteHostWindow`
when the Dock integration package is ready (Phase 3), or manual attachment via
`SatelliteManager` for Phase 1.

