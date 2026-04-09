using CabinetDesigner.Application.DTOs;

namespace CabinetDesigner.Application.Services;

public interface IRunSummaryService
{
    RunSummaryProjection GetCurrentSummary(IReadOnlyList<Guid> selectedCabinetIds);
}

public sealed record RunSummaryProjection(
    bool IsProjectOpen,
    RunSummaryDto? ActiveRunSummary);
