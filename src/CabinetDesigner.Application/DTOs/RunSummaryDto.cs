namespace CabinetDesigner.Application.DTOs;

public sealed record RunSummaryDto(
    Guid RunId,
    string WallId,
    decimal TotalNominalWidthInches,
    int CabinetCount,
    bool HasFillers,
    bool HasValidationErrors,
    bool IsOverCapacity,
    decimal RemainingLengthInches,
    decimal OverCapacityAmountInches,
    IReadOnlyList<RunSlotSummaryDto> Slots);

public sealed record RunSlotSummaryDto(
    Guid CabinetId,
    string CabinetTypeId,
    decimal NominalWidthInches,
    int Index);
