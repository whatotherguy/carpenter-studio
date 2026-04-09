# P11 — Presentation Layer Design

Source: `cabinet_ai_prompt_pack_v4_1_full.md`
Context: `architecture_summary.md`, `code_phase_global_instructions.md`, `application_layer.md`, `editor_engine.md`, `validation_engine.md`, `cross_cutting.md`

---

## 1. Goals

- Define the WPF/MVVM presentation layer as a **thin, binding-driven shell** over application services
- ViewModels consume application DTOs and application events — never domain entities or value objects
- All design mutations flow through application services, which route through the orchestrator — no code-behind business logic, no ViewModel-initiated domain mutation
- UI interaction state (selection, hover, drag preview) flows through the editor layer, not through ViewModel-internal state machines
- The presentation layer is the only place where user-facing display formatting (fractional inches, culture-specific strings) occurs
- Support design-time data and ViewModel testability without WPF runtime dependencies
- Leave room for future multi-document workflows without over-engineering the current single-project shell

---

## 2. Boundaries

### 2.1 What the Presentation Layer Owns

| Responsibility | Project |
|---|---|
| WPF windows, panels, user controls | `CabinetDesigner.Presentation` |
| ViewModels (all `ObservableObject` derivatives) | `CabinetDesigner.Presentation` |
| Command routing from UI gestures to application services | `CabinetDesigner.Presentation` |
| DTO-to-display-value mapping (formatting, unit conversion) | `CabinetDesigner.Presentation` |
| Event subscriptions and ViewModel property refresh | `CabinetDesigner.Presentation` |
| Design-time data providers | `CabinetDesigner.Presentation` |
| WPF resource dictionaries, styles, converters | `CabinetDesigner.Presentation` |
| Status bar aggregation | `CabinetDesigner.Presentation` |

### 2.2 What the Presentation Layer Does NOT Own

| Excluded Responsibility | Owned By |
|---|---|
| Domain entities, aggregates, value objects | `CabinetDesigner.Domain` |
| Resolution pipeline, command handlers, orchestrator | `CabinetDesigner.Application` |
| Editor interaction state machine, snap evaluation, drag context | `CabinetDesigner.Editor` |
| Canvas rendering, hit testing, adorners, guides | `CabinetDesigner.Rendering` |
| Persistence, SQLite, repositories | `CabinetDesigner.Persistence` |
| File I/O, logging, settings infrastructure | `CabinetDesigner.Infrastructure` |
| Filler math, run resolution, assembly rules | `CabinetDesigner.Domain` |
| Control template pixel design (visual design is a separate concern) | Designer / XAML resources |

### 2.3 Dependency Direction

```
CabinetDesigner.App (startup, DI wiring)
    └──▶ CabinetDesigner.Presentation
              ├──▶ CabinetDesigner.Application (services, DTOs, events)
              ├──▶ CabinetDesigner.Editor (interaction service, editor session queries)
              └──▶ CabinetDesigner.Rendering (canvas host, render surface)
```

Presentation depends on Application interfaces and DTOs. Application does not depend on Presentation. Presentation never references `CabinetDesigner.Domain` directly — all domain data arrives pre-shaped as DTOs.

---

## 3. Screen / Workspace Architecture

### 3.1 Shell Layout

The application uses a single-window shell with docked panels. The shell hosts the canvas workspace and surrounding tool panels.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Title Bar  [Project Name — CarpenterStudio]            [_][□][X]            │
├──────────────────────────────────────────────────────────────────────────────┤
│  Menu Bar   File  Edit  View  Design  Tools  Help                            │
├──────────────────────────────────────────────────────────────────────────────┤
│  Toolbar    [Undo][Redo] │ [Select][Draw Run] │ [Zoom Fit][Grid] │ [Save]   │
├────────────┬─────────────────────────────────────────────┬───────────────────┤
│            │                                             │                   │
│  Catalog   │                                             │  Property         │
│  Panel     │              Editor Canvas                  │  Inspector        │
│            │              (hosted from Rendering)        │  Panel            │
│  - Cabinet │                                             │                   │
│    Types   │                                             │  - Selected       │
│  - Search  │                                             │    entity props   │
│  - Drag    │                                             │  - Overrides      │
│    source  │                                             │  - Run context    │
│            │                                             │                   │
├────────────┤                                             ├───────────────────┤
│            │                                             │                   │
│  Run       │                                             │  Validation       │
│  Summary   │                                             │  Issue Panel      │
│  Panel     │                                             │                   │
│            │                                             │  - Severity       │
├────────────┴─────────────────────────────────────────────┴───────────────────┤
│  Status Bar   [Issues: 0E 2W 1I] │ [Revision: Draft v3] │ [Saved ✓]        │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Panel Visibility and Layout

