using System.Collections.Generic;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.MaterialContext;

namespace CabinetDesigner.Domain.ManufacturingContext;

public sealed record ManufacturingPlan
{
    public required IReadOnlyList<ManufacturingMaterialGroup> MaterialGroups { get; init; }

    public required IReadOnlyList<CutListItem> CutList { get; init; }

    public required IReadOnlyList<ManufacturingOperation> Operations { get; init; }

    public required IReadOnlyList<EdgeBandingRequirement> EdgeBandingRequirements { get; init; }

    public required ManufacturingReadinessResult Readiness { get; init; }
}

public sealed record ManufacturingMaterialGroup
{
    public required MaterialId MaterialId { get; init; }

    public required Thickness MaterialThickness { get; init; }

    public required GrainDirection GrainDirection { get; init; }

    public required IReadOnlyList<ManufacturingPart> Parts { get; init; }
}

public sealed record ManufacturingPart
{
    public required string PartId { get; init; }

    public required CabinetId CabinetId { get; init; }

    public required string PartType { get; init; }

    public required string Label { get; init; }

    public required Length CutWidth { get; init; }

    public required Length CutHeight { get; init; }

    public required Thickness MaterialThickness { get; init; }

    public required MaterialId MaterialId { get; init; }

    public required GrainDirection GrainDirection { get; init; }

    public required ManufacturedEdgeTreatment EdgeTreatment { get; init; }

    public bool RequiresEdgeBanding => EdgeTreatment.HasAnyBanding;
}

public sealed record CutListItem
{
    public required string PartId { get; init; }

    public required CabinetId CabinetId { get; init; }

    public required string PartType { get; init; }

    public required string Label { get; init; }

    public required Length CutWidth { get; init; }

    public required Length CutHeight { get; init; }

    public required Thickness MaterialThickness { get; init; }

    public required MaterialId MaterialId { get; init; }

    public required GrainDirection GrainDirection { get; init; }

    public required ManufacturedEdgeTreatment EdgeTreatment { get; init; }
}

public sealed record ManufacturedEdgeTreatment(
    string? TopEdgeBandingId,
    string? BottomEdgeBandingId,
    string? LeftEdgeBandingId,
    string? RightEdgeBandingId)
{
    public bool HasAnyBanding =>
        TopEdgeBandingId is not null ||
        BottomEdgeBandingId is not null ||
        LeftEdgeBandingId is not null ||
        RightEdgeBandingId is not null;

    public int BandedEdgeCount =>
        (TopEdgeBandingId is null ? 0 : 1) +
        (BottomEdgeBandingId is null ? 0 : 1) +
        (LeftEdgeBandingId is null ? 0 : 1) +
        (RightEdgeBandingId is null ? 0 : 1);
}

public sealed record ManufacturingOperation
{
    public required string PartId { get; init; }

    public required int Sequence { get; init; }

    public required ManufacturingOperationKind Kind { get; init; }

    public required IReadOnlyDictionary<string, OverrideValue> Parameters { get; init; }
}

public enum ManufacturingOperationKind
{
    SawCutRectangle,
    ApplyEdgeBanding
}

public sealed record EdgeBandingRequirement
{
    public required string EdgeBandingId { get; init; }

    public required int EdgeCount { get; init; }

    public required Length TotalLinearLength { get; init; }
}

public sealed record ManufacturingReadinessResult
{
    public required bool IsReady { get; init; }

    public required IReadOnlyList<ManufacturingBlocker> Blockers { get; init; }
}

public sealed record ManufacturingBlocker
{
    public required ManufacturingBlockerCode Code { get; init; }

    public required string Message { get; init; }

    public required IReadOnlyList<string> AffectedEntityIds { get; init; }

    public ValidationIssue ToValidationIssue() =>
        new(
            ValidationSeverity.ManufactureBlocker,
            $"manufacturing.{Code}",
            Message,
            AffectedEntityIds);
}

public enum ManufacturingBlockerCode
{
    MissingMaterial,
    InvalidThickness,
    PartTooSmall,
    PartTooLarge
}
