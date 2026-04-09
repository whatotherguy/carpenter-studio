# P7 — Editor Interaction Engine

Source: `cabinet_ai_prompt_pack_v4_1_full.md` (Phase 7)
Context: `architecture_summary.md`, `commands.md`, `geometry_system.md`, `orchestrator.md`, `application_layer.md`

---

## 1. Goals

- Translate raw mouse and keyboard events into previewable and committable design intent
- Manage editor mode, selection, hover, and viewport state — and nothing else
- Evaluate snap candidates on every mouse move without touching committed design state
- Score and rank candidates deterministically: same input + same scene = same winner, always
- Retain the previous snap winner under hysteresis to eliminate visual jitter
- On commit, translate the winning snap candidate into the correct typed `IDesignCommand`
- Route preview commands through `IPreviewCommandHandler` (fast path — stages 1–3 only)
- Route committed commands through `IDesignCommandHandler` (deep path — all 11 stages)
- Work correctly for runs at any wall angle — no assumption of horizontal or axis-aligned layout

---

## 2. Boundaries

### 2.1 What the Editor Layer Owns

| Responsibility | Lives In |
|---|---|
| Editor mode state machine | `CabinetDesigner.Editor` |
| Selection and hover state | `CabinetDesigner.Editor` |
| Viewport transform (screen ↔ world) | `CabinetDesigner.Editor` |
| Active drag context | `CabinetDesigner.Editor` |
| Snap candidate gathering and scoring | `CabinetDesigner.Editor` |
| Hysteresis state between mouse-move calls | `CabinetDesigner.Editor` |
| Translation of winning snap → typed `IDesignCommand` | `CabinetDesigner.Editor` |
| `PlacementCandidateDto` mapping for canvas rendering | `CabinetDesigner.Editor` |

### 2.2 What the Editor Layer Does NOT Own

| Excluded Responsibility | Owned By |
|---|---|
| Domain entities (cabinets, runs, walls) | `CabinetDesigner.Domain` |
| Resolution pipeline execution | `CabinetDesigner.Application` |
| Undo stack | `CabinetDesigner.Application` |
| Why Engine recording | `CabinetDesigner.Application` |
| WPF controls, hit-testing on rendered pixels | `CabinetDesigner.Presentation` / `CabinetDesigner.Rendering` |
| Final canvas rendering | `CabinetDesigner.Rendering` |
| Manufacturing or install logic | `CabinetDesigner.Domain` (downstream stages) |

### 2.3 Dependency Direction

```
CabinetDesigner.Presentation
    └──▶ CabinetDesigner.Editor
              └──▶ CabinetDesigner.Application
                        └──▶ CabinetDesigner.Domain
```

The Editor layer depends on Application interfaces. Application does not depend on Editor.

---

## 3. Editor State Model

### 3.1 EditorMode

```csharp
namespace CabinetDesigner.Editor;

public enum EditorMode
{
    Idle,              // No active operation; pointer tool active
    Selecting,         // Rubber-band box selection in progress
    PlacingCabinet,    // Dragging a new cabinet from the catalog panel
    MovingCabinet,     // Repositioning an existing cabinet within or between runs
    DrawingRun,        // Tracing a new run along a wall (two-phase: anchor, then endpoint)
    ResizingCabinet,   // Dragging a cabinet right-edge resize handle
    PanningViewport,   // Middle-mouse pan; all snap evaluation suspended
    ZoomingViewport,   // Scroll-wheel zoom; all snap evaluation suspended
}
```

**Valid mode transitions:**

```
Idle ──▶ PlacingCabinet    (catalog drag begins)
Idle ──▶ MovingCabinet     (existing cabinet grab begins)
Idle ──▶ DrawingRun        (wall click begins)
Idle ──▶ ResizingCabinet   (resize handle grab begins)
Idle ──▶ Selecting         (empty-space drag begins)
Idle ──▶ PanningViewport   (middle-mouse down)
Any  ──▶ Idle              (commit, abort, or Escape)
```

Transitions not listed above are illegal. The state machine must enforce this explicitly and throw `InvalidOperationException` on illegal transitions during development.

### 3.2 EditorSession

Holds all mutable interaction state for one open project. Replaced entirely on project open/close. Never persisted. Never referenced by the domain or orchestrator.

```csharp
namespace CabinetDesigner.Editor;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor.Snap;

/// <summary>
/// All mutable editor-local state for an active project.
/// Lives in CabinetDesigner.Editor.
/// Reset on project open/close.
/// Never referenced by the domain, orchestrator, or persistence layers.
/// </summary>
public sealed class EditorSession
{
    // ── Mode ──────────────────────────────────────────────────────────────
    public EditorMode Mode { get; private set; } = EditorMode.Idle;

    // ── Selection ─────────────────────────────────────────────────────────
    public IReadOnlyList<CabinetId> SelectedCabinetIds { get; private set; } = [];
    public RunId? ActiveRunId { get; private set; }
    public CabinetId? HoveredCabinetId { get; private set; }

    // ── Active drag ───────────────────────────────────────────────────────
    public DragContext? ActiveDrag { get; private set; }
    public DrawRunDragContext? ActiveRunDraw { get; private set; }

    // ── Viewport ──────────────────────────────────────────────────────────
    public ViewportTransform Viewport { get; private set; } = ViewportTransform.Default;

    // ── Snap settings ─────────────────────────────────────────────────────
    public SnapSettings SnapSettings { get; private set; } = SnapSettings.Default;

    // ── Hysteresis ────────────────────────────────────────────────────────
    /// <summary>
    /// Retains the previous snap winner between mouse-move evaluations.
    /// Used by ISnapResolver to apply a stickiness bonus and suppress jitter.
    /// Reset to null when a drag begins or ends.
    /// </summary>
    public SnapCandidate? PreviousSnapWinner { get; private set; }

    // ── Transitions ───────────────────────────────────────────────────────

    public void BeginCatalogDrag(DragContext context)
    {
        AssertMode(EditorMode.Idle);
        ActiveDrag = context;
        PreviousSnapWinner = null;
        Mode = EditorMode.PlacingCabinet;
    }

    public void BeginMoveDrag(DragContext context)
    {
        AssertMode(EditorMode.Idle);
        ActiveDrag = context;
        PreviousSnapWinner = null;
        Mode = EditorMode.MovingCabinet;
    }

    public void BeginResizeDrag(DragContext context)
    {
        AssertMode(EditorMode.Idle);
        ActiveDrag = context;
        PreviousSnapWinner = null;
        Mode = EditorMode.ResizingCabinet;
    }

    public void BeginRunDraw(DrawRunDragContext context)
    {
        AssertMode(EditorMode.Idle);
        ActiveRunDraw = context;
        PreviousSnapWinner = null;
        Mode = EditorMode.DrawingRun;
    }

    public void UpdateDragCursor(Point2D cursorWorld, RunId? targetRunId)
    {
        if (ActiveDrag is null) return;
        ActiveDrag = ActiveDrag with
        {
            CursorWorld = cursorWorld,
            TargetRunId = targetRunId
        };
    }

    public void UpdateRunDrawEndpoint(Point2D endpointWorld)
    {
        if (ActiveRunDraw is null) return;
        ActiveRunDraw = ActiveRunDraw with { CurrentEndWorld = endpointWorld };
    }

    public void RecordSnapWinner(SnapCandidate? winner)
    {
        PreviousSnapWinner = winner;
    }

    public void EndDrag()
    {
        ActiveDrag = null;
        ActiveRunDraw = null;
        PreviousSnapWinner = null;
        Mode = EditorMode.Idle;
    }

    public void AbortDrag()
    {
        ActiveDrag = null;
        ActiveRunDraw = null;
        PreviousSnapWinner = null;
        Mode = EditorMode.Idle;
    }

    public void SetSelection(IReadOnlyList<CabinetId> ids)
    {
        SelectedCabinetIds = ids;
        ActiveRunId = null;
    }

    public void SetActiveRun(RunId? runId) => ActiveRunId = runId;
    public void SetHover(CabinetId? id) => HoveredCabinetId = id;
    public void UpdateViewport(ViewportTransform t) => Viewport = t;
    public void ApplySnapSettings(SnapSettings s) => SnapSettings = s;

    private void AssertMode(EditorMode expected)
    {
        if (Mode != expected)
            throw new InvalidOperationException(
                $"Expected EditorMode.{expected} but was EditorMode.{Mode}.");
    }
}
```

### 3.3 ViewportTransform

Converts between screen pixels and world-space inches. Lives in `CabinetDesigner.Editor`. Never exposed to the domain.

