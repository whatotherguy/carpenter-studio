using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.SpatialContext;

public sealed class Obstacle
{
    public ObstacleId Id { get; }
    public RoomId RoomId { get; }
    public Rect2D Bounds { get; }
    public string Description { get; }

    public Obstacle(ObstacleId id, RoomId roomId, Rect2D bounds, string description)
    {
        if (id == default)
            throw new InvalidOperationException("Obstacle must have an identifier.");
        if (roomId == default)
            throw new InvalidOperationException("Obstacle must belong to a room.");
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException("Obstacle description is required.");
        if (bounds.Width <= Length.Zero || bounds.Height <= Length.Zero)
            throw new InvalidOperationException("Obstacle bounds must be positive.");

        Id = id;
        RoomId = roomId;
        Bounds = bounds;
        Description = description;
    }
}
