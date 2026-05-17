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
    }

    private void OnAttachLeft(object? sender, RoutedEventArgs e)
    {
        if (_manager == null || _leftSatellite != null) return;

        _leftSatellite = CreateSatellitePanel("Left Panel", Colors.DarkSlateBlue);
        _manager.Attach(_leftSatellite, SnapEdge.Left);
        _leftSatellite.Closed += (_, _) =>
        {
            _leftSatellite = null;
            UpdateStatus();
        };

        UpdateStatus();
    }

    private void OnAttachRight(object? sender, RoutedEventArgs e)
    {
        if (_manager == null || _rightSatellite != null) return;

        _rightSatellite = CreateSatellitePanel("Right Panel", Colors.DarkOliveGreen);
        _manager.Attach(_rightSatellite, SnapEdge.Right);
        _rightSatellite.Closed += (_, _) =>
        {
            _rightSatellite = null;
            UpdateStatus();
        };

        UpdateStatus();
    }

    private void OnDetachLeft(object? sender, RoutedEventArgs e)
    {
        if (_leftSatellite == null) return;
        _manager?.Detach(_leftSatellite, closeSatellite: true);
        _leftSatellite = null;
        UpdateStatus();
    }

    private void OnDetachRight(object? sender, RoutedEventArgs e)
    {
        if (_rightSatellite == null) return;
        _manager?.Detach(_rightSatellite, closeSatellite: true);
        _rightSatellite = null;
        UpdateStatus();
    }

    private void OnDetachAll(object? sender, RoutedEventArgs e)
    {
        _manager?.DetachAll();
        _leftSatellite = null;
        _rightSatellite = null;
        UpdateStatus();
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

    private void UpdateStatus()
    {
        var parts = new List<string>();
        if (_leftSatellite is { IsAttached: true }) parts.Add("Left");
        if (_rightSatellite is { IsAttached: true }) parts.Add("Right");

        StatusText.Text = parts.Count > 0
            ? $"Attached: {string.Join(", ", parts)}"
            : "No satellites attached";

        DetachLeftBtn.IsEnabled = _leftSatellite is { IsAttached: true };
        DetachRightBtn.IsEnabled = _rightSatellite is { IsAttached: true };
        AttachLeftBtn.IsEnabled = _leftSatellite == null;
        AttachRightBtn.IsEnabled = _rightSatellite == null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _manager?.Dispose();
        base.OnClosed(e);
    }
}
