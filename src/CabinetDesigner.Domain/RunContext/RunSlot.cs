using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.RunContext;

public sealed record RunSlot
{
    public RunSlotId Id { get; private init; }
    public RunId RunId { get; private init; }
    public RunSlotType SlotType { get; private init; }
    public int SlotIndex { get; internal init; }
    public Length OccupiedWidth { get; private init; }
    public CabinetId? CabinetId { get; private init; }

    public static RunSlot ForCabinet(RunSlotId id, RunId runId, CabinetId cabinetId, Length width, int index)
    {
        if (id == default)
            throw new InvalidOperationException("Run slot must have an identifier.");
        if (runId == default)
            throw new InvalidOperationException("Run slot must belong to a run.");
        if (cabinetId == default)
            throw new InvalidOperationException("Cabinet slot must reference a cabinet.");
        if (width <= Length.Zero)
            throw new InvalidOperationException("Cabinet slot width must be positive.");
        if (index < 0)
            throw new InvalidOperationException("Slot index cannot be negative.");

        return new RunSlot
        {
            Id = id,
            RunId = runId,
            SlotType = RunSlotType.Cabinet,
            SlotIndex = index,
            OccupiedWidth = width,
            CabinetId = cabinetId
        };
    }

    public static RunSlot ForFiller(RunSlotId id, RunId runId, Length width, int index)
    {
        if (id == default)
            throw new InvalidOperationException("Run slot must have an identifier.");
        if (runId == default)
            throw new InvalidOperationException("Run slot must belong to a run.");
        if (width <= Length.Zero)
            throw new InvalidOperationException("Filler slot width must be positive.");
        if (index < 0)
            throw new InvalidOperationException("Slot index cannot be negative.");

        return new RunSlot
        {
            Id = id,
            RunId = runId,
            SlotType = RunSlotType.Filler,
            SlotIndex = index,
            OccupiedWidth = width,
            CabinetId = null
        };
    }
}
