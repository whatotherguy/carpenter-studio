using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.MaterialContext;

public sealed class Material
{
    public MaterialId Id { get; }
    public string Name { get; }
    public string? Sku { get; }
    public MaterialCategory Category { get; }
    public Thickness SheetThickness { get; }
    public GrainDirection Grain { get; }
    public Length SheetWidth { get; }
    public Length SheetHeight { get; }

    public Material(
        MaterialId id,
        string name,
        string? sku,
        MaterialCategory category,
        Thickness sheetThickness,
        GrainDirection grain,
        Length sheetWidth,
        Length sheetHeight)
    {
        if (id == default)
            throw new InvalidOperationException("Material must have an identifier.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Material name is required.");
        if (sheetWidth <= Length.Zero)
            throw new InvalidOperationException("Sheet width must be positive.");
        if (sheetHeight <= Length.Zero)
            throw new InvalidOperationException("Sheet height must be positive.");
        if (RequiresNoGrain(category) && grain != GrainDirection.None)
            throw new InvalidOperationException("This material category cannot have a grain direction.");

        Id = id;
        Name = name;
        Sku = sku;
        Category = category;
        SheetThickness = sheetThickness;
        Grain = grain;
        SheetWidth = sheetWidth;
        SheetHeight = sheetHeight;
    }

    private static bool RequiresNoGrain(MaterialCategory category)
        => category is MaterialCategory.MDF or MaterialCategory.Melamine or MaterialCategory.Particleboard or MaterialCategory.Laminate;
}
