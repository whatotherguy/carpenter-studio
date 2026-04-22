using System.Threading;
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
using CabinetDesigner.Editor;
using CabinetDesigner.Presentation;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class ShellExportCommandTests
{
    [Fact]
    public async Task ExportCutList_InvalidDesign_DoesNotProduceFiles_AndSurfacesIssues()
    {
        var tempFolder = Directory.CreateTempSubdirectory("cutlist-invalid").FullName;
        try
        {
            var dialog = new StubDialogService { FolderPath = tempFolder };
            using var shell = CreateShell(dialog, out var eventBus, out var projectService, out var workflowService, out var validationService);
            var project = new ProjectSummaryDto(Guid.NewGuid(), "Broken Project", string.Empty, DateTimeOffset.UtcNow, "Rev Bad", false);
            projectService.SeedCurrentProject(project);
            workflowService.NextResult = new CutListWorkflowResult(false, null, null, "Validation failed.");
            validationService.SetIssues([new ValidationIssueSummaryDto("ManufactureBlocker", "PACKAGING_INVALID_DESIGN", "Validation failed.", ["cabinet-1"])], hasManufactureBlockers: true);
            eventBus.Publish(new ProjectOpenedEvent(project));

            await shell.ExportCutListCommand.ExecuteAsync();

            Assert.Empty(Directory.GetFiles(tempFolder));
            Assert.Equal("Validation failed.", shell.StatusBar.StatusMessage);
            Assert.True(shell.IssuePanel.HasValidationData);
            Assert.True(shell.IssuePanel.HasManufactureBlockers);
            Assert.Single(shell.IssuePanel.AllIssues);
        }
        finally
        {
            Directory.Delete(tempFolder, recursive: true);
        }
    }

    [Fact]
    public async Task ExportCutList_ValidDesign_WritesThreeFiles_ToChosenFolder()
    {
        var tempFolder = Directory.CreateTempSubdirectory("cutlist-valid").FullName;
        try
        {
            var dialog = new StubDialogService { FolderPath = tempFolder };
            using var shell = CreateShell(dialog, out var eventBus, out var projectService, out var workflowService, out _);
            var project = new ProjectSummaryDto(Guid.NewGuid(), "Kitchen Project", string.Empty, DateTimeOffset.UtcNow, "Rev 1", false);
            projectService.SeedCurrentProject(project);
            workflowService.NextResult = new CutListWorkflowResult(
                true,
                new CabinetDesigner.Application.Export.CutListExportResult(
                    System.Text.Encoding.UTF8.GetBytes("csv"),
                    System.Text.Encoding.UTF8.GetBytes("txt"),
                    System.Text.Encoding.UTF8.GetBytes("html"),
                    "abc123"),
                "Kitchen-Project-Rev-1",
                null);
            eventBus.Publish(new ProjectOpenedEvent(project));

            await shell.ExportCutListCommand.ExecuteAsync();

            Assert.True(File.Exists(Path.Combine(tempFolder, "Kitchen-Project-Rev-1.cutlist.csv")));
            Assert.True(File.Exists(Path.Combine(tempFolder, "Kitchen-Project-Rev-1.cutlist.txt")));
            Assert.True(File.Exists(Path.Combine(tempFolder, "Kitchen-Project-Rev-1.cutlist.html")));
            Assert.Equal($"Cut list exported to {tempFolder}.", shell.StatusBar.StatusMessage);
        }
        finally
        {
            Directory.Delete(tempFolder, recursive: true);
        }
    }

    private static ShellViewModel CreateShell(
        IDialogService dialogService,
        out ApplicationEventBus eventBus,
        out RecordingProjectService projectService,
        out RecordingCutListExportWorkflowService workflowService,
        out RecordingValidationSummaryService validationService)
    {
        projectService = new RecordingProjectService();
        eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        projectService.EventBus = eventBus;
        validationService = new RecordingValidationSummaryService();
        workflowService = new RecordingCutListExportWorkflowService(eventBus);
        var stateStore = new InMemoryDesignStateStore();
        var currentState = new CurrentWorkingRevisionSource(stateStore);
        SeedRunSummaryState(currentState);
        var runSummaryService = new RunSummaryService(currentState, stateStore);

        var projector = new RecordingSceneProjector();
        var runService = new RecordingRunService();
        var canvas = new EditorCanvasViewModel(
            runService,
            eventBus,
            projector,
            new NoOpRoomService(),
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            new RecordingCanvasHost(),
            new NoOpInteractionService(),
            logger);

        var catalog = new CatalogPanelViewModel(new CatalogService());
        var propertyInspector = new PropertyInspectorViewModel(runService, eventBus, logger);
        var runSummary = new RunSummaryPanelViewModel(runSummaryService, currentState, eventBus);
        var statusBar = new StatusBarViewModel(eventBus, validationService);
        var issuePanel = new IssuePanelViewModel(validationService, eventBus);

        return new ShellViewModel(
            projectService,
            new NoOpRoomService(),
            new RecordingUndoRedoService(),
            eventBus,
            logger,
            canvas,
            catalog,
            propertyInspector,
            runSummary,
            issuePanel,
            statusBar,
            dialogService,
            workflowService);
    }

    private static void SeedRunSummaryState(CurrentWorkingRevisionSource currentState)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var project = new ProjectRecord(projectId, "Demo Project", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var room = new Room(RoomId.New(), revisionId, "Kitchen", Length.FromFeet(8));
        var wall = new Wall(new WallId(Guid.Parse("11111111-1111-1111-1111-111111111111")), room.Id, Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(new RunId(Guid.Parse("00000000-0000-0000-0000-000000000001")), wall.Id, Length.FromInches(96m));
        var cabinetId = new CabinetId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        run.AppendCabinet(cabinetId, Length.FromInches(24m));
        var cabinet = new Cabinet(cabinetId, revisionId, "base-24", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(24m), Length.FromInches(24m), Length.FromInches(34.5m));
        var workingRevision = new WorkingRevision(revision, [room], [wall], [run], [cabinet], []);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);
        currentState.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));
    }

    private sealed class RecordingCutListExportWorkflowService(ApplicationEventBus eventBus) : ICutListExportWorkflowService
    {
        public CutListWorkflowResult NextResult { get; set; } = new(false, null, null, "No result configured.");

        public CutListWorkflowResult BuildCurrentProjectCutList()
        {
            eventBus.Publish(new DesignChangedEvent(CommandResultDto.NoOp("cut_list.export")));
            return NextResult;
        }
    }

    private sealed class RecordingValidationSummaryService : IValidationSummaryService
    {
        private IReadOnlyList<ValidationIssueSummaryDto> _issues = [];

        public bool HasManufactureBlockers { get; private set; }

        public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() => _issues;

        public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) =>
            _issues.Where(issue => issue.AffectedEntityIds?.Contains(entityId, StringComparer.Ordinal) == true).ToArray();

        public void SetIssues(IReadOnlyList<ValidationIssueSummaryDto> issues, bool hasManufactureBlockers)
        {
            _issues = issues;
            HasManufactureBlockers = hasManufactureBlockers;
        }
    }

    private sealed class RecordingProjectService : IProjectService
    {
        public ProjectSummaryDto? CurrentProject { get; private set; }

        public IApplicationEventBus? EventBus { get; set; }

        public Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), "Opened", filePath, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task<ProjectSummaryDto> OpenProjectAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), "Opened", string.Empty, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), name, string.Empty, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task<IReadOnlyList<ProjectSummaryDto>> ListProjectsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectSummaryDto>>(CurrentProject is null ? [] : [CurrentProject]);

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<RevisionDto> SaveRevisionAsync(string label, CancellationToken ct = default) => throw new NotImplementedException();

        public Task CloseAsync()
        {
            if (CurrentProject is not null)
            {
                EventBus?.Publish(new ProjectClosedEvent(CurrentProject.ProjectId));
            }

            CurrentProject = null;
            return Task.CompletedTask;
        }

        public void SeedCurrentProject(ProjectSummaryDto project) => CurrentProject = project;
    }

    private sealed class RecordingUndoRedoService : IUndoRedoService
    {
        public bool CanUndo => false;

        public bool CanRedo => false;

        public CommandResultDto Undo() => CommandResultDto.NoOp("undo");

        public CommandResultDto Redo() => CommandResultDto.NoOp("redo");

        public void Clear()
        {
        }
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

    private sealed class RecordingSceneProjector : ISceneProjector
    {
        public RenderSceneDto? Project() =>
            new(
                [],
                [new RunRenderDto(Guid.Parse("00000000-0000-0000-0000-000000000001"), new LineSegment2D(Point2D.Origin, new Point2D(96m, 0m)), new Rect2D(Point2D.Origin, Length.FromInches(96m), Length.FromInches(24m)), true)],
                [],
                null,
                new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
    }

    private sealed class RecordingCanvasHost : IEditorCanvasHost
    {
        public object View => new();
        public bool IsCtrlHeld => false;
        public double CanvasWidth => 800d;
        public double CanvasHeight => 600d;
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
        public IReadOnlyList<Guid> SelectedCabinetIds { get; private set; } = [];
        public Guid? HoveredCabinetId => null;
        public Guid? ActiveRoomId => null;
        public ViewportTransform Viewport => ViewportTransform.Default;
        public CabinetDesigner.Editor.Snap.SnapSettings SnapSettings => CabinetDesigner.Editor.Snap.SnapSettings.Default;
        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds) => SelectedCabinetIds = cabinetIds.ToArray();
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
        public DragPreviewResult OnDragMoved(double screenX, double screenY) => new(false, null, "No active drag.");
        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default) => Task.FromResult(DragCommitResult.Failed("No active drag."));
        public void OnDragAborted() { }
    }

    private sealed class NoOpRoomService : IRoomService
    {
        public Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct) => Task.FromException<Room>(new InvalidOperationException("Not used in test."));
        public Task<Wall> AddWallAsync(RoomId roomId, Point2D start, Point2D end, Thickness thickness, CancellationToken ct) => Task.FromException<Wall>(new InvalidOperationException("Not used in test."));
        public Task RemoveWallAsync(WallId wallId, CancellationToken ct) => Task.CompletedTask;
        public Task RenameRoomAsync(RoomId roomId, string newName, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Room>>([]);
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? FolderPath { get; set; }

        public string? ShowOpenFileDialog(string title, string filter) => null;

        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName) => null;

        public string? ShowFolderPicker(string title) => FolderPath;

        public bool ShowYesNoDialog(string title, string message) => false;
    }
}
