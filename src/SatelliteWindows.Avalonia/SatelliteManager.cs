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
    private readonly Dictionary<SatelliteWindow, EventHandler<PixelPointEventArgs>> _satPositionHandlers = new();
    private bool _isDisposed;
    private bool _isClosingAll;
    private bool _isRepositioning;
    private int _cachedBorderPx = -1; // -1 = not yet detected

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

        // Register Opened handler BEFORE Show to avoid missing the event
        void OnOpened(object? s, EventArgs e)
        {
            satellite.Opened -= OnOpened;
            if (satellite.Manager != this) return;

            // Reposition with accurate frame size now available
            PositionSatellite(attachment);

            // Start monitoring for user drags (after final positioning)
            if (_behavior.AutoDetachOnDrag)
            {
                EventHandler<PixelPointEventArgs> posHandler = (_, _) => OnSatelliteUserMoved(satellite);
                satellite.PositionChanged += posHandler;
                _satPositionHandlers[satellite] = posHandler;
            }
        }
        satellite.Opened += OnOpened;

        // Position before showing to reduce flicker
        PositionSatellite(attachment);
        satellite.Show(_mainWindow);
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

        UnsubscribeFromSatellite(satellite);
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
                UnsubscribeFromSatellite(sat);
                sat.Attachment = null;
                sat.Manager = null;
                try { sat.Close(); }
                catch (InvalidOperationException) { /* already closed */ }
            }
            _attachments.Clear();
            _hiddenByMinimize.Clear();
            _satPositionHandlers.Clear();
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

    // ── Satellite drag-away detection ───────────────────────────────

    private void OnSatelliteUserMoved(SatelliteWindow satellite)
    {
        if (_isRepositioning || _isClosingAll) return;

        var attachment = _attachments.Find(a => a.Satellite == satellite);
        if (attachment == null) return;

        var actual = satellite.Position;
        var expected = attachment.ExpectedPosition;
        int dx = Math.Abs(actual.X - expected.X);
        int dy = Math.Abs(actual.Y - expected.Y);

        // Small differences are normal (OS rounding, platform adjustments)
        if (dx <= 5 && dy <= 5) return;

        // Beyond detach threshold — user dragged it away
        if (dx > _behavior.DetachThresholdPx || dy > _behavior.DetachThresholdPx)
        {
            Detach(satellite); // Detach but keep the window open
        }
    }

    private void UnsubscribeFromSatellite(SatelliteWindow satellite)
    {
        if (_satPositionHandlers.TryGetValue(satellite, out var handler))
        {
            satellite.PositionChanged -= handler;
            _satPositionHandlers.Remove(satellite);
        }
    }

    // ── Positioning engine ──────────────────────────────────────────

    private void RepositionAll()
    {
        foreach (var attachment in _attachments)
            PositionSatellite(attachment);
    }

    private void PositionSatellite(SatelliteAttachment attachment)
    {
        _isRepositioning = true;
        try
        {
            var parentPos = _mainWindow.Position;
            var parentSize = GetWindowPixelSize(_mainWindow);
            var satSize = GetWindowPixelSize(attachment.Satellite);
            var scaling = _mainWindow.RenderScaling;
            var offsetPx = (int)Math.Round(attachment.OffsetAlongEdge * scaling);

            var targetPos = PositionCalculator.Calculate(
                parentPos, parentSize, satSize, attachment.Edge, offsetPx);

            // Compensate for invisible frame borders (DWM shadow/resize borders on Windows)
            int borderPx = GetBorderPx();
            if (borderPx > 0 && (attachment.Edge is SnapEdge.Left or SnapEdge.Right))
            {
                int overlap = 2 * borderPx;
                targetPos = attachment.Edge == SnapEdge.Right
                    ? new PixelPoint(targetPos.X - overlap, targetPos.Y)
                    : new PixelPoint(targetPos.X + overlap, targetPos.Y);
            }

            // Clamp to visible area across all screens
            var screens = _mainWindow.Screens;
            if (screens != null)
            {
                targetPos = ScreenHelper.ClampToVisibleArea(
                    targetPos, satSize, screens, _behavior.MinVisiblePixels);
            }

            attachment.ExpectedPosition = targetPos;
            attachment.Satellite.Position = targetPos;
        }
        finally
        {
            _isRepositioning = false;
        }
    }

    // ── Frame border detection ──────────────────────────────────────

    /// <summary>
    /// Detect the non-client border thickness in pixels.
    /// On Windows, this includes the invisible DWM resize/shadow border.
    /// Returns 0 on platforms without invisible borders or if detection fails.
    /// </summary>
    private int GetBorderPx()
    {
        if (_cachedBorderPx >= 0) return _cachedBorderPx;

        try
        {
            var clientOrigin = _mainWindow.PointToScreen(new Point(0, 0));
            int leftMargin = clientOrigin.X - _mainWindow.Position.X;

            // Only apply compensation when the margin looks like a Windows DWM border
            // (typically 5-10px at 100% DPI). Avoid compensating on platforms where
            // the margin is 0 (macOS/Linux) or very large (custom chrome with padding).
            _cachedBorderPx = (leftMargin >= 3 && leftMargin <= 20) ? leftMargin : 0;
        }
        catch
        {
            _cachedBorderPx = 0;
        }

        return _cachedBorderPx;
    }

    /// <summary>
    /// Get window size in screen pixels. Uses FrameSize (includes decorations)
    /// when available, falls back to ClientSize.
    /// </summary>
    internal static PixelSize GetWindowPixelSize(Window window)
    {
        var scaling = window.RenderScaling;

        var frameSize = window.FrameSize;
        if (frameSize.HasValue)
        {
            return new PixelSize(
                (int)Math.Round(frameSize.Value.Width * scaling),
                (int)Math.Round(frameSize.Value.Height * scaling));
        }

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
