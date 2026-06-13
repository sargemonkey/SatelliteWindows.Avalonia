// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using SatelliteWindows.Dock.Model.Core;

namespace SatelliteWindows.Dock.Model.Controls;

/// <summary>
/// Document content contract.
/// </summary>
[RequiresDataTemplate]
public interface IDocumentContent : IDockable
{
    /// <summary>
    /// Gets or sets document content.
    /// </summary>
    object? Content { get; set; }
}
