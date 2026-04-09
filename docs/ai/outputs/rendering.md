# P12 — Rendering Subsystem Design

Source: `cabinet_ai_prompt_pack_v4_1_full.md`
Context: `architecture_summary.md`, `code_phase_global_instructions.md`, `geometry_system.md`, `editor_engine.md`, `application_layer.md`, `presentation.md`

---

## 1. Goals

- Provide a **pure projection-based canvas rendering subsystem** for the 2D editor
- Translate scene state DTOs into pixel output — no domain logic, no state mutation
- Support a layered render model that correctly composes walls, runs, cabinets, guides, selection, snap overlays, and drag previews
- Expose a clean hit-testing interface that identifies entities and handles without touching committed state
- Integrate with the viewport transform defined by the editor layer
- Sustain 60 fps during drag-time interaction with a dirty-flag invalidation strategy
- Keep the coordinate and projection model open for a future 3D or richer rendering pass without polluting the current 2D architecture

---

## 2. Boundaries

### 2.1 What the Rendering Layer Owns

| Responsibility | Location |
|---|---|
| Translating `RenderSceneDto` into WPF drawing calls | `CabinetDesigner.Rendering` |
| Layer compositing (walls, cabinets, guides, overlays) | `CabinetDesigner.Rendering` |
| Hit testing against projected geometry (identify only) | `CabinetDesigner.Rendering` |
| Overlay and adorner drawing (selection handles, resize grips, snap lines) | `CabinetDesigner.Rendering` |
| Preview ghost rendering (drag shadows, validity coloring) | `CabinetDesigner.Rendering` |
| Coordinate transform helpers (world → screen, screen → world projection) | `CabinetDesigner.Rendering` (consumes `ViewportTransform` from Editor) |
| Redraw scheduling and dirty-flag management | `CabinetDesigner.Rendering` |
| Canvas visual element lifecycle | `CabinetDesigner.Rendering` |

### 2.2 What the Rendering Layer Does NOT Own

| Excluded Responsibility | Owned By |
|---|---|
| Domain entities, value objects, aggregates | `CabinetDesigner.Domain` |
| Snap scoring or snap candidate resolution | `CabinetDesigner.Editor` |
| Selection state management | `CabinetDesigner.Editor` |
| Command execution or state mutation | `CabinetDesigner.Application` |
| ViewportTransform ownership and pan/zoom logic | `CabinetDesigner.Editor` |
| DTO construction or scene projection building | `CabinetDesigner.Application` / `CabinetDesigner.Editor` |
| Persistence, manufacturing, or install logic | `CabinetDesigner.Domain` (downstream) |
| Hit test result routing or acting on hits | `CabinetDesigner.Presentation` (via ViewModel) |

### 2.3 Dependency Direction

```
CabinetDesigner.Presentation
    └──▶ CabinetDesigner.Rendering
              ├──▶ CabinetDesigner.Editor  (ViewportTransform, read-only)
              └──▶ CabinetDesigner.Domain.Geometry  (value objects only — no entities)
```

Rendering **never** depends on Application, Persistence, or Domain entities. All data arrives as pre-shaped DTOs. Rendering does not produce design state — it only reads it.

---

## 3. Render Scene Model

The render scene is a **frozen projection snapshot** produced by the editor/application layer and handed to rendering. Rendering consumes it as a read-only input. It contains only what is needed to draw — no business logic, no entity references, no mutable state.

### 3.1 RenderSceneDto

```csharp
namespace CabinetDesigner.Rendering;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Immutable snapshot of the scene as it should be drawn at a given point in time.
/// Produced by the Application or Editor layer. Rendering consumes this only — never mutates it.
/// Updated by the ViewModel on every preview cycle or committed design change.
/// </summary>
public sealed record RenderSceneDto(
    IReadOnlyList<WallRenderDto> Walls,
    IReadOnlyList<RunRenderDto> Runs,
    IReadOnlyList<CabinetRenderDto> Cabinets,
    IReadOnlyList<GuideLineDto> Guides,
    SelectionOverlayDto? Selection,
    SnapOverlayDto? SnapOverlay,
    PreviewGhostDto? PreviewGhost,
    GridSettingsDto Grid
);
```

### 3.2 WallRenderDto

