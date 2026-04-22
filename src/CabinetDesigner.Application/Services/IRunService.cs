using System.Threading.Tasks;
using System.Threading;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Application.Services;

public interface IRunService
{
    Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request);

    Task<CabinetRun> CreateRunAsync(RoomId roomId, WallId wallId, Length start, Length initialLength, CancellationToken ct = default)
        => Task.FromException<CabinetRun>(new InvalidOperationException("RUNSERVICE_CREATE_RUN_NOT_AVAILABLE"));

    Task<CommandResultDto> DeleteRunAsync(RunId runId);

    Task DeleteRunAsync(RunId runId, CancellationToken ct = default)
        => Task.FromException(new InvalidOperationException("RUNSERVICE_DELETE_RUN_NOT_AVAILABLE"));

    Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request);

    Task<Cabinet> PlaceCabinetAsync(RunId runId, string cabinetTypeId, CancellationToken ct = default)
        => Task.FromException<Cabinet>(new InvalidOperationException("RUNSERVICE_PLACE_CABINET_NOT_AVAILABLE"));

    Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request);

    Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request);

    Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request);

    Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request);

    Task DeleteCabinetAsync(CabinetId cabinetId, CancellationToken ct = default)
        => Task.FromException(new InvalidOperationException("RUNSERVICE_DELETE_CABINET_NOT_AVAILABLE"));

    RunSummaryDto GetRunSummary(RunId runId);
}
