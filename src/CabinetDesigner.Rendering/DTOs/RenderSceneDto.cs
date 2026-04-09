namespace CabinetDesigner.Rendering.DTOs;

public sealed record RenderSceneDto(
    IReadOnlyList<WallRenderDto> Walls,
    IReadOnlyList<RunRenderDto> Runs,
    IReadOnlyList<CabinetRenderDto> Cabinets,
    SelectionOverlayDto? Selection,
    GridSettingsDto Grid);