```csharp
/// <summary>
/// A wall segment as a world-space line for rendering. No domain entity reference.
/// </summary>
public sealed record WallRenderDto(
    string WallId,
    LineSegment2D Segment,
    bool IsHighlighted
);
```

### 3.3 RunRenderDto

```csharp
/// <summary>
/// A cabinet run's spatial footprint — the axis line and bounding region.
/// </summary>
public sealed record RunRenderDto(
    string RunId,
    LineSegment2D AxisSegment,
    Rect2D BoundingRect,
    bool IsActive
);
```

### 3.4 CabinetRenderDto

```csharp
/// <summary>
/// A single placed cabinet as renderable geometry.
/// Includes all annotations needed for drawing label, dimension, and state overlays.
/// </summary>
public sealed record CabinetRenderDto(
    string CabinetId,
    Rect2D WorldBounds,
    string Label,
    string TypeDisplayName,
    CabinetRenderState State,
    IReadOnlyList<HandleRenderDto> Handles
);

public enum CabinetRenderState
{
    Normal,
    Hovered,
    Selected,
    Invalid,       // Drawn with error coloring (validation issue)
    Ghost,         // Preview/drag placeholder; do not draw as authoritative
}
```

### 3.5 HandleRenderDto

```csharp
/// <summary>
/// A draggable handle on a placed cabinet (e.g., right-edge resize grip).
/// Position is in world-space. The renderer projects to screen.
/// </summary>
public sealed record HandleRenderDto(
    string HandleId,
    HandleType Type,
    Point2D WorldPosition
);

public enum HandleType
{
    ResizeRight,    // Right-edge resize grip
    MoveOrigin,     // Center-of-body move anchor (shown when selected)
}
```

### 3.6 GuideLineDto

```csharp
/// <summary>
/// A guide line or dimension annotation to be drawn as an overlay.
/// Not a domain entity — a computed visual hint.
/// </summary>
public sealed record GuideLineDto(
    LineSegment2D Segment,
    GuideType Type,
    string? Label,
    Point2D? LabelAnchor
);

public enum GuideType
{
    AlignmentGuide,      // Horizontal/vertical alignment across cabinets
    DimensionLine,       // Labeled span annotation
    WallReference,       // Wall surface reference guide
    RunBoundary,         // Start/end of a run
    FillerZone,          // Gap between cabinet and wall/run end
}
```

### 3.7 SelectionOverlayDto

```csharp
/// <summary>
/// Visual state for the current selection — handles, bounds highlights.
/// Null when nothing is selected.
/// </summary>
public sealed record SelectionOverlayDto(
    IReadOnlyList<string> SelectedCabinetIds,
    Rect2D? MultiSelectionBounds,     // Null for single selection
    IReadOnlyList<HandleRenderDto> Handles
);
```

### 3.8 SnapOverlayDto

```csharp
/// <summary>
/// Snap feedback drawn during drag — snap lines, snap point markers, guide extensions.
/// Produced by the editor snap engine and passed through as a DTO. Rendering does not score snaps.
/// </summary>
public sealed record SnapOverlayDto(
    Point2D? SnapPoint,              // Current winner's snapped world position (null if no snap)
    IReadOnlyList<LineSegment2D> SnapLines,   // Guide lines shown during snap
    SnapStrength Strength            // Used to choose visual weight (exact vs proximity)
);

public enum SnapStrength
{
    None,
    Proximity,
    Exact,
}
```

### 3.9 PreviewGhostDto

```csharp
/// <summary>
/// A drag-time ghost showing where the cabinet would land.
/// Validity coloring communicates drop legality to the user.
/// </summary>
public sealed record PreviewGhostDto(
    Rect2D WorldBounds,
    string Label,
    PreviewValidity Validity
);

public enum PreviewValidity
{
    Valid,      // Green — placement is legal
    Warning,    // Amber — placement has non-blocking issues (e.g., tight clearance)
    Invalid,    // Red — placement is not allowed
}
```

### 3.10 GridSettingsDto

```csharp
/// <summary>
/// Controls canvas grid rendering. Rendering uses this to draw the background grid.
/// </summary>
public sealed record GridSettingsDto(
    bool Visible,
    Length MajorSpacing,
    Length MinorSpacing
);
```

---

## 4. Layer Architecture

The canvas renders in strict layer order, bottom-to-top. Each layer is independent and can be individually invalidated.

