using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Rendering.DTOs;

public sealed record SelectionOverlayDto(
    IReadOnlyList<Guid> SelectedCabinetIds,
    Rect2D? MultiSelectionBounds,
    IReadOnlyList<HandleRenderDto> Handles,
    bool IsResizingAtMinimum = false);
