using System.Threading;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;
using Xunit;

namespace CabinetDesigner.Tests.Editor;

/// <summary>
/// Focused integration-style scenario tests for <see cref="EditorInteractionService"/>.
/// Each test exercises a complete user-visible gesture from begin to commit or abort,
/// verifying meaningful command values and resulting session state rather than just
/// confirming that calls occurred.
/// </summary>
/// <remarks>
/// Viewport default is 10 px/inch with no offset, so world coordinates equal
/// screen pixel coordinates divided by 10.
/// </remarks>
public sealed class EditorInteractionScenarioTests
{
    // =========================================================================
    // Scenario 1: Move cabinet — drag to end of run
    //   Verifies the committed MoveCabinetCommand carries the correct cabinet ID,
    //   source/target run IDs, and an insert index that places the cabinet after
    //   all existing cabinets it was dragged past.
    // =========================================================================

    [Fact]
    public async Task MoveDrag_ToEndOfRun_CommitsCorrectMoveCabinetCommand()
    {
        // Arrange: run 0–120", Cabinet A at 0–24" (index 0), Cabinet B at 24–48" (index 1).
        var (service, session, f) = ScenarioBuilder.TwoCabinetRun();

        // Begin move on Cabinet A at its midpoint (screenX=120 → 12" world).
        service.BeginMoveCabinet(f.CabinetAId, screenX: 120d, screenY: 0d);
        Assert.Equal(EditorMode.MovingCabinet, session.Mode);

        // Move the cursor past both cabinets (screenX=700 → 70" world).
        service.OnDragMoved(700d, 0d);

        var result = await service.OnDragCommittedAsync();

        // Command carries the right IDs and places the cabinet after Cabinet B.
        Assert.True(result.Success);
        var cmd = Assert.IsType<MoveCabinetCommand>(result.CommittedCommand);
        Assert.Equal(f.CabinetAId, cmd.CabinetId);
        Assert.Equal(f.RunId, cmd.SourceRunId);
        Assert.Equal(f.RunId, cmd.TargetRunId);
        Assert.Equal(1, cmd.TargetIndex); // After Cabinet B

        // Session returns to Idle with no residual drag state.
        Assert.Equal(EditorMode.Idle, session.Mode);
        Assert.Null(session.ActiveDrag);
    }

    // =========================================================================
    // Scenario 2: Move cabinet — drag to beginning of run
    //   Verifies that dragging a cabinet to a position before all existing
    //   cabinets produces TargetIndex = 0.
    // =========================================================================

    [Fact]
    public async Task MoveDrag_ToBeginningOfRun_CommitsInsertIndexZero()
    {
        // Arrange: two-cabinet run. Move Cabinet B (24–48") before Cabinet A.
        var (service, _, f) = ScenarioBuilder.TwoCabinetRun();

        // Begin move on Cabinet B at its midpoint (screenX=360 → 36" world).
        service.BeginMoveCabinet(f.CabinetBId, screenX: 360d, screenY: 0d);

        // Drag to before Cabinet A (screenX=10 → 1" world).
        service.OnDragMoved(10d, 0d);

        var result = await service.OnDragCommittedAsync();

        Assert.True(result.Success);
        var cmd = Assert.IsType<MoveCabinetCommand>(result.CommittedCommand);
        Assert.Equal(f.CabinetBId, cmd.CabinetId);
        Assert.Equal(0, cmd.TargetIndex); // Before Cabinet A
    }

    // =========================================================================
    // Scenario 3: Move drag preserves cabinet dimensions
    //   Verifies that both the original width and the non-standard depth survive
    //   intact in the DragContext for the lifetime of the move gesture.
    // =========================================================================

    [Fact]
    public void MoveDrag_PreservesBothWidthAndDepthInDragContext()
    {
        // Use a non-standard depth so that any accidental reset to a default is caught.
        var (service, session, f) = ScenarioBuilder.SingleCabinetRun(width: 30m, depth: 21m);

        service.BeginMoveCabinet(f.CabinetAId, screenX: 150d, screenY: 0d);

        Assert.Equal(Length.FromInches(30m), session.ActiveDrag!.NominalWidth);
        Assert.Equal(Length.FromInches(21m), session.ActiveDrag!.NominalDepth);
    }

