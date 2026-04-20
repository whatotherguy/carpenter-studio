using System.Collections.Generic;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Domain.Validation;

public enum ValidationMode
{
    Preview,
    Full
}

public sealed class ValidationContext
{
    public required IDesignCommand Command { get; init; }

    public required ValidationMode Mode { get; init; }

    public required ValidationStrictness Strictness { get; init; }

    public required IReadOnlyList<CabinetPositionSnapshot> CabinetPositions { get; init; }

    public required IReadOnlyList<RunValidationSnapshot> RunSnapshots { get; init; }

    public IReadOnlyList<ConstraintViolationSnapshot> Constraints { get; init; } = [];

    public IReadOnlyList<ManufacturingBlockerSnapshot> ManufacturingBlockers { get; init; } = [];

    public IReadOnlyList<InstallBlockerSnapshot> InstallBlockers { get; init; } = [];

    public required WorkflowStateSnapshot WorkflowState { get; init; }
}

public sealed record CabinetPositionSnapshot(
    string CabinetId,
    string RunId,
    Rect2D BoundingBox,
    int SlotIndex);

public sealed record RunValidationSnapshot(
    string RunId,
    Length Capacity,
    Length OccupiedLength,
    int SlotCount,
    bool HasLeftEndCondition,
    bool HasRightEndCondition);

public sealed record ConstraintViolationSnapshot(
    string ConstraintCode,
    string Message,
    ValidationSeverity Severity,
    IReadOnlyList<string> AffectedEntityIds);

public sealed record WorkflowStateSnapshot(
    string ApprovalState,
    bool HasUnapprovedChanges,
    bool HasPendingManufactureBlockers);

public sealed record ManufacturingBlockerSnapshot(
    string BlockerCode,
    string Message,
    IReadOnlyList<string> AffectedEntityIds);

public sealed record InstallBlockerSnapshot(
    string BlockerCode,
    string Message,
    IReadOnlyList<string> AffectedEntityIds);
