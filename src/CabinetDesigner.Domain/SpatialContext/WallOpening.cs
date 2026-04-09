using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.SpatialContext;

public sealed class WallOpening
{
    public WallOpeningId Id { get; }
    public WallId WallId { get; }
    public WallOpeningType Type { get; }
    public Length OffsetFromWallStart { get; }
    public Length Width { get; }
    public Length Height { get; }
    public Length SillHeight { get; }
    public bool AllowsCabinetsBelow => Type == WallOpeningType.Window;

    public WallOpening(
        WallOpeningId id,
        WallId wallId,
        WallOpeningType type,
        Length offsetFromStart,
        Length width,
        Length height,
        Length sillHeight)
    {
        if (id == default)
            throw new InvalidOperationException("Wall opening must have an identifier.");
        if (wallId == default)
            throw new InvalidOperationException("Wall opening must belong to a wall.");
        if (width <= Length.Zero)
            throw new InvalidOperationException("Wall opening width must be positive.");
        if (height <= Length.Zero)
            throw new InvalidOperationException("Wall opening height must be positive.");

        Id = id;
        WallId = wallId;
        Type = type;
        OffsetFromWallStart = offsetFromStart;
        Width = width;
        Height = height;
        SillHeight = sillHeight;
    }
}
