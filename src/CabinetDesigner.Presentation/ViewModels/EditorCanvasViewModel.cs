using System.Threading;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
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
    private readonly IRoomService _roomService;
    private readonly ICatalogService _catalogService;
    private readonly IEditorCanvasSession _editorSession;
    private readonly IHitTester _hitTester;
    private readonly IEditorCanvasHost _canvasHost;
    private readonly IEditorInteractionService _interactionService;
    private readonly IAppLogger _logger;
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
    private bool _isWallDrawingMode;
    private Point2D? _pendingWallStartWorld;

    public EditorCanvasViewModel(
        IRunService runService,
        IApplicationEventBus eventBus,
        ISceneProjector sceneProjector,
        IEditorCanvasSession editorSession,
        IHitTester hitTester,
        IEditorCanvasHost canvasHost,
        IEditorInteractionService interactionService,
        IAppLogger logger,
        ICatalogService? catalogService = null)
        : this(runService, eventBus, sceneProjector, new NoOpRoomService(), editorSession, hitTester, canvasHost, interactionService, logger, catalogService)
    {
    }

    public EditorCanvasViewModel(
        IRunService runService,
        IApplicationEventBus eventBus,
        ISceneProjector sceneProjector,
        IRoomService roomService,
        IEditorCanvasSession editorSession,
        IHitTester hitTester,
        IEditorCanvasHost canvasHost,
        IEditorInteractionService interactionService,
        IAppLogger logger,
        ICatalogService? catalogService = null)
    {
        _runService = runService ?? throw new ArgumentNullException(nameof(runService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _sceneProjector = sceneProjector ?? throw new ArgumentNullException(nameof(sceneProjector));
        _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
        _catalogService = catalogService ?? new CatalogService();
        _editorSession = editorSession ?? throw new ArgumentNullException(nameof(editorSession));
        _hitTester = hitTester ?? throw new ArgumentNullException(nameof(hitTester));
        _canvasHost = canvasHost ?? throw new ArgumentNullException(nameof(canvasHost));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ResetZoomCommand = new RelayCommand(ExecuteResetZoom);
        FitToViewCommand = new RelayCommand(ExecuteFitToView);
        ToggleWallDrawingCommand = new RelayCommand(ToggleWallDrawingMode);
        CancelWallDrawingCommand = new RelayCommand(CancelWallDrawingMode, () => IsWallDrawingMode);
        SelectAllCommand = new RelayCommand(ExecuteSelectAll, () => Scene is not null);
        SelectNoneCommand = new RelayCommand(ExecuteSelectNone, () => Scene is not null);
        DeleteSelectedCommand = new AsyncRelayCommand(ExecuteDeleteSelectedAsync, "canvas.delete-selected", logger, eventBus, CanDeleteSelected);

        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Subscribe<UndoAppliedEvent>(OnUndoApplied);
        _eventBus.Subscribe<RedoAppliedEvent>(OnRedoApplied);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _canvasHost.SetMouseDownHandler(OnMouseDown);
        _canvasHost.SetMouseMoveHandler(OnMouseMove);
        _canvasHost.SetMouseUpHandler(OnMouseUp);
        _canvasHost.SetMouseWheelHandler(OnMouseWheel);
        _canvasHost.SetMiddleButtonDragHandler(OnPanStart, OnPanMove, OnPanEnd);
        _canvasHost.SetDragOverHandler((x, y, payload) => OnCatalogDragOver(payload, x, y));
        _canvasHost.SetDropHandler((x, y, payload) => OnCatalogDropAsync(payload, x, y));
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
                DeleteSelectedCommand.NotifyCanExecuteChanged();
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

    public bool IsWallDrawingMode
    {
        get => _isWallDrawingMode;
        set
        {
            if (SetProperty(ref _isWallDrawingMode, value))
            {
                CancelWallDrawingCommand.NotifyCanExecuteChanged();
                if (!value)
                {
                    _pendingWallStartWorld = null;
                }
            }
        }
    }

    public RelayCommand ResetZoomCommand { get; }

    public RelayCommand FitToViewCommand { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand SelectNoneCommand { get; }

    public AsyncRelayCommand DeleteSelectedCommand { get; }

    public RelayCommand ToggleWallDrawingCommand { get; }

    public RelayCommand CancelWallDrawingCommand { get; }

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

    public System.Windows.DragDropEffects OnCatalogDragOver(object? payload, double screenX, double screenY)
    {
        if (payload is not CatalogTemplateDragPayload catalogPayload)
        {
            return System.Windows.DragDropEffects.None;
        }

        var template = FindCatalogTemplate(catalogPayload.CabinetTypeId);
        if (template is null)
        {
            StatusMessage = $"Unknown cabinet template '{catalogPayload.CabinetTypeId}'.";
            return System.Windows.DragDropEffects.None;
        }

        EnsureCatalogDragPreview(template, screenX, screenY);
        return System.Windows.DragDropEffects.Copy;
    }

    public async Task<System.Windows.DragDropEffects> OnCatalogDropAsync(object? payload, double screenX, double screenY)
    {
        try
        {
            if (payload is not CatalogTemplateDragPayload catalogPayload)
            {
                return System.Windows.DragDropEffects.None;
            }

            var template = FindCatalogTemplate(catalogPayload.CabinetTypeId);
            if (template is null)
            {
                StatusMessage = $"Unknown cabinet template '{catalogPayload.CabinetTypeId}'.";
                return System.Windows.DragDropEffects.None;
            }

            var worldPoint = _editorSession.Viewport.ToWorld(screenX, screenY);
            DropTarget? dropTarget = ResolveDropTarget(worldPoint);
            if (dropTarget is null)
            {
                StatusMessage = "Select a room before dropping cabinets.";
                return System.Windows.DragDropEffects.None;
            }

            if (dropTarget.RunId is null && dropTarget.WallId is null)
            {
                StatusMessage = "Select a room before dropping cabinets.";
                return System.Windows.DragDropEffects.None;
            }

            if (dropTarget.RunId is not null)
            {
                await _runService.PlaceCabinetAsync(new RunId(dropTarget.RunId.Value), template.TypeId).ConfigureAwait(true);
            }
            else
            {
                if (_editorSession.ActiveRoomId is null)
                {
                    StatusMessage = "Select a room before dropping cabinets.";
                    return System.Windows.DragDropEffects.None;
                }

                var createdRun = await _runService.CreateRunAsync(
                    new RoomId(_editorSession.ActiveRoomId.Value),
                    new WallId(dropTarget.WallId!.Value),
                    dropTarget.StartOffset,
                    template.NominalWidth).ConfigureAwait(true);
                await _runService.PlaceCabinetAsync(createdRun.Id, template.TypeId).ConfigureAwait(true);
            }

            RefreshScene();
            StatusMessage = "Cabinet placed.";
            return System.Windows.DragDropEffects.Copy;
        }
        catch (InvalidOperationException exception)
        {
            UserActionErrorReporter.Report(
                _logger,
                _eventBus,
                "Presentation",
                "canvas.drop-cabinet",
                "Failed to place cabinet from drop gesture.",
                exception);
            StatusMessage = exception.Message == "Cannot place cabinet: run capacity exceeded."
                ? "Cannot place cabinet: run capacity exceeded."
                : exception.Message;
            RefreshScene();
            return System.Windows.DragDropEffects.None;
        }
        finally
        {
            _interactionService.OnDragAborted();
        }
    }

    private CatalogItemDto? FindCatalogTemplate(string cabinetTypeId) =>
        _catalogService.GetAllItems().FirstOrDefault(item =>
            string.Equals(item.TypeId, cabinetTypeId, StringComparison.Ordinal));

    private void EnsureCatalogDragPreview(CatalogItemDto template, double screenX, double screenY)
    {
        if (_editorSession.CurrentMode != EditorMode.PlacingCabinet)
        {
            _interactionService.BeginPlaceCabinet(
                template.TypeId,
                template.NominalWidth,
                template.Depth,
                screenX,
                screenY);
        }

        var worldPoint = _editorSession.Viewport.ToWorld(screenX, screenY);
        var hit = Scene is null
            ? new HitTestResult(HitTestTarget.None, null, null)
            : _hitTester.HitTest(screenX, screenY, Scene, _editorSession.Viewport);

        if (hit.Target == HitTestTarget.Run && hit.EntityId is Guid runId)
        {
            _interactionService.OnDragMoved(screenX, screenY);
            StatusMessage = "Previewing placement.";
        }
        else
        {
            StatusMessage = _editorSession.ActiveRoomId is null
                ? "Drop onto a room wall to create a run."
                : "Previewing cabinet placement.";
        }
    }

    private DropTarget? ResolveDropTarget(Point2D worldPoint)
    {
        if (Scene is null)
        {
            return null;
        }

        var hit = _hitTester.HitTest(
            _editorSession.Viewport.ToScreen(worldPoint).X,
            _editorSession.Viewport.ToScreen(worldPoint).Y,
            Scene,
            _editorSession.Viewport);

        return hit.Target switch
        {
            HitTestTarget.Run when hit.EntityId is Guid runId => new DropTarget(runId, null, Length.Zero),
            HitTestTarget.Cabinet when hit.EntityId is Guid cabinetId => new DropTarget(
                Scene.Cabinets.FirstOrDefault(cabinet => cabinet.CabinetId == cabinetId)?.RunId,
                null,
                Length.Zero),
            HitTestTarget.Wall when hit.EntityId is Guid wallId => BuildWallDropTarget(wallId, worldPoint),
            _ => _editorSession.ActiveRoomId is null ? null : FindNearestWallDropTarget(worldPoint)
        };
    }

    private DropTarget? FindNearestWallDropTarget(Point2D worldPoint)
    {
        if (Scene is null || Scene.Walls.Count == 0)
        {
            return null;
        }

        var nearest = Scene.Walls
            .Select(wall => new
            {
                Wall = wall,
                Distance = DistanceToSegment(worldPoint, wall.Segment),
                Projection = ProjectOntoSegment(worldPoint, wall.Segment)
            })
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault();

        return nearest is null
            ? null
            : BuildWallDropTarget(nearest.Wall.WallId, nearest.Projection);
    }

    private DropTarget? BuildWallDropTarget(Guid wallId, Point2D worldPoint)
    {
        if (Scene is null)
        {
            return null;
        }

        var wall = Scene.Walls.FirstOrDefault(candidate => candidate.WallId == wallId);
        if (wall is null)
        {
            return null;
        }

        if (_editorSession.ActiveRoomId is null)
        {
            return null;
        }

        var startOffset = DistanceAlongSegment(wall.Segment, worldPoint);
        return new DropTarget(null, wall.WallId, startOffset);
    }

    private static Point2D ProjectOntoSegment(Point2D point, LineSegment2D segment)
    {
        var vector = segment.End - segment.Start;
        var lengthSquared = vector.Dx * vector.Dx + vector.Dy * vector.Dy;
        if (lengthSquared <= 0m)
        {
            return segment.Start;
        }

        var toPoint = point - segment.Start;
        var t = (toPoint.Dx * vector.Dx + toPoint.Dy * vector.Dy) / lengthSquared;
        t = Math.Clamp(t, 0m, 1m);
        return new Point2D(segment.Start.X + (vector.Dx * t), segment.Start.Y + (vector.Dy * t));
    }

    private static decimal DistanceToSegment(Point2D point, LineSegment2D segment) =>
        point.DistanceTo(ProjectOntoSegment(point, segment)).Inches;

    private static Length DistanceAlongSegment(LineSegment2D segment, Point2D point)
    {
        var projected = ProjectOntoSegment(point, segment);
        return segment.Start.DistanceTo(projected);
    }

    private sealed record DropTarget(Guid? RunId, Guid? WallId, Length StartOffset);

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

    public async void OnMouseDown(double screenX, double screenY)
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

        if (IsWallDrawingMode)
        {
            if (_scene is null)
            {
                StatusMessage = "Open a project and select a room before drawing walls.";
                return;
            }

            var activeRoomId = _editorSession.ActiveRoomId;
            if (activeRoomId is null)
            {
                StatusMessage = "Select a room before drawing walls.";
                return;
            }

            var worldPoint = _editorSession.Viewport.ToWorld(screenX, screenY);
            var snappedPoint = ResolveWallSnapPoint(worldPoint);
            if (_pendingWallStartWorld is null)
            {
                _pendingWallStartWorld = snappedPoint;
                StatusMessage = "Wall start point set.";
                return;
            }

            if (_pendingWallStartWorld.Value.DistanceTo(snappedPoint) <= Length.Zero)
            {
                StatusMessage = "Wall length must be greater than zero.";
                _pendingWallStartWorld = null;
                return;
            }

            try
            {
                await _roomService.AddWallAsync(
                    new RoomId(activeRoomId.Value),
                    _pendingWallStartWorld.Value,
                    snappedPoint,
                    Thickness.Exact(Length.FromInches(4m)),
                    CancellationToken.None).ConfigureAwait(true);
                StatusMessage = "Wall added.";
            }
            catch (Exception exception)
            {
                UserActionErrorReporter.Report(
                    _logger,
                    _eventBus,
                    "Presentation",
                    "canvas.wall.add",
                    "Unhandled exception while adding a wall.",
                    exception);
                StatusMessage = $"Failed to add wall: {exception.Message}";
            }
            finally
            {
                _pendingWallStartWorld = null;
            }

            return;
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
                UserActionErrorReporter.Report(
                    _logger,
                    _eventBus,
                    "Presentation",
                    "canvas.drag.commit",
                    "Unhandled exception escaping CommitDragAsync.",
                    exception);
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

    public void SetActiveRoom(Guid? roomId)
    {
        if (roomId is null)
        {
            _editorSession.SetActiveRoom(null);
            RefreshScene();
            return;
        }

        _editorSession.SetActiveRoom(roomId);
        RefreshScene();
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

        _editorSession.FitViewport(
            new ViewportBounds(
                (double)bounds.Value.Min.X,
                (double)bounds.Value.Min.Y,
                (double)bounds.Value.Max.X,
                (double)bounds.Value.Max.Y),
            canvasWidth,
            canvasHeight);
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

    private bool CanDeleteSelected() => Scene is not null && _editorSession.SelectedCabinetIds.Count > 0;

    private async Task ExecuteDeleteSelectedAsync()
    {
        if (!CanDeleteSelected())
        {
            return;
        }

        BeginBusy();
        try
        {
            var selectedIds = _editorSession.SelectedCabinetIds.ToArray();
            foreach (var cabinetId in selectedIds)
            {
                await _runService.DeleteCabinetAsync(new CabinetId(cabinetId)).ConfigureAwait(true);
            }

            _editorSession.SetSelectedCabinetIds([]);
            RefreshScene();
            StatusMessage = selectedIds.Length == 1
                ? "Cabinet deleted."
                : $"{selectedIds.Length} cabinets deleted.";
        }
        catch (InvalidOperationException exception)
        {
            UserActionErrorReporter.Report(
                _logger,
                _eventBus,
                "Presentation",
                "canvas.delete-selected",
                "Failed to delete selected cabinet(s).",
                exception);
            StatusMessage = exception.Message;
        }
        finally
        {
            EndBusy();
        }
    }

    private void ToggleWallDrawingMode()
    {
        IsWallDrawingMode = !IsWallDrawingMode;
        StatusMessage = IsWallDrawingMode
            ? "Wall drawing mode enabled."
            : "Wall drawing mode disabled.";
        if (!IsWallDrawingMode)
        {
            _pendingWallStartWorld = null;
        }
    }

    private void CancelWallDrawingMode()
    {
        if (!IsWallDrawingMode)
        {
            return;
        }

        IsWallDrawingMode = false;
        _pendingWallStartWorld = null;
        StatusMessage = "Wall drawing mode cancelled.";
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
            UserActionErrorReporter.Report(
                _logger,
                _eventBus,
                "Presentation",
                "canvas.drag.begin",
                "Could not begin drag; cabinet may have been removed before the drag threshold was reached.",
                invalidOp);
            StatusMessage = invalidOp.Message;
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
            UserActionErrorReporter.Report(
                _logger,
                _eventBus,
                "Presentation",
                "canvas.drag.commit",
                "An unexpected error occurred while committing a drag operation.",
                exception);
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

    private Point2D ResolveWallSnapPoint(Point2D worldPoint)
    {
        var snapRadius = _editorSession.SnapSettings.SnapRadius.Inches;
        var bestPoint = worldPoint;
        var bestDistance = decimal.MaxValue;

        var gridSize = _editorSession.SnapSettings.GridSize.Inches;
        if (gridSize > 0m)
        {
            var snappedX = Math.Round(worldPoint.X / gridSize, MidpointRounding.AwayFromZero) * gridSize;
            var snappedY = Math.Round(worldPoint.Y / gridSize, MidpointRounding.AwayFromZero) * gridSize;
            var gridPoint = new Point2D(snappedX, snappedY);
            var distance = worldPoint.DistanceTo(gridPoint).Inches;
            if (distance <= snapRadius && distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = gridPoint;
            }
        }

        if (_scene is not null)
        {
            foreach (var endpoint in _scene.Walls.SelectMany(wall => new[] { wall.Segment.Start, wall.Segment.End }))
            {
                var distance = worldPoint.DistanceTo(endpoint).Inches;
                if (distance <= snapRadius && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPoint = endpoint;
                }
            }
        }

        return bestPoint;
    }

    private void OnDesignChanged(DesignChangedEvent _) =>
        UiDispatchHelper.Run(() =>
        {
            RefreshScene();
            StatusMessage = "Design updated.";
        });

    private void OnUndoApplied(UndoAppliedEvent _) =>
        UiDispatchHelper.Run(() =>
        {
            RefreshScene();
            StatusMessage = "Undo applied.";
        });

    private void OnRedoApplied(RedoAppliedEvent _) =>
        UiDispatchHelper.Run(() =>
        {
            RefreshScene();
            StatusMessage = "Redo applied.";
        });

    private void OnProjectClosed(ProjectClosedEvent _) =>
        UiDispatchHelper.Run(() =>
        {
            if (_isDragActive || _pendingDragCabinetId is not null)
            {
                _interactionService.OnDragAborted();
            }

            _pendingDragCabinetId = null;
            _isDragActive = false;
            _pendingWallStartWorld = null;
            IsWallDrawingMode = false;
            Scene = null;
            SelectedCabinetIds = [];
            HoveredCabinetId = null;
            _canvasHost.UpdateViewport(_editorSession.Viewport);
            StatusMessage = "Project closed.";
            OnPropertyChanged(nameof(CurrentMode));
        });

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
        SelectAllCommand.NotifyCanExecuteChanged();
        SelectNoneCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
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

    private sealed class NoOpRoomService : IRoomService
    {
        public Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct) =>
            Task.FromException<Room>(new InvalidOperationException("Room service is not configured."));

        public Task<Wall> AddWallAsync(RoomId roomId, Point2D start, Point2D end, Thickness thickness, CancellationToken ct) =>
            Task.FromException<Wall>(new InvalidOperationException("Room service is not configured."));

        public Task RemoveWallAsync(WallId wallId, CancellationToken ct) =>
            Task.FromException(new InvalidOperationException("Room service is not configured."));

        public Task RenameRoomAsync(RoomId roomId, string newName, CancellationToken ct) =>
            Task.FromException(new InvalidOperationException("Room service is not configured."));

        public Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Room>>([]);
    }
}