```csharp
namespace CabinetDesigner.Editor;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Maps between screen pixels (double) and world-space inches (decimal).
/// Scale is pixels-per-inch. Offset is the screen-pixel position of world origin.
/// All internal snap math uses world coordinates (Point2D / decimal inches).
/// Screen coordinates are only handled at the Presentation boundary.
/// </summary>
public sealed record ViewportTransform(
    decimal ScalePixelsPerInch,
    decimal OffsetXPixels,
    decimal OffsetYPixels)
{
    public static readonly ViewportTransform Default = new(10m, 0m, 0m);

    /// <summary>Screen pixel → world-space (decimal inches). Entry point from mouse events.</summary>
    public Point2D ToWorld(double screenX, double screenY) =>
        new(((decimal)screenX - OffsetXPixels) / ScalePixelsPerInch,
            ((decimal)screenY - OffsetYPixels) / ScalePixelsPerInch);

    /// <summary>World-space (decimal inches) → screen pixels. Used by the rendering layer.</summary>
    public (double X, double Y) ToScreen(Point2D world) =>
        ((double)(world.X * ScalePixelsPerInch + OffsetXPixels),
         (double)(world.Y * ScalePixelsPerInch + OffsetYPixels));

    public ViewportTransform WithScale(decimal scale) =>
        this with { ScalePixelsPerInch = scale };

    public ViewportTransform Panned(decimal deltaXPixels, decimal deltaYPixels) =>
        this with
        {
            OffsetXPixels = OffsetXPixels + deltaXPixels,
            OffsetYPixels = OffsetYPixels + deltaYPixels
        };
}
```

---

## 4. Drag Lifecycle

### 4.1 DragContext

Carries all data required to evaluate snap candidates and construct the committed command for any cabinet-level drag operation. Immutable record — updated by replacing the whole instance on `EditorSession.UpdateDragCursor`.

```csharp
namespace CabinetDesigner.Editor;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public enum DragType
{
    PlaceCabinet,    // New cabinet from catalog
    MoveCabinet,     // Existing cabinet repositioned
    ResizeCabinet,   // Right-edge handle drag (width change)
}

/// <summary>
/// Immutable snapshot of a single drag operation's parameters.
/// Updated (replaced) on every mouse-move via EditorSession.UpdateDragCursor.
///
/// Reference point semantics:
///   PlaceCabinet  — reference point is the cabinet's left edge on the run axis
///   MoveCabinet   — reference point is the cabinet's left edge (grab offset applied before snapping)
///   ResizeCabinet — reference point is the cabinet's right edge (left edge is the fixed anchor)
/// </summary>
public sealed record DragContext(
    DragType DragType,

    /// <summary>Current cursor position in world-space inches (updated per mouse-move).</summary>
    Point2D CursorWorld,

    /// <summary>
    /// Offset from cursor to the cabinet reference point, in world-space inches.
    /// For PlaceCabinet: Vector2D(NominalWidth.Inches / 2, 0) — grabs at center of icon.
    /// For MoveCabinet: cursorAtGrabTime − cabinetLeftEdgeAtGrabTime.
    /// For ResizeCabinet: Vector2D.Zero — cursor IS the right edge.
    /// Applied as: candidateRefPoint = CursorWorld − GrabOffset.
    /// </summary>
    Vector2D GrabOffset,

    /// <summary>Nominal width of the cabinet being dragged.</summary>
    Length NominalWidth,

    /// <summary>Nominal depth of the cabinet (used for collision bounds; not yet in snap math).</summary>
    Length NominalDepth,

    /// <summary>
    /// Cabinet type being placed or moved.
    /// Null only for ResizeCabinet (type does not change during resize).
    /// </summary>
    string? CabinetTypeId,

    /// <summary>
    /// For MoveCabinet and ResizeCabinet: the subject cabinet.
    /// Null for PlaceCabinet.
    /// </summary>
    CabinetId? SubjectCabinetId,

    /// <summary>
    /// For MoveCabinet: the run the cabinet is currently in.
    /// Null for PlaceCabinet and ResizeCabinet.
    /// </summary>
    RunId? SourceRunId,

    /// <summary>
    /// For ResizeCabinet: the fixed left edge of the cabinet in world-space (run axis projection).
    /// Snap candidates for resize are measured as (snapPoint − FixedLeftEdge) along run axis.
    /// Null for other drag types.
    /// </summary>
    Point2D? FixedLeftEdgeWorld,

    /// <summary>
    /// The run currently under the cursor (hit-tested by IEditorSceneGraph).
    /// May change as the cursor crosses run boundaries.
    /// Null when cursor is not over any run.
    /// </summary>
    RunId? TargetRunId)
{
    /// <summary>
    /// Computes the candidate reference point by subtracting the grab offset from the cursor.
    /// This is what the snap engine projects onto the run axis.
    /// </summary>
    public Point2D CandidateRefPoint => CursorWorld - GrabOffset;
}
```

### 4.2 DrawRunDragContext

Draw-run is a two-phase drag. The snap engine runs separately on the start phase (find a wall endpoint or wall segment) and the end phase (find an endpoint that completes a valid run).

```csharp
namespace CabinetDesigner.Editor;

using CabinetDesigner.Domain.Geometry;

public enum DrawRunPhase
{
    SettingStart,   // First click — snap start point to wall
    SettingEnd,     // Second click / drag — snap end point along wall
}

public sealed record DrawRunDragContext(
    DrawRunPhase Phase,

    /// <summary>Snapped world-space start point. Set after phase 1 commits.</summary>
    Point2D? StartWorld,

    /// <summary>Current cursor world position (updated per mouse-move during phase 2).</summary>
    Point2D CurrentEndWorld,

    /// <summary>WallId the start point snapped to. Set after phase 1.</summary>
    string? WallId);
```

### 4.3 Drag Lifecycle Sequence

```
── PlaceCabinet ──────────────────────────────────────────────────────────────

1. User drags from catalog panel
   → Presentation calls IEditorInteractionService.BeginPlaceCabinet(...)
   → EditorSession.BeginCatalogDrag(DragContext)
   → Mode = PlacingCabinet

2. Mouse moves across canvas
   → MouseMove → IEditorInteractionService.OnDragMoved(screenX, screenY)
   → ViewportTransform.ToWorld → CursorWorld
   → IEditorSceneGraph.HitTestRun(CursorWorld) → TargetRunId?
   → EditorSession.UpdateDragCursor(CursorWorld, TargetRunId?)
   → ISnapResolver.Resolve(...) → SnapCandidate? winner
   → EditorSession.RecordSnapWinner(winner)
   → IPreviewCommandHandler.Preview(command) → PreviewResultDto
   → Map to PlacementCandidateDto[] → return PreviewResultDto to ViewModel

3. Mouse released / drop
   → IEditorInteractionService.OnDragCommitted()
   → Resolve final winner (same resolver, same cursor)
   → BuildCommand(PlaceCabinet, winner, drag) → AddCabinetToRunCommand
   → IDesignCommandHandler.Execute(command)
   → EditorSession.EndDrag()

4. Escape / cancel
   → IEditorInteractionService.OnDragAborted()
   → EditorSession.AbortDrag()
   → Mode = Idle; no command submitted

── DrawRun ───────────────────────────────────────────────────────────────────

Phase 1 — Setting Start:
1. User clicks wall → IEditorInteractionService.BeginDrawRun(screenX, screenY)
   → Snap to nearest wall endpoint or wall segment
   → If no valid snap: ignore click
   → DrawRunDragContext{ Phase=SettingEnd, StartWorld=snappedPoint, WallId=... }
   → Mode = DrawingRun

Phase 2 — Setting End:
2. Mouse moves → OnDragMoved → snap to wall endpoints, wall-aligned points
3. Click to commit → OnDragCommitted → CreateRunCommand
4. Escape → AbortDrag
```

---

## 5. Scene Graph Contract

### 5.1 IEditorSceneGraph

A read-only, editor-layer projection of authoritative design state. Updated by subscribing to `DesignChangedEvent`. Never mutated from inside the editor or snap pipeline. Never exposes domain aggregates — only flat view records.

```csharp
namespace CabinetDesigner.Editor;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Read-only, editor-local projection of the current design state.
/// Populated from DesignChangedEvent and ProjectOpenedEvent.
/// Provides spatial query APIs for snap evaluation and hit testing.
/// Consistency guarantee: all views reflect the same committed state.
/// Stale during a fast-path drag — snap candidates are opportunistic, not authoritative.
/// </summary>
public interface IEditorSceneGraph
{
    // ── Geometry ───────────────────────────────────────────────────────────
    IReadOnlyList<WallSegmentView> Walls { get; }
    IReadOnlyList<RunView> Runs { get; }

    // ── Spatial queries ───────────────────────────────────────────────────
    /// <summary>Returns the run hit by the world-space point, or null.</summary>
    RunView? HitTestRun(Point2D worldPoint, Length hitRadius);

    /// <summary>Returns the cabinet slot hit by the world-space point, or null.</summary>
    RunSlotView? HitTestCabinet(Point2D worldPoint, Length hitRadius);

    /// <summary>
    /// All wall endpoints (corners, termini) in world-space inches.
    /// Used by WallEndpointSnapSource.
    /// </summary>
    IReadOnlyList<Point2D> WallEndpoints { get; }

    /// <summary>
    /// All run boundary points (start and end of every run).
    /// Used by RunEndSnapSource.
    /// </summary>
    IReadOnlyList<RunBoundaryView> RunBoundaries { get; }

    /// <summary>
    /// All exposed cabinet face points (left and right face of every slot in every run).
    /// Used by CabinetFaceSnapSource.
    /// </summary>
    IReadOnlyList<CabinetFaceView> CabinetFaces { get; }
}
```

### 5.2 View Records