```
Layer 0 — Background / Grid
    └── Grid lines (major + minor)
    └── Room boundary outline

Layer 1 — Walls
    └── Wall segment lines
    └── Wall labels (at rest or hovered)

Layer 2 — Runs
    └── Run axis lines (subtle)
    └── Run bounding zones

Layer 3 — Cabinets (Committed)
    └── Cabinet body rectangles
    └── Cabinet labels
    └── Dimension tick annotations
    └── State coloring (normal / hovered / invalid)

Layer 4 — Selection Overlay
    └── Selection highlight borders
    └── Multi-selection bounding box
    └── Resize and move handles

Layer 5 — Guide / Dimension Lines
    └── Alignment guides
    └── Dimension lines and labels
    └── Wall reference guides
    └── Filler zone markers

Layer 6 — Snap Overlay
    └── Snap point marker
    └── Snap extension lines
    └── Snap strength visual weight

Layer 7 — Preview Ghost
    └── Ghost cabinet body (validity-colored, alpha-blended)
    └── Ghost label
    └── Drop target indicator on run

Layer 8 — HUD / Cursor Annotations
    └── Floating dimension readout at cursor during drag
    └── Mode hint text (e.g., "Click to set start point")
```

### 4.1 Layer Isolation Rules

- Each layer reads only from its corresponding portion of `RenderSceneDto`
- Layers do not share state or call each other
- A layer that has nothing to draw emits nothing — it does not short-circuit other layers
- Layer 6 (Snap) and Layer 7 (Ghost) are only drawn when a drag is in progress
- Layer 4 (Selection) is only drawn when `SelectionOverlayDto` is non-null

### 4.2 Layer Implementation Shape

```csharp
namespace CabinetDesigner.Rendering.Layers;

/// <summary>
/// One visual layer in the render stack.
/// Receives only its slice of the scene DTO and the active viewport.
/// Must not capture mutable state between render passes.
/// </summary>
public interface IRenderLayer
{
    void Draw(DrawingContext dc, RenderSceneDto scene, ViewportTransform viewport);
}
```

Each `IRenderLayer` implementation is stateless between calls. The canvas host calls layers in order on each redraw.

---

## 5. Hit Testing

Hit testing maps a screen-space point to zero or more logical entities in the scene. It is **read-only** — it identifies, never mutates.

### 5.1 HitTestResult

```csharp
namespace CabinetDesigner.Rendering;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// The result of a hit test at a screen-space point.
/// Contains the closest matching entity and handle (if any).
/// Rendering produces this; the Presentation layer acts on it.
/// </summary>
public sealed record HitTestResult(
    HitTestTarget Target,
    string? EntityId,      // CabinetId, RunId, WallId, or HandleId — null if Target is None
    string? HandleId       // Set when Target is Handle; otherwise null
);

public enum HitTestTarget
{
    None,
    Cabinet,
    Handle,
    Run,
    Wall,
}
```

### 5.2 IHitTester

```csharp
namespace CabinetDesigner.Rendering;

/// <summary>
/// Tests a screen-space point against the current render scene.
/// All geometry is projected using the viewport transform before comparison.
/// Must not mutate any state. May be called on every mouse-move.
/// </summary>
public interface IHitTester
{
    /// <summary>
    /// Returns the topmost logical entity at the given screen position.
    /// Priority order: Handle > Cabinet > Run > Wall > None.
    /// </summary>
    HitTestResult HitTest(double screenX, double screenY, RenderSceneDto scene, ViewportTransform viewport);
}
```

### 5.3 Hit Testing Strategy

- Hit test priority matches layer order (top layers win): Handle > Cabinet > Run > Wall
- Handle hit areas are expanded by a fixed screen-space tolerance (e.g., 8px radius) so thin handles remain clickable at all zoom levels
- Cabinet body hit test uses projected `WorldBounds` rectangle containment
- Wall hit test uses projected `LineSegment2D.DistanceTo` with a screen-space tolerance (e.g., 6px)
- All geometry is projected to screen before comparison — no world-space tolerance (zoom-invariant feel)
- Hit testing is performed against `RenderSceneDto` only — no entity model lookups
- The caller (ViewModel) receives a `HitTestResult` and routes it to the appropriate editor interaction call

