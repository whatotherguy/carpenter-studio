using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class RunSummaryPanelViewModelTests
{
    private static readonly Guid Run1Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Run2Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Wall1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Wall2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Cabinet1Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Cabinet2Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Cabinet3Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Cabinet4Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public void InitialState_NoProject_ShowsHonestPlaceholder()
    {
        var context = CreateContext(includeProjectState: false, includeRuns: false);
        using var viewModel = CreateViewModel(context);

        Assert.False(viewModel.IsProjectOpen);
        Assert.False(viewModel.HasActiveRun);
        Assert.False(viewModel.HasSelection);
        Assert.Equal("No active run selected", viewModel.ActiveRunDisplay);
        Assert.Equal("-", viewModel.TotalWidthDisplay);
        Assert.Equal("-", viewModel.CabinetCountDisplay);
        Assert.Equal("0 slots", viewModel.SlotCountDisplay);
        Assert.Equal("0 selected", viewModel.SelectionSummaryDisplay);
        Assert.Equal("Open a project to see the run summary.", viewModel.StatusMessage);
        Assert.Equal("No project open", viewModel.SourceLabel);
        Assert.Equal("Open a project to see the run summary.", viewModel.EmptyStateText);
        Assert.Empty(viewModel.Slots);
    }

    [Fact]
    public void ProjectOpenedEvent_WithRunState_ShowsLiveSummary()
    {
        var context = CreateContext(includeProjectState: true, includeRuns: true);
        using var viewModel = CreateViewModel(context);

        context.EventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        Assert.True(viewModel.IsProjectOpen);
        Assert.True(viewModel.HasActiveRun);
        Assert.Equal("0 selected", viewModel.SelectionSummaryDisplay);
        Assert.Equal("Live run summary", viewModel.SourceLabel);
        Assert.Equal("Showing the active run.", viewModel.StatusMessage);
        Assert.Equal("1 cabinet", viewModel.CabinetCountDisplay);
        Assert.Equal("66\"", viewModel.TotalWidthDisplay);
        Assert.Equal("2 slots", viewModel.SlotCountDisplay);
        Assert.Equal(2, viewModel.Slots.Count);
    }

    [Fact]
    public void OnSelectionChanged_SwitchesActiveRunAndHighlightsSelectedSlots()
    {
        var context = CreateContext(includeProjectState: true, includeRuns: true);
        using var viewModel = CreateViewModel(context);

        context.EventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        viewModel.OnSelectionChanged([context.SecondRunCabinetId]);

        Assert.True(viewModel.HasSelection);
        Assert.Equal("1 selected", viewModel.SelectionSummaryDisplay);
        Assert.Equal("Showing the run for the selected cabinet.", viewModel.StatusMessage);
        Assert.True(viewModel.HasActiveRun);
        Assert.Equal("Live run summary", viewModel.SourceLabel);
        Assert.Equal("2 cabinets", viewModel.CabinetCountDisplay);
        Assert.Equal("66\"", viewModel.TotalWidthDisplay);
        Assert.Equal(2, viewModel.Slots.Count);
        Assert.True(viewModel.Slots[0].IsSelected);
        Assert.False(viewModel.Slots[1].IsSelected);

        viewModel.OnSelectionChanged([context.SecondRunCabinetId, context.FirstRunCabinetId]);

        Assert.Equal("2 selected", viewModel.SelectionSummaryDisplay);
    }

    [Fact]
    public void DesignChangedEvent_RefreshesActiveRunState()
    {
        var context = CreateContext(includeProjectState: true, includeRuns: true);
        using var viewModel = CreateViewModel(context);

        context.EventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        Assert.Equal("66\"", viewModel.TotalWidthDisplay);
        Assert.Equal("2 cabinets", viewModel.CabinetCountDisplay);

        context.SeedUpdatedSecondRunState();
        context.EventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "layout.update", true, [], [], [])));

        Assert.Equal("96\"", viewModel.TotalWidthDisplay);
        Assert.Equal("3 cabinets", viewModel.CabinetCountDisplay);
        Assert.Equal("3 slots", viewModel.SlotCountDisplay);
        Assert.Equal(3, viewModel.Slots.Count);
    }

    [Fact]
    public void ProjectOpenedEvent_WithoutRuns_ShowsEmptyState()
    {
        var context = CreateContext(includeProjectState: true, includeRuns: false);
        using var viewModel = CreateViewModel(context);

        context.EventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        Assert.True(viewModel.IsProjectOpen);
        Assert.False(viewModel.HasActiveRun);
        Assert.Equal("No runs in design", viewModel.SourceLabel);
        Assert.Equal("No runs in design.", viewModel.EmptyStateText);
        Assert.Equal("No runs in design.", viewModel.StatusMessage);
        Assert.Empty(viewModel.Slots);
    }

    [Fact]
    public void ProjectClosedEvent_ResetsToNoProjectState()
    {
        var context = CreateContext(includeProjectState: true, includeRuns: true);
        using var viewModel = CreateViewModel(context);

        context.EventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));
        viewModel.OnSelectionChanged([context.SecondRunCabinetId]);
        context.ClearProjectState();
        context.EventBus.Publish(new ProjectClosedEvent(Guid.NewGuid()));

        Assert.False(viewModel.IsProjectOpen);
        Assert.False(viewModel.HasSelection);
        Assert.False(viewModel.HasActiveRun);
        Assert.Equal("No project open", viewModel.SourceLabel);
        Assert.Equal("Open a project to see the run summary.", viewModel.EmptyStateText);
        Assert.Equal("Open a project to see the run summary.", viewModel.StatusMessage);
        Assert.Equal("0 selected", viewModel.SelectionSummaryDisplay);
        Assert.Empty(viewModel.Slots);
    }

    private static RunSummaryPanelViewModel CreateViewModel(RunSummaryTestContext context) =>
        new(context.RunSummaryService, context.CurrentProjectState, context.EventBus);

    private static RunSummaryTestContext CreateContext(bool includeProjectState, bool includeRuns)
    {
        var stateStore = new InMemoryDesignStateStore();
        var currentProjectState = new CurrentWorkingRevisionSource(stateStore);
        var eventBus = new ApplicationEventBus();
        var runSummaryService = new RunSummaryService(currentProjectState, stateStore);

        if (includeProjectState)
        {
            currentProjectState.SetCurrentState(includeRuns
                ? BuildWorkingStateWithRuns()
                : BuildWorkingStateWithoutRuns());
        }

        return new RunSummaryTestContext(stateStore, currentProjectState, eventBus, runSummaryService);
    }

    private static PersistedProjectState BuildWorkingStateWithRuns()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var project = new ProjectRecord(projectId, "Demo Project", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");

        var room = new Room(RoomId.New(), revisionId, "Kitchen", Length.FromFeet(8));

        var wall1 = new Wall(
            new WallId(Wall1Id),
            room.Id,
            Point2D.Origin,
            new Point2D(96m, 0m),
            Thickness.Exact(Length.FromInches(4m)));
        var run1 = new CabinetRun(new RunId(Run1Id), wall1.Id, Length.FromInches(96m));
        run1.AppendCabinet(new CabinetId(Cabinet1Id), Length.FromInches(24m));
        var cabinet1 = new Cabinet(new CabinetId(Cabinet1Id), revisionId, "base-24", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(24m), Length.FromInches(24m), Length.FromInches(34.5m));

        var wall2 = new Wall(
            new WallId(Wall2Id),
            room.Id,
            new Point2D(0m, 96m),
            new Point2D(96m, 96m),
            Thickness.Exact(Length.FromInches(4m)));
        var run2 = new CabinetRun(new RunId(Run2Id), wall2.Id, Length.FromInches(120m));
        run2.AppendCabinet(new CabinetId(Cabinet2Id), Length.FromInches(30m));
        run2.AppendCabinet(new CabinetId(Cabinet3Id), Length.FromInches(36m));
        var cabinet2 = new Cabinet(new CabinetId(Cabinet2Id), revisionId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(30m), Length.FromInches(24m), Length.FromInches(34.5m));
        var cabinet3 = new Cabinet(new CabinetId(Cabinet3Id), revisionId, "base-36", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(36m), Length.FromInches(24m), Length.FromInches(34.5m));

        var workingRevision = new WorkingRevision(
            revision,
            [room],
            [wall1, wall2],
            [run1, run2],
            [cabinet1, cabinet2, cabinet3],
            []);

        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);
        return new PersistedProjectState(project, revision, workingRevision, checkpoint);
    }

    private static PersistedProjectState BuildWorkingStateWithoutRuns()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var project = new ProjectRecord(projectId, "Demo Project", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);
        return new PersistedProjectState(project, revision, workingRevision, checkpoint);
    }

    private sealed record RunSummaryTestContext(
        InMemoryDesignStateStore StateStore,
        CurrentWorkingRevisionSource CurrentProjectState,
        ApplicationEventBus EventBus,
        RunSummaryService RunSummaryService)
    {
        public Guid FirstRunCabinetId => Cabinet1Id;

        public Guid SecondRunCabinetId => Cabinet2Id;

        public void SeedUpdatedSecondRunState()
        {
            CurrentProjectState.SetCurrentState(BuildWorkingStateWithUpdatedSecondRun());
        }

        public void ClearProjectState() => CurrentProjectState.Clear();

        private static PersistedProjectState BuildWorkingStateWithUpdatedSecondRun()
        {
            var createdAt = DateTimeOffset.UtcNow;
            var projectId = ProjectId.New();
            var revisionId = RevisionId.New();
            var project = new ProjectRecord(projectId, "Demo Project", null, createdAt, createdAt, ApprovalState.Draft);
            var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
            var room = new Room(RoomId.New(), revisionId, "Kitchen", Length.FromFeet(8));

            var wall1 = new Wall(
                new WallId(Wall1Id),
                room.Id,
                Point2D.Origin,
                new Point2D(96m, 0m),
                Thickness.Exact(Length.FromInches(4m)));
            var run1 = new CabinetRun(new RunId(Run1Id), wall1.Id, Length.FromInches(96m));
            run1.AppendCabinet(new CabinetId(Cabinet1Id), Length.FromInches(24m));
            var cabinet1 = new Cabinet(new CabinetId(Cabinet1Id), revisionId, "base-24", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(24m), Length.FromInches(24m), Length.FromInches(34.5m));

            var wall2 = new Wall(
                new WallId(Wall2Id),
                room.Id,
                new Point2D(0m, 96m),
                new Point2D(96m, 96m),
                Thickness.Exact(Length.FromInches(4m)));
            var run2 = new CabinetRun(new RunId(Run2Id), wall2.Id, Length.FromInches(150m));
            run2.AppendCabinet(new CabinetId(Cabinet2Id), Length.FromInches(30m));
            run2.AppendCabinet(new CabinetId(Cabinet3Id), Length.FromInches(36m));
            run2.AppendCabinet(new CabinetId(Cabinet4Id), Length.FromInches(30m));
            var cabinet2 = new Cabinet(new CabinetId(Cabinet2Id), revisionId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(30m), Length.FromInches(24m), Length.FromInches(34.5m));
            var cabinet3 = new Cabinet(new CabinetId(Cabinet3Id), revisionId, "base-36", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(36m), Length.FromInches(24m), Length.FromInches(34.5m));
            var cabinet4 = new Cabinet(new CabinetId(Cabinet4Id), revisionId, "base-30b", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(30m), Length.FromInches(24m), Length.FromInches(34.5m));

            var workingRevision = new WorkingRevision(
                revision,
                [room],
                [wall1, wall2],
                [run1, run2],
                [cabinet1, cabinet2, cabinet3, cabinet4],
                []);

            var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);
            return new PersistedProjectState(project, revision, workingRevision, checkpoint);
        }
    }
}