- Panel visibility is a user preference (stored in `user_preferences.json` via `ISettingsProvider`)
- Panel layout is persisted as user preferences: docked position, collapsed/expanded state
- The canvas always occupies remaining space after panel allocation
- No panel contains business logic — panels are binding targets for ViewModel-exposed data

### 3.3 Future Multi-Document Considerations

The shell ViewModel structure uses a single `ActiveProject` concept today. Future multi-document support would:
- Replace `ShellViewModel.ActiveProject` with an `IReadOnlyList<ProjectShellViewModel>` and a `SelectedProject` property
- Add a tab strip or document switcher in the shell
- Each `ProjectShellViewModel` would own its own `EditorCanvasViewModel`, panel ViewModels, and event subscriptions

This future shape is acknowledged but not built. No interfaces are pre-abstracted for it.

---

## 4. ViewModel Catalog

### 4.1 ShellViewModel

The root ViewModel. Owns the application-level lifecycle and hosts all panel ViewModels.

```csharp
namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// Root ViewModel for the main application window.
/// Owns project lifecycle, panel composition, and top-level commands.
/// Subscribes to ProjectOpenedEvent, ProjectClosedEvent.
/// </summary>
public sealed class ShellViewModel : ObservableObject, IDisposable
{
    // ── Dependencies (injected) ──────────────────────────────────────
    private readonly IProjectService _projectService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly IApplicationEventBus _eventBus;

    // ── Child ViewModels ─────────────────────────────────────────────
    public EditorCanvasViewModel Canvas { get; }
    public CatalogPanelViewModel Catalog { get; }
    public PropertyInspectorViewModel PropertyInspector { get; }
    public RunSummaryPanelViewModel RunSummary { get; }
    public IssuePanelViewModel IssuePanel { get; }
    public RevisionHistoryViewModel RevisionHistory { get; }
    public StatusBarViewModel StatusBar { get; }

    // ── State ────────────────────────────────────────────────────────
    public ProjectSummaryDto? ActiveProject { get; private set; }
    public bool HasActiveProject => ActiveProject is not null;
    public string WindowTitle => ActiveProject is not null
        ? $"{ActiveProject.Name} — CarpenterStudio"
        : "CarpenterStudio";

    // ── Commands ─────────────────────────────────────────────────────
    public IAsyncRelayCommand NewProjectCommand { get; }
    public IAsyncRelayCommand OpenProjectCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CloseProjectCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }

    // ── Event Handlers ───────────────────────────────────────────────
    // Subscribed in constructor, unsubscribed in Dispose()
    // ProjectOpenedEvent  → sets ActiveProject, enables child VMs
    // ProjectClosedEvent  → clears ActiveProject, resets child VMs
    // UndoAppliedEvent    → refreshes UndoCommand/RedoCommand CanExecute
    // RedoAppliedEvent    → refreshes UndoCommand/RedoCommand CanExecute
}
```

### 4.2 EditorCanvasViewModel

Bridges the WPF canvas host and the editor interaction layer. Does not own rendering — delegates to `CabinetDesigner.Rendering` for visual output.

