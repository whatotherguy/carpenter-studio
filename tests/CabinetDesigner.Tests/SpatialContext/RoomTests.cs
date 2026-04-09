using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.SpatialContext;

public sealed class RoomTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var room = new Room(RoomId.New(), RevisionId.New(), "Kitchen", Length.FromFeet(8m));

        Assert.Equal("Kitchen", room.Name);
        Assert.Equal(Length.FromFeet(8m), room.CeilingHeight);
        Assert.Empty(room.Walls);
        Assert.Empty(room.Obstacles);
    }

    [Fact]
    public void AddWall_CreatesWall()
    {
        var room = CreateRoom();

        var wall = room.AddWall(new Point2D(0m, 0m), new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)));

        Assert.Single(room.Walls);
        Assert.Equal(room.Id, wall.RoomId);
    }

    [Fact]
    public void AddObstacle_CreatesObstacle()
    {
        var room = CreateRoom();

        var obstacle = room.AddObstacle(
            new Rect2D(Point2D.Origin, Length.FromInches(12m), Length.FromInches(24m)),
            "Vent");

        Assert.Single(room.Obstacles);
        Assert.Equal(room.Id, obstacle.RoomId);
    }

    [Fact]
    public void IsEnclosed_ReturnsTrueForClosedLoop()
    {
        var room = CreateRoom();
        var thickness = Thickness.Exact(Length.FromInches(4m));
        room.AddWall(new Point2D(0m, 0m), new Point2D(100m, 0m), thickness);
        room.AddWall(new Point2D(100m, 0m), new Point2D(100m, 100m), thickness);
        room.AddWall(new Point2D(100m, 100m), new Point2D(0m, 0m), thickness);

        Assert.True(room.IsEnclosed);
    }

    [Fact]
    public void IsEnclosed_ReturnsFalseForOpenConfiguration()
    {
        var room = CreateRoom();
        var thickness = Thickness.Exact(Length.FromInches(4m));
        room.AddWall(new Point2D(0m, 0m), new Point2D(100m, 0m), thickness);
        room.AddWall(new Point2D(100m, 0m), new Point2D(100m, 100m), thickness);
        room.AddWall(new Point2D(90m, 100m), new Point2D(0m, 0m), thickness);

        Assert.False(room.IsEnclosed);
    }

    private static Room CreateRoom() => new(RoomId.New(), RevisionId.New(), "Kitchen", Length.FromFeet(8m));
}