```csharp
namespace CabinetDesigner.Editor;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record WallSegmentView(
    string WallId,
    LineSegment2D Segment)
{
    /// <summary>Unit direction vector of this wall segment, in world space.</summary>
    public Vector2D Axis => Segment.Direction();
}

public sealed record RunView(
    RunId RunId,
    string WallId,

    /// <summary>
    /// World-space line segment from run start to run end.
    /// For an angled wall: Extent.Start and Extent.End may have arbitrary X and Y.
    /// All run-relative snap math must project onto Extent.Direction(), not onto a fixed axis.
    /// </summary>
    LineSegment2D Extent,

    /// <summary>Unit direction vector of the run axis (same as underlying wall direction).</summary>
    Vector2D Axis,

    Length TotalNominalWidth,
    IReadOnlyList<RunSlotView> Slots);

public sealed record RunSlotView(
    CabinetId CabinetId,
    string CabinetTypeId,
    Length NominalWidth,

    /// <summary>
    /// Left face position in world-space inches.
    /// For an angled wall: LeftFaceWorld = RunOrigin + (cumulativeOffset * RunAxis).
    /// </summary>
    Point2D LeftFaceWorld,

    /// <summary>Right face position in world-space inches.</summary>
    Point2D RightFaceWorld,

    int Index);

public sealed record RunBoundaryView(
    RunId RunId,
    Point2D Point,
    bool IsStart,
    Vector2D RunAxis);    // Direction vector of the run at this boundary

public sealed record CabinetFaceView(
    CabinetId CabinetId,
    RunId RunId,
    Point2D FaceWorld,
    bool IsLeftFace,
    Vector2D RunAxis);    // Direction vector of the run at this face
```

### 5.3 Scene Graph Update Strategy

The `EditorSceneGraph` implementation subscribes to `IApplicationEventBus`:

```csharp
// On DesignChangedEvent: rebuild all views from the projected run/wall data
// On ProjectOpenedEvent: full rebuild
// On ProjectClosedEvent: clear all views, reset to empty
```

The scene graph does **not** hold domain aggregates. It projects only the data that snap evaluation requires. Snap evaluation never calls any domain service; it works entirely from this read-only cache.

---

## 6. Snapping Architecture

### 6.1 SnapSettings

```csharp
namespace CabinetDesigner.Editor.Snap;

using CabinetDesigner.Domain.Geometry;

public sealed record SnapSettings(
    bool SnapToWallEndpoints,
    bool SnapToRunEnds,
    bool SnapToCabinetFaces,
    bool SnapToStandardWidths,
    bool SnapToGrid,

    /// <summary>
    /// Maximum world-space distance at which a candidate activates.
    /// A cursor must be within this radius of a snap point for the candidate to be produced.
    /// </summary>
    Length SnapRadius,

    /// <summary>
    /// Hysteresis bonus added to the previous winner's score.
    /// A challenger must exceed PreviousScore + HysteresisBonus to displace the winner.
    /// Prevents snap jitter when the cursor sits between two candidates of similar score.
    /// </summary>
    decimal HysteresisBonus,

    /// <summary>
    /// Standard cabinet widths available for width snapping.
    /// All values must be positive and in ascending order.
    /// </summary>
    IReadOnlyList<Length> StandardWidths,

    /// <summary>Grid snap interval (only relevant when SnapToGrid = true).</summary>
    Length GridInterval)
{
    public static readonly SnapSettings Default = new(
        SnapToWallEndpoints:  true,
        SnapToRunEnds:        true,
        SnapToCabinetFaces:   true,
        SnapToStandardWidths: true,
        SnapToGrid:           false,
        SnapRadius:           Length.FromInches(1.0m),
        HysteresisBonus:      5m,
        StandardWidths:       StandardCabinetWidths.Common,
        GridInterval:         Length.FromInches(3.0m));
}

public static class StandardCabinetWidths
{
    public static readonly IReadOnlyList<Length> Common =
    [
        Length.FromInches(9m),  Length.FromInches(12m), Length.FromInches(15m),
        Length.FromInches(18m), Length.FromInches(21m), Length.FromInches(24m),
        Length.FromInches(27m), Length.FromInches(30m), Length.FromInches(33m),
        Length.FromInches(36m), Length.FromInches(42m), Length.FromInches(48m),
    ];
}
```

### 6.2 SnapType

Ordered by natural priority. The ordinal value is used as a tie-breaking factor in scoring. Do not reorder without updating scoring constants.

```csharp
namespace CabinetDesigner.Editor.Snap;

public enum SnapType
{
    Grid            = 0,   // Background grid interval — lowest priority
    StandardWidth   = 10,  // Nearest standard cabinet width along run axis
    CabinetFace     = 20,  // Adjacent cabinet's exposed face (left or right)
    RunEnd          = 30,  // Existing run start or end boundary
    WallEndpoint    = 40,  // Wall corner, terminus, or opening edge — highest priority
}
```

### 6.3 SnapCandidate

A single evaluated snap target. Must carry enough information to support orientation, axis-aligned guides, width projection, and Why Engine labeling — not just a point.

```csharp
namespace CabinetDesigner.Editor.Snap;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// One snap target in world space.
///
/// SnapPoint:         Where the dragged element's reference point would land.
/// AlignmentAxis:     Unit direction vector of the wall/run at the snap point.
///                    Used by the rendering layer to draw orientation guides.
///                    Also used during command construction to orient placed elements.
/// ProjectedDistance: Signed distance from the run origin to SnapPoint along AlignmentAxis.
///                    For standard width snaps, this equals the target cumulative width.
///                    For move/resize, this is the offset from run start to the reference edge.
///                    Null for snaps not associated with a run (e.g., grid, wall endpoint only).
/// Distance:          World-space distance from CandidateRefPoint to SnapPoint (for scoring).
/// Label:             Human-readable description for debug overlay and Why Engine.
/// AnchorEntityId:    The entity this snap is anchored to (wall ID, run ID, cabinet ID).
///                    Used by the rendering layer to highlight the anchor entity.
/// SourceIndex:       Insertion order from the producing ISnapCandidateSource.
///                    Used as the final tie-breaker after all other factors are equal.
/// </summary>
public sealed record SnapCandidate(
    SnapType Type,
    Point2D SnapPoint,
    Vector2D AlignmentAxis,
    decimal? ProjectedDistance,
    Length Distance,
    string Label,
    string? AnchorEntityId = null,
    int SourceIndex = 0);
```

### 6.4 Run-Axis Projection Math (Arbitrary Wall Direction)

All run-relative snap math must work for walls at any angle. The key primitive is projection of a world-space point onto the run axis.

```csharp
namespace CabinetDesigner.Editor.Snap;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Static geometry helpers for run-axis projection.
/// All methods work for walls at any angle.
/// Internal intermediate values use double for Math.Sqrt but results are re-wrapped in decimal.
/// </summary>
public static class RunAxisProjection
{
    /// <summary>
    /// Project a world-space point onto the run axis.
    /// Returns the signed distance t from runOrigin along runAxis,
    /// and the projected Point2D on the axis.
    ///
    ///   t = (point − runOrigin) · runAxis
    ///   projectedPoint = runOrigin + t * runAxis
    ///
    /// runAxis must be a unit vector (use RunView.Axis which calls Segment.Direction()).
    /// t is in inches. Positive = in the run direction; negative = behind run origin.
    /// </summary>
    public static (decimal T, Point2D ProjectedPoint) ProjectOntoAxis(
        Point2D point,
        Point2D runOrigin,
        Vector2D runAxis)
    {
        var delta = point - runOrigin;   // Vector2D
        var t = delta.Dot(runAxis);      // decimal — signed distance along axis
        var projected = runOrigin + runAxis * t;
        return (t, projected);
    }

    /// <summary>
    /// Given a signed run-axis distance t, compute the world-space point on the axis.
    /// Used to convert a standard width value into a world-space snap point.
    /// </summary>
    public static Point2D PointAtDistance(Point2D runOrigin, Vector2D runAxis, decimal t)
        => runOrigin + runAxis * t;

    /// <summary>
    /// Compute the world-space position of a point that is:
    /// - t inches along the run axis from runOrigin
    /// - perp inches perpendicular to the run axis (positive = left of travel direction)
    /// Used for depth snapping or clearance checks (not MVP).
    /// </summary>
    public static Point2D PointAtRunCoordinates(
        Point2D runOrigin,
        Vector2D runAxis,
        decimal t,
        decimal perp = 0m)
    {
        var perpAxis = runAxis.PerpendicularCCW();
        return runOrigin + runAxis * t + perpAxis * perp;
    }
}
```

### 6.5 ISnapCandidateSource Contract

```csharp
namespace CabinetDesigner.Editor.Snap;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Produces snap candidates of a specific type for a given evaluation context.
/// Each implementation is focused on exactly one SnapType.
/// Sources are stateless — all state comes from the injected IEditorSceneGraph and parameters.
/// Must be fast: called on every mouse-move event during drag.
/// Must never mutate design state or editor session.
/// </summary>
public interface ISnapCandidateSource
{
    SnapType SnapType { get; }

    /// <summary>
    /// Returns all candidates within settings.SnapRadius of candidateRefPoint.
    /// candidateRefPoint = DragContext.CandidateRefPoint (cursor − grabOffset).
    /// SourceIndex must be set to the sequential index of each returned candidate
    /// so that tie-breaking is deterministic across calls.
    /// </summary>
    IReadOnlyList<SnapCandidate> GetCandidates(
        Point2D candidateRefPoint,
        DragContext drag,
        SnapSettings settings,
        IEditorSceneGraph scene);
}
```

