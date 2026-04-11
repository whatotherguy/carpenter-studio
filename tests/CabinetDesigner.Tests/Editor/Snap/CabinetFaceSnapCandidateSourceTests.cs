using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;
using Xunit;

namespace CabinetDesigner.Tests.Editor.Snap;

public sealed class CabinetFaceSnapCandidateSourceTests
{
    private static readonly RunId RunId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [Fact]
    public void GetCandidates_AllWithinSnapRadius_AssignsContiguousIndicesStartingAtZero()
    {
        // Arrange: two cabinets both with faces well within a large snap radius.
        var cabinet1Id = CabinetId.New();
        var cabinet2Id = CabinetId.New();
        var scene = new EditorSceneSnapshot(
        [
            new RunSceneView(
                RunId,
                Point2D.Origin,
                new Point2D(120m, 0m),
                Vector2D.UnitX,
                Length.FromInches(120m),
                [
                    new CabinetSceneView(cabinet1Id, RunId, 0, Length.FromInches(24m), Length.FromInches(24m),
                        Point2D.Origin, new Point2D(24m, 0m)),
                    new CabinetSceneView(cabinet2Id, RunId, 1, Length.FromInches(36m), Length.FromInches(24m),
                        new Point2D(24m, 0m), new Point2D(60m, 0m))
                ])
        ]);

        var request = new SnapRequest(
            scene,
            new DragContext(DragType.PlaceCabinet, new Point2D(30m, 0m), Vector2D.Zero,
                Length.FromInches(24m), Length.FromInches(24m), "base-24", null, null, null, RunId),
            new SnapSettings(Length.FromInches(1000m), Length.FromInches(12m), Length.FromInches(3m), Length.FromInches(0.5m)));

        var source = new CabinetFaceSnapCandidateSource();

        // Act
        var candidates = source.GetCandidates(request);

        // Assert: all four faces are within the large snap radius, indices must be contiguous 0,1,2,3
        Assert.Equal(4, candidates.Count);
        var indices = candidates.Select(c => c.SourceIndex).OrderBy(i => i).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, indices);
    }

    [Fact]
    public void GetCandidates_RejectedCandidates_DoNotAdvanceIndex()
    {
        // Arrange: cursor is near second cabinet only; first cabinet faces are far away.
        var cabinet1Id = CabinetId.New();
        var cabinet2Id = CabinetId.New();
        var scene = new EditorSceneSnapshot(
        [
            new RunSceneView(
                RunId,
                Point2D.Origin,
                new Point2D(120m, 0m),
                Vector2D.UnitX,
                Length.FromInches(120m),
                [
                    new CabinetSceneView(cabinet1Id, RunId, 0, Length.FromInches(24m), Length.FromInches(24m),
                        Point2D.Origin, new Point2D(24m, 0m)),
                    new CabinetSceneView(cabinet2Id, RunId, 1, Length.FromInches(36m), Length.FromInches(24m),
                        new Point2D(24m, 0m), new Point2D(60m, 0m))
                ])
        ]);

        // Cursor is at (24m, 0) — exactly on the boundary between the two cabinets.
        // With snap radius of 0.1 inches, only faces very close to the cursor are accepted.
        // Cabinet 1 left face: (0,0) → distance 24" — rejected.
        // Cabinet 1 right face: (24,0) → distance 0" — accepted.
        // Cabinet 2 left face: (24,0) → distance 0" — accepted.
        // Cabinet 2 right face: (60,0) → distance 36" — rejected.
        var request = new SnapRequest(
            scene,
            new DragContext(DragType.PlaceCabinet, new Point2D(24m, 0m), Vector2D.Zero,
                Length.FromInches(24m), Length.FromInches(24m), "base-24", null, null, null, RunId),
            new SnapSettings(Length.FromInches(0.1m), Length.FromInches(12m), Length.FromInches(3m), Length.FromInches(0.5m)));

        var source = new CabinetFaceSnapCandidateSource();

        // Act
        var candidates = source.GetCandidates(request);

        // Assert: only 2 candidates accepted (the two at distance 0); their indices start at 0.
        Assert.Equal(2, candidates.Count);
        var indices = candidates.Select(c => c.SourceIndex).OrderBy(i => i).ToArray();
        Assert.Equal(new[] { 0, 1 }, indices);
    }

    [Fact]
    public void GetCandidates_SameInputTwice_ProducesSameIndices()
    {
        // Arrange: deterministic scene.
        var cabinet1Id = CabinetId.New();
        var scene = new EditorSceneSnapshot(
        [
            new RunSceneView(
                RunId,
                Point2D.Origin,
                new Point2D(120m, 0m),
                Vector2D.UnitX,
                Length.FromInches(120m),
                [
                    new CabinetSceneView(cabinet1Id, RunId, 0, Length.FromInches(24m), Length.FromInches(24m),
                        Point2D.Origin, new Point2D(24m, 0m))
                ])
        ]);

        var request = new SnapRequest(
            scene,
            new DragContext(DragType.PlaceCabinet, new Point2D(12m, 0m), Vector2D.Zero,
                Length.FromInches(24m), Length.FromInches(24m), "base-24", null, null, null, RunId),
            new SnapSettings(Length.FromInches(1000m), Length.FromInches(12m), Length.FromInches(3m), Length.FromInches(0.5m)));

        var source = new CabinetFaceSnapCandidateSource();

        // Act
        var first = source.GetCandidates(request);
        var second = source.GetCandidates(request);

        // Assert: identical indices across both calls.
        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].SourceIndex, second[i].SourceIndex);
            Assert.Equal(first[i].SourceId, second[i].SourceId);
        }
    }
}
