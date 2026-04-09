using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.TemplateContext;

public sealed record CabinetTemplate
{
    public TemplateId Id { get; init; }
    public string Name { get; init; }
    public string CabinetTypeId { get; init; }
    public Length DefaultWidth { get; init; }
    public Length DefaultDepth { get; init; }
    public Length DefaultHeight { get; init; }
    public IReadOnlyDictionary<string, OverrideValue> DefaultOverrides { get; init; }

    public CabinetTemplate(
        TemplateId id,
        string name,
        string cabinetTypeId,
        Length defaultWidth,
        Length defaultDepth,
        Length defaultHeight,
        IReadOnlyDictionary<string, OverrideValue> defaultOverrides)
    {
        if (id == default)
            throw new InvalidOperationException("Cabinet template must have an identifier.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Cabinet template name is required.");
        if (string.IsNullOrWhiteSpace(cabinetTypeId))
            throw new InvalidOperationException("Cabinet template type identifier is required.");
        if (defaultWidth <= Length.Zero || defaultDepth <= Length.Zero || defaultHeight <= Length.Zero)
            throw new InvalidOperationException("Cabinet template dimensions must be positive.");

        Id = id;
        Name = name;
        CabinetTypeId = cabinetTypeId;
        DefaultWidth = defaultWidth;
        DefaultDepth = defaultDepth;
        DefaultHeight = defaultHeight;
        DefaultOverrides = defaultOverrides;
    }
}
