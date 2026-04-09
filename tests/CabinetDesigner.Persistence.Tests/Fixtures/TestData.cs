using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Persistence.Tests.Fixtures;

internal static class TestData
{
    public static (ProjectRecord Project, RevisionRecord Revision, WorkingRevision WorkingRevision, AutosaveCheckpoint Checkpoint) CreatePersistedState()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-08T17:00:00Z");
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var room = new Room(RoomId.New(), revisionId, "Kitchen", Length.FromFeet(8));
        var wall = new Wall(WallId.New(), room.Id, new Point2D(0, 0), new Point2D(120, 0), Thickness.Exact(Length.FromInches(4)));
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96));
        var cabinet = new Cabinet(CabinetId.New(), revisionId, "base-36", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(36), Length.FromInches(24), Length.FromInches(34.5m));
        cabinet.SetOverride("scribe", new OverrideValue.OfLength(Length.FromInches(0.5m)));
        run.AppendCabinet(cabinet.Id, cabinet.NominalWidth);

        var part = new GeneratedPart
        {
            PartId = Guid.NewGuid().ToString("N"),
            CabinetId = cabinet.Id,
            PartType = "left_side",
            Width = Length.FromInches(23.25m),
            Height = Length.FromInches(34.5m),
            MaterialThickness = Thickness.Exact(Length.FromInches(0.75m)),
            MaterialId = MaterialId.New(),
            GrainDirection = GrainDirection.LengthWise,
            Edges = new EdgeTreatment("top", null, "left", null),
            Label = "CAB1-L"
        };

        var project = new ProjectRecord(projectId, "Sample Kitchen", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var workingRevision = new WorkingRevision(revision, [room], [wall], [run], [cabinet], [part]);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);
        return (project, revision, workingRevision, checkpoint);
    }
}
