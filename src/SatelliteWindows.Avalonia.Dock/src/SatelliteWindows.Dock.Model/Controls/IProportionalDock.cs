// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Model.Controls;

/// <summary>
/// Proportional dock contract.
/// </summary>
[RequiresDataTemplate]
public interface IProportionalDock : IDock, IGlobalTarget
{
    /// <summary>
    /// Gets or sets layout orientation.
    /// </summary>
    Orientation Orientation { get; set; }
}
