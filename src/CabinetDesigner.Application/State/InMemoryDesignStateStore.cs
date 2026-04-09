using CabinetDesigner.Application.Pipeline;
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

    private readonly Dictionary<RunId, CabinetRun> _runs = [];
    private readonly Dictionary<WallId, Wall> _walls = [];
    private readonly Dictionary<CabinetId, CabinetStateRecord> _cabinets = [];
    private readonly Dictionary<RunId, RunSpatialInfo> _runSpatialInfo = [];

    public CabinetRun? GetRun(RunId id) => _runs.TryGetValue(id, out var run) ? run : null;

    public Wall? GetWall(WallId id) => _walls.TryGetValue(id, out var wall) ? wall : null;

    public CabinetStateRecord? GetCabinet(CabinetId id) => _cabinets.TryGetValue(id, out var cabinet) ? cabinet : null;

    public RunSlot? FindCabinetSlot(RunId runId, CabinetId cabinetId) =>
        _runs.TryGetValue(runId, out var run)
            ? run.Slots.FirstOrDefault(slot => slot.CabinetId == cabinetId)
            : null;

    public IReadOnlyList<CabinetRun> GetAllRuns() => _runs.Values.OrderBy(run => run.Id.Value).ToArray();

    public IReadOnlyList<CabinetStateRecord> GetAllCabinets() => _cabinets.Values.OrderBy(cabinet => cabinet.CabinetId.Value).ToArray();

    public RunSpatialInfo? GetRunSpatialInfo(RunId runId) =>
        _runSpatialInfo.TryGetValue(runId, out var spatialInfo) ? spatialInfo : null;

    public void AddWall(Wall wall)
    {
        ArgumentNullException.ThrowIfNull(wall);
        _walls[wall.Id] = wall;
    }

    public void AddRun(CabinetRun run, Point2D startWorld, Point2D endWorld)
    {
        ArgumentNullException.ThrowIfNull(run);
        _runs[run.Id] = run;
        _runSpatialInfo[run.Id] = new RunSpatialInfo(startWorld, endWorld);
    }

    public void UpdateRunSpatialInfo(RunId runId, Point2D startWorld, Point2D endWorld)
    {
        _runSpatialInfo[runId] = new RunSpatialInfo(startWorld, endWorld);
    }

    public void AddCabinet(CabinetStateRecord cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        _cabinets[cabinet.CabinetId] = cabinet;
    }

    public void UpdateCabinet(CabinetStateRecord cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        _cabinets[cabinet.CabinetId] = cabinet;
    }

    public void RemoveEntity(string entityId, string entityType)
    {
        switch (entityType)
        {
            case "CabinetRun":
                _runs.Remove(new RunId(Guid.Parse(entityId)));
                _runSpatialInfo.Remove(new RunId(Guid.Parse(entityId)));
                break;
            case "Cabinet":
                _cabinets.Remove(new CabinetId(Guid.Parse(entityId)));
                break;
            case "Wall":
                _walls.Remove(new WallId(Guid.Parse(entityId)));
                break;
            default:
                throw new InvalidOperationException($"Unsupported entity type '{entityType}'.");
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
        var spatialInfo = _runSpatialInfo.TryGetValue(run.Id, out var value)
            ? value
            : throw new InvalidOperationException($"Run {run.Id} is missing spatial info.");
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
        _runs[runId] = run;
        _runSpatialInfo[runId] = new RunSpatialInfo(
            new Point2D(runSnapshot.StartXInches, runSnapshot.StartYInches),
            new Point2D(runSnapshot.EndXInches, runSnapshot.EndYInches));
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
            new RunSlotId(cabinetSnapshot.SlotId));
        _cabinets[cabinet.CabinetId] = cabinet;
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
        _walls[wall.Id] = wall;
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
        Guid SlotId)
    {
        public static CabinetSnapshot From(CabinetStateRecord cabinet) =>
            new(
                cabinet.CabinetId.Value,
                cabinet.CabinetTypeId,
                cabinet.NominalWidth.Inches,
                cabinet.NominalDepth.Inches,
                cabinet.RunId.Value,
                cabinet.SlotId.Value);
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
