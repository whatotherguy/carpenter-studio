using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor.Snap;

namespace CabinetDesigner.Editor;

public sealed class EditorSession
{
    public EditorMode Mode { get; private set; } = EditorMode.Idle;

    public IReadOnlyList<CabinetId> SelectedCabinetIds { get; private set; } = [];

    public RunId? ActiveRunId { get; private set; }

    public CabinetId? HoveredCabinetId { get; private set; }

    public ViewportTransform Viewport { get; private set; } = ViewportTransform.Default;

    public SnapSettings SnapSettings { get; private set; } = SnapSettings.Default;

    public DragContext? ActiveDrag { get; private set; }

    public DrawRunDragContext? ActiveRunDraw { get; private set; }

    public SnapCandidate? PreviousSnapWinner { get; private set; }

    public void BeginMoveDrag(DragContext context)
    {
        AssertMode(EditorMode.Idle);
        ActiveDrag = context;
        PreviousSnapWinner = null;
        Mode = EditorMode.MovingCabinet;
    }

    public void BeginCatalogDrag(DragContext context)
    {
        AssertMode(EditorMode.Idle);
        ActiveDrag = context;
        PreviousSnapWinner = null;
        Mode = EditorMode.PlacingCabinet;
    }

    public void BeginResizeDrag(DragContext context)
    {
        AssertMode(EditorMode.Idle);
        ActiveDrag = context;
        PreviousSnapWinner = null;
        Mode = EditorMode.ResizingCabinet;
    }

    public void UpdateDragCursor(Point2D cursorWorld, RunId? targetRunId)
    {
        if (ActiveDrag is null)
        {
            return;
        }

        ActiveDrag = ActiveDrag with
        {
            CursorWorld = cursorWorld,
            TargetRunId = targetRunId
        };
    }

    public void EndDrag()
    {
        ActiveDrag = null;
        ActiveRunDraw = null;
        PreviousSnapWinner = null;
        Mode = EditorMode.Idle;
    }

    public void AbortDrag() => EndDrag();

    public void SetSelection(IReadOnlyList<CabinetId> ids) => SelectedCabinetIds = ids;

    public void SetSelection(IReadOnlyList<Guid> ids) => SelectedCabinetIds = ids.Select(id => new CabinetId(id)).ToArray();

    public void SetActiveRun(RunId? runId) => ActiveRunId = runId;

    public void SetHover(CabinetId? cabinetId) => HoveredCabinetId = cabinetId;

    public void ApplySnapSettings(SnapSettings snapSettings) => SnapSettings = snapSettings;

    public void RecordSnapWinner(SnapCandidate? winner) => PreviousSnapWinner = winner;

    public void SetViewport(ViewportTransform viewport) => Viewport = viewport;

    public void UpdateViewport(ViewportTransform viewport) => SetViewport(viewport);

    private void AssertMode(EditorMode expected)
    {
        if (Mode != expected)
        {
            throw new InvalidOperationException($"Expected EditorMode.{expected} but was EditorMode.{Mode}.");
        }
    }
}
