using System.Threading;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline;
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
            new RecordingPackagingResultStore(CreatePackagingResult(state.Revision.Id, clock.Now)),
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

    [Fact]
    public async Task GetRevisionHistoryAsync_IsAsync_DoesNotDeadlock()
    {
        // TG8 Regression: ensure GetRevisionHistoryAsync is truly async and returns a Task
        // This test verifies the bug is fixed: no .GetAwaiter().GetResult() on UI thread
        var state = CreateState();
        var service = new SnapshotService(
            new RecordingUnitOfWork(),
            new RecordingProjectRepository(state.Project),
            new RecordingRevisionRepository(state.Revision),
            new RecordingSnapshotRepository(),
            new RecordingWorkingRevisionSource(state),
            new RecordingValidationHistoryRepository(),
            new RecordingPackagingResultStore(CreatePackagingResult(state.Revision.Id, DateTimeOffset.Now)),
            new RecordingEventBus(),
            new FixedClock(DateTimeOffset.Now));

        // Verify it returns a Task and completes without deadlock
        var result = await service.GetRevisionHistoryAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetRevisionHistoryAsync_WithTwoRevisions_ReturnsBothWithCorrectFieldMappings()
    {
        // Test behavior: verify field mapping is correct
        var createdAt = DateTimeOffset.Parse("2026-04-08T12:00:00Z");
        var projectId = ProjectId.New();
        var revisionId1 = RevisionId.New();
        var revisionId2 = RevisionId.New();

        var revision1 = new RevisionRecord(revisionId1, projectId, 1, ApprovalState.UnderReview, createdAt, null, null, "Rev 1");
        var revision2 = new RevisionRecord(revisionId2, projectId, 2, ApprovalState.Approved, createdAt.AddHours(1), null, null, "Rev 2");

        var project = new ProjectRecord(projectId, "Sample", null, createdAt, createdAt, ApprovalState.Draft);
        var workingRevision = new WorkingRevision(revision1, [], [], [], [], []);
        var state = new PersistedProjectState(project, revision1, workingRevision, new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId1, createdAt, null, true));

        var revisionRepository = new RecordingRevisionRepositoryMultiple([revision1, revision2]);

        var service = new SnapshotService(
            new RecordingUnitOfWork(),
            new RecordingProjectRepository(project),
            revisionRepository,
            new RecordingSnapshotRepository(),
            new RecordingWorkingRevisionSource(state),
            new RecordingValidationHistoryRepository(),
            new RecordingPackagingResultStore(CreatePackagingResult(revision1.Id, createdAt)),
            new RecordingEventBus(),
            new FixedClock(createdAt));

        var result = await service.GetRevisionHistoryAsync();

        Assert.Equal(2, result.Count);

        var dto1 = result[0];
        Assert.Equal(revisionId1.Value, dto1.RevisionId);
        Assert.Equal("Rev 1", dto1.Label);
        Assert.Equal(createdAt, dto1.CreatedAt);
        Assert.Equal(ApprovalState.UnderReview.ToString(), dto1.ApprovalState);
        Assert.False(dto1.IsApproved);
        Assert.False(dto1.IsLocked);

        var dto2 = result[1];
        Assert.Equal(revisionId2.Value, dto2.RevisionId);
        Assert.Equal("Rev 2", dto2.Label);
        Assert.Equal(createdAt.AddHours(1), dto2.CreatedAt);
        Assert.Equal(ApprovalState.Approved.ToString(), dto2.ApprovalState);
        Assert.True(dto2.IsApproved);
        Assert.False(dto2.IsLocked);
    }

    [Fact]
    public async Task GetRevisionHistoryAsync_WhenNoProjectStateLoaded_ThrowsInvalidOperationException()
    {
        // Test null guard: should throw InvalidOperationException when no project state is loaded
        var service = new SnapshotService(
            new RecordingUnitOfWork(),
            new RecordingProjectRepository(null!),
            new RecordingRevisionRepository(null!),
            new RecordingSnapshotRepository(),
            new RecordingWorkingRevisionSourceThrowsInvalidOperation(),
            new RecordingValidationHistoryRepository(),
            new RecordingPackagingResultStore(null),
            new RecordingEventBus(),
            new FixedClock(DateTimeOffset.Now));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.GetRevisionHistoryAsync());
        Assert.Equal("No project state is currently loaded.", ex.Message);
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

    private static PackagingResult CreatePackagingResult(RevisionId revisionId, DateTimeOffset createdAt) =>
        new()
        {
            SnapshotId = $"snap:{revisionId.Value:D}:abcdef0123456789",
            RevisionId = revisionId,
            CreatedAt = createdAt,
            ContentHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
            Summary = new CabinetDesigner.Application.Pipeline.StageResults.SnapshotSummary(1, 1, 1, 0, 125m),
            DesignBlob = """{"schema_version":1,"revision_id":"00000000-0000-0000-0000-000000000001","payload":{"design":true}}""",
            PartsBlob = """{"schema_version":1,"revision_id":"00000000-0000-0000-0000-000000000001","payload":{"parts":true}}""",
            ManufacturingBlob = """{"schema_version":1,"revision_id":"00000000-0000-0000-0000-000000000001","payload":{"manufacturing":true}}""",
            InstallBlob = """{"schema_version":1,"revision_id":"00000000-0000-0000-0000-000000000001","payload":{"install":true}}""",
            EstimateBlob = """{"schema_version":1,"revision_id":"00000000-0000-0000-0000-000000000001","payload":{"cost_total":125.0}}""",
            ValidationBlob = """{"schema_version":1,"revision_id":"00000000-0000-0000-0000-000000000001","payload":{"issues":[]}}""",
            ExplanationBlob = """{"schema_version":1,"revision_id":"00000000-0000-0000-0000-000000000001","payload":{"explanation":[]}}"""
        };

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

    private sealed class RecordingPackagingResultStore(PackagingResult? current) : IPackagingResultStore
    {
        public PackagingResult? Current { get; private set; } = current;

        public void Update(PackagingResult result) => Current = result;

        public void Clear() => Current = null;
    }

    private sealed class RecordingRevisionRepositoryMultiple(IReadOnlyList<RevisionRecord> revisions) : IRevisionRepository
    {
        public Task<RevisionRecord?> FindAsync(RevisionId id, CancellationToken ct = default) =>
            Task.FromResult<RevisionRecord?>(revisions.FirstOrDefault(r => r.Id == id));

        public Task<RevisionRecord?> FindWorkingAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<RevisionRecord?>(revisions.FirstOrDefault(r => r.ProjectId == projectId));

        public Task SaveAsync(RevisionRecord revision, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<RevisionRecord>> ListAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RevisionRecord>>(revisions.Where(r => r.ProjectId == projectId).ToArray());
    }

    private sealed class RecordingWorkingRevisionSourceThrowsInvalidOperation : IWorkingRevisionSource
    {
        public PersistedProjectState CaptureCurrentState(PartGenerationResult? partResult = null) =>
            throw new InvalidOperationException("No project state is currently loaded.");
    }
}