```csharp
namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// ViewModel for the editor canvas panel.
/// Translates WPF mouse/keyboard events into editor interaction service calls.
/// Exposes preview state (snap candidates, placement ghosts) for the rendering layer.
/// Subscribes to DesignChangedEvent to trigger canvas refresh.
/// </summary>
public sealed class EditorCanvasViewModel : ObservableObject, IDisposable
{
    private readonly IEditorInteractionService _editorService;
    private readonly IApplicationEventBus _eventBus;

    // ── Bound State (live, updated on every mouse-move during drag) ──
    /// <summary>Current preview result during drag. Null when idle.</summary>
    public PreviewResultDto? ActivePreview { get; private set; }

    /// <summary>Current editor mode, read from EditorSession.</summary>
    public EditorMode CurrentMode { get; private set; }

    /// <summary>Selected entity IDs for highlight rendering.</summary>
    public IReadOnlyList<Guid> SelectedEntityIds { get; private set; }

    /// <summary>Hovered entity ID for hover highlight.</summary>
    public Guid? HoveredEntityId { get; private set; }

    // ── Derived State (refreshed on DesignChangedEvent) ──────────────
    /// <summary>
    /// Scene render data. Refreshed from IEditorSceneGraph on DesignChangedEvent.
    /// The rendering layer reads this to draw the canvas.
    /// </summary>
    public SceneRenderDataDto? SceneData { get; private set; }

    // ── Input Methods (called from code-behind input handlers) ───────
    // These are the ONLY code-behind methods allowed — pure input forwarding.
    // No business logic. No state decisions.

    /// <summary>Forward mouse-move to editor interaction service.</summary>
    public void OnMouseMove(double screenX, double screenY);

    /// <summary>Forward mouse-down to editor interaction service.</summary>
    public void OnMouseDown(double screenX, double screenY, MouseButton button);

    /// <summary>Forward mouse-up / drop to editor interaction service.</summary>
    public void OnMouseUp(double screenX, double screenY, MouseButton button);

    /// <summary>Forward scroll wheel to editor for zoom.</summary>
    public void OnMouseWheel(double screenX, double screenY, int delta);

    /// <summary>Forward key press (Escape for abort, Delete for remove, etc.).</summary>
    public void OnKeyDown(Key key, ModifierKeys modifiers);
}
```

**Live vs. derived state distinction:**
- **Live state** (`ActivePreview`, `CurrentMode`, `SelectedEntityIds`, `HoveredEntityId`) — updated on every mouse event during drag, via direct editor service calls. Must be fast (< 16 ms round-trip).
- **Derived state** (`SceneData`) — refreshed when `DesignChangedEvent` fires after a committed command. May take up to 200 ms to recompute. Triggers full canvas repaint.

### 4.3 CatalogPanelViewModel

Source for drag-and-drop cabinet placement.

```csharp
namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// Exposes the cabinet type catalog for browsing and drag initiation.
/// Loaded once on project open. Filtered by user search input.
/// Does not subscribe to DesignChangedEvent — catalog is static within a session.
/// </summary>
public sealed class CatalogPanelViewModel : ObservableObject
{
    // ── State ────────────────────────────────────────────────────────
    public IReadOnlyList<CatalogItemViewModel> AllItems { get; private set; }
    public IReadOnlyList<CatalogItemViewModel> FilteredItems { get; private set; }
    public string SearchText { get; set; }  // Two-way bound, triggers filter

    // ── Drag Initiation ──────────────────────────────────────────────
    /// <summary>
    /// Called when the user begins dragging a catalog item.
    /// Notifies the EditorCanvasViewModel to start a PlaceCabinet drag.
    /// </summary>
    public void BeginDrag(CatalogItemViewModel item);
}

public sealed class CatalogItemViewModel : ObservableObject
{
    public string CabinetTypeId { get; init; }
    public string DisplayName { get; init; }
    public string Category { get; init; }    // e.g., "Base", "Wall", "Tall"
    public string Description { get; init; }
    public string DefaultWidthDisplay { get; init; }  // e.g., "36\""
}
```

### 4.4 PropertyInspectorViewModel

Shows and edits properties of the currently selected entity.

