using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;
using Xunit;

namespace CabinetDesigner.Tests.Editor.Snap;

public sealed class DefaultSnapResolverTests
{
    private static readonly RunId RunId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly SnapSettings Settings = new(
        Length.FromInches(2m),
        Length.FromInches(12m),
        Length.FromInches(3m),
        Length.FromInches(0.5m));

    [Fact]
    public void Resolve_SelectsHigherPriorityCandidateDeterministically()
    {
        var resolver = new DefaultSnapResolver(
        [
            new StubSource(
            [
                Candidate(SnapKind.Grid, "grid", 0, 0.1m),
                Candidate(SnapKind.RunEndpoint, "endpoint", 0, 0.5m)
            ])
        ]);

        var result = resolver.Resolve(CreateRequest(), previousWinner: null);

        Assert.NotNull(result.Winner);
        Assert.Equal("endpoint", result.Winner!.SourceId);
    }

    [Fact]
    public void Resolve_ExcludesCandidatesOutsideToleranceThreshold()
    {
        var resolver = new DefaultSnapResolver(
        [
            new StubSource(
            [
                Candidate(SnapKind.Grid, "inside", 0, 2m),
                Candidate(SnapKind.Grid, "outside", 1, 2.01m)
            ])
        ]);

        var result = resolver.Resolve(CreateRequest(), previousWinner: null);

        Assert.Single(result.Candidates);
        Assert.Equal("inside", result.Winner!.SourceId);
    }

    [Fact]
    public void Resolve_BreaksAmbiguousSnapsByStableSourceOrdering()
    {
        var resolver = new DefaultSnapResolver(
        [
            new StubSource(
            [
                Candidate(SnapKind.CabinetFace, "b-face", 1, 0.5m),
                Candidate(SnapKind.CabinetFace, "a-face", 2, 0.5m)
            ])
        ]);

        var result = resolver.Resolve(CreateRequest(), previousWinner: null);

        Assert.Equal("a-face", result.Winner!.SourceId);
    }

    [Fact]
    public void Resolve_RetainsPreviousWinnerUntilHysteresisIsExceeded()
    {
        var resolver = new DefaultSnapResolver(
        [
            new StubSource(
            [
                Candidate(SnapKind.CabinetFace, "previous", 0, 1.4m),
                Candidate(SnapKind.CabinetFace, "challenger", 1, 1m)
            ])
        ]);

        var retained = resolver.Resolve(CreateRequest(), Candidate(SnapKind.CabinetFace, "previous", 0, 1.4m));
        Assert.Equal("previous", retained.Winner!.SourceId);

        var released = resolver.Resolve(CreateRequest(), Candidate(SnapKind.CabinetFace, "previous", 0, 1.6m));
        Assert.Equal("challenger", released.Winner!.SourceId);
    }

    private static SnapRequest CreateRequest() =>
        new(
            new EditorSceneSnapshot(
            [
                new RunSceneView(
                    RunId,
                    Point2D.Origin,
                    new Point2D(100m, 0m),
                    Vector2D.UnitX,
                    Length.FromInches(100m),
                    [])
            ]),
            new DragContext(
                DragType.PlaceCabinet,
                Point2D.Origin,
                Vector2D.Zero,
                Length.FromInches(24m),
                Length.FromInches(24m),
                "base-24",
                null,
                null,
                null,
                RunId),
            Settings);

    private static SnapCandidate Candidate(SnapKind kind, string sourceId, int sourceIndex, decimal distanceInches) =>
        new(
            kind,
            RunId,
            sourceId,
            sourceIndex,
            new Point2D(distanceInches, 0m),
            Length.FromInches(distanceInches),
            sourceId);

    private sealed class StubSource : ISnapCandidateSource
    {
        private readonly IReadOnlyList<SnapCandidate> _candidates;

        public StubSource(IReadOnlyList<SnapCandidate> candidates)
        {
            _candidates = candidates;
        }

        public IReadOnlyList<SnapCandidate> GetCandidates(SnapRequest request) =>
            _candidates.Where(candidate => candidate.Distance <= request.Settings.SnapRadius).ToArray();
    }
}
