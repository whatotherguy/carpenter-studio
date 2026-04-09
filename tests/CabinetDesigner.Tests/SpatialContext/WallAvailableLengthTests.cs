using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.SpatialContext;

public sealed class WallAvailableLengthTests
{
    [Fact]
    public void AvailableLength_OpeningsConsumeEntireWall_ReturnsZero()
    {
        var wall = new Wall(
            WallId.New(),
            RoomId.New(),
            Point2D.Origin,
            new Point2D(30m, 0m),
            Thickness.Exact(Length.FromInches(4m)));

        wall.AddOpening(
            WallOpeningType.Door,
            Length.Zero,
            Length.FromInches(30m),
            Length.FromInches(80m),
            Length.Zero);

        Assert.Equal(Length.Zero, wall.AvailableLength);
    }
}
