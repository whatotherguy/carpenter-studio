# U17 — Canvas Pan/Zoom via Mouse Wheel and Middle-Click Drag

## Context

`ViewportTransform` already supports `Panned()` and `ScalePixelsPerInch`, but no mouse wheel or
middle-click/drag handlers exist.  The viewport is frozen at its default (10 px/inch at origin).
After U14 the canvas already receives `MouseDown` and `MouseMove` events, so this prompt extends
those mechanisms for pan and zoom.

## Files to Read First

- `src/CabinetDesigner.Editor/ViewportTransform.cs` (or wherever `ViewportTransform` is defined — search for `Panned` and `ScalePixelsPerInch`)
- `src/CabinetDesigner.Rendering/EditorCanvas.cs`
- `src/CabinetDesigner.Presentation/ViewModels/IEditorCanvasHost.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasHost.cs`
- `src/CabinetDesigner.App/WpfEditorCanvasHost.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/IEditorCanvasSession.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasSessionAdapter.cs`

Understand how `ViewportTransform` creates a panned/scaled copy.  If `Panned(double dx, double dy)`
returns a new `ViewportTransform` offset by (dx, dy) in screen pixels, and if `WithScale` or
similar creates a zoomed copy, note their exact signatures before writing any code.

## Task

### 1. Add mouse-wheel and middle-drag callback slots to `IEditorCanvasHost`

Add to `IEditorCanvasHost`:

```csharp
void SetMouseWheelHandler(Action<double, double, double> handler);  // x, y, delta
void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd);
```

### 2. Implement in `WpfEditorCanvasHost`

```csharp
public void SetMouseWheelHandler(Action<double, double, double> handler)
{
    _canvas.MouseWheel += (_, e) =>
    {
        var pos = e.GetPosition(_canvas);
        handler(pos.X, pos.Y, e.Delta);
        e.Handled = true;
    };
}

public void SetMiddleButtonDragHandler(
    Action<double, double> onStart,
    Action<double, double> onMove,
    Action onEnd)
{
    System.Windows.Point? dragOrigin = null;

    _canvas.MouseDown += (_, e) =>
    {
        if (e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            dragOrigin = e.GetPosition(_canvas);
            _canvas.CaptureMouse();
            var pos = dragOrigin.Value;
            onStart(pos.X, pos.Y);
        }
    };

    _canvas.MouseMove += (_, e) =>
    {
        if (dragOrigin.HasValue && e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(_canvas);
            onMove(pos.X, pos.Y);
        }
    };

    _canvas.MouseUp += (_, e) =>
    {
        if (dragOrigin.HasValue && e.ChangedButton == System.Windows.Input.MouseButton.Middle)
        {
            dragOrigin = null;
            _canvas.ReleaseMouseCapture();
            onEnd();
        }
    };
}
```

> Note: `WpfEditorCanvasHost` registers multiple `MouseMove` lambdas (one from U14 for hover
> hit-testing, one here for pan drag).  This is fine — WPF `MouseMove` is a multicast event.

### 3. Add no-op implementations in `EditorCanvasHost`

```csharp
public void SetMouseWheelHandler(Action<double, double, double> handler) { }
public void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd) { }
```

### 4. Add viewport mutation methods to `IEditorCanvasSession`

```csharp
void ZoomAt(double screenX, double screenY, double scaleFactor);
void PanBy(double dx, double dy);
```

### 5. Implement in `EditorCanvasSessionAdapter`

Inspect `ViewportTransform`'s API first.  Implement by mutating the viewport on the underlying
`EditorSession`:

```csharp
public void ZoomAt(double screenX, double screenY, double scaleFactor)
{
    var vp = Viewport;
    // zoom centred on screen point (screenX, screenY)
    var newScale = (double)vp.ScalePixelsPerInch * scaleFactor;
    newScale = Math.Clamp(newScale, 2.0, 200.0);  // min 2px/inch, max 200px/inch
    // adjust origin so the point under the cursor stays fixed:
    // newOrigin = screenPoint - scaleFactor * (screenPoint - oldOrigin)
    var newOriginX = screenX - scaleFactor * (screenX - (double)vp.OriginX);
    var newOriginY = screenY - scaleFactor * (screenY - (double)vp.OriginY);
    _editorSession.SetViewport(vp.WithScaleAndOrigin((decimal)newScale, (decimal)newOriginX, (decimal)newOriginY));
}

public void PanBy(double dx, double dy)
{
    var vp = Viewport;
    _editorSession.SetViewport(vp.Panned((decimal)dx, (decimal)dy));
}
```

Use the actual `ViewportTransform` mutation API you found.  If `ViewportTransform` is immutable
(record), create a new instance via the appropriate constructor or `with` expression.  If
`EditorSession` does not have `SetViewport`, add it — it should store the viewport so the accessor
property (`Viewport`) returns the updated value.

### 6. Add `OnMouseWheel` and pan tracking to `EditorCanvasViewModel`

```csharp
private System.Windows.Point? _panDragOrigin;  // screen coords at drag start
private (decimal originX, decimal originY) _panViewportOriginAtDragStart;

public void OnMouseWheel(double screenX, double screenY, double delta)
{
    const double zoomStep = 1.1;
    var factor = delta > 0 ? zoomStep : 1.0 / zoomStep;
    _editorSession.ZoomAt(screenX, screenY, factor);
    RefreshScene();
}

public void OnPanStart(double screenX, double screenY)
{
    // store start position — handled via drag callbacks
}

public void OnPanMove(double screenX, double screenY)
{
    // implemented as delta tracking inside WpfEditorCanvasHost; just pan by delta
}

public void OnPanEnd() { }
```

Actually, the cleanest approach is to track the previous drag position in the ViewModel and pass
deltas to `PanBy`:

```csharp
private double _lastPanX;
private double _lastPanY;

// Called by onStart
public void OnPanStart(double x, double y) { _lastPanX = x; _lastPanY = y; }

// Called by onMove
public void OnPanMove(double x, double y)
{
    _editorSession.PanBy(x - _lastPanX, y - _lastPanY);
    _lastPanX = x;
    _lastPanY = y;
    RefreshScene();
}

public void OnPanEnd() { }
```

### 7. Wire in the constructor

At the end of `EditorCanvasViewModel`'s constructor, after the mouse handlers from U14:

```csharp
_canvasHost.SetMouseWheelHandler(OnMouseWheel);
_canvasHost.SetMiddleButtonDragHandler(OnPanStart, OnPanMove, OnPanEnd);
```

## Requirements

- Zoom must be centred on the cursor position so the point under the cursor stays stationary.
- Zoom limits: 2–200 px/inch (prevents infinite zoom in/out).
- Do not change how left-click selection works (U14).
- `EditorCanvasHost` (non-WPF) only needs no-op implementations.

## End State

- What is now usable: Mouse wheel zooms the canvas; middle-click drag pans it.
- What is still missing: Adding cabinets from catalog, cabinet labels, unsaved-changes prompt.
- Next prompt: U18 — Add cabinets from the catalog via double-click.
