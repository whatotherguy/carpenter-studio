using System;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.CabinetContext;

public sealed class CabinetConstructorInvariantTests
{
    [Fact]
    public void Constructor_DefaultCabinetId_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Cabinet(
            default,
            RevisionId.New(),
            "base-36",
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            Length.FromInches(36m),
            Length.FromInches(24m),
            Length.FromInches(34.5m)));
    }

    [Fact]
    public void Constructor_ZeroWidth_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Cabinet(
            CabinetId.New(),
            RevisionId.New(),
            "base-36",
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            Length.Zero,
            Length.FromInches(24m),
            Length.FromInches(34.5m)));
    }

    [Fact]
    public void RoomConstructor_NonPositiveCeilingHeight_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Room(
            RoomId.New(),
            RevisionId.New(),
            "Kitchen",
            Length.Zero));
    }

    [Fact]
    public void WallConstructor_ZeroLength_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Wall(
            WallId.New(),
            RoomId.New(),
            Point2D.Origin,
            Point2D.Origin,
            Thickness.Exact(Length.FromInches(4m))));
    }
}
