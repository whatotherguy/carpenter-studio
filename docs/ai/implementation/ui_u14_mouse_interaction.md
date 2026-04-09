# U14 — Wire Mouse Events to the Canvas (Click-to-Select and Hover)

## Context

`EditorCanvasViewModel.OnMouseDown(double screenX, double screenY)` exists and performs hit
testing, but nothing calls it.  `EditorCanvas` (a WPF `FrameworkElement` in
`src/CabinetDesigner.Rendering/EditorCanvas.cs`) has no `MouseDown`, `MouseMove`, or `MouseUp`
handlers, so the canvas is completely non-interactive.

`EditorCanvasViewModel` also tracks `HoveredCabinetId` and `CabinetLayer` has `HoverFill` /
`HighlightPen`, but no `MouseMove` handler performs hover hit-testing.

## Files to Read First

- `src/CabinetDesigner.Rendering/EditorCanvas.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/IEditorCanvasHost.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasHost.cs`
- `src/CabinetDesigner.App/WpfEditorCanvasHost.cs`
- `src/CabinetDesigner.Rendering/DefaultHitTester.cs`
- `src/CabinetDesigner.Rendering/IHitTester.cs`
- `src/CabinetDesigner.Editor/EditorSession.cs` (for `SetHoveredCabinetId` if it exists)
- `src/CabinetDesigner.Presentation/ViewModels/IEditorCanvasSession.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasSessionAdapter.cs`

## Task

### 1. Expose mouse event callbacks on `IEditorCanvasHost`

Add two method signatures to `IEditorCanvasHost`:

```csharp
void SetMouseDownHandler(Action<double, double> handler);
void SetMouseMoveHandler(Action<double, double> handler);
```

### 2. Implement in `WpfEditorCanvasHost`

In `WpfEditorCanvasHost.cs`:

```csharp
public void SetMouseDownHandler(Action<double, double> handler)
{
    _canvas.MouseDown += (_, e) =>
    {
        var pos = e.GetPosition(_canvas);
        handler(pos.X, pos.Y);
    };
}

public void SetMouseMoveHandler(Action<double, double> handler)
{
    _canvas.MouseMove += (_, e) =>
    {
        var pos = e.GetPosition(_canvas);
        handler(pos.X, pos.Y);
    };
}
```

Both handlers need `using System.Windows.Input;` (already present in the WPF project).

### 3. Implement in `EditorCanvasHost` (Presentation-layer non-WPF host)

`EditorCanvasHost` in `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasHost.cs` is used
when the WPF host is not present (test builds).  Add no-op implementations:

```csharp
public void SetMouseDownHandler(Action<double, double> handler) { }
public void SetMouseMoveHandler(Action<double, double> handler) { }
```

### 4. Add `OnMouseMove` to `EditorCanvasViewModel`

Below `OnMouseDown` in `EditorCanvasViewModel.cs`, add:

```csharp
public void OnMouseMove(double screenX, double screenY)
{
    if (Scene is null)
        return;

    var result = _hitTester.HitTest(screenX, screenY, Scene, _editorSession.Viewport);
    var hoveredId = result.Target == HitTestTarget.Cabinet ? result.EntityId : null;
    if (hoveredId != HoveredCabinetId)
    {
        _editorSession.SetHoveredCabinetId(hoveredId);
        RefreshScene();
    }
}
```

If `IEditorCanvasSession` does not have `SetHoveredCabinetId`, add it now:
- In `IEditorCanvasSession`: `void SetHoveredCabinetId(Guid? cabinetId);`
- In `EditorCanvasSessionAdapter`: delegate to the underlying `EditorSession`.

### 5. Wire the handlers in `EditorCanvasViewModel` constructor

At the end of the constructor in `EditorCanvasViewModel`, after all subscriptions:

```csharp
_canvasHost.SetMouseDownHandler(OnMouseDown);
_canvasHost.SetMouseMoveHandler(OnMouseMove);
```

### 6. Make `EditorCanvas` focusable so it can receive mouse events

In `EditorCanvas.cs`, inside the `#if WINDOWS` block, add to the WPF constructor:

```csharp
Focusable = true;
```

This ensures `MouseDown` fires reliably.

## Requirements

- Do not add `MouseDown` or `MouseMove` handlers directly in any XAML file — they must be wired
  through the host interface so the logic stays testable.
- Do not modify `ShellViewModel` — it already calls `RefreshSelectionDrivenPanels()` when
  `SelectedCabinetIds` changes.
- Only the `#if WINDOWS` block in `EditorCanvas.cs` needs changes; do not touch the non-WPF stub.

## End State

- What is now usable: Clicking a cabinet on the canvas selects it; moving the cursor hovers it.
- What is still missing: Keyboard shortcuts, file dialogs, canvas pan/zoom, catalog add.
- Next prompt: U15 — Wire keyboard shortcuts (Ctrl+N/O/S/W/Z/Y).
