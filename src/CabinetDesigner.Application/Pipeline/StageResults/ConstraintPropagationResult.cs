using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.HardwareContext;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.MaterialContext;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public sealed record ConstraintPropagationResult
{
    public required IReadOnlyList<MaterialAssignment> MaterialAssignments { get; init; }

    public required IReadOnlyList<HardwareAssignment> HardwareAssignments { get; init; }

    public required IReadOnlyList<ConstraintViolation> Violations { get; init; }
}

public sealed record MaterialAssignment(
    string PartId,
    MaterialId MaterialId,
    Thickness ResolvedThickness,
    GrainDirection GrainDirection);

public sealed record HardwareAssignment(
    OpeningId OpeningId,
    IReadOnlyList<HardwareItemId> HardwareIds,
    BoringPattern? BoringPattern);

public sealed record ConstraintViolation(
    string ConstraintCode,
    string Message,
    ValidationSeverity Severity,
    IReadOnlyList<string> AffectedEntityIds);
