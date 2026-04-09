namespace CabinetDesigner.Editor.Snap;

public sealed record SnapResolution(
    SnapCandidate? Winner,
    IReadOnlyList<SnapCandidate> Candidates);
