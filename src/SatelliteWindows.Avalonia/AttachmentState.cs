namespace SatelliteWindows.Avalonia;

/// <summary>
/// Serializable snapshot of a single satellite attachment for persist/restore.
/// </summary>
public record AttachmentState(
    string Id,
    string? ParentId,
    SnapEdge Edge,
    double OffsetAlongEdge,
    double Width,
    double Height
);
