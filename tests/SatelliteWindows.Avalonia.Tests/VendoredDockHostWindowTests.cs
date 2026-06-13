using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SatelliteWindows.Dock.Avalonia.Controls;
using Xunit;

namespace SatelliteWindows.Avalonia.Tests;

/// <summary>
/// Coverage for the vendored Dock.Avalonia integration. The unification
/// approach: <see cref="SatelliteWindow"/> inherits FROM <see cref="HostWindow"/>
/// (which in turn inherits from <see cref="Window"/>). The result is a single
/// apex class — every <see cref="SatelliteWindow"/> is also a Dock
/// <see cref="HostWindow"/>, so the host can return a <c>SatelliteWindow</c>
/// from <c>DockControl.HostWindowFactory</c> and dragged-out tabs land in a
/// snap-aware window that can later be promoted to a real satellite.
/// </summary>
public class VendoredDockHostWindowTests
{
    [AvaloniaFact]
    public void SatelliteWindow_InheritsFromHostWindow()
    {
        Assert.True(typeof(HostWindow).IsAssignableFrom(typeof(SatelliteWindow)),
            $"Expected SatelliteWindow to inherit from Dock HostWindow, but its " +
            $"base chain is: {DescribeBaseChain(typeof(SatelliteWindow))}");
    }

    [AvaloniaFact]
    public void SatelliteWindow_IsAssignableToHostWindowReference()
    {
        var sat = new SatelliteWindow();
        HostWindow asHost = sat;
        Assert.Same(sat, asHost);
        sat.Close();
    }

    [AvaloniaFact]
    public void HostWindow_ConstructedDirectly_IsStillAvaloniaWindow()
    {
        var w = new HostWindow();
        Assert.IsAssignableFrom<Window>(w);
        w.Close();
    }

    [AvaloniaFact]
    public void SatelliteWindow_DefaultsToFloatingRole()
    {
        // Smoke test: even after the inheritance flip, a freshly constructed
        // SatelliteWindow still starts in WindowRole.Floating (the rest state).
        var sat = new SatelliteWindow();
        Assert.Equal(WindowRole.Floating, sat.Role);
        sat.Close();
    }

    private static string DescribeBaseChain(System.Type t)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (var cur = t; cur != null; cur = cur.BaseType)
            parts.Add(cur.FullName ?? cur.Name);
        return string.Join(" → ", parts);
    }
}