### 6.6 Concrete Source Implementations

#### WallEndpointSnapSource

```csharp
namespace CabinetDesigner.Editor.Snap.Sources;

using CabinetDesigner.Domain.Geometry;

public sealed class WallEndpointSnapSource : ISnapCandidateSource
{
    public SnapType SnapType => SnapType.WallEndpoint;

    public IReadOnlyList<SnapCandidate> GetCandidates(
        Point2D refPoint,
        DragContext drag,
        SnapSettings settings,
        IEditorSceneGraph scene)
    {
        if (!settings.SnapToWallEndpoints) return [];

        var results = new List<SnapCandidate>();
        var index = 0;

        foreach (var wall in scene.Walls)
        {
            foreach (var ep in new[] { wall.Segment.Start, wall.Segment.End })
            {
                var dist = refPoint.DistanceTo(ep);
                if (dist <= settings.SnapRadius)
                {
                    results.Add(new SnapCandidate(
                        Type:               SnapType.WallEndpoint,
                        SnapPoint:          ep,
                        AlignmentAxis:      wall.Axis,
                        ProjectedDistance:  null,   // not run-relative
                        Distance:           dist,
                        Label:              $"Wall endpoint ({ep.X:F3}\", {ep.Y:F3}\")",
                        AnchorEntityId:     wall.WallId,
                        SourceIndex:        index++));
                }
            }
        }

        return results;
    }
}
```

#### RunEndSnapSource

```csharp
namespace CabinetDesigner.Editor.Snap.Sources;

using CabinetDesigner.Domain.Geometry;

public sealed class RunEndSnapSource : ISnapCandidateSource
{
    public SnapType SnapType => SnapType.RunEnd;

    public IReadOnlyList<SnapCandidate> GetCandidates(
        Point2D refPoint,
        DragContext drag,
        SnapSettings settings,
        IEditorSceneGraph scene)
    {
        if (!settings.SnapToRunEnds) return [];

        var results = new List<SnapCandidate>();
        var index = 0;

        foreach (var boundary in scene.RunBoundaries)
        {
            var dist = refPoint.DistanceTo(boundary.Point);
            if (dist <= settings.SnapRadius)
            {
                var label = boundary.IsStart
                    ? $"Run start ({boundary.Point.X:F3}\", {boundary.Point.Y:F3}\")"
                    : $"Run end ({boundary.Point.X:F3}\", {boundary.Point.Y:F3}\")";

                results.Add(new SnapCandidate(
                    Type:               SnapType.RunEnd,
                    SnapPoint:          boundary.Point,
                    AlignmentAxis:      boundary.RunAxis,
                    ProjectedDistance:  null,
                    Distance:           dist,
                    Label:              label,
                    AnchorEntityId:     boundary.RunId.Value.ToString(),
                    SourceIndex:        index++));
            }
        }

        return results;
    }
}
```

#### CabinetFaceSnapSource

```csharp
namespace CabinetDesigner.Editor.Snap.Sources;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Snaps to exposed cabinet faces within snap radius.
/// Skips the subject cabinet's own faces during a move operation.
/// </summary>
public sealed class CabinetFaceSnapSource : ISnapCandidateSource
{
    public SnapType SnapType => SnapType.CabinetFace;

    public IReadOnlyList<SnapCandidate> GetCandidates(
        Point2D refPoint,
        DragContext drag,
        SnapSettings settings,
        IEditorSceneGraph scene)
    {
        if (!settings.SnapToCabinetFaces) return [];

        var results = new List<SnapCandidate>();
        var index = 0;

        foreach (var face in scene.CabinetFaces)
        {
            // Skip self-faces during move (avoid snapping to the cabinet being dragged)
            if (drag.DragType == DragType.MoveCabinet &&
                face.CabinetId == drag.SubjectCabinetId)
                continue;

            var dist = refPoint.DistanceTo(face.FaceWorld);
            if (dist <= settings.SnapRadius)
            {
                var label = face.IsLeftFace
                    ? $"Cabinet left face ({face.FaceWorld.X:F3}\", {face.FaceWorld.Y:F3}\")"
                    : $"Cabinet right face ({face.FaceWorld.X:F3}\", {face.FaceWorld.Y:F3}\")";

                results.Add(new SnapCandidate(
                    Type:               SnapType.CabinetFace,
                    SnapPoint:          face.FaceWorld,
                    AlignmentAxis:      face.RunAxis,
                    ProjectedDistance:  null,
                    Distance:           dist,
                    Label:              label,
                    AnchorEntityId:     face.CabinetId.Value.ToString(),
                    SourceIndex:        index++));
            }
        }

        return results;
    }
}
```

#### StandardWidthSnapSource

Snaps to standard cabinet widths measured from the run origin along the run axis. Works for any wall angle because all math is axis-projected.

```csharp
namespace CabinetDesigner.Editor.Snap.Sources;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Produces snap candidates at cumulative standard widths along the active run axis.
/// For PlaceCabinet: generates snap points at 9", 12", 15", ... from run origin.
/// For ResizeCabinet: generates snap points at standard widths measured from the fixed left edge.
/// Works for any wall angle by projecting all math onto the run axis.
/// </summary>
public sealed class StandardWidthSnapSource : ISnapCandidateSource
{
    public SnapType SnapType => SnapType.StandardWidth;

    public IReadOnlyList<SnapCandidate> GetCandidates(
        Point2D refPoint,
        DragContext drag,
        SnapSettings settings,
        IEditorSceneGraph scene)
    {
        if (!settings.SnapToStandardWidths) return [];

        // Requires a target run
        if (drag.TargetRunId is null) return [];

        var run = scene.Runs.FirstOrDefault(r => r.RunId == drag.TargetRunId);
        if (run is null) return [];

        var results = new List<SnapCandidate>();
        var index = 0;

        // Determine the axis origin for width measurement
        Point2D axisOrigin = drag.DragType == DragType.ResizeCabinet && drag.FixedLeftEdgeWorld.HasValue
            ? drag.FixedLeftEdgeWorld.Value
            : run.Extent.Start;

        foreach (var width in settings.StandardWidths)
        {
            // Compute snap point at this width along the run axis from axisOrigin
            var snapPoint = RunAxisProjection.PointAtDistance(
                axisOrigin, run.Axis, width.Inches);

            var dist = refPoint.DistanceTo(snapPoint);
            if (dist <= settings.SnapRadius)
            {
                results.Add(new SnapCandidate(
                    Type:               SnapType.StandardWidth,
                    SnapPoint:          snapPoint,
                    AlignmentAxis:      run.Axis,
                    ProjectedDistance:  width.Inches,
                    Distance:           dist,
                    Label:              $"Standard width {width}",
                    AnchorEntityId:     run.RunId.Value.ToString(),
                    SourceIndex:        index++));
            }
        }

        return results;
    }
}
```

#### GridSnapSource

```csharp
namespace CabinetDesigner.Editor.Snap.Sources;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Snaps to the nearest grid point when SnapToGrid is enabled.
/// Grid is world-axis aligned (not run-axis aligned) at the configured interval.
/// Lowest priority snap type — only wins when nothing else is in range.
/// </summary>
public sealed class GridSnapSource : ISnapCandidateSource
{
    public SnapType SnapType => SnapType.Grid;

    public IReadOnlyList<SnapCandidate> GetCandidates(
        Point2D refPoint,
        DragContext drag,
        SnapSettings settings,
        IEditorSceneGraph scene)
    {
        if (!settings.SnapToGrid) return [];

        var interval = settings.GridInterval.Inches;
        if (interval <= 0m) return [];

        // Round refPoint to nearest grid point
        var snappedX = Math.Round(refPoint.X / interval, MidpointRounding.AwayFromZero) * interval;
        var snappedY = Math.Round(refPoint.Y / interval, MidpointRounding.AwayFromZero) * interval;
        var snapPoint = new Point2D(snappedX, snappedY);

        var dist = refPoint.DistanceTo(snapPoint);
        if (dist > settings.SnapRadius) return [];

        return
        [
            new SnapCandidate(
                Type:               SnapType.Grid,
                SnapPoint:          snapPoint,
                AlignmentAxis:      Vector2D.UnitX,   // Grid has no run orientation
                ProjectedDistance:  null,
                Distance:           dist,
                Label:              $"Grid ({snappedX:F2}\", {snappedY:F2}\")",
                SourceIndex:        0)
        ];
    }
}
```

---

## 7. Scoring and Tie-Breaking

### 7.1 Score Model

```csharp
namespace CabinetDesigner.Editor.Snap;

public sealed record ScoredCandidate(
    SnapCandidate Candidate,

    /// <summary>
    /// Composite score. Higher = preferred.
    /// Components: TypePriority (0–40) + Proximity (0–40) + AlignmentBonus (0–10) + HysteresisBonus (0 or HysteresisBonus setting).
    /// Not persisted or stored — valid only for one resolver invocation.
    /// </summary>
    decimal Score);
```

### 7.2 ISnapScorer

```csharp
namespace CabinetDesigner.Editor.Snap;

/// <summary>
/// Computes the raw score for a single candidate given drag and settings context.
/// Does NOT apply the hysteresis bonus — that is the resolver's responsibility.
/// Must be deterministic: identical inputs must produce identical output.
/// </summary>
public interface ISnapScorer
{
    ScoredCandidate Score(SnapCandidate candidate, DragContext drag, SnapSettings settings);
}
```