```csharp
namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// Displays properties of the selected cabinet, run, or room element.
/// Reads from application services when selection changes.
/// Writes via application services (which route through the orchestrator).
/// Subscribes to DesignChangedEvent to refresh after commits.
/// </summary>
public sealed class PropertyInspectorViewModel : ObservableObject, IDisposable
{
    private readonly IRunService _runService;
    private readonly IApplicationEventBus _eventBus;

    // ── State ────────────────────────────────────────────────────────
    public string? SelectedEntityLabel { get; private set; }  // "Base Cabinet 36\" — Run 1, Slot 3"
    public bool HasSelection { get; private set; }

    /// <summary>
    /// Flat list of editable property rows for the selected entity.
    /// Each row is a key-value pair with display name, current value, and edit command.
    /// </summary>
    public IReadOnlyList<PropertyRowViewModel> Properties { get; private set; }

    /// <summary>
    /// Validation issues scoped to the selected entity.
    /// Shown inline below the property grid.
    /// </summary>
    public IReadOnlyList<ValidationIssueSummaryDto> EntityIssues { get; private set; }

    // ── Methods ──────────────────────────────────────────────────────
    /// <summary>
    /// Called by the shell or canvas when selection changes.
    /// Reads entity properties from application services.
    /// </summary>
    public void OnSelectionChanged(IReadOnlyList<Guid> selectedIds);
}

/// <summary>
/// One row in the property inspector grid.
/// Supports display, inline editing, and override indication.
/// </summary>
public sealed class PropertyRowViewModel : ObservableObject
{
    public string Key { get; init; }           // Parameter key (e.g., "NominalWidth")
    public string DisplayName { get; init; }    // "Width"
    public string DisplayValue { get; set; }    // "36\"" — formatted for display
    public bool IsOverridden { get; init; }     // True if value differs from inherited default
    public string InheritedFrom { get; init; }  // "Run default" / "Shop standard" / etc.
    public bool IsEditable { get; init; }

    /// <summary>Commit an edit. Routes through application service → orchestrator.</summary>
    public IAsyncRelayCommand CommitEditCommand { get; }

    /// <summary>Clear the override, reverting to inherited value.</summary>
    public IAsyncRelayCommand ClearOverrideCommand { get; }
}
```

### 4.5 RunSummaryPanelViewModel

Shows an overview of the active run and its cabinet slots.

```csharp
namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// Displays a summary strip of the active run: slots, total width, filler status.
/// Refreshed on DesignChangedEvent when AffectedEntityIds includes the active run.
/// Selection of a slot in this panel syncs with the canvas selection.
/// </summary>
public sealed class RunSummaryPanelViewModel : ObservableObject, IDisposable
{
    private readonly IRunService _runService;
    private readonly IApplicationEventBus _eventBus;

    // ── State ────────────────────────────────────────────────────────
    public RunSummaryDto? ActiveRun { get; private set; }
    public bool HasActiveRun => ActiveRun is not null;
    public string TotalWidthDisplay { get; private set; }   // "144\""
    public string CabinetCountDisplay { get; private set; }  // "4 cabinets"

    public IReadOnlyList<RunSlotViewModel> Slots { get; private set; }

    /// <summary>Select a slot — syncs selection to canvas and property inspector.</summary>
    public IRelayCommand<RunSlotViewModel> SelectSlotCommand { get; }
}

public sealed class RunSlotViewModel : ObservableObject
{
    public Guid CabinetId { get; init; }
    public string CabinetTypeId { get; init; }
    public string WidthDisplay { get; init; }   // "36\""
    public int Index { get; init; }
    public bool IsSelected { get; set; }
}
```

### 4.6 IssuePanelViewModel

Aggregated validation issues across the project.

```csharp
namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// Displays all validation issues for the current project.
/// Subscribes to DesignChangedEvent and ValidationIssuesChangedEvent.
/// Supports filtering by severity and entity.
/// Clicking an issue selects the affected entity on the canvas.
/// </summary>
public sealed class IssuePanelViewModel : ObservableObject, IDisposable
{
    private readonly IValidationSummaryService _validationService;
    private readonly IApplicationEventBus _eventBus;

    // ── State ────────────────────────────────────────────────────────
    public IReadOnlyList<IssueRowViewModel> AllIssues { get; private set; }
    public IReadOnlyList<IssueRowViewModel> FilteredIssues { get; private set; }

    /// <summary>Active severity filter. Null = show all.</summary>
    public string? SeverityFilter { get; set; }

    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public int InfoCount { get; private set; }
    public bool HasManufactureBlockers { get; private set; }

    /// <summary>Navigate to the affected entity on the canvas.</summary>
    public IRelayCommand<IssueRowViewModel> GoToEntityCommand { get; }
}

public sealed class IssueRowViewModel : ObservableObject
{
    public string Severity { get; init; }      // "Error", "Warning", etc.
    public string Code { get; init; }
    public string Message { get; init; }
    public IReadOnlyList<string> AffectedEntityIds { get; init; }
}
```

### 4.7 RevisionHistoryViewModel

Read-only list of project revisions and snapshots.

