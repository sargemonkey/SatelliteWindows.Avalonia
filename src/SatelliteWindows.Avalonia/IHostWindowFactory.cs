using System;

namespace SatelliteWindows.Avalonia;

/// <summary>
/// Contract a dock framework can call (via the host application's integration
/// glue) to obtain a managed <see cref="SatelliteWindow"/> in
/// <see cref="WindowRole.DockHost"/> role, instead of constructing its own
/// sealed host-window type.
///
/// <para>Implementers typically wrap a <see cref="SatelliteDockManager"/> so
/// that the returned window can later be promoted to
/// <see cref="WindowRole.Satellite"/> via <see cref="SatelliteDockManager.SetMode"/>
/// or <see cref="SatelliteManager.Attach(SatelliteWindow, SnapEdge, double)"/>.</para>
///
/// <para>The library does not take a hard dependency on Dock.Avalonia (or any
/// other dock framework). Hosts wire the factory into whichever framework
/// they use — e.g. by exposing it through <c>IFactory.HostWindowLocator</c>
/// in Dock.Avalonia 12.</para>
/// </summary>
public interface IHostWindowFactory
{
    /// <summary>
    /// Construct a new window for hosting dock-framework floated content. The
    /// window is returned in <see cref="WindowRole.DockHost"/> role and has not
    /// been shown yet — the dock framework is expected to call <c>Show</c> as
    /// part of its drag-out flow.
    /// </summary>
    SatelliteWindow CreateHostWindow();
}

/// <summary>
/// Minimal built-in implementation of <see cref="IHostWindowFactory"/> that
/// constructs a vanilla <see cref="SatelliteWindow"/> in
/// <see cref="WindowRole.DockHost"/> role and invokes an optional callback so
/// the host can apply chrome / theming / sizing.
/// </summary>
public sealed class DefaultHostWindowFactory : IHostWindowFactory
{
    private readonly Action<SatelliteWindow>? _onCreated;

    /// <summary>
    /// Create a factory. The optional <paramref name="onCreated"/> callback runs
    /// after the window has been constructed and its role set, allowing the host
    /// to apply chrome (background, title-bar theming, content template, etc.).
    /// </summary>
    public DefaultHostWindowFactory(Action<SatelliteWindow>? onCreated = null)
        => _onCreated = onCreated;

    /// <inheritdoc />
    public SatelliteWindow CreateHostWindow()
    {
        var w = new SatelliteWindow { Role = WindowRole.DockHost };
        _onCreated?.Invoke(w);
        return w;
    }
}
