using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.ProjectContext;

namespace CabinetDesigner.Application.Services;

public sealed class SnapshotService : ISnapshotService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProjectRepository _projectRepository;
    private readonly IRevisionRepository _revisionRepository;
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IWorkingRevisionSource _workingRevisionSource;
    private readonly IValidationHistoryRepository _validationHistoryRepository;
    private readonly IApplicationEventBus _eventBus;
    private readonly IClock _clock;
    private readonly IAppLogger? _logger;

    public SnapshotService(
        IUnitOfWork unitOfWork,
        IProjectRepository projectRepository,
        IRevisionRepository revisionRepository,
        ISnapshotRepository snapshotRepository,
        IWorkingRevisionSource workingRevisionSource,
        IValidationHistoryRepository validationHistoryRepository,
        IApplicationEventBus eventBus,
        IClock clock,
        IAppLogger? logger = null)
    {
        _unitOfWork = unitOfWork;
        _projectRepository = projectRepository;
        _revisionRepository = revisionRepository;
        _snapshotRepository = snapshotRepository;
        _workingRevisionSource = workingRevisionSource;
        _validationHistoryRepository = validationHistoryRepository;
        _eventBus = eventBus;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger;
    }

    public async Task<RevisionDto> ApproveRevisionAsync(string label, CancellationToken ct = default)
    {
        var state = _workingRevisionSource.CaptureCurrentState();
        var approvedAt = _clock.Now;
        var revision = state.Revision with
        {
            Label = label,
            State = ApprovalState.Approved,
            ApprovedAt = approvedAt
        };
        var validationIssues = await _validationHistoryRepository.LoadAsync(revision.Id, ct).ConfigureAwait(false);
        var snapshot = new ApprovedSnapshot(
            revision.Id,
            state.Project.Id,
            revision.RevisionNumber,
            revision.ApprovedAt ?? approvedAt,
            revision.ApprovedBy,
            label,
            JsonSerializer.Serialize(new { schema_version = 1, revision_id = revision.Id.Value, label, project = state.Project.Name }),
            JsonSerializer.Serialize(new { schema_version = 1, revision_id = revision.Id.Value, parts = state.WorkingRevision.Parts }),
            JsonSerializer.Serialize(new { schema_version = 1, revision_id = revision.Id.Value, parts_count = state.WorkingRevision.Parts.Count }),
            JsonSerializer.Serialize(new { schema_version = 1, revision_id = revision.Id.Value, cabinet_count = state.WorkingRevision.Cabinets.Count }),
            JsonSerializer.Serialize(new { schema_version = 1, revision_id = revision.Id.Value, project = state.Project.Name }),
            JsonSerializer.Serialize(new { schema_version = 1, revision_id = revision.Id.Value, issues = validationIssues }),
            JsonSerializer.Serialize(new { schema_version = 1, revision_id = revision.Id.Value, explanation = Array.Empty<object>() }));

        await _unitOfWork.BeginAsync(ct).ConfigureAwait(false);
        try
        {
            await _revisionRepository.SaveAsync(revision, ct).ConfigureAwait(false);
            await _projectRepository.SaveAsync(state.Project with { CurrentState = ApprovalState.Approved, UpdatedAt = snapshot.ApprovedAt }, ct).ConfigureAwait(false);
            await _snapshotRepository.WriteAsync(snapshot, ct).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }

        var dto = new RevisionDto(revision.Id.Value, label, revision.CreatedAt, revision.State.ToString(), true, revision.State >= ApprovalState.LockedForManufacture);
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Persistence",
            Message = "Revision approved and snapshot written.",
            Timestamp = approvedAt,
            Properties = new Dictionary<string, string>
            {
                ["projectId"] = state.Project.Id.Value.ToString(),
                ["revisionId"] = revision.Id.Value.ToString(),
                ["label"] = label
            }
        });
        _eventBus.Publish(new RevisionApprovedEvent(dto));
        return dto;
    }

    public async Task<RevisionDto> LoadSnapshotAsync(Guid revisionId, CancellationToken ct = default)
    {
        var snapshot = await _snapshotRepository.ReadAsync(new Domain.Identifiers.RevisionId(revisionId), ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No approved snapshot exists for revision {revisionId}.");
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Persistence",
            Message = "Approved snapshot loaded.",
            Timestamp = _clock.Now,
            Properties = new Dictionary<string, string>
            {
                ["revisionId"] = revisionId.ToString()
            }
        });
        return new RevisionDto(snapshot.RevisionId.Value, snapshot.Label, snapshot.ApprovedAt, ApprovalState.Approved.ToString(), true, false);
    }

    public IReadOnlyList<RevisionDto> GetRevisionHistory()
    {
        var state = _workingRevisionSource.CaptureCurrentState();
        var revisions = _revisionRepository.ListAsync(state.Project.Id).GetAwaiter().GetResult();
        return revisions.Select(revision => new RevisionDto(
            revision.Id.Value,
            revision.Label ?? $"Rev {revision.RevisionNumber}",
            revision.CreatedAt,
            revision.State.ToString(),
            revision.State >= ApprovalState.Approved,
            revision.State >= ApprovalState.LockedForManufacture)).ToArray();
    }
}