```csharp
namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// Displays the revision history for the current project.
/// Subscribes to RevisionApprovedEvent.
/// Supports loading a read-only snapshot for comparison.
/// </summary>
public sealed class RevisionHistoryViewModel : ObservableObject, IDisposable
{
    private readonly ISnapshotService _snapshotService;
    private readonly IApplicationEventBus _eventBus;

    // ── State ────────────────────────────────────────────────────────
    public IReadOnlyList<RevisionRowViewModel> Revisions { get; private set; }
    public RevisionRowViewModel? SelectedRevision { get; set; }

    /// <summary>Load a snapshot for read-only viewing.</summary>
    public IAsyncRelayCommand<RevisionRowViewModel> LoadSnapshotCommand { get; }

    /// <summary>Approve the current working revision.</summary>
    public IAsyncRelayCommand ApproveRevisionCommand { get; }
}

public sealed class RevisionRowViewModel : ObservableObject
{
    public Guid RevisionId { get; init; }
    public string Label { get; init; }
    public string CreatedAtDisplay { get; init; }
    public string ApprovalState { get; init; }
    public bool IsApproved { get; init; }
    public bool IsLocked { get; init; }
}
```

### 4.8 StatusBarViewModel

Aggregates project-wide status indicators.

```csharp
namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// Bottom status bar. Shows validation summary counts, revision label,
/// save state, and autosave indicator.
/// Subscribes to DesignChangedEvent, AutosaveCompletedEvent, ValidationIssuesChangedEvent.
/// </summary>
public sealed class StatusBarViewModel : ObservableObject, IDisposable
{
    // ── State ────────────────────────────────────────────────────────
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public int InfoCount { get; private set; }
    public bool HasManufactureBlockers { get; private set; }

    public string RevisionLabel { get; private set; }    // "Draft v3"
    public bool HasUnsavedChanges { get; private set; }
    public string SaveStateDisplay { get; private set; }  // "Saved" / "Unsaved changes"
}
```

---

## 5. Command Routing

### 5.1 Command Flow

All user-initiated actions follow the same path:

```
WPF input (click, key, menu, drag-drop)
   │
   ▼
ViewModel ICommand / input method
   │
   │  For design mutations:
   │  ViewModel calls application service (e.g., IRunService.AddCabinetAsync)
   │  Service constructs IDesignCommand → IDesignCommandHandler → Orchestrator
   │
   │  For editor interactions (selection, zoom, pan):
   │  ViewModel calls IEditorInteractionService
   │  Editor updates EditorSession state → IEditorCommandHandler
   │
   │  For project lifecycle (open, save, close):
   │  ViewModel calls IProjectService async methods
   │
   ▼
Result (CommandResultDto / PreviewResultDto / void)
   │
   ▼
Event published (DesignChangedEvent / ProjectOpenedEvent / etc.)
   │
   ▼
Subscribed ViewModels refresh observable properties
   │
   ▼
WPF bindings update UI
```

### 5.2 Command Types in Presentation

ViewModels use `IRelayCommand` and `IAsyncRelayCommand` (from CommunityToolkit.Mvvm or equivalent). These are thin wrappers that:
- Call application service methods
- Update `CanExecute` based on ViewModel state
- Handle `async void` safely with try/catch error handling

```csharp
// Example: UndoCommand in ShellViewModel
UndoCommand = new RelayCommand(
    execute: () =>
    {
        var result = _undoRedoService.Undo();
        // Result propagates via UndoAppliedEvent — no manual refresh needed
    },
    canExecute: () => _undoRedoService.CanUndo);
```

### 5.3 CanExecute Refresh

`CanExecute` must be re-evaluated when relevant state changes. The pattern:

1. ViewModel subscribes to application events in its constructor
2. Event handler calls `OnPropertyChanged` for state properties and `NotifyCanExecuteChanged()` on affected commands
3. WPF automatically re-queries `CanExecute` and enables/disables bound controls

```csharp
// In ShellViewModel constructor:
_eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
_eventBus.Subscribe<UndoAppliedEvent>(OnUndoApplied);

private void OnDesignChanged(DesignChangedEvent e)
{
    UndoCommand.NotifyCanExecuteChanged();
    RedoCommand.NotifyCanExecuteChanged();
    OnPropertyChanged(nameof(ActiveProject)); // triggers HasUnsavedChanges refresh
}
```

### 5.4 Code-Behind Policy

Code-behind is permitted **only** for:
1. WPF input event forwarding to ViewModel methods (`MouseMove`, `MouseDown`, `KeyDown` → `EditorCanvasViewModel.OnMouseMove(...)`)
2. Drag-and-drop initiation from catalog panel (WPF DragDrop API requires code-behind)
3. Focus management that cannot be expressed in XAML bindings

