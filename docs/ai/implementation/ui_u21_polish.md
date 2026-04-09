# U21 — Quality-of-Life Polish: GridSplitters, Loading Indicators, Ctrl+Click Multi-Select

## Context

Three independent quality-of-life improvements that don't depend on each other but are grouped here
to avoid a proliferation of tiny prompts:

- **GridSplitters**: Left sidebar and bottom panels are hardcoded at 320 px / 260 px with no resize
  handles.  Adding `GridSplitter` elements lets users adjust panel sizes.
- **Loading indicators**: All async operations show no progress feedback — the status text changes
  but is easy to miss.  An `IsBusy` property on the relevant commands or ViewModels, bound to a
  `ProgressBar` or spinner, communicates activity visually.
- **Ctrl+click multi-select**: `EditorCanvasViewModel.OnMouseDown` always sets a single selection
  or clears.  Adding Ctrl+click for additive selection is the first step toward multi-select.

## Files to Read First

- `src/CabinetDesigner.Presentation/MainWindow.xaml`
- `src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/IEditorCanvasHost.cs`
- `src/CabinetDesigner.App/WpfEditorCanvasHost.cs`

---

## Task A — GridSplitters in `MainWindow.xaml`

### A1. Left sidebar column splitter

In the outer `Grid` (the one with `ColumnDefinitions`), add a `GridSplitter` in the gap column
(column index 1, currently `Width="12"`):

```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="320" MinWidth="200" />
    <ColumnDefinition Width="8" />
    <ColumnDefinition Width="*" />
</Grid.ColumnDefinitions>

<GridSplitter Grid.Column="1"
              Width="8"
              HorizontalAlignment="Stretch"
              VerticalAlignment="Stretch"
              ResizeDirection="Columns"
              ResizeBehavior="PreviousAndNext"
              Background="{StaticResource ShellBorderSoftBrush}"
              Cursor="SizeWE" />
```

### A2. Bottom row splitter — left column

In the inner left `Grid` (catalog + run summary), add a `GridSplitter` in the spacer row:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="*" MinHeight="120" />
    <RowDefinition Height="8" />
    <RowDefinition Height="260" MinHeight="120" />
</Grid.RowDefinitions>

<GridSplitter Grid.Row="1"
              Height="8"
              HorizontalAlignment="Stretch"
              VerticalAlignment="Stretch"
              ResizeDirection="Rows"
              ResizeBehavior="PreviousAndNext"
              Background="{StaticResource ShellBorderSoftBrush}"
              Cursor="SizeNS" />
```

### A3. Bottom row splitter — right column

Apply the same pattern to the right column grid (canvas + issue panel).

### A4. Property Inspector column splitter

In the inner right `Grid` that splits canvas (3*) and property inspector (2*), add a splitter:

```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="3*" MinWidth="400" />
    <ColumnDefinition Width="8" />
    <ColumnDefinition Width="2*" MinWidth="200" />
</Grid.ColumnDefinitions>

<GridSplitter Grid.Column="1"
              Width="8"
              HorizontalAlignment="Stretch"
              VerticalAlignment="Stretch"
              ResizeDirection="Columns"
              ResizeBehavior="PreviousAndNext"
              Background="{StaticResource ShellBorderSoftBrush}"
              Cursor="SizeWE" />
```

---

## Task B — Loading/Busy Indicators

### B1. Add `IsExecuting` property to `AsyncRelayCommand`

`AsyncRelayCommand` already has a private `_isExecuting` field.  Expose it publicly:

```csharp
public bool IsExecuting => _isExecuting;
```

Add `OnPropertyChanged(nameof(IsExecuting))` in `PostToUiThread(NotifyCanExecuteChanged)` call
sites (after U13), or — simpler — raise it from within `PostToUiThread` itself by checking if
`_isExecuting` changed.

Actually the simplest approach: after each assignment to `_isExecuting` in `ExecuteAsync`, fire
`PropertyChanged`.  Add `public event PropertyChangedEventHandler? PropertyChanged;` to
`AsyncRelayCommand` (implement `INotifyPropertyChanged`) and raise it for `IsExecuting`:

```csharp
public event PropertyChangedEventHandler? PropertyChanged;

