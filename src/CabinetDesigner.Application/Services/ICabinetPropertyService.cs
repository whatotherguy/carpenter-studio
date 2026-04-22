using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.CabinetContext;

namespace CabinetDesigner.Application.Services;

public interface ICabinetPropertyService
{
    IReadOnlyList<CabinetStateRecord> GetAllCabinets();

    CabinetStateRecord? GetCabinet(Guid cabinetId);

    IReadOnlyList<CabinetStateRecord> GetCabinets(IReadOnlyList<Guid> cabinetIds);

    Task<CommandResultDto> ResizeCabinetAsync(Guid cabinetId, decimal widthInches, decimal depthInches, decimal heightInches);

    Task<CommandResultDto> SetCabinetCategoryAsync(Guid cabinetId, CabinetCategory category);

    Task<CommandResultDto> SetCabinetConstructionAsync(Guid cabinetId, ConstructionMethod construction);

    Task<CommandResultDto> AddOpeningAsync(Guid cabinetId, OpeningType openingType, decimal widthInches, decimal heightInches, int? insertIndex);

    Task<CommandResultDto> RemoveOpeningAsync(Guid cabinetId, Guid openingId);

    Task<CommandResultDto> ReorderOpeningAsync(Guid cabinetId, Guid openingId, int newIndex);

    Task<CommandResultDto> SetCabinetOverrideAsync(Guid cabinetId, string overrideKey, OverrideValueDto value);

    Task<CommandResultDto> RemoveCabinetOverrideAsync(Guid cabinetId, string overrideKey);
}
