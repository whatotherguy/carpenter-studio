using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.MaterialContext;

public sealed record EdgeBanding
{
    public string EdgeBandingId { get; init; }
    public string Name { get; init; }
    public Thickness Thickness { get; init; }
    public Length Width { get; init; }
    public MaterialId? MatchesMaterialId { get; init; }

    public EdgeBanding(string edgeBandingId, string name, Thickness thickness, Length width, MaterialId? matchesMaterialId)
    {
        if (string.IsNullOrWhiteSpace(edgeBandingId))
            throw new InvalidOperationException("Edge banding identifier is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Edge banding name is required.");
        if (width <= Length.Zero)
            throw new InvalidOperationException("Edge banding width must be positive.");

        EdgeBandingId = edgeBandingId;
        Name = name;
        Thickness = thickness;
        Width = width;
        MatchesMaterialId = matchesMaterialId;
    }
}
