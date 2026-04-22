using System.Threading;
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

namespace CabinetDesigner.Tests.Services;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task CreateProjectAsync_PersistsEmptyWorkingRevision()
    {
        var fixture = CreateFixture();

        var summary = await fixture.Service.CreateProjectAsync("Shop A");

        var savedRevision = Assert.Single(fixture.WorkingRevisionRepository.SavedWorkingRevisions);
        Assert.Equal(summary.ProjectId, savedRevision.Revision.ProjectId.Value);
        Assert.Empty(savedRevision.Rooms);
        Assert.Empty(savedRevision.Walls);
        Assert.Empty(savedRevision.Runs);
        Assert.Empty(savedRevision.Cabinets);
        Assert.Empty(savedRevision.Parts);
    }

    [Fact]
    public async Task CreateProjectAsync_RoundTrip_OpenAgain_ReturnsSameProjectId()
    {
        var fixture = CreateFixture();

        var created = await fixture.Service.CreateProjectAsync("Shop A");
        var reopened = await fixture.Service.OpenProjectAsync(new ProjectId(created.ProjectId));

        Assert.Equal(created.ProjectId, reopened.ProjectId);
        Assert.Equal(created.Name, reopened.Name);
    }

    [Fact]
    public async Task OpenProjectAsync_MultipleMatches_Throws()
    {
        var fixture = CreateFixture();
        var created = await fixture.Service.CreateProjectAsync("Shop A");
        var duplicate = new ProjectRecord(new ProjectId(created.ProjectId), "Shop A 2", null, fixture.Clock.Now, fixture.Clock.Now, ApprovalState.Draft);
        fixture.ProjectRepository.SavedProjects.Add(duplicate);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.OpenProjectAsync(new ProjectId(created.ProjectId)));

        Assert.Contains("Multiple projects matched", exception.Message);
    }

    private static Fixture CreateFixture()
    {
        var projectRepository = new RecordingProjectRepository();
        var revisionRepository = new RecordingRevisionRepository();
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();
        var checkpointRepository = new RecordingCheckpointRepository();
        var clock = new FixedClock();
        var eventBus = new RecordingEventBus();
        var service = new ProjectService(
            projectRepository,
            revisionRepository,
            workingRevisionRepository,
            checkpointRepository,
            new RecordingUnitOfWork(),
            eventBus,
            clock,
            new RecordingAppLogger());

        return new Fixture(service, projectRepository, workingRevisionRepository, clock);
    }

    private sealed record Fixture(ProjectService Service, RecordingProjectRepository ProjectRepository, RecordingWorkingRevisionRepository WorkingRevisionRepository, FixedClock Clock);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.Parse("2026-04-18T12:00:00Z");
    }

    private sealed class RecordingAppLogger : IAppLogger
    {
        public void Log(LogEntry entry)
        {
        }
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
            Task.FromResult<IReadOnlyList<ProjectRecord>>(SavedProjects.Take(limit).ToArray());
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
            SavedWorkingRevisions.Add(revision);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCheckpointRepository : IAutosaveCheckpointRepository
    {
        public Task<AutosaveCheckpoint?> FindByProjectAsync(ProjectId projectId, CancellationToken ct = default) => Task.FromResult<AutosaveCheckpoint?>(null);
        public Task SaveAsync(AutosaveCheckpoint checkpoint, CancellationToken ct = default) => Task.CompletedTask;
        public Task MarkCleanAsync(ProjectId projectId, DateTimeOffset savedAt, CancellationToken ct = default) => Task.CompletedTask;
    }
}