---

## 6. Overlays and Adorners

Overlays are transient visual elements that communicate interaction state. They are not persisted and carry no domain meaning.

### 6.1 Selection Adorner

- Drawn for every `CabinetId` in `SelectionOverlayDto.SelectedCabinetIds`
- Border: 2px highlight stroke around projected `WorldBounds`
- Handles: square grips at right edge (resize) and body center (move indicator), sized in screen pixels (not world units) so they remain usable at all zoom levels
- Multi-selection: dashed bounding box encompassing all selected cabinets

### 6.2 Snap Adorner

Active only during drag. Driven entirely by `SnapOverlayDto`.

- **Snap point marker:** crosshair or circle at `SnapPoint` in world→screen projection
- **Snap lines:** thin dashed lines extending from `SnapLines` to communicate the alignment axis
- **Strength visual:** exact snaps render marker in accent color; proximity snaps in muted tone

### 6.3 Preview Ghost Adorner

Active only during drag. Driven by `PreviewGhostDto`.

- Renders at `WorldBounds` position with alpha transparency (e.g., 50% opacity)
- Body fill color maps to `PreviewValidity`: Valid → green tint, Warning → amber tint, Invalid → red tint
- Ghost label drawn in body center in italic
- Ghost does not draw handles

### 6.4 Guide / Dimension Adorner

- Dimension lines: perpendicular ticks at endpoints + label at midpoint
- Labels use the dimension format configured in `DisplaySettings` (fractional or decimal inches)
- Guide lines extend the full canvas width/height when representing alignment axes

### 6.5 HUD Adorner

Floating dimension readout that follows the cursor during drag:

```csharp
public sealed record HudAnnotationDto(
    Point2D WorldAnchor,           // Annotation follows cursor world position
    string DimensionText,          // Formatted current dimension (e.g., "23 1/2"")
    string? SecondaryText          // Optional secondary label (e.g., "Snapped to wall")
);
```

Drawn at Layer 8. Never overlaps the snap overlay — the HUD is offset by a fixed screen pixel vector from the cursor.

---

## 7. Viewport Integration

The `ViewportTransform` is owned by `CabinetDesigner.Editor` and passed to rendering as a read-only value. Rendering never modifies it.

### 7.1 Coordinate Transform Boundary

```
World space:   decimal inches, Point2D, origin at project (0,0)
Screen space:  double pixels, WPF DrawingContext coordinate system

Transform:
    screenX = (double)(world.X * scale + offsetX)
    screenY = (double)(world.Y * scale + offsetY)
```

The rendering layer provides a stateless projection helper:

```csharp
namespace CabinetDesigner.Rendering;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Editor;

/// <summary>
/// Stateless world-to-screen projection helpers.
/// All rendering geometry passes through here. No caching — called fresh per draw pass.
/// </summary>
public static class ScreenProjection
{
    public static System.Windows.Point ToScreen(Point2D world, ViewportTransform vp)
    {
        var (x, y) = vp.ToScreen(world);
        return new System.Windows.Point(x, y);
    }

    public static System.Windows.Rect ToScreen(Rect2D rect, ViewportTransform vp)
    {
        var min = ToScreen(rect.Min, vp);
        var max = ToScreen(rect.Max, vp);
        return new System.Windows.Rect(min, max);
    }

    public static System.Windows.Point[] ToScreen(LineSegment2D seg, ViewportTransform vp) =>
        [ToScreen(seg.Start, vp), ToScreen(seg.End, vp)];

    /// <summary>Screen-space length of one world inch at current scale.</summary>
    public static double PixelsPerInch(ViewportTransform vp) => (double)vp.ScalePixelsPerInch;
}
```

### 7.2 Zoom Behavior

- Grid minor lines suppressed below a threshold scale (e.g., < 4px/inch) to avoid noise
- Cabinet labels suppressed when projected bounds are narrower than the label text
- Handle sizes are fixed in screen pixels — they do not scale with zoom
- Snap line weight is independent of zoom level
- All thresholds are constants on the render layer, not domain rules

### 7.3 Pan and Zoom Rendering Interaction

During `PanningViewport` and `ZoomingViewport` modes, rendering receives an updated `ViewportTransform` and triggers a full redraw. No snap or ghost overlays are drawn in these modes (the scene DTO omits them).

