using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Editor.Snap;

public sealed class GridSnapCandidateSource : ISnapCandidateSource
{
    public IReadOnlyList<SnapCandidate> GetCandidates(SnapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var run = request.Drag.TargetRunId is null
            ? null
            : request.Scene.FindRun(request.Drag.TargetRunId.Value);
        if (run is null || request.Settings.GridSize <= Length.Zero)
        {
            return [];
        }

        var (distanceAlongAxis, _) = RunAxisProjection.ProjectOntoAxis(
            request.Drag.CandidateRefPoint,
            run.StartWorld,
            run.Axis);
        var snappedDistance = Math.Round(distanceAlongAxis / request.Settings.GridSize.Inches, MidpointRounding.AwayFromZero)
            * request.Settings.GridSize.Inches;
        var snappedPoint = RunAxisProjection.PointAtDistance(run.StartWorld, run.Axis, snappedDistance);
        var distance = request.Drag.CandidateRefPoint.DistanceTo(snappedPoint);
        if (distance > request.Settings.SnapRadius)
        {
            return [];
        }

        return
        [
            new SnapCandidate(
                SnapKind.Grid,
                run.RunId,
                $"{run.RunId.Value}:grid:{snappedDistance}",
                0,
                snappedPoint,
                distance,
                "Grid")
        ];
    }
}
