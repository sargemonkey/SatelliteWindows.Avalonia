using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SatelliteWindows.Avalonia;

namespace SatelliteDemo;

public partial class MainWindow : Window
{
    private SatelliteManager? _manager;
    private SatelliteWindow? _leftSatellite;
    private SatelliteWindow? _rightSatellite;

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

        var chained = CreateSatellitePanel("Chained (Bottom→R)", Colors.DarkGoldenrod);
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

    private void OnDetachLeft(object? sender, RoutedEventArgs e)
    {
        if (_leftSatellite == null) return;
        _manager?.Detach(_leftSatellite, closeSatellite: true);
        _leftSatellite = null;
    }

    private void OnDetachRight(object? sender, RoutedEventArgs e)
    {
        if (_rightSatellite == null) return;
        _manager?.Detach(_rightSatellite, closeSatellite: true);
        _rightSatellite = null;
    }

    private void OnDetachAll(object? sender, RoutedEventArgs e)
    {
        _manager?.DetachAll();
        _leftSatellite = null;
        _rightSatellite = null;
    }

    /// <summary>
    /// Re-discover attached satellites from the manager (handles re-snap)
    /// and update the UI.
    /// </summary>
    private void SyncAndUpdateStatus()
    {
        if (_manager == null) return;

        // Sync references: filter by direct children of main window only
        var leftAtt = _manager.Attachments.FirstOrDefault(a => a.Edge == SnapEdge.Left && a.Parent == _manager.MainWindow);
        var rightAtt = _manager.Attachments.FirstOrDefault(a => a.Edge == SnapEdge.Right && a.Parent == _manager.MainWindow);

        if (leftAtt != null) _leftSatellite = leftAtt.Satellite;
        if (rightAtt != null) _rightSatellite = rightAtt.Satellite;

        bool leftAttached = leftAtt != null;
        bool rightAttached = rightAtt != null;
        bool hasFloating = (_leftSatellite != null && !leftAttached)
                        || (_rightSatellite != null && !rightAttached);

        var parts = new List<string>();
        if (leftAttached) parts.Add("Left");
        if (rightAttached) parts.Add("Right");

        StatusText.Text = parts.Count > 0
            ? $"Attached: {string.Join(", ", parts)}"
            : hasFloating
                ? "Drag near edge to re-snap"
                : "No satellites attached";

        DetachLeftBtn.IsEnabled = leftAttached;
        DetachRightBtn.IsEnabled = rightAttached;
        AttachLeftBtn.IsEnabled = _leftSatellite == null;
        AttachRightBtn.IsEnabled = _rightSatellite == null;
        AttachBottomBtn.IsEnabled = rightAttached;
    }

    private static SatelliteWindow CreateSatellitePanel(string title, Color accentColor)
    {
        var satellite = new SatelliteWindow
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
                            Text = "This is a satellite window.\nMove the main window — I follow!",
                            TextAlignment = Avalonia.Media.TextAlignment.Center,
                            Opacity = 0.7
                        }
                    }
                }
            }
        };

        return satellite;
    }

    protected override void OnClosed(EventArgs e)
    {
        _manager?.Dispose();
        base.OnClosed(e);
    }
}
