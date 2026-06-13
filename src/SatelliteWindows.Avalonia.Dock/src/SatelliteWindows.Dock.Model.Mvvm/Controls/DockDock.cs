// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Runtime.Serialization;
using SatelliteWindows.Dock.Model.Controls;
using SatelliteWindows.Dock.Model.Mvvm.Core;

namespace SatelliteWindows.Dock.Model.Mvvm.Controls;

/// <summary>
/// Docking panel dock.
/// </summary>
public class DockDock : DockBase, IDockDock
{        
    private bool _lastChildFill = true;

    /// <inheritdoc/>
    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public bool LastChildFill
    {
        get => _lastChildFill;
        set => SetProperty(ref _lastChildFill, value);
    }
}
