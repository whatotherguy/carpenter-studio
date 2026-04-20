using System.Threading;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task OpenProjectAsync_UsesSelectedFilePathAndPersistsNormalizedPath()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 18, 12, 30, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var currentState = new RecordingCurrentState();
        var seeded = SeedPersistedProject(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock.Now, filePath: null, isClean: true);
        currentState.SetCurrentState(seeded);
        var service = CreateService(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock, out _, currentState);

        var summary = await service.OpenProjectAsync(@".\projects\shop-a.db");

        var expectedPath = Path.GetFullPath(@".\projects\shop-a.db");
        Assert.Equal(expectedPath, summary.FilePath);
        Assert.Equal(expectedPath, projectRepository.SavedProjects.Single(project => project.Id == seeded.Project.Id).FilePath);
    }

    [Fact]
    public async Task OpenProjectAsync_WithMultipleProjectsInStore_Throws()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 18, 13, 0, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        SeedPersistedProject(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock.Now, @"C:\projects\a.db", true);
        SeedPersistedProject(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock.Now.AddMinutes(1), @"C:\projects\b.db", true);
        var service = CreateService(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock, out _);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenProjectAsync(@"C:\projects\b.db"));

        Assert.Contains("Ambiguous project state", exception.Message);
    }

    [Fact]
    public async Task CreateProjectAsync_PersistsEmptyWorkingRevision()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 18, 14, 0, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var currentState = new RecordingCurrentState();
        var service = CreateService(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock, out _, currentState);

        var summary = await service.CreateProjectAsync("Shop A");

        var saved = Assert.Single(workingRevisionRepository.SavedWorkingRevisions);
        Assert.Equal(summary.ProjectId, saved.Revision.ProjectId.Value);
        Assert.Empty(saved.Rooms);
        Assert.Empty(saved.Walls);
        Assert.Empty(saved.Runs);
        Assert.Empty(saved.Cabinets);
        Assert.Empty(saved.Parts);
        Assert.NotNull(await workingRevisionRepository.LoadAsync(saved.Revision.ProjectId));
    }

    [Fact]
    public async Task DesignChangedEvent_MarksProjectDirty_AndSaveAsyncMarksItClean()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 18, 15, 0, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var currentState = new RecordingCurrentState();
        var service = CreateService(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock, out var eventBus, currentState);

        await service.CreateProjectAsync("Shop A");
        checkpointRepository.CleanMarks.Clear();

        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "layout.add", true, [], [], [])));
        Assert.True(service.CurrentProject!.HasUnsavedChanges);

        await service.SaveAsync();

        Assert.False(service.CurrentProject!.HasUnsavedChanges);
        var cleanMark = Assert.Single(checkpointRepository.CleanMarks);
        Assert.Equal(clock.Now, cleanMark.savedAt);
    }

    [Fact]
    public async Task CloseAsync_DoesNotMarkCheckpointClean_WhenProjectIsDirty()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 18, 16, 0, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var currentState = new RecordingCurrentState();
        var service = CreateService(projectRepository, revisionRepository, workingRevisionRepository, checkpointRepository, clock, out var eventBus, currentState);

        await service.CreateProjectAsync("Shop A");
        checkpointRepository.CleanMarks.Clear();
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "layout.add", true, [], [], [])));

        await service.CloseAsync();

        Assert.Empty(checkpointRepository.CleanMarks);
        Assert.Null(service.CurrentProject);
    }

    private static PersistedProjectState SeedPersistedProject(
        RecordingProjectRepository projectRepository,
        RecordingRevisionRepository revisionRepository,
        RecordingWorkingRevisionRepository workingRevisionRepository,
        RecordingCheckpointRepository checkpointRepository,
        DateTimeOffset createdAt,
        string? filePath,
        bool isClean)
    {
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var project = new ProjectRecord(projectId, "Shop A", null, createdAt, createdAt, ApprovalState.Draft, filePath);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, isClean);

        projectRepository.Save(project);
        revisionRepository.Save(revision);
        workingRevisionRepository.Save(workingRevision);
        checkpointRepository.Save(checkpoint);

        return new PersistedProjectState(project, revision, workingRevision, checkpoint);
    }

    private static ProjectService CreateService(
        RecordingProjectRepository projectRepository,
        RecordingRevisionRepository revisionRepository,
        RecordingWorkingRevisionRepository workingRevisionRepository,
        RecordingCheckpointRepository checkpointRepository,
        IClock clock,
        out RecordingEventBus eventBus,
        RecordingCurrentState? currentState = null)
    {
        eventBus = new RecordingEventBus();
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

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed class RecordingAppLogger : IAppLogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log(LogEntry entry) => Entries.Add(entry);
    }

    private sealed class RecordingEventBus : IApplicationEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = [];

        public void Publish<TEvent>(TEvent @event) where TEvent : IApplicationEvent
        {
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
            Task.FromResult<IReadOnlyList<ProjectRecord>>(SavedProjects
                .OrderByDescending(project => project.UpdatedAt)
                .Take(limit)
                .ToArray());
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

        public List<(ProjectId projectId, DateTimeOffset savedAt)> CleanMarks { get; } = [];

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
            CleanMarks.Add((projectId, savedAt));
            var checkpoint = SavedCheckpoints.LastOrDefault(existing => existing.ProjectId == projectId);
            if (checkpoint is not null)
            {
                Save(checkpoint with { SavedAt = savedAt, IsClean = true });
            }

            return Task.CompletedTask;
        }
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
