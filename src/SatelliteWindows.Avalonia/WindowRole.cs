namespace SatelliteWindows.Avalonia;

/// <summary>
/// What role a <see cref="SatelliteWindow"/> instance is fulfilling.
/// One window class, three modes — selected at construction or via
/// <see cref="SatelliteWindow.Role"/>.
///
/// <para>The role drives feature-wiring:</para>
/// <list type="bullet">
///   <item><see cref="Satellite"/> — magnetic edge snap to a main window;
///         follow on resize; close-with-main; small chrome.</item>
///   <item><see cref="Floating"/> — standalone window, no snap, free
///         positioning. Same chrome as Satellite so the user gets
///         consistent decoration regardless of which mode they picked.</item>
///   <item><see cref="DockHost"/> — host for dock-floated panels (replaces
///         Dock.Avalonia's sealed <c>HostWindow</c>). Allows Dock to drag
///         tabs out into a managed Mux-themed window that can later be
///         promoted to <see cref="Satellite"/> via the manager.</item>
/// </list>
/// </summary>
public enum WindowRole
{
    /// <summary>
    /// Standalone floating window — no snap, no host-managed lifecycle.
    /// Default for newly-constructed windows that haven't been attached
    /// to a <see cref="SatelliteManager"/> or used by Dock.
    /// </summary>
    Floating,

    /// <summary>
    /// Magnetically snapped to a main window's edge. Wired via
    /// <see cref="SatelliteManager.Attach"/>; snap thresholds and
    /// behaviour come from <see cref="SnapBehavior"/>.
    /// </summary>
    Satellite,

    /// <summary>
    /// Host for Dock.Avalonia floated panels. Same visual chrome as the
    /// other roles, but the lifecycle is driven by the dock framework
    /// (not the host application). Promotable to <see cref="Satellite"/>
    /// at runtime — that's the whole point of doing this in our library
    /// instead of using Dock's sealed HostWindow.
    /// </summary>
    DockHost,
}
