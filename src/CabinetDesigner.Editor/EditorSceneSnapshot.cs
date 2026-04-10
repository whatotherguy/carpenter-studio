using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Editor;

public sealed record EditorSceneSnapshot(IReadOnlyList<RunSceneView> Runs)
{
    public RunSceneView? FindRun(RunId runId) => Runs.FirstOrDefault(run => run.RunId == runId);

    public CabinetSceneView? FindCabinet(CabinetId cabinetId) =>
        Runs.SelectMany(run => run.Cabinets).FirstOrDefault(cabinet => cabinet.CabinetId == cabinetId);
}

public sealed record RunSceneView(
    RunId RunId,
    Point2D StartWorld,
    Point2D EndWorld,
    Vector2D Axis,
    Length Capacity,
    IReadOnlyList<CabinetSceneView> Cabinets)
{
    public LineSegment2D Segment => new(StartWorld, EndWorld);
}

public sealed record CabinetSceneView(
    CabinetId CabinetId,
    RunId RunId,
    int SlotIndex,
    Length Width,
    Length Depth,
    Point2D LeftFaceWorld,
    Point2D RightFaceWorld);