### 7.3 DefaultSnapScorer

```csharp
namespace CabinetDesigner.Editor.Snap;

using CabinetDesigner.Domain.Geometry;

public sealed class DefaultSnapScorer : ISnapScorer
{
    // Type priority: maps SnapType ordinal to base score (0–40)
    // Uses the SnapType ordinal directly, scaled to [0, 40].
    // WallEndpoint(40) → 40, RunEnd(30) → 30, CabinetFace(20) → 20,
    // StandardWidth(10) → 10, Grid(0) → 0.
    private const decimal MaxTypeScore      = 40m;
    private const decimal MaxProximityScore = 40m;
    private const decimal MaxAlignmentBonus = 10m;

    public ScoredCandidate Score(
        SnapCandidate candidate,
        DragContext drag,
        SnapSettings settings)
    {
        // 1. Type priority (0–40)
        // SnapType ordinal already encodes priority. Scale to [0, MaxTypeScore].
        var typePriority = (decimal)candidate.Type;   // 0 | 10 | 20 | 30 | 40

        // 2. Proximity (0–40)
        // Linear decay: 0 distance → 40 pts, distance ≥ SnapRadius → 0 pts.
        // Clamped to [0, MaxProximityScore].
        var proximityFraction = settings.SnapRadius > Length.Zero
            ? Math.Max(0m, 1m - candidate.Distance.Inches / settings.SnapRadius.Inches)
            : 0m;
        var proximityScore = proximityFraction * MaxProximityScore;

        // 3. Alignment bonus (0–10)
        // Applied when the candidate's snap point lies on the target run's axis
        // within shop tolerance. Computed here without scene graph access;
        // the resolver applies the scene-aware refinement (see §7.5).
        // Set to 0 here; resolver adds it after scoring.
        const decimal alignmentScore = 0m;

        var total = typePriority + proximityScore + alignmentScore;
        return new ScoredCandidate(candidate, total);
    }
}
```

### 7.4 Tie-Breaking Rules

When two candidates have equal adjusted scores (after hysteresis), resolve in this order:

| Priority | Rule | Rationale |
|---|---|---|
| 1 | Higher `SnapType` ordinal | Structural anchors beat lower-priority types |
| 2 | Smaller `Distance` | Closer candidate is more intentional |
| 3 | Smaller `SourceIndex` | Stable sort — deterministic insertion order from sources |

This combination guarantees a total order over all candidates. No two candidates can tie on all three criteria simultaneously.

```csharp
// Tie-breaking comparer — used by resolver before final sort
internal static int TieBreak(ScoredCandidate a, ScoredCandidate b)
{
    // 1. Score descending
    var scoreCmp = b.Score.CompareTo(a.Score);
    if (scoreCmp != 0) return scoreCmp;

    // 2. SnapType descending (higher ordinal wins)
    var typeCmp = ((int)b.Candidate.Type).CompareTo((int)a.Candidate.Type);
    if (typeCmp != 0) return typeCmp;

    // 3. Distance ascending (closer wins)
    var distCmp = a.Candidate.Distance.Inches.CompareTo(b.Candidate.Distance.Inches);
    if (distCmp != 0) return distCmp;

    // 4. SourceIndex ascending (stable order)
    return a.Candidate.SourceIndex.CompareTo(b.Candidate.SourceIndex);
}
```

---

## 8. Hysteresis and Sticky Snapping

### 8.1 Problem

When the cursor hovers between two snap candidates of similar score, tiny sub-pixel mouse movement causes the winner to alternate between them on consecutive mouse-move events. The result is visible snap jitter — the ghost cabinet flickers between two positions at 60 Hz.

### 8.2 Solution: Previous Winner Bonus

The resolver applies an additive `HysteresisBonus` to the score of the previous winner before sorting. A new candidate must score at least `PreviousScore + HysteresisBonus` to displace the current winner.

This means:
- Once snapped to a wall endpoint, the cursor must move meaningfully away before the snap releases
- The threshold is configurable per `SnapSettings.HysteresisBonus` (default: 5 score points)
- The bonus is removed when the previous winner falls outside `SnapRadius` (ensuring the snap does eventually release)
- The bonus is reset to zero when the drag begins or ends

### 8.3 Algorithm

```
previousWinner = EditorSession.PreviousSnapWinner
allCandidates  = gather from all sources
allScored      = score each candidate (raw scores, no hysteresis yet)

if previousWinner is not null:
    find previousWinnerScored in allScored where SnapPoint == previousWinner.SnapPoint
    if found:
        apply HysteresisBonus: previousWinnerScored.Score += settings.HysteresisBonus
    else:
        // previous winner is no longer in range → no bonus applied
        previousWinner = null

sort allScored using TieBreak comparer (score desc, type desc, distance asc, index asc)
winner = allScored[0].Candidate (if any)

EditorSession.RecordSnapWinner(winner)
return winner
```

The comparison for "same candidate" uses `SnapPoint` approximate equality via `GeometryTolerance.ApproximatelyEqual(a, b, tolerance: 1/64")`. Not reference equality — candidates are recreated on every mouse-move call.

### 8.4 ISnapResolver

```csharp
namespace CabinetDesigner.Editor.Snap;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Aggregates all snap candidates, scores them, applies hysteresis and alignment bonuses,
/// and returns the single winning candidate for a given drag state.
/// Called once per mouse-move event during active drag.
/// Returns null if no candidate is within SnapRadius.
/// </summary>
public interface ISnapResolver
{
    /// <summary>
    /// Evaluate candidates and return the winner.
    /// previousWinner: the winner from the last mouse-move (for hysteresis). May be null.
    /// This method is stateless — caller (EditorSession) owns previousWinner state.
    /// </summary>
    SnapCandidate? Resolve(
        Point2D candidateRefPoint,
        DragContext drag,
        SnapSettings settings,
        IEditorSceneGraph scene,
        SnapCandidate? previousWinner);
}
```

### 8.5 DefaultSnapResolver

```csharp
namespace CabinetDesigner.Editor.Snap;

using CabinetDesigner.Domain.Geometry;

public sealed class DefaultSnapResolver : ISnapResolver
{
    private readonly IReadOnlyList<ISnapCandidateSource> _sources;
    private readonly ISnapScorer _scorer;
    private const decimal MaxAlignmentBonus = 10m;

    public DefaultSnapResolver(
        IReadOnlyList<ISnapCandidateSource> sources,
        ISnapScorer scorer)
    {
        _sources = sources;
        _scorer  = scorer;
    }

    public SnapCandidate? Resolve(
        Point2D refPoint,
        DragContext drag,
        SnapSettings settings,
        IEditorSceneGraph scene,
        SnapCandidate? previousWinner)
    {
        // 1. Gather all candidates from all enabled sources
        var allCandidates = _sources
            .Where(s => IsEnabled(s.SnapType, settings))
            .SelectMany(s => s.GetCandidates(refPoint, drag, settings, scene))
            .ToList();

        if (allCandidates.Count == 0) return null;

        // 2. Score each candidate
        var activeRun = drag.TargetRunId.HasValue
            ? scene.Runs.FirstOrDefault(r => r.RunId == drag.TargetRunId)
            : null;

        var scored = allCandidates
            .Select(c =>
            {
                var s = _scorer.Score(c, drag, settings);
                var alignBonus = ComputeAlignmentBonus(c, activeRun);
                return s with { Score = s.Score + alignBonus };
            })
            .ToList();

        // 3. Apply hysteresis bonus to previous winner
        if (previousWinner is not null)
        {
            for (var i = 0; i < scored.Count; i++)
            {
                if (GeometryTolerance.ApproximatelyEqual(
                        scored[i].Candidate.SnapPoint,
                        previousWinner.SnapPoint,
                        GeometryTolerance.DefaultShopTolerance))
                {
                    scored[i] = scored[i] with { Score = scored[i].Score + settings.HysteresisBonus };
                    break;
                }
            }
        }

        // 4. Sort with full tie-breaking, return top candidate
        scored.Sort(TieBreak);
        return scored[0].Candidate;
    }

    private static decimal ComputeAlignmentBonus(SnapCandidate candidate, RunView? activeRun)
    {
        if (activeRun is null) return 0m;
        var closest = activeRun.Extent.ClosestPointTo(candidate.SnapPoint);
        var deviation = closest.DistanceTo(candidate.SnapPoint);
        return deviation <= GeometryTolerance.DefaultShopTolerance ? MaxAlignmentBonus : 0m;
    }

    private static bool IsEnabled(SnapType type, SnapSettings s) => type switch
    {
        SnapType.WallEndpoint   => s.SnapToWallEndpoints,
        SnapType.RunEnd         => s.SnapToRunEnds,
        SnapType.CabinetFace    => s.SnapToCabinetFaces,
        SnapType.StandardWidth  => s.SnapToStandardWidths,
        SnapType.Grid           => s.SnapToGrid,
        _                       => false,
    };

    private static int TieBreak(ScoredCandidate a, ScoredCandidate b)
    {
        var scoreCmp = b.Score.CompareTo(a.Score);
        if (scoreCmp != 0) return scoreCmp;
        var typeCmp = ((int)b.Candidate.Type).CompareTo((int)a.Candidate.Type);
        if (typeCmp != 0) return typeCmp;
        var distCmp = a.Candidate.Distance.Inches.CompareTo(b.Candidate.Distance.Inches);
        if (distCmp != 0) return distCmp;
        return a.Candidate.SourceIndex.CompareTo(b.Candidate.SourceIndex);
    }
}
```

