using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Rendering.DTOs;

public sealed record WallRenderDto(
    Guid WallId,
    LineSegment2D Segment,
    bool IsHighlighted);
