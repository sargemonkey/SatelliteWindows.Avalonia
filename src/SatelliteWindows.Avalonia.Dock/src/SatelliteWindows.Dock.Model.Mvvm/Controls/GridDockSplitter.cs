// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Runtime.Serialization;
using SatelliteWindows.Dock.Model.Controls;
using SatelliteWindows.Dock.Model.Mvvm.Core;
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Model.Mvvm.Controls;

/// <summary>
/// Grid dock splitter.
/// </summary>
public class GridDockSplitter : DockableBase, IGridDockSplitter
{
    private GridResizeDirection _resizeDirection;

    /// <inheritdoc/>
    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public GridResizeDirection ResizeDirection
    {
        get => _resizeDirection;
        set => SetProperty(ref _resizeDirection, value);
    }
}
