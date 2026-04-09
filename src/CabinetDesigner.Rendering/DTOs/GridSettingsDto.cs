using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Rendering.DTOs;

public sealed record GridSettingsDto(
    bool Visible,
    Length MajorSpacing,
    Length MinorSpacing);
