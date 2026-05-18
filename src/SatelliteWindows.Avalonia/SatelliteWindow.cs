using Avalonia.Controls;

namespace SatelliteWindows.Avalonia;

/// <summary>
/// A window subclass with satellite snap awareness.
/// Set your content, size, and any window properties before attaching via <see cref="SatelliteManager"/>.
/// For minimal chrome, set SystemDecorations and ExtendClientAreaToDecorationsHint before attaching.
/// </summary>
public class SatelliteWindow : Window
{
    internal SatelliteAttachment? Attachment { get; set; }
    internal SatelliteManager? Manager { get; set; }

    /// <summary>Identifier used for persist/restore. Set before saving state.</summary>
    public string? SatelliteId { get; set; }

    public SatelliteWindow()
    {
        ShowInTaskbar = false;
    }

    /// <summary>Whether this satellite is currently attached to a manager.</summary>
    public bool IsAttached => Manager != null;

    /// <summary>
    /// Brief opacity flash to give visual feedback on snap.
    /// </summary>
    internal async void FlashSnap()
    {
        try
        {
            Opacity = 0.75;
            await Task.Delay(150);
            if (IsVisible) Opacity = 1.0;
        }
        catch { /* window may have closed during animation */ }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Manager?.Detach(this, DetachMode.ReparentChildren);
    }
}
