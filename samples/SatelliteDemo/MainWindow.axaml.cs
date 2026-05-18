using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SatelliteWindows.Avalonia;

namespace SatelliteDemo;

public partial class MainWindow : Window, ISatelliteDockHost
{
    private SatelliteManager? _manager;
    private SatelliteWindow? _leftSatellite;
    private SatelliteWindow? _rightSatellite;
    private readonly Dictionary<SatelliteWindow, (object? content, Border dockArea)> _dockedContent = new();

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnMainOpened;
    }

    private void OnMainOpened(object? sender, EventArgs e)
    {
        _manager = new SatelliteManager(this);
        _manager.AttachmentChanged += SyncAndUpdateStatus;
    }

    // ── ISatelliteDockHost ──────────────────────────────────────────

    public bool TryDockSatellite(SatelliteWindow satellite, SnapEdge edge)
    {
        var dockArea = edge == SnapEdge.Left ? LeftDockArea : RightDockArea;
        if (dockArea.IsVisible) return false;

        var content = satellite.Content;
        satellite.Content = null;
        dockArea.Child = content as Control;
        dockArea.IsVisible = true;
        _dockedContent[satellite] = (content, dockArea);
        return true;
    }

    public bool TryUndockSatellite(SatelliteWindow satellite)
    {
        if (!_dockedContent.TryGetValue(satellite, out var info)) return false;

        var content = info.dockArea.Child;
        info.dockArea.Child = null;
        info.dockArea.IsVisible = false;
        satellite.Content = content;
        _dockedContent.Remove(satellite);
        return true;
    }

    // ── Button handlers ─────────────────────────────────────────────

    private void OnAttachLeft(object? sender, RoutedEventArgs e)
    {
        if (_manager == null || _leftSatellite != null) return;
        _leftSatellite = CreateSatellitePanel("Left Panel", Colors.DarkSlateBlue);
        _leftSatellite.Closed += (_, _) => { _leftSatellite = null; SyncAndUpdateStatus(); };
        _manager.Attach(_leftSatellite, SnapEdge.Left);
    }

    private void OnAttachRight(object? sender, RoutedEventArgs e)
    {
        if (_manager == null || _rightSatellite != null) return;
        _rightSatellite = CreateSatellitePanel("Right Panel", Colors.DarkOliveGreen);
        _rightSatellite.Closed += (_, _) => { _rightSatellite = null; SyncAndUpdateStatus(); };
        _manager.Attach(_rightSatellite, SnapEdge.Right);
    }

    private void OnAttachBottomToRight(object? sender, RoutedEventArgs e)
    {
        if (_manager == null || _rightSatellite == null || !_rightSatellite.IsAttached) return;
        var chained = CreateSatellitePanel("Chained (Bottom->R)", Colors.DarkGoldenrod);
        chained.Height = 200;
        chained.Closed += (_, _) => SyncAndUpdateStatus();
        _manager.Attach(chained, _rightSatellite, SnapEdge.Bottom);
    }

    private void OnStackRight(object? sender, RoutedEventArgs e)
    {
        if (_manager == null) return;
        int count = _manager.GetChildren(_manager.MainWindow).Count(a => a.Edge == SnapEdge.Right) + 1;
        var stacked = CreateSatellitePanel($"Right #{count}", Colors.DarkCyan);
        stacked.Height = 200;
        stacked.Closed += (_, _) => SyncAndUpdateStatus();
        _manager.Attach(stacked, SnapEdge.Right);
    }

    private void OnDockLeft(object? sender, RoutedEventArgs e)
    {
        if (_leftSatellite != null && _manager != null)
        {
            if (_manager.IsDocked(_leftSatellite))
                _manager.Undock(_leftSatellite);
            else
                _manager.Dock(_leftSatellite, SnapEdge.Left);
        }
    }

    private void OnDockRight(object? sender, RoutedEventArgs e)
    {
        if (_rightSatellite != null && _manager != null)
        {
            if (_manager.IsDocked(_rightSatellite))
                _manager.Undock(_rightSatellite);
            else
                _manager.Dock(_rightSatellite, SnapEdge.Right);
        }
    }

    private void OnDetachLeft(object? sender, RoutedEventArgs e)
    {
        if (_leftSatellite == null || _manager == null) return;
        if (_manager.IsDocked(_leftSatellite))
            _manager.Undock(_leftSatellite);
        _manager.Detach(_leftSatellite, closeSatellite: true);
        _leftSatellite = null;
    }

    private void OnDetachRight(object? sender, RoutedEventArgs e)
    {
        if (_rightSatellite == null || _manager == null) return;
        if (_manager.IsDocked(_rightSatellite))
            _manager.Undock(_rightSatellite);
        _manager.Detach(_rightSatellite, closeSatellite: true);
        _rightSatellite = null;
    }

    private void OnDetachAll(object? sender, RoutedEventArgs e)
    {
        _manager?.DetachAll();
        _leftSatellite = null;
        _rightSatellite = null;
        LeftDockArea.Child = null; LeftDockArea.IsVisible = false;
        RightDockArea.Child = null; RightDockArea.IsVisible = false;
        _dockedContent.Clear();
    }

    // ── Status sync ─────────────────────────────────────────────────

    private void SyncAndUpdateStatus()
    {
        if (_manager == null) return;

        var leftAtt = _manager.Attachments.FirstOrDefault(a => a.Edge == SnapEdge.Left && a.Parent == _manager.MainWindow);
        var rightAtt = _manager.Attachments.FirstOrDefault(a => a.Edge == SnapEdge.Right && a.Parent == _manager.MainWindow);

        if (leftAtt != null) _leftSatellite = leftAtt.Satellite;
        if (rightAtt != null) _rightSatellite = rightAtt.Satellite;

        bool leftAttached = leftAtt != null;
        bool rightAttached = rightAtt != null;
        bool leftDocked = _leftSatellite != null && _manager.IsDocked(_leftSatellite);
        bool rightDocked = _rightSatellite != null && _manager.IsDocked(_rightSatellite);
        bool hasFloating = (_leftSatellite != null && !leftAttached && !leftDocked)
                        || (_rightSatellite != null && !rightAttached && !rightDocked);

        var parts = new List<string>();
        if (leftDocked) parts.Add("Left(docked)");
        else if (leftAttached) parts.Add("Left");
        if (rightDocked) parts.Add("Right(docked)");
        else if (rightAttached) parts.Add("Right");

        StatusText.Text = parts.Count > 0
            ? $"Satellites: {string.Join(", ", parts)}"
            : hasFloating ? "Drag near edge to re-snap" : "No satellites";

        DetachLeftBtn.IsEnabled = leftAttached || leftDocked;
        DetachRightBtn.IsEnabled = rightAttached || rightDocked;
        AttachLeftBtn.IsEnabled = _leftSatellite == null;
        AttachRightBtn.IsEnabled = _rightSatellite == null;
        AttachBottomBtn.IsEnabled = rightAttached;

        DockLeftBtn.IsEnabled = _leftSatellite != null && (leftAttached || leftDocked);
        DockLeftBtn.Content = leftDocked ? "Undock Left" : "Dock Left";
        DockRightBtn.IsEnabled = _rightSatellite != null && (rightAttached || rightDocked);
        DockRightBtn.Content = rightDocked ? "Undock Right" : "Dock Right";
    }

    private static SatelliteWindow CreateSatellitePanel(string title, Color accentColor)
    {
        return new SatelliteWindow
        {
            Title = title,
            Width = 250,
            Height = 350,
            Content = new Border
            {
                Background = new SolidColorBrush(accentColor, 0.3),
                Child = new StackPanel
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 18,
                            FontWeight = FontWeight.SemiBold,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Satellite panel content.\nDock me or snap me!",
                            TextAlignment = Avalonia.Media.TextAlignment.Center,
                            Opacity = 0.7
                        }
                    }
                }
            }
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        _manager?.Dispose();
        base.OnClosed(e);
    }
}
