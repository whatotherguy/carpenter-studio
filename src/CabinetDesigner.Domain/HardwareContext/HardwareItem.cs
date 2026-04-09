using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.HardwareContext;

public sealed class HardwareItem
{
    public HardwareItemId Id { get; }
    public string Name { get; }
    public string? ManufacturerSku { get; }
    public HardwareCategory Category { get; }
    public Length? MinOpeningWidth { get; }
    public Length? MaxOpeningWidth { get; }
    public BoringPattern? BoringPattern { get; }
    public Length? RequiredClearance { get; }

    public HardwareItem(
        HardwareItemId id,
        string name,
        string? sku,
        HardwareCategory category,
        Length? minOpeningWidth = null,
        Length? maxOpeningWidth = null,
        BoringPattern? boringPattern = null,
        Length? requiredClearance = null)
    {
        if (id == default)
            throw new InvalidOperationException("Hardware item must have an identifier.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Hardware item name is required.");
        if (minOpeningWidth is not null && maxOpeningWidth is not null && minOpeningWidth > maxOpeningWidth)
            throw new InvalidOperationException("Minimum opening width cannot exceed maximum opening width.");
        if (requiredClearance is not null && requiredClearance <= Length.Zero)
            throw new InvalidOperationException("Required clearance must be positive.");

        Id = id;
        Name = name;
        ManufacturerSku = sku;
        Category = category;
        MinOpeningWidth = minOpeningWidth;
        MaxOpeningWidth = maxOpeningWidth;
        BoringPattern = boringPattern;
        RequiredClearance = requiredClearance;
    }
}
