using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering;

public static class CabinetRenderModelFactory
{
    public static IReadOnlyList<HandleRenderDto> CreateHandles(Guid cabinetId, Rect2D worldBounds)
    {
        var center = worldBounds.Center;
        var rightCenter = new Point2D(worldBounds.Max.X, center.Y);

        return
        [
            CreateHandle(cabinetId, HandleType.MoveOrigin, center),
            CreateHandle(cabinetId, HandleType.ResizeRight, rightCenter)
        ];
    }

    private static HandleRenderDto CreateHandle(Guid cabinetId, HandleType type, Point2D worldPosition)
    {
        var typeBytes = BitConverter.GetBytes((int)type);
        var cabinetBytes = cabinetId.ToByteArray();
        for (var i = 0; i < typeBytes.Length; i++)
        {
            cabinetBytes[i] ^= typeBytes[i];
        }

        return new HandleRenderDto(new Guid(cabinetBytes), type, worldPosition);
    }
}
