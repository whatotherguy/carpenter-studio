using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Editor;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class EditorCanvasViewModelTests
{
    [Fact]
    public async Task AddCabinetToRunAsync_DelegatesToRunService()
    {
        var runService = new RecordingRunService();
        using var viewModel = CreateViewModel(runService, out _, out _, out _);

        await viewModel.AddCabinetToRunAsync(Guid.NewGuid(), "base-36", 36m);

        Assert.NotNull(runService.LastAddRequest);
        Assert.Equal("base-36", runService.LastAddRequest!.CabinetTypeId);
        Assert.Equal("Cabinet added.", viewModel.StatusMessage);
    }

    [Fact]
    public void DesignChangedEvent_RefreshesProjectedScene()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out var canvasHost);
        projector.Scene = new RenderSceneDto(
            [],
            [],
            [new CabinetRenderDto(Guid.NewGuid(), Guid.NewGuid(), new Rect2D(Point2D.Origin, Length.FromInches(10m), Length.FromInches(10m)), "cab", "cab", CabinetRenderState.Normal, [])],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));

        Assert.Single(viewModel.Scene!.Cabinets);
        Assert.Same(viewModel.Scene, canvasHost.Scene);
        Assert.Equal("Design updated.", viewModel.StatusMessage);
    }

    [Fact]
    public void OnMouseDown_SelectsCabinetAndRaisesPropertyChanged()
    {
        using var viewModel = CreateViewModel(new RecordingRunService(), out var projector, out var eventBus, out _);
        var selectedCabinetId = Guid.NewGuid();
        projector.Scene = new RenderSceneDto(
            [],
            [],
            [new CabinetRenderDto(selectedCabinetId, Guid.NewGuid(), new Rect2D(Point2D.Origin, Length.FromInches(10m), Length.FromInches(10m)), "cab", "cab", CabinetRenderState.Normal, [])],
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
        eventBus.Publish(new DesignChangedEvent(new CommandResultDto(Guid.NewGuid(), "test", true, [], [], [])));
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        viewModel.OnMouseDown(5d, 5d);

        Assert.Contains(selectedCabinetId, viewModel.SelectedCabinetIds);
        Assert.Contains(nameof(EditorCanvasViewModel.SelectedCabinetIds), changedProperties);
        Assert.Equal("Cabinet selected.", viewModel.StatusMessage);
    }

    private static EditorCanvasViewModel CreateViewModel(
        RecordingRunService runService,
        out RecordingSceneProjector projector,
        out ApplicationEventBus eventBus,
        out RecordingCanvasHost canvasHost)
    {
        projector = new RecordingSceneProjector();
        eventBus = new ApplicationEventBus();
        canvasHost = new RecordingCanvasHost();
        return new EditorCanvasViewModel(
            runService,
            eventBus,
            projector,
            new TestEditorCanvasSession(),
            new DefaultHitTester(),
            canvasHost);
    }

    private sealed class RecordingSceneProjector : ISceneProjector
    {
        public RenderSceneDto Scene { get; set; } = new([], [], [], null, new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));

        public RenderSceneDto Project() => Scene;
    }

    private sealed class RecordingRunService : IRunService
    {
        public AddCabinetRequestDto? LastAddRequest { get; private set; }

        public Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> DeleteRunAsync(RunId runId) => throw new NotImplementedException();

        public Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request)
        {
            LastAddRequest = request;
            return Task.FromResult(new CommandResultDto(Guid.NewGuid(), "layout.add_cabinet_to_run", true, [], [request.RunId.ToString()], []));
        }

        public Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request) =>
            Task.FromResult(new CommandResultDto(Guid.NewGuid(), "layout.move_cabinet", true, [], [request.CabinetId.ToString()], []));

        public Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request) => throw new NotImplementedException();

        public Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request) => throw new NotImplementedException();

        public RunSummaryDto GetRunSummary(RunId runId) => throw new NotImplementedException();
    }

    private sealed class RecordingCanvasHost : IEditorCanvasHost
    {
        public object View => new();

        public bool IsCtrlHeld => false;

        public RenderSceneDto? Scene { get; private set; }

        public ViewportTransform Viewport { get; private set; } = ViewportTransform.Default;

        public void UpdateScene(RenderSceneDto scene) => Scene = scene;

        public void UpdateViewport(ViewportTransform viewport) => Viewport = viewport;

        public void SetMouseDownHandler(Action<double, double> handler)
        {
        }

        public void SetMouseMoveHandler(Action<double, double> handler)
        {
        }

        public void SetMouseWheelHandler(Action<double, double, double> handler)
        {
        }

        public void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd)
        {
        }
    }

    private sealed class TestEditorCanvasSession : IEditorCanvasSession
    {
        public EditorMode CurrentMode { get; private set; } = EditorMode.Idle;

        public IReadOnlyList<Guid> SelectedCabinetIds { get; private set; } = [];

        public Guid? HoveredCabinetId { get; private set; }

        public ViewportTransform Viewport { get; } = ViewportTransform.Default;

        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds)
        {
            SelectedCabinetIds = cabinetIds.ToArray();
        }

        public void SetHoveredCabinetId(Guid? cabinetId)
        {
            HoveredCabinetId = cabinetId;
        }

        public void ZoomAt(double screenX, double screenY, double scaleFactor)
        {
        }

        public void PanBy(double dx, double dy)
        {
        }
    }
}
