using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using Xunit;

namespace CabinetDesigner.Tests.RunContext;

public sealed class RunSlotTests
{
    [Fact]
    public void ForCabinet_SetsCabinetProperties()
    {
        var cabinetId = CabinetId.New();

        var slot = RunSlot.ForCabinet(RunSlotId.New(), RunId.New(), cabinetId, Length.FromInches(30m), 2);

        Assert.Equal(RunSlotType.Cabinet, slot.SlotType);
        Assert.Equal(cabinetId, slot.CabinetId);
        Assert.Equal(2, slot.SlotIndex);
    }

    [Fact]
    public void ForFiller_SetsCabinetIdToNull()
    {
        var slot = RunSlot.ForFiller(RunSlotId.New(), RunId.New(), Length.FromInches(3m), 1);

        Assert.Equal(RunSlotType.Filler, slot.SlotType);
        Assert.Null(slot.CabinetId);
    }
}
