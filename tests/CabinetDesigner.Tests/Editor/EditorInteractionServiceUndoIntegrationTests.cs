using System.Threading;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Explanation;
using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;
using Xunit;

namespace CabinetDesigner.Tests.Editor;

/// <summary>
/// Integration tests for <see cref="EditorInteractionService"/> that verify committed drag
/// operations produce correct undo stack entries.
/// </summary>
public sealed class EditorInteractionServiceUndoIntegrationTests
{
    [Fact]
    public async Task DragCommit_PlaceCabinet_CommitCommandExecutorReceivesAddCabinetCommand()
    {
        var (service, spy, _, _) = CreateServiceWithSpy(out var runId);

        service.BeginPlaceCabinet("base-24", Length.FromInches(24m), Length.FromInches(24m), 60d, 0d);
        service.OnDragMoved(60d, 0d);
        var result = await service.OnDragCommittedAsync();

        Assert.True(result.Success);
        Assert.IsType<AddCabinetToRunCommand>(spy.LastCommand);
        Assert.Equal(runId, ((AddCabinetToRunCommand)spy.LastCommand!).RunId);
    }

    [Fact]
    public async Task DragCommit_MoveCabinet_CommitCommandExecutorReceivesMoveCabinetCommand()
    {
        var cabinetId = CabinetId.New();
        var (service, spy, _, _) = CreateServiceWithSpyAndCabinet(cabinetId);

        service.BeginMoveCabinet(cabinetId, 12d, 0d);
        service.OnDragMoved(60d, 0d);
        var result = await service.OnDragCommittedAsync();

        Assert.True(result.Success);
        Assert.IsType<MoveCabinetCommand>(spy.LastCommand);
    }

    [Fact]
    public async Task DragCommit_ResizeCabinet_CommitCommandExecutorReceivesResizeCabinetCommand()
    {
        var cabinetId = CabinetId.New();
        var (service, spy, _, _) = CreateServiceWithSpyAndCabinet(cabinetId);

        service.BeginResizeCabinet(cabinetId, 24d, 0d);
        service.OnDragMoved(36d, 0d);
        var result = await service.OnDragCommittedAsync();

        Assert.True(result.Success);
        Assert.IsType<ResizeCabinetCommand>(spy.LastCommand);
    }

    [Fact]
    public async Task DragCommit_PlaceCabinet_WhenOrchestratorTracksDeltas_ProducesUndoEntry()
    {
        // FIX (Batch 5): Verify that a committed drag produces an undo stack entry via
        // the real ResolutionOrchestrator pipeline.
        var (service, _, undoStack, _) = CreateServiceWithRealOrchestrator(out _);

        service.BeginPlaceCabinet("base-24", Length.FromInches(24m), Length.FromInches(24m), 60d, 0d);
        service.OnDragMoved(60d, 0d);
        var result = await service.OnDragCommittedAsync();

        Assert.True(result.Success, result.FailureReason);
        Assert.True(undoStack.CanUndo, "CanUndo must be true after a successful drag commit.");
    }

