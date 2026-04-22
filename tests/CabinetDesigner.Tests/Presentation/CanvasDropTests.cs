using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Editor;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class CanvasDropTests
{
    [Fact]
    public async Task OnDropOfCatalogTemplate_InsideRun_AppendsCabinetToRun()
    {
        var context = CreateContext(withRun: true);

        var previewEffects = context.ViewModel.OnCatalogDragOver(new CatalogTemplateDragPayload("base-standard-24"), 10, 10);
        Assert.Equal(System.Windows.DragDropEffects.Copy, previewEffects);
        Assert.Equal("Previewing placement.", context.ViewModel.StatusMessage);

        var dropEffects = await context.ViewModel.OnCatalogDropAsync(new CatalogTemplateDragPayload("base-standard-24"), 10, 10);

        Assert.Equal(System.Windows.DragDropEffects.Copy, dropEffects);
        Assert.Equal("Cabinet placed.", context.ViewModel.StatusMessage);
        Assert.Single(context.StateStore.GetAllRuns());
        Assert.Single(context.StateStore.GetAllCabinets());
        Assert.Equal("base-standard-24", context.StateStore.GetAllCabinets()[0].CabinetTypeId);
        Assert.Single(context.Projector.Project()!.Cabinets);
        Assert.Equal(context.RunId.Value, context.Projector.Project()!.Cabinets[0].RunId);
    }

    [Fact]
    public async Task OnDropOfCatalogTemplate_OutsideAnyRun_CreatesNewRunAndPlacesCabinet()
    {
        var context = CreateContext(withRun: false);

        var dropEffects = await context.ViewModel.OnCatalogDropAsync(new CatalogTemplateDragPayload("wall-standard-30"), 12, 2);

        Assert.Equal(System.Windows.DragDropEffects.Copy, dropEffects);
        Assert.Equal("Cabinet placed.", context.ViewModel.StatusMessage);
        Assert.Single(context.StateStore.GetAllRuns());
        Assert.Single(context.StateStore.GetAllCabinets());
        Assert.Equal("wall-standard-30", context.StateStore.GetAllCabinets()[0].CabinetTypeId);
        Assert.Single(context.Projector.Project()!.Runs);
        Assert.Single(context.Projector.Project()!.Cabinets);
    }

    [Fact]
    public async Task OnDropWhenCapacityExceeded_AbortsDrop_AndPushesStatusMessage()
    {
        var context = CreateContext(withRun: true);
        await context.RunService.PlaceCabinetAsync(context.RunId, "base-standard-24");

        var dropEffects = await context.ViewModel.OnCatalogDropAsync(new CatalogTemplateDragPayload("base-drawer-18"), 10, 10);

        Assert.Equal(System.Windows.DragDropEffects.None, dropEffects);
        Assert.Equal("Cannot place cabinet: run capacity exceeded.", context.ViewModel.StatusMessage);
        Assert.Single(context.StateStore.GetAllCabinets());
        Assert.Single(context.Projector.Project()!.Cabinets);
        Assert.False(context.InteractionService.WasCommitted);
        Assert.True(context.InteractionService.WasAborted);
    }

    private static DropTestContext CreateContext(bool withRun)
    {
        var stateStore = new InMemoryDesignStateStore();
        var revisionId = RevisionId.New();
        var roomId = RoomId.New();
        var room = new Room(roomId, revisionId, "Kitchen", Length.FromInches(96m));
        var wall = room.AddWall(Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        stateStore.AddRoom(room);
        stateStore.AddWall(wall);

        RunId? runId = null;
        if (withRun)
        {
            var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(24m));
            stateStore.AddRun(run, wall.StartPoint, new Point2D(24m, 0m));
            runId = run.Id;
        }

        var session = new TestEditorCanvasSession { ActiveRoomId = roomId.Value };
        var projector = new SceneProjector(stateStore, session);
        var eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        var runService = new RunService(new NoOpDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);
        var interactionService = new RecordingInteractionService();
        var canvasHost = new RecordingCanvasHost();
        var viewModel = new EditorCanvasViewModel(
            runService,
            eventBus,
            projector,
            session,
            new DefaultHitTester(),
            canvasHost,
            interactionService,
            logger,
            catalogService: new CatalogService());

        if (projector.Project() is null)
        {
            throw new InvalidOperationException("Projected scene should not be null in the test setup.");
        }

        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        return new DropTestContext(stateStore, projector, runService, viewModel, interactionService, session, roomId, runId ?? default, wall.Id);
    }

    private sealed record DropTestContext(
        InMemoryDesignStateStore StateStore,
        SceneProjector Projector,
        RunService RunService,
        EditorCanvasViewModel ViewModel,
        RecordingInteractionService InteractionService,
        TestEditorCanvasSession Session,
        RoomId RoomId,
        RunId RunId,
        WallId WallId);

    private sealed class TestEditorCanvasSession : IEditorCanvasSession
    {
        public EditorMode CurrentMode { get; private set; } = EditorMode.Idle;

        public IReadOnlyList<Guid> SelectedCabinetIds { get; private set; } = [];

        public Guid? HoveredCabinetId { get; private set; }

        public Guid? ActiveRoomId { get; set; }

        public ViewportTransform Viewport { get; private set; } = ViewportTransform.Default;

        public CabinetDesigner.Editor.Snap.SnapSettings SnapSettings => CabinetDesigner.Editor.Snap.SnapSettings.Default;

        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds) => SelectedCabinetIds = cabinetIds.ToArray();

        public void SetHoveredCabinetId(Guid? cabinetId) => HoveredCabinetId = cabinetId;

        public void SetActiveRoom(Guid? roomId) => ActiveRoomId = roomId;

        public void ZoomAt(double screenX, double screenY, double scaleFactor)
        {
        }

        public void PanBy(double dx, double dy)
        {
        }

        public void BeginPan()
        {
        }

        public void EndPan()
        {
        }

        public void ResetViewport()
        {
        }

        public void FitViewport(ViewportBounds contentBounds, double canvasWidth, double canvasHeight)
        {
        }
    }

    private sealed class RecordingCanvasHost : IEditorCanvasHost
    {
        public object View => new object();

        public bool IsCtrlHeld => false;

        public double CanvasWidth => 800;

        public double CanvasHeight => 600;

        public void UpdateScene(RenderSceneDto scene)
        {
        }

        public void UpdateViewport(ViewportTransform viewport)
        {
        }

        public void SetMouseDownHandler(Action<double, double> handler)
        {
        }

        public void SetMouseMoveHandler(Action<double, double> handler)
        {
        }

        public void SetMouseUpHandler(Action<double, double> handler)
        {
        }

        public void SetMouseWheelHandler(Action<double, double, double> handler)
        {
        }

        public void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd)
        {
        }
    }

    private sealed class RecordingInteractionService : IEditorInteractionService
    {
        public bool WasCommitted { get; private set; }

        public bool WasAborted { get; private set; }

        public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY)
        {
        }

        public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY)
        {
        }

        public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY)
        {
        }

        public DragPreviewResult OnDragMoved(double screenX, double screenY)
        {
            return new DragPreviewResult(true, null, null);
        }

        public Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default)
        {
            WasCommitted = true;
            return Task.FromResult(new DragCommitResult(true, null, null));
        }

        public void OnDragAborted()
        {
            WasAborted = true;
        }
    }

    private sealed class NoOpDesignCommandHandler : IDesignCommandHandler
    {
        public Task<CommandResultDto> ExecuteAsync(IDesignCommand command, CancellationToken ct = default)
        {
            return Task.FromResult(new CommandResultDto(Guid.NewGuid(), command.CommandType, true, [], [], []));
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }
}
