using System.Threading;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class ProjectServiceLifecycleTests
{
    [Fact]
    public async Task OpenProjectAsync_EmitsProjectOpenedEventExactlyOnce()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var eventBus = new RecordingEventBus();
        var seeded = SeedPersistedProject(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock.Now);
        var service = CreateService(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock, eventBus, currentState: null);

        var summary = await service.OpenProjectAsync(seeded.Project.Id);

        Assert.Equal(seeded.Project.Id.Value, summary.ProjectId);
        Assert.Equal(1, eventBus.Count<ProjectOpenedEvent>());
        var opened = Assert.Single(eventBus.OfType<ProjectOpenedEvent>());
        Assert.Equal(seeded.Project.Id.Value, opened.Project.ProjectId);
    }

    [Fact]
    public async Task CloseAsync_EmitsProjectClosedEventExactlyOnce()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 18, 13, 0, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var eventBus = new RecordingEventBus();
        var service = CreateService(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock, eventBus, currentState: null);

        var created = await service.CreateProjectAsync("Shop A");
        eventBus.Clear();

        await service.CloseAsync();

        Assert.Null(service.CurrentProject);
        Assert.Equal(1, eventBus.Count<ProjectClosedEvent>());
        var closed = Assert.Single(eventBus.OfType<ProjectClosedEvent>());
        Assert.Equal(created.ProjectId, closed.ProjectId);
    }

    [Fact]
    public async Task CreateProjectAsync_FollowedByOpenProjectAsync_WithoutSave_PreservesWorkingRevision()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 18, 14, 0, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var eventBus = new RecordingEventBus();
        var currentState = new RecordingCurrentState();
        var service = CreateService(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock, eventBus, currentState);

        var created = await service.CreateProjectAsync("Shop A");
        await service.CloseAsync();

        var reopened = await service.OpenProjectAsync(new ProjectId(created.ProjectId));
        var loaded = Assert.Single(workingRevisionRepository.SavedWorkingRevisions);

        Assert.Equal(created.ProjectId, reopened.ProjectId);
        Assert.Empty(loaded.Rooms);
        Assert.Empty(loaded.Walls);
        Assert.Empty(loaded.Runs);
        Assert.Empty(loaded.Cabinets);
        Assert.Empty(loaded.Parts);
    }

    [Fact]
    public async Task SaveAsync_Failure_PropagatesException()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 18, 15, 0, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new ThrowingCheckpointRepository();
        var eventBus = new RecordingEventBus();
        var service = CreateService(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock, eventBus);

        await service.CreateProjectAsync("Shop A");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync());

        Assert.Equal("boom", exception.Message);
    }

    private static ProjectService CreateService(
        RecordingProjectRepository projectRepository,
        RecordingRevisionRepository revisionRepository,
        RecordingWorkingRevisionRepository workingRevisionRepository,
        IAutosaveCheckpointRepository checkpointRepository,
        IClock clock,
        RecordingEventBus eventBus,
        RecordingCurrentState? currentState = null)
    {
        currentState ??= new RecordingCurrentState();

        return new ProjectService(
            projectRepository,
            revisionRepository,
            workingRevisionRepository,
            checkpointRepository,
            new RecordingUnitOfWork(),
            eventBus,
            clock,
            currentState,
            currentState,
            new RecordingAppLogger());
    }

    private static PersistedProjectState SeedPersistedProject(
        RecordingProjectRepository projectRepository,
        RecordingRevisionRepository revisionRepository,
        RecordingWorkingRevisionRepository workingRevisionRepository,
        RecordingCheckpointRepository checkpointRepository,
        DateTimeOffset createdAt)
    {
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var project = new ProjectRecord(projectId, "Shop A", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);

        projectRepository.Save(project);
        revisionRepository.Save(revision);
        workingRevisionRepository.Save(workingRevision);
        checkpointRepository.Save(checkpoint);

        return new PersistedProjectState(project, revision, workingRevision, checkpoint);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed class RecordingAppLogger : IAppLogger
    {
        public void Log(LogEntry entry)
        {
        }
    }

    private sealed class RecordingEventBus : IApplicationEventBus
    {
        private readonly List<IApplicationEvent> _events = [];
        private readonly Dictionary<Type, List<Delegate>> _handlers = [];

        public IReadOnlyList<IApplicationEvent> Events => _events;

        public int Count<TEvent>() where TEvent : IApplicationEvent => _events.OfType<TEvent>().Count();

        public IEnumerable<TEvent> OfType<TEvent>() where TEvent : IApplicationEvent => _events.OfType<TEvent>();

        public void Clear() => _events.Clear();

        public void Publish<TEvent>(TEvent @event) where TEvent : IApplicationEvent
        {
            _events.Add(@event);

            if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                return;
            }

            foreach (var handler in handlers.Cast<Action<TEvent>>().ToArray())
            {
                handler(@event);
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers = [];
                _handlers[typeof(TEvent)] = handlers;
            }

            handlers.Add(handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public Task BeginAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingProjectRepository : IProjectRepository
    {
        public List<ProjectRecord> SavedProjects { get; } = [];

        public Task<ProjectRecord?> FindAsync(ProjectId id, CancellationToken ct = default) =>
            Task.FromResult<ProjectRecord?>(SavedProjects.LastOrDefault(project => project.Id == id));

        public Task SaveAsync(ProjectRecord project, CancellationToken ct = default)
        {
            Save(project);
            return Task.CompletedTask;
        }

        public void Save(ProjectRecord project)
        {
            var index = SavedProjects.FindIndex(existing => existing.Id == project.Id);
            if (index >= 0)
            {
                SavedProjects[index] = project;
            }
            else
            {
                SavedProjects.Add(project);
            }
        }

        public Task<IReadOnlyList<ProjectRecord>> ListRecentAsync(int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectRecord>>(SavedProjects.OrderByDescending(project => project.UpdatedAt).Take(limit).ToArray());
    }

    private sealed class RecordingRevisionRepository : IRevisionRepository
    {
        public List<RevisionRecord> SavedRevisions { get; } = [];

        public Task<RevisionRecord?> FindAsync(RevisionId id, CancellationToken ct = default) =>
            Task.FromResult<RevisionRecord?>(SavedRevisions.LastOrDefault(revision => revision.Id == id));

        public Task<RevisionRecord?> FindWorkingAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<RevisionRecord?>(SavedRevisions.LastOrDefault(revision => revision.ProjectId == projectId && revision.State == ApprovalState.Draft));

        public Task SaveAsync(RevisionRecord revision, CancellationToken ct = default)
        {
            Save(revision);
            return Task.CompletedTask;
        }

        public void Save(RevisionRecord revision)
        {
            var index = SavedRevisions.FindIndex(existing => existing.Id == revision.Id);
            if (index >= 0)
            {
                SavedRevisions[index] = revision;
            }
            else
            {
                SavedRevisions.Add(revision);
            }
        }

        public Task<IReadOnlyList<RevisionRecord>> ListAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RevisionRecord>>(SavedRevisions.Where(revision => revision.ProjectId == projectId).ToArray());
    }

    private sealed class RecordingWorkingRevisionRepository : IWorkingRevisionRepository
    {
        public List<WorkingRevision> SavedWorkingRevisions { get; } = [];

        public Task<WorkingRevision?> LoadAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<WorkingRevision?>(SavedWorkingRevisions.LastOrDefault(revision => revision.Revision.ProjectId == projectId));

        public Task SaveAsync(WorkingRevision revision, CancellationToken ct = default)
        {
            Save(revision);
            return Task.CompletedTask;
        }

        public void Save(WorkingRevision revision)
        {
            var index = SavedWorkingRevisions.FindIndex(existing => existing.Revision.Id == revision.Revision.Id);
            if (index >= 0)
            {
                SavedWorkingRevisions[index] = revision;
            }
            else
            {
                SavedWorkingRevisions.Add(revision);
            }
        }
    }

    private sealed class RecordingCheckpointRepository : IAutosaveCheckpointRepository
    {
        public List<AutosaveCheckpoint> SavedCheckpoints { get; } = [];

        public Task<AutosaveCheckpoint?> FindByProjectAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<AutosaveCheckpoint?>(SavedCheckpoints.LastOrDefault(checkpoint => checkpoint.ProjectId == projectId));

        public Task SaveAsync(AutosaveCheckpoint checkpoint, CancellationToken ct = default)
        {
            Save(checkpoint);
            return Task.CompletedTask;
        }

        public void Save(AutosaveCheckpoint checkpoint)
        {
            var index = SavedCheckpoints.FindIndex(existing => existing.Id == checkpoint.Id);
            if (index >= 0)
            {
                SavedCheckpoints[index] = checkpoint;
            }
            else
            {
                SavedCheckpoints.Add(checkpoint);
            }
        }

        public Task MarkCleanAsync(ProjectId projectId, DateTimeOffset savedAt, CancellationToken ct = default)
        {
            var checkpoint = SavedCheckpoints.LastOrDefault(existing => existing.ProjectId == projectId);
            if (checkpoint is not null)
            {
                Save(checkpoint with { SavedAt = savedAt, IsClean = true });
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCheckpointRepository : IAutosaveCheckpointRepository
    {
        public Task<AutosaveCheckpoint?> FindByProjectAsync(ProjectId projectId, CancellationToken ct = default) => Task.FromResult<AutosaveCheckpoint?>(null);

        public Task SaveAsync(AutosaveCheckpoint checkpoint, CancellationToken ct = default) => Task.CompletedTask;

        public Task MarkCleanAsync(ProjectId projectId, DateTimeOffset savedAt, CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class RecordingCurrentState : ICurrentPersistedProjectState, IWorkingRevisionSource
    {
        public PersistedProjectState? CurrentState { get; private set; }

        public void SetCurrentState(PersistedProjectState state) => CurrentState = state;

        public void Clear() => CurrentState = null;

        public PersistedProjectState CaptureCurrentState(CabinetDesigner.Application.Pipeline.StageResults.PartGenerationResult? partResult = null) =>
            CurrentState ?? throw new InvalidOperationException("No state captured.");
    }
}