// In ExecuteAsync:
_isExecuting = true;
PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
PostToUiThread(NotifyCanExecuteChanged);
// ...
finally
{
    _isExecuting = false;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
    PostToUiThread(NotifyCanExecuteChanged);
}
```

### B2. Expose `IsBusy` on `ShellViewModel`

```csharp
public bool IsBusy =>
    NewProjectCommand.IsExecuting ||
    OpenProjectCommand.IsExecuting ||
    SaveCommand.IsExecuting ||
    CloseProjectCommand.IsExecuting;
```

Subscribe to `PropertyChanged` on each of those commands and fire `OnPropertyChanged(nameof(IsBusy))`:

```csharp
// In constructor:
NewProjectCommand.PropertyChanged += OnCommandPropertyChanged;
OpenProjectCommand.PropertyChanged += OnCommandPropertyChanged;
SaveCommand.PropertyChanged += OnCommandPropertyChanged;
CloseProjectCommand.PropertyChanged += OnCommandPropertyChanged;

// In Dispose():
NewProjectCommand.PropertyChanged -= OnCommandPropertyChanged;
OpenProjectCommand.PropertyChanged -= OnCommandPropertyChanged;
SaveCommand.PropertyChanged -= OnCommandPropertyChanged;
CloseProjectCommand.PropertyChanged -= OnCommandPropertyChanged;

// Handler:
private void OnCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(AsyncRelayCommand.IsExecuting))
        OnPropertyChanged(nameof(IsBusy));
}
```

### B3. Add a busy indicator in `MainWindow.xaml`

In the `ShellToolbarView` or the canvas header area, add a `ProgressBar` that is visible only when
`IsBusy` is true:

```xml
<ProgressBar IsIndeterminate="True"
             Height="3"
             Visibility="{Binding IsBusy, Converter={StaticResource BoolToVisibilityConverter}}"
             DockPanel.Dock="Top" />
```

If `BoolToVisibilityConverter` is not already in `ShellResources.xaml`, add it:

```xml
<BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter" />
```

Place the `ProgressBar` at the very top of the `DockPanel` (before the `Menu`), docked to `Top`.

---

## Task C — Ctrl+Click Multi-Select

### C1. Extend `IEditorCanvasHost` with modifier key query

Add to `IEditorCanvasHost`:

```csharp
bool IsCtrlHeld { get; }
```

### C2. Implement in `WpfEditorCanvasHost`

```csharp
public bool IsCtrlHeld =>
    System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
    System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);
```

### C3. Implement in `EditorCanvasHost` (no-op)

```csharp
public bool IsCtrlHeld => false;
```

### C4. Update `EditorCanvasViewModel.OnMouseDown`

```csharp
public void OnMouseDown(double screenX, double screenY)
{
    if (Scene is null)
        return;

    var result = _hitTester.HitTest(screenX, screenY, Scene, _editorSession.Viewport);

    if (result.Target == HitTestTarget.Cabinet && result.EntityId is Guid cabinetId)
    {
        if (_canvasHost.IsCtrlHeld)
        {
            // Additive: toggle the clicked cabinet
            var current = _editorSession.SelectedCabinetIds.ToList();
            if (current.Contains(cabinetId))
                current.Remove(cabinetId);
            else
                current.Add(cabinetId);
            _editorSession.SetSelectedCabinetIds(current);
        }
        else
        {
            _editorSession.SetSelectedCabinetIds([cabinetId]);
        }
        StatusMessage = _editorSession.SelectedCabinetIds.Count == 1
            ? "Cabinet selected."
            : $"{_editorSession.SelectedCabinetIds.Count} cabinets selected.";
    }
    else
    {
        _editorSession.SetSelectedCabinetIds([]);
        StatusMessage = "Selection cleared.";
    }

    RefreshInteractionState();
}
```

## Requirements

- GridSplitter changes are XAML-only; do not touch any `.cs` files for Task A.
- `AsyncRelayCommand` implementing `INotifyPropertyChanged` should not break existing tests — the
  interface adds behaviour, it does not remove any.
- Ctrl+click multi-select only toggles individual cabinets.  Shift+click range-select is out of scope.
- Do not remove single-click selection — it must still work when Ctrl is not held.

## End State

- What is now usable: Panels are resizable; async operations show a thin progress bar; Ctrl+click
  builds a multi-selection.
- What is still placeholder: Run switching in the run summary panel, navigate-to-entity zoom on
  issue select, save-state watch for design events (covered in U20).
- What to tackle next: Address any remaining review items or move on to real data integration.
