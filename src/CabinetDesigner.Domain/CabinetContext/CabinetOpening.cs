using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.CabinetContext;

public sealed class CabinetOpening
{
    private readonly List<HardwareItemId> _hardwareIds = [];

    public OpeningId Id { get; }
    public CabinetId CabinetId { get; }
    public Length Width { get; private set; }
    public Length Height { get; private set; }
    public OpeningType Type { get; private set; }
    public int Index { get; }
    public IReadOnlyList<HardwareItemId> HardwareIds => _hardwareIds;
    public MaterialId? FrontMaterialId { get; private set; }

    public CabinetOpening(
        OpeningId id,
        CabinetId cabinetId,
        Length width,
        Length height,
        OpeningType type,
        int index)
    {
        if (id == default)
            throw new InvalidOperationException("Cabinet opening must have an identifier.");
        if (cabinetId == default)
            throw new InvalidOperationException("Cabinet opening must belong to a cabinet.");
        if (width <= Length.Zero)
            throw new InvalidOperationException("Cabinet opening width must be positive.");
        if (height <= Length.Zero)
            throw new InvalidOperationException("Cabinet opening height must be positive.");
        if (index < 0)
            throw new InvalidOperationException("Cabinet opening index cannot be negative.");

        Id = id;
        CabinetId = cabinetId;
        Width = width;
        Height = height;
        Type = type;
        Index = index;
    }

    public void ChangeType(OpeningType newType)
    {
        Type = newType;
    }

    public void AssignFrontMaterial(MaterialId materialId)
    {
        if (materialId == default)
            throw new InvalidOperationException("Front material identifier is required.");

        FrontMaterialId = materialId;
    }

    public void AssignHardware(HardwareItemId hardwareId)
    {
        if (hardwareId == default)
            throw new InvalidOperationException("Hardware identifier is required.");

        if (!_hardwareIds.Contains(hardwareId))
            _hardwareIds.Add(hardwareId);
    }
}
