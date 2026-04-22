using System.Threading.Tasks;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Commands.Structural;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;

namespace CabinetDesigner.Application.Services;

public sealed class RunService : IRunService, ICabinetPropertyService
{
    private readonly IDesignCommandHandler _handler;
    private readonly IClock _clock;
    private readonly IDesignStateStore _stateStore;
    private readonly ICatalogService _catalogService;
    private readonly ICurrentPersistedProjectState? _projectState;

    public RunService(
        IDesignCommandHandler handler,
        IClock clock,
        IDesignStateStore stateStore,
        ICatalogService? catalogService = null,
        ICurrentPersistedProjectState? projectState = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _catalogService = catalogService ?? new CatalogService();
        _projectState = projectState;
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

    public Task<CabinetRun> CreateRunAsync(
        RoomId roomId,
        WallId wallId,
        Length start,
        Length initialLength,
        CancellationToken ct = default)
    {
        _ = ct;

        if (roomId == default)
        {
            throw new InvalidOperationException("Room ID is required.");
        }

        if (wallId == default)
        {
            throw new InvalidOperationException("Wall ID is required.");
        }

        if (start < Length.Zero)
        {
            throw new InvalidOperationException("Run start must not be negative.");
        }

        if (initialLength <= Length.Zero)
        {
            throw new InvalidOperationException("Run length must be positive.");
        }

        var room = _stateStore.GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId.Value} was not found.");
        if (room.CeilingHeight <= Length.Zero)
        {
            throw new InvalidOperationException("Cannot create a run in a room with zero ceiling height.");
        }

        var wall = _stateStore.GetWall(wallId)
            ?? throw new InvalidOperationException($"Wall {wallId.Value} was not found.");
        if (wall.RoomId != roomId)
        {
            throw new InvalidOperationException($"Wall {wallId.Value} does not belong to room {roomId.Value}.");
        }

        var startWorld = wall.StartPoint + wall.Direction * start.Inches;
        var endWorld = startWorld + wall.Direction * initialLength.Inches;
        var run = new CabinetRun(RunId.New(), wallId, initialLength);
        _stateStore.AddRun(run, startWorld, endWorld);
        return Task.FromResult(run);
    }

    public Task<CommandResultDto> DeleteRunAsync(RunId runId) =>
        _handler.ExecuteAsync(new DeleteRunCommand(
            runId,
            CommandOrigin.User,
            $"Delete run {runId.Value}",
            _clock.Now));

    public Task DeleteRunAsync(RunId runId, CancellationToken ct = default)
    {
        _ = ct;

        var run = _stateStore.GetRun(runId)
            ?? throw new InvalidOperationException($"Run {runId.Value} was not found.");

        if (run.CabinetCount > 0)
        {
            throw new InvalidOperationException("RUN_NOT_EMPTY");
        }

        _stateStore.RemoveRun(runId);
        return Task.CompletedTask;
    }

    public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var placement = ParseRunPlacement(request.Placement, nameof(request.Placement));
        var category = InferCategoryFromTypeId(request.CabinetTypeId);
        var construction = InferConstructionFromTypeId(request.CabinetTypeId);

        var command = new AddCabinetToRunCommand(
            new RunId(request.RunId),
            request.CabinetTypeId,
            Length.FromInches(request.NominalWidthInches),
            placement,
            CommandOrigin.User,
            $"Add {request.NominalWidthInches}\" {request.CabinetTypeId} to run",
            _clock.Now,
            category: category,
            construction: construction);

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

        var targetPlacement = ParseRunPlacement(request.TargetPlacement, nameof(request.TargetPlacement));

        var command = new MoveCabinetCommand(
            new CabinetId(request.CabinetId),
            new RunId(request.SourceRunId),
            new RunId(request.TargetRunId),
            targetPlacement,
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

    public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ParameterKey);

