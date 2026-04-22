using CabinetDesigner.Presentation.Commands;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class ProjectStartupViewModel : ObservableObject, IDisposable
{
    private readonly IProjectService _projectService;
    private readonly IApplicationEventBus _eventBus;
    private IReadOnlyList<ProjectSummaryDto> _recentProjects = [];
    private ProjectSummaryDto? _selectedProject;
    private string _newProjectName = "New Project";
    private bool _isBusy;

    public ProjectStartupViewModel(IProjectService projectService, IApplicationEventBus eventBus, IAppLogger logger)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        ArgumentNullException.ThrowIfNull(logger);

        NewProjectCommand = new AsyncRelayCommand(CreateProjectAsync, logger, eventBus, () => !string.IsNullOrWhiteSpace(NewProjectName));
        OpenSelectedProjectCommand = new AsyncRelayCommand(OpenSelectedProjectAsync, logger, eventBus, () => SelectedProject is not null);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, logger, eventBus);

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
    }

    public IReadOnlyList<ProjectSummaryDto> RecentProjects
    {
        get => _recentProjects;
        private set => SetProperty(ref _recentProjects, value);
    }

    public ProjectSummaryDto? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                OpenSelectedProjectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string NewProjectName
    {
        get => _newProjectName;
        set
        {
            if (SetProperty(ref _newProjectName, value))
            {
                NewProjectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool HasProjects => RecentProjects.Count > 0;

    public string EmptyStateText => HasProjects
        ? "Select a recent project or create a new one."
        : "Create your first project to begin.";

    public AsyncRelayCommand NewProjectCommand { get; }

    public AsyncRelayCommand OpenSelectedProjectCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public async Task InitializeAsync() => await RefreshAsync().ConfigureAwait(true);

    public void Dispose()
    {
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
    }

    private async Task CreateProjectAsync()
    {
        IsBusy = true;
        try
        {
            var project = await _projectService.CreateProjectAsync(NewProjectName).ConfigureAwait(true);
            SelectedProject = project;
            await RefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenSelectedProjectAsync()
    {
        if (SelectedProject is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _projectService.OpenProjectAsync(new ProjectId(SelectedProject.ProjectId)).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            RecentProjects = await _projectService.ListProjectsAsync().ConfigureAwait(true);
            SelectedProject ??= RecentProjects.FirstOrDefault();
            OpenSelectedProjectCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasProjects));
            OnPropertyChanged(nameof(EmptyStateText));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnProjectOpened(ProjectOpenedEvent @event) =>
        DispatchIfNeeded(() =>
        {
            SelectedProject = @event.Project;
            _ = RefreshAsync();
        });

    private void OnProjectClosed(ProjectClosedEvent @event) =>
        DispatchIfNeeded(() =>
        {
            SelectedProject = null;
            _ = RefreshAsync();
        });

    private static void DispatchIfNeeded(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
