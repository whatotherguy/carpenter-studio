using System;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.TemplateContext;

public sealed record StylePreset
{
    public string PresetId { get; init; }
    public string Name { get; init; }
    public MaterialId? DefaultCaseMaterialId { get; init; }
    public MaterialId? DefaultFrontMaterialId { get; init; }
    public string? DefaultEdgeBandingId { get; init; }
    public string? DefaultHardwareProfileId { get; init; }

    public StylePreset(
        string presetId,
        string name,
        MaterialId? defaultCaseMaterialId,
        MaterialId? defaultFrontMaterialId,
        string? defaultEdgeBandingId,
        string? defaultHardwareProfileId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            throw new InvalidOperationException("Style preset identifier is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Style preset name is required.");

        PresetId = presetId;
        Name = name;
        DefaultCaseMaterialId = defaultCaseMaterialId;
        DefaultFrontMaterialId = defaultFrontMaterialId;
        DefaultEdgeBandingId = defaultEdgeBandingId;
        DefaultHardwareProfileId = defaultHardwareProfileId;
    }
}
