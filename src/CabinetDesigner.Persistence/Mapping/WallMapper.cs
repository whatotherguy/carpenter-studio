using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class WallMapper
{
    public static WallRow ToRow(Wall wall, RevisionId revisionId, DateTimeOffset timestamp) => new()
    {
        Id = wall.Id.Value.ToString(),
        RevisionId = revisionId.Value.ToString(),
        RoomId = wall.RoomId.Value.ToString(),
        StartPoint = LengthText.FormatPoint(wall.StartPoint),
        EndPoint = LengthText.FormatPoint(wall.EndPoint),
        Thickness = LengthText.FormatThickness(wall.WallThickness),
        CreatedAt = timestamp.UtcDateTime.ToString("O"),
        UpdatedAt = timestamp.UtcDateTime.ToString("O")
    };

    public static Wall ToDomain(WallRow row) => new(
        new WallId(Guid.Parse(row.Id)),
        new RoomId(Guid.Parse(row.RoomId)),
        LengthText.ParsePoint(row.StartPoint),
        LengthText.ParsePoint(row.EndPoint),
        LengthText.ParseThickness(row.Thickness));
}
