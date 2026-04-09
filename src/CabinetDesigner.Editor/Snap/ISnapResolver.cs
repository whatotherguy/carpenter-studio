namespace CabinetDesigner.Editor.Snap;

public interface ISnapResolver
{
    SnapResolution Resolve(SnapRequest request, SnapCandidate? previousWinner);
}
