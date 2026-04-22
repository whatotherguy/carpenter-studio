using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Application.DTOs;

public sealed record CatalogItemDto(
    string TypeId,
    string DisplayName,
    string Category,
    ConstructionMethod ConstructionMethod,
    Length NominalWidth,
    Length Depth,
    Length Height,
    int DefaultOpenings,
    string Description)
{
    public decimal DefaultNominalWidthInches => NominalWidth.Inches;
}