    [Fact]
    public async Task DragCommit_PlaceCabinet_ThenUndo_RevertsCabinetCount()
    {
        // FIX (Batch 5): Verify that Undo() after a committed drag reverts the design state.
        var (service, _, undoStack, stateStore) = CreateServiceWithRealOrchestrator(out var orchestrator);

        service.BeginPlaceCabinet("base-24", Length.FromInches(24m), Length.FromInches(24m), 60d, 0d);
        service.OnDragMoved(60d, 0d);
        var result = await service.OnDragCommittedAsync();
        Assert.True(result.Success, result.FailureReason);

        var cabinetCountAfterCommit = stateStore.GetAllCabinets().Count;

        orchestrator.Undo();

        var cabinetCountAfterUndo = stateStore.GetAllCabinets().Count;
        Assert.Equal(cabinetCountAfterCommit - 1, cabinetCountAfterUndo);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Creates a service with a spy commit executor (no real orchestrator).</summary>
    private static (EditorInteractionService service, SpyCommitExecutor spy, InMemoryUndoStack undoStack, EditorSceneSnapshot scene)
        CreateServiceWithSpy(out RunId runId)
    {
        runId = RunId.New();
        var scene = BuildScene(runId, []);
        var session = new EditorSession();
        var spy = new SpyCommitExecutor();
        var undoStack = new InMemoryUndoStack();
        var service = new EditorInteractionService(
            session,
            new StubSceneGraph(scene, runId),
            new DefaultSnapResolver([new RunEndpointSnapCandidateSource(), new GridSnapCandidateSource()]),
            new NoOpPreviewExecutor(),
            spy,
            new FixedClock());
        return (service, spy, undoStack, scene);
    }

    /// <summary>Creates a service with a spy commit executor and a pre-seeded cabinet.</summary>
    private static (EditorInteractionService service, SpyCommitExecutor spy, InMemoryUndoStack undoStack, EditorSceneSnapshot scene)
        CreateServiceWithSpyAndCabinet(CabinetId cabinetId)
    {
        var runId = RunId.New();
        var cabinet = new CabinetSceneView(cabinetId, runId, 0, Length.FromInches(24m), Length.FromInches(24m),
            Point2D.Origin, new Point2D(24m, 0m));
        var scene = BuildScene(runId, [cabinet]);
        var session = new EditorSession();
        var spy = new SpyCommitExecutor();
        var undoStack = new InMemoryUndoStack();
        var service = new EditorInteractionService(
            session,
            new StubSceneGraph(scene, runId),
            new DefaultSnapResolver([new RunEndpointSnapCandidateSource(), new GridSnapCandidateSource()]),
            new NoOpPreviewExecutor(),
            spy,
            new FixedClock());
        return (service, spy, undoStack, scene);
    }

    /// <summary>Creates a service wired to the real pipeline (Option A from checklist).</summary>
    private static (EditorInteractionService service, SpyCommitExecutor spy, InMemoryUndoStack undoStack, InMemoryDesignStateStore stateStore)
        CreateServiceWithRealOrchestrator(out ResolutionOrchestrator orchestrator)
    {
        var stateStore = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(120m));
        stateStore.AddWall(wall);
        stateStore.AddRun(run, wall.StartPoint, wall.EndPoint);

        var deltaTracker = new InMemoryDeltaTracker();
        var undoStack = new InMemoryUndoStack();
        var whyEngine = new WhyEngine();
        orchestrator = new ResolutionOrchestrator(
            deltaTracker,
            whyEngine,
            undoStack,
            stateStore,
            stateStore,
            stages: [
                new InputCaptureStage(stateStore),
                new InteractionInterpretationStage(deltaTracker, stateStore),
                new SpatialResolutionStage(stateStore)
            ]);

        var eventBus = new ApplicationEventBus();
        var handler = new DesignCommandHandler(orchestrator, eventBus, new NoOpPersistencePort());

        var runId = run.Id;
        var scene = BuildScene(runId, []);
        var session = new EditorSession();
        var spy = new SpyCommitExecutor(handler);
        var service = new EditorInteractionService(
            session,
            new StubSceneGraph(scene, runId),
            new DefaultSnapResolver([new RunEndpointSnapCandidateSource(), new GridSnapCandidateSource()]),
            new NoOpPreviewExecutor(),
            spy,
            new FixedClock());

        return (service, spy, undoStack, stateStore);
    }

    private static EditorSceneSnapshot BuildScene(RunId runId, IReadOnlyList<CabinetSceneView> cabinets) =>
        new(
        [
            new RunSceneView(runId, Point2D.Origin, new Point2D(120m, 0m), Vector2D.UnitX, Length.FromInches(120m), cabinets)
        ]);

    // ─── Test doubles ───────────────────────────────────────────────────────────

    private sealed class SpyCommitExecutor : ICommitCommandExecutor
    {
        private readonly IDesignCommandHandler? _handler;

        public SpyCommitExecutor(IDesignCommandHandler? handler = null) => _handler = handler;

        public IDesignCommand? LastCommand { get; private set; }

        public async Task<DragCommitResult> ExecuteAsync(IDesignCommand command, CancellationToken ct = default)
        {
            LastCommand = command;
            if (_handler is not null)
            {
                var dto = await _handler.ExecuteAsync(command, ct).ConfigureAwait(false);
                return dto.Success
                    ? new DragCommitResult(true, command, null)
                    : new DragCommitResult(false, null, string.Join("; ", dto.Issues.Select(i => i.Message)));
            }

            return new DragCommitResult(true, command, null);
        }
    }

    private sealed class NoOpPreviewExecutor : IPreviewCommandExecutor
    {
        public DragPreviewResult Preview(IDesignCommand command) => new(true, command, null);
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

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.UnixEpoch;
    }

    private sealed class NoOpPersistencePort : ICommandPersistencePort
    {
        public Task CommitCommandAsync(IDesignCommand command, CommandResult result, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
