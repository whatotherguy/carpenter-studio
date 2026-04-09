using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Editor.Snap;

public sealed class DefaultSnapResolver : ISnapResolver
{
    private readonly IReadOnlyList<ISnapCandidateSource> _sources;

    public DefaultSnapResolver(IEnumerable<ISnapCandidateSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = sources.ToArray();
    }

    public SnapResolution Resolve(SnapRequest request, SnapCandidate? previousWinner)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidates = _sources
            .SelectMany(source => source.GetCandidates(request))
            .OrderByDescending(candidate => GetPriority(candidate.Kind))
            .ThenBy(candidate => GetEffectiveDistance(candidate, request.Settings, previousWinner).Inches)
            .ThenBy(candidate => candidate.Distance.Inches)
            .ThenBy(candidate => candidate.SourceId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SourceIndex)
            .ToArray();

        return new SnapResolution(candidates.FirstOrDefault(), candidates);
    }

    private static Length GetEffectiveDistance(SnapCandidate candidate, SnapSettings settings, SnapCandidate? previousWinner)
    {
        if (previousWinner is null || !IsSameCandidate(candidate, previousWinner))
        {
            return candidate.Distance;
        }

        var reduced = previousWinner.Distance.Inches - settings.HysteresisDistance.Inches;
        return Length.FromInches(Math.Max(0m, reduced));
    }

    private static bool IsSameCandidate(SnapCandidate candidate, SnapCandidate other) =>
        candidate.Kind == other.Kind
        && candidate.RunId == other.RunId
        && string.Equals(candidate.SourceId, other.SourceId, StringComparison.Ordinal);

    private static int GetPriority(SnapKind kind) =>
        kind switch
        {
            SnapKind.RunEndpoint => 3,
            SnapKind.CabinetFace => 2,
            SnapKind.Grid => 1,
            _ => 0
        };
}
