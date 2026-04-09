using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.Commands;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IProjectService _projectService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly IApplicationEventBus _eventBus;
    private ProjectSummaryDto? _activeProject;
    private string _pendingProjectName = "New Project";
    private string _pendingProjectFilePath = string.Empty;

    public ShellViewModel(
        IProjectService projectService,
        IUndoRedoService undoRedoService,
        IApplicationEventBus eventBus,
        EditorCanvasViewModel canvas)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

        NewProjectCommand = new AsyncRelayCommand(CreateProjectAsync, () => !string.IsNullOrWhiteSpace(PendingProjectName));
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync, () => !string.IsNullOrWhiteSpace(PendingProjectFilePath));
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasActiveProject);
        CloseProjectCommand = new AsyncRelayCommand(CloseProjectAsync, () => HasActiveProject);
        UndoCommand = new RelayCommand(() => _ = _undoRedoService.Undo(), () => _undoRedoService.CanUndo);
        RedoCommand = new RelayCommand(() => _ = _undoRedoService.Redo(), () => _undoRedoService.CanRedo);

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Subscribe<UndoAppliedEvent>(OnUndoRedoApplied);
        _eventBus.Subscribe<RedoAppliedEvent>(OnUndoRedoApplied);

        SetActiveProject(_projectService.CurrentProject);
    }

    public EditorCanvasViewModel Canvas { get; }

    public ProjectSummaryDto? ActiveProject
    {
        get => _activeProject;
        private set => SetActiveProject(value);
    }

    public bool HasActiveProject => ActiveProject is not null;

    public string WindowTitle => ActiveProject is null
        ? "Carpenter Studio"
        : $"{ActiveProject.Name} - Carpenter Studio";

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

    public void Dispose()
    {
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Unsubscribe<UndoAppliedEvent>(OnUndoRedoApplied);
        _eventBus.Unsubscribe<RedoAppliedEvent>(OnUndoRedoApplied);
        Canvas.Dispose();
    }

    private async Task CreateProjectAsync()
    {
        ActiveProject = await _projectService.CreateProjectAsync(PendingProjectName).ConfigureAwait(false);
    }

    private async Task OpenProjectAsync()
    {
        ActiveProject = await _projectService.OpenProjectAsync(PendingProjectFilePath).ConfigureAwait(false);
    }

    private async Task SaveAsync()
    {
        await _projectService.SaveAsync().ConfigureAwait(false);
        SetActiveProject(_projectService.CurrentProject);
    }

    private async Task CloseProjectAsync()
    {
        await _projectService.CloseAsync().ConfigureAwait(false);
    }

    private void OnProjectOpened(ProjectOpenedEvent @event) => SetActiveProject(@event.Project);

    private void OnProjectClosed(ProjectClosedEvent _) => SetActiveProject(null);

    private void OnUndoRedoApplied<TEvent>(TEvent _) where TEvent : IApplicationEvent => RefreshCommandStates();

    private bool SetActiveProject(ProjectSummaryDto? project)
    {
        if (!SetProperty(ref _activeProject, project, nameof(ActiveProject)))
        {
            RefreshCommandStates();
            return false;
        }

        OnPropertyChanged(nameof(HasActiveProject));
        OnPropertyChanged(nameof(WindowTitle));
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
}
