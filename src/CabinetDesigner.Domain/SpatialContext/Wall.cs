using System;
using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.SpatialContext;

public sealed class Wall
{
    private readonly List<WallOpening> _openings = [];

    public WallId Id { get; }
    public RoomId RoomId { get; }
    public Point2D StartPoint { get; }
    public Point2D EndPoint { get; }
    public Thickness WallThickness { get; }
    public IReadOnlyList<WallOpening> Openings => _openings;
    public Length Length => StartPoint.DistanceTo(EndPoint);
    public Vector2D Direction => (EndPoint - StartPoint).Normalized();
    public LineSegment2D Segment => new(StartPoint, EndPoint);

    public Length AvailableLength
    {
        get
        {
            var totalOpeningWidth = _openings.Aggregate(Length.Zero, (sum, opening) => sum + opening.Width);
            var available = Length - totalOpeningWidth;
            return available >= Offset.Zero
                ? available.Abs()
                : Length.Zero;
        }
    }

    public Wall(WallId id, RoomId roomId, Point2D start, Point2D end, Thickness wallThickness)
    {
        if (id == default)
            throw new InvalidOperationException("Wall must have an identifier.");
        if (roomId == default)
            throw new InvalidOperationException("Wall must belong to a room.");
        if (start.DistanceTo(end) <= Length.Zero)
            throw new InvalidOperationException("Wall length must be greater than zero.");

        Id = id;
        RoomId = roomId;
        StartPoint = start;
        EndPoint = end;
        WallThickness = wallThickness;
    }

    public WallOpening AddOpening(
        WallOpeningType type,
        Length offsetFromStart,
        Length width,
        Length height,
        Length sillHeight)
    {
        var opening = new WallOpening(WallOpeningId.New(), Id, type, offsetFromStart, width, height, sillHeight);

        if ((offsetFromStart + width) > Length)
            throw new InvalidOperationException("Opening extends beyond wall length.");

        if (HasOverlappingOpening(opening))
            throw new InvalidOperationException("Opening overlaps with an existing opening.");

        _openings.Add(opening);
        return opening;
    }

    private bool HasOverlappingOpening(WallOpening candidate)
    {
        foreach (var existing in _openings)
        {
            var existingEnd = existing.OffsetFromWallStart + existing.Width;
            var candidateEnd = candidate.OffsetFromWallStart + candidate.Width;

            var overlaps =
                candidate.OffsetFromWallStart.Inches < existingEnd.Inches &&
                candidateEnd.Inches > existing.OffsetFromWallStart.Inches;

            if (overlaps)
                return true;
        }

        return false;
    }
}
