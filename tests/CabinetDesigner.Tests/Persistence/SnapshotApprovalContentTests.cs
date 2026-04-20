using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using System.Threading;
using Xunit;

namespace CabinetDesigner.Tests.Persistence;

public sealed class SnapshotApprovalContentTests
{
    [Fact]
    public async Task ApproveRevision_SnapshotBlob_ContainsManufacturingAndCost()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-18T12:00:00Z");
        var state = CreateState(createdAt);
        var snapshotRepository = new RecordingSnapshotRepository();
        var packaging = CreatePackagingResult(state.Revision.Id, createdAt);
        var service = new SnapshotService(
            new RecordingUnitOfWork(),
            new RecordingProjectRepository(state.Project),
            new RecordingRevisionRepository(state.Revision),
            snapshotRepository,
            new RecordingWorkingRevisionSource(state),
            new RecordingValidationHistoryRepository(),
            new RecordingPackagingResultStore(packaging),
            new RecordingEventBus(),
            new FixedClock(createdAt));

        await service.ApproveRevisionAsync("Rev A");

        var snapshot = Assert.Single(snapshotRepository.WrittenSnapshots);
        Assert.Equal(packaging.ManufacturingBlob, snapshot.ManufacturingBlob);
        Assert.Equal(packaging.EstimateBlob, snapshot.EstimateBlob);
        Assert.Contains("cut_list", snapshot.ManufacturingBlob, StringComparison.Ordinal);
        Assert.Contains("cost_total", snapshot.EstimateBlob, StringComparison.Ordinal);
    }

    private static PersistedProjectState CreateState(DateTimeOffset createdAt)
    {
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
            SnapshotId = $"snap:{revisionId.Value:D}:feedfacecafebeef",
            RevisionId = revisionId,
            CreatedAt = createdAt,
            ContentHash = "feedfacecafebeef0123456789abcdef0123456789abcdef0123456789abcdef",
            Summary = new CabinetDesigner.Application.Pipeline.StageResults.SnapshotSummary(1, 1, 2, 0, 211.42m),
            DesignBlob = $"{{\"schema_version\":1,\"revision_id\":\"{revisionId.Value}\",\"payload\":{{\"design\":true}}}}",
            PartsBlob = $"{{\"schema_version\":1,\"revision_id\":\"{revisionId.Value}\",\"payload\":{{\"parts\":[{{\"part_id\":\"part:1\"}}]}}}}",
            ManufacturingBlob = $"{{\"schema_version\":1,\"revision_id\":\"{revisionId.Value}\",\"payload\":{{\"cut_list\":[{{\"part_id\":\"part:1\"}}],\"constraints\":[]}}}}",
            InstallBlob = $"{{\"schema_version\":1,\"revision_id\":\"{revisionId.Value}\",\"payload\":{{\"install_steps\":[{{\"step_key\":\"install:1\"}}]}}}}",
            EstimateBlob = $"{{\"schema_version\":1,\"revision_id\":\"{revisionId.Value}\",\"payload\":{{\"cost_total\":211.42}}}}",
            ValidationBlob = $"{{\"schema_version\":1,\"revision_id\":\"{revisionId.Value}\",\"payload\":{{\"issues\":[]}}}}",
            ExplanationBlob = $"{{\"schema_version\":1,\"revision_id\":\"{revisionId.Value}\",\"payload\":{{\"explanation\":[]}}}}"
        };

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
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
        public Task<IReadOnlyList<ProjectRecord>> ListRecentAsync(int limit, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ProjectRecord>>([project]);
    }

    private sealed class RecordingRevisionRepository(RevisionRecord revision) : IRevisionRepository
    {
        public Task<RevisionRecord?> FindAsync(RevisionId id, CancellationToken ct = default) => Task.FromResult<RevisionRecord?>(revision);
        public Task<RevisionRecord?> FindWorkingAsync(ProjectId projectId, CancellationToken ct = default) => Task.FromResult<RevisionRecord?>(revision);
        public Task SaveAsync(RevisionRecord revision, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RevisionRecord>> ListAsync(ProjectId projectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RevisionRecord>>([revision]);
    }

    private sealed class RecordingWorkingRevisionSource(PersistedProjectState state) : IWorkingRevisionSource
    {
        public PersistedProjectState CaptureCurrentState(PartGenerationResult? partResult = null) => state;
    }

    private sealed class RecordingValidationHistoryRepository : IValidationHistoryRepository
    {
        public Task SaveIssuesAsync(RevisionId revisionId, IReadOnlyList<ValidationIssueRecord> issues, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ValidationIssueRecord>> LoadAsync(RevisionId revisionId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ValidationIssueRecord>>([]);
    }

    private sealed class RecordingPackagingResultStore(PackagingResult current) : IPackagingResultStore
    {
        public PackagingResult? Current { get; private set; } = current;
        public void Update(PackagingResult result) => Current = result;
        public void Clear() => Current = null;
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

    private sealed class RecordingEventBus : IApplicationEventBus
    {
        public void Publish<TEvent>(TEvent @event) where TEvent : IApplicationEvent { }
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent { }
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent { }
    }
}
