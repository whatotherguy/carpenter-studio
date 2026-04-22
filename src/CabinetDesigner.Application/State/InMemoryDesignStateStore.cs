using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;

namespace CabinetDesigner.Application.State;

public sealed class InMemoryDesignStateStore : IDesignStateStore, IStateManager
{
    private const string SnapshotKey = "Snapshot";
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    // Protects all four dictionaries.  Acquired for every read and write so that
    // compound operations such as LoadWorkingRevision (ClearAll + multiple Adds) and
    // AddRun (two-dictionary write) are atomic from the perspective of any concurrent
    // reader running on a thread-pool continuation.
    private readonly object _sync = new();

    private readonly Dictionary<RoomId, Room> _rooms = [];
    private readonly Dictionary<RunId, CabinetRun> _runs = [];
    private readonly Dictionary<WallId, Wall> _walls = [];
    private readonly Dictionary<CabinetId, CabinetStateRecord> _cabinets = [];
    private readonly Dictionary<RunId, RunSpatialInfo> _runSpatialInfo = [];

    public Room? GetRoom(RoomId id)
    {
        lock (_sync)
        {
            return _rooms.TryGetValue(id, out var room) ? room : null;
        }
    }

    public CabinetRun? GetRun(RunId id)
    {
        lock (_sync)
        {
            return _runs.TryGetValue(id, out var run) ? run : null;
        }
    }

    public Wall? GetWall(WallId id)
    {
        lock (_sync)
        {
            return _walls.TryGetValue(id, out var wall) ? wall : null;
        }
    }

    public CabinetStateRecord? GetCabinet(CabinetId id)
    {
        lock (_sync)
        {
            return _cabinets.TryGetValue(id, out var cabinet) ? cabinet : null;
        }
    }

    public RunSlot? FindCabinetSlot(RunId runId, CabinetId cabinetId)
    {
        lock (_sync)
        {
            return _runs.TryGetValue(runId, out var run)
                ? run.Slots.FirstOrDefault(slot => slot.CabinetId == cabinetId)
                : null;
        }
    }

    public IReadOnlyList<CabinetRun> GetAllRuns()
    {
        lock (_sync)
        {
            return _runs.Values.OrderBy(run => run.Id.Value).ToArray();
        }
    }

    public IReadOnlyList<Room> GetAllRooms()
    {
        lock (_sync)
        {
            return _rooms.Values.OrderBy(room => room.Id.Value).ToArray();
        }
    }

    public IReadOnlyList<Wall> GetAllWalls()
    {
        lock (_sync)
        {
            return _walls.Values.OrderBy(wall => wall.Id.Value).ToArray();
        }
    }

    public IReadOnlyList<CabinetStateRecord> GetAllCabinets()
    {
        lock (_sync)
        {
            return _cabinets.Values.OrderBy(cabinet => cabinet.CabinetId.Value).ToArray();
        }
    }

    public RunSpatialInfo? GetRunSpatialInfo(RunId runId)
    {
        lock (_sync)
        {
            return _runSpatialInfo.TryGetValue(runId, out var spatialInfo) ? spatialInfo : null;
        }
    }

    public void AddRoom(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        lock (_sync)
        {
            _rooms[room.Id] = room;
        }
    }

    public void AddWall(Wall wall)
    {
        ArgumentNullException.ThrowIfNull(wall);
        lock (_sync)
        {
            _walls[wall.Id] = wall;
        }
    }

    public void AddRun(CabinetRun run, Point2D startWorld, Point2D endWorld)
    {
        ArgumentNullException.ThrowIfNull(run);
        lock (_sync)
        {
            _runs[run.Id] = run;
            _runSpatialInfo[run.Id] = new RunSpatialInfo(startWorld, endWorld);
        }
    }

    public void UpdateRunSpatialInfo(RunId runId, Point2D startWorld, Point2D endWorld)
    {
        lock (_sync)
        {
            _runSpatialInfo[runId] = new RunSpatialInfo(startWorld, endWorld);
        }
    }

    public void AddCabinet(CabinetStateRecord cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        lock (_sync)
        {
            _cabinets[cabinet.CabinetId] = cabinet;
        }
    }

    public void UpdateCabinet(CabinetStateRecord cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        lock (_sync)
        {
            _cabinets[cabinet.CabinetId] = cabinet;
        }
    }

