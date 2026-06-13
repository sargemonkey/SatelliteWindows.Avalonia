# SatelliteWindows.Avalonia.Dock — Vendored Dock.Avalonia

This folder contains the **Dock.Avalonia** source code (12.0.0.2, MIT) vendored
into the SatelliteWindows.Avalonia repository so consumers get docking +
satellite-snap features from a single library, and so we can unify
`SatelliteWindows.Dock.Avalonia.Controls.HostWindow` with
`SatelliteWindows.Avalonia.SatelliteWindow`.

## Why vendor?

Upstream `Dock.Avalonia.Controls.HostWindow` is
`public sealed class HostWindow : Window`. That means consumers cannot
subclass it to add custom chrome, DWM title-bar theming, or
snap-to-main-window behaviour. The published `IHostWindowFactory` seam helps
but still leaves a "tab dragged out → non-snapping window with wrong chrome"
experience.

By vendoring the source we can:
- **Unseal** `HostWindow` (still derives from `Window`).
- Have `SatelliteWindow` inherit FROM the unsealed `HostWindow`, so every
  `SatelliteWindow` instance is also a Dock `HostWindow`.
- A Dock framework `DockControl` (or anything else expecting `HostWindow`)
  can be given a factory that returns `SatelliteWindow` instances, making
  dragged-out tabs snap-capable, themable, and lifecycle-tracked.
- Drop the upstream `Dock.Avalonia` NuGet dependency from downstream apps.

## Class hierarchy

```
Avalonia.Controls.Window
        ▲
        │
SatelliteWindows.Dock.Avalonia.Controls.HostWindow    ← unsealed (was `sealed`)
        ▲
        │
SatelliteWindows.Avalonia.SatelliteWindow             ← snap-aware, carries Role
```

## Origin

- Source: https://github.com/wieslawsoltes/Dock
- Tag: v12.0.0.2
- License: MIT — see `LICENSE-Dock.TXT`
- Author: Wiesław Šoltés

## Vendored projects (12)

Only the projects required by SatelliteWindows.Avalonia (and their transitive
deps) are vendored. All have been renamed with the `SatelliteWindows.Dock.*`
prefix and their namespaces updated to `SatelliteWindows.Dock.*` so they don't
collide with any upstream `Dock.Avalonia` NuGet package a consumer might also
reference.

| Project | Purpose |
|---|---|
| `SatelliteWindows.Dock.Model` | Core dock layout model interfaces |
| `SatelliteWindows.Dock.Model.Mvvm` | MVVM base classes for layout factory |
| `SatelliteWindows.Dock.Avalonia` | Avalonia controls (`DockControl`, splitter, `HostWindow`, …) |
| `SatelliteWindows.Dock.Avalonia.Themes.Fluent` | Default Fluent theme |
| `SatelliteWindows.Dock.Settings` | Static dock settings |
| `SatelliteWindows.Dock.MarkupExtension` | XAML markup helpers |
| `SatelliteWindows.Dock.Controls.DeferredContentControl` | Lazy content rendering |
| `SatelliteWindows.Dock.Controls.ProportionalStackPanel` | Splittable stack panel |
| `SatelliteWindows.Dock.Controls.Recycling` | Control recycling |
| `SatelliteWindows.Dock.Controls.Recycling.Model` | Recycling model |
| `SatelliteWindows.Dock.Serializer.SystemTextJson` | Layout JSON serializer |
| `SatelliteWindows.Dock.Serializer.SystemTextJson.Generators` | Source generators for above |

Excluded upstream projects (Diagnostics, ReactiveUI, Prism, Newtonsoft, Yaml,
Protobuf, Caliburn, etc.) are not needed.

## Modifications from upstream

| File | Change | Reason |
|---|---|---|
| `SatelliteWindows.Dock.Avalonia/Controls/HostWindow.axaml.cs` | `sealed` removed | So `SatelliteWindow` can inherit from it (the unification). The base class itself is otherwise unchanged. |
| All project files | Renamed `Dock.*` → `SatelliteWindows.Dock.*` and namespaces likewise | Avoids collision with any upstream `Dock.Avalonia` NuGet a consumer might also reference. |

All other source files are **unmodified** (apart from `using` and `namespace`
renames that follow from the package rename). To re-sync with a future
upstream release, re-vendor the source, re-apply the rename, and re-apply the
`sealed` removal patch.
