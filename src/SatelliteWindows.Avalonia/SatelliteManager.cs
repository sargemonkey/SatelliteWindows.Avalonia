using Avalonia;
using Avalonia.Controls;
using SatelliteWindows.Avalonia.Internal;

namespace SatelliteWindows.Avalonia;

/// <summary>
/// Manages satellite windows attached to a main window.
/// Supports snap chaining (satellite-to-satellite trees), edge stacking,
/// magnetic snap to any managed window, drag-away detach, and persist/restore.
/// </summary>
public sealed class SatelliteManager : IDisposable
{
    private readonly Window _mainWindow;
    private readonly SnapBehavior _behavior;
    private readonly List<SatelliteAttachment> _allAttachments = new();
    private readonly Dictionary<Window, List<SatelliteAttachment>> _childMap = new();
    private readonly HashSet<SatelliteWindow> _hiddenByMinimize = new();
    private readonly Dictionary<SatelliteWindow, EventHandler<PixelPointEventArgs>> _satPositionHandlers = new();
    private readonly Dictionary<SatelliteWindow, (EventHandler<PixelPointEventArgs> pos, EventHandler closed)> _reSnapHandlers = new();
    private bool _isDisposed;
    private bool _isClosingAll;
    private bool _isRepositioning;
    private int _cachedBorderPx = -1;

