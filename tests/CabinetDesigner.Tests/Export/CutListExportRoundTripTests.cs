using System.Globalization;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Costing;
using CabinetDesigner.Application.Export;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CabinetDesigner.Tests.Export;

public sealed class CutListExportRoundTripTests
{
    [Fact]
    public void EndToEndPipeline_ExportForSampleRoom_ContentHashIsDeterministic()
    {
        using var firstProvider = CreateProvider();
        SeedSampleProject(firstProvider.GetRequiredService<CurrentWorkingRevisionSource>());
        var first = firstProvider.GetRequiredService<ICutListExportWorkflowService>().BuildCurrentProjectCutList();

        using var secondProvider = CreateProvider();
        SeedSampleProject(secondProvider.GetRequiredService<CurrentWorkingRevisionSource>());
        var second = secondProvider.GetRequiredService<ICutListExportWorkflowService>().BuildCurrentProjectCutList();

        Assert.True(first.Success, first.FailureMessage);
        Assert.True(second.Success, second.FailureMessage);
        Assert.NotNull(first.Export);
        Assert.NotNull(second.Export);
        Assert.Equal(first.Export!.ContentHash, second.Export!.ContentHash);
        Assert.Equal(first.Export.Csv, second.Export.Csv);
        Assert.Equal(first.Export.Txt, second.Export.Txt);
        Assert.Equal(first.Export.Html, second.Export.Html);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddSingleton<IPreviousApprovedCostLookup, NullPreviousApprovedCostLookup>();
        return services.BuildServiceProvider();
    }

    private static void SeedSampleProject(CurrentWorkingRevisionSource currentState)
    {
        var createdAt = DateTimeOffset.Parse("2026-04-21T18:00:00Z", CultureInfo.InvariantCulture);
        var projectId = new ProjectId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var revisionId = new RevisionId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        var roomId = new RoomId(Guid.Parse("50000000-0000-0000-0000-000000000001"));
        var baseWallId = new WallId(Guid.Parse("60000000-0000-0000-0000-000000000001"));
        var wallWallId = new WallId(Guid.Parse("70000000-0000-0000-0000-000000000001"));
        var baseRunId = new RunId(Guid.Parse("80000000-0000-0000-0000-000000000001"));
        var wallRunId = new RunId(Guid.Parse("90000000-0000-0000-0000-000000000001"));
        var baseCabinetId = new CabinetId(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
        var wallCabinetId = new CabinetId(Guid.Parse("b0000000-0000-0000-0000-000000000001"));

        var project = new ProjectRecord(projectId, "Production Demo", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var room = new Room(roomId, revisionId, "Kitchen", Length.FromFeet(8m));
        var baseWall = new Wall(baseWallId, roomId, Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var wallWall = new Wall(wallWallId, roomId, new Point2D(0m, 96m), new Point2D(120m, 96m), Thickness.Exact(Length.FromInches(4m)));
        var baseRun = new CabinetRun(baseRunId, baseWall.Id, Length.FromInches(96m));
        var wallRun = new CabinetRun(wallRunId, wallWall.Id, Length.FromInches(96m));
        baseRun.AppendCabinet(baseCabinetId, Length.FromInches(24m));
        wallRun.AppendCabinet(wallCabinetId, Length.FromInches(30m));

        var baseCabinet = new Cabinet(baseCabinetId, revisionId, "base-standard-24", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(24m), Length.FromInches(24m), Length.FromInches(34.5m));
        var wallCabinet = new Cabinet(wallCabinetId, revisionId, "wall-standard-30", CabinetCategory.Wall, ConstructionMethod.Frameless, Length.FromInches(30m), Length.FromInches(12m), Length.FromInches(30m));
        var workingRevision = new WorkingRevision(revision, [room], [baseWall, wallWall], [baseRun, wallRun], [baseCabinet, wallCabinet], []);
        var checkpoint = new AutosaveCheckpoint("cutlist-roundtrip", projectId, revisionId, createdAt, null, true);

        currentState.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));
    }
}
