using System.Threading;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.State;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation;
using CabinetDesigner.Presentation.Commands;
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
        Assert.Equal("60\"", shell.RunSummary.TotalWidthDisplay);
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
        var selectedCabinetId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(), "Test Project", "C:\\test.cab", DateTimeOffset.UtcNow, "Rev 1", false)));
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
        Assert.Equal("60\"", shell.RunSummary.TotalWidthDisplay);
        Assert.Equal(2, shell.RunSummary.Slots.Count);
        Assert.False(shell.RunSummary.Slots[0].IsSelected);
        Assert.True(shell.RunSummary.Slots[1].IsSelected);
    }

    [Fact]
    public async Task AsyncRelayCommand_WhenDelegateThrows_RoutesExceptionToStatusBar()
    {
        using var shell = CreateShellViewModel(out _, out _, out _, out var eventBus, out _);

        var exceptionRouted = false;
        shell.StatusBar.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StatusBarViewModel.StatusMessage))
                exceptionRouted = true;
        };

        var logger = new CapturingAppLogger();
        var thrower = new AsyncRelayCommand(
            () => throw new InvalidOperationException("test error"),
            "test.command",
            logger,
            eventBus);
        await thrower.ExecuteAsync();

        Assert.True(exceptionRouted);
        Assert.StartsWith("Error in test.command: test error (ref: ", shell.StatusBar.StatusMessage);
    }

    [Fact]
    public void ShellViewModel_ExposesMountedPanelsAndCanvasSurface()
    {
        using var shell = CreateShellViewModel(out _, out _, out _, out _, out _);

        Assert.NotNull(shell.Catalog);
        Assert.NotNull(shell.PropertyInspector);
        Assert.NotNull(shell.RunSummary);
        Assert.NotNull(shell.IssuePanel);
        Assert.NotNull(shell.StatusBar);
        Assert.NotNull(shell.Canvas);
        Assert.NotNull(shell.CanvasView);
        Assert.Equal(shell.Canvas.StatusMessage, shell.CurrentStatusText);
        Assert.Equal(shell.Canvas.CurrentMode, shell.CanvasCurrentMode);
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
        Assert.Equal("No project is open, so there are no cabinet properties to inspect yet. Open or create a project to populate this panel.", shell.PropertyInspector.EmptyStateText);
        Assert.Equal("No editable properties", shell.PropertyInspector.EditabilityStatusDisplay);
        Assert.Equal("-", shell.PropertyInspector.NominalWidthDisplay);
        Assert.False(shell.RunSummary.HasSelection);
        Assert.False(shell.RunSummary.IsProjectOpen);
        Assert.Equal("No project open", shell.RunSummary.SourceLabel);
        Assert.Equal("No project is open, so there is no run summary yet. Open or create a project to populate this panel.", shell.RunSummary.EmptyStateText);
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

        Assert.StartsWith("Error in project.save: Save failed. (ref: ", shell.StatusBar.StatusMessage);
        Assert.False(shell.SaveCommand.IsExecuting);
    }

    [Fact]
    public async Task CloseProjectCommand_WithUnsavedChangesAndDialogYes_SavesThenCloses()
    {
        var dialogService = new StubDialogService { YesNoResult = true };
        using var shell = CreateShellViewModel(dialogService, out var projectService, out _, out _, out var eventBus, out _);
        var project = new ProjectSummaryDto(Guid.NewGuid(), "Shop A", "C:\\shop.cab", DateTimeOffset.UtcNow, "Rev 1", true);
        projectService.SeedCurrentProject(project);
        eventBus.Publish(new ProjectOpenedEvent(project));

        await shell.CloseProjectCommand.ExecuteAsync();

        Assert.True(projectService.SaveCalled);
        Assert.True(projectService.CloseCalled);
        Assert.Null(shell.ActiveProject);
    }

    [Fact]
    public async Task CloseProjectCommand_WithUnsavedChangesAndDialogNo_ClosesWithoutSaving()
    {
        var dialogService = new StubDialogService { YesNoResult = false };
        using var shell = CreateShellViewModel(dialogService, out var projectService, out _, out _, out var eventBus, out _);
        var project = new ProjectSummaryDto(Guid.NewGuid(), "Shop A", "C:\\shop.cab", DateTimeOffset.UtcNow, "Rev 1", true);
        projectService.SeedCurrentProject(project);
        eventBus.Publish(new ProjectOpenedEvent(project));

        await shell.CloseProjectCommand.ExecuteAsync();

        Assert.False(projectService.SaveCalled);
        Assert.True(projectService.CloseCalled);
        Assert.Null(shell.ActiveProject);
    }

    [Fact]
    public async Task EventBusHandlers_DispatchRefreshCommandStatesToUIThread()
    {
        using var shell = CreateShellViewModel(out _, out var undoRedoService, out _, out var eventBus, out var currentState);
        SeedRunSummaryState(currentState);
        undoRedoService.CanUndoValue = true;
        undoRedoService.CanRedoValue = true;

        var exceptionThrown = false;

        // Simulate publishing UndoAppliedEvent from a background thread (which would previously throw)
        await Task.Run(() =>
        {
            try
            {
                eventBus.Publish(new UndoAppliedEvent(new CommandResultDto(Guid.NewGuid(), "undo", true, [], [], [])));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during background event: {ex}");
                exceptionThrown = true;
            }
        });

        // Similarly test RedoAppliedEvent
        await Task.Run(() =>
        {
            try
            {
                eventBus.Publish(new RedoAppliedEvent(new CommandResultDto(Guid.NewGuid(), "redo", true, [], [], [])));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during background event: {ex}");
                exceptionThrown = true;
            }
        });

        // If the dispatcher wrapping is not in place, this would throw InvalidOperationException
        // due to WPF binding refresh happening off the UI thread.
        // With the fix, no exception should be thrown.
        Assert.False(exceptionThrown);
    }

    [Fact]
    public async Task ProjectOpenedEvent_FromBackgroundThread_DoesNotThrow()
    {
        using var shell = CreateShellViewModel(out _, out _, out _, out var eventBus, out _);

        var exceptionThrown = false;

        // Simulate publishing ProjectOpenedEvent from a background thread
        await Task.Run(() =>
        {
            try
            {
                eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
                    Guid.NewGuid(),
                    "Background Project",
                    "C:\\background.cab",
                    DateTimeOffset.UtcNow,
                    "Rev 1",
                    false)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during background event: {ex}");
                exceptionThrown = true;
            }
        });

        Assert.False(exceptionThrown);
    }

    [Fact]
    public async Task DesignChangedEvent_FromBackgroundThread_DoesNotThrow()
    {
        using var shell = CreateShellViewModel(out _, out _, out _, out var eventBus, out _);

        var exceptionThrown = false;

        // Simulate publishing DesignChangedEvent from a background thread
        await Task.Run(() =>
        {
            try
            {
                eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "design.update", true, [], [], [])));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during background event: {ex}");
                exceptionThrown = true;
            }
        });

        Assert.False(exceptionThrown);
    }

    [Fact]
    public async Task ProjectClosedEvent_FromBackgroundThread_DoesNotThrow()
    {
        using var shell = CreateShellViewModel(out _, out _, out _, out var eventBus, out _);

        var exceptionThrown = false;

        // Simulate publishing ProjectClosedEvent from a background thread
        await Task.Run(() =>
        {
            try
            {
                eventBus.Publish(new ProjectClosedEvent(Guid.NewGuid()));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during background event: {ex}");
                exceptionThrown = true;
            }
        });

        Assert.False(exceptionThrown);
    }

    [Fact]
    public async Task OnCatalogItemActivated_WhenAddCabinetThrows_CatchesExceptionAndDisplaysErrorMessage()
    {
        // Setup: Create shell with run service that will throw
        var projectService = new RecordingProjectService();
        var eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        var throwingRunService = new ThrowingRunService();
        var currentState = new CurrentWorkingRevisionSource(new InMemoryDesignStateStore());
        var projector = new RecordingSceneProjector();

        var canvas = new EditorCanvasViewModel(
            throwingRunService,
            eventBus,
            projector,
            new NoOpRoomService(),
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            new RecordingCanvasHost(),
            new NoOpInteractionService(),
            logger);

        var catalog = new CatalogPanelViewModel(new CatalogService());
        var propertyInspector = new PropertyInspectorViewModel(throwingRunService, eventBus, logger);
        var validationService = new RecordingValidationSummaryService();
        var stateStore = new InMemoryDesignStateStore();
        var runSummary = new RunSummaryPanelViewModel(new RunSummaryService(currentState, stateStore), currentState, eventBus);
        var statusBar = new StatusBarViewModel(eventBus, validationService);
        var issuePanel = new IssuePanelViewModel(validationService, eventBus);

        using var shell = new ShellViewModel(
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
            new StubDialogService(),
            new NoOpCutListExportWorkflowService());

        // Setup: Open a project with a run so ResolveTargetRunId doesn't early-exit
        var project = new ProjectSummaryDto(Guid.NewGuid(), "Test Project", "C:\\test.cab", DateTimeOffset.UtcNow, "Rev 1", false);
        projectService.SeedCurrentProject(project);
        eventBus.Publish(new ProjectOpenedEvent(project));

        SeedRunSummaryState(currentState);

        // Create a run for the test - this is needed so ResolveTargetRunId doesn't return null
        var runId = Guid.NewGuid();
        projector.Scene = new RenderSceneDto(
            [],
            [
                new RunRenderDto(
                    runId,
                    new LineSegment2D(Point2D.Origin, new Point2D(96m, 0m)),
                    new Rect2D(Point2D.Origin, Length.FromInches(96m), Length.FromInches(24m)),
                    true)
            ],
            [],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "layout.update", true, [], [], [])));

        // Act: Create a catalog item and trigger the ItemActivated event through ActivateItem
        var catalogItem = new CatalogItemViewModel(
            "base-36",
            "Base Cabinet 36\"",
            "Base",
            "Frameless",
            "Base cabinet 36 inches wide",
            "36\" W x 24\" D x 34.5\" H",
            "1 opening",
            "36\"",
            36m);

        // Call ActivateItem which raises the ItemActivated event that OnCatalogItemActivated subscribes to
        catalog.ActivateItem(catalogItem);

        // Wait for the async void handler to complete
        await Task.Delay(200);

        // Assert: Error was surfaced to the status bar with a stable correlation reference.
        Assert.StartsWith("Error in project.catalog.add: Simulated error when adding cabinet to run (ref: ", statusBar.StatusMessage);
    }

    private static ShellViewModel CreateShellViewModel(
        out RecordingProjectService projectService,
        out RecordingUndoRedoService undoRedoService,
        out RecordingSceneProjector projector,
        out ApplicationEventBus eventBus,
        out CurrentWorkingRevisionSource currentState) =>
        CreateShellViewModel(new StubDialogService(), out projectService, out undoRedoService, out projector, out eventBus, out currentState);

    private static ShellViewModel CreateShellViewModel(
        IDialogService dialogService,
        out RecordingProjectService projectService,
        out RecordingUndoRedoService undoRedoService,
        out RecordingSceneProjector projector,
        out ApplicationEventBus eventBus,
        out CurrentWorkingRevisionSource currentState)
    {
        projectService = new RecordingProjectService();
        undoRedoService = new RecordingUndoRedoService();
        eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        projectService.EventBus = eventBus;
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
            new NoOpRoomService(),
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            new RecordingCanvasHost(),
            new NoOpInteractionService(),
            logger);

        var catalog = new CatalogPanelViewModel(new CatalogService());
        var propertyInspector = new PropertyInspectorViewModel(runService, eventBus, logger);
        var runSummary = new RunSummaryPanelViewModel(runSummaryService, currentState, eventBus);
        var statusBar = new StatusBarViewModel(eventBus, validationSummaryService);
        var issuePanel = new IssuePanelViewModel(validationSummaryService, eventBus);

        return new ShellViewModel(projectService, new NoOpRoomService(), undoRedoService, eventBus, logger, canvas, catalog, propertyInspector, runSummary, issuePanel, statusBar, dialogService, new NoOpCutListExportWorkflowService());
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

        public IApplicationEventBus? EventBus { get; set; }

        public bool SaveCalled { get; private set; }

        public bool CloseCalled { get; private set; }

        public bool ThrowOnSave { get; set; }

        public Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), "Opened", filePath, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task<ProjectSummaryDto> OpenProjectAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), "Opened", string.Empty, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(CurrentProject = new ProjectSummaryDto(Guid.NewGuid(), name, string.Empty, DateTimeOffset.UtcNow, "Rev 1", false));

        public Task<IReadOnlyList<ProjectSummaryDto>> ListProjectsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectSummaryDto>>(CurrentProject is null ? [] : [CurrentProject]);

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
            CloseCalled = true;
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

    private sealed class NoOpRoomService : IRoomService
    {
        public Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct) =>
            Task.FromException<Room>(new InvalidOperationException("Not used in this test."));

        public Task<Wall> AddWallAsync(RoomId roomId, Point2D start, Point2D end, Thickness thickness, CancellationToken ct) =>
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
        public RenderSceneDto Scene { get; set; } = new([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        public RenderSceneDto? Project() => Scene;
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

        public DragPreviewResult OnDragMoved(double screenX, double screenY) =>
            new(false, null, "No active drag.");

        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default) =>
            Task.FromResult(DragCommitResult.Failed("No active drag."));

        public void OnDragAborted() { }
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? OpenFilePath { get; set; }

        public bool YesNoResult { get; set; }

        public string? ShowOpenFileDialog(string title, string filter) => OpenFilePath;

        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName) => null;

        public string? ShowFolderPicker(string title) => null;

        public bool ShowYesNoDialog(string title, string message) => YesNoResult;
    }

    private sealed class NoOpCutListExportWorkflowService : ICutListExportWorkflowService
    {
        public CutListWorkflowResult BuildCurrentProjectCutList() => new(false, null, null, "Not used in test.");
    }

    private sealed class ThrowingRunService : IRunService
    {
        public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> DeleteRunAsync(RunId runId) => throw new NotImplementedException();

        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request) =>
            throw new InvalidOperationException("Simulated error when adding cabinet to run");

        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) => throw new NotImplementedException();

        public RunSummaryDto GetRunSummary(RunId runId) => throw new NotImplementedException();
    }
}
