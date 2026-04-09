using System.Threading.Tasks;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Services;

public interface IRunService
{
    Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request);

    Task<CommandResultDto> DeleteRunAsync(RunId runId);

    Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request);

    Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request);

    Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request);

    Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request);

    Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request);

    RunSummaryDto GetRunSummary(RunId runId);
}
