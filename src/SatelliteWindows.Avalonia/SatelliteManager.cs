using Avalonia;
using Avalonia.Controls;
using SatelliteWindows.Avalonia.Internal;

namespace SatelliteWindows.Avalonia;

/// <summary>
/// Manages satellite windows attached to a main window.
/// Handles positioning, follow-on-move, follow-on-resize,
/// minimize/restore synchronization, and close propagation.
/// </summary>
public sealed class SatelliteManager : IDisposable
{
    private readonly Window _mainWindow;
    private readonly SnapBehavior _behavior;
    private readonly List<SatelliteAttachment> _attachments = new();
    private readonly HashSet<SatelliteWindow> _hiddenByMinimize = new();
    private bool _isDisposed;
    private bool _isClosingAll;

    public SatelliteManager(Window mainWindow, SnapBehavior? behavior = null)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _behavior = behavior ?? new SnapBehavior();
        SubscribeToMainWindow();
    }

    /// <summary>Current attachments (read-only view).</summary>
    public IReadOnlyList<SatelliteAttachment> Attachments => _attachments.AsReadOnly();

    /// <summary>Active snap behavior configuration.</summary>
    public SnapBehavior Behavior => _behavior;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Attach a satellite to the main window's edge.
    /// The satellite is positioned and shown automatically.
    /// </summary>
    public void Attach(SatelliteWindow satellite, SnapEdge edge, double offsetAlongEdge = 0)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        ThrowIfDisposed();

        if (satellite.Manager != null)
            throw new InvalidOperationException("Satellite is already attached to a manager.");

        var attachment = new SatelliteAttachment(satellite, _mainWindow, edge, offsetAlongEdge);
        satellite.Attachment = attachment;
        satellite.Manager = this;
        _attachments.Add(attachment);

        // Position before showing to reduce flicker
        PositionSatellite(attachment);
        satellite.Show(_mainWindow);

        // Reposition after opened — frame size is now known accurately
        void OnOpened(object? s, EventArgs e)
        {
            satellite.Opened -= OnOpened;
            if (satellite.Manager == this)
                PositionSatellite(attachment);
        }
        satellite.Opened += OnOpened;
    }

    /// <summary>
    /// Detach a satellite. Does not close the satellite window unless
    /// <paramref name="closeSatellite"/> is true.
    /// </summary>
    public void Detach(SatelliteWindow satellite, bool closeSatellite = false)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        if (_isClosingAll) return;

        var attachment = _attachments.Find(a => a.Satellite == satellite);
        if (attachment == null) return;

        _attachments.Remove(attachment);
        _hiddenByMinimize.Remove(satellite);
        satellite.Attachment = null;
        satellite.Manager = null;

        if (closeSatellite)
        {
            try { satellite.Close(); }
            catch (InvalidOperationException) { /* already closed */ }
        }
    }

    /// <summary>Close and detach all satellites.</summary>
    public void DetachAll()
    {
        _isClosingAll = true;
        try
        {
            foreach (var attachment in _attachments.ToArray())
            {
                var sat = attachment.Satellite;
                sat.Attachment = null;
                sat.Manager = null;
                try { sat.Close(); }
                catch (InvalidOperationException) { /* already closed */ }
            }
            _attachments.Clear();
            _hiddenByMinimize.Clear();
        }
        finally
        {
            _isClosingAll = false;
        }
    }

    // ── Main-window event handlers ──────────────────────────────────

    private void SubscribeToMainWindow()
    {
        _mainWindow.PositionChanged += OnMainPositionChanged;
        _mainWindow.Closing += OnMainClosing;
        _mainWindow.PropertyChanged += OnMainPropertyChanged;
    }

    private void UnsubscribeFromMainWindow()
    {
        _mainWindow.PositionChanged -= OnMainPositionChanged;
        _mainWindow.Closing -= OnMainClosing;
        _mainWindow.PropertyChanged -= OnMainPropertyChanged;
    }

    private void OnMainPositionChanged(object? sender, PixelPointEventArgs e)
    {
        RepositionAll();
    }

    private void OnMainPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty && _behavior.FollowOnResize)
        {
            RepositionAll();
        }
        else if (e.Property == Window.WindowStateProperty && e.NewValue is WindowState state)
        {
            OnMainWindowStateChanged(state);
        }
    }

    private void OnMainWindowStateChanged(WindowState state)
    {
        if (!_behavior.MinimizeWithMain) return;

        if (state == WindowState.Minimized)
        {
            // Hide all visible satellites and remember which ones we hid
            foreach (var attachment in _attachments.ToArray())
            {
                if (attachment.Satellite.IsVisible)
                {
                    _hiddenByMinimize.Add(attachment.Satellite);
                    attachment.Satellite.Hide();
                }
            }
        }
        else if (_hiddenByMinimize.Count > 0)
        {
            // Restore only the satellites we hid (not manually-hidden ones)
            foreach (var satellite in _hiddenByMinimize.ToArray())
            {
                if (satellite.Manager == this)
                    satellite.Show(_mainWindow);
            }
            _hiddenByMinimize.Clear();
            RepositionAll();
        }
    }

    private void OnMainClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!e.Cancel && _behavior.CloseWithMain)
            DetachAll();
    }

    // ── Positioning engine ──────────────────────────────────────────

    private void RepositionAll()
    {
        foreach (var attachment in _attachments)
            PositionSatellite(attachment);
    }

    private void PositionSatellite(SatelliteAttachment attachment)
    {
        var parentPos = _mainWindow.Position;
        var parentSize = GetWindowPixelSize(_mainWindow);
        var satSize = GetWindowPixelSize(attachment.Satellite);
        var scaling = _mainWindow.RenderScaling;
        var offsetPx = (int)Math.Round(attachment.OffsetAlongEdge * scaling);

        var targetPos = PositionCalculator.Calculate(
            parentPos, parentSize, satSize, attachment.Edge, offsetPx);

        // Clamp to visible area across all screens
        var screens = _mainWindow.Screens;
        if (screens != null)
        {
            targetPos = ScreenHelper.ClampToVisibleArea(
                targetPos, satSize, screens, _behavior.MinVisiblePixels);
        }

        attachment.Satellite.Position = targetPos;
    }

    /// <summary>
    /// Get window size in screen pixels. Uses FrameSize (includes decorations)
    /// when available, falls back to ClientSize.
    /// </summary>
    internal static PixelSize GetWindowPixelSize(Window window)
    {
        var scaling = window.RenderScaling;

        // FrameSize includes window chrome, in logical units
        var frameSize = window.FrameSize;
        if (frameSize.HasValue)
        {
            return new PixelSize(
                (int)Math.Round(frameSize.Value.Width * scaling),
                (int)Math.Round(frameSize.Value.Height * scaling));
        }

        // Fallback: client area only (may cause small gaps with decorations)
        return DpiHelper.LogicalToPixel(window.ClientSize, scaling);
    }

    // ── Lifecycle ───────────────────────────────────────────────────

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        UnsubscribeFromMainWindow();
        DetachAll();
    }
}
