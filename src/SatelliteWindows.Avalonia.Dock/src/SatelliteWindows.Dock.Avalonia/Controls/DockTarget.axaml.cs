// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Avalonia.Controls;

/// <summary>
/// Interaction logic for <see cref="DockTarget"/> xaml.
/// </summary>
public class DockTarget : DockTargetBase
{
    /// <inheritdoc />
    protected override DockOperation DefaultDockOperation => DockOperation.Window;
}
