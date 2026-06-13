using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SatelliteWindows.Avalonia;

namespace SatelliteDemo;

public partial class MainWindow : Window
{
    private const string PanelId = "tools";
    private const double DragThresholdPx = 8;

    private SatelliteDockManager? _manager;
    private ToolsPanel? _panel;
    private SatelliteWindow? _external; // currently popped-out window (Satellite or Floating)
    private bool _dockBackArmed; // true once the popped-out window has moved fully outside the slot

    private Point _pressOrigin;
    private bool _isPotentialDrag;
    private PointerPressedEventArgs? _pressArgs;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;

        DragGrip.PointerPressed += OnGripPressed;
        DragGrip.PointerMoved += OnGripMoved;
        DragGrip.PointerReleased += OnGripReleased;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _panel = new ToolsPanel(BuildPanelContent());
        var bridge = new SingleDockBridge(_panel, DockContentHost, DragGrip, DropPlaceholder);

        _manager = new SatelliteDockManager(this, bridge);
        _manager.SatelliteCreated += OnPoppedOut;
        _manager.SatelliteClosedByUser += _ => { _external = null; UpdateStatus(); };
        _manager.SatelliteManager.AttachmentChanged += UpdateStatus;

        _manager.SetMode(PanelId, SatelliteDockManager.PanelMode.Docked);
        UpdateStatus();
    }

    // ── Grip drag → detach to Satellite at cursor ───────────────────

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_external != null) return; // already popped out
        _pressOrigin = e.GetPosition(this);
        _isPotentialDrag = true;
        _pressArgs = e;
        e.Pointer.Capture(DragGrip);
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPotentialDrag || _manager == null) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _pressOrigin.X) + Math.Abs(p.Y - _pressOrigin.Y) < DragThresholdPx)
            return;

        // Threshold crossed — pop out as Satellite, hand the drag off to the new window.
        _isPotentialDrag = false;
        e.Pointer.Capture(null);

        _manager.SetMode(PanelId, SatelliteDockManager.PanelMode.Satellite);
        if (_external != null)
        {
            // Reposition the satellite under the cursor and start an OS-level move drag.
            var screen = this.PointToScreen(p);
            _external.Position = new PixelPoint(screen.X - 60, screen.Y - 12);
            try
            {
                if (_pressArgs != null) _external.BeginMoveDrag(_pressArgs);
            }
            catch { /* drag may not start if pointer already released */ }
        }
        _pressArgs = null;
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPotentialDrag = false;
        _pressArgs = null;
        e.Pointer.Capture(null);
    }

    // ── Popped-out window lifecycle ─────────────────────────────────

    private void OnPoppedOut(SatelliteWindow window, ISatellitePanel _)
    {
        _external = window;
        _dockBackArmed = false; // user must drag the window fully clear of the slot before re-dock arms
        window.PositionChanged += OnExternalPositionChanged;
        window.Closed += (_, _) =>
        {
            window.PositionChanged -= OnExternalPositionChanged;
            if (_external == window) _external = null;
            UpdateStatus();
        };
    }

    private void OnExternalPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (sender is not SatelliteWindow w || _manager == null) return;
        if (_external != w) return; // already docked back; ignore late events

        bool overlaps = PointerOverlapsDockSlot(w);

        if (!_dockBackArmed)
        {
            if (!overlaps) _dockBackArmed = true;
            return;
        }

        if (overlaps)
        {
            _external = null; // clear first so re-entrant PositionChanged short-circuits
            _manager.SetMode(PanelId, SatelliteDockManager.PanelMode.Docked);
            UpdateStatus();
        }
    }

    private bool PointerOverlapsDockSlot(SatelliteWindow w)
    {
        try
        {
            var topLeft = DockSlot.PointToScreen(new Point(0, 0));
            var scaling = RenderScaling;
            var slotW = (int)(DockSlot.Bounds.Width * scaling);
            var slotH = (int)(DockSlot.Bounds.Height * scaling);
            int cx = w.Position.X + (int)(w.Width * scaling / 2);
            int cy = w.Position.Y + (int)(w.Height * scaling / 2);
            return cx >= topLeft.X && cx <= topLeft.X + slotW
                && cy >= topLeft.Y && cy <= topLeft.Y + slotH;
        }
        catch { return false; }
    }

    private void UpdateStatus()
    {
        if (_manager == null) return;
        var mode = _manager.GetMode(PanelId);
        var roleSuffix = _external != null ? $"  (window role = {_external.Role})" : "";
        StatusText.Text = $"Mode: {mode}{roleSuffix}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _manager?.Dispose();
        base.OnClosed(e);
    }

    // ── Panel content ──

    private static Control BuildPanelContent() => new Border
    {
        Padding = new Thickness(16),
        Child = new StackPanel
        {
            Spacing = 10,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Tools Panel", FontSize = 18,
                    FontWeight = FontWeight.SemiBold, Foreground = Brushes.White },
                new TextBlock {
                    Text = "Same instance — docked, floating, or satellite.\n" +
                           "Grip ↑ to detach, drop back on the slot to dock.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)),
                    TextWrapping = TextWrapping.Wrap }
            }
        }
    };

    // ── Minimal ISatellitePanel + ISatelliteDockBridge for one panel ──

    private sealed class ToolsPanel : ISatellitePanel
    {
        public ToolsPanel(Control content) { Content = content; }
        public string Id => PanelId;
        public string? Title => "Tools";
        public object Content { get; }
        public SnapEdge DefaultSnapEdge => SnapEdge.Right;
        public double DefaultSatelliteWidth => 260;
        public double DefaultSatelliteHeight => 0;
    }

    /// <summary>
    /// Bridge for a single panel. Keeps the dock slot visible at all times so it
    /// doubles as a drop target — only the inner content swaps in/out.
    /// </summary>
    private sealed class SingleDockBridge : ISatelliteDockBridge
    {
        private readonly ToolsPanel _panel;
        private readonly ContentControl _host;
        private readonly Control _grip;
        private readonly Control _placeholder;

        public SingleDockBridge(ToolsPanel panel, ContentControl host, Control grip, Control placeholder)
        {
            _panel = panel; _host = host; _grip = grip; _placeholder = placeholder;
        }

        public ISatellitePanel? FindPanel(string id) => id == _panel.Id ? _panel : null;
        public bool IsDocked(string id) => id == _panel.Id && _host.Content != null;

        public bool ShowDocked(string id)
        {
            if (id != _panel.Id) return false;
            _host.Content = _panel.Content;
            _host.IsVisible = true;
            _grip.IsVisible = true;
            _placeholder.IsVisible = false;
            return true;
        }

        public bool HideFromDock(string id)
        {
            if (id != _panel.Id) return false;
            _host.Content = null;
            _grip.IsVisible = false;
            _placeholder.IsVisible = true;
            return true;
        }

        public ISatellitePanel? ExtractForSatellite(string id)
        {
            if (id != _panel.Id) return null;
            _host.Content = null;
            _grip.IsVisible = false;
            _placeholder.IsVisible = true; // becomes the drop target
            return _panel;
        }

        public bool ReinsertFromSatellite(string id) => ShowDocked(id);
    }
}
