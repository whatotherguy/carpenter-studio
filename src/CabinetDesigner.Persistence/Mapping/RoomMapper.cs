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
            new RoomShapePayload(LengthText.FormatLength(room.CeilingHeight), []),
            SqliteJson.Options),
        CreatedAt = timestamp.UtcDateTime.ToString("O"),
        UpdatedAt = timestamp.UtcDateTime.ToString("O")
    };

    public static Room ToDomain(RoomRow row)
    {
        var payload = JsonSerializer.Deserialize<RoomShapePayload>(row.ShapeJson, SqliteJson.Options)
            ?? throw new FormatException("Room shape payload missing.");
        return new Room(
            new RoomId(Guid.Parse(row.Id)),
            new RevisionId(Guid.Parse(row.RevisionId)),
            row.Name ?? "Room",
            LengthText.ParseLength(payload.CeilingHeight));
    }

    private sealed record RoomShapePayload(string CeilingHeight, IReadOnlyList<string> Obstacles);
}