    public void RemoveCabinet(CabinetId cabinetId)
    {
        lock (_sync)
        {
            if (!_cabinets.TryGetValue(cabinetId, out var cabinet))
            {
                throw new InvalidOperationException($"Cabinet {cabinetId.Value} was not found.");
            }

            if (_runs.TryGetValue(cabinet.RunId, out var run))
            {
                var slot = run.Slots.FirstOrDefault(candidate => candidate.CabinetId == cabinetId);
                if (slot is not null)
                {
                    run.RemoveSlot(slot.Id);
                }
            }

            _cabinets.Remove(cabinetId);
        }
    }

    public void RemoveRun(RunId runId)
    {
        lock (_sync)
        {
            _runs.Remove(runId);
            _runSpatialInfo.Remove(runId);
        }
    }

    public void RemoveWall(WallId wallId)
    {
        lock (_sync)
        {
            if (_walls.TryGetValue(wallId, out var wall) && _rooms.TryGetValue(wall.RoomId, out var room))
            {
                room.RemoveWall(wallId);
            }

            _walls.Remove(wallId);
        }
    }

    public void LoadWorkingRevision(WorkingRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        // Resolve all data outside the lock to minimise the critical section.
        var rooms = revision.Rooms.OrderBy(room => room.Id.Value).ToArray();
        var walls = revision.Walls.OrderBy(wall => wall.Id.Value).ToArray();
        var runs = revision.Runs.OrderBy(run => run.Id.Value).ToArray();

        var wallLookup = walls.ToDictionary(w => w.Id);

        var runSpatialInfos = new List<(RunId, RunSpatialInfo)>();
        foreach (var run in runs)
        {
            if (!wallLookup.TryGetValue(run.WallId, out var wall))
            {
                throw new InvalidOperationException($"Wall {run.WallId} was not found while loading working revision.");
            }

            runSpatialInfos.Add((run.Id, new RunSpatialInfo(wall.StartPoint, wall.StartPoint + wall.Direction * run.Capacity.Inches)));
        }

        var allSlots = revision.Runs.SelectMany(run => run.Slots).ToArray();

        var cabinetRecords = new List<CabinetStateRecord>();
        foreach (var cabinet in revision.Cabinets.OrderBy(cabinet => cabinet.Id.Value))
        {
            var slot = allSlots.FirstOrDefault(candidate => candidate.CabinetId == cabinet.Id)
                ?? throw new InvalidOperationException($"Cabinet {cabinet.Id} was not assigned to a run slot.");
            cabinetRecords.Add(new CabinetStateRecord(
                cabinet.Id,
                cabinet.CabinetTypeId,
                cabinet.NominalWidth,
                cabinet.Depth,
                slot.RunId,
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
            cabinet.DefaultOpeningCount));
        }

        lock (_sync)
        {
            _rooms.Clear();
            _runs.Clear();
            _walls.Clear();
            _cabinets.Clear();
            _runSpatialInfo.Clear();

            foreach (var room in rooms)
            {
                _rooms[room.Id] = room;
            }

            foreach (var wall in walls)
            {
                _walls[wall.Id] = wall;
            }

            foreach (var run in runs)
            {
                _runs[run.Id] = run;
            }

            foreach (var (runId, spatialInfo) in runSpatialInfos)
            {
                _runSpatialInfo[runId] = spatialInfo;
            }

            foreach (var cabinet in cabinetRecords)
            {
                _cabinets[cabinet.CabinetId] = cabinet;
            }
        }
    }

    public void ClearAll()
    {
        lock (_sync)
        {
            _rooms.Clear();
            _runs.Clear();
            _walls.Clear();
            _cabinets.Clear();
            _runSpatialInfo.Clear();
        }
    }

    public void RemoveEntity(string entityId, string entityType)
    {
        // Parse outside the lock so that invalid input throws before acquiring _sync
        // and so Guid.Parse is not called twice for the CabinetRun case.
        var parsed = Guid.Parse(entityId);

        lock (_sync)
        {
            switch (entityType)
            {
                case "CabinetRun":
                    var runId = new RunId(parsed);
                    _runs.Remove(runId);
                    _runSpatialInfo.Remove(runId);
                    break;
                case "Cabinet":
                    RemoveCabinet(new CabinetId(parsed));
                    break;
                case "Wall":
                    _walls.Remove(new WallId(parsed));
                    break;
                case "Room":
                    _rooms.Remove(new RoomId(parsed));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported entity type '{entityType}'.");
            }
        }
    }

