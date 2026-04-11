using System.Threading.Tasks;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Commands.Structural;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;

namespace CabinetDesigner.Application.Services;

public sealed class RunService : IRunService
{
    private readonly IDesignCommandHandler _handler;
    private readonly IClock _clock;
    private readonly IDesignStateStore _stateStore;

    public RunService(IDesignCommandHandler handler, IClock clock, IDesignStateStore stateStore)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateRunCommand(
            new Point2D(request.StartXInches, request.StartYInches),
            new Point2D(request.EndXInches, request.EndYInches),
            request.WallId,
            CommandOrigin.User,
            $"Create run on wall {request.WallId}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> DeleteRunAsync(RunId runId) =>
        throw new NotImplementedException("NOT IMPLEMENTED YET: DeleteRunCommand has not been introduced in the domain layer.");

    public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new AddCabinetToRunCommand(
            new RunId(request.RunId),
            request.CabinetTypeId,
            Length.FromInches(request.NominalWidthInches),
            Enum.Parse<RunPlacement>(request.Placement, ignoreCase: true),
            CommandOrigin.User,
            $"Add {request.NominalWidthInches}\" {request.CabinetTypeId} to run",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new InsertCabinetIntoRunCommand(
            new RunId(request.RunId),
            request.CabinetTypeId,
            Length.FromInches(request.NominalWidthInches),
            request.InsertAtIndex,
            new CabinetId(request.LeftNeighborId),
            new CabinetId(request.RightNeighborId),
            CommandOrigin.User,
            $"Insert {request.CabinetTypeId} at index {request.InsertAtIndex}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new MoveCabinetCommand(
            new CabinetId(request.CabinetId),
            new RunId(request.SourceRunId),
            new RunId(request.TargetRunId),
            Enum.Parse<RunPlacement>(request.TargetPlacement, ignoreCase: true),
            CommandOrigin.User,
            $"Move cabinet {request.CabinetId} to run {request.TargetRunId}",
            _clock.Now,
            request.TargetIndex);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new ResizeCabinetCommand(
            new CabinetId(request.CabinetId),
            Length.FromInches(request.CurrentNominalWidthInches),
            Length.FromInches(request.NewNominalWidthInches),
            CommandOrigin.User,
            $"Resize cabinet {request.CabinetId} to {request.NewNominalWidthInches}\"",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) =>
        throw new NotImplementedException("NOT IMPLEMENTED YET: SetCabinetOverrideCommand has not been introduced in the domain layer.");

    public RunSummaryDto GetRunSummary(RunId runId)
    {
        ArgumentNullException.ThrowIfNull(runId);

        var run = _stateStore.GetRun(runId)
            ?? throw new KeyNotFoundException($"Run {runId.Value} was not found in the design state store.");

        var slots = run.Slots
            .Where(slot => slot.SlotType == RunSlotType.Cabinet && slot.CabinetId is not null)
            .Select(slot =>
            {
                var cabinet = _stateStore.GetCabinet(slot.CabinetId!.Value);
                return new RunSlotSummaryDto(
                    slot.CabinetId.Value.Value,
                    cabinet?.CabinetTypeId ?? "Unknown cabinet",
                    slot.OccupiedWidth.Inches,
                    slot.SlotIndex);
            })
            .ToArray();

        return new RunSummaryDto(
            run.Id.Value,
            run.WallId.Value.ToString(),
            slots.Sum(s => s.NominalWidthInches),
            slots.Length,
            run.Slots.Any(slot => slot.SlotType == RunSlotType.Filler),
            run.OccupiedLength > run.Capacity,
            slots);
    }
}