---

## 9. Preview Path

### 9.1 Explicit Flow

```
MouseMove
  │
  ▼ (Presentation)
IEditorInteractionService.OnDragMoved(double screenX, double screenY)
  │
  ├─ 1. Convert: ViewportTransform.ToWorld(screenX, screenY) → cursorWorld
  │
  ├─ 2. Hit test: IEditorSceneGraph.HitTestRun(cursorWorld, hitRadius) → targetRunId?
  │
  ├─ 3. Update: EditorSession.UpdateDragCursor(cursorWorld, targetRunId?)
  │       → ActiveDrag.CursorWorld and .TargetRunId updated
  │
  ├─ 4. Get refPoint: ActiveDrag.CandidateRefPoint (= cursorWorld − GrabOffset)
  │
  ├─ 5. Resolve: ISnapResolver.Resolve(refPoint, drag, settings, scene, previousWinner)
  │       → SnapCandidate? winner
  │
  ├─ 6. Record: EditorSession.RecordSnapWinner(winner)
  │
  ├─ 7. Build preview command: BuildPreviewCommand(winner, drag) → IDesignCommand
  │       → Structural validation only (no state access)
  │
  ├─ 8. Preview: IPreviewCommandHandler.Preview(command)
  │       → Pipeline stages 1–3 only
  │       → No state committed, no undo entry, no explanation nodes
  │       → Returns PreviewResultDto { IsValid, RejectionReason, Warnings }
  │
  ├─ 9. Map candidates: MapToDto(allScoredCandidates, winner) → PlacementCandidateDto[]
  │
  └─ 10. Return: PreviewResultDto enriched with PlacementCandidateDto[]
              → ViewModel renders ghost cabinet, snap indicators, alignment guides
```

### 9.2 PlacementCandidateDto

```csharp
namespace CabinetDesigner.Application.DTOs;

/// <summary>
/// Carries snap candidate data across the Application/Presentation boundary.
/// All values are primitives — no geometry types cross the boundary.
/// AlignmentAxis is included so the rendering layer can draw orientation guides
/// without needing to know anything about the underlying wall geometry.
/// </summary>
public sealed record PlacementCandidateDto(
    decimal SnapPointXInches,
    decimal SnapPointYInches,
    decimal AlignmentAxisDx,     // X component of unit direction vector
    decimal AlignmentAxisDy,     // Y component of unit direction vector
    string SnapType,             // "WallEndpoint" | "RunEnd" | "CabinetFace" | "StandardWidth" | "Grid"
    decimal Score,               // For debug overlay
    string Label,
    string? AnchorEntityId,
    bool IsWinner);
```

**Mapping:**

```csharp
private static IReadOnlyList<PlacementCandidateDto> MapToDto(
    IReadOnlyList<ScoredCandidate> scored,
    SnapCandidate? winner)
{
    return scored
        .OrderByDescending(s => s.Score)
        .Select(s => new PlacementCandidateDto(
            SnapPointXInches:  s.Candidate.SnapPoint.X,
            SnapPointYInches:  s.Candidate.SnapPoint.Y,
            AlignmentAxisDx:   s.Candidate.AlignmentAxis.Dx,
            AlignmentAxisDy:   s.Candidate.AlignmentAxis.Dy,
            SnapType:          s.Candidate.Type.ToString(),
            Score:             s.Score,
            Label:             s.Candidate.Label,
            AnchorEntityId:    s.Candidate.AnchorEntityId,
            IsWinner:          winner is not null &&
                               GeometryTolerance.ApproximatelyEqual(
                                   s.Candidate.SnapPoint,
                                   winner.SnapPoint,
                                   GeometryTolerance.DefaultShopTolerance)))
        .ToList();
}
```

---

## 10. Commit Path

### 10.1 Explicit Flow

```
MouseUp / Drop / Enter key
  │
  ▼
IEditorInteractionService.OnDragCommitted()
  │
  ├─ 1. Resolve final winner: ISnapResolver.Resolve(...) with current drag state
  │
  ├─ 2. Translate: BuildCommand(drag, winner) → IDesignCommand
  │       → See §11 for full translation rules per drag type
  │
  ├─ 3. Validate: command.ValidateStructure() → reject if Error/ManufactureBlocker
  │
  ├─ 4. Execute: IDesignCommandHandler.Execute(command)
  │       → All 11 pipeline stages
  │       → State committed, undo entry pushed, explanation nodes recorded
  │       → DesignChangedEvent published
  │
  ├─ 5. End drag: EditorSession.EndDrag()
  │       → Mode = Idle, PreviousSnapWinner = null
  │
  └─ 6. Return: CommandResultDto → ViewModel shows result (success / validation issues)
```

### 10.2 Abort Flow

```
Escape / Cancel / right-click
  │
  ▼
IEditorInteractionService.OnDragAborted()
  │
  ├─ EditorSession.AbortDrag()
  │       → Mode = Idle, ActiveDrag = null, PreviousSnapWinner = null
  │
  └─ No command submitted. No state changed.
```

---

## 11. Command Translation

The editor layer owns the moment of command construction. This is the only place where:
- `IClock.Now` is read
- `CommandOrigin` is determined
- `IntentDescription` is composed
- The winning snap point is projected onto the run axis to extract the authoritative `Length`

### 11.1 Place Cabinet

**Input:** `DragContext` (type = PlaceCabinet) + `SnapCandidate? winner`
**Output:** `AddCabinetToRunCommand`

```csharp
private IDesignCommand BuildPlaceCabinetCommand(DragContext drag, SnapCandidate? winner)
{
    // Requires a target run — can't place a cabinet in empty space
    if (drag.TargetRunId is null)
        throw new InvalidOperationException("Cannot place cabinet: no target run.");

    var run = _scene.Runs.First(r => r.RunId == drag.TargetRunId);

    // Determine the placement index from the snap point position along the run axis
    // If no winner, use raw cursor projection (fallback to end-of-run)
    Point2D refPoint = winner is not null ? winner.SnapPoint : drag.CandidateRefPoint;
    var (t, _) = RunAxisProjection.ProjectOntoAxis(refPoint, run.Extent.Start, run.Axis);

    // Find insertion index: first slot whose start offset > t → insert before it
    var insertIndex = run.Slots
        .Where(s => RunAxisProjection
            .ProjectOntoAxis(s.LeftFaceWorld, run.Extent.Start, run.Axis).T > t)
        .Select(s => s.Index)
        .DefaultIfEmpty(run.Slots.Count)  // end-of-run
        .Min();

    var placement = insertIndex == run.Slots.Count
        ? RunPlacement.EndOfRun
        : RunPlacement.AtIndex;

    return new AddCabinetToRunCommand(
        runId:             drag.TargetRunId.Value,
        cabinetTypeId:     drag.CabinetTypeId!,
        nominalWidth:      drag.NominalWidth,
        placement:         placement,
        origin:            CommandOrigin.User,
        intentDescription: $"Place {drag.NominalWidth} {drag.CabinetTypeId}" +
                           (winner is not null ? $" — snapped to {winner.Label}" : " — freeform"),
        timestamp:         _clock.Now,
        insertAtIndex:     placement == RunPlacement.AtIndex ? insertIndex : null);
}
```

**Rules:**
- Always requires a `TargetRunId`. If null, the interaction service must block the commit.
- Placement index is derived from axis projection, not from screen position arithmetic.
- `CommandOrigin.User` always — the user initiated the drag.
- The snap label feeds `IntentDescription` for the Why Engine.

### 11.2 Move Cabinet

**Input:** `DragContext` (type = MoveCabinet) + `SnapCandidate? winner`
**Output:** `MoveCabinetCommand`

```csharp
private IDesignCommand BuildMoveCabinetCommand(DragContext drag, SnapCandidate? winner)
{
    if (drag.TargetRunId is null)
        throw new InvalidOperationException("Cannot move cabinet: no target run.");

    var targetRun = _scene.Runs.First(r => r.RunId == drag.TargetRunId);

    // Apply grab offset: the reference point is where the cabinet's left edge should land
    // winner.SnapPoint is already the intended reference point location (not raw cursor)
    Point2D refPoint = winner is not null ? winner.SnapPoint : drag.CandidateRefPoint;
    var (t, _) = RunAxisProjection.ProjectOntoAxis(
        refPoint, targetRun.Extent.Start, targetRun.Axis);

    var insertIndex = targetRun.Slots
        .Where(s => RunAxisProjection
            .ProjectOntoAxis(s.LeftFaceWorld, targetRun.Extent.Start, targetRun.Axis).T > t)
        .Select(s => s.Index)
        .DefaultIfEmpty(targetRun.Slots.Count)
        .Min();

    var placement = insertIndex == targetRun.Slots.Count
        ? RunPlacement.EndOfRun
        : RunPlacement.AtIndex;

    return new MoveCabinetCommand(
        cabinetId:         drag.SubjectCabinetId!.Value,
        sourceRunId:       drag.SourceRunId!.Value,
        targetRunId:       drag.TargetRunId.Value,
        targetPlacement:   placement,
        origin:            CommandOrigin.User,
        intentDescription: $"Move cabinet to {targetRun.RunId}" +
                           (winner is not null ? $" — snapped to {winner.Label}" : " — freeform"),
        timestamp:         _clock.Now,
        targetIndex:       placement == RunPlacement.AtIndex ? insertIndex : null);
}
```

