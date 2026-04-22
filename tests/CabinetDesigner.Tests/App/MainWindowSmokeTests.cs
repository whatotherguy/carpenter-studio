using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
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
using CabinetDesigner.Tests.Presentation;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using Xunit;
using Controls = System.Windows.Controls;

namespace CabinetDesigner.Tests.App;

[CollectionDefinition("MainWindowSmoke", DisableParallelization = true)]
public sealed class MainWindowSmokeCollection : ICollectionFixture<MainWindowSmokeTests.MainWindowSmokeThreadFixture>
{
}

[Collection("MainWindowSmoke")]
public sealed class MainWindowSmokeTests
{
    private readonly MainWindowSmokeThreadFixture _fixture;

    public MainWindowSmokeTests(MainWindowSmokeThreadFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void MainWindow_Instantiates_WithoutBindingExceptions()
    {
        RunInSta(() =>
        {
            var createdApplication = EnsureApplicationResourcesLoaded();

            using var shell = CreateShellViewModel();
            using var listener = new BindingTraceCollector();
            var window = new CabinetDesigner.App.MainWindow(shell);

            try
            {
                listener.Attach();
                new WindowInteropHelper(window).EnsureHandle();
                window.ApplyTemplate();
                window.Measure(new Size(1600, 1000));
                window.Arrange(new Rect(0, 0, 1600, 1000));
                window.UpdateLayout();

                Assert.Equal(0, listener.SystemErrorCount);
            }
            finally
            {
                window.Close();
                listener.Detach();
                if (createdApplication)
                {
                    ShutdownApplication();
                }
            }
        });
    }

    [Fact]
    public void MainWindow_AllExpectedPanels_Present()
    {
        RunInSta(() =>
        {
            var createdApplication = EnsureApplicationResourcesLoaded();

            using var shell = CreateShellViewModel();
            var window = new CabinetDesigner.App.MainWindow(shell);

            try
            {
                new WindowInteropHelper(window).EnsureHandle();
                window.ApplyTemplate();
                window.Measure(new Size(1600, 1000));
                window.Arrange(new Rect(0, 0, 1600, 1000));
                window.UpdateLayout();

                Assert.Single(FindDescendants<CabinetDesigner.Presentation.Views.ShellToolbarView>(window));
                Assert.Single(FindDescendants<CabinetDesigner.Presentation.Views.CatalogPanelView>(window));
                Assert.Single(FindDescendants<CabinetDesigner.Presentation.Views.PropertyInspectorView>(window));
                Assert.Single(FindDescendants<CabinetDesigner.Presentation.Views.RunSummaryPanelView>(window));
                Assert.Single(FindDescendants<CabinetDesigner.Presentation.Views.IssuePanelView>(window));
                Assert.Single(FindDescendants<CabinetDesigner.Presentation.Views.StatusBarView>(window));
            }
            finally
            {
                window.Close();
                if (createdApplication)
                {
                    ShutdownApplication();
                }
            }
        });
    }

    private void RunInSta(Action action)
    {
        var failure = _fixture.Invoke(action);
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException($"STA smoke test failed: {failure}");
        }
    }

    private static bool EnsureApplicationResourcesLoaded()
    {
        if (System.Windows.Application.Current is null)
        {
            var app = new CabinetDesigner.App.App();
            app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            return true;
        }

        return false;
    }

    private static void ShutdownApplication()
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        if (!app.Dispatcher.HasShutdownStarted && !app.Dispatcher.HasShutdownFinished)
        {
            app.Dispatcher.Invoke(() => app.Shutdown());
        }

