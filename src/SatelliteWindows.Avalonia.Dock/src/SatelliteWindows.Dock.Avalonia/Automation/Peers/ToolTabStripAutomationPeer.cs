// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Avalonia.Controls;
using SatelliteWindows.Dock.Model.Controls;
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Avalonia.Automation.Peers;

internal sealed class ToolTabStripAutomationPeer : DockTabStripAutomationPeer<ToolTabStrip>
{
    internal ToolTabStripAutomationPeer(ToolTabStrip owner)
        : base(owner)
    {
    }

    protected override string ClassName => nameof(ToolTabStrip);

    protected override string FallbackName => "Tool tabs";

    protected override string BuildStateText(IDock? dock)
    {
        var toolDock = dock as IToolDock;

        return DockAutomationPeerHelper.FormatState(
            ("CanCreate", OwnerControl.CanCreateItem),
            ("SelectedIndex", OwnerControl.SelectedIndex),
            ("Items", OwnerControl.Items.Count),
            ("ScrollOrientation", OwnerControl.MouseWheelScrollOrientation),
            ("Alignment", toolDock?.Alignment),
            ("Expanded", toolDock?.IsExpanded ?? false),
            ("AutoHide", toolDock?.AutoHide ?? false));
    }
}
