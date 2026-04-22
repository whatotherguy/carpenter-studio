using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.SpatialContext;

namespace CabinetDesigner.Application.Services;

public sealed class RoomService : IRoomService
{
    private readonly IWorkingRevisionRepository _workingRevisionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPersistedProjectState _currentPersistedProjectState;
    private readonly IWorkingRevisionSource _workingRevisionSource;
    private readonly IDesignStateStore _stateStore;
    private readonly IDeltaTracker _deltaTracker;
    private readonly IUndoStack _undoStack;
    private readonly IApplicationEventBus _eventBus;
    private readonly IClock _clock;

    public RoomService(
        IWorkingRevisionRepository workingRevisionRepository,
        IUnitOfWork unitOfWork,
        ICurrentPersistedProjectState currentPersistedProjectState,
        IWorkingRevisionSource workingRevisionSource,
        IDesignStateStore stateStore,
        IDeltaTracker deltaTracker,
        IUndoStack undoStack,
        IApplicationEventBus eventBus,
        IClock clock)
    {
        _workingRevisionRepository = workingRevisionRepository ?? throw new ArgumentNullException(nameof(workingRevisionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentPersistedProjectState = currentPersistedProjectState ?? throw new ArgumentNullException(nameof(currentPersistedProjectState));
        _workingRevisionSource = workingRevisionSource ?? throw new ArgumentNullException(nameof(workingRevisionSource));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _deltaTracker = deltaTracker ?? throw new ArgumentNullException(nameof(deltaTracker));
        _undoStack = undoStack ?? throw new ArgumentNullException(nameof(undoStack));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct)
    {
        var state = EnsureCurrentState();
        var roomName = ValidateRoomName(name);
        ValidatePositiveCeilingHeight(ceilingHeight);

        var room = new Room(RoomId.New(), state.Revision.Id, roomName, ceilingHeight);
        var metadata = CommandMetadata.Create(
            _clock.Now,
            CommandOrigin.User,
            $"Create room '{room.Name}'",
            [room.Id.Value.ToString()]);

        _deltaTracker.Begin();
        await _unitOfWork.BeginAsync(ct).ConfigureAwait(false);
        try
        {
            _stateStore.AddRoom(room);
            _deltaTracker.RecordDelta(new StateDelta(
                room.Id.Value.ToString(),
                "Room",
                DeltaOperation.Created,
                null,
                _stateStore.CaptureRoomValues(room)));

            var capturedState = await PersistCurrentStateAsync(ct).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(ct).ConfigureAwait(false);

            var deltas = _deltaTracker.Finalize();
            _currentPersistedProjectState.SetCurrentState(capturedState);
            _undoStack.Push(new UndoEntry(metadata, deltas, []));
            PublishChange(metadata, "room.create", room.Id.Value.ToString());
            return room;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct).ConfigureAwait(false);
            if (_stateStore.GetRoom(room.Id) is not null)
            {
                _stateStore.RemoveEntity(room.Id.Value.ToString(), "Room");
            }

            _deltaTracker.Finalize();
            throw;
        }
    }

    public async Task<Wall> AddWallAsync(RoomId roomId, Point2D start, Point2D end, Thickness thickness, CancellationToken ct)
    {
        var room = RequireRoom(roomId);
        var previousRoomValues = _stateStore.CaptureRoomValues(room);
        ValidateWallLength(start, end);
        Wall? wall = null;

        var metadata = CommandMetadata.Create(
            _clock.Now,
            CommandOrigin.User,
            $"Add wall to room '{room.Name}'",
            [room.Id.Value.ToString()]);

        _deltaTracker.Begin();
        await _unitOfWork.BeginAsync(ct).ConfigureAwait(false);
        try
        {
            wall = room.AddWall(start, end, thickness);
            _stateStore.AddWall(wall);
            _deltaTracker.RecordDelta(new StateDelta(
                room.Id.Value.ToString(),
                "Room",
                DeltaOperation.Modified,
                previousRoomValues,
                _stateStore.CaptureRoomValues(room)));
            _deltaTracker.RecordDelta(new StateDelta(
                wall.Id.Value.ToString(),
                "Wall",
                DeltaOperation.Created,
                null,
                _stateStore.CaptureWallValues(wall)));

            var capturedState = await PersistCurrentStateAsync(ct).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(ct).ConfigureAwait(false);

            var deltas = _deltaTracker.Finalize();
            _currentPersistedProjectState.SetCurrentState(capturedState);
            _undoStack.Push(new UndoEntry(metadata, deltas, []));
            PublishChange(metadata, "wall.add", wall.Id.Value.ToString(), room.Id.Value.ToString());
            return wall;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct).ConfigureAwait(false);
            if (wall is not null)
            {
                _stateStore.RemoveEntity(wall.Id.Value.ToString(), "Wall");
            }

            _stateStore.RestoreEntity(roomId.Value.ToString(), "Room", previousRoomValues);
            _deltaTracker.Finalize();
            throw;
        }
    }