    public void RestoreValues(string entityId, string entityType, IReadOnlyDictionary<string, DeltaValue> values)
    {
        RestoreEntity(entityId, entityType, values);
    }

    public void RestoreEntity(string entityId, string entityType, IReadOnlyDictionary<string, DeltaValue> values)
    {
        if (!values.TryGetValue(SnapshotKey, out var snapshotValue) || snapshotValue is not DeltaValue.OfString snapshotText)
        {
            throw new InvalidOperationException($"Entity '{entityType}' requires a '{SnapshotKey}' delta value.");
        }

        switch (entityType)
        {
            case "Room":
                RestoreRoom(snapshotText.Value);
                break;
            case "CabinetRun":
                RestoreRun(snapshotText.Value);
                break;
            case "Cabinet":
                RestoreCabinet(snapshotText.Value);
                break;
            case "Wall":
                RestoreWall(snapshotText.Value);
                break;
            default:
                throw new InvalidOperationException($"Unsupported entity type '{entityType}'.");
        }
    }

    public IReadOnlyDictionary<string, DeltaValue> CaptureRoomValues(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return CreateSnapshotValues(JsonSerializer.Serialize(RoomSnapshot.From(room), SnapshotJsonOptions));
    }

    public IReadOnlyDictionary<string, DeltaValue> CaptureRunValues(CabinetRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        RunSpatialInfo spatialInfo;
        lock (_sync)
        {
            spatialInfo = _runSpatialInfo.TryGetValue(run.Id, out var value)
                ? value
                : throw new InvalidOperationException($"Run {run.Id} is missing spatial info.");
        }

        return CreateSnapshotValues(JsonSerializer.Serialize(RunSnapshot.From(run, spatialInfo), SnapshotJsonOptions));
    }

    public IReadOnlyDictionary<string, DeltaValue> CaptureCabinetValues(CabinetStateRecord cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        return CreateSnapshotValues(JsonSerializer.Serialize(CabinetSnapshot.From(cabinet), SnapshotJsonOptions));
    }

    public IReadOnlyDictionary<string, DeltaValue> CaptureWallValues(Wall wall)
    {
        ArgumentNullException.ThrowIfNull(wall);
        return CreateSnapshotValues(JsonSerializer.Serialize(WallSnapshot.From(wall), SnapshotJsonOptions));
    }

    private static IReadOnlyDictionary<string, DeltaValue> CreateSnapshotValues(string snapshot) =>
        new Dictionary<string, DeltaValue>(StringComparer.Ordinal)
        {
            [SnapshotKey] = new DeltaValue.OfString(snapshot)
        };

    private void RestoreRun(string snapshot)
    {
        var runSnapshot = JsonSerializer.Deserialize<RunSnapshot>(snapshot, SnapshotJsonOptions)
            ?? throw new InvalidOperationException("Run snapshot could not be deserialized.");
        var runId = new RunId(runSnapshot.RunId);
        var wallId = new WallId(runSnapshot.WallId);
        var run = CabinetRun.Reconstitute(
            runId,
            wallId,
            Length.FromInches(runSnapshot.CapacityInches),
            runSnapshot.Slots
                .OrderBy(item => item.SlotIndex)
                .Select(snapshotSlot => snapshotSlot.CabinetId is Guid cabinetId
                    ? RunSlot.ForCabinet(
                        new RunSlotId(snapshotSlot.RunSlotId),
                        runId,
                        new CabinetId(cabinetId),
                        Length.FromInches(snapshotSlot.OccupiedWidthInches),
                        snapshotSlot.SlotIndex)
                    : RunSlot.ForFiller(
                        new RunSlotId(snapshotSlot.RunSlotId),
                        runId,
                        Length.FromInches(snapshotSlot.OccupiedWidthInches),
                        snapshotSlot.SlotIndex))
                .ToArray());

        var spatialInfo = new RunSpatialInfo(
            new Point2D(runSnapshot.StartXInches, runSnapshot.StartYInches),
            new Point2D(runSnapshot.EndXInches, runSnapshot.EndYInches));

        lock (_sync)
        {
            _runs[runId] = run;
            _runSpatialInfo[runId] = spatialInfo;
        }
    }

