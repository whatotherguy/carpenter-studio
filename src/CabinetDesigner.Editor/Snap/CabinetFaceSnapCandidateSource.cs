namespace CabinetDesigner.Editor.Snap;

public sealed class CabinetFaceSnapCandidateSource : ISnapCandidateSource
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

        var results = new List<SnapCandidate>();
        var sourceIndex = 0;
        foreach (var cabinet in run.Cabinets.OrderBy(cabinet => cabinet.SlotIndex).ThenBy(cabinet => cabinet.CabinetId.Value))
        {
            if (request.Drag.SubjectCabinetId == cabinet.CabinetId)
            {
                continue;
            }

            AddCandidate(cabinet.LeftFaceWorld, "left", "Cabinet Left Face");
            AddCandidate(cabinet.RightFaceWorld, "right", "Cabinet Right Face");
        }

        return results;

        void AddCandidate(Domain.Geometry.Point2D point, string suffix, string label)
        {
            var distance = request.Drag.CandidateRefPoint.DistanceTo(point);
            if (distance > request.Settings.SnapRadius)
            {
                sourceIndex++;
                return;
            }

            results.Add(new SnapCandidate(
                SnapKind.CabinetFace,
                run.RunId,
                $"{run.RunId.Value}:{suffix}:{sourceIndex}",
                sourceIndex,
                point,
                distance,
                label));
            sourceIndex++;
        }
    }
}
