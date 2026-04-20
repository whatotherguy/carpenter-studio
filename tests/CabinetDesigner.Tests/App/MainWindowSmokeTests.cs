using System.Windows;
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
using Controls = System.Windows.Controls;

namespace CabinetDesigner.Tests.App;

/// <summary>
/// Manual checklist:
/// 1. Launch the desktop app and verify the toolbar, catalog, canvas, inspector tabs, and status bar are visible on first paint.
/// 2. Resize the window narrower and wider to confirm the split layout remains usable for cabinet-design work.
/// 3. Create or open a project and verify live status/issues flow into the mounted panels.
/// </summary>
public sealed class MainWindowSmokeTests
{
    [Fact]
    public void MainWindow_StartsWithMountedRegionsBoundToShellViewModel()
    {
        RunInSta(() =>
        {
            var createdApplication = EnsureApplicationResourcesLoaded();

            using var shell = CreateShellViewModel();
            var window = new CabinetDesigner.App.MainWindow(shell);
            try
            {
                window.Show();
                window.ApplyTemplate();
                window.UpdateLayout();

                var toolbar = Assert.IsType<CabinetDesigner.Presentation.Views.ShellToolbarView>(window.FindName("ToolbarRegion"));
                var catalog = Assert.IsType<CabinetDesigner.Presentation.Views.CatalogPanelView>(window.FindName("CatalogRegion"));
                var canvas = Assert.IsType<Controls.ContentControl>(window.FindName("CanvasRegion"));
                var tabs = Assert.IsType<Controls.TabControl>(window.FindName("InspectorTabs"));
                var propertyInspector = Assert.IsType<CabinetDesigner.Presentation.Views.PropertyInspectorView>(window.FindName("PropertyInspectorRegion"));
                var runSummary = Assert.IsType<CabinetDesigner.Presentation.Views.RunSummaryPanelView>(window.FindName("RunSummaryRegion"));
                var issuePanel = Assert.IsType<CabinetDesigner.Presentation.Views.IssuePanelView>(window.FindName("IssuePanelRegion"));
                var statusBar = Assert.IsType<CabinetDesigner.Presentation.Views.StatusBarView>(window.FindName("StatusBarRegion"));

                Assert.Same(shell, window.DataContext);
                Assert.Same(shell, toolbar.DataContext);
                Assert.Same(shell.Catalog, catalog.DataContext);
                Assert.Same(shell, canvas.DataContext);
                Assert.NotNull(canvas.Content);
                Assert.Equal(3, tabs.Items.Count);
                Assert.Same(shell.PropertyInspector, propertyInspector.DataContext);
                Assert.Same(shell.RunSummary, runSummary.DataContext);
                Assert.Same(shell.IssuePanel, issuePanel.DataContext);
                Assert.Same(shell.StatusBar, statusBar.DataContext);
            }
            finally
            {
                window.Close();
                if (createdApplication && System.Windows.Application.Current is CabinetDesigner.App.App app)
                {
                    app.Shutdown();
                }
            }
        });
    }

    private static bool EnsureApplicationResourcesLoaded()
    {
        if (System.Windows.Application.Current is null)
        {
            var app = new CabinetDesigner.App.App();
            app.InitializeComponent();
            return true;
        }

        return false;
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException($"STA smoke test failed: {failure}");
        }
    }

    private static ShellViewModel CreateShellViewModel()
    {
        var projectService = new RecordingProjectService();
        var undoRedoService = new RecordingUndoRedoService();
        var eventBus = new ApplicationEventBus();
        var validationSummaryService = new RecordingValidationSummaryService();
        var stateStore = new InMemoryDesignStateStore();
        var currentState = new CurrentWorkingRevisionSource(stateStore);
        var runSummaryService = new RunSummaryService(currentState, stateStore);

        SeedRunSummaryState(currentState);

        var canvas = new EditorCanvasViewModel(
            new RecordingRunService(),
            eventBus,
            new RecordingSceneProjector(),
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            new RecordingCanvasHost(),
            new NoOpInteractionService());

        var catalog = new CatalogPanelViewModel(new CatalogService());
        var propertyInspector = new PropertyInspectorViewModel(new RecordingRunService(), eventBus);
        var runSummary = new RunSummaryPanelViewModel(runSummaryService, currentState, eventBus);
        var statusBar = new StatusBarViewModel(eventBus, validationSummaryService);
        var issuePanel = new IssuePanelViewModel(validationSummaryService, eventBus);

        var shell = new ShellViewModel(
            projectService,
            undoRedoService,
            eventBus,
            canvas,
            catalog,
            propertyInspector,
            runSummary,
            issuePanel,
            statusBar,
            new StubDialogService());

        var project = new ProjectSummaryDto(Guid.NewGuid(), "Smoke Project", "C:\\smoke.db", DateTimeOffset.UtcNow, "Rev 1", false);
        projectService.SeedCurrentProject(project);
        eventBus.Publish(new ProjectOpenedEvent(project));

        return shell;
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
            WallId.New(),
            room.Id,
            Point2D.Origin,
            new Point2D(96m, 0m),
            CabinetDesigner.Domain.Geometry.Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        var cabinetId = CabinetId.New();
        run.AppendCabinet(cabinetId, Length.FromInches(30m));
        var cabinet = new Cabinet(cabinetId, revisionId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(30m), Length.FromInches(24m), Length.FromInches(34.5m));

        var workingRevision = new WorkingRevision(revision, [room], [wall], [run], [cabinet], []);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);
        currentState.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));
    }

    private sealed class RecordingProjectService : IProjectService
    {
        public ProjectSummaryDto? CurrentProject { get; private set; }

        public Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), "Opened", filePath, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), name, string.Empty, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

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
        public bool CanUndo => false;

        public bool CanRedo => false;

        public CommandResultDto Undo() => new(Guid.NewGuid(), "undo", true, [], [], []);

        public CommandResultDto Redo() => new(Guid.NewGuid(), "redo", true, [], [], []);

        public void Clear()
        {
        }
    }

    private sealed class RecordingRunService : IRunService
    {
        public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> DeleteRunAsync(RunId runId) => throw new NotImplementedException();

        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request) =>
            Task.FromResult(new CommandResultDto(Guid.NewGuid(), "add_cabinet", true, [], [], []));

        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) =>
            Task.FromResult(new CommandResultDto(Guid.NewGuid(), "move_cabinet", true, [], [], []));

        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request) =>
            Task.FromResult(new CommandResultDto(Guid.NewGuid(), "resize_cabinet", true, [], [], []));

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
        public RenderSceneDto? Project() =>
            new(
                [],
                [],
                [],
                null,
                new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
    }

    private sealed class RecordingCanvasHost : IEditorCanvasHost
    {
        public object View => new Controls.Border();

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

        public void BeginPan()
        {
        }

        public void EndPan()
        {
        }

        public void ResetViewport()
        {
        }

        public void FitViewport(Rect2D contentBounds, double canvasWidth, double canvasHeight)
        {
        }
    }

    private sealed class NoOpInteractionService : IEditorInteractionService
    {
        public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY)
        {
        }

        public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY)
        {
        }

        public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY)
        {
        }

        public DragPreviewResult OnDragMoved(double screenX, double screenY) => new(false, null, "No active drag.");

        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default) =>
            Task.FromResult(DragCommitResult.Failed("No active drag."));

        public void OnDragAborted()
        {
        }
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? ShowOpenFileDialog(string title, string filter) => null;

        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName) => null;

        public bool ShowYesNoDialog(string title, string message) => false;
    }
}
