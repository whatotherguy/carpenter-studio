using System.Threading;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.State;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Editor;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task SaveCommand_DelegatesToProjectService_AndRaisesPropertyChanges()
    {
        using var shell = CreateShellViewModel(out var projectService, out _, out _, out var eventBus, out _);
        var project = new ProjectSummaryDto(Guid.NewGuid(), "Shop A", "C:\\shop.cab", DateTimeOffset.UtcNow, "Rev 1", true);
        projectService.SeedCurrentProject(project);
        eventBus.Publish(new ProjectOpenedEvent(project));

        var changedProperties = new List<string>();
        shell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        await shell.SaveCommand.ExecuteAsync();

        Assert.True(projectService.SaveCalled);
        Assert.Contains(nameof(ShellViewModel.ActiveProject), changedProperties);
        Assert.False(shell.ActiveProject!.HasUnsavedChanges);
    }

    [Fact]
    public void ProjectOpenedEvent_UpdatesVisibleProjectState()
    {
        using var shell = CreateShellViewModel(out _, out var undoRedoService, out _, out var eventBus, out var currentState);
        SeedRunSummaryState(currentState);
        undoRedoService.CanUndoValue = true;
        undoRedoService.CanRedoValue = true;

        var saveCommandChanges = 0;
        var closeCommandChanges = 0;
        var undoCommandChanges = 0;
        var redoCommandChanges = 0;
        shell.SaveCommand.CanExecuteChanged += (_, _) => saveCommandChanges++;
        shell.CloseProjectCommand.CanExecuteChanged += (_, _) => closeCommandChanges++;
        shell.UndoCommand.CanExecuteChanged += (_, _) => undoCommandChanges++;
        shell.RedoCommand.CanExecuteChanged += (_, _) => redoCommandChanges++;

        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 3",
            false)));

        Assert.True(shell.HasActiveProject);
        Assert.Equal("Demo Project", shell.ActiveProjectNameText);
        Assert.Equal("Project open", shell.ProjectOpenText);
        Assert.Equal("Revision Rev 3", shell.RevisionText);
        Assert.Equal("Saved", shell.SaveStateText);
        Assert.Equal("Demo Project - Carpenter Studio", shell.WindowTitle);
        Assert.True(shell.SaveCommand.CanExecute(null));
        Assert.True(shell.CloseProjectCommand.CanExecute(null));
        Assert.True(shell.UndoCommand.CanExecute(null));
        Assert.True(shell.RedoCommand.CanExecute(null));
        Assert.True(shell.RunSummary.IsProjectOpen);
        Assert.True(shell.RunSummary.HasActiveRun);
        Assert.Equal("Live run summary", shell.RunSummary.SourceLabel);
        Assert.Equal("2 cabinets", shell.RunSummary.CabinetCountDisplay);
        Assert.Equal("66\"", shell.RunSummary.TotalWidthDisplay);
        Assert.Equal("2 slots", shell.RunSummary.SlotCountDisplay);
        Assert.NotEqual(0, saveCommandChanges);
        Assert.NotEqual(0, closeCommandChanges);
        Assert.NotEqual(0, undoCommandChanges);
        Assert.NotEqual(0, redoCommandChanges);
    }

    [Fact]
    public void PendingProjectName_TogglesNewCommandAvailability()
    {
        using var shell = CreateShellViewModel(out _, out _, out _, out _, out _);

        shell.PendingProjectName = "   ";

        Assert.False(shell.NewProjectCommand.CanExecute(null));

        shell.PendingProjectName = "Kitchen Remodel";

        Assert.True(shell.NewProjectCommand.CanExecute(null));
    }

    [Fact]
    public void OpenProjectCommand_IsAlwaysAvailable()
    {
        using var shell = CreateShellViewModel(out _, out _, out _, out _, out _);

        // The Open command launches a file dialog internally and is always enabled
        // so the user can browse even before a file path is pre-populated.
        Assert.True(shell.OpenProjectCommand.CanExecute(null));
    }

    [Fact]
    public void DesignChangedEvent_RefreshesCurrentStatusText()
    {
        using var shell = CreateShellViewModel(out _, out _, out _, out var eventBus, out _);

        var changedProperties = new List<string>();
        shell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "layout.update", true, [], [], [])));

        Assert.Equal("Design updated.", shell.CurrentStatusText);
        Assert.Contains(nameof(ShellViewModel.CurrentStatusText), changedProperties);
    }

    [Fact]
    public void CanvasSelection_UpdatesInspectorAndRunSummary()
    {
        using var shell = CreateShellViewModel(out _, out _, out var projector, out var eventBus, out var currentState);
        SeedRunSummaryState(currentState);
        var selectedCabinetId = Guid.NewGuid();
        projector.Scene = new RenderSceneDto(
            [],
            [],
            [
                new CabinetRenderDto(
                    selectedCabinetId,
                    Guid.NewGuid(),
                    new Rect2D(Point2D.Origin, Length.FromInches(36m), Length.FromInches(24m)),
                    "base-36",
                    "Base Cabinet 36\"",
                    CabinetRenderState.Normal,
                    [])
            ],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "layout.update", true, [], [], [])));

        shell.Canvas.SetSelectedCabinetIds([selectedCabinetId]);

        Assert.True(shell.PropertyInspector.HasSelection);
        Assert.True(shell.PropertyInspector.HasSingleSelection);
        Assert.StartsWith("Base Cabinet 36\"", shell.PropertyInspector.SelectedEntityLabel);
        Assert.Equal("Projected scene data", shell.PropertyInspector.SourceLabel);
        Assert.Equal("Nominal width editable", shell.PropertyInspector.EditabilityStatusDisplay);
        Assert.Equal("1 selected", shell.PropertyInspector.SelectionSummaryDisplay);
        Assert.Equal("6 details", shell.PropertyInspector.PropertySummaryDisplay);
        Assert.Equal("36\"", shell.PropertyInspector.NominalWidthDisplay);
        Assert.True(shell.RunSummary.HasSelection);
        Assert.Equal("1 selected", shell.RunSummary.SelectionSummaryDisplay);
        Assert.True(shell.RunSummary.HasActiveRun);
        Assert.Equal("Live run summary", shell.RunSummary.SourceLabel);
        Assert.Equal("Showing the run for the selected cabinet.", shell.RunSummary.StatusMessage);
        Assert.Equal("2 cabinets", shell.RunSummary.CabinetCountDisplay);
        Assert.Equal("66\"", shell.RunSummary.TotalWidthDisplay);
        Assert.Equal(2, shell.RunSummary.Slots.Count);
        Assert.False(shell.RunSummary.Slots[0].IsSelected);
        Assert.True(shell.RunSummary.Slots[1].IsSelected);
    }

    [Fact]
    public void ProjectClosedEvent_ClearsSelectionDrivenPanels()
    {
        using var shell = CreateShellViewModel(out _, out _, out var projector, out var eventBus, out var currentState);
        SeedRunSummaryState(currentState);
        var selectedCabinetId = Guid.NewGuid();
        projector.Scene = new RenderSceneDto(
            [],
            [],
            [
                new CabinetRenderDto(
                    selectedCabinetId,
                    Guid.NewGuid(),
                    new Rect2D(Point2D.Origin, Length.FromInches(36m), Length.FromInches(24m)),
                    "base-36",
                    "Base Cabinet 36\"",
                    CabinetRenderState.Normal,
                    [])
            ],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "layout.update", true, [], [], [])));
        shell.Canvas.SetSelectedCabinetIds([selectedCabinetId]);

        currentState.Clear();
        eventBus.Publish(new ProjectClosedEvent(Guid.NewGuid()));

        Assert.False(shell.PropertyInspector.HasSelection);
        Assert.Equal("Open a project to inspect properties.", shell.PropertyInspector.EmptyStateText);
        Assert.Equal("No editable properties", shell.PropertyInspector.EditabilityStatusDisplay);
        Assert.Equal("-", shell.PropertyInspector.NominalWidthDisplay);
        Assert.False(shell.RunSummary.HasSelection);
        Assert.False(shell.RunSummary.IsProjectOpen);
        Assert.Equal("No project open", shell.RunSummary.SourceLabel);
        Assert.Equal("Open a project to see the run summary.", shell.RunSummary.EmptyStateText);
        Assert.Equal("Project closed.", shell.StatusBar.StatusMessage);
    }

    [Fact]
    public async Task SaveCommand_WhenProjectServiceThrows_RoutesExceptionToStatusBar()
    {
        using var shell = CreateShellViewModel(out var projectService, out _, out _, out var eventBus, out _);
        var project = new ProjectSummaryDto(Guid.NewGuid(), "Shop A", "C:\\shop.cab", DateTimeOffset.UtcNow, "Rev 1", false);
        projectService.SeedCurrentProject(project);
        eventBus.Publish(new ProjectOpenedEvent(project));

        projectService.ThrowOnSave = true;

        await shell.SaveCommand.ExecuteAsync();

        Assert.StartsWith("Error:", shell.StatusBar.StatusMessage);
        Assert.False(shell.SaveCommand.IsExecuting);
    }

    private static ShellViewModel CreateShellViewModel(
        out RecordingProjectService projectService,
        out RecordingUndoRedoService undoRedoService,
        out RecordingSceneProjector projector,
        out ApplicationEventBus eventBus,
        out CurrentWorkingRevisionSource currentState)
    {
        projectService = new RecordingProjectService();
        undoRedoService = new RecordingUndoRedoService();
        eventBus = new ApplicationEventBus();
        var validationSummaryService = new RecordingValidationSummaryService();
        var stateStore = new InMemoryDesignStateStore();
        currentState = new CurrentWorkingRevisionSource(stateStore);
        var runSummaryService = new RunSummaryService(currentState, stateStore);

        projector = new RecordingSceneProjector();
        var runService = new RecordingRunService();
        var canvas = new EditorCanvasViewModel(
            runService,
            eventBus,
            projector,
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            new RecordingCanvasHost(),
            new NoOpInteractionService());

        var catalog = new CatalogPanelViewModel(new CatalogService());
        var propertyInspector = new PropertyInspectorViewModel(runService, eventBus);
        var runSummary = new RunSummaryPanelViewModel(runSummaryService, currentState, eventBus);
        var statusBar = new StatusBarViewModel(eventBus, validationSummaryService);
        var issuePanel = new IssuePanelViewModel(validationSummaryService, eventBus);

        return new ShellViewModel(projectService, undoRedoService, eventBus, canvas, catalog, propertyInspector, runSummary, issuePanel, statusBar, new StubDialogService());
    }

    private static void SeedRunSummaryState(CurrentWorkingRevisionSource currentState)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var project = new ProjectRecord(projectId, "Demo Project", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var room = new Room(RoomId.New(), revisionId, "Kitchen", Length.FromFeet(8));
        var wall = new Wall(
            new WallId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            room.Id,
            Point2D.Origin,
            new Point2D(96m, 0m),
            Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(new RunId(Guid.Parse("00000000-0000-0000-0000-000000000001")), wall.Id, Length.FromInches(96m));
        var firstCabinetId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondCabinetId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        run.AppendCabinet(new CabinetId(firstCabinetId), Length.FromInches(24m));
        run.AppendCabinet(new CabinetId(secondCabinetId), Length.FromInches(36m));
        var firstCabinet = new Cabinet(new CabinetId(firstCabinetId), revisionId, "base-24", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(24m), Length.FromInches(24m), Length.FromInches(34.5m));
        var secondCabinet = new Cabinet(new CabinetId(secondCabinetId), revisionId, "base-36", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(36m), Length.FromInches(24m), Length.FromInches(34.5m));

        var workingRevision = new WorkingRevision(revision, [room], [wall], [run], [firstCabinet, secondCabinet], []);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);
        currentState.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));
    }

    private sealed class RecordingProjectService : IProjectService
    {
        public ProjectSummaryDto? CurrentProject { get; private set; }

        public bool SaveCalled { get; private set; }

        public bool ThrowOnSave { get; set; }

        public Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), "Opened", filePath, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), name, string.Empty, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task SaveAsync(CancellationToken ct = default)
        {
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("Save failed.");
            }

            SaveCalled = true;
            if (CurrentProject is not null)
            {
                CurrentProject = CurrentProject with { HasUnsavedChanges = false };
            }

            return Task.CompletedTask;
        }

        public Task<RevisionDto> SaveRevisionAsync(string label, CancellationToken ct = default) => throw new NotImplementedException();

        public Task CloseAsync()
        {
            CurrentProject = null;
            return Task.CompletedTask;
        }

        public void SeedCurrentProject(ProjectSummaryDto project) => CurrentProject = project;
    }

    private sealed class RecordingUndoRedoService : IUndoRedoService
    {
        public bool CanUndoValue { get; set; }

        public bool CanRedoValue { get; set; }

        public bool CanUndo => CanUndoValue;

        public bool CanRedo => CanRedoValue;

        public CommandResultDto Undo() => new(Guid.NewGuid(), "undo", true, [], [], []);

        public CommandResultDto Redo() => new(Guid.NewGuid(), "redo", true, [], [], []);

        public void Clear()
        {
        }
    }

    private sealed class RecordingRunService : IRunService
    {
        public ResizeCabinetRequestDto? LastResizeRequest { get; private set; }

        public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> DeleteRunAsync(RunId runId) => throw new NotImplementedException();

        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request)
        {
            LastResizeRequest = request;
            return Task.FromResult(new CommandResultDto(Guid.NewGuid(), "resize_cabinet", true, [], [], []));
        }

        public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) => throw new NotImplementedException();

        public RunSummaryDto GetRunSummary(RunId runId) => throw new NotImplementedException();
    }

    private sealed class RecordingValidationSummaryService : IValidationSummaryService
    {
        public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() => [];

        public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) => [];

        public bool HasManufactureBlockers => false;
    }

    private sealed class RecordingSceneProjector : ISceneProjector
    {
        public RenderSceneDto Scene { get; set; } = new([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        public RenderSceneDto Project() => Scene;
    }

    private sealed class RecordingCanvasHost : IEditorCanvasHost
    {
        public object View => new();

        public bool IsCtrlHeld => false;

        public double CanvasWidth => 800.0;

        public double CanvasHeight => 600.0;

        public void UpdateScene(RenderSceneDto scene)
        {
        }

        public void UpdateViewport(ViewportTransform viewport)
        {
        }

        public void SetMouseDownHandler(Action<double, double> handler)
        {
        }

        public void SetMouseMoveHandler(Action<double, double> handler)
        {
        }

        public void SetMouseUpHandler(Action<double, double> handler)
        {
        }

        public void SetMouseWheelHandler(Action<double, double, double> handler)
        {
        }

        public void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd)
        {
        }
    }

    private sealed class TestEditorCanvasSession : IEditorCanvasSession
    {
        public EditorMode CurrentMode => EditorMode.Idle;

        public IReadOnlyList<Guid> SelectedCabinetIds { get; private set; } = [];

        public Guid? HoveredCabinetId => null;

        public ViewportTransform Viewport => ViewportTransform.Default;

        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds)
        {
            SelectedCabinetIds = cabinetIds.ToArray();
        }

        public void SetHoveredCabinetId(Guid? cabinetId)
        {
        }

        public void ZoomAt(double screenX, double screenY, double scaleFactor)
        {
        }

        public void PanBy(double dx, double dy)
        {
        }

        public void BeginPan() { }

        public void EndPan() { }

        public void ResetViewport() { }

        public void FitViewport(CabinetDesigner.Domain.Geometry.Rect2D contentBounds, double canvasWidth, double canvasHeight) { }
    }

    private sealed class NoOpInteractionService : IEditorInteractionService
    {
        public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY) { }

        public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY) { }

        public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY) { }

        public DragPreviewResult OnDragMoved(double screenX, double screenY) =>
            new(false, null, "No active drag.");

        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default) =>
            Task.FromResult(DragCommitResult.Failed("No active drag."));

        public void OnDragAborted() { }
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? ShowOpenFileDialog(string title, string filter) => null;

        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName) => null;

        public bool ShowYesNoDialog(string title, string message) => false;
    }
}
