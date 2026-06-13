using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using SatelliteWindows.Dock.Avalonia.Controls;

namespace SatelliteWindows.Avalonia;

/// <summary>
/// The single apex window class consumers use. Inherits from Dock.Avalonia's
/// <see cref="HostWindow"/> so every <see cref="SatelliteWindow"/> instance
/// carries BOTH the snap-window features added here AND the full Dock
/// host-window machinery (drag-out target, tab strip, document chrome) from
/// the underlying base.
///
/// <para>Three placement modes are exposed via <see cref="Role"/>:
/// <see cref="WindowRole.Satellite"/> (snapped to a main window),
/// <see cref="WindowRole.Floating"/> (standalone), and
/// <see cref="WindowRole.DockHost"/> (created by Dock's drag-out flow).
/// Switching role at runtime is supported — see <see cref="SatelliteDockManager"/>.</para>
///
/// <para>One class, one chrome, one inheritance path — apps target only
/// <c>SatelliteWindow</c> and never need to instantiate or reference
/// <c>HostWindow</c> directly. Dock's drag-out factory hook
/// (<see cref="DockControl.HostWindowFactory"/>) can be wired to return new
/// <see cref="SatelliteWindow"/> instances so dragged tabs land in the apex
/// class with all features available.</para>
/// </summary>
public class SatelliteWindow : HostWindow
{
    /// <summary>The role this window is currently fulfilling. See
    /// <see cref="WindowRole"/>. Defaults to <see cref="WindowRole.Floating"/>;
    /// <see cref="SatelliteManager.Attach(SatelliteWindow, SnapEdge, double)"/>
    /// switches it to <see cref="WindowRole.Satellite"/>; Dock host factories
    /// set it to <see cref="WindowRole.DockHost"/>.
    ///
    /// <para>Changing <see cref="Role"/> at runtime invalidates the style key
    /// (see <see cref="StyleKeyOverride"/>) and re-applies the appropriate
    /// theme, so it is safe to flip after the window has been shown — for
    /// example, when promoting a <see cref="WindowRole.DockHost"/> drag-out
    /// to a snapped <see cref="WindowRole.Satellite"/>.</para>
    /// </summary>
    public static readonly StyledProperty<WindowRole> RoleProperty =
        AvaloniaProperty.Register<SatelliteWindow, WindowRole>(
            nameof(Role), defaultValue: WindowRole.Floating);

    public WindowRole Role
    {
        get => GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    internal SatelliteAttachment? Attachment { get; set; }
    internal SatelliteManager? Manager { get; set; }

    /// <summary>Identifier used for persist/restore. Set before saving state.</summary>
    public string? SatelliteId { get; set; }

    public SatelliteWindow() : base()
    {
        // Satellite windows are accessory windows — don't appear in taskbar by
        // default. The base HostWindow's ctor leaves Avalonia's default (true);
        // override here. Callers using SatelliteWindow as a main app window
        // re-enable in their own ctor.
        ShowInTaskbar = false;
    }

    /// <summary>
    /// Theme-key selector — critical for apex unification.
    ///
    /// <para>HostWindow's ControlTheme (in Dock.Avalonia.Themes.Fluent) replaces
    /// the window's <c>Content</c> with a <c>DockControl Layout="{Binding}"</c>
    /// template, overrides <c>Title</c> to <c>{Binding FocusedDockable.Title}</c>,
    /// and forces <c>ExtendClientAreaToDecorationsHint=False</c>. Useful for
    /// a dragged-out tab window — fatal for a main app window which has its
    /// own AXAML content tree, custom title bar, and Title binding.</para>
    ///
    /// <para>So we route theme selection by <see cref="Role"/>:</para>
    /// <list type="bullet">
    ///   <item><see cref="WindowRole.DockHost"/>: use HostWindow's ControlTheme
    ///         (Dock framework's drag-out flow expects it).</item>
    ///   <item><see cref="WindowRole.Floating"/> / <see cref="WindowRole.Satellite"/>:
    ///         fall through to <see cref="Window"/>'s default theme so consumer-provided
    ///         AXAML content renders normally.</item>
    /// </list>
    ///
    /// <para>Runtime <see cref="Role"/> changes are handled by
    /// <see cref="OnPropertyChanged"/>, which invalidates styling so the new
    /// theme picks up.</para>
    /// </summary>
    protected override Type StyleKeyOverride =>
        Role == WindowRole.DockHost ? typeof(HostWindow) : typeof(Window);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // When Role flips at runtime (e.g. DockHost drag-out promoted to
        // Satellite, or Satellite reverted to Floating), the StyleKeyOverride
        // result changes — but Avalonia only re-evaluates the style key when
        // styles are invalidated. Force it here so the new theme actually
        // applies instead of leaving the window painted with the stale theme.
        if (change.Property == RoleProperty)
        {
            // Cycle the Classes collection to trigger a style re-evaluation —
            // Layoutable.InvalidateStyles is internal so we use a public proxy.
            Classes.Add("__role_changed__");
            Classes.Remove("__role_changed__");
        }
    }

