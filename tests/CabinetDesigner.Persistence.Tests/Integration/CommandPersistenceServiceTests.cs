using System.Reflection;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using CabinetDesigner.Persistence.UnitOfWork;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

public sealed class CommandPersistenceServiceTests
{
    [Fact]
    public async Task CommitCommandAsync_MarksAutosaveCheckpointDirty()
    {
        var tupleState = TestData.CreatePersistedState();
        var state = new PersistedProjectState(tupleState.Project, tupleState.Revision, tupleState.WorkingRevision, tupleState.Checkpoint);
        var logger = new RecordingAppLogger();
        var graph = new DependencyGraph(state);
        var service = CreateService(graph, logger);
        var command = new TestDesignCommand();
        var result = CommandResult.Succeeded(command.Metadata, [], []);

        await service.CommitCommandAsync(command, result);

        var checkpoint = Assert.Single(graph.CheckpointRepository.SavedCheckpoints);
        Assert.False(checkpoint.IsClean);
        Assert.Equal(command.Metadata.CommandId, checkpoint.LastCommandId);
        Assert.Contains(logger.Entries, entry => entry.Message == "Autosave checkpoint marked dirty after command commit.");
    }

    [Fact]
    public async Task CommitCommandAsync_WhenRepositoryThrows_RollsBackAllWrites()
    {
        var tupleState = TestData.CreatePersistedState();
        var state = new PersistedProjectState(tupleState.Project, tupleState.Revision, tupleState.WorkingRevision, tupleState.Checkpoint);
        var unitOfWork = new RecordingUnitOfWork();
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var commandJournalRepository = new RecordingCommandJournalRepository();
        var explanationRepository = new RecordingExplanationRepository();
        var checkpointRepository = new RecordingAutosaveCheckpointRepository();
        var service = CreateService(
            unitOfWork,
            new RecordingWorkingRevisionSource(state),
            projectRepository,
            revisionRepository,
            workingRevisionRepository,
            commandJournalRepository,
            explanationRepository,
            new ThrowingValidationHistoryRepository(),
            checkpointRepository,
            new RecordingWhyEngine(),
            logger: null);
        var command = new TestDesignCommand();
        var result = CommandResult.Succeeded(command.Metadata, [], []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitCommandAsync(command, result));

        Assert.Equal(1, unitOfWork.BeginCalls);
        Assert.Equal(0, unitOfWork.CommitCalls);
        Assert.Equal(1, unitOfWork.RollbackCalls);
        Assert.Single(projectRepository.SavedProjects);
        Assert.Single(revisionRepository.SavedRevisions);
        Assert.Single(workingRevisionRepository.SavedRevisions);
        Assert.Single(commandJournalRepository.Entries);
        Assert.Empty(checkpointRepository.SavedCheckpoints);
    }

    [Fact]
    public async Task CommitCommandAsync_WhenSecondWriteFails_RollsBackAllWritesToDatabase()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var tupleState = TestData.CreatePersistedState();
        var state = new PersistedProjectState(tupleState.Project, tupleState.Revision, tupleState.WorkingRevision, tupleState.Checkpoint);

        var unitOfWork = new SqliteUnitOfWork(fixture.ConnectionFactory, fixture.SessionAccessor);
        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var commandJournalRepository = new CommandJournalRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var explanationRepository = new ExplanationRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var validationHistoryRepository = new ThrowingValidationHistoryRepository();
        var checkpointRepository = new AutosaveCheckpointRepository(fixture.ConnectionFactory, fixture.SessionAccessor);

        var service = CreateService(
            unitOfWork,
            new RecordingWorkingRevisionSource(state),
            projectRepository,
            revisionRepository,
            workingRevisionRepository,
            commandJournalRepository,
            explanationRepository,
            validationHistoryRepository,
            checkpointRepository,
            new RecordingWhyEngine(),
            logger: null);

        var command = new TestDesignCommand();
        var result = CommandResult.Succeeded(command.Metadata, [], []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitCommandAsync(command, result));

        // Verify no data was persisted: all writes should have been rolled back.
        var projectsCount = await CountRowsAsync(fixture, "SELECT COUNT(*) FROM projects;");
        var revisionsCount = await CountRowsAsync(fixture, "SELECT COUNT(*) FROM revisions;");
        var commandJournalCount = await CountRowsAsync(fixture, "SELECT COUNT(*) FROM command_journal;");
        var checkpointsCount = await CountRowsAsync(fixture, "SELECT COUNT(*) FROM autosave_checkpoints;");

