// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Model.Controls;

/// <summary>
/// Grid splitter dock contract.
/// </summary>
[RequiresDataTemplate]
public interface IGridDockSplitter : ISplitter
{
    /// <summary>
    /// Gets or sets resize direction.
    /// </summary>
    GridResizeDirection ResizeDirection { get; set; }
}
