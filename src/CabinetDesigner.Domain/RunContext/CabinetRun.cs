using System;
using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.RunContext;

public sealed class CabinetRun
{
    private readonly List<RunSlot> _slots = [];

    public RunId Id { get; }
    public WallId WallId { get; }
    public Length Capacity { get; private set; }
    public EndCondition LeftEndCondition { get; private set; }
    public EndCondition RightEndCondition { get; private set; }
    public IReadOnlyList<RunSlot> Slots => _slots;
    public Length OccupiedLength => _slots.Aggregate(Length.Zero, (sum, slot) => sum + slot.OccupiedWidth);

    public Length RemainingLength
    {
        get
        {
            return OccupiedLength <= Capacity
                ? (Capacity - OccupiedLength).Abs()
                : Length.Zero;
        }
    }

    public int CabinetCount => _slots.Count(slot => slot.SlotType == RunSlotType.Cabinet);

    public CabinetRun(RunId id, WallId wallId, Length capacity)
    {
        if (id == default)
            throw new InvalidOperationException("Run must have an identifier.");
        if (wallId == default)
            throw new InvalidOperationException("Run must belong to a wall.");
        if (capacity <= Length.Zero)
            throw new InvalidOperationException("Run capacity must be positive.");

        Id = id;
        WallId = wallId;
        Capacity = capacity;
        LeftEndCondition = EndCondition.Open();
        RightEndCondition = EndCondition.Open();
    }

    public static CabinetRun Reconstitute(
        RunId id,
        WallId wallId,
        Length capacity,
        IReadOnlyList<RunSlot> slots,
        EndCondition? leftEndCondition = null,
        EndCondition? rightEndCondition = null)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var run = new CabinetRun(id, wallId, capacity)
        {
            LeftEndCondition = leftEndCondition ?? EndCondition.Open(),
            RightEndCondition = rightEndCondition ?? EndCondition.Open()
        };

        foreach (var slot in slots.OrderBy(slot => slot.SlotIndex))
        {
            if (slot.RunId != id)
            {
                throw new InvalidOperationException($"Slot {slot.Id} does not belong to run {id}.");
            }

            if (slot.SlotIndex != run._slots.Count)
            {
                throw new InvalidOperationException("Run slots must be contiguous and ordered during reconstitution.");
            }

            run.ValidateSlotFits(slot);
            run._slots.Add(slot);
        }

        return run;
    }

    public void UpdateCapacity(Length newCapacity)
    {
        if (newCapacity <= Length.Zero)
            throw new InvalidOperationException("Run capacity must be positive.");
        if (OccupiedLength > newCapacity)
            throw new InvalidOperationException("Run capacity cannot be reduced below occupied length.");

        Capacity = newCapacity;
    }

    public RunSlot AppendCabinet(CabinetId cabinetId, Length nominalWidth)
    {
        var slot = RunSlot.ForCabinet(RunSlotId.New(), Id, cabinetId, nominalWidth, _slots.Count);
        ValidateSlotFits(slot);
        _slots.Add(slot);
        return slot;
    }

    public RunSlot InsertCabinetAt(int index, CabinetId cabinetId, Length nominalWidth)
    {
        if (index < 0 || index > _slots.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var slot = RunSlot.ForCabinet(RunSlotId.New(), Id, cabinetId, nominalWidth, index);
        ValidateSlotFits(slot);
        _slots.Insert(index, slot);
        ReindexSlots();
        return slot;
    }

    public void RemoveSlot(RunSlotId slotId)
    {
        var index = _slots.FindIndex(slot => slot.Id == slotId);
        if (index < 0)
            throw new InvalidOperationException($"Slot {slotId} not found in run.");

        _slots.RemoveAt(index);
        ReindexSlots();
    }

    public RunSlot InsertFiller(int index, Length fillerWidth)
    {
        if (index < 0 || index > _slots.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var slot = RunSlot.ForFiller(RunSlotId.New(), Id, fillerWidth, index);
        ValidateSlotFits(slot);
        _slots.Insert(index, slot);
        ReindexSlots();
        return slot;
    }

    public void SetLeftEndCondition(EndConditionType type, Length? fillerWidth = null)
    {
        LeftEndCondition = new EndCondition(type, fillerWidth);
    }

    public void SetRightEndCondition(EndConditionType type, Length? fillerWidth = null)
    {
        RightEndCondition = new EndCondition(type, fillerWidth);
    }

    public Length SlotOffset(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > _slots.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        return _slots
            .Take(slotIndex)
            .Aggregate(Length.Zero, (sum, slot) => sum + slot.OccupiedWidth);
    }

    private void ValidateSlotFits(RunSlot slot)
    {
        var projectedOccupied = OccupiedLength + slot.OccupiedWidth;
        if (projectedOccupied > Capacity)
            throw new InvalidOperationException(
                $"Slot width ({slot.OccupiedWidth}) exceeds remaining run capacity ({RemainingLength}).");
    }

    private void ReindexSlots()
    {
        for (var index = 0; index < _slots.Count; index++)
            _slots[index] = _slots[index] with { SlotIndex = index };
    }
}
