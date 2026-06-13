using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using SatelliteWindows.Dock.Avalonia.Controls;
using SatelliteWindows.Dock.Avalonia.Contract;

namespace SatelliteWindows.Dock.Avalonia.Internal;

internal class DefaultDragOffsetCalculator : IDragOffsetCalculator
{
    public PixelPoint CalculateOffset(Control dragControl, DockControl dockControl, Point pointerPosition)
    {
        var screenPoint = dockControl.PointToScreen(pointerPosition);

        if (dragControl is not Control{ Name: "PART_Grip"})
        {
            var corner = dragControl.PointToScreen(new Point());
            return corner - screenPoint;
        }

        return default;
    }
}
