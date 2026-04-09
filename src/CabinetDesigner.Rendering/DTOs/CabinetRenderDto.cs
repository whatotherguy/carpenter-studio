using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Rendering.DTOs;

public sealed record CabinetRenderDto(
    Guid CabinetId,
    Guid RunId,
    Rect2D WorldBounds,
    string Label,
    string TypeDisplayName,
    CabinetRenderState State,
    IReadOnlyList<HandleRenderDto> Handles);
