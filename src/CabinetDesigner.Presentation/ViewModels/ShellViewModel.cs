using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Presentation;
using CabinetDesigner.Presentation.Commands;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IProjectService _projectService;
    private readonly IRoomService _roomService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly IApplicationEventBus _eventBus;
    private readonly IAppLogger _logger;
    private readonly IDialogService _dialogService;
    private readonly ICutListExportWorkflowService _cutListExportWorkflowService;
    private readonly ProjectStartupViewModel _projectStartup;
    private readonly RoomsPanelViewModel _roomsPanel;
    private ProjectSummaryDto? _activeProject;
    private ShellMode _mode = ShellMode.Startup;
    private string _pendingProjectName = "New Project";
    private string _pendingProjectFilePath = string.Empty;

    public ShellViewModel(
        IProjectService projectService,
        IUndoRedoService undoRedoService,
        IApplicationEventBus eventBus,
        IAppLogger logger,
        EditorCanvasViewModel canvas,
        CatalogPanelViewModel catalog,
        PropertyInspectorViewModel propertyInspector,
        RunSummaryPanelViewModel runSummary,
        IssuePanelViewModel issuePanel,
        StatusBarViewModel statusBar,
        IDialogService dialogService,
        ICutListExportWorkflowService cutListExportWorkflowService)
        : this(projectService, null, undoRedoService, eventBus, logger, canvas, catalog, propertyInspector, runSummary, issuePanel, statusBar, dialogService, cutListExportWorkflowService)
    {
    }

    public ShellViewModel(
        IProjectService projectService,
        IRoomService? roomService,
        IUndoRedoService undoRedoService,
        IApplicationEventBus eventBus,
        IAppLogger logger,
        EditorCanvasViewModel canvas,
        CatalogPanelViewModel catalog,
        PropertyInspectorViewModel propertyInspector,
        RunSummaryPanelViewModel runSummary,
        IssuePanelViewModel issuePanel,
        StatusBarViewModel statusBar,
        IDialogService dialogService,
        ICutListExportWorkflowService cutListExportWorkflowService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _roomService = roomService ?? new NoOpRoomService();
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _cutListExportWorkflowService = cutListExportWorkflowService ?? throw new ArgumentNullException(nameof(cutListExportWorkflowService));
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        PropertyInspector = propertyInspector ?? throw new ArgumentNullException(nameof(propertyInspector));
        RunSummary = runSummary ?? throw new ArgumentNullException(nameof(runSummary));
        IssuePanel = issuePanel ?? throw new ArgumentNullException(nameof(issuePanel));
        StatusBar = statusBar ?? throw new ArgumentNullException(nameof(statusBar));
        _projectStartup = new ProjectStartupViewModel(_projectService, _eventBus, _logger);
        _roomsPanel = new RoomsPanelViewModel(_roomService, _eventBus, _logger, Canvas.SetActiveRoom);
        Canvas.PropertyChanged += OnCanvasPropertyChanged;
        Catalog.ItemActivated += OnCatalogItemActivated;
        IssuePanel.SetSelectionCallback(SelectEntities);

        NewProjectCommand = new AsyncRelayCommand(CreateProjectAsync, "project.create", _logger, _eventBus, () => !string.IsNullOrWhiteSpace(PendingProjectName));
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync, "project.open", _logger, _eventBus);
        SaveCommand = new AsyncRelayCommand(SaveAsync, "project.save", _logger, _eventBus, () => HasActiveProject);
        CloseProjectCommand = new AsyncRelayCommand(CloseProjectAsync, "project.close", _logger, _eventBus, () => HasActiveProject);
        ExportCutListCommand = new AsyncRelayCommand(ExportCutListAsync, "project.export.cutlist", _logger, _eventBus, () => HasActiveProject);
        PreviewHtmlCommand = new AsyncRelayCommand(PreviewHtmlAsync, "project.export.cutlist.preview-html", _logger, _eventBus, () => HasActiveProject);
        ShowAlphaLimitationsCommand = new RelayCommand(ShowAlphaLimitations);
        UndoCommand = new RelayCommand(() => _ = _undoRedoService.Undo(), () => _undoRedoService.CanUndo);
        RedoCommand = new RelayCommand(() => _ = _undoRedoService.Redo(), () => _undoRedoService.CanRedo);

        NewProjectCommand.PropertyChanged += OnCommandPropertyChanged;
        OpenProjectCommand.PropertyChanged += OnCommandPropertyChanged;
        SaveCommand.PropertyChanged += OnCommandPropertyChanged;
        CloseProjectCommand.PropertyChanged += OnCommandPropertyChanged;
        ExportCutListCommand.PropertyChanged += OnCommandPropertyChanged;
        PreviewHtmlCommand.PropertyChanged += OnCommandPropertyChanged;

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Subscribe<ActiveRoomChangedEvent>(OnActiveRoomChanged);
        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Subscribe<UndoAppliedEvent>(OnUndoRedoApplied);
        _eventBus.Subscribe<RedoAppliedEvent>(OnUndoRedoApplied);

        _ = _projectStartup.InitializeAsync();
        _ = _roomsPanel.InitializeAsync();
        SetActiveProject(_projectService.CurrentProject);
        StatusBar.SetStatusMessage(Canvas.StatusMessage);
        RefreshSelectionDrivenPanels();
    }

    public EditorCanvasViewModel Canvas { get; }

    public CatalogPanelViewModel Catalog { get; }

    public PropertyInspectorViewModel PropertyInspector { get; }

    public RunSummaryPanelViewModel RunSummary { get; }

    public IssuePanelViewModel IssuePanel { get; }

    public StatusBarViewModel StatusBar { get; }

    public ProjectStartupViewModel ProjectStartup => _projectStartup;

    public RoomsPanelViewModel RoomsPanel => _roomsPanel;

    public object CanvasView => Canvas.CanvasView;

    public bool IsBusy =>
        Canvas.IsBusy ||
        NewProjectCommand.IsExecuting ||
        OpenProjectCommand.IsExecuting ||
        SaveCommand.IsExecuting ||
        CloseProjectCommand.IsExecuting ||
        ExportCutListCommand.IsExecuting ||
        PreviewHtmlCommand.IsExecuting;

    public string CanvasCurrentMode => Canvas.CurrentMode;

    public string CurrentStatusText => Canvas.StatusMessage;

    public string ActiveProjectNameText => ActiveProject is null ? "No active project" : ActiveProject.Name;

    public string ProjectOpenText => HasActiveProject ? "Project open" : "No project open";

    public string RevisionText => ActiveProject is null ? "No revision" : $"Revision {ActiveProject.CurrentRevisionLabel}";

    public string SaveStateText => ActiveProject is null
        ? "Ready"
        : (ActiveProject.HasUnsavedChanges ? "Unsaved changes" : "Saved");

    public ProjectSummaryDto? ActiveProject
    {
        get => _activeProject;
        private set => SetActiveProject(value);
    }

    public bool HasActiveProject => ActiveProject is not null;

    public string WindowTitle => ActiveProject is null
        ? "Carpenter Studio"
        : $"{ActiveProject.Name} - Carpenter Studio";

    public string WorkspaceTitle => ActiveProject is null
        ? "Workspace"
        : ActiveProject.Name;

    public string WorkspaceSubtitle => ActiveProject is null
        ? "Canvas and shell controls"
        : $"{RevisionText} - {ProjectOpenText}";

    public ShellMode Mode => _mode;

    public bool IsStartupMode => Mode == ShellMode.Startup;

    public bool IsEditorMode => Mode == ShellMode.Editor;

    internal void SelectEntities(IReadOnlyList<Guid> entityIds) => Canvas.SetSelectedCabinetIds(entityIds);

    private async void OnCatalogItemActivated(object? sender, CatalogItemViewModel item)
    {
        try
        {
            if (!HasActiveProject)
            {
                return;
            }

            var runId = ResolveTargetRunId();
            if (runId is null)
            {
                Canvas.SetStatusMessage("No runs available to add to.");
                return;
            }

            await Canvas.AddCabinetToRunAsync(runId.Value, item.TypeId, item.DefaultNominalWidthInches)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            UserActionErrorReporter.Report(
                _logger,
                _eventBus,
                "Presentation",
                "project.catalog.add",
                "Failed to add cabinet from catalog item activation.",
                ex);
        }
    }

    private Guid? ResolveTargetRunId()
    {
        var scene = Canvas.Scene;
        if (scene is null || scene.Runs.Count == 0)
        {
            return null;
        }

        if (Canvas.SelectedCabinetIds.Count > 0)
        {
            var selectedId = Canvas.SelectedCabinetIds[0];
            var selectedCabinet = scene.Cabinets.FirstOrDefault(cabinet => cabinet.CabinetId == selectedId);
            if (selectedCabinet is not null)
            {
                return selectedCabinet.RunId;
            }
        }

        return scene.Runs[0].RunId;
    }

    public string PendingProjectName
    {
        get => _pendingProjectName;
        set
        {
            if (SetProperty(ref _pendingProjectName, value))
            {
                NewProjectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string PendingProjectFilePath
    {
        get => _pendingProjectFilePath;
        set => SetProperty(ref _pendingProjectFilePath, value);
    }

    public AsyncRelayCommand NewProjectCommand { get; }

    public AsyncRelayCommand OpenProjectCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand CloseProjectCommand { get; }

    public AsyncRelayCommand ExportCutListCommand { get; }

    public AsyncRelayCommand PreviewHtmlCommand { get; }

    public RelayCommand ShowAlphaLimitationsCommand { get; }

    public RelayCommand UndoCommand { get; }

    public RelayCommand RedoCommand { get; }

    public RelayCommand ResetZoomCommand => Canvas.ResetZoomCommand;

    public RelayCommand FitToViewCommand => Canvas.FitToViewCommand;

    public RelayCommand SelectAllCommand => Canvas.SelectAllCommand;

    public RelayCommand SelectNoneCommand => Canvas.SelectNoneCommand;

    public AsyncRelayCommand DeleteSelectedCommand => Canvas.DeleteSelectedCommand;

    public void Dispose()
    {
        Canvas.PropertyChanged -= OnCanvasPropertyChanged;
        Catalog.ItemActivated -= OnCatalogItemActivated;
        NewProjectCommand.PropertyChanged -= OnCommandPropertyChanged;
        OpenProjectCommand.PropertyChanged -= OnCommandPropertyChanged;
        SaveCommand.PropertyChanged -= OnCommandPropertyChanged;
        CloseProjectCommand.PropertyChanged -= OnCommandPropertyChanged;
        ExportCutListCommand.PropertyChanged -= OnCommandPropertyChanged;
        PreviewHtmlCommand.PropertyChanged -= OnCommandPropertyChanged;
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Unsubscribe<ActiveRoomChangedEvent>(OnActiveRoomChanged);
        _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<UndoAppliedEvent>(OnUndoRedoApplied);
        _eventBus.Unsubscribe<RedoAppliedEvent>(OnUndoRedoApplied);
        ProjectStartup.Dispose();
        RoomsPanel.Dispose();
        PropertyInspector.Dispose();
        IssuePanel.Dispose();
        RunSummary.Dispose();
        StatusBar.Dispose();
        Canvas.Dispose();
    }

    private async Task CreateProjectAsync()
    {
        // TODO: per-project file path when multi-project is supported.
        var name = string.IsNullOrWhiteSpace(PendingProjectName) ? "New Project" : PendingProjectName;
        ActiveProject = await _projectService.CreateProjectAsync(name).ConfigureAwait(true);
    }

    private async Task OpenProjectAsync()
    {
        var path = _dialogService.ShowOpenFileDialog(
            "Open Project",
            "Carpenter Studio Project (*.db)|*.db|All files (*.*)|*.*");

        if (path is null)
        {
            return;
        }

        PendingProjectFilePath = path;
        ActiveProject = await _projectService.OpenProjectAsync(path).ConfigureAwait(true);
    }

    private async Task SaveAsync()
    {
        await _projectService.SaveAsync().ConfigureAwait(true);
        SetActiveProject(_projectService.CurrentProject);
    }

    private async Task CloseProjectAsync()
    {
        if (ActiveProject?.HasUnsavedChanges == true)
        {
            var save = _dialogService.ShowYesNoDialog(
                "Unsaved Changes",
                $"'{ActiveProject.Name}' has unsaved changes. Save before closing?");

            if (save)
            {
                await _projectService.SaveAsync().ConfigureAwait(true);
            }
        }

        await _projectService.CloseAsync().ConfigureAwait(false);
    }

    private Task ExportCutListAsync()
    {
        var folderPath = _dialogService.ShowFolderPicker("Export Cut List");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return Task.CompletedTask;
        }

        var result = _cutListExportWorkflowService.BuildCurrentProjectCutList();
        if (!result.Success || result.Export is null || string.IsNullOrWhiteSpace(result.FileStem))
        {
            StatusBar.SetStatusMessage(result.FailureMessage ?? "Cut list export failed.");
            return Task.CompletedTask;
        }

        var csvPath = Path.Combine(folderPath, result.FileStem + ".cutlist.csv");
        var txtPath = Path.Combine(folderPath, result.FileStem + ".cutlist.txt");
        var htmlPath = Path.Combine(folderPath, result.FileStem + ".cutlist.html");
        File.WriteAllBytes(csvPath, result.Export.Csv);
        File.WriteAllBytes(txtPath, result.Export.Txt);
        File.WriteAllBytes(htmlPath, result.Export.Html);
        StatusBar.SetStatusMessage($"Cut list exported to {folderPath}.");
        return Task.CompletedTask;
    }

    private Task PreviewHtmlAsync()
    {
        var result = _cutListExportWorkflowService.BuildCurrentProjectCutList();
        if (!result.Success || result.Export is null || string.IsNullOrWhiteSpace(result.FileStem))
        {
            StatusBar.SetStatusMessage(result.FailureMessage ?? "HTML preview failed.");
            return Task.CompletedTask;
        }

        var previewPath = Path.Combine(Path.GetTempPath(), result.FileStem + ".cutlist.preview.html");
        File.WriteAllBytes(previewPath, result.Export.Html);
        Process.Start(new ProcessStartInfo(previewPath)
        {
            UseShellExecute = true
        });
        StatusBar.SetStatusMessage("Cut list HTML preview opened in your browser.");
        return Task.CompletedTask;
    }

    private void ShowAlphaLimitations() => _dialogService.ShowAlphaLimitationsDialog();

    private void OnProjectOpened(ProjectOpenedEvent @event) =>
        UiDispatchHelper.Run(() =>
        {
            SetActiveProject(@event.Project);
            SetMode(ShellMode.Editor);
        });

    private void OnProjectClosed(ProjectClosedEvent _) =>
        UiDispatchHelper.Run(() =>
        {
            SetActiveProject(null);
            SetMode(ShellMode.Startup);
            RefreshSelectionDrivenPanels();
        });

    private void OnActiveRoomChanged(ActiveRoomChangedEvent _) =>
        UiDispatchHelper.Run(() => OnPropertyChanged(nameof(IsEditorMode)));

    private void OnDesignChanged(DesignChangedEvent _) =>
        UiDispatchHelper.Run(() => SetActiveProject(_projectService.CurrentProject));

    private void OnCanvasPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorCanvasViewModel.StatusMessage))
        {
            OnPropertyChanged(nameof(CurrentStatusText));
            StatusBar.SetStatusMessage(Canvas.StatusMessage);
        }
        else if (e.PropertyName is nameof(EditorCanvasViewModel.CurrentMode))
        {
            OnPropertyChanged(nameof(CanvasCurrentMode));
        }
        else if (e.PropertyName is nameof(EditorCanvasViewModel.SelectedCabinetIds) ||
                 e.PropertyName is nameof(EditorCanvasViewModel.Scene) ||
                 e.PropertyName is nameof(EditorCanvasViewModel.IsBusy))
        {
            RefreshSelectionDrivenPanels();
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    private void OnUndoRedoApplied<TEvent>(TEvent _) where TEvent : IApplicationEvent =>
        UiDispatchHelper.Run(() => RefreshCommandStates());

    private void OnCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AsyncRelayCommand.IsExecuting))
        {
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    private bool SetActiveProject(ProjectSummaryDto? project)
    {
        if (!SetProperty(ref _activeProject, project, nameof(ActiveProject)))
        {
            RefreshCommandStates();
            return false;
        }

        OnPropertyChanged(nameof(HasActiveProject));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(WorkspaceSubtitle));
        OnPropertyChanged(nameof(ActiveProjectNameText));
        OnPropertyChanged(nameof(ProjectOpenText));
        OnPropertyChanged(nameof(RevisionText));
        OnPropertyChanged(nameof(SaveStateText));
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsStartupMode));
        OnPropertyChanged(nameof(IsEditorMode));
        StatusBar.SetProjectSummary(project);
        RefreshCommandStates();
        SetMode(project is null ? ShellMode.Startup : ShellMode.Editor);
        return true;
    }

    private void SetMode(ShellMode mode)
    {
        if (SetProperty(ref _mode, mode, nameof(Mode)))
        {
            OnPropertyChanged(nameof(IsStartupMode));
            OnPropertyChanged(nameof(IsEditorMode));
        }
    }

    private void RefreshCommandStates()
    {
        SaveCommand.NotifyCanExecuteChanged();
        CloseProjectCommand.NotifyCanExecuteChanged();
        ExportCutListCommand.NotifyCanExecuteChanged();
        PreviewHtmlCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void RefreshSelectionDrivenPanels()
    {
        PropertyInspector.OnSelectionChanged(Canvas.SelectedCabinetIds, Canvas.Scene);
        RunSummary.OnSelectionChanged(Canvas.SelectedCabinetIds);
    }

    private sealed class NoOpRoomService : IRoomService
    {
        public Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct) =>
            Task.FromException<Room>(new InvalidOperationException("Room service is not configured."));

        public Task<Wall> AddWallAsync(RoomId roomId, Point2D start, Point2D end, CabinetDesigner.Domain.Geometry.Thickness thickness, CancellationToken ct) =>
            Task.FromException<Wall>(new InvalidOperationException("Room service is not configured."));

        public Task RemoveWallAsync(WallId wallId, CancellationToken ct) =>
            Task.FromException(new InvalidOperationException("Room service is not configured."));

        public Task RenameRoomAsync(RoomId roomId, string newName, CancellationToken ct) =>
            Task.FromException(new InvalidOperationException("Room service is not configured."));

        public Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Room>>([]);
    }
}
