using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public sealed record InteractionInterpretationResult
{
    public required IReadOnlyList<DomainOperation> Operations { get; init; }

    public required IReadOnlyDictionary<string, OverrideValue> InterpretedParameters { get; init; }
}

public abstract record DomainOperation
{
    private DomainOperation()
    {
    }

    public sealed record None : DomainOperation;

    public sealed record InsertSlot(
        RunId RunId,
        CabinetId CabinetId,
        int InsertIndex) : DomainOperation;

    public sealed record RemoveSlot(
        RunId RunId,
        RunSlotId RunSlotId) : DomainOperation;

    public sealed record MoveSlot(
        RunId SourceRunId,
        RunId TargetRunId,
        RunSlotId RunSlotId,
        int TargetIndex) : DomainOperation;

    public sealed record ResizeCabinet(
        CabinetId CabinetId,
        Length NewNominalWidth) : DomainOperation;

    public sealed record InsertFiller(
        RunId RunId,
        int InsertIndex,
        Length FillerWidth) : DomainOperation;

    public sealed record UpdateRunCapacity(
        RunId RunId,
        Length NewCapacity) : DomainOperation;

    public sealed record DeleteRun(
        RunId RunId) : DomainOperation;

    public sealed record SetCabinetOverride(
        CabinetId CabinetId,
        string OverrideKey) : DomainOperation;
}
