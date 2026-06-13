using Avalonia;
using Avalonia.Controls;
using SatelliteWindows.Avalonia.Internal;

namespace SatelliteWindows.Avalonia;

/// <summary>
/// Manages satellite windows attached to a main window.
/// Supports snap chaining (satellite-to-satellite trees), edge stacking,
/// magnetic snap to any managed window, drag-away detach, and persist/restore.
///
/// <para><b>Role-preservation contract:</b> when a <see cref="SatelliteWindow"/>
/// has <see cref="SatelliteWindow.Role"/> == <see cref="WindowRole.DockHost"/>,
/// the manager preserves that role across
/// <see cref="Attach(SatelliteWindow, SnapEdge, double)"/> /
/// <see cref="Detach(SatelliteWindow, bool)"/> /
/// <see cref="AttachFloating"/> instead of flipping to
/// <see cref="WindowRole.Satellite"/> or <see cref="WindowRole.Floating"/>.
/// This keeps the Dock framework's <c>ControlTheme</c> (chrome grips, tab
/// strip, drag-back-to-dock detection) intact while the window is also being
/// snap-managed. Auto-snap on drag is also skipped for <c>DockHost</c> windows
/// because it interferes with Dock's drag-back-to-dock detection (the satellite
/// would snap to a screen edge before the cursor could reach a drop zone).
/// This is a deliberate cross-concern between the snap library and the
/// vendored Dock framework — see PLAN-tri-role-windows.md for the rationale.</para>
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
    private readonly Dictionary<SatelliteWindow, SnapEdge> _dockedSatellites = new();
    private readonly Dictionary<SatelliteWindow, DateTime> _detachedAt = new();
    private readonly Dictionary<SatelliteWindow, EventHandler> _floatingTracked = new();
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

    /// <summary>
    /// Track a window in <see cref="WindowRole.Floating"/> mode — no snap, no edge
    /// positioning, but the manager still owns its lifecycle (close-with-main,
    /// minimize-with-main when <see cref="SnapBehavior.MinimizeWithMain"/> is on).
    /// Use this to give a free-floating tool window the same lifecycle coupling as
    /// snapped satellites without the magnetic behaviour.
    /// </summary>
    public void AttachFloating(SatelliteWindow satellite)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        ThrowIfDisposed();

        if (satellite.Manager != null)
            throw new InvalidOperationException("Satellite is already attached to a manager.");

        satellite.Manager = this;
        // Preserve DockHost role for Dock-framework-owned windows so they keep
        // HostWindow's ControlTheme (which renders the dragged-out content via
        // the dock tab strip + DockControl). For non-dock-host windows, set
        // Floating so SatelliteWindow's StyleKeyOverride falls back to the
        // plain Window theme.
        if (satellite.Role != WindowRole.DockHost)
            satellite.Role = WindowRole.Floating;

        EventHandler onClosed = (_, _) => OnFloatingClosed(satellite);
        satellite.Closed += onClosed;
        _floatingTracked[satellite] = onClosed;

        // Enable magnetic snap on user drag if behavior allows. AttachFloating
        // previously only wired follow-main-on-move (via _floatingTracked); the
        // snap-near-edge gesture lived only in the post-Detach code path. For
        // floating windows that were never Attach()'d (e.g. drag-outs that
        // started life as floating), we need explicit re-snap monitoring too.
        //
        // EXCEPTION: skip for DockHost role. Auto-snap during a Dock-framework
        // drag interferes with Dock's drag-back-to-dock detection — as soon as
        // the window enters MagneticThresholdPx of an edge it snaps, freezing
        // the cursor before it can reach the dock's drop zones. DockHost windows
        // get snap-on-drag via the explicit right-click "Snap to Edge" menu
        // instead, leaving the native drag flow intact for drag-back-to-dock.
        if (_behavior.AutoSnapOnDrag && satellite.Role != WindowRole.DockHost)
            StartReSnapMonitoring(satellite);

        if (!satellite.IsVisible)
            satellite.Show(_mainWindow);

        AttachmentChanged?.Invoke();
    }

    /// <summary>Stop tracking a floating-mode window. Reverses <see cref="AttachFloating"/>.</summary>
    public void DetachFloating(SatelliteWindow satellite, bool closeSatellite = false)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        if (!_floatingTracked.TryGetValue(satellite, out var onClosed)) return;

        satellite.Closed -= onClosed;
        _floatingTracked.Remove(satellite);
        satellite.Manager = null;
        // Role stays Floating — that's the rest state.

        // Mirror AttachFloating's resnap-monitoring setup teardown.
        StopReSnapMonitoring(satellite);

        if (closeSatellite)
        {
            FlushContentForReparent(satellite);
            try { satellite.Close(); }
            catch (InvalidOperationException) { }
        }
        AttachmentChanged?.Invoke();
    }

    private void OnFloatingClosed(SatelliteWindow satellite)
    {
        if (_isClosingAll || _isDisposed) return;
        if (_floatingTracked.Remove(satellite, out _))
        {
            satellite.Manager = null;
            AttachmentChanged?.Invoke();
        }
    }

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
                att.Satellite.Role = WindowRole.Floating;
                FlushContentForReparent(att.Satellite);
                try { att.Satellite.Close(); }
                catch (InvalidOperationException) { }
            }
            _allAttachments.Clear();
            _childMap.Clear();
            _hiddenByMinimize.Clear();
            _satPositionHandlers.Clear();

            // Close docked satellites too
            foreach (var (sat, _) in _dockedSatellites)
            {
                sat.Manager = null;
                sat.Role = WindowRole.Floating;
                FlushContentForReparent(sat);
                try { sat.Close(); }
                catch (InvalidOperationException) { }
            }
            _dockedSatellites.Clear();

            // Close floating-tracked windows
            foreach (var (sat, handler) in _floatingTracked.ToArray())
            {
                sat.Closed -= handler;
                sat.Manager = null;
                FlushContentForReparent(sat);
                try { sat.Close(); }
                catch (InvalidOperationException) { }
            }
            _floatingTracked.Clear();
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

    /// <summary>Whether a satellite is currently docked inside the main window.</summary>
    public bool IsDocked(SatelliteWindow satellite) => _dockedSatellites.ContainsKey(satellite);

    // ── Dock / Undock ───────────────────────────────────────────────

    /// <summary>
    /// Dock a satellite inside the main window. The main window must implement
    /// <see cref="ISatelliteDockHost"/>. Only leaf satellites (no children) can be docked.
    /// </summary>
    public void Dock(SatelliteWindow satellite, SnapEdge? edge = null)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        ThrowIfDisposed();

        if (_dockedSatellites.ContainsKey(satellite))
            throw new InvalidOperationException("Satellite is already docked.");

        if (_mainWindow is not ISatelliteDockHost host)
            throw new InvalidOperationException("Main window does not implement ISatelliteDockHost.");

        if (GetChildren(satellite).Count > 0)
            throw new InvalidOperationException("Cannot dock a satellite with children. Detach children first.");

        // Determine dock edge
        var attachment = _allAttachments.Find(a => a.Satellite == satellite);
        var dockEdge = edge ?? attachment?.Edge ?? SnapEdge.Right;

        // Ask host to embed the content
        if (!host.TryDockSatellite(satellite, dockEdge))
            return;

        // Remove from external tree if attached
        if (attachment != null)
        {
            UnsubscribeFromSatellite(satellite);
            RemoveFromTree(attachment);
        }
        StopReSnapMonitoring(satellite);

        satellite.Attachment = null;
        satellite.Manager = this; // Still managed
        satellite.Hide();
        _dockedSatellites[satellite] = dockEdge;

        AttachmentChanged?.Invoke();
    }

    /// <summary>
    /// Undock a satellite from the main window back to an external satellite.
    /// </summary>
    public void Undock(SatelliteWindow satellite, SnapEdge? edge = null)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        ThrowIfDisposed();

        if (!_dockedSatellites.TryGetValue(satellite, out var dockedEdge))
            throw new InvalidOperationException("Satellite is not docked.");

        if (_mainWindow is not ISatelliteDockHost host)
            throw new InvalidOperationException("Main window does not implement ISatelliteDockHost.");

        if (!host.TryUndockSatellite(satellite))
            return;

        _dockedSatellites.Remove(satellite);
        satellite.Manager = null; // Clear so Attach doesn't reject

        // Re-attach externally
        Attach(satellite, edge ?? dockedEdge);
    }

    // ── Persist / Restore ───────────────────────────────────────────

    /// <summary>Serialize all satellites (external + docked). All must have SatelliteId set.</summary>
    /// <exception cref="InvalidOperationException">Thrown <em>before</em> any state is
    /// produced if any satellite is missing its <see cref="SatelliteWindow.SatelliteId"/>.</exception>
    public AttachmentState[] SaveState()
    {
        // Validate up-front so a single missing id doesn't leave the caller with a
        // half-built (and silently dropped) list.
        foreach (var a in _allAttachments)
        {
            if (a.Satellite.SatelliteId is null)
                throw new InvalidOperationException(
                    $"Satellite must have SatelliteId to save state (found unset id on a {a.Edge}-edge attachment).");
        }
        foreach (var (sat, edge) in _dockedSatellites)
        {
            if (sat.SatelliteId is null)
                throw new InvalidOperationException(
                    $"Satellite must have SatelliteId to save state (found unset id on a docked {edge}-edge satellite).");
        }

        var states = new List<AttachmentState>(_allAttachments.Count + _dockedSatellites.Count);

        foreach (var a in _allAttachments)
        {
            states.Add(new AttachmentState(
                a.Satellite.SatelliteId!,
                a.Parent == _mainWindow ? null : ((SatelliteWindow)a.Parent).SatelliteId,
                a.Edge, a.OffsetAlongEdge, a.Satellite.Width, a.Satellite.Height));
        }

        foreach (var (sat, edge) in _dockedSatellites)
        {
            states.Add(new AttachmentState(
                sat.SatelliteId!,
                null, edge, 0, sat.Width, sat.Height, IsDocked: true));
        }

        return states.ToArray();
    }

    /// <summary>Restore attachment tree from saved state. Creates windows via factory.</summary>
    /// <exception cref="InvalidOperationException">Thrown if the state graph contains
    /// a cycle, or a parent reference that no entry satisfies.</exception>
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

        // Cycle / unsatisfiable-parent guard: no valid tree needs more passes than
        // there are nodes (each pass attaches at least one new node). If we exit
        // the loop with un-attached entries, the input has a cycle or a dangling
        // parent reference.
        int maxIters = state.Length + 1;
        int iters = 0;

        while (progress)
        {
            if (++iters > maxIters)
                throw new InvalidOperationException("RestoreState iteration limit exceeded — state graph likely contains a cycle.");

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

        if (attached.Count != state.Length)
        {
            var orphans = state.Where(s => !attached.Contains(s.Id))
                .Select(s => $"'{s.Id}' (parent='{s.ParentId}')");
            throw new InvalidOperationException(
                "RestoreState could not attach all entries — unsatisfiable parent references: " +
                string.Join(", ", orphans));
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
        // Preserve DockHost role for Dock-framework-owned windows. Flipping to
        // Satellite would swap StyleKeyOverride from typeof(HostWindow) to
        // typeof(Window), losing HostWindow's ControlTheme — and with it the
        // chrome grips that initiate drag-back-to-dock. For non-DockHost
        // windows (e.g. manually popped-out via menu), Satellite is correct.
        if (satellite.Role != WindowRole.DockHost)
            satellite.Role = WindowRole.Satellite;
        AddToTree(attachment);

        PositionSatellite(attachment);

        if (!satellite.IsVisible)
        {
            // Initial attach — no cooldown needed
            void OnOpened(object? s, EventArgs e)
            {
                satellite.Opened -= OnOpened;
                if (satellite.Manager != this || satellite.Attachment != attachment) return;
                PositionSatellite(attachment);
                SubscribeDragDetection(satellite);
            }
            satellite.Opened += OnOpened;
            satellite.Show(_mainWindow);
        }
        else
        {
            // Re-snap — mark for cooldown resistance + visual feedback
            attachment.IsReSnap = true;
            SubscribeDragDetection(satellite);
            satellite.FlashSnap();
        }

        AttachmentChanged?.Invoke();
    }

    private void DetachCore(SatelliteWindow satellite, DetachMode mode, bool closeSatellite)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        if (_isClosingAll) return;

        var attachment = _allAttachments.Find(a => a.Satellite == satellite);
        if (attachment == null)
        {
            // The satellite was already auto-detached (e.g. drag detection released
            // it from its parent's snap chain). The caller still asked us to close
            // it, so honour that — otherwise the window is leaked on screen.
            if (closeSatellite)
            {
                FlushContentForReparent(satellite);
                try { satellite.Close(); }
                catch (InvalidOperationException) { }
            }
            return;
        }

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
        satellite.Manager = null; // Clear before Close to prevent reentrancy from OnClosed
        // Preserve DockHost role for Dock-framework-owned windows so their
        // HostWindow ControlTheme (chrome grips, drop detection, tab strip)
        // survives the detach. Otherwise the role flip to Floating swaps the
        // theme to plain Window — losing Dock's drag-back-to-dock machinery
        // mid-drag. For non-DockHost (e.g. user-popped-out satellites that
        // were dragged away from snap), Floating is the correct rest state.
        if (satellite.Role != WindowRole.DockHost)
            satellite.Role = WindowRole.Floating;

        if (closeSatellite)
        {
            FlushContentForReparent(satellite);
            try { satellite.Close(); }
            catch (InvalidOperationException) { }
        }

        if (mode == DetachMode.ReparentChildren)
            RepositionSubtree(attachment.Parent);

        AttachmentChanged?.Invoke();
    }

    /// <summary>
    /// Release the satellite's <see cref="Window.Content"/> and force a synchronous
    /// layout pass so its <c>LayoutManager</c> drops any dirty entries for that
    /// content. Call this on a satellite whose content you intend to re-parent into
    /// another window <em>before</em> closing the satellite. Without this, the next
    /// render tick after re-parent raises
    /// <c>"Attempt to call InvalidateArrange on wrong LayoutManager"</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Detach(SatelliteWindow, bool)"/>, <see cref="DetachFloating"/>,
    /// <see cref="DetachAll"/>, and the internal close paths all invoke this
    /// automatically when <c>closeSatellite</c> is <c>true</c>. You only need to
    /// call it directly when you want to re-parent the content into a new visual
    /// parent <em>before</em> closing the satellite. See also the instance helper
    /// <see cref="SatelliteWindow.PrepareContentForReparent"/> which forwards here.
    /// </remarks>
    public static void FlushContentForReparent(SatelliteWindow satellite)
    {
        ArgumentNullException.ThrowIfNull(satellite);
        try
        {
            if (satellite.Content is null) return;
            satellite.Content = null;
            satellite.UpdateLayout();
        }
        catch
        {
            // Best-effort: this runs on the close path and must NOT throw, since
            // a failure here would prevent the satellite from being torn down.
            // Either:
            //   - Content was already null (handled above, won't reach here), or
            //   - the satellite is mid-disposal and UpdateLayout throws (the
            //     close that follows will still proceed; the visual tree will
            //     be torn down by Window.Close in any case).
        }
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
            child.Satellite.Role = WindowRole.Floating;
            FlushContentForReparent(child.Satellite);
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
            // Hard cap — the snap tree should never reach this depth. If it
            // does, it indicates a corrupt _allAttachments graph (e.g. a cycle
            // the AttachCore descendant-check failed to prevent). Throwing
            // beats silently returning a wrong depth that would then trick
            // ChainDepthLimit into accepting / rejecting the wrong attachments.
            if (depth > 100)
                throw new InvalidOperationException(
                    "Snap-tree depth exceeded 100 — internal graph likely corrupt (cycle?).");
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
        // Note: RenderScaling is a CLR property on Visual, not an AvaloniaProperty,
        // so it can't be observed via property-changed events. To track DPI
        // changes (e.g. moving between monitors with different scaling), subscribe
        // to TopLevel.ScalingChanged on the main window during attach instead.
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
            foreach (var sat in _floatingTracked.Keys.ToArray())
            {
                if (sat.IsVisible)
                {
                    _hiddenByMinimize.Add(sat);
                    sat.Hide();
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
        if (!_behavior.Enabled || !_behavior.AutoDetachOnDrag) return;
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
            {
                StopReSnapMonitoring(satellite); // Clean up any stale state
                StartReSnapMonitoring(satellite);
            }
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

    private void StartReSnapMonitoring(SatelliteWindow satellite)
    {
        if (!_behavior.Enabled) return;
        StopReSnapMonitoring(satellite); // Ensure clean state
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
            && (DateTime.UtcNow - dt).TotalMilliseconds < 300)
            return;

        var snap = DetectNearestSnap(satellite);
        if (snap == null) return;

        // Compute offset from satellite's current screen position relative to the snap parent
        var parent = snap.Value.parent;
        var parentPos = parent.Position;
        var scaling = parent.RenderScaling;
        double offset = (snap.Value.edge is SnapEdge.Left or SnapEdge.Right)
            ? (satellite.Position.Y - parentPos.Y) / scaling
            : (satellite.Position.X - parentPos.X) / scaling;

        // If this window was being tracked as floating (e.g. a Dock drag-out
        // promoted via AttachFloating), release that tracking first. Attach()
        // throws "already attached" if satellite.Manager is non-null, and
        // AttachFloating set it on us; we must clear before promoting to
        // snapped-satellite mode. StopReSnapMonitoring is also called here so
        // we don't double-fire while Attach re-establishes its own handlers.
        if (_floatingTracked.TryGetValue(satellite, out var onClosedFloating))
        {
            satellite.Closed -= onClosedFloating;
            _floatingTracked.Remove(satellite);
            satellite.Manager = null;
        }
        StopReSnapMonitoring(satellite);

        if (parent == _mainWindow)
            Attach(satellite, snap.Value.edge, offset);
        else if (parent is SatelliteWindow parentSat)
            Attach(satellite, parentSat, snap.Value.edge, offset);
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
            if (att.Satellite != satellite && att.Satellite.IsVisible
                && !IsDescendant(att.Satellite, satellite))
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
            // Vertical borders are asymmetric (title bar on top, small border on bottom).
            // Use a smaller overlap — just enough to close the invisible bottom border gap.
            int borderPx = GetBorderPx();
            int ol = Math.Max(0, borderPx - 1); // Single-side compensation
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
