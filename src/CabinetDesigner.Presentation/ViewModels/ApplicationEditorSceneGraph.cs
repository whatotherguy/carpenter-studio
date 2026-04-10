using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class ApplicationEditorSceneGraph : IEditorSceneGraph
{
    private readonly IDesignStateStore _stateStore;

    public ApplicationEditorSceneGraph(IDesignStateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public EditorSceneSnapshot Capture()
    {
        var runViews = new List<RunSceneView>();

        foreach (var run in _stateStore.GetAllRuns())
        {
            var wall = _stateStore.GetWall(run.WallId);
            var spatialInfo = _stateStore.GetRunSpatialInfo(run.Id);
            if (wall is null || spatialInfo is null)
            {
                continue;
            }

            var cabinetViews = new List<CabinetSceneView>();
            foreach (var slot in run.Slots.Where(slot => slot.CabinetId is not null).OrderBy(slot => slot.SlotIndex))
            {
                var cabinetRecord = _stateStore.GetCabinet(slot.CabinetId!.Value)
                    ?? throw new InvalidOperationException(
                        $"Run {run.Id} slot {slot.SlotIndex} references cabinet {slot.CabinetId} " +
                        $"which was not found in the design state store. The state is inconsistent.");

                var leftEdge = spatialInfo.StartWorld + wall.Direction * run.SlotOffset(slot.SlotIndex).Inches;
                var rightEdge = leftEdge + wall.Direction * slot.OccupiedWidth.Inches;
                cabinetViews.Add(new CabinetSceneView(
                    slot.CabinetId!.Value,
                    run.Id,
                    slot.SlotIndex,
                    slot.OccupiedWidth,
                    cabinetRecord.NominalDepth,
                    leftEdge,
                    rightEdge));
            }

            var axis = (spatialInfo.EndWorld - spatialInfo.StartWorld);
            var axisLengthSquared = axis.Dx * axis.Dx + axis.Dy * axis.Dy;
            var normalizedAxis = axisLengthSquared > 0
                ? new Vector2D(axis.Dx / (decimal)Math.Sqrt((double)axisLengthSquared), axis.Dy / (decimal)Math.Sqrt((double)axisLengthSquared))
                : wall.Direction;

            runViews.Add(new RunSceneView(
                run.Id,
                spatialInfo.StartWorld,
                spatialInfo.EndWorld,
                normalizedAxis,
                run.Capacity,
                cabinetViews));
        }

        return new EditorSceneSnapshot(runViews);
    }

    public RunId? HitTestRun(Point2D worldPoint, Length hitRadius)
    {
        RunId? bestRunId = null;
        var bestDistanceSquared = decimal.MaxValue;
        var hitRadiusSquared = hitRadius.Inches * hitRadius.Inches;

        foreach (var run in _stateStore.GetAllRuns())
        {
            var wall = _stateStore.GetWall(run.WallId);
            var spatialInfo = _stateStore.GetRunSpatialInfo(run.Id);
            if (wall is null || spatialInfo is null)
            {
                continue;
            }

            var start = spatialInfo.StartWorld;
            var end = spatialInfo.EndWorld;
            var runVector = end - start;
            var toPoint = worldPoint - start;

            var runLengthSquared = runVector.Dx * runVector.Dx + runVector.Dy * runVector.Dy;
            if (runLengthSquared <= 0m)
            {
                continue;
            }

            var t = (toPoint.Dx * runVector.Dx + toPoint.Dy * runVector.Dy) / runLengthSquared;
            t = Math.Clamp(t, 0m, 1m);

            var closestPoint = new Point2D(start.X + t * runVector.Dx, start.Y + t * runVector.Dy);
            var dx = worldPoint.X - closestPoint.X;
            var dy = worldPoint.Y - closestPoint.Y;
            var distanceSquared = dx * dx + dy * dy;

            if (distanceSquared <= hitRadiusSquared && distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestRunId = run.Id;
            }
        }

        return bestRunId;
    }
}
