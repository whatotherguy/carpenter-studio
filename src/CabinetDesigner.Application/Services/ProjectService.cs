using System.Threading;
using System.Threading.Tasks;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Domain;
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
        IAppLogger? logger = null)
    {
        _projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
        _revisionRepository = revisionRepository ?? throw new ArgumentNullException(nameof(revisionRepository));
        _workingRevisionRepository = workingRevisionRepository ?? throw new ArgumentNullException(nameof(workingRevisionRepository));
        _checkpointRepository = checkpointRepository ?? throw new ArgumentNullException(nameof(checkpointRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _currentPersistedProjectState = currentPersistedProjectState;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger;
    }

    public ProjectSummaryDto? CurrentProject { get; private set; }

    public async Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default)
    {
        var project = (await _projectRepository.ListRecentAsync(1, ct).ConfigureAwait(false)).SingleOrDefault()
            ?? throw new InvalidOperationException("No persisted project was found in the current cabinet file.");
        var revision = await _revisionRepository.FindWorkingAsync(project.Id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("No working revision was found for the persisted project.");
        var workingRevision = await _workingRevisionRepository.LoadAsync(project.Id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("No working revision payload was found for the persisted project.");
        var checkpoint = await _checkpointRepository.FindByProjectAsync(project.Id, ct).ConfigureAwait(false);
        _currentPersistedProjectState?.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));

        CurrentProject = ToSummary(project, revision, filePath, checkpoint is { IsClean: false });
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
                    ["filePath"] = filePath
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
                ["filePath"] = filePath,
                ["dirty"] = (checkpoint is { IsClean: false }).ToString()
            }
        });
        _eventBus.Publish(new ProjectOpenedEvent(CurrentProject));
        return CurrentProject;
    }

    public async Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default)
    {
        var createdAt = _clock.Now;
        var projectId = Domain.Identifiers.ProjectId.New();
        var project = new ProjectRecord(projectId, name, null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(
            Domain.Identifiers.RevisionId.New(),
            projectId,
            1,
            ApprovalState.Draft,
            createdAt,
            null,
            null,
            "Rev 1");
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revision.Id, createdAt, null, true);

        await _unitOfWork.BeginAsync(ct).ConfigureAwait(false);
        try
        {
            await _projectRepository.SaveAsync(project, ct).ConfigureAwait(false);
            await _revisionRepository.SaveAsync(revision, ct).ConfigureAwait(false);
            await _checkpointRepository.SaveAsync(checkpoint, ct).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }

        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
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
        return CurrentProject;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        EnsureCurrentProject();
        var savedAt = _clock.Now;
        await _checkpointRepository.MarkCleanAsync(new Domain.Identifiers.ProjectId(CurrentProject!.ProjectId), savedAt, ct).ConfigureAwait(false);
        CurrentProject = CurrentProject with { HasUnsavedChanges = false };
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
        var projectId = new Domain.Identifiers.ProjectId(CurrentProject!.ProjectId);
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

    public async Task CloseAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        var savedAt = _clock.Now;
        await _checkpointRepository.MarkCleanAsync(new Domain.Identifiers.ProjectId(CurrentProject.ProjectId), savedAt).ConfigureAwait(false);
        _eventBus.Publish(new ProjectClosedEvent(CurrentProject.ProjectId));
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Application",
            Message = "Project closed.",
            Timestamp = savedAt,
            Properties = new Dictionary<string, string>
            {
                ["projectId"] = CurrentProject.ProjectId.ToString()
            }
        });
        CurrentProject = null;
        _currentPersistedProjectState?.Clear();
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
}