        var command = new SetCabinetOverrideCommand(
            new CabinetId(request.CabinetId),
            request.ParameterKey,
            MapOverrideValue(request.Value),
            CommandOrigin.User,
            $"Set cabinet override {request.ParameterKey} on cabinet {request.CabinetId}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public CabinetStateRecord? GetCabinet(Guid cabinetId) =>
        _stateStore.GetCabinet(new CabinetId(cabinetId));

    public IReadOnlyList<CabinetStateRecord> GetAllCabinets() => _stateStore.GetAllCabinets();

    public IReadOnlyList<CabinetStateRecord> GetCabinets(IReadOnlyList<Guid> cabinetIds) =>
        cabinetIds
            .Select(id => _stateStore.GetCabinet(new CabinetId(id)))
            .Where(cabinet => cabinet is not null)
            .Select(cabinet => cabinet!)
            .OrderBy(cabinet => cabinet.CabinetTypeId, StringComparer.Ordinal)
            .ThenBy(cabinet => cabinet.CabinetId.Value)
            .ToArray();

    public Task<CommandResultDto> ResizeCabinetAsync(Guid cabinetId, decimal widthInches, decimal depthInches, decimal heightInches)
    {
        var command = new ResizeCabinetCommand(
            new CabinetId(cabinetId),
            Length.FromInches(widthInches),
            Length.FromInches(depthInches),
            Length.FromInches(heightInches),
            CommandOrigin.User,
            $"Resize cabinet {cabinetId}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> SetCabinetCategoryAsync(Guid cabinetId, CabinetCategory category)
    {
        var command = new SetCabinetCategoryCommand(
            new CabinetId(cabinetId),
            category,
            CommandOrigin.User,
            $"Set cabinet {cabinetId} category to {category}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> SetCabinetConstructionAsync(Guid cabinetId, ConstructionMethod construction)
    {
        var command = new SetCabinetConstructionCommand(
            new CabinetId(cabinetId),
            construction,
            CommandOrigin.User,
            $"Set cabinet {cabinetId} construction to {construction}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> AddOpeningAsync(Guid cabinetId, OpeningType openingType, decimal widthInches, decimal heightInches, int? insertIndex)
    {
        var command = new AddOpeningCommand(
            new CabinetId(cabinetId),
            openingType,
            Length.FromInches(widthInches),
            Length.FromInches(heightInches),
            insertIndex,
            CommandOrigin.User,
            $"Add opening to cabinet {cabinetId}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> RemoveOpeningAsync(Guid cabinetId, Guid openingId)
    {
        var command = new RemoveOpeningCommand(
            new CabinetId(cabinetId),
            new OpeningId(openingId),
            CommandOrigin.User,
            $"Remove opening {openingId} from cabinet {cabinetId}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> ReorderOpeningAsync(Guid cabinetId, Guid openingId, int newIndex)
    {
        var command = new ReorderOpeningCommand(
            new CabinetId(cabinetId),
            new OpeningId(openingId),
            newIndex,
            CommandOrigin.User,
            $"Reorder opening {openingId} on cabinet {cabinetId}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> SetCabinetOverrideAsync(Guid cabinetId, string overrideKey, OverrideValueDto value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(overrideKey);

        var command = new SetCabinetOverrideCommand(
            new CabinetId(cabinetId),
            overrideKey,
            MapOverrideValue(value),
            CommandOrigin.User,
            $"Set cabinet override {overrideKey} on cabinet {cabinetId}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<CommandResultDto> RemoveCabinetOverrideAsync(Guid cabinetId, string overrideKey)
    {
        var command = new RemoveCabinetOverrideCommand(
            new CabinetId(cabinetId),
            overrideKey,
            CommandOrigin.User,
            $"Remove cabinet override {overrideKey} on cabinet {cabinetId}",
            _clock.Now);

        return _handler.ExecuteAsync(command);
    }

    public Task<Cabinet> PlaceCabinetAsync(RunId runId, string cabinetTypeId, CancellationToken ct = default)
    {
        _ = ct;

        if (runId == default)
        {
            throw new InvalidOperationException("Run ID is required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(cabinetTypeId);

        var run = _stateStore.GetRun(runId)
            ?? throw new InvalidOperationException($"Run {runId.Value} was not found.");
        var template = _catalogService.GetAllItems().FirstOrDefault(item => item.TypeId == cabinetTypeId)
            ?? throw new InvalidOperationException($"Cabinet template '{cabinetTypeId}' was not found.");

        var cabinetId = CabinetId.New();
        RunSlot slot;
        try
        {
            slot = run.AppendCabinet(cabinetId, template.NominalWidth);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("Cannot place cabinet: run capacity exceeded.");
        }

        var revisionId = _projectState?.CurrentState?.Revision.Id ?? RevisionId.New();
        var cabinet = new Cabinet(
            cabinetId,
            revisionId,
            template.TypeId,
            InferCategoryFromTypeId(template.TypeId),
            template.ConstructionMethod,
            template.NominalWidth,
            template.Depth,
            template.Height,
            template.DefaultOpenings);

        var cabinetRecord = new CabinetStateRecord(
            cabinet.Id,
            cabinet.CabinetTypeId,
            cabinet.NominalWidth,
            cabinet.Depth,
            run.Id,
            slot.Id,
            cabinet.Category,
            cabinet.Construction,
            cabinet.Height,
            cabinet.Openings.Select(opening => new CabinetOpeningStateRecord(
                opening.Id.Value,
                opening.Index,
                opening.Type,
                opening.Width,
                opening.Height)).ToArray(),
            new Dictionary<string, OverrideValue>(cabinet.Overrides, StringComparer.Ordinal),
            cabinet.DefaultOpeningCount);

        _stateStore.AddCabinet(cabinetRecord);
        return Task.FromResult(cabinet);
    }

    public Task DeleteCabinetAsync(CabinetId cabinetId, CancellationToken ct = default)
    {
        _ = ct;

        if (cabinetId == default)
        {
            throw new InvalidOperationException("Cabinet ID is required.");
        }

        _stateStore.RemoveCabinet(cabinetId);
        return Task.CompletedTask;
    }

    public RunSummaryDto GetRunSummary(RunId runId)
    {
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
            run.IsOverCapacity,
            run.RemainingLength.Inches,
            run.OverCapacityAmount.Inches,
            slots);
    }

    private static readonly string[] _runPlacementNames = Enum.GetNames<RunPlacement>();

    private static RunPlacement ParseRunPlacement(string value, string paramName)
    {
        if (!_runPlacementNames.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid RunPlacement value. Expected one of: {string.Join(", ", _runPlacementNames)}.",
                paramName);
        }

        return Enum.Parse<RunPlacement>(value, ignoreCase: true);
    }

    private static CabinetCategory InferCategoryFromTypeId(string cabinetTypeId) =>
        cabinetTypeId.StartsWith("base-", StringComparison.OrdinalIgnoreCase) ? CabinetCategory.Base
        : cabinetTypeId.StartsWith("wall-", StringComparison.OrdinalIgnoreCase) ? CabinetCategory.Wall
        : cabinetTypeId.StartsWith("tall-", StringComparison.OrdinalIgnoreCase) ? CabinetCategory.Tall
        : cabinetTypeId.StartsWith("vanity-", StringComparison.OrdinalIgnoreCase) ? CabinetCategory.Vanity
        : cabinetTypeId.StartsWith("specialty-", StringComparison.OrdinalIgnoreCase) ? CabinetCategory.Specialty
        : CabinetCategory.Base;

    private static ConstructionMethod InferConstructionFromTypeId(string cabinetTypeId)
    {
        // Convention: if the typeId contains "faceframe", treat as FaceFrame; otherwise default to Frameless
        // This is extensible for future naming conventions
        return cabinetTypeId.Contains("faceframe", StringComparison.OrdinalIgnoreCase)
            ? ConstructionMethod.FaceFrame
            : ConstructionMethod.Frameless;
    }

    private static OverrideValue MapOverrideValue(OverrideValueDto value) =>
        value switch
        {
            OverrideValueDto.OfDecimalInches length => new OverrideValue.OfLength(Length.FromInches(length.Inches)),
            OverrideValueDto.OfString text => new OverrideValue.OfString(text.Value),
            OverrideValueDto.OfBool boolean => new OverrideValue.OfBool(boolean.Value),
            OverrideValueDto.OfInt integer => new OverrideValue.OfInt(integer.Value),
            OverrideValueDto.OfMaterialId material => new OverrideValue.OfMaterialId(new MaterialId(material.MaterialId)),
            OverrideValueDto.OfHardwareItemId hardware => new OverrideValue.OfHardwareItemId(new HardwareItemId(hardware.HardwareItemId)),
            _ => throw new ArgumentOutOfRangeException(nameof(value), $"Unsupported override value type '{value.GetType().Name}'.")
        };
}
