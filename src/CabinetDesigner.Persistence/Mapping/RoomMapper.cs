using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class RoomMapper
{
    public static RoomRow ToRow(Room room, DateTimeOffset timestamp) => new()
    {
        Id = room.Id.Value.ToString(),
        RevisionId = room.RevisionId.Value.ToString(),
        Name = room.Name,
        ShapeJson = JsonSerializer.Serialize(
            RoomShapePayload.From(room),
            SqliteJson.Options),
        CreatedAt = timestamp.UtcDateTime.ToString("O"),
        UpdatedAt = timestamp.UtcDateTime.ToString("O")
    };

    public static Room ToDomain(RoomRow row)
    {
        var payload = JsonSerializer.Deserialize<RoomShapePayload>(row.ShapeJson, SqliteJson.Options)
            ?? throw new FormatException("Room shape payload missing.");
        var walls = (payload.Walls ?? [])
            .OrderBy(wall => wall.WallId)
            .Select(wall => new Wall(
                new WallId(wall.WallId),
                new RoomId(Guid.Parse(row.Id)),
                LengthText.ParsePoint(wall.StartPoint),
                LengthText.ParsePoint(wall.EndPoint),
                LengthText.ParseThickness(wall.Thickness)))
            .ToArray();

        var obstacles = (payload.Obstacles ?? [])
            .OrderBy(obstacle => obstacle.ObstacleId)
            .Select(obstacle => new Obstacle(
                new ObstacleId(obstacle.ObstacleId),
                new RoomId(Guid.Parse(row.Id)),
                new Rect2D(
                    LengthText.ParsePoint(obstacle.Bounds.MinPoint),
                    Length.FromInches(obstacle.Bounds.WidthInches),
                    Length.FromInches(obstacle.Bounds.HeightInches)),
                obstacle.Description))
            .ToArray();

        return Room.Reconstitute(
            new RoomId(Guid.Parse(row.Id)),
            new RevisionId(Guid.Parse(row.RevisionId)),
            row.Name ?? "Room",
            LengthText.ParseLength(payload.CeilingHeight),
            walls,
            obstacles);
    }

    private sealed record RoomShapePayload(
        string CeilingHeight,
        IReadOnlyList<RoomWallPayload>? Walls,
        IReadOnlyList<RoomObstaclePayload>? Obstacles)
    {
        public static RoomShapePayload From(Room room) =>
            new(
                LengthText.FormatLength(room.CeilingHeight),
                room.Walls
                    .OrderBy(wall => wall.Id.Value)
                    .Select(wall => new RoomWallPayload(
                        wall.Id.Value,
                        LengthText.FormatPoint(wall.StartPoint),
                        LengthText.FormatPoint(wall.EndPoint),
                        LengthText.FormatThickness(wall.WallThickness)))
                    .ToArray(),
                room.Obstacles
                    .OrderBy(obstacle => obstacle.Id.Value)
                    .Select(obstacle => new RoomObstaclePayload(
                        obstacle.Id.Value,
                        new RoomBoundsPayload(
                            LengthText.FormatPoint(obstacle.Bounds.Min),
                            obstacle.Bounds.Width.Inches,
                            obstacle.Bounds.Height.Inches),
                        obstacle.Description))
                    .ToArray());
    }

    private sealed record RoomWallPayload(Guid WallId, string StartPoint, string EndPoint, string Thickness);

    private sealed record RoomObstaclePayload(Guid ObstacleId, RoomBoundsPayload Bounds, string Description);

    private sealed record RoomBoundsPayload(string MinPoint, decimal WidthInches, decimal HeightInches);
}
