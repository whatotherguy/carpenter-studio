using System.ComponentModel;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation;
using CabinetDesigner.Presentation.Commands;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IProjectService _projectService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly IApplicationEventBus _eventBus;
    private readonly IDialogService _dialogService;
    private ProjectSummaryDto? _activeProject;
    private string _pendingProjectName = "New Project";
    private string _pendingProjectFilePath = string.Empty;

    public ShellViewModel(
        IProjectService projectService,
        IUndoRedoService undoRedoService,
        IApplicationEventBus eventBus,
        EditorCanvasViewModel canvas,
        CatalogPanelViewModel catalog,
        PropertyInspectorViewModel propertyInspector,
        RunSummaryPanelViewModel runSummary,
        IssuePanelViewModel issuePanel,
        StatusBarViewModel statusBar,
        IDialogService dialogService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        PropertyInspector = propertyInspector ?? throw new ArgumentNullException(nameof(propertyInspector));
        RunSummary = runSummary ?? throw new ArgumentNullException(nameof(runSummary));
        IssuePanel = issuePanel ?? throw new ArgumentNullException(nameof(issuePanel));
        StatusBar = statusBar ?? throw new ArgumentNullException(nameof(statusBar));
        Canvas.PropertyChanged += OnCanvasPropertyChanged;
        Catalog.ItemActivated += OnCatalogItemActivated;
        IssuePanel.SetSelectionCallback(SelectEntities);

        NewProjectCommand = new AsyncRelayCommand(CreateProjectAsync, () => true);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync, () => true);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasActiveProject);
        CloseProjectCommand = new AsyncRelayCommand(CloseProjectAsync, () => HasActiveProject);
        UndoCommand = new RelayCommand(() => _ = _undoRedoService.Undo(), () => _undoRedoService.CanUndo);
        RedoCommand = new RelayCommand(() => _ = _undoRedoService.Redo(), () => _undoRedoService.CanRedo);

        NewProjectCommand.PropertyChanged += OnCommandPropertyChanged;
        OpenProjectCommand.PropertyChanged += OnCommandPropertyChanged;
        SaveCommand.PropertyChanged += OnCommandPropertyChanged;
        CloseProjectCommand.PropertyChanged += OnCommandPropertyChanged;

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Subscribe<UndoAppliedEvent>(OnUndoRedoApplied);
        _eventBus.Subscribe<RedoAppliedEvent>(OnUndoRedoApplied);

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

    public object CanvasView => Canvas.CanvasView;

    public bool IsBusy =>
        Canvas.IsBusy ||
        NewProjectCommand.IsExecuting ||
        OpenProjectCommand.IsExecuting ||
        SaveCommand.IsExecuting ||
        CloseProjectCommand.IsExecuting;

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

    internal void SelectEntities(IReadOnlyList<Guid> entityIds) => Canvas.SetSelectedCabinetIds(entityIds);

    private async void OnCatalogItemActivated(object? sender, CatalogItemViewModel item)
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
            SetProperty(ref _pendingProjectName, value);
        }
    }

    public string PendingProjectFilePath
    {
        get => _pendingProjectFilePath;
        set
        {
            if (SetProperty(ref _pendingProjectFilePath, value))
            {
                OpenProjectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand NewProjectCommand { get; }

    public AsyncRelayCommand OpenProjectCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand CloseProjectCommand { get; }

    public RelayCommand UndoCommand { get; }

    public RelayCommand RedoCommand { get; }

    public RelayCommand ResetZoomCommand => Canvas.ResetZoomCommand;

    public RelayCommand FitToViewCommand => Canvas.FitToViewCommand;

    public RelayCommand SelectAllCommand => Canvas.SelectAllCommand;

    public RelayCommand SelectNoneCommand => Canvas.SelectNoneCommand;

    public void Dispose()
    {
        Canvas.PropertyChanged -= OnCanvasPropertyChanged;
        Catalog.ItemActivated -= OnCatalogItemActivated;
        NewProjectCommand.PropertyChanged -= OnCommandPropertyChanged;
        OpenProjectCommand.PropertyChanged -= OnCommandPropertyChanged;
        SaveCommand.PropertyChanged -= OnCommandPropertyChanged;
        CloseProjectCommand.PropertyChanged -= OnCommandPropertyChanged;
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<UndoAppliedEvent>(OnUndoRedoApplied);
        _eventBus.Unsubscribe<RedoAppliedEvent>(OnUndoRedoApplied);
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

    private void OnProjectOpened(ProjectOpenedEvent @event) => SetActiveProject(@event.Project);

    private void OnProjectClosed(ProjectClosedEvent _)
    {
        SetActiveProject(null);
        RefreshSelectionDrivenPanels();
    }

    private void OnDesignChanged(DesignChangedEvent _)
    {
        SetActiveProject(_projectService.CurrentProject);
    }

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

    private void OnUndoRedoApplied<TEvent>(TEvent _) where TEvent : IApplicationEvent => RefreshCommandStates();

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
        StatusBar.SetProjectSummary(project);
        RefreshCommandStates();
        return true;
    }

    private void RefreshCommandStates()
    {
        SaveCommand.NotifyCanExecuteChanged();
        CloseProjectCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void RefreshSelectionDrivenPanels()
    {
        PropertyInspector.OnSelectionChanged(Canvas.SelectedCabinetIds, Canvas.Scene);
        RunSummary.OnSelectionChanged(Canvas.SelectedCabinetIds);
    }
}
