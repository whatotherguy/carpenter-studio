using System;
using System.Linq;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using Xunit;

namespace CabinetDesigner.Tests.RunContext;

public sealed class CabinetRunTests
{
    [Fact]
    public void AppendCabinet_AddsSlotAtEnd()
    {
        var run = CreateRun();
        var slot = run.AppendCabinet(CabinetId.New(), Length.FromInches(30m));

        Assert.Single(run.Slots);
        Assert.Equal(0, slot.SlotIndex);
    }

    [Fact]
    public void InsertCabinetAt_InsertsAndReindexes()
    {
        var run = CreateRun();
        run.AppendCabinet(CabinetId.New(), Length.FromInches(30m));
        run.AppendCabinet(CabinetId.New(), Length.FromInches(30m));

        var inserted = run.InsertCabinetAt(1, CabinetId.New(), Length.FromInches(15m));

        Assert.Equal(new[] { 0, 1, 2 }, run.Slots.Select(slot => slot.SlotIndex).ToArray());
        Assert.Equal(inserted.Id, run.Slots[1].Id);
    }

    [Fact]
    public void RemoveSlot_RemovesAndReindexes()
    {
        var run = CreateRun();
        var first = run.AppendCabinet(CabinetId.New(), Length.FromInches(20m));
        run.AppendCabinet(CabinetId.New(), Length.FromInches(20m));

        run.RemoveSlot(first.Id);

        Assert.Single(run.Slots);
        Assert.Equal(0, run.Slots[0].SlotIndex);
    }

    [Fact]
    public void InsertFiller_CreatesFillerSlot()
    {
        var run = CreateRun();

        var filler = run.InsertFiller(0, Length.FromInches(3m));

        Assert.Equal(RunSlotType.Filler, filler.SlotType);
        Assert.Null(filler.CabinetId);
    }

    [Fact]
    public void OccupiedLength_AndRemainingLength_AreComputed()
    {
        var run = CreateRun();
        run.AppendCabinet(CabinetId.New(), Length.FromInches(30m));
        run.InsertFiller(1, Length.FromInches(6m));

        Assert.Equal(Length.FromInches(36m), run.OccupiedLength);
        Assert.Equal(Length.FromInches(84m), run.RemainingLength);
    }

    [Fact]
    public void AppendCabinet_ExceedingCapacityThrows()
    {
        var run = CreateRun(Length.FromInches(20m));

        Assert.Throws<InvalidOperationException>(() =>
            run.AppendCabinet(CabinetId.New(), Length.FromInches(21m)));
    }

    [Fact]
    public void EndConditions_CanBeUpdated()
    {
        var run = CreateRun();

        run.SetLeftEndCondition(EndConditionType.AgainstWall);
        run.SetRightEndCondition(EndConditionType.Filler, Length.FromInches(2m));

        Assert.Equal(EndConditionType.AgainstWall, run.LeftEndCondition.Type);
        Assert.Equal(EndConditionType.Filler, run.RightEndCondition.Type);
        Assert.Equal(Length.FromInches(2m), run.RightEndCondition.FillerWidth);
    }

    [Fact]
    public void SlotOffset_ReturnsCumulativeWidth()
    {
        var run = CreateRun();
        run.AppendCabinet(CabinetId.New(), Length.FromInches(24m));
        run.InsertFiller(1, Length.FromInches(3m));
        run.AppendCabinet(CabinetId.New(), Length.FromInches(18m));

        Assert.Equal(Length.FromInches(27m), run.SlotOffset(2));
    }

    [Fact]
    public void CabinetCount_CountsOnlyCabinetSlots()
    {
        var run = CreateRun();
        run.AppendCabinet(CabinetId.New(), Length.FromInches(24m));
        run.InsertFiller(1, Length.FromInches(3m));
        run.AppendCabinet(CabinetId.New(), Length.FromInches(18m));

        Assert.Equal(2, run.CabinetCount);
    }

    private static CabinetRun CreateRun(Length? capacity = null)
        => new(RunId.New(), WallId.New(), capacity ?? Length.FromInches(120m));
}
