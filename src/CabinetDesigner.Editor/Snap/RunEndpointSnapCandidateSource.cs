using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Editor.Snap;

public sealed class RunEndpointSnapCandidateSource : ISnapCandidateSource
{
    public IReadOnlyList<SnapCandidate> GetCandidates(SnapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var run = request.Drag.TargetRunId is null
            ? null
            : request.Scene.FindRun(request.Drag.TargetRunId.Value);
        if (run is null)
        {
            return [];
        }

        var points = new[]
        {
            (Point: run.StartWorld, Label: "Run Start", Suffix: "start"),
            (Point: run.EndWorld, Label: "Run End", Suffix: "end")
        };

        var results = new List<SnapCandidate>(points.Length);
        for (var index = 0; index < points.Length; index++)
        {
            var distance = request.Drag.CandidateRefPoint.DistanceTo(points[index].Point);
            if (distance > request.Settings.SnapRadius)
            {
                continue;
            }

            results.Add(new SnapCandidate(
                SnapKind.RunEndpoint,
                run.RunId,
                $"{run.RunId.Value}:{points[index].Suffix}",
                index,
                points[index].Point,
                distance,
                points[index].Label));
        }

        return results;
    }
}
