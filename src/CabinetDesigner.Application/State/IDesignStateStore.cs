using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;

namespace CabinetDesigner.Application.State;

public interface IDesignStateStore
{
    CabinetRun? GetRun(RunId id);

    Wall? GetWall(WallId id);

    CabinetStateRecord? GetCabinet(CabinetId id);

    RunSlot? FindCabinetSlot(RunId runId, CabinetId cabinetId);

    IReadOnlyList<CabinetRun> GetAllRuns();

    IReadOnlyList<CabinetStateRecord> GetAllCabinets();

    RunSpatialInfo? GetRunSpatialInfo(RunId runId);

    void AddWall(Wall wall);

    void AddRun(CabinetRun run, Point2D startWorld, Point2D endWorld);

    void UpdateRunSpatialInfo(RunId runId, Point2D startWorld, Point2D endWorld);

    void AddCabinet(CabinetStateRecord cabinet);

    void UpdateCabinet(CabinetStateRecord cabinet);

    IReadOnlyDictionary<string, DeltaValue> CaptureRunValues(CabinetRun run);

    IReadOnlyDictionary<string, DeltaValue> CaptureCabinetValues(CabinetStateRecord cabinet);

    IReadOnlyDictionary<string, DeltaValue> CaptureWallValues(Wall wall);
}

public sealed record RunSpatialInfo(Point2D StartWorld, Point2D EndWorld);

public sealed record CabinetStateRecord(
    CabinetId CabinetId,
    string CabinetTypeId,
    Length NominalWidth,
    Length NominalDepth,
    RunId RunId,
    RunSlotId SlotId,
    CabinetCategory Category,
    ConstructionMethod Construction) : IDomainEntity
{
    public string EntityId => CabinetId.Value.ToString();

    public string EntityType => "Cabinet";
}

public sealed record ResolvedRunEntity(CabinetRun Run) : IDomainEntity
{
    public string EntityId => Run.Id.Value.ToString();

    public string EntityType => "CabinetRun";
}

public sealed record ResolvedWallEntity(Wall Wall) : IDomainEntity
{
    public string EntityId => Wall.Id.Value.ToString();

    public string EntityType => "Wall";
}

public sealed record ResolvedCabinetEntity(CabinetStateRecord Cabinet) : IDomainEntity
{
    public string EntityId => Cabinet.CabinetId.Value.ToString();

    public string EntityType => "Cabinet";
}