**Rules:**
- `GrabOffset` is already baked into `CandidateRefPoint`. The snap winner gives the reference point directly — no additional offset math at construction time.
- Cross-run move is valid: `SourceRunId ≠ TargetRunId` is legal.
- `SubjectCabinetId` and `SourceRunId` must be non-null for move (asserted at drag-begin time).

### 11.3 Resize Cabinet

**Input:** `DragContext` (type = ResizeCabinet) + `SnapCandidate? winner`
**Output:** `ResizeCabinetCommand`

```csharp
private IDesignCommand BuildResizeCabinetCommand(DragContext drag, SnapCandidate? winner)
{
    if (drag.TargetRunId is null || drag.FixedLeftEdgeWorld is null)
        throw new InvalidOperationException("Cannot resize cabinet: missing run or left edge.");

    var run = _scene.Runs.First(r => r.RunId == drag.TargetRunId);

    // New width = distance from fixed left edge to snap point, projected onto run axis
    Point2D rightEdge = winner is not null ? winner.SnapPoint : drag.CandidateRefPoint;
    var (rightT, _) = RunAxisProjection.ProjectOntoAxis(
        rightEdge, drag.FixedLeftEdgeWorld.Value, run.Axis);

    // rightT is the new nominal width in inches (signed along run axis; clamped to positive)
    var newWidth = Length.FromInches(Math.Max(0m, rightT));

    // Retrieve previous width from the slot view
    var slot = run.Slots.First(s => s.CabinetId == drag.SubjectCabinetId);

    return new ResizeCabinetCommand(
        cabinetId:          drag.SubjectCabinetId!.Value,
        previousNominalWidth: slot.NominalWidth,
        newNominalWidth:    newWidth,
        origin:             CommandOrigin.User,
        intentDescription:  $"Resize cabinet to {newWidth}" +
                            (winner is not null ? $" — snapped to {winner.Label}" : " — freeform"),
        timestamp:          _clock.Now);
}
```

**Rules:**
- `FixedLeftEdgeWorld` is captured at drag-begin time from the slot's `LeftFaceWorld`. It does not change during the drag.
- New width is purely the axis-projected distance from fixed left edge to snap point.
- A width of zero or negative fails `ValidateStructure()` — caught before submission.
- `StandardWidthSnapSource` for resize uses `FixedLeftEdgeWorld` as the axis origin (see §6.6).

### 11.4 Draw Run

**Input:** `DrawRunDragContext` + final start snap + final end snap
**Output:** `CreateRunCommand`

```csharp
private IDesignCommand BuildCreateRunCommand(
    DrawRunDragContext ctx,
    SnapCandidate? endWinner)
{
    if (ctx.StartWorld is null || ctx.WallId is null)
        throw new InvalidOperationException("Draw run context is missing start or wall.");

    var endPoint = endWinner is not null ? endWinner.SnapPoint : ctx.CurrentEndWorld;

    return new CreateRunCommand(
        startPoint:        ctx.StartWorld.Value,
        endPoint:          endPoint,
        wallId:            ctx.WallId,
        origin:            CommandOrigin.User,
        intentDescription: $"Draw run from ({ctx.StartWorld.Value.X:F2}\", {ctx.StartWorld.Value.Y:F2}\")" +
                           $" to ({endPoint.X:F2}\", {endPoint.Y:F2}\")" +
                           (endWinner is not null ? $" — snapped to {endWinner.Label}" : ""),
        timestamp:         _clock.Now);
}
```

**Phase 1 (SettingStart):** Snap the start point to a wall endpoint or point on a wall segment. On click, `DrawRunDragContext.StartWorld` is fixed and phase transitions to `SettingEnd`. The start-phase snap uses only `WallEndpointSnapSource` and a wall-segment projection source (not detailed here — uses `LineSegment2D.ClosestPointTo`).

**Phase 2 (SettingEnd):** On each mouse-move, snap the end point to wall endpoints, run ends, or wall-segment positions. On commit, `BuildCreateRunCommand` is called. On Escape during either phase, abort the draw.

---

## 12. Editor Commands

### 12.1 Command Definitions

These commands change interaction state only. They bypass `IResolutionOrchestrator` entirely. No delta tracking. No undo entries. No explanation nodes.

```csharp
namespace CabinetDesigner.Editor.Commands;

using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor.Snap;

// Selection

public sealed record SelectCabinetsCommand(
    CommandMetadata Metadata,
    IReadOnlyList<CabinetId> CabinetIds,
    bool AddToExistingSelection) : IEditorCommand
{
    public string CommandType => "editor.select_cabinets";
}

public sealed record ClearSelectionCommand(
    CommandMetadata Metadata) : IEditorCommand
{
    public string CommandType => "editor.clear_selection";
}

public sealed record SetHoverCommand(
    CommandMetadata Metadata,
    CabinetId? CabinetId) : IEditorCommand
{
    public string CommandType => "editor.set_hover";
}

public sealed record SetActiveRunCommand(
    CommandMetadata Metadata,
    RunId? RunId) : IEditorCommand
{
    public string CommandType => "editor.set_active_run";
}

// Viewport

public sealed record SetViewportCommand(
    CommandMetadata Metadata,
    ViewportTransform Transform) : IEditorCommand
{
    public string CommandType => "editor.set_viewport";
}

// Snap settings

public sealed record UpdateSnapSettingsCommand(
    CommandMetadata Metadata,
    SnapSettings Settings) : IEditorCommand
{
    public string CommandType => "editor.update_snap_settings";
}
```

### 12.2 IEditorCommandHandler

```csharp
namespace CabinetDesigner.Editor;

using CabinetDesigner.Domain.Commands;

/// <summary>
/// Applies IEditorCommand instances to EditorSession.
/// Never calls IResolutionOrchestrator.
/// Never creates undo entries.
/// Never records explanation nodes.
/// Type system enforces the boundary: IEditorCommand ≠ IDesignCommand.
/// </summary>
public interface IEditorCommandHandler
{
    void Execute(IEditorCommand command);
}

public sealed class EditorCommandHandler : IEditorCommandHandler
{
    private readonly EditorSession _session;

    public EditorCommandHandler(EditorSession session) => _session = session;

    public void Execute(IEditorCommand command)
    {
        switch (command)
        {
            case SelectCabinetsCommand sel:
                var newIds = sel.AddToExistingSelection
                    ? _session.SelectedCabinetIds.Concat(sel.CabinetIds).Distinct().ToList()
                    : sel.CabinetIds.ToList();
                _session.SetSelection(newIds);
                break;

            case ClearSelectionCommand:
                _session.SetSelection([]);
                break;

            case SetHoverCommand hover:
                _session.SetHover(hover.CabinetId);
                break;

            case SetActiveRunCommand run:
                _session.SetActiveRun(run.RunId);
                break;

            case SetViewportCommand vp:
                _session.UpdateViewport(vp.Transform);
                break;

            case UpdateSnapSettingsCommand snap:
                _session.ApplySnapSettings(snap.Settings);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unhandled editor command: {command.CommandType}");
        }
    }
}
```

### 12.3 IEditorInteractionService

The single public surface consumed by ViewModels during active drag operations.

```csharp
namespace CabinetDesigner.Editor;

using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public interface IEditorInteractionService
{
    // ── Cabinet placement ─────────────────────────────────────────────────

    /// <summary>Begin a catalog-drag for a new cabinet.</summary>
    void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth,
                           double screenX, double screenY);

    /// <summary>Begin dragging an existing cabinet to a new position.</summary>
    void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY);

    /// <summary>Begin dragging the resize handle of a cabinet.</summary>
    void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY);

    // ── Run drawing ───────────────────────────────────────────────────────

    /// <summary>Begin the two-phase run draw. First click sets the start point.</summary>
    PreviewResultDto BeginDrawRun(double screenX, double screenY);

    // ── Active drag ───────────────────────────────────────────────────────

    /// <summary>
    /// Called on every mouse-move during an active drag.
    /// Returns a preview result for canvas rendering. No state committed.
    /// Safe to call at 60 Hz — no side effects on authoritative design state.
    /// </summary>
    PreviewResultDto OnDragMoved(double screenX, double screenY);

    /// <summary>
    /// Called on mouse-up, drop, or Enter key. Commits the current drag.
    /// Returns the full command result (success, validation issues, affected entities).
    /// </summary>
    CommandResultDto OnDragCommitted();

    /// <summary>
    /// Called on Escape, right-click, or drag cancel. Discards the current drag.
    /// No design state changes. No command submitted.
    /// </summary>
    void OnDragAborted();
}
```

---

## 13. Dependency Injection Registration

