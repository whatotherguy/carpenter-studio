using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;

namespace CabinetDesigner.Application.Services;

public sealed class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IRevisionRepository _revisionRepository;
    private readonly IWorkingRevisionRepository _workingRevisionRepository;
    private readonly IAutosaveCheckpointRepository _checkpointRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationEventBus _eventBus;
    private readonly ICurrentPersistedProjectState? _currentPersistedProjectState;
    private readonly IWorkingRevisionSource? _workingRevisionSource;
    private readonly IClock _clock;
    private readonly IAppLogger? _logger;

    public ProjectService(
        IProjectRepository projectRepository,
        IRevisionRepository revisionRepository,
        IWorkingRevisionRepository workingRevisionRepository,
        IAutosaveCheckpointRepository checkpointRepository,
        IUnitOfWork unitOfWork,
        IApplicationEventBus eventBus,
        IClock clock,
        IAppLogger? logger = null)
        : this(
            projectRepository,
            revisionRepository,
            workingRevisionRepository,
            checkpointRepository,
            unitOfWork,
            eventBus,
            clock,
            null,
            null,
            logger)
    {
    }

    public ProjectService(
        IProjectRepository projectRepository,
        IRevisionRepository revisionRepository,
        IWorkingRevisionRepository workingRevisionRepository,
        IAutosaveCheckpointRepository checkpointRepository,
        IUnitOfWork unitOfWork,
        IApplicationEventBus eventBus,
        IClock clock,
        ICurrentPersistedProjectState? currentPersistedProjectState,
        IWorkingRevisionSource? workingRevisionSource = null,
        IAppLogger? logger = null)
    {
        _projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
        _revisionRepository = revisionRepository ?? throw new ArgumentNullException(nameof(revisionRepository));
        _workingRevisionRepository = workingRevisionRepository ?? throw new ArgumentNullException(nameof(workingRevisionRepository));
        _checkpointRepository = checkpointRepository ?? throw new ArgumentNullException(nameof(checkpointRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _currentPersistedProjectState = currentPersistedProjectState;
        _workingRevisionSource = workingRevisionSource;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger;

        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
    }

    public ProjectSummaryDto? CurrentProject { get; private set; }

    public async Task<IReadOnlyList<ProjectSummaryDto>> ListProjectsAsync(CancellationToken ct = default)
    {
        var projects = await _projectRepository.ListRecentAsync(50, ct).ConfigureAwait(false);
        var results = new List<ProjectSummaryDto>(projects.Count);
        foreach (var project in projects)
        {
            var revision = await _revisionRepository.FindWorkingAsync(project.Id, ct).ConfigureAwait(false);
            if (revision is null)
            {
                revision = (await _revisionRepository.ListAsync(project.Id, ct).ConfigureAwait(false)).FirstOrDefault();
            }

            if (revision is null)
            {
                throw new InvalidOperationException($"No revision was found for project {project.Name}.");
            }

            results.Add(ToSummary(project, revision, project.FilePath ?? string.Empty, hasUnsavedChanges: false));
        }

        return results;
    }

    public async Task<ProjectSummaryDto> OpenProjectAsync(ProjectId projectId, CancellationToken ct = default)
    {
        var recentProjects = await _projectRepository.ListRecentAsync(50, ct).ConfigureAwait(false);
        var matchingProjects = recentProjects.Where(project => project.Id == projectId).ToArray();
        if (matchingProjects.Length > 1)
        {
            throw new InvalidOperationException($"Multiple projects matched project id '{projectId.Value}'.");
        }

        var project = matchingProjects.Length == 1
            ? matchingProjects[0]
            : await _projectRepository.FindAsync(projectId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Project {projectId.Value} was not found.");

        var revision = await _revisionRepository.FindWorkingAsync(project.Id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No working revision was found for project '{project.Name}'.");
        var workingRevision = await _workingRevisionRepository.LoadAsync(project.Id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No working revision payload was found for project '{project.Name}'.");
        var checkpoint = await _checkpointRepository.FindByProjectAsync(project.Id, ct).ConfigureAwait(false);
        _currentPersistedProjectState?.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));

        CurrentProject = ToSummary(project, revision, project.FilePath ?? string.Empty, checkpoint is { IsClean: false });
        if (checkpoint is { IsClean: false })
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Warning,
                Category = "Infrastructure",
                Message = "Previous session did not shut down cleanly.",
                Timestamp = _clock.Now,
                Properties = new Dictionary<string, string>
                {
                    ["projectId"] = project.Id.Value.ToString(),
                    ["revisionId"] = revision.Id.Value.ToString(),
                    ["projectName"] = project.Name
                }
            });
        }

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Application",
            Message = "Project opened.",
            Timestamp = _clock.Now,
            Properties = new Dictionary<string, string>
            {
                ["projectId"] = project.Id.Value.ToString(),
                ["revisionId"] = revision.Id.Value.ToString(),
                ["projectName"] = project.Name,
                ["dirty"] = (checkpoint is { IsClean: false }).ToString()
            }
        });
        _eventBus.Publish(new ProjectOpenedEvent(CurrentProject));
        return CurrentProject;
    }

    public async Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default)
    {
        var normalizedPath = NormalizeFilePath(filePath);
        var projects = await _projectRepository.ListRecentAsync(2, ct).ConfigureAwait(false);
        if (projects.Count == 0)
        {
            throw new InvalidOperationException("No persisted project was found in the selected cabinet file.");
        }

        if (projects.Count > 1)
        {
            throw new InvalidOperationException("Ambiguous project state; this cabinet file must contain exactly one project.");
        }

        var project = projects[0];
        if (!string.Equals(project.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            project = project with { FilePath = normalizedPath };
            await _projectRepository.SaveAsync(project, ct).ConfigureAwait(false);
        }

        var revision = await _revisionRepository.FindWorkingAsync(project.Id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("No working revision was found for the persisted project.");
        var workingRevision = await _workingRevisionRepository.LoadAsync(project.Id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("No working revision payload was found for the persisted project.");
        var checkpoint = await _checkpointRepository.FindByProjectAsync(project.Id, ct).ConfigureAwait(false);
        _currentPersistedProjectState?.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));

        CurrentProject = ToSummary(project, revision, normalizedPath, checkpoint is { IsClean: false });
        if (checkpoint is { IsClean: false })
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Warning,
                Category = "Infrastructure",
                Message = "Previous session did not shut down cleanly.",
                Timestamp = _clock.Now,
                Properties = new Dictionary<string, string>
                {
                    ["projectId"] = project.Id.Value.ToString(),
                    ["revisionId"] = revision.Id.Value.ToString(),
                    ["filePath"] = normalizedPath
                }
            });
        }

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Application",
            Message = "Project opened.",
            Timestamp = _clock.Now,
            Properties = new Dictionary<string, string>
            {
                ["projectId"] = project.Id.Value.ToString(),
                ["revisionId"] = revision.Id.Value.ToString(),
                ["filePath"] = normalizedPath,
                ["dirty"] = (checkpoint is { IsClean: false }).ToString()
            }
        });
        _eventBus.Publish(new ProjectOpenedEvent(CurrentProject));
        return CurrentProject;
    }

    public async Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default)
    {
        var createdAt = _clock.Now;
        var projectId = ProjectId.New();
        var project = new ProjectRecord(projectId, name, null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(
            RevisionId.New(),
            projectId,
            1,
            ApprovalState.Draft,
            createdAt,
            null,
            null,
            "Rev 1");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revision.Id, createdAt, null, true);

        await _unitOfWork.BeginAsync(ct).ConfigureAwait(false);
        try
        {
            await _projectRepository.SaveAsync(project, ct).ConfigureAwait(false);
            await _revisionRepository.SaveAsync(revision, ct).ConfigureAwait(false);
            await _workingRevisionRepository.SaveAsync(workingRevision, ct).ConfigureAwait(false);
            await _checkpointRepository.SaveAsync(checkpoint, ct).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }

        _currentPersistedProjectState?.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));
        CurrentProject = ToSummary(project, revision, string.Empty, hasUnsavedChanges: false);
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Application",
            Message = "Project created.",
            Timestamp = createdAt,
            Properties = new Dictionary<string, string>
            {
                ["projectId"] = project.Id.Value.ToString(),
                ["revisionId"] = revision.Id.Value.ToString()
            }
        });
        _eventBus.Publish(new ProjectOpenedEvent(CurrentProject));
        return CurrentProject;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        EnsureCurrentProject();
        var savedAt = _clock.Now;
        var projectId = new ProjectId(CurrentProject!.ProjectId);
        await _checkpointRepository.MarkCleanAsync(projectId, savedAt, ct).ConfigureAwait(false);
        CurrentProject = CurrentProject with { HasUnsavedChanges = false, LastModified = savedAt };
        UpdateCurrentState(savedAt, isClean: true, lastCommandId: null);
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Infrastructure",
            Message = "Autosave checkpoint marked clean.",
            Timestamp = savedAt,
            Properties = new Dictionary<string, string>
            {
                ["projectId"] = CurrentProject.ProjectId.ToString()
            }
        });
    }

    public async Task<RevisionDto> SaveRevisionAsync(string label, CancellationToken ct = default)
    {
        EnsureCurrentProject();
        var projectId = new ProjectId(CurrentProject!.ProjectId);
        var current = await _revisionRepository.FindWorkingAsync(projectId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Cannot save a revision without an active working revision.");
        var updated = current with
        {
            Label = label,
            State = ApprovalState.UnderReview
        };
        await _revisionRepository.SaveAsync(updated, ct).ConfigureAwait(false);
        if (_currentPersistedProjectState?.CurrentState is { } currentState)
        {
            _currentPersistedProjectState.SetCurrentState(currentState with { Revision = updated });
        }

        CurrentProject = CurrentProject with
        {
            CurrentRevisionLabel = label,
            HasUnsavedChanges = false
        };
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Application",
            Message = "Revision marked for review.",
            Timestamp = _clock.Now,
            Properties = new Dictionary<string, string>
            {
                ["projectId"] = projectId.Value.ToString(),
                ["revisionId"] = updated.Id.Value.ToString(),
                ["label"] = label
            }
        });
        return ToRevisionDto(updated);
    }

    public Task CloseAsync()
    {
        if (CurrentProject is null)
        {
            return Task.CompletedTask;
        }

        _eventBus.Publish(new ProjectClosedEvent(CurrentProject.ProjectId));
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Application",
            Message = "Project closed.",
            Timestamp = _clock.Now,
            Properties = new Dictionary<string, string>
            {
                ["projectId"] = CurrentProject.ProjectId.ToString()
            }
        });
        CurrentProject = null;
        _currentPersistedProjectState?.Clear();
        return Task.CompletedTask;
    }

    private static RevisionDto ToRevisionDto(RevisionRecord revision) =>
        new(
            revision.Id.Value,
            revision.Label ?? $"Rev {revision.RevisionNumber}",
            revision.CreatedAt,
            revision.State.ToString(),
            revision.State >= ApprovalState.Approved,
            revision.State >= ApprovalState.LockedForManufacture);

    private static ProjectSummaryDto ToSummary(ProjectRecord project, RevisionRecord revision, string filePath, bool hasUnsavedChanges) =>
        new(
            project.Id.Value,
            project.Name,
            filePath,
            project.UpdatedAt,
            revision.Label ?? $"Rev {revision.RevisionNumber}",
            hasUnsavedChanges);

    private void EnsureCurrentProject()
    {
        if (CurrentProject is null)
        {
            throw new InvalidOperationException("No current project is open.");
        }
    }

    private void OnDesignChanged(DesignChangedEvent @event)
    {
        if (CurrentProject is null)
        {
            return;
        }

        var changedAt = _clock.Now;
        CurrentProject = CurrentProject with
        {
            HasUnsavedChanges = true,
            LastModified = changedAt
        };

        CommandId? lastCommandId = @event.Result.CommandId == Guid.Empty
            ? null
            : new CommandId(@event.Result.CommandId);

        UpdateCurrentState(changedAt, isClean: false, lastCommandId);
    }

    private void UpdateCurrentState(DateTimeOffset savedAt, bool isClean, CommandId? lastCommandId)
    {
        if (_currentPersistedProjectState?.CurrentState is not { } || _workingRevisionSource is null)
        {
            return;
        }

        var latestState = _workingRevisionSource.CaptureCurrentState();
        var checkpoint = latestState.Checkpoint ?? new AutosaveCheckpoint(
            Guid.NewGuid().ToString("N"),
            latestState.Project.Id,
            latestState.Revision.Id,
            savedAt,
            lastCommandId,
            isClean);

        _currentPersistedProjectState.SetCurrentState(latestState with
        {
            Project = latestState.Project with { UpdatedAt = savedAt },
            Checkpoint = checkpoint with
            {
                SavedAt = savedAt,
                LastCommandId = lastCommandId ?? checkpoint.LastCommandId,
                IsClean = isClean
            }
        });
    }

    private static string NormalizeFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A project file path is required.", nameof(filePath));
        }

        return Path.GetFullPath(filePath);
    }
}
