using System.Threading;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;
using Xunit;

namespace CabinetDesigner.Tests.Editor;

public sealed class EditorInteractionServiceTests
{
    [Fact]
    public async Task DragPreview_UsesPreviewHandlerUntilCommitAndKeepsCommittedPathSeparate()
    {
        var runId = RunId.New();
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
                        CabinetId.New(),
                        runId,
                        0,
                        Length.FromInches(24m),
                        Length.FromInches(24m),
                        Point2D.Origin,
                        new Point2D(24m, 0m))
                ])
        ]);

        var session = new EditorSession();
        var previewHandler = new RecordingPreviewCommandExecutor();
        var designHandler = new RecordingCommitCommandExecutor();
        var service = new EditorInteractionService(
            session,
            new StubSceneGraph(scene, runId),
            new DefaultSnapResolver(
            [
                new RunEndpointSnapCandidateSource(),
                new CabinetFaceSnapCandidateSource(),
                new GridSnapCandidateSource()
            ]),
            previewHandler,
            designHandler,
            new StubClock());

        service.BeginPlaceCabinet("base-24", Length.FromInches(24m), Length.FromInches(24m), 238d, 0d);

        var preview = service.OnDragMoved(238d, 0d);

        Assert.True(preview.IsValid);
        Assert.Equal(1, previewHandler.PreviewCallCount);
        Assert.Equal(0, designHandler.CommitCallCount);
        Assert.Equal(EditorMode.PlacingCabinet, session.Mode);
        var previewCommand = Assert.IsType<AddCabinetToRunCommand>(preview.PreviewCommand);
        Assert.Equal(1, previewCommand.InsertAtIndex);

        var committed = await service.OnDragCommittedAsync();

        Assert.True(committed.Success);
        Assert.Equal(1, previewHandler.PreviewCallCount);
        Assert.Equal(1, designHandler.CommitCallCount);
        Assert.Equal(EditorMode.Idle, session.Mode);
        var committedCommand = Assert.IsType<AddCabinetToRunCommand>(committed.CommittedCommand);
        Assert.Equal(previewCommand.RunId, committedCommand.RunId);
        Assert.Equal(previewCommand.InsertAtIndex, committedCommand.InsertAtIndex);
    }

    [Fact]
    public void DragPreview_PlaceCabinet_NominalDepthFlowsFromDragContextToCommand()
    {
        var runId = RunId.New();
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
                        CabinetId.New(),
                        runId,
                        0,
                        Length.FromInches(24m),
                        Length.FromInches(24m),
                        Point2D.Origin,
                        new Point2D(24m, 0m))
                ])
        ]);

        var session = new EditorSession();
        var previewHandler = new RecordingPreviewCommandExecutor();
        var service = new EditorInteractionService(
            session,
            new StubSceneGraph(scene, runId),
            new DefaultSnapResolver(
            [
                new RunEndpointSnapCandidateSource(),
                new CabinetFaceSnapCandidateSource(),
                new GridSnapCandidateSource()
            ]),
            previewHandler,
            new RecordingCommitCommandExecutor(),
            new StubClock());

        // Place a cabinet with a non-standard 30" depth.
        service.BeginPlaceCabinet("tall-24", Length.FromInches(24m), Length.FromInches(30m), 238d, 0d);

        var preview = service.OnDragMoved(238d, 0d);

        Assert.True(preview.IsValid);
        var previewCommand = Assert.IsType<AddCabinetToRunCommand>(preview.PreviewCommand);
        Assert.Equal(Length.FromInches(30m), previewCommand.NominalDepth);
    }

    [Fact]
    public void BeginMoveCabinet_WithNonStandardDepth_PreservesActualDepthInDragContext()
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
                        Length.FromInches(30m),
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
            new RecordingPreviewCommandExecutor(),
            new RecordingCommitCommandExecutor(),
            new StubClock());

        service.BeginMoveCabinet(cabinetId, 12d, 0d);

        Assert.Equal(Length.FromInches(30m), session.ActiveDrag!.NominalDepth);
    }

    [Fact]
    public void BeginResizeCabinet_WithNonStandardDepth_PreservesActualDepthInDragContext()
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
                        Length.FromInches(30m),
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
            new RecordingPreviewCommandExecutor(),
            new RecordingCommitCommandExecutor(),
            new StubClock());

        service.BeginResizeCabinet(cabinetId, 24d, 0d);

        Assert.Equal(Length.FromInches(30m), session.ActiveDrag!.NominalDepth);
    }

    [Fact]
    public void BeginMoveCabinet_WithStandardDepth_PreservesDepthInDragContext()
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
            new RecordingPreviewCommandExecutor(),
            new RecordingCommitCommandExecutor(),
            new StubClock());

        service.BeginMoveCabinet(cabinetId, 12d, 0d);

        Assert.Equal(Length.FromInches(24m), session.ActiveDrag!.NominalDepth);
    }

    [Fact]
    public void OnDragMoved_WhenNotOverRun_ReturnsPlaceCabinetSpecificRejection()
    {
        var runId = RunId.New();
        var scene = new EditorSceneSnapshot([
            new RunSceneView(
                runId,
                Point2D.Origin,
                new Point2D(120m, 0m),
                Vector2D.UnitX,
                Length.FromInches(120m),
                [])
        ]);

        var session = new EditorSession();
        var service = new EditorInteractionService(
            session,
            new NoHitSceneGraph(scene),
            new DefaultSnapResolver(
            [
                new RunEndpointSnapCandidateSource(),
                new CabinetFaceSnapCandidateSource(),
                new GridSnapCandidateSource()
            ]),
            new RecordingPreviewCommandExecutor(),
            new RecordingCommitCommandExecutor(),
            new StubClock());

        service.BeginPlaceCabinet("base-24", Length.FromInches(24m), Length.FromInches(24m), 0d, 0d);
        var result = service.OnDragMoved(500d, 500d);

        Assert.False(result.IsValid);
        Assert.Equal("Cabinet must be dragged onto a wall run to place it.", result.RejectionReason);
    }

    [Fact]
    public async Task OnDragCommittedAsync_WhenNotOverRun_ReturnsMoveCabinetSpecificRejection()
    {
        var runId = RunId.New();
        var cabinetId = CabinetId.New();
        var scene = new EditorSceneSnapshot([
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
            new NoHitSceneGraph(scene),
            new DefaultSnapResolver(
            [
                new RunEndpointSnapCandidateSource(),
                new CabinetFaceSnapCandidateSource(),
                new GridSnapCandidateSource()
            ]),
            new RecordingPreviewCommandExecutor(),
            new RecordingCommitCommandExecutor(),
            new StubClock());

        // Force an active drag with no target run by calling BeginMoveDrag directly — the
        // NoHitSceneGraph always returns null from HitTestRun, so TargetRunId will be null.
        session.BeginMoveDrag(new DragContext(
            DragType.MoveCabinet,
            Point2D.Origin,
            Vector2D.Zero,
            Length.FromInches(24m),
            Length.FromInches(24m),
            null,
            cabinetId,
            runId,
            null,
            null));

        var result = await service.OnDragCommittedAsync();

        Assert.False(result.Success);
        Assert.Equal("Cabinet must be dragged onto a wall run to move it.", result.FailureReason);
    }

    [Fact]
    public void OnDragMoved_WhenResizeHasNoTargetRun_ReturnsResizeCabinetSpecificRejection()
    {
        var runId = RunId.New();
        var cabinetId = CabinetId.New();
        var scene = new EditorSceneSnapshot([
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
            new NoHitSceneGraph(scene),
            new DefaultSnapResolver(
            [
                new RunEndpointSnapCandidateSource(),
                new CabinetFaceSnapCandidateSource(),
                new GridSnapCandidateSource()
            ]),
            new RecordingPreviewCommandExecutor(),
            new RecordingCommitCommandExecutor(),
            new StubClock());

        // Force an active resize drag with no target run by calling BeginResizeDrag directly.
        session.BeginResizeDrag(new DragContext(
            DragType.ResizeCabinet,
            Point2D.Origin,
            Vector2D.Zero,
            Length.FromInches(24m),
            Length.FromInches(24m),
            null,
            cabinetId,
            null,
            Point2D.Origin,
            null));

        var result = service.OnDragMoved(500d, 500d);

        Assert.False(result.IsValid);
        Assert.Equal("Drag the handle along the run to set the cabinet width.", result.RejectionReason);
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

    private sealed class NoHitSceneGraph : IEditorSceneGraph
    {
        private readonly EditorSceneSnapshot _scene;

        public NoHitSceneGraph(EditorSceneSnapshot scene) => _scene = scene;

        public EditorSceneSnapshot Capture() => _scene;

        public RunId? HitTestRun(Point2D worldPoint, Length hitRadius) => null;
    }

    private sealed class RecordingPreviewCommandExecutor : IPreviewCommandExecutor
    {
        public int PreviewCallCount { get; private set; }

        public IDesignCommand? LastCommand { get; private set; }

        public DragPreviewResult Preview(IDesignCommand command)
        {
            PreviewCallCount++;
            LastCommand = command;
            return new DragPreviewResult(true, command, null);
        }
    }

    private sealed class RecordingCommitCommandExecutor : ICommitCommandExecutor
    {
        public int CommitCallCount { get; private set; }

        public IDesignCommand? LastCommand { get; private set; }

        public Task<DragCommitResult> ExecuteAsync(IDesignCommand command, CancellationToken ct = default)
        {
            CommitCallCount++;
            LastCommand = command;
            return Task.FromResult(new DragCommitResult(true, command, null));
        }
    }

    private sealed class StubClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.UnixEpoch;
    }
}
