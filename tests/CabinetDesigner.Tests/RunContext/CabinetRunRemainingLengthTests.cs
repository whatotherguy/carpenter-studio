using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using Xunit;

namespace CabinetDesigner.Tests.RunContext;

public sealed class CabinetRunRemainingLengthTests
{
    [Fact]
    public void RemainingLength_WhenFullyOccupied_ReturnsZero()
    {
        var run = CreateRun(Length.FromInches(30m));

        run.AppendCabinet(CabinetId.New(), Length.FromInches(30m));

        Assert.Equal(Length.Zero, run.RemainingLength);
    }

    [Fact]
    public void UpdateCapacity_BelowOccupiedLength_Throws()
    {
        var run = CreateRun(Length.FromInches(48m));
        run.AppendCabinet(CabinetId.New(), Length.FromInches(30m));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            run.UpdateCapacity(Length.FromInches(24m)));

        Assert.Contains("cannot be reduced below occupied length", exception.Message);
    }

    private static CabinetRun CreateRun(Length capacity)
        => new(RunId.New(), WallId.New(), capacity);
}
