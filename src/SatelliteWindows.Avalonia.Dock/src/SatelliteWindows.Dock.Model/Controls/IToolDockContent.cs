// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Model.Controls;

/// <summary>
/// Tool dock content contract.
/// </summary>
public interface IToolDockContent : IDock
{
    /// <summary>
    /// Gets or sets tool template.
    /// </summary>
    IToolTemplate? ToolTemplate { get; set; }

    /// <summary>
    /// Create new tool from template.
    /// </summary>
    /// <returns>The new tool instance.</returns>
    object? CreateToolFromTemplate();
}
