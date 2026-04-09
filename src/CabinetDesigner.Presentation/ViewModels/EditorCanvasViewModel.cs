using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class EditorCanvasViewModel : ObservableObject, IDisposable
{
    private readonly IRunService _runService;
    private readonly IApplicationEventBus _eventBus;
    private readonly ISceneProjector _sceneProjector;
    private readonly IEditorCanvasSession _editorSession;
    private readonly IHitTester _hitTester;
    private readonly IEditorCanvasHost _canvasHost;
    private RenderSceneDto? _scene;
    private IReadOnlyList<Guid> _selectedCabinetIds = [];
    private Guid? _hoveredCabinetId;
    private string _statusMessage = "Ready";

    public EditorCanvasViewModel(
        IRunService runService,
        IApplicationEventBus eventBus,
        ISceneProjector sceneProjector,
        IEditorCanvasSession editorSession,
        IHitTester hitTester,
        IEditorCanvasHost canvasHost)
    {
        _runService = runService ?? throw new ArgumentNullException(nameof(runService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _sceneProjector = sceneProjector ?? throw new ArgumentNullException(nameof(sceneProjector));
        _editorSession = editorSession ?? throw new ArgumentNullException(nameof(editorSession));
        _hitTester = hitTester ?? throw new ArgumentNullException(nameof(hitTester));
        _canvasHost = canvasHost ?? throw new ArgumentNullException(nameof(canvasHost));

        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Subscribe<UndoAppliedEvent>(OnUndoApplied);
        _eventBus.Subscribe<RedoAppliedEvent>(OnRedoApplied);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        RefreshScene();
    }

    public object CanvasView => _canvasHost.View;

    public RenderSceneDto? Scene
    {
        get => _scene;
        private set => SetProperty(ref _scene, value);
    }

    public IReadOnlyList<Guid> SelectedCabinetIds
    {
        get => _selectedCabinetIds;
        private set => SetProperty(ref _selectedCabinetIds, value);
    }

    public Guid? HoveredCabinetId
    {
        get => _hoveredCabinetId;
        private set => SetProperty(ref _hoveredCabinetId, value);
    }

    public string CurrentMode => _editorSession.CurrentMode.ToString();

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public async Task<CommandResultDto> AddCabinetToRunAsync(Guid runId, string cabinetTypeId, decimal nominalWidthInches)
    {
        StatusMessage = "Adding cabinet...";
        var result = await _runService.AddCabinetAsync(
            new AddCabinetRequestDto(runId, cabinetTypeId, nominalWidthInches, "EndOfRun"))
            .ConfigureAwait(false);
        StatusMessage = result.Success ? "Cabinet added." : "Cabinet add rejected.";
        return result;
    }

    public async Task<CommandResultDto> MoveCabinetAsync(Guid cabinetId, Guid sourceRunId, Guid targetRunId, int? targetIndex = null)
    {
        var placement = targetIndex.HasValue ? "AtIndex" : "EndOfRun";
        StatusMessage = "Moving cabinet...";
        var result = await _runService.MoveCabinetAsync(
            new MoveCabinetRequestDto(cabinetId, sourceRunId, targetRunId, placement, targetIndex))
            .ConfigureAwait(false);
        StatusMessage = result.Success ? "Cabinet moved." : "Cabinet move rejected.";
        return result;
    }

    public void OnMouseDown(double screenX, double screenY)
    {
        if (Scene is null)
        {
            return;
        }

        // Selection is editor interaction state, so it is updated through the editor session rather than the design pipeline.
        var result = _hitTester.HitTest(screenX, screenY, Scene, _editorSession.Viewport);
        if (result.Target == HitTestTarget.Cabinet && result.EntityId is Guid cabinetId)
        {
            _editorSession.SetSelectedCabinetIds([cabinetId]);
            StatusMessage = "Cabinet selected.";
        }
        else
        {
            _editorSession.SetSelectedCabinetIds([]);
            StatusMessage = "Selection cleared.";
        }

        RefreshInteractionState();
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<UndoAppliedEvent>(OnUndoApplied);
        _eventBus.Unsubscribe<RedoAppliedEvent>(OnRedoApplied);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
    }

    private void OnDesignChanged(DesignChangedEvent _)
    {
        RefreshScene();
        StatusMessage = "Design updated.";
    }

    private void OnUndoApplied(UndoAppliedEvent _)
    {
        RefreshScene();
        StatusMessage = "Undo applied.";
    }

    private void OnRedoApplied(RedoAppliedEvent _)
    {
        RefreshScene();
        StatusMessage = "Redo applied.";
    }

    private void OnProjectClosed(ProjectClosedEvent _)
    {
        Scene = null;
        SelectedCabinetIds = [];
        HoveredCabinetId = null;
        _canvasHost.UpdateViewport(_editorSession.Viewport);
        StatusMessage = "Project closed.";
        OnPropertyChanged(nameof(CurrentMode));
    }

    private void RefreshScene()
    {
        Scene = RenderSceneComposer.ApplyInteractionState(
            _sceneProjector.Project(),
            _editorSession.SelectedCabinetIds,
            _editorSession.HoveredCabinetId);
        _canvasHost.UpdateScene(Scene);
        _canvasHost.UpdateViewport(_editorSession.Viewport);
        RefreshInteractionState();
    }

    private void RefreshInteractionState()
    {
        SelectedCabinetIds = _editorSession.SelectedCabinetIds.ToArray();
        HoveredCabinetId = _editorSession.HoveredCabinetId;
        OnPropertyChanged(nameof(CurrentMode));
    }
}