    // =========================================================================
    // Scenario 4: Resize drag — correct new width from cursor geometry
    //   Verifies that the committed ResizeCabinetCommand carries the new width
    //   computed from the right-edge cursor position, not from a stored nominal.
    // =========================================================================

    [Fact]
    public async Task ResizeDrag_CursorAt36Inches_CommitsNewWidthOf36Inches()
    {
        // Cabinet A: width=24", leftFace at origin.
        var (service, session, f) = ScenarioBuilder.SingleCabinetRun(width: 24m, depth: 24m);

        // Begin resize at the right edge (screenX=240 → 24" world).
        service.BeginResizeCabinet(f.CabinetAId, screenX: 240d, screenY: 0d);
        Assert.Equal(EditorMode.ResizingCabinet, session.Mode);

        // Drag the right edge to 36" (screenX=360).
        service.OnDragMoved(360d, 0d);

        var result = await service.OnDragCommittedAsync();

        Assert.True(result.Success);
        var cmd = Assert.IsType<ResizeCabinetCommand>(result.CommittedCommand);
        Assert.Equal(f.CabinetAId, cmd.CabinetId);
        Assert.Equal(Length.FromInches(24m), cmd.PreviousNominalWidth);
        Assert.Equal(Length.FromInches(36m), cmd.NewNominalWidth);
        Assert.Equal(EditorMode.Idle, session.Mode);
    }

    // =========================================================================
    // Scenario 5: Resize preserves unaffected dimensions (depth not mutated)
    //   Depth is not part of the resize command; verifies it is not accidentally
    //   cleared or overwritten in the DragContext during the gesture.
    // =========================================================================

    [Fact]
    public void ResizeDrag_DepthPreservedInDragContextThroughoutGesture()
    {
        // Non-standard depth to detect any unintended reset.
        var (service, session, f) = ScenarioBuilder.SingleCabinetRun(width: 24m, depth: 30m);

        // Begin resize — depth must be recorded immediately.
        service.BeginResizeCabinet(f.CabinetAId, screenX: 240d, screenY: 0d);
        Assert.Equal(Length.FromInches(30m), session.ActiveDrag!.NominalDepth);

        // Move the cursor — UpdateDragCursor must not touch NominalDepth.
        service.OnDragMoved(360d, 0d);
        Assert.Equal(Length.FromInches(30m), session.ActiveDrag!.NominalDepth);
    }

    // =========================================================================
    // Scenario 6: Invalid drag target — drag over empty canvas space
    //   When the cursor is over an area with no run, OnDragMoved must return
    //   an Invalid result with a non-empty human-readable rejection reason.
    // =========================================================================

    [Fact]
    public void OnDragMoved_WhenDragIsOverEmptyCanvas_ReturnsInvalidWithReason()
    {
        // The NullHitSceneGraph always returns null from HitTestRun.
        // BeginMoveCabinet succeeds because it falls back to cabinet.RunId,
        // but every subsequent OnDragMoved receives a null hit → TargetRunId
        // is set to null → no valid command can be built → Invalid result.
        var (service, _, cabinetId) = ScenarioBuilder.BuildServiceWithNullHitTestGraph();

        service.BeginMoveCabinet(cabinetId, screenX: 120d, screenY: 0d);
        var preview = service.OnDragMoved(9999d, 0d);

        Assert.False(preview.IsValid);
        Assert.NotNull(preview.RejectionReason);
        Assert.NotEmpty(preview.RejectionReason!);
    }

    // =========================================================================
    // Scenario 7: No active drag — commit returns a failure with a reason
    //   OnDragCommittedAsync called with no drag active (e.g. duplicate event)
    //   must return a failed result with a descriptive message.
    // =========================================================================

    [Fact]
    public async Task OnDragCommittedAsync_WithNoActiveDrag_ReturnsFailedWithReason()
    {
        var (service, session, _) = ScenarioBuilder.TwoCabinetRun();
        Assert.Equal(EditorMode.Idle, session.Mode); // Guard: no drag is active.

        var result = await service.OnDragCommittedAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.NotEmpty(result.FailureReason!);
        Assert.Equal(EditorMode.Idle, session.Mode); // Mode must stay Idle.
    }

