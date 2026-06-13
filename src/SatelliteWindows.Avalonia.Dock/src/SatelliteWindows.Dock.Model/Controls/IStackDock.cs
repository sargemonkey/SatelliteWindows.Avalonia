// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Model.Controls;

/// <summary>
/// Stack dock contract.
/// </summary>
[RequiresDataTemplate]
public interface IStackDock : IDock
{
    /// <summary>
    /// Gets or sets layout orientation.
    /// </summary>
    Orientation Orientation { get; set; }

    /// <summary>
    /// Gets or sets spacing between items.
    /// </summary>
    double Spacing { get; set; }
}