    private void RestoreCabinet(string snapshot)
    {
        var cabinetSnapshot = JsonSerializer.Deserialize<CabinetSnapshot>(snapshot, SnapshotJsonOptions)
            ?? throw new InvalidOperationException("Cabinet snapshot could not be deserialized.");
        var cabinet = new CabinetStateRecord(
            new CabinetId(cabinetSnapshot.CabinetId),
            cabinetSnapshot.CabinetTypeId,
            Length.FromInches(cabinetSnapshot.NominalWidthInches),
            Length.FromInches(cabinetSnapshot.NominalDepthInches),
            new RunId(cabinetSnapshot.RunId),
            new RunSlotId(cabinetSnapshot.SlotId),
            cabinetSnapshot.Category,
            cabinetSnapshot.Construction,
            Length.FromInches(cabinetSnapshot.NominalHeightInches),
            cabinetSnapshot.DeserializeOpenings(),
            cabinetSnapshot.DeserializeOverrides(),
            cabinetSnapshot.DefaultOpeningCount);

        lock (_sync)
        {
            _cabinets[cabinet.CabinetId] = cabinet;
        }
    }

    private void RestoreWall(string snapshot)
    {
        var wallSnapshot = JsonSerializer.Deserialize<WallSnapshot>(snapshot, SnapshotJsonOptions)
            ?? throw new InvalidOperationException("Wall snapshot could not be deserialized.");
        var wall = new Wall(
            new WallId(wallSnapshot.WallId),
            new RoomId(wallSnapshot.RoomId),
            new Point2D(wallSnapshot.StartXInches, wallSnapshot.StartYInches),
            new Point2D(wallSnapshot.EndXInches, wallSnapshot.EndYInches),
            Thickness.Exact(Length.FromInches(wallSnapshot.ThicknessInches)));

        lock (_sync)
        {
            _walls[wall.Id] = wall;
        }
    }

    private void RestoreRoom(string snapshot)
    {
        var roomSnapshot = JsonSerializer.Deserialize<RoomSnapshot>(snapshot, SnapshotJsonOptions)
            ?? throw new InvalidOperationException("Room snapshot could not be deserialized.");
        var walls = roomSnapshot.Walls
            .OrderBy(wall => wall.WallId)
            .Select(wall => new Wall(
                new WallId(wall.WallId),
                new RoomId(roomSnapshot.RoomId),
                new Point2D(wall.StartXInches, wall.StartYInches),
                new Point2D(wall.EndXInches, wall.EndYInches),
                Thickness.Exact(Length.FromInches(wall.ThicknessInches))))
            .ToArray();
        var obstacles = roomSnapshot.Obstacles
            .OrderBy(obstacle => obstacle.ObstacleId)
            .Select(obstacle => new Obstacle(
                new ObstacleId(obstacle.ObstacleId),
                new RoomId(roomSnapshot.RoomId),
                new Rect2D(
                    new Point2D(obstacle.MinXInches, obstacle.MinYInches),
                    Length.FromInches(obstacle.WidthInches),
                    Length.FromInches(obstacle.HeightInches)),
                obstacle.Description))
            .ToArray();
        var room = Room.Reconstitute(
            new RoomId(roomSnapshot.RoomId),
            new RevisionId(roomSnapshot.RevisionId),
            roomSnapshot.Name,
            Length.FromInches(roomSnapshot.CeilingHeightInches),
            walls,
            obstacles);

        lock (_sync)
        {
            _rooms[room.Id] = room;
            foreach (var wall in walls)
            {
                _walls[wall.Id] = wall;
            }
        }
    }

    private sealed record RunSnapshot(
        Guid RunId,
        Guid WallId,
        decimal CapacityInches,
        decimal StartXInches,
        decimal StartYInches,
        decimal EndXInches,
        decimal EndYInches,
        IReadOnlyList<RunSlotSnapshot> Slots)
    {
        public static RunSnapshot From(CabinetRun run, RunSpatialInfo spatialInfo) =>
            new(
                run.Id.Value,
                run.WallId.Value,
                run.Capacity.Inches,
                spatialInfo.StartWorld.X,
                spatialInfo.StartWorld.Y,
                spatialInfo.EndWorld.X,
                spatialInfo.EndWorld.Y,
                run.Slots.Select(RunSlotSnapshot.From).ToArray());
    }

