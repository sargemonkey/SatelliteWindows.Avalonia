namespace SatelliteWindows.Avalonia;

/// <summary>
/// Controls what happens to a satellite's children when it is detached.
/// </summary>
public enum DetachMode
{
    /// <summary>Detach only this satellite. Its children are reparented to its parent.</summary>
    ReparentChildren,

    /// <summary>Detach this satellite and close its entire subtree.</summary>
    DetachChain
}
