using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

/// <summary>
/// Regression tests for C2: Fix Snapshot Data Corruption
/// Verifies that CabinetCategory and ConstructionMethod are preserved through the state round-trip.
/// </summary>
public sealed class CurrentWorkingRevisionSourceTests
{
    [Fact]
    public void CaptureCurrentState_WithWallCategory_PreservesCategoryInRoundTrip()
    {
        // Arrange
        var store = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        var cabinetId = CabinetId.New();
        var slot = run.AppendCabinet(cabinetId, Length.FromInches(30m));

        // Create a cabinet with Wall category
        var cabinetStateRecord = new CabinetStateRecord(
            cabinetId,
            "wall-30",
            Length.FromInches(30m),
            Length.FromInches(24m),
            run.Id,
            slot.Id,
            CabinetCategory.Wall,
            ConstructionMethod.FaceFrame);

        var domainCabinet = new Cabinet(
            cabinetId,
            RevisionId.New(),
            "wall-30",
            CabinetCategory.Wall,
            ConstructionMethod.FaceFrame,
            Length.FromInches(30m),
            Length.FromInches(24m),
            Length.FromInches(34.5m));

        store.AddWall(wall);
        store.AddRun(run, wall.StartPoint, wall.EndPoint);
        store.AddCabinet(cabinetStateRecord);

        var now = DateTimeOffset.UtcNow;
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var projectRecord = new ProjectRecord(projectId, "Test", null, now, now, ApprovalState.Draft);
        var revisionRecord = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, now, null, null, null);
        var workingRevision = new WorkingRevision(
            revisionRecord,
            [],
            [wall],
            [run],
            [domainCabinet],
            []);
        var persistedState = new PersistedProjectState(projectRecord, revisionRecord, workingRevision, null);

        var source = new CurrentWorkingRevisionSource(store);
        source.SetCurrentState(persistedState);

        // Act
        var capturedState = source.CaptureCurrentState();

        // Assert - the cabinet should preserve its Wall category and FaceFrame construction
        var resultCabinet = Assert.Single(capturedState.WorkingRevision.Cabinets);
        Assert.Equal(CabinetCategory.Wall, resultCabinet.Category);
        Assert.Equal(ConstructionMethod.FaceFrame, resultCabinet.Construction);
    }

    [Fact]
    public void CaptureCurrentState_WithTallCategory_PreservesCategoryInRoundTrip()
    {
        // Arrange
        var store = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        var cabinetId = CabinetId.New();
        var slot = run.AppendCabinet(cabinetId, Length.FromInches(36m));

        // Create a cabinet with Tall category
        var cabinetStateRecord = new CabinetStateRecord(
            cabinetId,
            "tall-36",
            Length.FromInches(36m),
            Length.FromInches(24m),
            run.Id,
            slot.Id,
            CabinetCategory.Tall,
            ConstructionMethod.Frameless);

        var domainCabinet = new Cabinet(
            cabinetId,
            RevisionId.New(),
            "tall-36",
            CabinetCategory.Tall,
            ConstructionMethod.Frameless,
            Length.FromInches(36m),
            Length.FromInches(24m),
            Length.FromInches(34.5m));

        store.AddWall(wall);
        store.AddRun(run, wall.StartPoint, wall.EndPoint);
        store.AddCabinet(cabinetStateRecord);

        var now = DateTimeOffset.UtcNow;
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var projectRecord = new ProjectRecord(projectId, "Test", null, now, now, ApprovalState.Draft);
        var revisionRecord = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, now, null, null, null);
        var workingRevision = new WorkingRevision(
            revisionRecord,
            [],
            [wall],
            [run],
            [domainCabinet],
            []);
        var persistedState = new PersistedProjectState(projectRecord, revisionRecord, workingRevision, null);

        var source = new CurrentWorkingRevisionSource(store);
        source.SetCurrentState(persistedState);

        // Act
        var capturedState = source.CaptureCurrentState();

        // Assert - the cabinet should preserve its Tall category
        var resultCabinet = Assert.Single(capturedState.WorkingRevision.Cabinets);
        Assert.Equal(CabinetCategory.Tall, resultCabinet.Category);
        Assert.Equal(ConstructionMethod.Frameless, resultCabinet.Construction);
    }

    [Fact]
    public void CaptureCurrentState_WithBaseCategory_PreservesCategoryInRoundTrip()
    {
        // Arrange
        var store = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        var cabinetId = CabinetId.New();
        var slot = run.AppendCabinet(cabinetId, Length.FromInches(30m));

        // Create a cabinet with Base category (the default case from the bug)
        var cabinetStateRecord = new CabinetStateRecord(
            cabinetId,
            "base-30",
            Length.FromInches(30m),
            Length.FromInches(24m),
            run.Id,
            slot.Id,
            CabinetCategory.Base,
            ConstructionMethod.Frameless);

        var domainCabinet = new Cabinet(
            cabinetId,
            RevisionId.New(),
            "base-30",
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            Length.FromInches(30m),
            Length.FromInches(24m),
            Length.FromInches(34.5m));

        store.AddWall(wall);
        store.AddRun(run, wall.StartPoint, wall.EndPoint);
        store.AddCabinet(cabinetStateRecord);

        var now = DateTimeOffset.UtcNow;
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var projectRecord = new ProjectRecord(projectId, "Test", null, now, now, ApprovalState.Draft);
        var revisionRecord = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, now, null, null, null);
        var workingRevision = new WorkingRevision(
            revisionRecord,
            [],
            [wall],
            [run],
            [domainCabinet],
            []);
        var persistedState = new PersistedProjectState(projectRecord, revisionRecord, workingRevision, null);

        var source = new CurrentWorkingRevisionSource(store);
        source.SetCurrentState(persistedState);

        // Act
        var capturedState = source.CaptureCurrentState();

        // Assert
        var resultCabinet = Assert.Single(capturedState.WorkingRevision.Cabinets);
        Assert.Equal(CabinetCategory.Base, resultCabinet.Category);
        Assert.Equal(ConstructionMethod.Frameless, resultCabinet.Construction);
    }
}
