using System.Text.RegularExpressions;
using System.Threading;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class EmptyStateCopyTests
{
    [Fact]
    public void ProjectStartup_EmptyState_IsTwoSentences()
    {
        var eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        using var viewModel = new ProjectStartupViewModel(new EmptyProjectService(), eventBus, logger);

        AssertTwoSentenceCopy(viewModel.EmptyStateText);
    }

    [Fact]
    public void Catalog_EmptyState_IsTwoSentences()
    {
        var viewModel = new CatalogPanelViewModel(new CatalogService())
        {
            SearchText = "missing"
        };

        AssertTwoSentenceCopy(viewModel.EmptyStateText);
    }

    [Fact]
    public void IssuePanel_EmptyState_IsTwoSentences()
    {
        var eventBus = new ApplicationEventBus();
        using var viewModel = new IssuePanelViewModel(new EmptyValidationSummaryService(), eventBus);

        AssertTwoSentenceCopy(viewModel.EmptyStateText);
    }

    [Fact]
    public void RoomsPanel_EmptyState_IsTwoSentences()
    {
        var eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        using var viewModel = new RoomsPanelViewModel(new EmptyRoomService(), eventBus, logger, _ => { });

        AssertTwoSentenceCopy(viewModel.EmptyStateText);
    }

    [Fact]
    public void RunSummary_NoRuns_EmptyState_IsTwoSentences_AndMentionsF1()
    {
        var eventBus = new ApplicationEventBus();
        using var viewModel = new RunSummaryPanelViewModel(
            new StubRunSummaryService(new RunSummaryProjection(true, null)),
            new StubCurrentProjectState(),
            eventBus);

        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

        AssertTwoSentenceCopy(viewModel.EmptyStateText);
        Assert.Contains("F1", viewModel.EmptyStateText);
    }

    [Fact]
    public void PropertyInspector_SelectedCabinet_EmptyState_IsTwoSentences_AndMentionsF1()
    {
        var eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        var cabinetId = Guid.NewGuid();
        var propertyService = new SingleCabinetPropertyService(cabinetId);
        using var viewModel = new PropertyInspectorViewModel(propertyService, eventBus, logger);

        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));
        viewModel.OnSelectionChanged([cabinetId]);

        AssertTwoSentenceCopy(viewModel.EmptyStateText);
        Assert.Contains("F1", viewModel.EmptyStateText);
    }

    private static void AssertTwoSentenceCopy(string text)
    {
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Equal(2, Regex.Matches(text, @"[.!?](?:\s|$)").Count);
    }

    private sealed class EmptyProjectService : IProjectService
    {
        public ProjectSummaryDto? CurrentProject => null;

        public Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ProjectSummaryDto> OpenProjectAsync(ProjectId projectId, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<ProjectSummaryDto>> ListProjectsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectSummaryDto>>([]);

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<RevisionDto> SaveRevisionAsync(string label, CancellationToken ct = default) => throw new NotImplementedException();

        public Task CloseAsync() => Task.CompletedTask;
    }

    private sealed class EmptyValidationSummaryService : IValidationSummaryService
    {
        public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() => [];

        public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) => [];

        public bool HasManufactureBlockers => false;
    }

    private sealed class EmptyRoomService : IRoomService
    {
        public Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct) => throw new NotImplementedException();

        public Task<Wall> AddWallAsync(RoomId roomId, Point2D start, Point2D end, Thickness thickness, CancellationToken ct) => throw new NotImplementedException();

        public Task RemoveWallAsync(WallId wallId, CancellationToken ct) => Task.CompletedTask;

        public Task RenameRoomAsync(RoomId roomId, string newName, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Room>>([]);
    }

    private sealed class StubRunSummaryService : IRunSummaryService
    {
        private readonly RunSummaryProjection _projection;

        public StubRunSummaryService(RunSummaryProjection projection) => _projection = projection;

        public RunSummaryProjection GetCurrentSummary(IReadOnlyList<Guid> selectedCabinetIds) => _projection;
    }

    private sealed class StubCurrentProjectState : ICurrentPersistedProjectState
    {
        public PersistedProjectState? CurrentState { get; private set; }

        public void SetCurrentState(PersistedProjectState state) => CurrentState = state;

        public void Clear() => CurrentState = null;
    }

    private sealed class SingleCabinetPropertyService : ICabinetPropertyService
    {
        private readonly CabinetStateRecord _cabinet;

        public SingleCabinetPropertyService(Guid cabinetId)
        {
            _cabinet = new CabinetStateRecord(
                new CabinetId(cabinetId),
                "base-36",
                Length.FromInches(36m),
                Length.FromInches(24m),
                new RunId(Guid.NewGuid()),
                new RunSlotId(Guid.NewGuid()),
                CabinetCategory.Base,
                ConstructionMethod.Frameless,
                Length.FromInches(34.5m));
        }

        public IReadOnlyList<CabinetStateRecord> GetAllCabinets() => [_cabinet];

        public CabinetStateRecord? GetCabinet(Guid cabinetId) => _cabinet.CabinetId.Value == cabinetId ? _cabinet : null;

        public IReadOnlyList<CabinetStateRecord> GetCabinets(IReadOnlyList<Guid> cabinetIds) =>
            cabinetIds.Contains(_cabinet.CabinetId.Value) ? [_cabinet] : [];

        public Task<CommandResultDto> ResizeCabinetAsync(Guid cabinetId, decimal widthInches, decimal depthInches, decimal heightInches) => Task.FromResult(CommandResultDto.NoOp("resize_cabinet"));

        public Task<CommandResultDto> SetCabinetCategoryAsync(Guid cabinetId, CabinetCategory category) => Task.FromResult(CommandResultDto.NoOp("set_cabinet_category"));

        public Task<CommandResultDto> SetCabinetConstructionAsync(Guid cabinetId, ConstructionMethod construction) => Task.FromResult(CommandResultDto.NoOp("set_cabinet_construction"));

        public Task<CommandResultDto> AddOpeningAsync(Guid cabinetId, OpeningType openingType, decimal widthInches, decimal heightInches, int? insertIndex) => Task.FromResult(CommandResultDto.NoOp("add_opening"));

        public Task<CommandResultDto> RemoveOpeningAsync(Guid cabinetId, Guid openingId) => Task.FromResult(CommandResultDto.NoOp("remove_opening"));

        public Task<CommandResultDto> ReorderOpeningAsync(Guid cabinetId, Guid openingId, int newIndex) => Task.FromResult(CommandResultDto.NoOp("reorder_opening"));

        public Task<CommandResultDto> SetCabinetOverrideAsync(Guid cabinetId, string overrideKey, OverrideValueDto value) => Task.FromResult(CommandResultDto.NoOp("set_cabinet_override"));

        public Task<CommandResultDto> RemoveCabinetOverrideAsync(Guid cabinetId, string overrideKey) => Task.FromResult(CommandResultDto.NoOp("remove_cabinet_override"));
    }
}
