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

    public SatelliteWindow()
    {
        ShowInTaskbar = false;
    }

    /// <summary>Whether this satellite is currently attached to a manager.</summary>
    public bool IsAttached => Manager != null;

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Notify the manager so it can clean up the attachment
        Manager?.Detach(this);
    }
}
