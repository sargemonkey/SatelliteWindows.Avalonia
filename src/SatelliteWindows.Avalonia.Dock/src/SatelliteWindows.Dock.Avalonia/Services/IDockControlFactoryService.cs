// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Avalonia.Controls;
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Avalonia.Services;

internal interface IDockControlFactoryService
{
    void InitializeControlRecycling(DockControl control);
    void CleanupFactory(DockControl control, IDock layout);
}
