using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.SpatialContext;

public sealed class WallOpeningTests
{
    [Fact]
    public void AllowsCabinetsBelow_IsTrueOnlyForWindows()
    {
        var window = CreateOpening(WallOpeningType.Window);
        var door = CreateOpening(WallOpeningType.Door);

        Assert.True(window.AllowsCabinetsBelow);
        Assert.False(door.AllowsCabinetsBelow);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var wallId = WallId.New();
        var opening = new WallOpening(
            WallOpeningId.New(),
            wallId,
            WallOpeningType.Passthrough,
            Length.FromInches(12m),
            Length.FromInches(36m),
            Length.FromInches(24m),
            Length.FromInches(42m));

        Assert.Equal(wallId, opening.WallId);
        Assert.Equal(Length.FromInches(36m), opening.Width);
        Assert.Equal(Length.FromInches(24m), opening.Height);
    }

    private static WallOpening CreateOpening(WallOpeningType type)
        => new(
            WallOpeningId.New(),
            WallId.New(),
            type,
            Length.FromInches(5m),
            Length.FromInches(20m),
            Length.FromInches(30m),
            Length.FromInches(12m));
}
