// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using SatelliteWindows.Dock.Model.Controls;
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Model;

/// <summary>
/// Factory base class.
/// </summary>
public abstract partial class FactoryBase
{
    /// <inheritdoc/>
    public abstract IDictionary<IDockable, object> ToolControls { get; }

    /// <inheritdoc/>
    public abstract IDictionary<IDockable, object> DocumentControls { get; }
}
