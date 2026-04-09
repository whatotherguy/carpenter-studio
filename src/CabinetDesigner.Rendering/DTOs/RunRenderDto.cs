using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Rendering.DTOs;

public sealed record RunRenderDto(
    Guid RunId,
    LineSegment2D AxisSegment,
    Rect2D BoundingRect,
    bool IsActive);