        if (!app.Dispatcher.HasShutdownStarted && !app.Dispatcher.HasShutdownFinished)
        {
            app.Dispatcher.InvokeShutdown();
        }
    }

    private static IReadOnlyList<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var results = new List<T>();
        var visited = new HashSet<DependencyObject>();
        Walk(root, results, visited);
        return results;
    }

    private static void Walk<T>(DependencyObject current, List<T> results, HashSet<DependencyObject> visited)
        where T : DependencyObject
    {
        if (current is System.Windows.Controls.DefinitionBase)
        {
            return;
        }

        if (!visited.Add(current))
        {
            return;
        }

        if (current is T typed)
        {
            results.Add(typed);
        }

        if (current is Visual or Visual3D)
        {
            var visualCount = VisualTreeHelper.GetChildrenCount(current);
            for (var index = 0; index < visualCount; index++)
            {
                if (VisualTreeHelper.GetChild(current, index) is DependencyObject visualChild)
                {
                    Walk(visualChild, results, visited);
                }
            }
        }

        foreach (var logicalChild in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
        {
            Walk(logicalChild, results, visited);
        }
    }

    private static ShellViewModel CreateShellViewModel()
    {
        var projectService = new RecordingProjectService();
        var undoRedoService = new RecordingUndoRedoService();
        var eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        var validationSummaryService = new RecordingValidationSummaryService();
        var stateStore = new InMemoryDesignStateStore();
        var currentState = new CurrentWorkingRevisionSource(stateStore);
        var runSummaryService = new RunSummaryService(currentState, stateStore);

        SeedRunSummaryState(currentState);

        var canvas = new EditorCanvasViewModel(
            new RecordingRunService(),
            eventBus,
            new RecordingSceneProjector(),
            new NoOpRoomService(),
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            new RecordingCanvasHost(),
            new NoOpInteractionService(),
            logger);

        var catalog = new CatalogPanelViewModel(new CatalogService());
        var propertyInspector = new PropertyInspectorViewModel(new RecordingRunService(), eventBus, logger);
        var runSummary = new RunSummaryPanelViewModel(runSummaryService, currentState, eventBus);
        var statusBar = new StatusBarViewModel(eventBus, validationSummaryService);
        var issuePanel = new IssuePanelViewModel(validationSummaryService, eventBus);

        var shell = new ShellViewModel(
            projectService,
            new NoOpRoomService(),
            undoRedoService,
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

    public sealed class MainWindowSmokeThreadFixture : IDisposable
    {
        private readonly BlockingCollection<(Action Action, TaskCompletionSource<Exception?> Completion)> _queue = new();
        private readonly Thread _thread;

        public MainWindowSmokeThreadFixture()
        {
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "MainWindowSmokeTests STA"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public Exception? Invoke(Action action)
        {
            var completion = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Add((action, completion));
            return completion.Task.GetAwaiter().GetResult();
        }

        private void Run()
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                try
                {
                    item.Action();
                    item.Completion.SetResult(null);
                }
                catch (Exception ex)
                {
                    item.Completion.SetResult(ex);
                }
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            if (!_thread.Join(TimeSpan.FromSeconds(10)))
            {
                _thread.Interrupt();
                _thread.Join(TimeSpan.FromSeconds(5));
            }
        }
    }

    private sealed class BindingTraceCollector : TraceListener, IDisposable
    {
        private readonly object _syncRoot = new();
        private int _systemErrorCount;
        private bool _attached;

        public int SystemErrorCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _systemErrorCount;
                }
            }
        }

        public void Attach()
        {
            if (_attached)
            {
                return;
            }

            PresentationTraceSources.DataBindingSource.Listeners.Add(this);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            _attached = true;
        }

        public void Detach()
        {
            if (!_attached)
            {
                return;
            }

            PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
            _attached = false;
        }

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            if (eventType is TraceEventType.Error or TraceEventType.Critical)
            {
                lock (_syncRoot)
                {
                    _systemErrorCount++;
                }
            }
        }

        public new void Dispose() => Detach();
    }

    private sealed class RecordingProjectService : IProjectService
    {
        public ProjectSummaryDto? CurrentProject { get; private set; }

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
        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request) => throw new NotImplementedException();
        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();
        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) => throw new NotImplementedException();
        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request) =>
            Task.FromResult(new CommandResultDto(Guid.NewGuid(), "resize_cabinet", true, [], [], []));
        public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) => throw new NotImplementedException();
        public RunSummaryDto GetRunSummary(RunId runId) => throw new NotImplementedException();
    }

    private sealed class NoOpRoomService : IRoomService
    {
        public Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct) =>
            Task.FromException<Room>(new InvalidOperationException("Not used in this test."));

        public Task<Wall> AddWallAsync(RoomId roomId, Point2D start, Point2D end, CabinetDesigner.Domain.Geometry.Thickness thickness, CancellationToken ct) =>
            Task.FromException<Wall>(new InvalidOperationException("Not used in this test."));

        public Task RemoveWallAsync(WallId wallId, CancellationToken ct) => Task.CompletedTask;

        public Task RenameRoomAsync(RoomId roomId, string newName, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Room>>([]);
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
        public Guid? ActiveRoomId => null;
        public ViewportTransform Viewport => ViewportTransform.Default;
        public CabinetDesigner.Editor.Snap.SnapSettings SnapSettings => CabinetDesigner.Editor.Snap.SnapSettings.Default;
        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds)
        {
            SelectedCabinetIds = cabinetIds.ToArray();
        }
        public void SetHoveredCabinetId(Guid? cabinetId)
        {
        }
        public void SetActiveRoom(Guid? roomId)
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
        public void FitViewport(ViewportBounds contentBounds, double canvasWidth, double canvasHeight)
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
        public string? ShowFolderPicker(string title) => null;
        public bool ShowYesNoDialog(string title, string message) => false;
    }

    private sealed class NoOpCutListExportWorkflowService : ICutListExportWorkflowService
    {
        public CutListWorkflowResult BuildCurrentProjectCutList() => new(false, null, null, "Not used in test.");
    }
}
