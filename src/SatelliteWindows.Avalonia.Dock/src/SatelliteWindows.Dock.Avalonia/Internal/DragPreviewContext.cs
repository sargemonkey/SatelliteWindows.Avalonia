// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Avalonia.Internal;

internal static class DragPreviewContext
{
    public static bool IsActive { get; set; }

    public static IDockable? Dockable { get; set; }

    public static bool IsPreviewing(IDockable dockable)
    {
        return IsActive && ReferenceEquals(Dockable, dockable);
    }

    public static void Clear()
    {
        IsActive = false;
        Dockable = null;
    }
}
