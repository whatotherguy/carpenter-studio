# U18 — Add Cabinets from the Catalog via Double-Click

## Context

`CatalogPanelViewModel.ItemActivated` exists and `ActivateItem()` fires it, but nothing subscribes
to the event.  `EditorCanvasViewModel.AddCabinetToRunAsync(Guid runId, string cabinetTypeId, decimal nominalWidthInches)`
exists but has no UI trigger.  Users currently have no way to add cabinets.

The simplest flow: user double-clicks a catalog card → the item is added to the first available run
(or the run containing the selected cabinet).

## Files to Read First

- `src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/CatalogItemViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- `src/CabinetDesigner.Presentation/Views/CatalogPanelView.xaml`
- `src/CabinetDesigner.Presentation/Views/CatalogPanelView.xaml.cs`
- `src/CabinetDesigner.Application/Services/IRunService.cs` (to see GetRunsAsync or similar)
- `src/CabinetDesigner.Rendering/DTOs/RenderSceneDto.cs`
- `src/CabinetDesigner.Rendering/DTOs/RunRenderDto.cs`

## Task

### 1. Add an "add cabinet" bridge to `ShellViewModel`

`ShellViewModel` is the natural coordinator because it has access to both `Canvas` (which knows the
selected cabinet) and `Catalog`.

Add a private async method and subscribe to `Catalog.ItemActivated` in the constructor:

```csharp
// In constructor:
Catalog.ItemActivated += OnCatalogItemActivated;

// In Dispose():
Catalog.ItemActivated -= OnCatalogItemActivated;

// New method:
private async void OnCatalogItemActivated(object? sender, CatalogItemViewModel item)
{
    if (!HasActiveProject)
        return;

    // Determine which run to add to:
    // 1. If a cabinet is selected, use its run.
    // 2. Otherwise use the first run in the scene.
    var runId = ResolveTargetRunId();
    if (runId is null)
        return;

    await Canvas.AddCabinetToRunAsync(runId.Value, item.TypeId, item.DefaultNominalWidthInches)
        .ConfigureAwait(true);
}

private Guid? ResolveTargetRunId()
{
    var scene = Canvas.Scene;
    if (scene is null || scene.Runs.Count == 0)
        return null;

    // Prefer the run of the first selected cabinet
    if (Canvas.SelectedCabinetIds.Count > 0)
    {
        var selectedId = Canvas.SelectedCabinetIds[0];
        var run = scene.Runs.FirstOrDefault(r => r.CabinetIds.Contains(selectedId));
        if (run is not null)
            return run.RunId;
    }

    // Fall back to first run
    return scene.Runs[0].RunId;
}
```

Check whether `RunRenderDto` exposes `CabinetIds` — if not, use whatever field lists the cabinets
in a run.  If `RenderSceneDto` has no run-to-cabinet mapping, fall back to always using the first
run.

### 2. Expose `DefaultNominalWidthInches` on `CatalogItemViewModel`

`CatalogItemViewModel` may only store `DefaultWidthDisplay` (a formatted string).  Add a
`decimal DefaultNominalWidthInches` property that holds the raw decimal value.

In `CatalogPanelViewModel`, when constructing the item pass the raw `item.DefaultNominalWidthInches`
through to the new property.

### 3. Wire double-click in `CatalogPanelView.xaml`

Open `CatalogPanelView.xaml`.  Find the `ItemsControl` or `ListBox` item template that renders
catalog cards.  Add a `MouseDoubleClick` (or `PreviewMouseDoubleClick`) handler on the item
container or a button within it.

The cleanest WPF approach is to attach the event in code-behind rather than XAML, to avoid coupling
the view to the ViewModel's `ActivateItem` method name.  In `CatalogPanelView.xaml.cs`:

```csharp
private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
{
    if (sender is FrameworkElement { DataContext: CatalogItemViewModel item } &&
        DataContext is CatalogPanelViewModel vm)
    {
        vm.ActivateItem(item);
    }
}
```

And in XAML on the item container (e.g., a `Border` or `Button` inside the `DataTemplate`):
```xml
MouseDoubleClick="OnItemDoubleClick"
```

Alternatively, if catalog items are rendered inside a `ListBox`, handle
`ListBox.MouseDoubleClick` on the `ListBox` element itself.

### 4. Provide a user-visible result

`AddCabinetToRunAsync` already sets `Canvas.StatusMessage` to "Cabinet added." or "Cabinet add
rejected." — the status bar already picks this up via `ShellViewModel.OnCanvasPropertyChanged`.
No additional UI feedback is needed.

## Requirements

- If no project is open, `OnCatalogItemActivated` must be a no-op.
- If there are no runs in the design, log the reason in `StatusMessage` ("No runs available to add
  to.") and return.
- Do not create a new run automatically — only add to an existing run.
- `CatalogItemViewModel` changes must not break the existing `FilteredItems` binding.

## End State

- What is now usable: Double-clicking a catalog item adds it to the current run.
- What is still missing: Cabinet labels on canvas, unsaved-changes prompt, panel resizing.
- Next prompt: U19 — Render cabinet labels on the canvas.
