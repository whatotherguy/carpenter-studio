using System.Threading;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.Validation;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class SnapshotServiceTests
{
    [Fact]
    public async Task ApproveRevisionAsync_UsesClockForSnapshotTimestamp()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 8, 14, 0, 0, TimeSpan.Zero));
        var state = CreateState();
        var snapshotRepository = new RecordingSnapshotRepository();
        var logger = new RecordingAppLogger();
        var eventBus = new RecordingEventBus();

        var service = new SnapshotService(
            new RecordingUnitOfWork(),
            new RecordingProjectRepository(state.Project),
            new RecordingRevisionRepository(state.Revision),
            snapshotRepository,
            new RecordingWorkingRevisionSource(state),
            new RecordingValidationHistoryRepository(),
            eventBus,
            clock,
            logger);

        var result = await service.ApproveRevisionAsync("Rev A");

        Assert.Equal(clock.Now, snapshotRepository.WrittenSnapshots.Single().ApprovedAt);
        Assert.Equal("Rev A", snapshotRepository.WrittenSnapshots.Single().Label);
        Assert.True(result.IsApproved);
        Assert.IsType<RevisionApprovedEvent>(Assert.Single(eventBus.PublishedEvents));
        Assert.Contains(logger.Entries, entry => entry.Message == "Revision approved and snapshot written.");
    }

    private static PersistedProjectState CreateState()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-08T12:00:00Z");
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var project = new ProjectRecord(projectId, "Sample", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.UnderReview, createdAt, null, null, "Rev 1");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);
        return new PersistedProjectState(project, revision, workingRevision, checkpoint);
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
        public List<IApplicationEvent> PublishedEvents { get; } = [];

        public void Publish<TEvent>(TEvent @event) where TEvent : IApplicationEvent => PublishedEvents.Add(@event);

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

    private sealed class RecordingProjectRepository(ProjectRecord project) : IProjectRepository
    {
        public Task<ProjectRecord?> FindAsync(ProjectId id, CancellationToken ct = default) => Task.FromResult<ProjectRecord?>(project);

        public Task SaveAsync(ProjectRecord project, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ProjectRecord>> ListRecentAsync(int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectRecord>>([project]);
    }

    private sealed class RecordingRevisionRepository(RevisionRecord revision) : IRevisionRepository
    {
        public Task<RevisionRecord?> FindAsync(RevisionId id, CancellationToken ct = default) => Task.FromResult<RevisionRecord?>(revision);

        public Task<RevisionRecord?> FindWorkingAsync(ProjectId projectId, CancellationToken ct = default) => Task.FromResult<RevisionRecord?>(revision);

        public Task SaveAsync(RevisionRecord revision, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<RevisionRecord>> ListAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RevisionRecord>>([revision]);
    }

    private sealed class RecordingWorkingRevisionSource(PersistedProjectState state) : IWorkingRevisionSource
    {
        public PersistedProjectState CaptureCurrentState(PartGenerationResult? partResult = null) => state;
    }

    private sealed class RecordingValidationHistoryRepository : IValidationHistoryRepository
    {
        public Task SaveIssuesAsync(RevisionId revisionId, IReadOnlyList<ValidationIssueRecord> issues, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ValidationIssueRecord>> LoadAsync(RevisionId revisionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ValidationIssueRecord>>([]);
    }

    private sealed class RecordingSnapshotRepository : ISnapshotRepository
    {
        public List<ApprovedSnapshot> WrittenSnapshots { get; } = [];

        public Task WriteAsync(ApprovedSnapshot snapshot, CancellationToken ct = default)
        {
            WrittenSnapshots.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<ApprovedSnapshot?> ReadAsync(RevisionId revisionId, CancellationToken ct = default) =>
            Task.FromResult<ApprovedSnapshot?>(WrittenSnapshots.LastOrDefault(snapshot => snapshot.RevisionId == revisionId));

        public Task<IReadOnlyList<CabinetDesigner.Application.Persistence.SnapshotSummary>> ListAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CabinetDesigner.Application.Persistence.SnapshotSummary>>([]);
    }
}