---

## 8. Redraw Strategy

### 8.1 Invalidation Model

Rendering uses a **dirty-flag invalidation model** backed by WPF's `InvalidateVisual()`. There is no retained scene graph — each redraw re-projects the current `RenderSceneDto` from scratch.

```csharp
namespace CabinetDesigner.Rendering;

/// <summary>
/// The canvas rendering element. Hosted by the Presentation layer in a WPF layout.
/// Calls InvalidateVisual() when notified of scene changes.
/// All drawing occurs in OnRender; no geometry is cached between passes.
/// </summary>
public sealed class EditorCanvas : FrameworkElement
{
    private RenderSceneDto? _scene;
    private ViewportTransform _viewport = ViewportTransform.Default;
    private readonly IReadOnlyList<IRenderLayer> _layers;
    private readonly IHitTester _hitTester;

    public void UpdateScene(RenderSceneDto scene)
    {
        _scene = scene;
        InvalidateVisual();
    }

    public void UpdateViewport(ViewportTransform viewport)
    {
        _viewport = viewport;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_scene is null) return;
        foreach (var layer in _layers)
            layer.Draw(dc, _scene, _viewport);
    }
}
```

### 8.2 Scene Update Triggers

| Event | Scene Area Updated | Redraw Scope |
|---|---|---|
| Mouse move during drag | `SnapOverlay`, `PreviewGhost`, `HUD` only | Full redraw (lightweight DTO swap) |
| Design change committed | All committed geometry layers | Full redraw |
| Selection changed | `SelectionOverlay` | Full redraw |
| Viewport pan / zoom | No DTO change — viewport swap only | Full redraw |
| Hover changed | `CabinetRenderState.Hovered` updated | Full redraw |

> **Note:** All redraws are full — the dirty-flag model avoids partial region management complexity. At interactive frame rates (~60fps during drag) a full redraw of a typical floor plan (< 200 cabinets) is well within budget. Partial invalidation can be introduced later if profiling reveals it is necessary.

### 8.3 DTO Swap Safety

The `RenderSceneDto` is immutable. The ViewModel constructs a new snapshot on each preview cycle and calls `EditorCanvas.UpdateScene(dto)`. There are no partial mutations. Rendering never holds a reference to a DTO beyond the current `OnRender` call.

---

## 9. Performance Constraints

### 9.1 Drag-Time Target

| Metric | Requirement |
|---|---|
| Frame time during drag | ≤ 16ms (60fps target) |
| Scene DTO construction time | ≤ 4ms |
| Render pass time | ≤ 8ms |
| Hit test time per mouse-move | ≤ 2ms |
| Snap overlay projection | ≤ 2ms |

These are interactive-feel targets, not hard system limits. Degradation is acceptable for very large scenes, but the typical MVP scene (< 200 cabinets, < 20 runs) must hit them reliably.

### 9.2 Performance Design Decisions

- **No retained geometry:** render from scratch per frame — avoids stale-state bugs
- **No LINQ in hot path:** layer drawing loops use `for` over pre-allocated lists, not LINQ chains
- **Pre-projected values:** `ScreenProjection` helpers are pure functions with no allocation; results are used directly in DrawingContext calls
- **No WPF dependency properties on the canvas element:** `EditorCanvas` uses field-backed state updated by method calls from the ViewModel — no binding overhead in the render path
- **Snap and ghost overlay drawn last:** if render time is over budget, these layers degrade first without corrupting the committed scene
- **Grid suppression at low scale:** avoids thousands of line draw calls at high zoom-out

### 9.3 DrawingContext Use

All drawing uses WPF's `DrawingContext` API directly (in `OnRender`). No `Visual` tree children are created for scene entities — all geometry is drawn imperatively. This keeps the visual tree minimal and eliminates WPF layout pass cost during drag.

---

## 10. Invariants

