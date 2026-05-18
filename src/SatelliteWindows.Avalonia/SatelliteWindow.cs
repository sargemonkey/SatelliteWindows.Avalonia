using Avalonia.Controls;

namespace SatelliteWindows.Avalonia;

/// <summary>
/// A window subclass with satellite snap awareness.
/// Set your content, size, and any window properties before attaching via <see cref="SatelliteManager"/>.
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

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Reparent children to grandparent so closing one satellite doesn't kill the chain
        Manager?.Detach(this, DetachMode.ReparentChildren);
    }
}