    /// <summary>True when this window is tracked by a <see cref="SatelliteManager"/>
    /// — either as a snapped <see cref="WindowRole.Satellite"/>, or as a
    /// lifecycle-tracked <see cref="WindowRole.Floating"/> via
    /// <see cref="SatelliteManager.AttachFloating"/>. Use the more specific
    /// <see cref="IsSnapped"/> or <see cref="IsFloatingTracked"/> when the
    /// distinction matters.</summary>
    public bool IsAttached => Manager != null;

    /// <summary>True when this window is a snapped satellite in a manager's
    /// snap-chain (i.e. its <see cref="Attachment"/> is non-null). This is the
    /// stricter cousin of <see cref="IsAttached"/>, which is also true for
    /// floating-tracked windows.</summary>
    public bool IsSnapped => Attachment != null;

    /// <summary>True when this window is tracked in floating mode by a manager
    /// (lifecycle-coupled, no snap). Mutually exclusive with <see cref="IsSnapped"/>.</summary>
    public bool IsFloatingTracked => Manager != null && Attachment == null;

    /// <summary>True when this window is currently snapping to a main window.
    /// Equivalent to <c>Role == WindowRole.Satellite &amp;&amp; IsAttached &amp;&amp; Manager.Behavior.Enabled</c>.</summary>
    public bool IsSnapping => Role == WindowRole.Satellite && Manager is { Behavior.Enabled: true };

    /// <summary>
    /// Release this window's <see cref="ContentControl.Content"/> and flush the
    /// satellite's layout queue so the released control can be safely re-parented
    /// into another window. Without this, Avalonia raises
    /// <c>"Attempt to call InvalidateArrange on wrong LayoutManager"</c> on the
    /// next render tick. Forwards to <see cref="SatelliteManager.FlushContentForReparent"/>.
    /// </summary>
    /// <remarks>
    /// You only need to call this directly when you want to move the satellite's
    /// content into a new visual parent <em>before</em> closing the satellite.
    /// <see cref="SatelliteManager.Detach(SatelliteWindow, bool)"/>,
    /// <see cref="SatelliteManager.DetachFloating"/>, and
    /// <see cref="SatelliteManager.DetachAll"/> already do this for you when
    /// <c>closeSatellite</c> is <c>true</c>.
    /// </remarks>
    public void PrepareContentForReparent() =>
        SatelliteManager.FlushContentForReparent(this);

    /// <summary>
    /// Brief opacity flash to give visual feedback on snap. Only meaningful
    /// in <see cref="WindowRole.Satellite"/>.
    /// </summary>
    internal async void FlashSnap()
    {
        try
        {
            Opacity = 0.75;
            await Task.Delay(150);
            if (IsVisible) Opacity = 1.0;
        }
        catch
        {
            // The window may have been closed mid-animation — Opacity assignment
            // throws on a closed top-level. There's no recovery action here:
            // the animation was cosmetic and the window is gone.
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Manager?.Detach(this, DetachMode.ReparentChildren);
    }
}