    public SatelliteManager(Window mainWindow, SnapBehavior? behavior = null)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _behavior = behavior ?? new SnapBehavior();
        SubscribeToMainWindow();
    }

    /// <summary>All attachments in the tree (read-only).</summary>
    public IReadOnlyList<SatelliteAttachment> Attachments => _allAttachments.AsReadOnly();

    /// <summary>Active snap behavior configuration.</summary>
    public SnapBehavior Behavior => _behavior;

    /// <summary>The main window this manager is attached to.</summary>
    public Window MainWindow => _mainWindow;

    /// <summary>Fired on any attachment topology change.</summary>
    public event Action? AttachmentChanged;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>Attach a satellite to the main window''s edge.</summary>
    public void Attach(SatelliteWindow satellite, SnapEdge edge, double offsetAlongEdge = 0)
        => AttachCore(satellite, _mainWindow, edge, offsetAlongEdge);

    /// <summary>Attach a satellite to another satellite''s edge (chaining).</summary>
    public void Attach(SatelliteWindow satellite, SatelliteWindow parent, SnapEdge edge, double offsetAlongEdge = 0)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent.Manager != this)
            throw new InvalidOperationException("Parent satellite is not managed by this manager.");
        AttachCore(satellite, parent, edge, offsetAlongEdge);
    }

    /// <summary>Detach a satellite (backward-compatible, defaults to DetachChain).</summary>
    public void Detach(SatelliteWindow satellite, bool closeSatellite = false)
        => DetachCore(satellite, DetachMode.DetachChain, closeSatellite);

    /// <summary>Detach a satellite with explicit chain handling.</summary>
    public void Detach(SatelliteWindow satellite, DetachMode mode, bool closeSatellite = false)
        => DetachCore(satellite, mode, closeSatellite);

    /// <summary>Close and detach all satellites.</summary>
    public void DetachAll()
    {
        _isClosingAll = true;
        try
        {
            foreach (var sat in _reSnapHandlers.Keys.ToArray())
                StopReSnapMonitoring(sat);

            foreach (var att in _allAttachments.ToArray())
            {
                UnsubscribeFromSatellite(att.Satellite);
                att.Satellite.Attachment = null;
                att.Satellite.Manager = null;
                try { att.Satellite.Close(); }
                catch (InvalidOperationException) { }
            }
            _allAttachments.Clear();
            _childMap.Clear();
            _hiddenByMinimize.Clear();
            _satPositionHandlers.Clear();
        }
        finally
        {
            _isClosingAll = false;
        }
        AttachmentChanged?.Invoke();
    }

    /// <summary>Get direct children of a window in the snap tree.</summary>
    public IReadOnlyList<SatelliteAttachment> GetChildren(Window parent)
    {
        if (_childMap.TryGetValue(parent, out var children))
            return children.ToArray();
        return Array.Empty<SatelliteAttachment>();
    }

    // ── Persist / Restore ───────────────────────────────────────────

    /// <summary>Serialize the attachment tree. All satellites must have SatelliteId set.</summary>
    public AttachmentState[] SaveState()
    {
        return _allAttachments.Select(a => new AttachmentState(
            a.Satellite.SatelliteId ?? throw new InvalidOperationException("Satellite must have SatelliteId to save state."),
            a.Parent == _mainWindow ? null : ((SatelliteWindow)a.Parent).SatelliteId,
            a.Edge,
            a.OffsetAlongEdge,
            a.Satellite.Width,
            a.Satellite.Height
        )).ToArray();
    }

    /// <summary>Restore attachment tree from saved state. Creates windows via factory.</summary>
    public void RestoreState(AttachmentState[] state, Func<string, SatelliteWindow> windowFactory)
    {
        DetachAll();
        var windows = new Dictionary<string, SatelliteWindow>();
        foreach (var s in state)
        {
            var sw = windowFactory(s.Id);
            sw.SatelliteId = s.Id;
            sw.Width = s.Width;
            sw.Height = s.Height;
            windows[s.Id] = sw;
        }

        var attached = new HashSet<string>();
        bool progress = true;
        while (progress)
        {
            progress = false;
            foreach (var s in state)
            {
                if (attached.Contains(s.Id)) continue;
                if (s.ParentId != null && !attached.Contains(s.ParentId)) continue;

                var satellite = windows[s.Id];
                if (s.ParentId == null)
                    Attach(satellite, s.Edge, s.OffsetAlongEdge);
                else
                    Attach(satellite, windows[s.ParentId], s.Edge, s.OffsetAlongEdge);
                attached.Add(s.Id);
                progress = true;
            }
        }
    }

    // ── Core attach / detach ────────────────────────────────────────

    private void AttachCore(SatelliteWindow satellite, Window parent, SnapEdge edge, double offset)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        ThrowIfDisposed();

        if (satellite.Manager != null)
            throw new InvalidOperationException("Satellite is already attached to a manager.");
        if (satellite == parent)
            throw new InvalidOperationException("Cannot attach a satellite to itself.");
        if (parent is SatelliteWindow && IsDescendant(parent, satellite))
            throw new InvalidOperationException("Cannot attach to a descendant (would create cycle).");
        if (_behavior.ChainDepthLimit >= 0 && GetDepth(parent) + 1 > _behavior.ChainDepthLimit)
            throw new InvalidOperationException("Chain depth limit exceeded.");

        StopReSnapMonitoring(satellite);

        // Auto-stack: if offset is 0 and siblings exist on this edge, stack after them
        double effectiveOffset = offset;
        if (offset == 0)
            effectiveOffset = CalculateStackOffset(parent, edge);

        var attachment = new SatelliteAttachment(satellite, parent, edge, effectiveOffset);
        attachment.AttachedAt = DateTime.UtcNow;
        satellite.Attachment = attachment;
        satellite.Manager = this;
        AddToTree(attachment);

        PositionSatellite(attachment);

        if (!satellite.IsVisible)
        {
            // Initial attach — no cooldown needed
            void OnOpened(object? s, EventArgs e)
            {
                satellite.Opened -= OnOpened;
                if (satellite.Manager != this) return;
                PositionSatellite(attachment);
                SubscribeDragDetection(satellite);
            }
            satellite.Opened += OnOpened;
            satellite.Show(_mainWindow);
        }
        else
        {
            // Re-snap — mark for cooldown resistance
            attachment.IsReSnap = true;
            SubscribeDragDetection(satellite);
        }

        AttachmentChanged?.Invoke();
    }

    private void DetachCore(SatelliteWindow satellite, DetachMode mode, bool closeSatellite)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        if (_isClosingAll) return;

        var attachment = _allAttachments.Find(a => a.Satellite == satellite);
        if (attachment == null) return;

        if (mode == DetachMode.ReparentChildren)
        {
            foreach (var child in GetChildren(satellite).ToArray())
            {
                RemoveFromTree(child);
                child.Parent = attachment.Parent;
                AddToTree(child);
            }
        }
        else
        {
            CloseSubtree(satellite);
        }

        UnsubscribeFromSatellite(satellite);
        RemoveFromTree(attachment);
        _hiddenByMinimize.Remove(satellite);
        satellite.Attachment = null;
        satellite.Manager = null;

        if (closeSatellite)
        {
            try { satellite.Close(); }
            catch (InvalidOperationException) { }
        }

        if (mode == DetachMode.ReparentChildren)
            RepositionSubtree(attachment.Parent);

        AttachmentChanged?.Invoke();
    }

    private void CloseSubtree(SatelliteWindow root)
    {
        foreach (var child in GetChildren(root).ToArray())
        {
            CloseSubtree(child.Satellite);
            UnsubscribeFromSatellite(child.Satellite);
            RemoveFromTree(child);
            child.Satellite.Attachment = null;
            child.Satellite.Manager = null;
            try { child.Satellite.Close(); }
            catch (InvalidOperationException) { }
        }
    }

    // ── Tree mutation helpers ────────────────────────────────────────

    private void AddToTree(SatelliteAttachment attachment)
    {
        _allAttachments.Add(attachment);
        if (!_childMap.TryGetValue(attachment.Parent, out var siblings))
        {
            siblings = new List<SatelliteAttachment>();
            _childMap[attachment.Parent] = siblings;
        }
        siblings.Add(attachment);
    }

    private void RemoveFromTree(SatelliteAttachment attachment)
    {
        _allAttachments.Remove(attachment);
        if (_childMap.TryGetValue(attachment.Parent, out var siblings))
        {
            siblings.Remove(attachment);
            if (siblings.Count == 0)
                _childMap.Remove(attachment.Parent);
        }
    }

    private int GetDepth(Window window)
    {
        int depth = 0;
        var current = window;
        while (current != _mainWindow)
        {
            var att = _allAttachments.Find(a => a.Satellite == current);
            if (att == null) break;
            current = att.Parent;
            depth++;
            if (depth > 100) break;
        }
        return depth;
    }

    private bool IsDescendant(Window candidate, Window ancestor)
    {
        if (!_childMap.TryGetValue(ancestor, out var children)) return false;
        foreach (var child in children)
        {
            if (child.Satellite == candidate) return true;
            if (IsDescendant(candidate, child.Satellite)) return true;
        }
        return false;
    }

    private double CalculateStackOffset(Window parent, SnapEdge edge)
    {
        if (!_childMap.TryGetValue(parent, out var children)) return 0;
        double maxExtent = 0;
        foreach (var child in children)
        {
            if (child.Edge != edge) continue;
            double size = (edge is SnapEdge.Left or SnapEdge.Right)
                ? child.Satellite.Height
                : child.Satellite.Width;
            maxExtent = Math.Max(maxExtent, child.OffsetAlongEdge + size);
        }
        return maxExtent;
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
        => RepositionAll();

    private void OnMainPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty && _behavior.FollowOnResize)
            RepositionAll();
        else if (e.Property == Window.WindowStateProperty && e.NewValue is WindowState state)
            OnMainWindowStateChanged(state);
    }

    private void OnMainWindowStateChanged(WindowState state)
    {
        if (!_behavior.MinimizeWithMain) return;

        if (state == WindowState.Minimized)
        {
            foreach (var att in _allAttachments.ToArray())
            {
                if (att.Satellite.IsVisible)
                {
                    _hiddenByMinimize.Add(att.Satellite);
                    att.Satellite.Hide();
                }
            }
        }
        else if (_hiddenByMinimize.Count > 0)
        {
            foreach (var sat in _hiddenByMinimize.ToArray())
            {
                if (sat.Manager == this)
                    sat.Show(_mainWindow);
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

    // ── Drag-away detection ─────────────────────────────────────────

    private void SubscribeDragDetection(SatelliteWindow satellite)
    {
        if (!_behavior.AutoDetachOnDrag) return;
        UnsubscribeFromSatellite(satellite);
        EventHandler<PixelPointEventArgs> handler = (_, _) => OnSatelliteUserMoved(satellite);
        satellite.PositionChanged += handler;
        _satPositionHandlers[satellite] = handler;
    }

    private void OnSatelliteUserMoved(SatelliteWindow satellite)
    {
        if (_isRepositioning || _isClosingAll) return;

        var attachment = _allAttachments.Find(a => a.Satellite == satellite);
        if (attachment == null) return;

        // Cooldown: only for re-snaps (magnetic drag-back), pin to snap position briefly
        if (attachment.IsReSnap && (DateTime.UtcNow - attachment.AttachedAt).TotalMilliseconds < 300)
        {
            PositionSatellite(attachment);
            return;
        }
        attachment.IsReSnap = false;

        var actual = satellite.Position;
        var expected = attachment.ExpectedPosition;
        int dx = actual.X - expected.X;
        int dy = actual.Y - expected.Y;

        // Decompose into perpendicular (away from edge) and parallel (along edge)
        int perpendicular, parallel;
        if (attachment.Edge is SnapEdge.Left or SnapEdge.Right)
        {
            perpendicular = Math.Abs(dx);
            parallel = dy;
        }
        else
        {
            perpendicular = Math.Abs(dy);
            parallel = dx;
        }

        // Perpendicular exceeds threshold → detach
        if (perpendicular > _behavior.DetachThresholdPx)
        {
            DetachCore(satellite, DetachMode.ReparentChildren, closeSatellite: false);
            if (_behavior.AutoSnapOnDrag)
                StartReSnapMonitoring(satellite);
            return;
        }

        // Parallel movement → slide along edge (update offset, don't fight the drag)
        if (Math.Abs(parallel) > 3)
        {
            var scaling = attachment.Parent.RenderScaling;
            attachment.OffsetAlongEdge += parallel / scaling;
            // Accept user's position — avoids fighting the OS drag.
            // Perpendicular snap corrects on next main window move.
            attachment.ExpectedPosition = satellite.Position;
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

    // ── Magnetic re-snap monitoring ─────────────────────────────────

    private readonly Dictionary<SatelliteWindow, DateTime> _detachedAt = new();

    private void StartReSnapMonitoring(SatelliteWindow satellite)
    {
        if (_reSnapHandlers.ContainsKey(satellite)) return;
        _detachedAt[satellite] = DateTime.UtcNow;
        EventHandler<PixelPointEventArgs> posHandler = (_, _) => OnDetachedSatelliteMoved(satellite);
        EventHandler closedHandler = (_, _) => StopReSnapMonitoring(satellite);
        satellite.PositionChanged += posHandler;
        satellite.Closed += closedHandler;
        _reSnapHandlers[satellite] = (posHandler, closedHandler);
    }

    private void StopReSnapMonitoring(SatelliteWindow satellite)
    {
        _detachedAt.Remove(satellite);
        if (_reSnapHandlers.TryGetValue(satellite, out var h))
        {
            satellite.PositionChanged -= h.pos;
            satellite.Closed -= h.closed;
            _reSnapHandlers.Remove(satellite);
        }
    }

    private void OnDetachedSatelliteMoved(SatelliteWindow satellite)
    {
        if (_isClosingAll || _isDisposed) return;
        if (!satellite.IsVisible) { StopReSnapMonitoring(satellite); return; }

        // Don't re-snap immediately after detach — prevents stutter loop
        if (_detachedAt.TryGetValue(satellite, out var dt)
            && (DateTime.UtcNow - dt).TotalMilliseconds < 500)
            return;

        var snap = DetectNearestSnap(satellite);
        if (snap == null) return;

        if (snap.Value.parent == _mainWindow)
            Attach(satellite, snap.Value.edge);
        else if (snap.Value.parent is SatelliteWindow parentSat)
            Attach(satellite, parentSat, snap.Value.edge);
    }

    private (Window parent, SnapEdge edge)? DetectNearestSnap(SatelliteWindow satellite)
    {
        var satPos = satellite.Position;
        var satSize = GetWindowPixelSize(satellite);
        int threshold = _behavior.MagneticThresholdPx;
        int overlap = GetFrameOverlapPx();

        double bestDist = double.MaxValue;
        (Window parent, SnapEdge edge)? best = null;

        var candidates = new List<Window> { _mainWindow };
        foreach (var att in _allAttachments)
        {
            if (att.Satellite != satellite && !IsDescendant(att.Satellite, satellite))
                candidates.Add(att.Satellite);
        }

        foreach (var cand in candidates)
        {
            var pPos = cand.Position;
            var pSize = GetWindowPixelSize(cand);

            // For Left/Right edges: require sufficient vertical overlap
            double vertOverlapRatio = CalculateOverlapRatio(
                satPos.Y, satSize.Height, pPos.Y, pSize.Height);

            // For Top/Bottom edges: require sufficient horizontal overlap
            double horizOverlapRatio = CalculateOverlapRatio(
                satPos.X, satSize.Width, pPos.X, pSize.Width);

            if (vertOverlapRatio >= _behavior.SnapOverlapRatio)
            {
                CheckEdge(pPos.X + pSize.Width - overlap, satPos.X, cand, SnapEdge.Right, ref bestDist, ref best, threshold);
                CheckEdge(pPos.X - satSize.Width + overlap, satPos.X, cand, SnapEdge.Left, ref bestDist, ref best, threshold);
            }
            if (horizOverlapRatio >= _behavior.SnapOverlapRatio)
            {
                CheckEdge(pPos.Y + pSize.Height - overlap, satPos.Y, cand, SnapEdge.Bottom, ref bestDist, ref best, threshold);
                CheckEdge(pPos.Y - satSize.Height + overlap, satPos.Y, cand, SnapEdge.Top, ref bestDist, ref best, threshold);
            }
        }

        return best;
    }

    private static void CheckEdge(int snapCoord, int actualCoord, Window parent, SnapEdge edge,
        ref double bestDist, ref (Window, SnapEdge)? best, int threshold)
    {
        int dist = Math.Abs(actualCoord - snapCoord);
        if (dist <= threshold && dist < bestDist)
        {
            bestDist = dist;
            best = (parent, edge);
        }
    }

    /// <summary>
    /// Calculate what fraction of the smaller window overlaps the larger along one axis.
    /// Returns 0.0 (no overlap) to 1.0 (fully contained).
    /// </summary>
    private static double CalculateOverlapRatio(int aPos, int aSize, int bPos, int bSize)
    {
        int overlapStart = Math.Max(aPos, bPos);
        int overlapEnd = Math.Min(aPos + aSize, bPos + bSize);
        int overlap = Math.Max(0, overlapEnd - overlapStart);
        int smaller = Math.Min(aSize, bSize);
        return smaller > 0 ? (double)overlap / smaller : 0;
    }

    // ── Positioning engine ──────────────────────────────────────────

    private void RepositionAll()
    {
        _isRepositioning = true;
        try { RepositionSubtreeCore(_mainWindow); }
        finally { _isRepositioning = false; }
    }

    private void RepositionSubtree(Window parent)
    {
        _isRepositioning = true;
        try { RepositionSubtreeCore(parent); }
        finally { _isRepositioning = false; }
    }

    private void RepositionSubtreeCore(Window parent)
    {
        if (!_childMap.TryGetValue(parent, out var children)) return;
        foreach (var child in children)
        {
            PositionSatelliteCore(child);
            RepositionSubtreeCore(child.Satellite);
        }
    }

    private void PositionSatellite(SatelliteAttachment attachment)
    {
        _isRepositioning = true;
        try
        {
            PositionSatelliteCore(attachment);
            RepositionSubtreeCore(attachment.Satellite);
        }
        finally { _isRepositioning = false; }
    }

    private void PositionSatelliteCore(SatelliteAttachment attachment)
    {
        var parent = attachment.Parent;
        var parentPos = parent.Position;
        var parentSize = GetWindowPixelSize(parent);
        var satSize = GetWindowPixelSize(attachment.Satellite);
        var scaling = parent.RenderScaling;
        var offsetPx = (int)Math.Round(attachment.OffsetAlongEdge * scaling);

        var targetPos = PositionCalculator.Calculate(
            parentPos, parentSize, satSize, attachment.Edge, offsetPx);

        if (attachment.Edge is SnapEdge.Left or SnapEdge.Right)
        {
            int ol = GetFrameOverlapPx();
            targetPos = attachment.Edge == SnapEdge.Right
                ? new PixelPoint(targetPos.X - ol, targetPos.Y)
                : new PixelPoint(targetPos.X + ol, targetPos.Y);
        }
        else if (attachment.Edge is SnapEdge.Top or SnapEdge.Bottom)
        {
            int ol = GetFrameOverlapPx();
            targetPos = attachment.Edge == SnapEdge.Bottom
                ? new PixelPoint(targetPos.X, targetPos.Y - ol)
                : new PixelPoint(targetPos.X, targetPos.Y + ol);
        }

        var screens = _mainWindow.Screens;
        if (screens != null)
            targetPos = ScreenHelper.ClampToVisibleArea(targetPos, satSize, screens, _behavior.MinVisiblePixels);

        attachment.ExpectedPosition = targetPos;
        attachment.Satellite.Position = targetPos;
    }

    // ── Frame border detection ──────────────────────────────────────

    private int GetFrameOverlapPx()
    {
        int b = GetBorderPx();
        return Math.Max(0, 2 * b - 2);
    }

    private int GetBorderPx()
    {
        if (_cachedBorderPx >= 0) return _cachedBorderPx;
        try
        {
            var co = _mainWindow.PointToScreen(new Point(0, 0));
            int m = co.X - _mainWindow.Position.X;
            _cachedBorderPx = (m >= 3 && m <= 20) ? m : 0;
        }
        catch { _cachedBorderPx = 0; }
        return _cachedBorderPx;
    }

    internal static PixelSize GetWindowPixelSize(Window window)
    {
        var s = window.RenderScaling;
        var fs = window.FrameSize;
        if (fs.HasValue)
            return new PixelSize((int)Math.Round(fs.Value.Width * s), (int)Math.Round(fs.Value.Height * s));
        return DpiHelper.LogicalToPixel(window.ClientSize, s);
    }

    // ── Lifecycle ───────────────────────────────────────────────────

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        UnsubscribeFromMainWindow();
        foreach (var sat in _reSnapHandlers.Keys.ToArray())
            StopReSnapMonitoring(sat);
        DetachAll();
    }
}
