using CabinetDesigner.Persistence.Mapping;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Mapping;

public sealed class RoundTripTests
{
    [Fact]
    public void ProjectMapper_RoundTripsFilePath()
    {
        var now = DateTimeOffset.Parse("2026-04-08T17:00:00Z");
        var record = new ProjectRecord(ProjectId.New(), "Kitchen", "Desc", now, now, ApprovalState.UnderReview, @"C:\projects\kitchen.cds");

        var roundTrip = ProjectMapper.ToRecord(ProjectMapper.ToRow(record));

        Assert.Equal(record, roundTrip);
    }

    [Fact]
    public void RevisionMapper_RoundTripsApprovalNotes()
    {
        var now = DateTimeOffset.Parse("2026-04-08T17:00:00Z");
        var record = new RevisionRecord(RevisionId.New(), ProjectId.New(), 3, ApprovalState.Approved, now, now, "approver", "Rev 3", "Approved pending print");

        var roundTrip = RevisionMapper.ToRecord(RevisionMapper.ToRow(record));

        Assert.Equal(record, roundTrip);
    }

    [Fact]
    public void RoomMapper_RoundTrips()
    {
        var room = new Room(RoomId.New(), RevisionId.New(), "Kitchen", Length.FromFeet(9));

        var roundTrip = RoomMapper.ToDomain(RoomMapper.ToRow(room, DateTimeOffset.UnixEpoch));

        Assert.Equal(room.Id, roundTrip.Id);
        Assert.Equal(room.RevisionId, roundTrip.RevisionId);
        Assert.Equal(room.Name, roundTrip.Name);
        Assert.Equal(room.CeilingHeight, roundTrip.CeilingHeight);
    }

    [Fact]
    public void WallMapper_RoundTrips()
    {
        var wall = new Wall(WallId.New(), RoomId.New(), new Point2D(1.25m, 2.5m), new Point2D(90.75m, 2.5m), new Thickness(Length.FromInches(4), Length.FromInches(3.5m)));

        var roundTrip = WallMapper.ToDomain(WallMapper.ToRow(wall, RevisionId.New(), DateTimeOffset.UnixEpoch));

        Assert.Equal(wall.Id, roundTrip.Id);
        Assert.Equal(wall.RoomId, roundTrip.RoomId);
        Assert.Equal(wall.StartPoint, roundTrip.StartPoint);
        Assert.Equal(wall.EndPoint, roundTrip.EndPoint);
        Assert.Equal(wall.WallThickness, roundTrip.WallThickness);
    }

    [Fact]
    public void RunMapper_RoundTrips()
    {
        var run = new CabinetRun(RunId.New(), WallId.New(), Length.FromInches(102.5m));
        run.SetLeftEndCondition(EndConditionType.AgainstWall);
        run.SetRightEndCondition(EndConditionType.Open);

        var roundTrip = RunMapper.ToDomain(RunMapper.ToRow(run, RevisionId.New(), 4, DateTimeOffset.UnixEpoch));

        Assert.Equal(run.Id, roundTrip.Id);
        Assert.Equal(run.WallId, roundTrip.WallId);
        Assert.Equal(run.Capacity, roundTrip.Capacity);
        Assert.Equal(run.LeftEndCondition.Type, roundTrip.LeftEndCondition.Type);
        Assert.Equal(run.RightEndCondition.Type, roundTrip.RightEndCondition.Type);
    }

    [Fact]
    public void PartMapper_RoundTripsGrainDirection()
    {
        var part = new GeneratedPart
        {
            PartId = Guid.NewGuid().ToString("N"),
            CabinetId = CabinetId.New(),
            PartType = "panel",
            Width = Length.FromInches(18.25m),
            Height = Length.FromInches(31.5m),
            MaterialThickness = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.71m)),
            MaterialId = MaterialId.New(),
            GrainDirection = GrainDirection.WidthWise,
            Edges = new EdgeTreatment("top", null, "left", "right"),
            Label = "PANEL-1"
        };

        var roundTrip = PartMapper.ToDomain(PartMapper.ToRow(part, RevisionId.New(), DateTimeOffset.UnixEpoch));

        Assert.Equal(part.PartId, roundTrip.PartId);
        Assert.Equal(part.CabinetId, roundTrip.CabinetId);
        Assert.Equal(part.PartType, roundTrip.PartType);
        Assert.Equal(part.Width, roundTrip.Width);
        Assert.Equal(part.Height, roundTrip.Height);
        Assert.Equal(part.MaterialThickness, roundTrip.MaterialThickness);
        Assert.Equal(part.MaterialId, roundTrip.MaterialId);
        Assert.Equal(part.GrainDirection, roundTrip.GrainDirection);
        Assert.Equal(part.Edges, roundTrip.Edges);
        Assert.Equal(part.Label, roundTrip.Label);
    }

    [Fact]
    public void ExplanationNodeMapper_RoundTrips()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-08T17:00:00Z");
        var record = new ExplanationNodeRecord(
            ExplanationNodeId.New(),
            RevisionId.New(),
            CommandId.New(),
            2,
            ExplanationNodeType.StageDecision,
            "FitCheck",
            "Validated cabinet placement",
            ["cab-1", "cab-2"],
            ExplanationNodeId.New(),
            "depends_on",
            ExplanationNodeStatus.Active,
            createdAt);

        var roundTrip = ExplanationNodeMapper.ToRecord(ExplanationNodeMapper.ToRow(record));

        Assert.Equal(record.Id, roundTrip.Id);
        Assert.Equal(record.RevisionId, roundTrip.RevisionId);
        Assert.Equal(record.CommandId, roundTrip.CommandId);
        Assert.Equal(record.StageNumber, roundTrip.StageNumber);
        Assert.Equal(record.NodeType, roundTrip.NodeType);
        Assert.Equal(record.DecisionType, roundTrip.DecisionType);
        Assert.Equal(record.Description, roundTrip.Description);
        Assert.Equal(record.AffectedEntityIds, roundTrip.AffectedEntityIds);
        Assert.Equal(record.ParentNodeId, roundTrip.ParentNodeId);
        Assert.Equal(record.EdgeType, roundTrip.EdgeType);
        Assert.Equal(record.Status, roundTrip.Status);
        Assert.Equal(record.CreatedAt, roundTrip.CreatedAt);
    }

    [Fact]
    public void ValidationIssueMapper_RoundTrips()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-08T17:00:00Z");
        var record = new ValidationIssueRecord(
            new ValidationIssueId("Overlap", ["cab-2", "cab-1"]),
            RevisionId.New(),
            createdAt,
            ValidationSeverity.Error,
            "Overlap",
            "Cabinets overlap.",
            ["cab-2", "cab-1"],
            """{"suggested":"move"}""");

        var roundTrip = ValidationIssueMapper.ToRecord(ValidationIssueMapper.ToRow(record));

        Assert.Equal(record.Id, roundTrip.Id);
        Assert.Equal(record.RevisionId, roundTrip.RevisionId);
        Assert.Equal(record.RunAt, roundTrip.RunAt);
        Assert.Equal(record.Severity, roundTrip.Severity);
        Assert.Equal(record.RuleCode, roundTrip.RuleCode);
        Assert.Equal(record.Message, roundTrip.Message);
        Assert.Equal(record.AffectedEntityIds, roundTrip.AffectedEntityIds);
        Assert.Equal(record.SuggestedFixJson, roundTrip.SuggestedFixJson);
    }
}
