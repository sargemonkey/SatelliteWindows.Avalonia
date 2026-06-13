// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Avalonia.Controls;
using SatelliteWindows.Dock.Model.Controls;
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Avalonia.Automation.Peers;

internal sealed class DocumentTabStripAutomationPeer : DockTabStripAutomationPeer<DocumentTabStrip>
{
    internal DocumentTabStripAutomationPeer(DocumentTabStrip owner)
        : base(owner)
    {
    }

    protected override string ClassName => nameof(DocumentTabStrip);

    protected override string FallbackName => "Document tabs";

    protected override string BuildStateText(IDock? dock)
    {
        var documentDock = dock as IDocumentDock;

        return DockAutomationPeerHelper.FormatState(
            ("CanCreate", OwnerControl.CanCreateItem),
            ("Active", OwnerControl.IsActive),
            ("EnableWindowDrag", OwnerControl.EnableWindowDrag),
            ("Orientation", OwnerControl.Orientation),
            ("SelectedIndex", OwnerControl.SelectedIndex),
            ("Items", OwnerControl.Items.Count),
            ("CanCreateDocument", documentDock?.CanCreateDocument ?? false),
            ("LayoutMode", documentDock?.LayoutMode));
    }
}
