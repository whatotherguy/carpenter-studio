using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Services;

public interface ICatalogService
{
    bool IsPricingConfigured { get; }

    IReadOnlyList<CatalogItemDto> GetAllItems();

    MaterialId ResolvePartMaterial(string partType, CabinetCategory category, ConstructionMethod construction);

    Thickness ResolvePartThickness(string partType, CabinetCategory category);

    bool IsKnownMaterial(MaterialId id);

    string GetMaterialDisplayName(MaterialId id);

    Thickness ResolveMaterialThickness(MaterialId id);

    Domain.MaterialContext.GrainDirection ResolveMaterialGrain(MaterialId id);

    IReadOnlyList<HardwareItemId> ResolveHardwareForOpening(OpeningId openingId, CabinetCategory category);

    decimal GetMaterialPricePerSquareFoot(MaterialId id, Thickness thickness);

    decimal GetHardwarePrice(HardwareItemId id);
}
