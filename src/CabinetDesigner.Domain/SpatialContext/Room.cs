using System;
using System.Collections.Generic;
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

    public Wall AddWall(Point2D start, Point2D end, Thickness wallThickness)
    {
        var wall = new Wall(WallId.New(), Id, start, end, wallThickness);
        _walls.Add(wall);
        return wall;
    }

    public Obstacle AddObstacle(Rect2D bounds, string description)
    {
        var obstacle = new Obstacle(ObstacleId.New(), Id, bounds, description);
        _obstacles.Add(obstacle);
        return obstacle;
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
