using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;

namespace CabinetDesigner.Application.Services;

public interface IRoomService
{
    Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct);

    Task<Wall> AddWallAsync(RoomId roomId, Point2D start, Point2D end, Thickness thickness, CancellationToken ct);

    Task RemoveWallAsync(WallId wallId, CancellationToken ct);

    Task RenameRoomAsync(RoomId roomId, string newName, CancellationToken ct);

    Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken ct);
}
