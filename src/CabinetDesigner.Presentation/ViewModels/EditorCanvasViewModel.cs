using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;
using CabinetDesigner.Presentation.Commands;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class EditorCanvasViewModel : ObservableObject, IDisposable
{
    private const double DragThresholdPixels = 4.0;

    private readonly IRunService _runService;
    private readonly IApplicationEventBus _eventBus;
    private readonly ISceneProjector _sceneProjector;
    private readonly IEditorCanvasSession _editorSession;
    private readonly IHitTester _hitTester;
    private readonly IEditorCanvasHost _canvasHost;
    private readonly IEditorInteractionService _interactionService;
    private readonly IAppLogger? _logger;
    private RenderSceneDto? _scene;
    private IReadOnlyList<Guid> _selectedCabinetIds = [];
    private Guid? _hoveredCabinetId;
    private string _statusMessage = "Ready";
    private double _lastPanX;
    private double _lastPanY;
    private int _busyCount;
    private double _pendingDragStartX;
    private double _pendingDragStartY;
    private Guid? _pendingDragCabinetId;
    private HitTestTarget _pendingDragTarget;
    private bool _isDragActive;
    private bool _isCommitInFlight;
    private bool _isResizingAtMinimum;

    public EditorCanvasViewModel(
        IRunService runService,
        IApplicationEventBus eventBus,
        ISceneProjector sceneProjector,
        IEditorCanvasSession editorSession,
        IHitTester hitTester,
        IEditorCanvasHost canvasHost,
        IEditorInteractionService interactionService,
        IAppLogger? logger = null)
    {
        _runService = runService ?? throw new ArgumentNullException(nameof(runService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _sceneProjector = sceneProjector ?? throw new ArgumentNullException(nameof(sceneProjector));
        _editorSession = editorSession ?? throw new ArgumentNullException(nameof(editorSession));
        _hitTester = hitTester ?? throw new ArgumentNullException(nameof(hitTester));
        _canvasHost = canvasHost ?? throw new ArgumentNullException(nameof(canvasHost));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
        _logger = logger;

        ResetZoomCommand = new RelayCommand(ExecuteResetZoom);
        FitToViewCommand = new RelayCommand(ExecuteFitToView);
        SelectAllCommand = new RelayCommand(ExecuteSelectAll, () => Scene is not null);
        SelectNoneCommand = new RelayCommand(ExecuteSelectNone, () => Scene is not null);

        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Subscribe<UndoAppliedEvent>(OnUndoApplied);
        _eventBus.Subscribe<RedoAppliedEvent>(OnRedoApplied);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _canvasHost.SetMouseDownHandler(OnMouseDown);
        _canvasHost.SetMouseMoveHandler(OnMouseMove);
        _canvasHost.SetMouseUpHandler(OnMouseUp);
        _canvasHost.SetMouseWheelHandler(OnMouseWheel);
        _canvasHost.SetMiddleButtonDragHandler(OnPanStart, OnPanMove, OnPanEnd);
        RefreshScene();
    }

    public object CanvasView => _canvasHost.View;

    public RenderSceneDto? Scene
    {
        get => _scene;
        private set
        {
            if (SetProperty(ref _scene, value))
            {
                SelectAllCommand.NotifyCanExecuteChanged();
                SelectNoneCommand.NotifyCanExecuteChanged();
            }
        }
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

    public bool IsBusy => _busyCount > 0;

    public RelayCommand ResetZoomCommand { get; }

    public RelayCommand FitToViewCommand { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand SelectNoneCommand { get; }

    public void SetStatusMessage(string statusMessage) => StatusMessage = statusMessage;

    public async Task<CommandResultDto> AddCabinetToRunAsync(Guid runId, string cabinetTypeId, decimal nominalWidthInches)
    {
        BeginBusy();
        try
        {
            StatusMessage = "Adding cabinet...";
            var result = await _runService.AddCabinetAsync(
                new AddCabinetRequestDto(runId, cabinetTypeId, nominalWidthInches, "EndOfRun"))
                .ConfigureAwait(true);
            StatusMessage = result.Success ? "Cabinet added." : "Could not add cabinet — check validation issues.";
            return result;
        }
        finally
        {
            EndBusy();
        }
    }

    public async Task<CommandResultDto> MoveCabinetAsync(Guid cabinetId, Guid sourceRunId, Guid targetRunId, int? targetIndex = null)
    {
        BeginBusy();
        try
        {
            var placement = targetIndex.HasValue ? "AtIndex" : "EndOfRun";
            StatusMessage = "Moving cabinet...";
            var result = await _runService.MoveCabinetAsync(
                new MoveCabinetRequestDto(cabinetId, sourceRunId, targetRunId, placement, targetIndex))
                .ConfigureAwait(true);
            StatusMessage = result.Success ? "Cabinet moved." : "Could not move cabinet — check validation issues.";
            return result;
        }
        finally
        {
            EndBusy();
        }
    }

    public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds)
    {
        _editorSession.SetSelectedCabinetIds(cabinetIds);
        RefreshInteractionState();
    }

    public void OnMouseDown(double screenX, double screenY)
    {
        // Guard: if a drag commit is still in flight, abort the session before accepting new input.
        if (_isCommitInFlight || _isDragActive)
        {
            _isDragActive = false;
            _isCommitInFlight = false;
            _isResizingAtMinimum = false;
            _pendingDragCabinetId = null;
            _interactionService.OnDragAborted();
        }

        if (Scene is null)
        {
            return;
        }

        var result = _hitTester.HitTest(screenX, screenY, Scene, _editorSession.Viewport);

        // Selection is editor interaction state, so it is updated through the editor session rather than the design pipeline.
        if (result.Target == HitTestTarget.Cabinet && result.EntityId is Guid cabinetId)
        {
            if (_canvasHost.IsCtrlHeld)
            {
                var current = _editorSession.SelectedCabinetIds.ToList();
                if (current.Contains(cabinetId))
                {
                    current.Remove(cabinetId);
                }
                else
                {
                    current.Add(cabinetId);
                }

                _editorSession.SetSelectedCabinetIds(current);
            }
            else
            {
                _editorSession.SetSelectedCabinetIds([cabinetId]);
            }

            StatusMessage = _editorSession.SelectedCabinetIds.Count switch
            {
                0 => "Selection cleared.",
                1 => "Cabinet selected.",
                _ => $"{_editorSession.SelectedCabinetIds.Count} cabinets selected."
            };

            // Record pending drag start — the drag begins only once the threshold is exceeded.
            _pendingDragCabinetId = cabinetId;
            _pendingDragTarget = result.Target;
            _pendingDragStartX = screenX;
            _pendingDragStartY = screenY;
        }
        else if (result.Target == HitTestTarget.Handle && result.EntityId is Guid handleCabinetId)
        {
            // Handle hit — selection follows the cabinet, and a resize drag starts after threshold.
            _editorSession.SetSelectedCabinetIds([handleCabinetId]);
            StatusMessage = "Cabinet selected.";
            _pendingDragCabinetId = handleCabinetId;
            _pendingDragTarget = HitTestTarget.Handle;
            _pendingDragStartX = screenX;
            _pendingDragStartY = screenY;
        }
        else
        {
            _editorSession.SetSelectedCabinetIds([]);
            StatusMessage = "Selection cleared.";
        }

        RefreshScene();
    }

    public void OnMouseMove(double screenX, double screenY)
    {
        if (Scene is null)
        {
            return;
        }

        // Promote pending drag to an active drag once the movement exceeds the threshold.
        if (_pendingDragCabinetId.HasValue)
        {
            var dx = screenX - _pendingDragStartX;
            var dy = screenY - _pendingDragStartY;
            if ((dx * dx) + (dy * dy) >= DragThresholdPixels * DragThresholdPixels)
            {
                BeginDrag(_pendingDragCabinetId.Value, _pendingDragTarget, _pendingDragStartX, _pendingDragStartY);
                _pendingDragCabinetId = null;
            }
        }

        // Update the active drag preview.
        if (_isDragActive)
        {
            var previewResult = _interactionService.OnDragMoved(screenX, screenY);
            _isResizingAtMinimum = previewResult.IsResizingAtMinimum;
            RefreshScene();
            return;
        }

        // Update hover highlight when not dragging.
        var result = _hitTester.HitTest(screenX, screenY, Scene, _editorSession.Viewport);
        var hoveredId = result.Target is HitTestTarget.Cabinet or HitTestTarget.Handle ? result.EntityId : null;
        if (hoveredId != HoveredCabinetId)
        {
            _editorSession.SetHoveredCabinetId(hoveredId);
            RefreshScene();
        }
    }

    public async void OnMouseUp(double screenX, double screenY)
    {
        // Discard any pending drag that never exceeded the threshold — the mouse-down
        // already handled selection, so no further action is needed.
        _pendingDragCabinetId = null;

        var wasDragActive = _isDragActive;
        _isDragActive = false;
        _isResizingAtMinimum = false;

        if (wasDragActive)
        {
            try
            {
                await CommitDragAsync().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                _logger?.Log(new LogEntry
                {
                    Level = LogLevel.Error,
                    Category = "EditorCanvasViewModel",
                    Message = "Unhandled exception escaping CommitDragAsync.",
                    Timestamp = DateTimeOffset.UtcNow,
                    Exception = exception
                });
                StatusMessage = "Drag failed unexpectedly.";
                RefreshScene();
            }
        }
    }

    public void OnMouseWheel(double screenX, double screenY, double delta)
    {
        if (Scene is null)
        {
            return;
        }

        const double zoomStep = 1.1;
        var factor = delta > 0 ? zoomStep : 1.0 / zoomStep;
        _editorSession.ZoomAt(screenX, screenY, factor);
        RefreshScene();
    }

    public void OnPanStart(double screenX, double screenY)
    {
        if (Scene is null)
        {
            return;
        }

        _editorSession.BeginPan();
        _lastPanX = screenX;
        _lastPanY = screenY;
        RefreshInteractionState();
    }

    public void OnPanMove(double screenX, double screenY)
    {
        if (Scene is null)
        {
            return;
        }

        _editorSession.PanBy(screenX - _lastPanX, screenY - _lastPanY);
        _lastPanX = screenX;
        _lastPanY = screenY;
        RefreshScene();
    }

    public void OnPanEnd()
    {
        _editorSession.EndPan();
        RefreshInteractionState();
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<UndoAppliedEvent>(OnUndoApplied);
        _eventBus.Unsubscribe<RedoAppliedEvent>(OnRedoApplied);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
        if (_canvasHost is IDisposable disposableHost)
        {
            disposableHost.Dispose();
        }
    }

    private void ExecuteResetZoom()
    {
        _editorSession.ResetViewport();
        if (Scene is null)
        {
            RefreshInteractionState();
        }
        else
        {
            RefreshScene();
        }

        StatusMessage = "Zoom reset.";
    }

    private void ExecuteFitToView()
    {
        if (Scene is null)
        {
            return;
        }

        var bounds = RenderSceneBoundsCalculator.Calculate(Scene);
        if (bounds is null)
        {
            StatusMessage = "Nothing to fit — canvas is empty.";
            return;
        }

        var canvasWidth = _canvasHost.CanvasWidth;
        var canvasHeight = _canvasHost.CanvasHeight;
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            StatusMessage = "Unable to fit to view — canvas is not ready.";
            return;
        }

        _editorSession.FitViewport(bounds.Value, canvasWidth, canvasHeight);
        RefreshScene();
        StatusMessage = "Fit to view.";
    }

    private void ExecuteSelectAll()
    {
        if (Scene is null)
        {
            return;
        }

        var allIds = Scene.Cabinets.Select(c => c.CabinetId).ToArray();
        _editorSession.SetSelectedCabinetIds(allIds);
        RefreshScene();
        StatusMessage = allIds.Length switch
        {
            0 => "Nothing to select.",
            1 => "1 cabinet selected.",
            _ => $"{allIds.Length} cabinets selected."
        };
    }

    private void ExecuteSelectNone()
    {
        if (Scene is null)
        {
            return;
        }

        _editorSession.SetSelectedCabinetIds([]);
        RefreshScene();
        StatusMessage = "Selection cleared.";
    }

    private void BeginDrag(Guid cabinetId, HitTestTarget target, double screenX, double screenY)
    {
        try
        {
            if (target == HitTestTarget.Handle)
            {
                _interactionService.BeginResizeCabinet(new CabinetId(cabinetId), screenX, screenY);
                StatusMessage = "Resizing cabinet...";
            }
            else
            {
                _interactionService.BeginMoveCabinet(new CabinetId(cabinetId), screenX, screenY);
                StatusMessage = "Moving cabinet...";
            }

            _isDragActive = true;
        }
        catch (InvalidOperationException invalidOp)
        {
            // Cabinet may have been removed between mouse-down and the drag threshold being reached.
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Warning,
                Category = "EditorCanvasViewModel",
                Message = "Could not begin drag; cabinet may have been removed before the drag threshold was reached.",
                Timestamp = DateTimeOffset.UtcNow,
                Exception = invalidOp
            });
            _pendingDragCabinetId = null;
        }
    }

    private async Task CommitDragAsync()
    {
        _isCommitInFlight = true;
        BeginBusy();
        try
        {
            var result = await _interactionService.OnDragCommittedAsync().ConfigureAwait(true);
            if (result.Success)
            {
                StatusMessage = "Drag committed.";
            }
            else
            {
                StatusMessage = !string.IsNullOrEmpty(result.FailureReason)
                    ? result.FailureReason
                    : "Placement rejected — check validation issues.";
            }
        }
        catch (OperationCanceledException)
        {
            _interactionService.OnDragAborted();
            StatusMessage = "Drag cancelled.";
        }
        catch (Exception exception)
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Error,
                Category = "EditorCanvasViewModel",
                Message = "An unexpected error occurred while committing a drag operation.",
                Timestamp = DateTimeOffset.UtcNow,
                Exception = exception
            });
            _interactionService.OnDragAborted();
            StatusMessage = "Drag failed.";
        }
        finally
        {
            _isCommitInFlight = false;
            EndBusy();
            RefreshScene();
        }
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
        if (_isDragActive || _pendingDragCabinetId is not null)
        {
            _interactionService.OnDragAborted();
        }

        _pendingDragCabinetId = null;
        _isDragActive = false;
        Scene = null;
        SelectedCabinetIds = [];
        HoveredCabinetId = null;
        _canvasHost.UpdateViewport(_editorSession.Viewport);
        StatusMessage = "Project closed.";
        OnPropertyChanged(nameof(CurrentMode));
    }

    private void RefreshScene()
    {
        var projected = _sceneProjector.Project();
        if (projected is null)
        {
            Scene = null;
            RefreshInteractionState();
            return;
        }

        Scene = RenderSceneComposer.ApplyInteractionState(
            projected,
            _editorSession.SelectedCabinetIds,
            _editorSession.HoveredCabinetId,
            _isResizingAtMinimum);
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

    private void BeginBusy()
    {
        _busyCount++;
        OnPropertyChanged(nameof(IsBusy));
    }

    private void EndBusy()
    {
        if (_busyCount > 0)
        {
            _busyCount--;
        }

        OnPropertyChanged(nameof(IsBusy));
    }
}
