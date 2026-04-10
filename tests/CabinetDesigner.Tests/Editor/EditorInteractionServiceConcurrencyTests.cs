using System.Threading;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;
using Xunit;

namespace CabinetDesigner.Tests.Editor;

/// <summary>
/// Tests that verify drag/mode transitions in <see cref="EditorInteractionService"/> remain
/// valid under rapid event sequences and that <see cref="EditorSession.EndDrag"/> is always
/// reached after a commit (the <c>finally</c> guarantee).
/// </summary>
public sealed class EditorInteractionServiceConcurrencyTests
{
    // ------------------------------------------------------------------
    // EndDrag-after-commit guarantee
    // ------------------------------------------------------------------

    /// <summary>
    /// Verifies that after <see cref="EditorInteractionService.OnDragCommittedAsync"/> returns,
    /// the session is always back in Idle state — even when the commit executor itself yields
    /// asynchronously — because the <c>finally</c> block is guaranteed to run before the
    /// awaitable completes (ConfigureAwait(true) preserves the call-site context).
    /// </summary>
    [Fact]
    public async Task OnDragCommittedAsync_AlwaysEndsInIdle_WhenCommitSucceeds()
    {
        var (service, session, _) = BuildService();
        service.BeginPlaceCabinet("base-24", Length.FromInches(24m), Length.FromInches(24m), 238d, 0d);
        service.OnDragMoved(238d, 0d);

        var result = await service.OnDragCommittedAsync();

        Assert.True(result.Success);
        Assert.Equal(EditorMode.Idle, session.Mode);
        Assert.Null(session.ActiveDrag);
    }

    /// <summary>
    /// Verifies that the session returns to Idle even when the commit executor reports failure.
    /// </summary>
    [Fact]
    public async Task OnDragCommittedAsync_AlwaysEndsInIdle_WhenCommitFails()
    {
        var (service, session, _) = BuildService(commitFails: true);
        service.BeginPlaceCabinet("base-24", Length.FromInches(24m), Length.FromInches(24m), 238d, 0d);

        var result = await service.OnDragCommittedAsync();

        Assert.False(result.Success);
        Assert.Equal(EditorMode.Idle, session.Mode);
        Assert.Null(session.ActiveDrag);
    }

    /// <summary>
    /// Verifies that the session returns to Idle even when the commit executor throws.
    /// </summary>
    [Fact]
    public async Task OnDragCommittedAsync_AlwaysEndsInIdle_WhenCommitThrows()
    {
        var (service, session, _) = BuildService(commitThrows: true);
        service.BeginPlaceCabinet("base-24", Length.FromInches(24m), Length.FromInches(24m), 238d, 0d);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OnDragCommittedAsync());

        Assert.Equal(EditorMode.Idle, session.Mode);
        Assert.Null(session.ActiveDrag);
    }

    // ------------------------------------------------------------------
    // Rapid drag transition sequences
    // ------------------------------------------------------------------

    /// <summary>
    /// Simulates rapid Begin → Abort cycles to confirm the session toggles between
    /// MovingCabinet and Idle without becoming stuck.
    /// </summary>
    [Fact]
    public void RapidBeginAbort_KeepsSessionConsistent()
    {
        var (service, session, cabinetId) = BuildService();

        for (var i = 0; i < 50; i++)
        {
            service.BeginMoveCabinet(cabinetId, 12d, 0d);
            Assert.Equal(EditorMode.MovingCabinet, session.Mode);

            service.OnDragAborted();
            Assert.Equal(EditorMode.Idle, session.Mode);
            Assert.Null(session.ActiveDrag);
        }
    }

    /// <summary>
    /// Verifies that <see cref="EditorInteractionService.OnDragMoved"/> returns a stable
    /// Invalid result when there is no active drag (e.g. abort arrived before the move event).
    /// </summary>
    [Fact]
    public void OnDragMoved_WithNoActiveDrag_ReturnsInvalid()
    {
        var (service, _, _) = BuildService();

        var result = service.OnDragMoved(100d, 0d);

        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Verifies that calling OnDragAborted when no drag is active is a no-op.
    /// </summary>
    [Fact]
    public void OnDragAborted_WhenNoDragActive_IsNoOp()
    {
        var (service, session, _) = BuildService();

        // Must not throw.
        service.OnDragAborted();

        Assert.Equal(EditorMode.Idle, session.Mode);
    }

    // ------------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------------

    private static (EditorInteractionService Service, EditorSession Session, CabinetId CabinetId)
        BuildService(bool commitFails = false, bool commitThrows = false)
    {
        var runId = RunId.New();
        var cabinetId = CabinetId.New();
        var scene = new EditorSceneSnapshot(
        [
            new RunSceneView(
                runId,
                Point2D.Origin,
                new Point2D(120m, 0m),
                Vector2D.UnitX,
                Length.FromInches(120m),
                [
                    new CabinetSceneView(
                        cabinetId,
                        runId,
                        0,
                        Length.FromInches(24m),
                        Length.FromInches(24m),
                        Point2D.Origin,
                        new Point2D(24m, 0m))
                ])
        ]);

        var session = new EditorSession();
        var service = new EditorInteractionService(
            session,
            new StubSceneGraph(scene, runId),
            new DefaultSnapResolver(
            [
                new RunEndpointSnapCandidateSource(),
                new CabinetFaceSnapCandidateSource(),
                new GridSnapCandidateSource()
            ]),
            new StubPreviewExecutor(),
            new StubCommitExecutor(commitFails, commitThrows),
            new StubClock());

        return (service, session, cabinetId);
    }

    private sealed class StubSceneGraph : IEditorSceneGraph
    {
        private readonly EditorSceneSnapshot _scene;
        private readonly RunId _hitRunId;

        public StubSceneGraph(EditorSceneSnapshot scene, RunId hitRunId)
        {
            _scene = scene;
            _hitRunId = hitRunId;
        }

        public EditorSceneSnapshot Capture() => _scene;
        public RunId? HitTestRun(Point2D worldPoint, Length hitRadius) => _hitRunId;
    }

    private sealed class StubPreviewExecutor : IPreviewCommandExecutor
    {
        public DragPreviewResult Preview(IDesignCommand command) =>
            new(true, command, null);
    }

    private sealed class StubCommitExecutor : ICommitCommandExecutor
    {
        private readonly bool _fail;
        private readonly bool _throws;

        public StubCommitExecutor(bool fail, bool throws)
        {
            _fail = fail;
            _throws = throws;
        }

        public Task<DragCommitResult> ExecuteAsync(IDesignCommand command, CancellationToken ct = default)
        {
            if (_throws)
            {
                throw new InvalidOperationException("Simulated commit failure.");
            }

            return Task.FromResult(_fail
                ? DragCommitResult.Failed("Simulated rejection.")
                : new DragCommitResult(true, command, null));
        }
    }

    private sealed class StubClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.UnixEpoch;
    }
}
