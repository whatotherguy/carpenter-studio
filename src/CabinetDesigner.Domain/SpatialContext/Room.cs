using System;
using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.SpatialContext;

public sealed class Room
{
    private readonly List<Wall> _walls = [];
    private readonly List<Obstacle> _obstacles = [];

    public RoomId Id { get; }
    public RevisionId RevisionId { get; }
    public string Name { get; private set; }
    public Length CeilingHeight { get; private set; }
    public IReadOnlyList<Wall> Walls => _walls;
    public IReadOnlyList<Obstacle> Obstacles => _obstacles;
    public bool IsEnclosed => _walls.Count >= 3 && WallsFormClosedLoop();

    public Room(RoomId id, RevisionId revisionId, string name, Length ceilingHeight)
    {
        if (id == default)
            throw new InvalidOperationException("Room must have an identifier.");
        if (revisionId == default)
            throw new InvalidOperationException("Room must belong to a revision.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Room name is required.");
        if (ceilingHeight <= Length.Zero)
            throw new InvalidOperationException("Ceiling height must be positive.");

        Id = id;
        RevisionId = revisionId;
        Name = name;
        CeilingHeight = ceilingHeight;
    }

    public static Room Reconstitute(
        RoomId id,
        RevisionId revisionId,
        string name,
        Length ceilingHeight,
        IReadOnlyList<Wall> walls,
        IReadOnlyList<Obstacle> obstacles)
    {
        var room = new Room(id, revisionId, name, ceilingHeight);
        room._walls.AddRange(walls);
        room._obstacles.AddRange(obstacles);
        return room;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new InvalidOperationException("Room name is required.");

        Name = newName;
    }

    public Wall AddWall(Point2D start, Point2D end, Thickness wallThickness)
    {
        var wall = new Wall(WallId.New(), Id, start, end, wallThickness);
        _walls.Add(wall);
        return wall;
    }

    public void RemoveWall(WallId wallId)
    {
        var wall = _walls.FirstOrDefault(candidate => candidate.Id == wallId);
        if (wall is null)
        {
            throw new InvalidOperationException($"Wall {wallId.Value} was not found in room {Id.Value}.");
        }

        _walls.Remove(wall);
    }

    public Obstacle AddObstacle(Rect2D bounds, string description)
    {
        var obstacle = new Obstacle(ObstacleId.New(), Id, bounds, description);
        _obstacles.Add(obstacle);
        return obstacle;
    }

    public void RemoveObstacle(ObstacleId obstacleId)
    {
        var obstacle = _obstacles.FirstOrDefault(candidate => candidate.Id == obstacleId);
        if (obstacle is null)
        {
            throw new InvalidOperationException($"Obstacle {obstacleId.Value} was not found in room {Id.Value}.");
        }

        _obstacles.Remove(obstacle);
    }

    private bool WallsFormClosedLoop()
    {
        for (var index = 0; index < _walls.Count; index++)
        {
            var current = _walls[index];
            var next = _walls[(index + 1) % _walls.Count];

            if (!GeometryTolerance.ApproximatelyEqual(
                    current.EndPoint,
                    next.StartPoint,
                    GeometryTolerance.DefaultShopTolerance))
            {
                return false;
            }
        }

        return true;
    }
}
