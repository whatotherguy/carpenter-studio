using CabinetDesigner.Application.Diagnostics;

namespace CabinetDesigner.Persistence.UnitOfWork;

internal sealed class CommandPersistenceService : ICommandPersistencePort
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkingRevisionSource _workingRevisionSource;
    private readonly IProjectRepository _projectRepository;
    private readonly IRevisionRepository _revisionRepository;
    private readonly IWorkingRevisionRepository _workingRevisionRepository;
    private readonly ICommandJournalRepository _commandJournalRepository;
    private readonly IExplanationRepository _explanationRepository;
    private readonly IValidationHistoryRepository _validationHistoryRepository;
    private readonly IAutosaveCheckpointRepository _checkpointRepository;
    private readonly IWhyEngine _whyEngine;
    private readonly IAppLogger? _logger;

    public CommandPersistenceService(
        IUnitOfWork unitOfWork,
        IWorkingRevisionSource workingRevisionSource,
        IProjectRepository projectRepository,
        IRevisionRepository revisionRepository,
        IWorkingRevisionRepository workingRevisionRepository,
        ICommandJournalRepository commandJournalRepository,
        IExplanationRepository explanationRepository,
        IValidationHistoryRepository validationHistoryRepository,
        IAutosaveCheckpointRepository checkpointRepository,
        IWhyEngine whyEngine,
        IAppLogger? logger = null)
    {
        _unitOfWork = unitOfWork;
        _workingRevisionSource = workingRevisionSource;
        _projectRepository = projectRepository;
        _revisionRepository = revisionRepository;
        _workingRevisionRepository = workingRevisionRepository;
        _commandJournalRepository = commandJournalRepository;
        _explanationRepository = explanationRepository;
        _validationHistoryRepository = validationHistoryRepository;
        _checkpointRepository = checkpointRepository;
        _whyEngine = whyEngine;
        _logger = logger;
    }

    public async Task CommitCommandAsync(IDesignCommand command, CommandResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(result);

        var state = _workingRevisionSource.CaptureCurrentState();
        var explanationNodeIds = result.ExplanationNodeIds.ToHashSet();
        var nodes = _whyEngine
            .GetAllNodes()
            .Where(node => explanationNodeIds.Contains(node.Id))
            .OrderBy(node => node.Timestamp)
            .Select(node => new ExplanationNodeRecord(
                node.Id,
                state.Revision.Id,
                node.CommandId,
                node.StageNumber,
                node.NodeType,
                node.DecisionType,
                node.Description,
                node.AffectedEntityIds,
                node.Edges.FirstOrDefault()?.TargetNodeId,
                node.Edges.FirstOrDefault()?.EdgeType.ToString(),
                ExplanationNodeStatus.Active,
                node.Timestamp))
            .ToArray();

        var validationIssues = result.Issues
            .Select(issue => new ValidationIssueRecord(
                new ValidationIssueId(issue.Code, issue.AffectedEntityIds ?? []),
                state.Revision.Id,
                command.Metadata.Timestamp,
                issue.Severity,
                issue.Code,
                issue.Message,
                issue.AffectedEntityIds ?? [],
                null))
            .ToArray();

        await _unitOfWork.BeginAsync(ct).ConfigureAwait(false);
        try
        {
            await _projectRepository.SaveAsync(state.Project, ct).ConfigureAwait(false);
            await _revisionRepository.SaveAsync(state.Revision, ct).ConfigureAwait(false);
            await _workingRevisionRepository.SaveAsync(state.WorkingRevision, ct).ConfigureAwait(false);
            await _commandJournalRepository.AppendAsync(new CommandJournalEntry(
                command.Metadata.CommandId,
                state.Revision.Id,
                0,
                command.CommandType,
                command.Metadata.Origin,
                command.Metadata.IntentDescription,
                command.Metadata.AffectedEntityIds,
                command.Metadata.ParentCommandId,
                command.Metadata.Timestamp,
                JsonSerializer.Serialize(command, command.GetType(), Mapping.SqliteJson.Options),
                result.Deltas,
                result.Success), ct).ConfigureAwait(false);

            foreach (var node in nodes)
            {
                await _explanationRepository.AppendNodeAsync(node, ct).ConfigureAwait(false);
            }

            await _validationHistoryRepository.SaveIssuesAsync(state.Revision.Id, validationIssues, ct).ConfigureAwait(false);

            var checkpoint = state.Checkpoint ?? new AutosaveCheckpoint(
                Guid.NewGuid().ToString("N"),
                state.Project.Id,
                state.Revision.Id,
                command.Metadata.Timestamp,
                command.Metadata.CommandId,
                false);
            await _checkpointRepository.SaveAsync(checkpoint with
            {
                SavedAt = command.Metadata.Timestamp,
                LastCommandId = command.Metadata.CommandId,
                IsClean = false
            }, ct).ConfigureAwait(false);

            await _unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Info,
                Category = "Infrastructure",
                Message = "Autosave checkpoint marked dirty after command commit.",
                Timestamp = command.Metadata.Timestamp,
                CommandId = command.Metadata.CommandId.Value.ToString(),
                Properties = new Dictionary<string, string>
                {
                    ["projectId"] = state.Project.Id.Value.ToString(),
                    ["revisionId"] = state.Revision.Id.Value.ToString(),
                    ["isClean"] = "false"
                }
            });
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }
}
