using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor.Snap;

namespace CabinetDesigner.Editor;

/// <summary>
/// Holds all transient interaction state for the editor canvas.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Threading model:</strong> <see cref="EditorSession"/> is designed to be
/// UI-thread-affine in production — all mutations are driven by WPF input events or
/// continuations captured with <c>ConfigureAwait(true)</c>.  The internal lock ensures
/// that the compound <c>AssertMode → mutate</c> transitions are atomic, which guards
/// against any future callers that inadvertently cross thread boundaries (e.g. a
/// <c>ConfigureAwait(false)</c> continuation reaching <see cref="EndDrag"/>).
/// </para>
/// <para>
/// Callers that read a property and then call a mutating method in two separate steps
/// (e.g. check <see cref="ActiveDrag"/>, then call <see cref="UpdateDragCursor"/>) must
/// remain on the UI thread so that the WPF dispatcher serialises those two operations.
/// </para>
/// </remarks>
public sealed class EditorSession
{
    // Protects compound Mode + drag-context transitions so that AssertMode and the
    // subsequent state mutation are performed atomically.
    private readonly object _sync = new();

    private EditorMode _mode = EditorMode.Idle;
    private IReadOnlyList<CabinetId> _selectedCabinetIds = [];
    private RunId? _activeRunId;
    private CabinetId? _hoveredCabinetId;
    private ViewportTransform _viewport = ViewportTransform.Default;
    private SnapSettings _snapSettings = SnapSettings.Default;
    private DragContext? _activeDrag;
    private DrawRunDragContext? _activeRunDraw;
    private SnapCandidate? _previousSnapWinner;

    public EditorMode Mode
    {
        get
        {
            lock (_sync) return _mode;
        }
    }

    public IReadOnlyList<CabinetId> SelectedCabinetIds
    {
        get
        {
            lock (_sync) return _selectedCabinetIds;
        }
    }

    public RunId? ActiveRunId
    {
        get
        {
            lock (_sync) return _activeRunId;
        }
    }

    public CabinetId? HoveredCabinetId
    {
        get
        {
            lock (_sync) return _hoveredCabinetId;
        }
    }

    public ViewportTransform Viewport
    {
        get
        {
            lock (_sync) return _viewport;
        }
    }

    public SnapSettings SnapSettings
    {
        get
        {
            lock (_sync) return _snapSettings;
        }
    }

    public DragContext? ActiveDrag
    {
        get
        {
            lock (_sync) return _activeDrag;
        }
    }

    public DrawRunDragContext? ActiveRunDraw
    {
        get
        {
            lock (_sync) return _activeRunDraw;
        }
    }

    public SnapCandidate? PreviousSnapWinner
    {
        get
        {
            lock (_sync) return _previousSnapWinner;
        }
    }

    public void BeginMoveDrag(DragContext context)
    {
        lock (_sync)
        {
            AssertMode(EditorMode.Idle);
            _activeDrag = context;
            _previousSnapWinner = null;
            _mode = EditorMode.MovingCabinet;
        }
    }

    public void BeginCatalogDrag(DragContext context)
    {
        lock (_sync)
        {
            AssertMode(EditorMode.Idle);
            _activeDrag = context;
            _previousSnapWinner = null;
            _mode = EditorMode.PlacingCabinet;
        }
    }

    public void BeginResizeDrag(DragContext context)
    {
        lock (_sync)
        {
            AssertMode(EditorMode.Idle);
            _activeDrag = context;
            _previousSnapWinner = null;
            _mode = EditorMode.ResizingCabinet;
        }
    }

    public void UpdateDragCursor(Point2D cursorWorld, RunId? targetRunId)
    {
        lock (_sync)
        {
            if (_activeDrag is null)
            {
                return;
            }

            _activeDrag = _activeDrag with
            {
                CursorWorld = cursorWorld,
                TargetRunId = targetRunId
            };
        }
    }

    public void EndDrag()
    {
        lock (_sync)
        {
            _activeDrag = null;
            _activeRunDraw = null;
            _previousSnapWinner = null;
            _mode = EditorMode.Idle;
        }
    }

    public void AbortDrag() => EndDrag();

    public void SetSelection(IReadOnlyList<CabinetId> ids)
    {
        // Copy to an immutable array so callers cannot mutate the backing list from
        // outside the lock after this method returns.
        var snapshot = ids.ToArray();
        lock (_sync)
        {
            _selectedCabinetIds = snapshot;
        }
    }

    public void SetSelection(IReadOnlyList<Guid> ids)
    {
        lock (_sync)
        {
            _selectedCabinetIds = ids.Select(id => new CabinetId(id)).ToArray();
        }
    }

    public void SetActiveRun(RunId? runId)
    {
        lock (_sync)
        {
            _activeRunId = runId;
        }
    }

    public void SetHover(CabinetId? cabinetId)
    {
        lock (_sync)
        {
            _hoveredCabinetId = cabinetId;
        }
    }

    public void ApplySnapSettings(SnapSettings snapSettings)
    {
        lock (_sync)
        {
            _snapSettings = snapSettings;
        }
    }

    public void RecordSnapWinner(SnapCandidate? winner)
    {
        lock (_sync)
        {
            _previousSnapWinner = winner;
        }
    }

    public void SetViewport(ViewportTransform viewport)
    {
        lock (_sync)
        {
            _viewport = viewport;
        }
    }

    public void UpdateViewport(ViewportTransform viewport) => SetViewport(viewport);

    // Must be called while _sync is held.
    private void AssertMode(EditorMode expected)
    {
        if (_mode != expected)
        {
            throw new InvalidOperationException($"Expected EditorMode.{expected} but was EditorMode.{_mode}.");
        }
    }
}
