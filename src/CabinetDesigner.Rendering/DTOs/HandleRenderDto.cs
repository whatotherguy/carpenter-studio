using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Rendering.DTOs;

public enum HandleType
{
    MoveOrigin,
    ResizeRight
}

public sealed record HandleRenderDto(Guid HandleId, HandleType Type, Point2D WorldPosition);
