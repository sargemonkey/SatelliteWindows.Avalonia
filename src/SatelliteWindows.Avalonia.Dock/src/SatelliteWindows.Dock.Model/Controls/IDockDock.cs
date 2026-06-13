// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Model.Controls;

/// <summary>
/// Docking panel contract.
/// </summary>
[RequiresDataTemplate]
public interface IDockDock : IDock
{
    /// <summary>
    /// Gets or sets a value which indicates whether the last child of the fills the remaining space in the panel.
    /// </summary>
    bool LastChildFill { get; set; }
}