Code-behind must **never**:
- Call application services directly
- Contain conditional logic based on domain state
- Transform or interpret DTOs
- Create or execute commands

---

## 6. Event / Update Flow

### 6.1 Event Subscription Pattern

Every ViewModel that subscribes to events follows this lifecycle:

```csharp
public sealed class SomeViewModel : ObservableObject, IDisposable
{
    private readonly IApplicationEventBus _eventBus;

    public SomeViewModel(IApplicationEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
    }

    private void OnDesignChanged(DesignChangedEvent e)
    {
        // Update observable properties from service queries
        // Call OnPropertyChanged / NotifyCanExecuteChanged
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
    }
}
```

**Rules:**
- Subscribe in constructor, unsubscribe in `Dispose()`
- Event handlers must not throw — they log and degrade gracefully
- Event handlers must not dispatch new design commands (no cascading mutations)
- Event handlers run on the UI thread (per `IApplicationEventBus` delivery semantics)

### 6.2 Event-to-ViewModel Mapping

| Event | Consuming ViewModels | Refresh Action |
|---|---|---|
| `DesignChangedEvent` | `EditorCanvasViewModel`, `PropertyInspectorViewModel`, `RunSummaryPanelViewModel`, `IssuePanelViewModel`, `StatusBarViewModel` | Re-query affected data from services; update observable properties |
| `ProjectOpenedEvent` | `ShellViewModel` | Set `ActiveProject`; initialize child VMs; update window title |
| `ProjectClosedEvent` | `ShellViewModel` | Clear `ActiveProject`; reset child VMs; update window title |
| `RevisionApprovedEvent` | `RevisionHistoryViewModel`, `StatusBarViewModel` | Append revision to list; update revision label |
| `UndoAppliedEvent` | `EditorCanvasViewModel`, `PropertyInspectorViewModel`, `StatusBarViewModel` | Refresh canvas, property grid, undo/redo button state |
| `RedoAppliedEvent` | `EditorCanvasViewModel`, `PropertyInspectorViewModel`, `StatusBarViewModel` | Same as undo |
| `ValidationIssuesChangedEvent` | `IssuePanelViewModel`, `StatusBarViewModel` | Refresh issue list and severity counts |
| `SettingsChangedEvent` | Any ViewModel displaying setting-dependent values | Re-read from `ISettingsProvider` |
| `AutosaveCompletedEvent` | `StatusBarViewModel` | Flash "Saved" indicator |

### 6.3 Scoped Refresh

`DesignChangedEvent` carries `AffectedEntityIds`. ViewModels use this to minimize refresh scope:

- `PropertyInspectorViewModel` — only refreshes if the selected entity is in `AffectedEntityIds`
- `RunSummaryPanelViewModel` — only refreshes if the active run ID is in `AffectedEntityIds`
- `EditorCanvasViewModel` — always refreshes scene data (the rendering layer handles per-entity invalidation)

---

## 7. Binding and Data Shaping

### 7.1 DTO-to-Display Mapping

Application DTOs carry raw values (e.g., `decimal NominalWidthInches`). The presentation layer is responsible for formatting these into display strings.

**Formatting rules:**
- Dimensional values → formatted per user preference (fractional inches: `36 1/2"`, decimal: `36.5"`, metric: `927 mm`)
- Formatting logic lives in a shared `DisplayFormatter` utility in `CabinetDesigner.Presentation`
- `DisplayFormatter` reads display unit preference from `ISettingsProvider`
- ViewModels expose pre-formatted `string` properties (e.g., `WidthDisplay`) — XAML never performs formatting

```csharp
namespace CabinetDesigner.Presentation.Formatting;

/// <summary>
/// Formats dimensional values for display based on user preferences.
/// Reads display unit setting from ISettingsProvider.
/// Stateless — safe to call from any ViewModel.
/// </summary>
public sealed class DisplayFormatter
{
    private readonly ISettingsProvider _settings;

    /// <summary>Format a dimension value (in inches) for display.</summary>
    public string FormatDimension(decimal inches);

    /// <summary>Format a date for display.</summary>
    public string FormatDate(DateTimeOffset date);

    /// <summary>Format a validation severity for display.</summary>
    public string FormatSeverity(string severity);
}
```

