using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public sealed record SpatialResolutionResult
{
    public required IReadOnlyList<SlotPositionUpdate> SlotPositionUpdates { get; init; }

    public required IReadOnlyList<AdjacencyChange> AdjacencyChanges { get; init; }

    public required IReadOnlyList<RunSummary> RunSummaries { get; init; }

    public required IReadOnlyList<RunPlacement> Placements { get; init; }
}

public sealed record SlotPositionUpdate(
    RunSlotId SlotId,
    CabinetId CabinetId,
    RunId RunId,
    int NewIndex,
    Point2D WorldPosition,
    Length OccupiedWidth);

public sealed record AdjacencyChange(
    CabinetId CabinetId,
    CabinetId? LeftNeighborCabinetId,
    CabinetId? RightNeighborCabinetId);

public sealed record RunSummary(
    RunId RunId,
    Length Capacity,
    Length OccupiedLength,
    Length RemainingLength,
    int SlotCount);

public sealed record RunPlacement(
    RunId RunId,
    CabinetId CabinetId,
    Point2D Origin,
    Vector2D Direction,
    Rect2D WorldBounds,
    Length OccupiedWidth);
