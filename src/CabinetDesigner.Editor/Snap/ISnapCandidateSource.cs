namespace CabinetDesigner.Editor.Snap;

public interface ISnapCandidateSource
{
    IReadOnlyList<SnapCandidate> GetCandidates(SnapRequest request);
}