1. **Rendering never mutates state.** `OnRender`, `HitTest`, and all layer `Draw` methods have no side effects.
2. **Rendering never reads domain entities.** All data arrives via `RenderSceneDto`. No aggregate lookups.
3. **Hit test results are identifiers only.** `HitTestResult` carries string IDs; the rendering layer does not resolve them to objects.
4. **ViewportTransform flows in one direction.** Rendering reads it from the editor layer; it never writes back.
5. **Layer order is fixed.** Layers are registered in index order at construction and called in that order. Layers may not reorder themselves.
6. **Ghost and snap overlays are transient.** They are present in the DTO only during drag; rendering draws them unconditionally when present. The application/editor layer controls their presence.
7. **Screen coordinates use `double`.** World coordinates use `decimal` (via geometry value objects). The conversion happens exactly once, in `ScreenProjection`. No rendering code uses `decimal` arithmetic after projection.
8. **Rendering has no knowledge of EditorMode.** It draws what the DTO contains. Mode-specific behavior is expressed by what the editor puts into the DTO.

---

## 11. Testing Strategy

### 11.1 Projection Tests (Unit)

- `ScreenProjection.ToScreen` round-trip at default scale
- `ToScreen(rect)` produces correct pixel bounding box at various scales
- Zoom-in and zoom-out produce correct pixel distances
- `ToScreen` at negative offset (panned canvas)

### 11.2 Hit Test Tests (Unit)

- `HitTest` returns `HitTestTarget.None` for empty scene
- `HitTest` correctly returns `Cabinet` for point inside projected bounds
- `HitTest` returns `Handle` over `Cabinet` when point is within handle tolerance
- `HitTest` returns `Wall` for point within 6px of projected wall segment
- `HitTest` priority is correct: Handle wins over Cabinet wins over Run wins over Wall
- Handle tolerance is applied in screen space, not world space (zoom-invariant)

### 11.3 DTO Mapping Tests (Unit)

- `PreviewValidity` → fill color mapping
- `SnapStrength` → visual weight mapping
- `CabinetRenderState.Invalid` → error color

### 11.4 Layer Isolation Tests (Unit)

- Each `IRenderLayer` implementation can be constructed and `Draw` called on an empty scene without throwing
- Each layer only accesses its relevant portion of `RenderSceneDto`

### 11.5 Integration Tests

- `EditorCanvas.UpdateScene` followed by forced `OnRender` does not throw for any valid `RenderSceneDto` shape
- Null ghost and null snap overlay produce a complete render without branching errors
- Hit test against a scene with overlapping wall and cabinet at same point returns `Cabinet` (priority enforced)

### 11.6 Performance Tests (Benchmark)

- Render pass for 200 cabinets + 20 runs + snap overlay + ghost ≤ 8ms median
- Hit test for 200 cabinets ≤ 2ms per call
- Scene DTO swap (`UpdateScene`) cost ≤ 0.5ms

---

## 12. Risks and Edge Cases

| Risk | Mitigation |
|---|---|
| `decimal` geometry in DTO passed to `DrawingContext` (which uses `double`) | `ScreenProjection` is the single conversion boundary; all downstream draw calls use `double` or `System.Windows.Point` |
| Snap overlay drawn for stale DTO after drag ends | DTO is replaced atomically on drag end; ghost and snap fields become null; rendering draws nothing |
| Handle hit tolerance too large at high zoom — wrong entity selected | All tolerances are in screen pixels; scale does not affect tolerance |
| Wall hit testing is too slow for large room scenes | `LineSegment2D.DistanceTo` is O(1); spatial acceleration not needed for MVP room scale (< 50 walls) |
| Grid draws thousands of lines at high zoom-out | Minor grid suppressed below scale threshold; grid density is adaptive |
| Cabinet labels overlap at low zoom | Labels suppressed when projected width < threshold — no wrapping or truncation attempted |
| Ghost coloring conflicts with underlying cabinet coloring | Ghost drawn at Layer 7 (above committed cabinets); alpha blend ensures both visible |
| Simultaneous snap overlay and run boundary guides visually clash | Snap overlay draws on top of guide lines; rendering order resolves conflict deterministically |
| Future 3D path | World-space geometry is already axis-independent. `RenderSceneDto` is a DTO with no 2D-specific invariants that would block a parallel 3D renderer consuming the same scene projection. Screen-space projection (`ScreenProjection`) is isolated — a 3D renderer replaces this boundary only. Domain geometry value objects are shared. |
| WPF rendering thread vs. DTO update thread | `UpdateScene` must be called on the WPF dispatcher thread. The ViewModel is responsible for marshaling before calling `EditorCanvas.UpdateScene`. No cross-thread DTO swap is performed inside the rendering layer. |