        Assert.Equal(0, projectsCount);
        Assert.Equal(0, revisionsCount);
        Assert.Equal(0, commandJournalCount);
        Assert.Equal(0, checkpointsCount);
    }

    private static ICommandPersistencePort CreateService(DependencyGraph graph, RecordingAppLogger logger)
    {
        return CreateService(
            graph.UnitOfWork,
            graph.WorkingRevisionSource,
            graph.ProjectRepository,
            graph.RevisionRepository,
            graph.WorkingRevisionRepository,
            graph.CommandJournalRepository,
            graph.ExplanationRepository,
            graph.ValidationHistoryRepository,
            graph.CheckpointRepository,
            graph.WhyEngine,
            logger);
    }

    private static ICommandPersistencePort CreateService(
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
        IAppLogger? logger)
    {
        var type = Type.GetType("CabinetDesigner.Persistence.UnitOfWork.CommandPersistenceService, CabinetDesigner.Persistence", throwOnError: true)!;
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 11);
        return (ICommandPersistencePort)constructor.Invoke(new object?[]
        {
            unitOfWork,
            workingRevisionSource,
            projectRepository,
            revisionRepository,
            workingRevisionRepository,
            commandJournalRepository,
            explanationRepository,
            validationHistoryRepository,
            checkpointRepository,
            whyEngine,
            logger
        });
    }

    private sealed class DependencyGraph(PersistedProjectState state)
    {
        public RecordingUnitOfWork UnitOfWork { get; } = new();

        public RecordingWorkingRevisionSource WorkingRevisionSource { get; } = new(state);

        public RecordingProjectRepository ProjectRepository { get; } = new();

        public RecordingRevisionRepository RevisionRepository { get; } = new();

        public RecordingWorkingRevisionRepository WorkingRevisionRepository { get; } = new();

        public RecordingCommandJournalRepository CommandJournalRepository { get; } = new();

        public RecordingExplanationRepository ExplanationRepository { get; } = new();

        public RecordingValidationHistoryRepository ValidationHistoryRepository { get; } = new();

        public RecordingAutosaveCheckpointRepository CheckpointRepository { get; } = new();

        public RecordingWhyEngine WhyEngine { get; } = new();
    }

    private sealed class RecordingAppLogger : IAppLogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log(LogEntry entry) => Entries.Add(entry);
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int BeginCalls { get; private set; }

        public int CommitCalls { get; private set; }

        public int RollbackCalls { get; private set; }

        public Task BeginAsync(CancellationToken ct = default)
        {
            BeginCalls++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken ct = default)
        {
            CommitCalls++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            RollbackCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingWorkingRevisionSource(PersistedProjectState state) : IWorkingRevisionSource
    {
        public PersistedProjectState CaptureCurrentState(PartGenerationResult? partResult = null) => state;
    }

    private sealed class RecordingProjectRepository : IProjectRepository
    {
        public List<ProjectRecord> SavedProjects { get; } = [];

        public Task<ProjectRecord?> FindAsync(ProjectId id, CancellationToken ct = default) => Task.FromResult<ProjectRecord?>(null);

        public Task SaveAsync(ProjectRecord project, CancellationToken ct = default)
        {
            SavedProjects.Add(project);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProjectRecord>> ListRecentAsync(int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectRecord>>([]);
    }

    private sealed class RecordingRevisionRepository : IRevisionRepository
    {
        public List<RevisionRecord> SavedRevisions { get; } = [];

        public Task<RevisionRecord?> FindAsync(RevisionId id, CancellationToken ct = default) => Task.FromResult<RevisionRecord?>(null);

        public Task<RevisionRecord?> FindWorkingAsync(ProjectId projectId, CancellationToken ct = default) => Task.FromResult<RevisionRecord?>(null);

        public Task SaveAsync(RevisionRecord revision, CancellationToken ct = default)
        {
            SavedRevisions.Add(revision);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RevisionRecord>> ListAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RevisionRecord>>([]);
    }

    private sealed class RecordingWorkingRevisionRepository : IWorkingRevisionRepository
    {
        public List<WorkingRevision> SavedRevisions { get; } = [];

        public Task<WorkingRevision?> LoadAsync(ProjectId projectId, CancellationToken ct = default) => Task.FromResult<WorkingRevision?>(null);

        public Task SaveAsync(WorkingRevision revision, CancellationToken ct = default)
        {
            SavedRevisions.Add(revision);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCommandJournalRepository : ICommandJournalRepository
    {
        public List<CommandJournalEntry> Entries { get; } = [];

        public Task AppendAsync(CommandJournalEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CommandJournalEntry>> LoadForRevisionAsync(RevisionId revisionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CommandJournalEntry>>([]);
    }

    private sealed class RecordingExplanationRepository : IExplanationRepository
    {
        public List<ExplanationNodeRecord> Nodes { get; } = [];

        public Task AppendNodeAsync(ExplanationNodeRecord node, CancellationToken ct = default)
        {
            Nodes.Add(node);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExplanationNodeRecord>> LoadForEntityAsync(string entityId, RevisionId revisionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExplanationNodeRecord>>([]);

        public Task<IReadOnlyList<ExplanationNodeRecord>> LoadForCommandAsync(CommandId commandId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExplanationNodeRecord>>([]);
    }

    private sealed class RecordingValidationHistoryRepository : IValidationHistoryRepository
    {
        public List<(RevisionId RevisionId, IReadOnlyList<ValidationIssueRecord> Issues)> SavedIssues { get; } = [];

        public Task SaveIssuesAsync(RevisionId revisionId, IReadOnlyList<ValidationIssueRecord> issues, CancellationToken ct = default)
        {
            SavedIssues.Add((revisionId, issues));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ValidationIssueRecord>> LoadAsync(RevisionId revisionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ValidationIssueRecord>>([]);
    }

    private sealed class ThrowingValidationHistoryRepository : IValidationHistoryRepository
    {
        public Task SaveIssuesAsync(RevisionId revisionId, IReadOnlyList<ValidationIssueRecord> issues, CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<ValidationIssueRecord>> LoadAsync(RevisionId revisionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ValidationIssueRecord>>([]);
    }

    private sealed class RecordingAutosaveCheckpointRepository : IAutosaveCheckpointRepository
    {
        public List<AutosaveCheckpoint> SavedCheckpoints { get; } = [];

        public Task<AutosaveCheckpoint?> FindByProjectAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<AutosaveCheckpoint?>(SavedCheckpoints.LastOrDefault(checkpoint => checkpoint.ProjectId == projectId));

        public Task SaveAsync(AutosaveCheckpoint checkpoint, CancellationToken ct = default)
        {
            SavedCheckpoints.Add(checkpoint);
            return Task.CompletedTask;
        }

        public Task MarkCleanAsync(ProjectId projectId, DateTimeOffset savedAt, CancellationToken ct = default)
        {
            SavedCheckpoints.Add(new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, RevisionId.New(), savedAt, null, true));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWhyEngine : IWhyEngine
    {
        public IReadOnlyList<ExplanationNodeId> RecordCommand(IDesignCommand command, IReadOnlyList<StateDelta> deltas) => [];

        public ExplanationNodeId RecordDecision(CommandId commandId, int stageNumber, string decisionType, string description, IReadOnlyList<string>? affectedEntityIds = null, IReadOnlyDictionary<string, string>? context = null, ExplanationRuleRecord? rule = null) =>
            ExplanationNodeId.New();

        public ExplanationNodeId RecordDecisionWithEdges(CommandId commandId, int stageNumber, string decisionType, string description, IReadOnlyList<ExplanationEdge> edges, IReadOnlyList<string>? affectedEntityIds = null, IReadOnlyDictionary<string, string>? context = null, ExplanationRuleRecord? rule = null) =>
            ExplanationNodeId.New();

        public IReadOnlyList<ExplanationNodeId> RecordUndo(CommandMetadata commandMetadata, IReadOnlyList<StateDelta> deltas, IReadOnlyList<ExplanationNodeId> priorExplanationNodeIds) => [];

        public IReadOnlyList<ExplanationNodeId> RecordRedo(CommandMetadata commandMetadata, IReadOnlyList<StateDelta> deltas, IReadOnlyList<ExplanationNodeId> priorExplanationNodeIds) => [];

        public IReadOnlyList<ExplanationNode> GetEntityHistory(string entityId) => [];

        public IReadOnlyList<ExplanationNode> GetCommandExplanation(CommandId commandId) => [];

        public ExplanationNode? GetCommandRoot(CommandId commandId) => null;

        public IReadOnlyList<ExplanationNode> GetStageDecisions(int stageNumber) => [];

        public IReadOnlyList<ExplanationNode> GetDecisionsByRule(string ruleId) => [];

        public IReadOnlyList<ExplanationNode> GetDecisionTrail(string entityId) => [];

        public ExplanationNode? GetPropertyExplanation(string entityId, string propertyName) => null;

        public IReadOnlyList<ExplanationNode> GetNodesByStatus(ExplanationNodeStatus status) => [];

        public IReadOnlyList<ExplanationNode> GetAllNodes() => [];
    }

    private sealed class TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Commit test", [CabinetId.New().Value.ToString()]);

        public string CommandType => "test.commit";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }

    private static async Task<int> CountRowsAsync(SqliteTestFixture fixture, string query)
    {
        await using var connection = (Microsoft.Data.Sqlite.SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