### 7.2 Two-Way Binding for Property Edits

The property inspector supports inline editing. The binding flow:

```
PropertyRowViewModel.DisplayValue  ←→  TextBox (two-way binding)
                                            │
User edits and presses Enter                │
                                            ▼
PropertyRowViewModel.CommitEditCommand.Execute()
   │
   ▼
Parse display string → OverrideValueDto (presentation layer parses)
   │
   ▼
IRunService.SetCabinetOverrideAsync(SetCabinetOverrideRequestDto)
   │
   ▼
Application service → orchestrator → DesignChangedEvent
   │
   ▼
PropertyInspectorViewModel refreshes from service
   │
   ▼
PropertyRowViewModel.DisplayValue updated (may differ from input due to rounding/normalization)
```

**Parsing rules:**
- The presentation layer parses user input strings into typed values
- Invalid input shows an inline validation message without calling the service
- Successful parse produces an `OverrideValueDto` matching the parameter type
- The application service converts `OverrideValueDto` → domain `OverrideValue` (with geometry value object construction)

### 7.3 Collection Binding

Observable collections in ViewModels use `ObservableCollection<T>` or read-only list replacement:

- **Small, infrequently-changing lists** (revisions, issues): Replace the entire `IReadOnlyList<T>` property and raise `PropertyChanged`. Simpler than collection change tracking.
- **Catalog items**: Loaded once, filtered in-memory. `FilteredItems` is replaced on each search text change.
- **Run slots**: Replaced on `DesignChangedEvent` for the affected run.

No ViewModel exposes domain collections or mutable domain state.

---

## 8. Validation and Explanation Presentation

### 8.1 Validation Display

Validation issues are presented at three levels:

| Level | Location | Source |
|---|---|---|
| **Project-wide** | Issue panel, status bar | `IValidationSummaryService.GetAllIssues()` |
| **Entity-scoped** | Property inspector (inline) | `IValidationSummaryService.GetIssuesFor(entityId)` |
| **Canvas inline** | Adorners/badges on affected elements | Rendering layer reads issue state from `EditorCanvasViewModel` |

### 8.2 Severity Display Mapping

| Severity | Status Bar | Issue Panel | Canvas |
|---|---|---|---|
| `Info` | Count shown | Shown, collapsed by default | No visual |
| `Warning` | Count shown | Shown, yellow indicator | Yellow border/badge |
| `Error` | Count shown, red | Shown, red indicator | Red border/badge |
| `ManufactureBlocker` | Count shown, red + badge | Shown, red + blocked icon | Red border + stop icon |

### 8.3 Explanation Traceability

`CommandResultDto` carries `ExplanationNodeIds`. The presentation layer can link issues to explanations:

- Issue panel rows that have associated explanation nodes show a "Why?" link
- Clicking "Why?" queries the Why Engine (via an application service) and shows the explanation chain in a flyout or dialog
- This is a future feature surface — the ViewModel exposes the `ExplanationNodeIds` but the "Why?" UI is not part of MVP

### 8.4 Blocking Issue UX

When a user attempts an action that requires no blocking issues (e.g., Approve Revision, Export Cut List):

1. ViewModel checks `IValidationSummaryService.HasManufactureBlockers`
2. If blockers exist, the command's `CanExecute` returns `false` (button disabled)
3. A tooltip or status message explains: "Resolve all errors before approving"
4. No modal dialog on attempt — the button is simply disabled with explanation

---

## 9. Testability Guidance

### 9.1 ViewModel Testability

All ViewModels are testable without WPF:

- ViewModels depend on application service interfaces, not implementations
- `IApplicationEventBus` is replaceable with `TestEventBus` (captures published events, allows manual dispatch)
- No ViewModel references WPF types (`Window`, `Control`, `DependencyObject`, `Dispatcher`) — only `ObservableObject` base
- `IRelayCommand` / `IAsyncRelayCommand` are testable: call `Execute()`, assert state, check `CanExecute()`

### 9.2 Test Strategy

