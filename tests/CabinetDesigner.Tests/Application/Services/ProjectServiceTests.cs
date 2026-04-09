using System.Threading;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Diagnostics;
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
    public async Task CreateProjectAsync_UsesClockAndSeedsCleanCheckpoint()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 8, 12, 30, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var service = CreateService(projectRepository, revisionRepository, checkpointRepository, clock);

        var summary = await service.CreateProjectAsync("Shop A");

        Assert.Equal(clock.Now, projectRepository.SavedProjects.Single().CreatedAt);
        Assert.Equal(clock.Now, revisionRepository.SavedRevisions.Single().CreatedAt);
        Assert.True(checkpointRepository.SavedCheckpoints.Single().IsClean);
        Assert.Equal(summary.ProjectId, service.CurrentProject!.ProjectId);
    }

    [Fact]
    public async Task SaveAsync_MarksCheckpointCleanUsingClock()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 8, 13, 0, 0, TimeSpan.Zero));
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var service = CreateService(projectRepository, revisionRepository, checkpointRepository, clock);

        await service.CreateProjectAsync("Shop A");
        checkpointRepository.CleanMarks.Clear();

        await service.SaveAsync();

        var cleanMark = Assert.Single(checkpointRepository.CleanMarks);
        Assert.Equal(clock.Now, cleanMark.savedAt);
        Assert.False(service.CurrentProject!.HasUnsavedChanges);
    }

    private static ProjectService CreateService(
        RecordingProjectRepository projectRepository,
        RecordingRevisionRepository revisionRepository,
        RecordingCheckpointRepository checkpointRepository,
        IClock clock) =>
        new(
            projectRepository,
            revisionRepository,
            new RecordingWorkingRevisionRepository(),
            checkpointRepository,
            new RecordingUnitOfWork(),
            new RecordingEventBus(),
            clock,
            new RecordingAppLogger());

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
        public void Publish<TEvent>(TEvent @event) where TEvent : IApplicationEvent
        {
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent
        {
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent
        {
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
        public Task<WorkingRevision?> LoadAsync(ProjectId projectId, CancellationToken ct = default) => Task.FromResult<WorkingRevision?>(null);

        public Task SaveAsync(WorkingRevision revision, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class RecordingCheckpointRepository : IAutosaveCheckpointRepository
    {
        public List<AutosaveCheckpoint> SavedCheckpoints { get; } = [];

        public List<(ProjectId projectId, DateTimeOffset savedAt)> CleanMarks { get; } = [];

        public Task<AutosaveCheckpoint?> FindByProjectAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<AutosaveCheckpoint?>(SavedCheckpoints.LastOrDefault(checkpoint => checkpoint.ProjectId == projectId));

        public Task SaveAsync(AutosaveCheckpoint checkpoint, CancellationToken ct = default)
        {
            SavedCheckpoints.Add(checkpoint);
            return Task.CompletedTask;
        }

        public Task MarkCleanAsync(ProjectId projectId, DateTimeOffset savedAt, CancellationToken ct = default)
        {
            CleanMarks.Add((projectId, savedAt));
            return Task.CompletedTask;
        }
    }
}
