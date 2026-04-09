using System.Collections.Generic;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.CabinetContext;

public sealed class CabinetTypeTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var type = CreateType();

        Assert.Equal("base-36", type.TypeId);
        Assert.Equal(CabinetCategory.Base, type.Category);
        Assert.Equal(2, type.DefaultOpeningCount);
    }

    [Fact]
    public void RecordEquality_WorksForSameValues()
    {
        var first = CreateType();
        var second = CreateType();

        Assert.Equal(first, second);
    }

    private static CabinetType CreateType() =>
        new(
            "base-36",
            "Base 36",
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            new List<Length> { Length.FromInches(30m), Length.FromInches(36m) },
            Length.FromInches(24m),
            Length.FromInches(34.5m),
            2);
}