| What to Test | How |
|---|---|
| ViewModel initializes with correct default state | Construct with mock services, assert initial property values |
| Event subscription refreshes properties | Construct VM, publish event via `TestEventBus`, assert properties updated |
| Command routing calls correct service method | Construct VM with mock service, execute command, verify service was called with expected arguments |
| `CanExecute` reflects state correctly | Set up state (e.g., no active project), assert `CanExecute` returns false; set up state with project, assert true |
| Scoped refresh skips unrelated events | Publish `DesignChangedEvent` with unrelated entity IDs, assert property inspector did NOT refresh |
| Dispose unsubscribes from events | Dispose VM, publish event, assert no property changes (no stale handler) |

### 9.3 Design-Time Data

ViewModels support design-time preview in the XAML designer:

- Each ViewModel has a parameterless constructor marked `[Obsolete("Design-time only")]` that populates sample data
- Design-time data uses `d:DataContext` in XAML: `d:DataContext="{d:DesignInstance Type=vm:ShellViewModel, IsDesignTimeCreatable=True}"`
- Design-time constructors populate representative sample data (e.g., a few catalog items, a run with 3 cabinets, some validation issues)
- Design-time constructors never call services or subscribe to events

---

## 10. Invariants

1. **ViewModels never reference domain types.** All data enters via application DTOs. No `Length`, `CabinetId`, `RunId`, or domain entity appears in ViewModel code. ViewModels use `Guid`, `decimal`, `string` primitives from DTOs.

2. **No code-behind business logic.** Code-behind contains only input forwarding, drag-drop API calls, and focus management. Zero conditionals based on domain or application state.

3. **All design mutations flow through application services.** ViewModels never construct `IDesignCommand` directly. They call service methods that construct commands internally.

4. **All display formatting happens in the presentation layer.** DTOs carry raw values. `DisplayFormatter` converts to user-facing strings. XAML templates do not contain formatting logic.

5. **Every event subscription has a matching unsubscription.** ViewModels implement `IDisposable`. The `Dispose()` method unsubscribes from all events. No leaked subscriptions.

6. **Event handlers never dispatch design commands.** An event handler may refresh ViewModel state and re-query services, but it must never call a service method that triggers a new command through the orchestrator.

7. **Live editor state and derived design state are distinct.** `ActivePreview` (live, per-frame during drag) is never confused with `SceneData` (derived, refreshed on commit). Different update paths, different performance expectations.

8. **CanExecute is always consistent with state.** If a command is exposed, its `CanExecute` accurately reflects whether execution is valid. Stale `CanExecute` states are prevented by event-driven refresh.

---

## 11. Risks and Edge Cases

| Risk | Mitigation |
|---|---|
| **Stale ViewModel state after event bus error** | Event bus catches and logs handler exceptions (per `cross_cutting.md` §6.2). ViewModel state may be stale until the next event. A manual "Refresh" action in the View menu forces all ViewModels to re-query services |
| **Property inspector edit conflicts with in-flight command** | Property edits are serialized through the same command pipeline. While a command is in-flight, the property inspector's commit button is disabled (`CanExecute = false` while awaiting) |
| **Drag preview jank from slow IPreviewCommandHandler** | Performance budget: < 16 ms for fast-path preview. If exceeded, the editor layer must be profiled — not the presentation layer. The ViewModel simply forwards and binds |
| **Large issue lists degrade panel performance** | Issue panel virtualizes its list (WPF `VirtualizingStackPanel`). ViewModel exposes the full list; the panel renders only visible rows |
| **Multiple rapid DesignChangedEvents cause redundant refreshes** | ViewModels use scoped refresh via `AffectedEntityIds`. Additionally, the event bus delivers synchronously — events cannot overlap. If batching is needed in the future, it would be added at the event bus level, not in ViewModels |
| **User preference changes (display units) require full display refresh** | `SettingsChangedEvent` triggers all ViewModels to re-format display values. This is infrequent and acceptable |
| **async void in ViewModel command handlers** | All `async void` handlers wrap their body in try/catch. Exceptions are logged via `IAppLogger` and shown to the user via a non-modal error notification. Never silently swallowed |
| **Design-time constructors accidentally used at runtime** | Design-time constructors are marked `[Obsolete]`. The DI container always resolves the parameterized constructor. If the obsolete constructor is called at runtime, it produces a compiler warning |
| **Multi-document future migration** | The shell ViewModel owns child VMs directly today. Migration to a tabbed document model requires wrapping per-project VMs in a `ProjectShellViewModel` and adding a tab collection. The event subscription pattern (per-VM subscribe/unsubscribe) already supports this — each project VM subscribes independently |
