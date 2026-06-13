using System.Collections.Generic;
using Avalonia.Controls;

namespace SatelliteWindows.Avalonia.Tests;

/// <summary>
/// Shared test helpers — fakes for <see cref="ISatellitePanel"/> and
/// <see cref="ISatelliteDockBridge"/>, plus a small harness that wires a main
/// window + dock host + manager together.
/// </summary>
internal static class TestHelpers
{
    public sealed class FakePanel : ISatellitePanel
    {
        public FakePanel(Control content, string id = "p1", SnapEdge edge = SnapEdge.Right,
            double width = 200, double height = 0, string? title = "Test")
        {
            Content = content; Id = id; DefaultSnapEdge = edge;
            DefaultSatelliteWidth = width; DefaultSatelliteHeight = height; Title = title;
        }
        public string Id { get; }
        public string? Title { get; }
        public object Content { get; }
        public SnapEdge DefaultSnapEdge { get; }
        public double DefaultSatelliteWidth { get; }
        public double DefaultSatelliteHeight { get; }
    }

    /// <summary>
    /// Multi-panel bridge — one ContentControl host per panel id. Lets us verify
    /// that <see cref="SatelliteDockManager"/> drives independent dock-slots
    /// correctly when several panels share a single bridge.
    /// </summary>
    public sealed class FakeBridge : ISatelliteDockBridge
    {
        private readonly Dictionary<string, (FakePanel panel, ContentControl host)> _slots = new();
        public void Register(FakePanel panel, ContentControl host) => _slots[panel.Id] = (panel, host);

        public ISatellitePanel? FindPanel(string id) => _slots.TryGetValue(id, out var s) ? s.panel : null;
        public bool IsDocked(string id) => _slots.TryGetValue(id, out var s) && s.host.Content != null;
        public bool ShowDocked(string id) { if (!_slots.TryGetValue(id, out var s)) return false; s.host.Content = s.panel.Content; return true; }
        public bool HideFromDock(string id) { if (!_slots.TryGetValue(id, out var s)) return false; s.host.Content = null; return true; }
        public ISatellitePanel? ExtractForSatellite(string id) { if (!_slots.TryGetValue(id, out var s)) return null; s.host.Content = null; return s.panel; }
        public bool ReinsertFromSatellite(string id) => ShowDocked(id);
    }

    public sealed class Harness
    {
        public Window Main = null!;
        public ContentControl Host = null!;
        public FakePanel Panel = null!;
        public FakeBridge Bridge = null!;
        public SatelliteDockManager Manager = null!;

        public void Cleanup()
        {
            try { Manager.Dispose(); } catch { }
            try { Main.Close(); } catch { }
        }
    }

    public static Harness BuildHost(string panelId = "p1", SnapEdge edge = SnapEdge.Right)
    {
        var content = new Border { Width = 100, Height = 100 };
        var panel = new FakePanel(content, panelId, edge);
        var host = new ContentControl();
        var bridge = new FakeBridge();
        bridge.Register(panel, host);
        var main = new Window { Width = 400, Height = 300, Content = host };
        main.Show();
        var mgr = new SatelliteDockManager(main, bridge);
        mgr.SetMode(panel.Id, SatelliteDockManager.PanelMode.Docked);
        return new Harness { Main = main, Host = host, Panel = panel, Bridge = bridge, Manager = mgr };
    }
}
