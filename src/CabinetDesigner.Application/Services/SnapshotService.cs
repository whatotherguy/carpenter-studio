using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
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
    private readonly IValidationResultStore _validationResultStore;
    private readonly IPackagingResultStore _packagingResultStore;
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
        IValidationResultStore validationResultStore,
        IPackagingResultStore packagingResultStore,
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
        _validationResultStore = validationResultStore ?? throw new ArgumentNullException(nameof(validationResultStore));
        _packagingResultStore = packagingResultStore ?? throw new ArgumentNullException(nameof(packagingResultStore));
        _eventBus = eventBus;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger;
    }

    public async Task<RevisionDto> ApproveRevisionAsync(string label, CancellationToken ct = default)
    {
        var state = _workingRevisionSource.CaptureCurrentState();
        var approvedAt = _clock.Now;
        if (_validationResultStore.Current is { IsValid: false })
        {
            throw new InvalidOperationException("Cannot approve an invalid design.");
        }

        var packaging = _packagingResultStore.Current;
        if (packaging is null || packaging.RevisionId != state.Revision.Id || string.IsNullOrWhiteSpace(packaging.ContentHash))
        {
            throw new InvalidOperationException("No current packaged snapshot is available for approval.");
        }

        var revision = state.Revision with
        {
            Label = label,
            State = ApprovalState.Approved,
            ApprovedAt = approvedAt
        };
        _ = await _validationHistoryRepository.LoadAsync(revision.Id, ct).ConfigureAwait(false);
        var snapshot = new ApprovedSnapshot(
            revision.Id,
            state.Project.Id,
            revision.RevisionNumber,
            revision.ApprovedAt ?? approvedAt,
            revision.ApprovedBy,
            label,
            packaging.ContentHash,
            packaging.DesignBlob,
            packaging.PartsBlob,
            packaging.ManufacturingBlob,
            packaging.InstallBlob,
            packaging.EstimateBlob,
            packaging.ValidationBlob,
            packaging.ExplanationBlob);

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

    public async Task<IReadOnlyList<RevisionDto>> GetRevisionHistoryAsync(CancellationToken ct = default)
    {
        var state = _workingRevisionSource.CaptureCurrentState();
        if (state is null)
            throw new InvalidOperationException("No project state is currently loaded.");

        var revisions = await _revisionRepository.ListAsync(state.Project.Id, ct).ConfigureAwait(false);
        return revisions.Select(revision => new RevisionDto(
            revision.Id.Value,
            revision.Label ?? $"Rev {revision.RevisionNumber}",
            revision.CreatedAt,
            revision.State.ToString(),
            revision.State >= ApprovalState.Approved,
            revision.State >= ApprovalState.LockedForManufacture)).ToArray();
    }
}