    // =========================================================================
    // Scenario 8: Drag abort — leaves session fully clean
    //   OnDragAborted called mid-move must reset Mode, ActiveDrag, and the
    //   recorded snap winner back to a fully idle, blank state.
    // =========================================================================

    [Fact]
    public void OnDragAborted_DuringMoveDrag_ClearsAllSessionDragState()
    {
        var (service, session, f) = ScenarioBuilder.TwoCabinetRun();

        service.BeginMoveCabinet(f.CabinetAId, 120d, 0d);
        service.OnDragMoved(300d, 0d); // Advances cursor so a snap winner may be recorded.

        Assert.Equal(EditorMode.MovingCabinet, session.Mode);
        Assert.NotNull(session.ActiveDrag);

        service.OnDragAborted();

        Assert.Equal(EditorMode.Idle, session.Mode);
        Assert.Null(session.ActiveDrag);
        Assert.Null(session.PreviousSnapWinner);
    }

    // =========================================================================
    // Scenario 9: Interrupted gesture — double-abort after a failed commit
    //   OnDragCommittedAsync always calls EndDrag in its finally block, even
    //   when the executor rejects the command.  A subsequent OnDragAborted call
    //   (e.g. from an error-handling code path) must be a harmless no-op.
    // =========================================================================

    [Fact]
    public async Task OnDragAborted_AfterCommitFailureCleanedUp_IsNoOp()
    {
        var (service, session, _) = ScenarioBuilder.TwoCabinetRun(commitFails: true);

        // Begin a place-cabinet drag and commit it.  The executor will reject it.
        service.BeginPlaceCabinet("base-24", Length.FromInches(24m), Length.FromInches(24m), 238d, 0d);
        var result = await service.OnDragCommittedAsync();

        // The finally block must have reset the session even on rejection.
        Assert.False(result.Success);
        Assert.Equal(EditorMode.Idle, session.Mode);

        // A second abort (simulating a race or duplicated cleanup call) must not throw.
        service.OnDragAborted();

        Assert.Equal(EditorMode.Idle, session.Mode);
        Assert.Null(session.ActiveDrag);
    }

    // =========================================================================
    // Test infrastructure
    // =========================================================================

    /// <summary>
    /// Carries the stable identifiers produced by a scenario setup so that tests
    /// can assert against them without recreating the values.
    /// </summary>
    private sealed record RunFixture(
        RunId RunId,
        CabinetId CabinetAId,
        CabinetId CabinetBId);

    /// <summary>
    /// Builds pre-wired <see cref="EditorInteractionService"/> instances for common
    /// cabinet-and-run configurations.  All builds use the real DefaultSnapResolver
    /// so that snap logic is exercised; the viewport defaults to 10 px/inch.
    /// </summary>
    private static class ScenarioBuilder
    {
        private static readonly DefaultSnapResolver DefaultResolver = new(
        [
            new RunEndpointSnapCandidateSource(),
            new CabinetFaceSnapCandidateSource(),
            new GridSnapCandidateSource()
        ]);