    private sealed record RunSlotSnapshot(
        Guid RunSlotId,
        RunSlotType SlotType,
        int SlotIndex,
        decimal OccupiedWidthInches,
        Guid? CabinetId)
    {
        public static RunSlotSnapshot From(RunSlot slot) =>
            new(
                slot.Id.Value,
                slot.SlotType,
                slot.SlotIndex,
                slot.OccupiedWidth.Inches,
                slot.CabinetId?.Value);
    }

    private sealed record CabinetSnapshot(
        Guid CabinetId,
        string CabinetTypeId,
        decimal NominalWidthInches,
        decimal NominalDepthInches,
        decimal NominalHeightInches,
        Guid RunId,
        Guid SlotId,
        CabinetCategory Category,
        ConstructionMethod Construction,
        string OpeningsJson,
        string OverridesJson,
        int DefaultOpeningCount)
    {
        public static CabinetSnapshot From(CabinetStateRecord cabinet) =>
            new(
                cabinet.CabinetId.Value,
                cabinet.CabinetTypeId,
                cabinet.NominalWidth.Inches,
                cabinet.NominalDepth.Inches,
                cabinet.EffectiveNominalHeight.Inches,
                cabinet.RunId.Value,
                cabinet.SlotId.Value,
                cabinet.Category,
                cabinet.Construction,
                JsonSerializer.Serialize(cabinet.EffectiveOpenings.OrderBy(opening => opening.Index), SnapshotJsonOptions),
                JsonSerializer.Serialize(cabinet.EffectiveOverrides, SnapshotJsonOptions),
                cabinet.EffectiveDefaultOpeningCount);

        public IReadOnlyList<CabinetOpeningStateRecord> DeserializeOpenings() =>
            JsonSerializer.Deserialize<List<CabinetOpeningStateRecord>>(OpeningsJson, SnapshotJsonOptions)
            ?? [];

        public IReadOnlyDictionary<string, OverrideValue> DeserializeOverrides() =>
            JsonSerializer.Deserialize<Dictionary<string, OverrideValue>>(OverridesJson, SnapshotJsonOptions)
            ?? new Dictionary<string, OverrideValue>(StringComparer.Ordinal);
    }

    private sealed record WallSnapshot(
        Guid WallId,
        Guid RoomId,
        decimal StartXInches,
        decimal StartYInches,
        decimal EndXInches,
        decimal EndYInches,
        decimal ThicknessInches)
    {
        public static WallSnapshot From(Wall wall) =>
            new(
                wall.Id.Value,
                wall.RoomId.Value,
                wall.StartPoint.X,
                wall.StartPoint.Y,
                wall.EndPoint.X,
                wall.EndPoint.Y,
                wall.WallThickness.Actual.Inches);
    }

    private sealed record RoomSnapshot(
        Guid RoomId,
        Guid RevisionId,
        string Name,
        decimal CeilingHeightInches,
        IReadOnlyList<RoomWallSnapshot> Walls,
        IReadOnlyList<RoomObstacleSnapshot> Obstacles)
    {
        public static RoomSnapshot From(Room room) =>
            new(
                room.Id.Value,
                room.RevisionId.Value,
                room.Name,
                room.CeilingHeight.Inches,
                room.Walls
                    .OrderBy(wall => wall.Id.Value)
                    .Select(wall => new RoomWallSnapshot(
                        wall.Id.Value,
                        wall.StartPoint.X,
                        wall.StartPoint.Y,
                        wall.EndPoint.X,
                        wall.EndPoint.Y,
                        wall.WallThickness.Actual.Inches))
                    .ToArray(),
                room.Obstacles
                    .OrderBy(obstacle => obstacle.Id.Value)
                    .Select(obstacle => new RoomObstacleSnapshot(
                        obstacle.Id.Value,
                        obstacle.Bounds.Min.X,
                        obstacle.Bounds.Min.Y,
                        obstacle.Bounds.Width.Inches,
                        obstacle.Bounds.Height.Inches,
                        obstacle.Description))
                    .ToArray());
    }

    private sealed record RoomWallSnapshot(
        Guid WallId,
        decimal StartXInches,
        decimal StartYInches,
        decimal EndXInches,
        decimal EndYInches,
        decimal ThicknessInches);

    private sealed record RoomObstacleSnapshot(
        Guid ObstacleId,
        decimal MinXInches,
        decimal MinYInches,
        decimal WidthInches,
        decimal HeightInches,
        string Description);
}
