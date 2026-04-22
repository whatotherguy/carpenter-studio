using System.Threading;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Editor;
using CabinetDesigner.Presentation;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class ProjectStartupViewModelTests
{
    [Fact]
    public async Task NewProject_Command_CreatesProject_AndEntersEditorMode()
    {
        var fixture = CreateFixture();

        await fixture.Shell.ProjectStartup.NewProjectCommand.ExecuteAsync();

        Assert.True(fixture.ProjectService.CreateCalled);
        Assert.NotNull(fixture.ProjectService.CurrentProject);
        Assert.Equal(ShellMode.Editor, fixture.Shell.Mode);
        Assert.Equal(fixture.ProjectService.CurrentProject, fixture.Shell.ActiveProject);
    }

    [Fact]
    public async Task OpenProject_WithSelection_SwitchesShellMode()
    {
        var fixture = CreateFixture();
        var project = new ProjectSummaryDto(Guid.NewGuid(), "Shop A", string.Empty, DateTimeOffset.UtcNow, "Rev 1", false);
        fixture.ProjectService.Seed(project);

        fixture.Shell.ProjectStartup.SelectedProject = project;
        await fixture.Shell.ProjectStartup.OpenSelectedProjectCommand.ExecuteAsync();

        Assert.True(fixture.ProjectService.OpenSelectedCalled);
        Assert.Equal(ShellMode.Editor, fixture.Shell.Mode);
        Assert.Equal(project, fixture.Shell.ActiveProject);
    }

    private static Fixture CreateFixture()
    {
        var eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        var projectService = new RecordingProjectService(eventBus);
        var undoRedo = new RecordingUndoRedoService();
        var runService = new RecordingRunService();
        var stateStore = new InMemoryDesignStateStore();
        var currentState = new RecordingCurrentState();
        currentState.SetCurrentState(CreateCurrentState());
        var canvas = new EditorCanvasViewModel(
            runService,
            eventBus,
            new RecordingSceneProjector(),
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            new RecordingCanvasHost(),
            new NoOpInteractionService(),
            logger);
        var catalog = new CatalogPanelViewModel(new CatalogService());
        var propertyInspector = new PropertyInspectorViewModel(runService, eventBus, logger);
        var runSummary = new RunSummaryPanelViewModel(new RunSummaryService(currentState, stateStore), currentState, eventBus);
        var issuePanel = new IssuePanelViewModel(new RecordingValidationSummaryService(), eventBus);
        var statusBar = new StatusBarViewModel(eventBus, new RecordingValidationSummaryService());
        var shell = new ShellViewModel(
            projectService,
            new NoOpRoomService(),
            undoRedo,
            eventBus,
            logger,
            canvas,
            catalog,
            propertyInspector,
            runSummary,
            issuePanel,
            statusBar,
            new StubDialogService(),
            new NoOpCutListExportWorkflowService());

        return new Fixture(shell, projectService);
    }

    private static PersistedProjectState CreateCurrentState()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-18T12:00:00Z");
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var project = new ProjectRecord(projectId, "Sample", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var room = new Room(RoomId.New(), revisionId, "Kitchen", Length.FromInches(96m));
        var workingRevision = new WorkingRevision(revision, [room], [], [], [], []);
        return new PersistedProjectState(project, revision, workingRevision, null);
    }

    private sealed record Fixture(ShellViewModel Shell, RecordingProjectService ProjectService);

    private sealed class RecordingProjectService : IProjectService
    {
        private readonly IApplicationEventBus _eventBus;
        private readonly List<ProjectSummaryDto> _projects = [];

        public RecordingProjectService(IApplicationEventBus eventBus) => _eventBus = eventBus;

        public ProjectSummaryDto? CurrentProject { get; private set; }

        public bool CreateCalled { get; private set; }

        public bool OpenSelectedCalled { get; private set; }

        public void Seed(ProjectSummaryDto project) => _projects.Add(project);

        public Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default)
        {
            CreateCalled = true;
            CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), name, string.Empty, DateTimeOffset.UtcNow, "Rev 1", false);
            _eventBus.Publish(new ProjectOpenedEvent(CurrentProject));
            return Task.FromResult(CurrentProject);
        }

        public Task<ProjectSummaryDto> OpenProjectAsync(ProjectId projectId, CancellationToken ct = default)
        {
            OpenSelectedCalled = true;
            var project = _projects.First(project => project.ProjectId == projectId.Value);
            CurrentProject = project;
            _eventBus.Publish(new ProjectOpenedEvent(project));
            return Task.FromResult(project);
        }

        public Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ProjectSummaryDto>> ListProjectsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectSummaryDto>>(_projects.ToArray());

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<RevisionDto> SaveRevisionAsync(string label, CancellationToken ct = default) => throw new NotImplementedException();

        public Task CloseAsync() => Task.CompletedTask;
    }

    private sealed class RecordingRunService : IRunService
    {
        public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request) => throw new NotImplementedException();
        public Task<CommandResultDto> DeleteRunAsync(RunId runId) => throw new NotImplementedException();
        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request) => throw new NotImplementedException();
        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();
        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) => throw new NotImplementedException();
        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request) => throw new NotImplementedException();
        public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) => throw new NotImplementedException();
        public RunSummaryDto GetRunSummary(RunId runId) => throw new NotImplementedException();
    }

    private sealed class RecordingUndoRedoService : IUndoRedoService
    {
        public bool CanUndo => false;

        public bool CanRedo => false;

        public CommandResultDto Undo() => throw new NotImplementedException();

        public CommandResultDto Redo() => throw new NotImplementedException();

        public void Clear()
        {
        }
    }

    private sealed class RecordingSceneProjector : ISceneProjector
    {
        public RenderSceneDto? Project() => new([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
    }

    private sealed class RecordingCanvasHost : IEditorCanvasHost
    {
        public object View => new();
        public bool IsCtrlHeld => false;
        public double CanvasWidth => 800;
        public double CanvasHeight => 600;
        public void UpdateScene(RenderSceneDto scene) { }
        public void UpdateViewport(ViewportTransform viewport) { }
        public void SetMouseDownHandler(Action<double, double> handler) { }
        public void SetMouseMoveHandler(Action<double, double> handler) { }
        public void SetMouseUpHandler(Action<double, double> handler) { }
        public void SetMouseWheelHandler(Action<double, double, double> handler) { }
        public void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd) { }
    }

    private sealed class TestEditorCanvasSession : IEditorCanvasSession
    {
        public EditorMode CurrentMode => EditorMode.Idle;
        public IReadOnlyList<Guid> SelectedCabinetIds => [];
        public Guid? HoveredCabinetId => null;
        public Guid? ActiveRoomId => null;
        public ViewportTransform Viewport => ViewportTransform.Default;
        public CabinetDesigner.Editor.Snap.SnapSettings SnapSettings => CabinetDesigner.Editor.Snap.SnapSettings.Default;
        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds) { }
        public void SetHoveredCabinetId(Guid? cabinetId) { }
        public void SetActiveRoom(Guid? roomId) { }
        public void ZoomAt(double screenX, double screenY, double scaleFactor) { }
        public void PanBy(double dx, double dy) { }
        public void BeginPan() { }
        public void EndPan() { }
        public void ResetViewport() { }
        public void FitViewport(ViewportBounds contentBounds, double canvasWidth, double canvasHeight) { }
    }

    private sealed class NoOpInteractionService : IEditorInteractionService
    {
        public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY) { }
        public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY) { }
        public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY) { }
        public DragPreviewResult OnDragMoved(double screenX, double screenY) => new(false, null, "Not used.");
        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default) => Task.FromResult(DragCommitResult.Failed("Not used."));
        public void OnDragAborted() { }
    }

    private sealed class NoOpRoomService : IRoomService
    {
        public Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct) =>
            Task.FromException<Room>(new InvalidOperationException("Not used."));

        public Task<Wall> AddWallAsync(RoomId roomId, Point2D start, Point2D end, CabinetDesigner.Domain.Geometry.Thickness thickness, CancellationToken ct) =>
            Task.FromException<Wall>(new InvalidOperationException("Not used."));

        public Task RemoveWallAsync(WallId wallId, CancellationToken ct) => Task.CompletedTask;
        public Task RenameRoomAsync(RoomId roomId, string newName, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Room>>([]);
    }

    private sealed class RecordingCurrentState : ICurrentPersistedProjectState
    {
        public PersistedProjectState? CurrentState { get; private set; }
        public void SetCurrentState(PersistedProjectState state) => CurrentState = state;
        public void Clear() => CurrentState = null;
    }

    private sealed class RecordingValidationSummaryService : IValidationSummaryService
    {
        public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() => [];
        public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) => [];
        public bool HasManufactureBlockers => false;
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? ShowOpenFileDialog(string title, string filter) => null;
        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName) => null;
        public string? ShowFolderPicker(string title) => null;
        public bool ShowYesNoDialog(string title, string message) => false;
    }

    private sealed class NoOpCutListExportWorkflowService : ICutListExportWorkflowService
    {
        public CutListWorkflowResult BuildCurrentProjectCutList() => new(false, null, null, "Not used in test.");
    }
}
