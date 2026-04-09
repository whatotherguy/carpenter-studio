using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.SpatialContext;

public sealed class WallTests
{
    [Fact]
    public void Constructor_ComputesLengthAndDirection()
    {
        var wall = CreateWall(new Point2D(0m, 0m), new Point2D(120m, 0m));

        Assert.Equal(Length.FromInches(120m), wall.Length);
        Assert.Equal(new Vector2D(1m, 0m), wall.Direction);
    }

    [Fact]
    public void AddOpening_ValidOpeningSucceeds()
    {
        var wall = CreateWall();

        var opening = wall.AddOpening(
            WallOpeningType.Window,
            Length.FromInches(10m),
            Length.FromInches(30m),
            Length.FromInches(40m),
            Length.FromInches(36m));

        Assert.Single(wall.Openings);
        Assert.Equal(wall.Id, opening.WallId);
    }

    [Fact]
    public void AddOpening_OverlappingOpeningThrows()
    {
        var wall = CreateWall();
        wall.AddOpening(WallOpeningType.Window, Length.FromInches(10m), Length.FromInches(30m), Length.FromInches(40m), Length.FromInches(36m));

        Assert.Throws<InvalidOperationException>(() =>
            wall.AddOpening(WallOpeningType.Door, Length.FromInches(25m), Length.FromInches(20m), Length.FromInches(80m), Length.Zero));
    }

    [Fact]
    public void AddOpening_BeyondWallThrows()
    {
        var wall = CreateWall();

        Assert.Throws<InvalidOperationException>(() =>
            wall.AddOpening(WallOpeningType.Window, Length.FromInches(100m), Length.FromInches(30m), Length.FromInches(40m), Length.FromInches(36m)));
    }

    [Fact]
    public void AvailableLength_SubtractsOpeningWidths()
    {
        var wall = CreateWall();
        wall.AddOpening(WallOpeningType.Window, Length.FromInches(10m), Length.FromInches(20m), Length.FromInches(40m), Length.FromInches(36m));
        wall.AddOpening(WallOpeningType.Door, Length.FromInches(40m), Length.FromInches(30m), Length.FromInches(80m), Length.Zero);

        Assert.Equal(Length.FromInches(70m), wall.AvailableLength);
    }

    private static Wall CreateWall(Point2D? start = null, Point2D? end = null)
        => new(
            WallId.New(),
            RoomId.New(),
            start ?? Point2D.Origin,
            end ?? new Point2D(120m, 0m),
            Thickness.Exact(Length.FromInches(4m)));
}
