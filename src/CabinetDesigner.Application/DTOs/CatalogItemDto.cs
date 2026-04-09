namespace CabinetDesigner.Application.DTOs;

public sealed record CatalogItemDto(
    string TypeId,
    string DisplayName,
    string Category,
    string Description,
    decimal DefaultNominalWidthInches);