    public async Task RemoveWallAsync(WallId wallId, CancellationToken ct)
    {
        var wall = RequireWall(wallId);
        var room = RequireRoom(wall.RoomId);
        var previousRoomValues = _stateStore.CaptureRoomValues(room);
        var previousWallValues = _stateStore.CaptureWallValues(wall);

        var metadata = CommandMetadata.Create(
            _clock.Now,
            CommandOrigin.User,
            $"Remove wall from room '{room.Name}'",
            [wall.Id.Value.ToString(), room.Id.Value.ToString()]);

        _deltaTracker.Begin();
        await _unitOfWork.BeginAsync(ct).ConfigureAwait(false);
        try
        {
            _stateStore.RemoveWall(wallId);
            _deltaTracker.RecordDelta(new StateDelta(
                room.Id.Value.ToString(),
                "Room",
                DeltaOperation.Modified,
                previousRoomValues,
                _stateStore.CaptureRoomValues(room)));
            _deltaTracker.RecordDelta(new StateDelta(
                wall.Id.Value.ToString(),
                "Wall",
                DeltaOperation.Removed,
                previousWallValues,
                null));

            var capturedState = await PersistCurrentStateAsync(ct).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(ct).ConfigureAwait(false);

            var deltas = _deltaTracker.Finalize();
            _currentPersistedProjectState.SetCurrentState(capturedState);
            _undoStack.Push(new UndoEntry(metadata, deltas, []));
            PublishChange(metadata, "wall.remove", wall.Id.Value.ToString(), room.Id.Value.ToString());
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct).ConfigureAwait(false);
            _stateStore.RestoreEntity(room.Id.Value.ToString(), "Room", previousRoomValues);
            _stateStore.RestoreEntity(wall.Id.Value.ToString(), "Wall", previousWallValues);
            _deltaTracker.Finalize();
            throw;
        }
    }

    public async Task RenameRoomAsync(RoomId roomId, string newName, CancellationToken ct)
    {
        var room = RequireRoom(roomId);
        var previousRoomValues = _stateStore.CaptureRoomValues(room);
        var renamed = ValidateRoomName(newName);

        var metadata = CommandMetadata.Create(
            _clock.Now,
            CommandOrigin.User,
            $"Rename room '{room.Name}'",
            [room.Id.Value.ToString()]);

        _deltaTracker.Begin();
        await _unitOfWork.BeginAsync(ct).ConfigureAwait(false);
        try
        {
            room.Rename(renamed);
            _deltaTracker.RecordDelta(new StateDelta(
                room.Id.Value.ToString(),
                "Room",
                DeltaOperation.Modified,
                previousRoomValues,
                _stateStore.CaptureRoomValues(room)));

            var capturedState = await PersistCurrentStateAsync(ct).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(ct).ConfigureAwait(false);

            var deltas = _deltaTracker.Finalize();
            _currentPersistedProjectState.SetCurrentState(capturedState);
            _undoStack.Push(new UndoEntry(metadata, deltas, []));
            PublishChange(metadata, "room.rename", room.Id.Value.ToString());
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct).ConfigureAwait(false);
            _stateStore.RestoreEntity(room.Id.Value.ToString(), "Room", previousRoomValues);
            _deltaTracker.Finalize();
            throw;
        }
    }

    public Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken ct)
    {
        EnsureCurrentState();
        return Task.FromResult<IReadOnlyList<Room>>(_stateStore.GetAllRooms());
    }

    private PersistedProjectState EnsureCurrentState() =>
        _currentPersistedProjectState.CurrentState
        ?? throw new InvalidOperationException("No current project is open.");

    private Room RequireRoom(RoomId roomId) =>
        _stateStore.GetRoom(roomId)
        ?? throw new InvalidOperationException($"Room {roomId.Value} was not found in the current project.");

    private Wall RequireWall(WallId wallId) =>
        _stateStore.GetWall(wallId)
        ?? throw new InvalidOperationException($"Wall {wallId.Value} was not found in the current project.");

    private async Task<PersistedProjectState> PersistCurrentStateAsync(CancellationToken ct)
    {
        var currentState = _workingRevisionSource.CaptureCurrentState();
        await _workingRevisionRepository.SaveAsync(currentState.WorkingRevision, ct).ConfigureAwait(false);
        return currentState;
    }

    private void PublishChange(CommandMetadata metadata, string commandType, params string[] affectedEntityIds)
    {
        _eventBus.Publish(new DesignChangedEvent(new CommandResultDto(
            metadata.CommandId.Value,
            commandType,
            true,
            [],
            affectedEntityIds,
            [])));
    }

    private static string ValidateRoomName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Room name is required.", nameof(name));
        }

        return name.Trim();
    }

    private static void ValidatePositiveCeilingHeight(Length ceilingHeight)
    {
        if (ceilingHeight <= Length.Zero)
        {
            throw new ArgumentException("Ceiling height must be positive.", nameof(ceilingHeight));
        }
    }

    private static void ValidateWallLength(Point2D start, Point2D end)
    {
        if (start.DistanceTo(end) <= Length.Zero)
        {
            throw new ArgumentException("Wall length must be greater than zero.");
        }
    }
}
