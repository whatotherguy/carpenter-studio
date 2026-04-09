using System.Collections.Generic;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Commands;

public sealed class StateDeltaTests
{
    [Fact]
    public void Constructor_SetsAllFields()
    {
        var delta = new StateDelta("cabinet-1", "Cabinet", DeltaOperation.Modified);

        Assert.Equal("cabinet-1", delta.EntityId);
        Assert.Equal("Cabinet", delta.EntityType);
        Assert.Equal(DeltaOperation.Modified, delta.Operation);
    }

    [Fact]
    public void PreviousValues_DefaultsToNull()
    {
        var delta = new StateDelta("cabinet-1", "Cabinet", DeltaOperation.Created);

        Assert.Null(delta.PreviousValues);
    }

    [Fact]
    public void NewValues_DefaultsToNull()
    {
        var delta = new StateDelta("cabinet-1", "Cabinet", DeltaOperation.Removed);

        Assert.Null(delta.NewValues);
    }

    [Fact]
    public void Constructor_WithValues_SetsBoth()
    {
        IReadOnlyDictionary<string, DeltaValue> previousValues = new Dictionary<string, DeltaValue>
        {
            ["Width"] = new DeltaValue.OfLength(Length.FromInches(24m))
        };
        IReadOnlyDictionary<string, DeltaValue> newValues = new Dictionary<string, DeltaValue>
        {
            ["Width"] = new DeltaValue.OfLength(Length.FromInches(30m))
        };

        var delta = new StateDelta(
            "cabinet-1",
            "Cabinet",
            DeltaOperation.Modified,
            previousValues,
            newValues);

        Assert.Same(previousValues, delta.PreviousValues);
        Assert.Same(newValues, delta.NewValues);
    }
}
