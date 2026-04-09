using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public sealed record PartGenerationResult
{
    public required IReadOnlyList<GeneratedPart> Parts { get; init; }
}

public sealed record GeneratedPart
{
    public required string PartId { get; init; }

    public required CabinetId CabinetId { get; init; }

    public required string PartType { get; init; }

    public required Length Width { get; init; }

    public required Length Height { get; init; }

    public required Thickness MaterialThickness { get; init; }

    public required MaterialId MaterialId { get; init; }

    public required GrainDirection GrainDirection { get; init; }

    public required EdgeTreatment Edges { get; init; }

    public required string Label { get; init; }
}

public sealed record EdgeTreatment(
    string? TopEdgeBandingId,
    string? BottomEdgeBandingId,
    string? LeftEdgeBandingId,
    string? RightEdgeBandingId);