        /// <summary>
        /// Run 0–120", single cabinet at index 0 with the specified dimensions.
        /// </summary>
        public static (EditorInteractionService Service, EditorSession Session, RunFixture Fixture)
            SingleCabinetRun(decimal width = 24m, decimal depth = 24m, bool commitFails = false)
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
                            Length.FromInches(width),
                            Length.FromInches(depth),
                            Point2D.Origin,
                            new Point2D(width, 0m))
                    ])
            ]);

            var session = new EditorSession();
            var service = BuildService(session, scene, runId, commitFails);
            return (service, session, new RunFixture(runId, cabinetId, default));
        }

        /// <summary>
        /// Run 0–120", Cabinet A at 0–24" (index 0), Cabinet B at 24–48" (index 1).
        /// Both cabinets have width=24", depth=24".
        /// </summary>
        public static (EditorInteractionService Service, EditorSession Session, RunFixture Fixture)
            TwoCabinetRun(bool commitFails = false)
        {
            var runId = RunId.New();
            var cabinetAId = CabinetId.New();
            var cabinetBId = CabinetId.New();

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
                            cabinetAId,
                            runId,
                            0,
                            Length.FromInches(24m),
                            Length.FromInches(24m),
                            Point2D.Origin,
                            new Point2D(24m, 0m)),
                        new CabinetSceneView(
                            cabinetBId,
                            runId,
                            1,
                            Length.FromInches(24m),
                            Length.FromInches(24m),
                            new Point2D(24m, 0m),
                            new Point2D(48m, 0m))
                    ])
            ]);

            var session = new EditorSession();
            var service = BuildService(session, scene, runId, commitFails);
            return (service, session, new RunFixture(runId, cabinetAId, cabinetBId));
        }

        /// <summary>
        /// Builds a service backed by a <see cref="NullHitSceneGraph"/> that always
        /// returns <c>null</c> from <see cref="IEditorSceneGraph.HitTestRun"/>,
        /// simulating a drag gesture that moves over an area with no run.
        /// BeginMoveCabinet still succeeds because it falls back to <c>cabinet.RunId</c>.
        /// </summary>
        public static (EditorInteractionService Service, EditorSession Session, CabinetId CabinetId)
            BuildServiceWithNullHitTestGraph()
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
                new NullHitSceneGraph(scene),
                DefaultResolver,
                new PassThroughPreviewExecutor(),
                new StubCommitExecutor(),
                new StubClock());

            return (service, session, cabinetId);
        }

        private static EditorInteractionService BuildService(
            EditorSession session,
            EditorSceneSnapshot scene,
            RunId hitRunId,
            bool commitFails)
        {
            return new EditorInteractionService(
                session,
                new FixedHitSceneGraph(scene, hitRunId),
                DefaultResolver,
                new PassThroughPreviewExecutor(),
                new StubCommitExecutor(commitFails),
                new StubClock());
        }
    }

    // ------------------------------------------------------------------
    // Test doubles
    // ------------------------------------------------------------------

    /// <summary>
    /// Always returns a fixed <see cref="RunId"/> from <see cref="HitTestRun"/>,
    /// so every cursor position is treated as being "over" that run.
    /// </summary>
    private sealed class FixedHitSceneGraph : IEditorSceneGraph
    {
        private readonly EditorSceneSnapshot _scene;
        private readonly RunId _hitRunId;

        public FixedHitSceneGraph(EditorSceneSnapshot scene, RunId hitRunId)
        {
            _scene = scene;
            _hitRunId = hitRunId;
        }

        public EditorSceneSnapshot Capture() => _scene;
        public RunId? HitTestRun(Point2D worldPoint, Length hitRadius) => _hitRunId;
    }

    /// <summary>
    /// Always returns <c>null</c> from <see cref="HitTestRun"/>, simulating a
    /// gesture dragged over an area with no run.
    /// </summary>
    private sealed class NullHitSceneGraph : IEditorSceneGraph
    {
        private readonly EditorSceneSnapshot _scene;

        public NullHitSceneGraph(EditorSceneSnapshot scene) => _scene = scene;

        public EditorSceneSnapshot Capture() => _scene;
        public RunId? HitTestRun(Point2D worldPoint, Length hitRadius) => null;
    }

    /// <summary>
    /// Returns the command as a valid preview result without any real rendering.
    /// </summary>
    private sealed class PassThroughPreviewExecutor : IPreviewCommandExecutor
    {
        public DragPreviewResult Preview(IDesignCommand command) =>
            new(true, command, null);
    }

    private sealed class StubCommitExecutor : ICommitCommandExecutor
    {
        private readonly bool _fail;

        public StubCommitExecutor(bool fail = false) => _fail = fail;

        public Task<DragCommitResult> ExecuteAsync(IDesignCommand command, CancellationToken ct = default) =>
            Task.FromResult(_fail
                ? DragCommitResult.Failed("Simulated rejection.")
                : new DragCommitResult(true, command, null));
    }

    private sealed class StubClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.UnixEpoch;
    }
}
