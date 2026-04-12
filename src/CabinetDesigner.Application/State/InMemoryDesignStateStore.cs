using CabinetDesigner.Application.Pipeline;
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

    private readonly Dictionary<RunId, CabinetRun> _runs = [];
    private readonly Dictionary<WallId, Wall> _walls = [];
    private readonly Dictionary<CabinetId, CabinetStateRecord> _cabinets = [];
    private readonly Dictionary<RunId, RunSpatialInfo> _runSpatialInfo = [];

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

    public void LoadWorkingRevision(WorkingRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        // Resolve all data outside the lock to minimise the critical section.
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
                cabinet.Construction));
        }

        lock (_sync)
        {
            _runs.Clear();
            _walls.Clear();
            _cabinets.Clear();
            _runSpatialInfo.Clear();

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
                    _cabinets.Remove(new CabinetId(parsed));
                    break;
                case "Wall":
                    _walls.Remove(new WallId(parsed));
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
            cabinetSnapshot.Construction);

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
        Guid RunId,
        Guid SlotId,
        CabinetCategory Category,
        ConstructionMethod Construction)
    {
        public static CabinetSnapshot From(CabinetStateRecord cabinet) =>
            new(
                cabinet.CabinetId.Value,
                cabinet.CabinetTypeId,
                cabinet.NominalWidth.Inches,
                cabinet.NominalDepth.Inches,
                cabinet.RunId.Value,
                cabinet.SlotId.Value,
                cabinet.Category,
                cabinet.Construction);
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
}