```csharp
// CabinetDesigner.App — DI wiring for the Editor module

// Scene graph (singleton — shared read model, updated on DesignChangedEvent)
services.AddSingleton<IEditorSceneGraph, EditorSceneGraph>();

// Editor session (singleton per project — reset on project open/close)
services.AddSingleton<EditorSession>();

// Snap sources — order here does not affect scoring; all are always queried
services.AddSingleton<ISnapCandidateSource, WallEndpointSnapSource>();
services.AddSingleton<ISnapCandidateSource, RunEndSnapSource>();
services.AddSingleton<ISnapCandidateSource, CabinetFaceSnapSource>();
services.AddSingleton<ISnapCandidateSource, StandardWidthSnapSource>();
services.AddSingleton<ISnapCandidateSource, GridSnapSource>();

// Snap pipeline
services.AddSingleton<ISnapScorer, DefaultSnapScorer>();
services.AddSingleton<ISnapResolver, DefaultSnapResolver>();

// Editor command handler
services.AddSingleton<IEditorCommandHandler, EditorCommandHandler>();

// Interaction service (scoped: one per editor view session)
services.AddScoped<IEditorInteractionService, EditorInteractionService>();
```

---

## 14. Invariants

| Invariant | Enforcement |
|---|---|
| `IEditorCommand` never flows through `IResolutionOrchestrator` | Compile-time type separation; `IEditorCommandHandler` has no orchestrator dependency |
| No design state mutation from snap evaluation | All snap sources and scorer are read-only; `IEditorSceneGraph` exposes no write path |
| All snap math uses geometry value objects | `Point2D`, `Length`, `Vector2D`, `Offset` — no raw `double` coordinates in snap pipeline |
| Scoring is deterministic | Fixed weights, deterministic tie-breaking, no randomness, no time-based values |
| Hysteresis bonus is applied to previous winner only | Resolver receives `previousWinner` as a parameter — EditorSession owns the state |
| Grab offset is resolved at drag-begin, not at commit | `DragContext.GrabOffset` is set once when drag starts and carried through all mouse-move events |
| All run-relative math uses axis projection | No `X` or `Y` coordinate extraction from run endpoints — always `RunAxisProjection.ProjectOntoAxis` |
| `AlignmentAxis` is always a unit vector | All `RunView.Axis` values come from `Segment.Direction()` which calls `Normalized()` |
| Preview path has zero side effects | `IPreviewCommandHandler.Preview()` runs stages 1–3 only; no undo, no explanation, no event bus |
| Mode transitions are explicitly guarded | `EditorSession.AssertMode` throws on illegal transitions — no silent state corruption |
| `SourceIndex` is set sequentially per source per call | Tie-breaking is stable and deterministic; index resets to 0 per source per `GetCandidates` call |

---

## 15. Testing Strategy

### 15.1 Unit Tests — Geometry (RunAxisProjection)

| Test | Assertion |
|---|---|
| Horizontal run, point on axis | `T` equals X offset from origin; `ProjectedPoint.Y == origin.Y` |
| Vertical run, point on axis | `T` equals Y offset; `ProjectedPoint.X == origin.X` |
| 45° diagonal run | `T = √2 / 2 * distance` from origin |
| Point off-axis | `ProjectedPoint` lies on segment within tolerance |
| `PointAtDistance` round-trip | `ProjectOntoAxis(PointAtDistance(origin, axis, t), origin, axis).T == t` |

### 15.2 Unit Tests — Snap Sources

| Source | Test cases |
|---|---|
| `WallEndpointSnapSource` | Returns candidate when within radius; returns empty when disabled or out of range |
| `RunEndSnapSource` | Returns start and end of run; skips when disabled |
| `CabinetFaceSnapSource` | Skips subject cabinet during move; returns faces from other cabinets |
| `StandardWidthSnapSource` | Returns 12" snap point 12" along axis from run origin regardless of wall angle |
| `StandardWidthSnapSource` (resize) | Uses `FixedLeftEdgeWorld` as axis origin, not run start |
| `GridSnapSource` | Rounds to nearest interval; returns empty when disabled |

### 15.3 Unit Tests — Scorer

| Test | Assertion |
|---|---|
| `WallEndpoint` at distance X scores higher than `Grid` at distance 0 | Type priority dominates at equal proximity |
| Two `WallEndpoint` candidates: closer scores higher | Proximity score increases as distance decreases |
| Identical input → identical score on repeated calls | Determinism |
| Score is non-negative for all valid inputs | No negative score output |

### 15.4 Unit Tests — Resolver and Tie-breaking

| Test | Assertion |
|---|---|
| No candidates → returns null | Empty scene produces null winner |
| `WallEndpoint` beats `CabinetFace` at equal distance | Type priority wins |
| Equal type, equal score → closer distance wins | Tie-break rule 2 |
| Equal type, equal distance, equal score → lower `SourceIndex` wins | Tie-break rule 3 |
| Previous winner receives hysteresis bonus | Winner retained when challenger scores within HysteresisBonus |
| Previous winner outside SnapRadius → no bonus applied | Snap releases when cursor moves far enough away |
| Hysteresis resets on new drag | `PreviousSnapWinner = null` at drag begin |

### 15.5 Unit Tests — Hysteresis

| Test | Scenario |
|---|---|
| Jitter suppression | Cursor hovers between A (score 45) and B (score 43). A wins first. On next call, B scores 44 (gained proximity). With hysteresis bonus of 5, A's effective score = 50 → A retains win. |
| Release on movement | Cursor moves: A now scores 38, B scores 44. B's score 44 > A's 38 + 5 = 43. B wins. |
| Reset on abort | After `AbortDrag`, `PreviousSnapWinner = null`. Next drag starts with no bias. |

### 15.6 Integration Tests — Preview Path

| Test | Assertion |
|---|---|
| `OnDragMoved` → `PreviewResultDto.Candidates` ordered by score descending | Mapping is sorted |
| `IsWinner = true` for exactly one candidate | No multi-winner output |
| Preview with no target run → `IsValid = false` | Cannot preview placement with no run |
| Preview calls `IPreviewCommandHandler.Preview` exactly once per `OnDragMoved` | No double-execution |

### 15.7 Integration Tests — Commit Path

| Test | Assertion |
|---|---|
| `OnDragCommitted` with `SnapType.WallEndpoint` winner → command uses snapped point | Not raw cursor |
| `OnDragCommitted` with no winner → command uses `CandidateRefPoint` | Graceful fallback |
| After commit: `EditorSession.Mode == Idle` | Mode reset correctly |
| After abort: no command submitted | `IDesignCommandHandler.Execute` not called |
| After commit failure: mode still resets to Idle | Session cleanup is unconditional |

### 15.8 Integration Tests — Command Translation

| Drag type | Test |
|---|---|
| PlaceCabinet | Axis-projected insert index matches expected slot position for angled wall |
| MoveCabinet | Grab offset correctly shifts reference point; insert index derived from adjusted ref |
| ResizeCabinet | New width = axis-projected distance from `FixedLeftEdgeWorld` to snap point |
| DrawRun | Both start and end points use snapped coordinates; `WallId` is preserved from phase 1 |

---

## 16. Risks and Edge Cases

| Risk | Mitigation |
|---|---|
| Snap jitter at candidate boundary | Hysteresis bonus (§8). Configurable via `SnapSettings.HysteresisBonus`. |
| Angled wall: width appears correct in axis space but looks wrong on screen | Rendering must use `AlignmentAxis` from `PlacementCandidateDto` to orient ghost cabinet. Snap math is correct by design. |
| Cursor between two runs during drag | `HitTestRun` uses a configurable `hitRadius`. Nearest run wins. If ambiguous, tie-break by closest run boundary distance. |
| `PreviousSnapWinner` comparison uses approximate equality (1/64") | Scene graph views are exact (decimal inches). Snap points coming from the same wall endpoint will compare equal within tolerance. |
| Source produces candidates outside SnapRadius | Sources must filter to `<= settings.SnapRadius`. Resolver does not re-filter. If a source violates this, it will produce a candidate that scores high proximity — add a guard assertion in resolver during development. |
| `StandardWidths` list is empty | `StandardWidthSnapSource.GetCandidates` returns empty. No crash. User must configure widths via `UpdateSnapSettingsCommand`. |
| `DragContext.CabinetTypeId` is null for `PlaceCabinet` | Validation in `BuildPlaceCabinetCommand` — throws before command construction. Interaction service must set `CabinetTypeId` at `BeginPlaceCabinet`. |
| Move to a run at a different wall angle | `MoveCabinetCommand` carries no orientation. The domain resolves the cabinet into the target run's geometry at stage 3 (Spatial Resolution). The editor does not need to handle cross-angle orientation. |
| Resize below minimum width | `ResizeCabinetCommand.ValidateStructure()` rejects zero-or-negative widths. The snap radius prevents snapping to a point behind the fixed left edge, but the `Math.Max(0m, rightT)` guard handles any edge case. |
| Draw run with zero length | `CreateRunCommand.ValidateStructure()` rejects it (StartPoint == EndPoint → zero length). UI should suppress commit if start ≈ end. |
| SceneGraph stale during fast drag | Expected and acceptable — snap candidates are opportunistic. Domain pipeline at commit time performs authoritative spatial validation (stage 3). Stale candidates produce a visual preview error, not a committed design error. |
| Multiple monitors, high-DPI | `ViewportTransform.ToWorld` works on logical pixels, not physical pixels. WPF provides logical pixels natively. No additional scaling needed. |
