using CabinetDesigner.Application.DTOs;

namespace CabinetDesigner.Application.Services;

public interface ICatalogService
{
    IReadOnlyList<CatalogItemDto> GetAllItems();
}
