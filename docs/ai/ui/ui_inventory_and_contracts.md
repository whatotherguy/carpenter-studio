# MVP UI Inventory and Binding Contracts

Date: 2026-04-08
Basis: Current codebase + `docs/ai/ui/ui_gap_audit.md`

---

## 1. Shell Layout Regions

MainWindow uses a `DockPanel` with these docked areas (top to bottom), wrapping a central `Grid`:

```
┌─────────────────────────────────────────────────────────────────────┐
│  R1: Menu Bar              (DockPanel.Dock="Top")                   │
├─────────────────────────────────────────────────────────────────────┤
│  R2: Toolbar Strip         (DockPanel.Dock="Top")                   │
├────────────┬────────────────────────────────────┬───────────────────┤
│            │                                    │                   │
│  R3:       │  R5: Editor Canvas                 │  R6: Property     │
│  Catalog   │  (Grid Row 0, Col 2, star)         │  Inspector        │
│  Panel     │                                    │  (Grid Row 0,     │
│  (Grid     │                                    │   Col 4, 320px)   │
│  Row 0,    │                                    │                   │
│  Col 0,    │                                    │                   │
│  270px)    │                                    │                   │
├────────────┤                                    ├───────────────────┤
│  R4: Run   │                                    │  R7: Issue        │
│  Summary   │                                    │  Panel            │
│  (Grid     │                                    │  (Grid Row 2,     │
│  Row 2,    │                                    │   Col 3-4, 190px) │
│  Col 0-1,  │                                    │                   │
│  190px)    │                                    │                   │
├────────────┴────────────────────────────────────┴───────────────────┤
│  R8: Status Bar            (DockPanel.Dock="Bottom")                │
└─────────────────────────────────────────────────────────────────────┘
```

**Current state:** R1-R7 exist in XAML. R8 (status bar) is missing. R3, R4, R6, R7 contain placeholder bullet lists. R1, R2, R5 are functional.

---

## 2. Region / Panel Contracts

### R1: Shell Menu Bar

**Status: READY NOW**

| Field | Value |
|---|---|
| **Purpose** | Top-level application commands via standard menu layout |
| **Owning VM** | `ShellViewModel` |
| **Primary user actions** | New, Open, Save, Close, Undo, Redo |
| **Data dependencies** | `IProjectService`, `IUndoRedoService` |

**Bindings (implemented):**

| Menu Item | Command Binding | CanExecute | Status |
|---|---|---|---|
| File > New | `NewProjectCommand` | `!string.IsNullOrWhiteSpace(PendingProjectName)` | READY NOW |
| File > Open | `OpenProjectCommand` | `!string.IsNullOrWhiteSpace(PendingProjectFilePath)` | READY NOW |
| File > Save | `SaveCommand` | `HasActiveProject` | READY NOW |
| File > Close | `CloseProjectCommand` | `HasActiveProject` | READY NOW |
| Edit > Undo | `UndoCommand` | `IUndoRedoService.CanUndo` | READY NOW |
| Edit > Redo | `RedoCommand` | `IUndoRedoService.CanRedo` | READY NOW |
| View > Zoom Fit | — | — | LATER |
| View > Show Grid | — | — | LATER |
| Design > Select Mode | — | — | LATER |
| Design > Draw Run Mode | — | — | LATER |
| Tools > Options | — | — | LATER |
| Help > About | — | — | LATER |

**States:**
- No project open: Save, Close disabled. Undo/Redo disabled.
- Project open: Save, Close enabled. Undo/Redo enabled per stack state.

---

### R2: Toolbar Strip

**Status: READY NOW**

| Field | Value |
|---|---|
| **Purpose** | Quick access to frequent commands |
| **Owning VM** | `ShellViewModel` |
| **Primary user actions** | New, Open, Save, Undo, Redo |
| **Data dependencies** | Same as R1 |

**Bindings (implemented):**

| Button | Command Binding | Status |
|---|---|---|
| New | `NewProjectCommand` | READY NOW |
| Open | `OpenProjectCommand` | READY NOW |
| Save | `SaveCommand` | READY NOW |
| Undo | `UndoCommand` | READY NOW |
| Redo | `RedoCommand` | READY NOW |

**Phase 1 additions (LATER):** Mode toggle buttons (Select, Draw Run), Zoom controls.

---

### R3: Catalog Panel

**Status: NEEDS VIEWMODEL**

| Field | Value |
|---|---|
| **Purpose** | Browse cabinet types; initiate drag-to-canvas placement |
| **Owning VM** | `CatalogPanelViewModel` (new) |
| **Primary user actions** | Browse categories, search/filter, drag item to canvas |
| **Data dependencies** | Cabinet type catalog (static data or catalog service) |

**Required bindings:**

| Property | Type | Source | Status |
|---|---|---|---|
| `AllItems` | `IReadOnlyList<CatalogItemViewModel>` | Hardcoded MVP catalog or future catalog service | NEEDS VIEWMODEL |
| `FilteredItems` | `IReadOnlyList<CatalogItemViewModel>` | Filtered from `AllItems` by `SearchText` | NEEDS VIEWMODEL |
| `SearchText` | `string` (two-way) | User input | NEEDS VIEWMODEL |

**CatalogItemViewModel properties:**

| Property | Type | Purpose |
|---|---|---|
| `CabinetTypeId` | `string` | Unique type key (e.g., `"base-36"`) |
| `DisplayName` | `string` | User-facing name (e.g., `"Base Cabinet 36\""`) |
| `Category` | `string` | Group header (e.g., `"Base"`, `"Wall"`, `"Tall"`) |
| `DefaultWidthDisplay` | `string` | Pre-formatted default width |

**States:**
- **Loading:** N/A (catalog is static at MVP)
- **Empty:** "No matching items" when search yields nothing
- **Normal:** Grouped list of items

**Phase 1 scope:** Static hardcoded list of 4-8 cabinet types grouped by category. No search. No drag-drop. Click-to-add via `EditorCanvasViewModel.AddCabinetToRunAsync` as interim.

**Phase 2 scope:** Search/filter. Drag-and-drop initiation (requires code-behind DragDrop API + editor `BeginCatalogDrag`).

---

### R4: Run Summary Panel

**Status: NEEDS VIEWMODEL**

| Field | Value |
|---|---|
| **Purpose** | Overview of the active run: slot list, total width, cabinet count |
| **Owning VM** | `RunSummaryPanelViewModel` (new) |
| **Primary user actions** | View run info; click slot to select cabinet on canvas |
| **Data dependencies** | `IRunService.GetRunSummary(RunId)`, `DesignChangedEvent` |

**Required bindings:**

| Property | Type | Source | Status |
|---|---|---|---|
| `HasActiveRun` | `bool` | Derived from `ActiveRun is not null` | NEEDS VIEWMODEL |
| `ActiveRun` | `RunSummaryDto?` | `IRunService.GetRunSummary()` on selection/event | NEEDS VIEWMODEL |
| `TotalWidthDisplay` | `string` | Formatted from `RunSummaryDto.TotalNominalWidthInches` | NEEDS VIEWMODEL |
| `CabinetCountDisplay` | `string` | Formatted from `RunSummaryDto.CabinetCount` | NEEDS VIEWMODEL |
| `Slots` | `IReadOnlyList<RunSlotViewModel>` | Mapped from `RunSummaryDto.Slots` | NEEDS VIEWMODEL |
| `SelectSlotCommand` | `IRelayCommand<RunSlotViewModel>` | Syncs selection to canvas | NEEDS VIEWMODEL |

**RunSlotViewModel properties:**

| Property | Type | Purpose |
|---|---|---|
| `CabinetId` | `Guid` | Entity identifier |
| `CabinetTypeId` | `string` | Type key for label |
| `WidthDisplay` | `string` | Formatted width |
| `Index` | `int` | Slot position |
| `IsSelected` | `bool` | Matches canvas selection |

**Event subscriptions:**
- `DesignChangedEvent` — refresh if `AffectedEntityIds` includes the active run
- `UndoAppliedEvent` / `RedoAppliedEvent` — refresh unconditionally

**Active run source:** When `EditorCanvasViewModel.SelectedCabinetIds` changes and the selected cabinet belongs to a run, `RunSummaryPanelViewModel` activates that run. Requires a lookup: `IDesignStateStore.GetCabinet(id) → CabinetStateRecord.RunId`. This lookup should be exposed through an application service method, not by referencing the state store directly from Presentation.

**NEEDS APP SERVICE SUPPORT:** An `IRunService.GetRunForCabinet(Guid cabinetId)` or equivalent method to resolve which run a cabinet belongs to, returning a `RunId?`. Alternatively, `RunSummaryDto` could be looked up from `IDesignStateStore` through an existing path. Verify if `IRunService.GetRunSummary` accepts the run ID that can be derived from the selected cabinet's state record.

**States:**
- **Empty:** "No run selected" when no cabinet or run is active
- **Normal:** Slot strip with cabinet type labels and widths
- **Error:** N/A (run summary is read-only)

**Phase 1 scope:** Fully functional. Display run info when a cabinet is selected. Slot click syncs to canvas selection.

---

### R5: Editor Canvas

**Status: READY NOW (core) / NEEDS VIEWMODEL additions (input)**

| Field | Value |
|---|---|
| **Purpose** | 2D plan view editor canvas; render cabinets, walls, runs, selection |
| **Owning VM** | `EditorCanvasViewModel` |
| **Primary user actions** | Click to select, view design |
| **Data dependencies** | `ISceneProjector`, `IHitTester`, `IEditorCanvasSession`, `IEditorCanvasHost`, `DesignChangedEvent` |

**Implemented bindings:**

| Property | Type | Status |
|---|---|---|
| `CanvasView` | `object` (WPF UIElement from `IEditorCanvasHost.View`) | READY NOW |
| `Scene` | `RenderSceneDto?` | READY NOW |
| `SelectedCabinetIds` | `IReadOnlyList<Guid>` | READY NOW |
| `HoveredCabinetId` | `Guid?` | READY NOW |
| `CurrentMode` | `string` | READY NOW |
| `StatusMessage` | `string` | READY NOW |

**Implemented methods:**

| Method | Purpose | Status |
|---|---|---|
| `OnMouseDown(x, y)` | Hit test and select cabinet | READY NOW |
| `AddCabinetToRunAsync(runId, typeId, width)` | Add cabinet via `IRunService` | READY NOW |
| `MoveCabinetAsync(cabId, srcRun, tgtRun, idx)` | Move cabinet via `IRunService` | READY NOW |

**Missing input methods (NEEDS VIEWMODEL):**

| Method | Purpose | Phase |
|---|---|---|
| `OnMouseMove(x, y)` | Hover highlight, drag preview | Phase 2 |
| `OnMouseUp(x, y)` | Drop / commit | Phase 2 |
| `OnMouseWheel(x, y, delta)` | Zoom | Phase 2 |
| `OnKeyDown(key, modifiers)` | Escape, Delete, shortcuts | Phase 2 |

**States:**
- **No project:** Empty dark canvas
- **Project open, no entities:** Empty canvas with grid (when enabled)
- **Normal:** Rendered walls, runs, cabinets with selection highlights

**Phase 1 scope:** Click-to-select, scene rendering, status messages. All functional today.

**Phase 2 scope:** Mouse move hover, drag-drop placement, zoom/pan, keyboard shortcuts.

---

### R6: Property Inspector Panel

**Status: NEEDS VIEWMODEL**

| Field | Value |
|---|---|
| **Purpose** | Display and edit properties of the selected entity |
| **Owning VM** | `PropertyInspectorViewModel` (new) |
| **Primary user actions** | View properties, edit values, clear overrides |
| **Data dependencies** | Selected cabinet data from `IRunService` or state query, `DesignChangedEvent` |

**Required bindings:**

| Property | Type | Source | Status |
|---|---|---|---|
| `HasSelection` | `bool` | Derived from selected entity state | NEEDS VIEWMODEL |
| `SelectedEntityLabel` | `string?` | Formatted from selected entity DTO | NEEDS VIEWMODEL |
| `Properties` | `IReadOnlyList<PropertyRowViewModel>` | Mapped from entity properties | NEEDS VIEWMODEL |
| `EntityIssues` | `IReadOnlyList<ValidationIssueSummaryDto>` | `IValidationSummaryService.GetIssuesFor(entityId)` | NEEDS VIEWMODEL |

**PropertyRowViewModel properties:**

| Property | Type | Purpose |
|---|---|---|
| `Key` | `string` | Parameter key (e.g., `"NominalWidth"`) |
| `DisplayName` | `string` | User label (e.g., `"Width"`) |
| `DisplayValue` | `string` (two-way) | Formatted current value |
| `IsOverridden` | `bool` | True if differs from inherited default |
| `InheritedFrom` | `string` | Source level (e.g., `"Run default"`) |
| `IsEditable` | `bool` | Whether inline editing is allowed |
| `CommitEditCommand` | `IAsyncRelayCommand` | Routes through `IRunService.SetCabinetOverrideAsync` |
| `ClearOverrideCommand` | `IAsyncRelayCommand` | Reverts to inherited value |

**NEEDS APP SERVICE SUPPORT:** There is no current application service method that returns a structured list of properties with override/inheritance information for a selected cabinet. Options:
1. Add `IRunService.GetCabinetProperties(Guid cabinetId) → CabinetPropertyListDto` (preferred)
2. Construct properties manually from `RunSummaryDto` slot data (limited)

For Phase 1, a minimal read-only version can be built using `RunSlotSummaryDto` data (type ID, width, index) plus `CabinetStateRecord` data if exposed through a service.

**Event subscriptions:**
- `DesignChangedEvent` — refresh only if `AffectedEntityIds` includes the selected entity
- `UndoAppliedEvent` / `RedoAppliedEvent` — refresh if selected entity is affected

**States:**
- **Empty:** "No entity selected — click a cabinet to inspect"
- **Normal:** Property rows with values, override indicators
- **Error:** Inline validation issues below property grid

**Phase 1 scope:** Read-only display of cabinet type, width, depth, run membership. No inline editing. No override indicators.

**Phase 2 scope:** Inline editing via `CommitEditCommand`, override indicators, `ClearOverrideCommand`.

---

### R7: Validation Issue Panel

**Status: NEEDS VIEWMODEL**

| Field | Value |
|---|---|
| **Purpose** | Project-wide validation issues with severity filtering and entity navigation |
| **Owning VM** | `IssuePanelViewModel` (new) |
| **Primary user actions** | View issues, filter by severity, click to select affected entity |
| **Data dependencies** | `IValidationSummaryService.GetAllIssues()`, `DesignChangedEvent` |

**Required bindings:**

| Property | Type | Source | Status |
|---|---|---|---|
| `AllIssues` | `IReadOnlyList<IssueRowViewModel>` | Mapped from `IValidationSummaryService.GetAllIssues()` | NEEDS VIEWMODEL |
| `FilteredIssues` | `IReadOnlyList<IssueRowViewModel>` | Filtered by `SeverityFilter` | NEEDS VIEWMODEL |
| `SeverityFilter` | `string?` | User selection; null = show all | NEEDS VIEWMODEL |
| `ErrorCount` | `int` | Count from issue list | NEEDS VIEWMODEL |
| `WarningCount` | `int` | Count from issue list | NEEDS VIEWMODEL |
| `InfoCount` | `int` | Count from issue list | NEEDS VIEWMODEL |
| `HasManufactureBlockers` | `bool` | `IValidationSummaryService.HasManufactureBlockers` | NEEDS VIEWMODEL |
| `GoToEntityCommand` | `IRelayCommand<IssueRowViewModel>` | Selects affected entity on canvas | NEEDS VIEWMODEL |

**IssueRowViewModel properties:**

| Property | Type | Purpose |
|---|---|---|
| `Severity` | `string` | `"Error"`, `"Warning"`, `"Info"`, `"ManufactureBlocker"` |
| `Code` | `string` | Issue code |
| `Message` | `string` | Human-readable message |
| `AffectedEntityIds` | `IReadOnlyList<string>` | For navigation command |

**Event subscriptions:**
- `DesignChangedEvent` — refresh all issues
- `UndoAppliedEvent` / `RedoAppliedEvent` — refresh all issues

**States:**
- **Empty:** "No validation issues" (good state)
- **Normal:** Issue list with severity colors
- **Blocker warning:** Red badge or highlight when manufacture blockers exist

**Phase 1 scope:** Fully functional read-only list. Severity counts. `GoToEntityCommand` syncs canvas selection. No severity filtering UI (show all).

**Phase 2 scope:** Severity filter dropdown/toggle buttons.

---

### R8: Status Bar

**Status: NEEDS VIEWMODEL + NEEDS XAML**

| Field | Value |
|---|---|
| **Purpose** | Persistent bottom strip: validation counts, revision label, save state |
| **Owning VM** | `StatusBarViewModel` (new) |
| **Primary user actions** | Passive display (no direct actions) |
| **Data dependencies** | `IValidationSummaryService`, `ProjectSummaryDto`, `DesignChangedEvent` |

**Required bindings:**

| Property | Type | Source | Status |
|---|---|---|---|
| `ErrorCount` | `int` | `IValidationSummaryService.GetAllIssues()` count | NEEDS VIEWMODEL |
| `WarningCount` | `int` | Same | NEEDS VIEWMODEL |
| `InfoCount` | `int` | Same | NEEDS VIEWMODEL |
| `HasManufactureBlockers` | `bool` | `IValidationSummaryService.HasManufactureBlockers` | NEEDS VIEWMODEL |
| `RevisionLabel` | `string` | `ProjectSummaryDto.CurrentRevisionLabel` | NEEDS VIEWMODEL |
| `HasUnsavedChanges` | `bool` | `ProjectSummaryDto.HasUnsavedChanges` | NEEDS VIEWMODEL |
| `SaveStateDisplay` | `string` | `"Saved"` / `"Unsaved changes"` | NEEDS VIEWMODEL |
| `StatusMessage` | `string` | Forwarded from `EditorCanvasViewModel.StatusMessage` | NEEDS VIEWMODEL |

**Event subscriptions:**
- `DesignChangedEvent` — refresh validation counts and save state
- `ProjectOpenedEvent` — set revision label
- `ProjectClosedEvent` — clear all
- `UndoAppliedEvent` / `RedoAppliedEvent` — refresh counts

**XAML needed:** A `DockPanel.Dock="Bottom"` section in `MainWindow.xaml` before the main `Grid`, containing a horizontal strip with three segments:
```
[Issues: 0E 2W 1I] | [Revision: Draft v3] | [Saved / Unsaved changes]
```

**States:**
- **No project:** "Ready" or blank
- **Project open:** Validation counts + revision label + save state
- **Blockers present:** Red highlight on error count

**Phase 1 scope:** Fully functional. All data available from existing services.

---

## 3. Shared Components

### 3.1 ObservableObject (base class)

**Status: READY NOW**

Location: `src/CabinetDesigner.Presentation/ObservableObject.cs`
Provides `INotifyPropertyChanged`, `SetProperty<T>`, `OnPropertyChanged`.

### 3.2 RelayCommand / AsyncRelayCommand

**Status: READY NOW**

Location: `src/CabinetDesigner.Presentation/Commands/`
- `RelayCommand` — synchronous `ICommand` with `NotifyCanExecuteChanged`
- `AsyncRelayCommand` — async `ICommand` with reentrancy guard

### 3.3 SceneProjector / ISceneProjector

**Status: READY NOW**

Location: `src/CabinetDesigner.Presentation/Projection/`
Projects `IDesignStateStore` contents into `RenderSceneDto` for canvas rendering.

### 3.4 EditorCanvasHost / IEditorCanvasHost

**Status: READY NOW**

Location: `src/CabinetDesigner.Presentation/ViewModels/`
Wraps `EditorCanvas` (from Rendering) for WPF `ContentControl` hosting.

### 3.5 EditorCanvasSessionAdapter / IEditorCanvasSession

**Status: READY NOW**

Location: `src/CabinetDesigner.Presentation/ViewModels/`
Adapts `EditorSession` (Editor layer) to Presentation-safe `Guid`-based interface.

### 3.6 DisplayFormatter (utility)

**Status: LATER**

Not yet needed for Phase 1. Phase 1 can use inline `$"{value}\""` formatting. Phase 2 introduces `DisplayFormatter` for user-preference-driven formatting (fractional inches, decimal, metric).

### 3.7 PresentationServiceRegistration

**Status: READY NOW (needs update for new VMs)**

Location: `src/CabinetDesigner.Presentation/PresentationServiceRegistration.cs`
Must be updated to register all new ViewModels as they are created.

---

## 4. Application Services Availability Matrix

| Service | Interface | Implementation | Consumed By (Presentation) |
|---|---|---|---|
| `IProjectService` | Exists | `ProjectService` exists | `ShellViewModel` — READY NOW |
| `IRunService` | Exists | `RunService` exists | `EditorCanvasViewModel` — READY NOW; `RunSummaryPanelViewModel`, `PropertyInspectorViewModel` — NEEDS VIEWMODEL |
| `IUndoRedoService` | Exists | `UndoRedoService` exists | `ShellViewModel` — READY NOW |
| `IValidationSummaryService` | Exists | Verify implementation exists | `IssuePanelViewModel`, `StatusBarViewModel` — NEEDS VIEWMODEL |
| `ISnapshotService` | Exists | Verify implementation exists | `RevisionHistoryViewModel` — LATER |
| `IApplicationEventBus` | Exists | `ApplicationEventBus` exists | All VMs — READY NOW |
| `IDesignStateStore` | Exists | `InMemoryDesignStateStore` exists | `SceneProjector` — READY NOW |

**Service gap:** No service method returns structured cabinet properties for the property inspector. Options:
1. Add `IRunService.GetCabinetDetail(Guid cabinetId) → CabinetDetailDto` returning type, width, depth, run ID, slot index, and parameter overrides.
2. For Phase 1, construct a minimal read-only property list from `RunSlotSummaryDto` + `IDesignStateStore.GetCabinet()` data exposed through a thin query method.

---

## 5. Event-to-ViewModel Subscription Map

| Event | ShellVM | CanvasVM | RunSummaryVM | PropertyVM | IssueVM | StatusBarVM |
|---|---|---|---|---|---|---|
| `ProjectOpenedEvent` | X | — | — | — | — | X |
| `ProjectClosedEvent` | X | X | X | X | X | X |
| `DesignChangedEvent` | — | X | X | X | X | X |
| `UndoAppliedEvent` | X | X | X | X | X | X |
| `RedoAppliedEvent` | X | X | X | X | X | X |
| `RevisionApprovedEvent` | — | — | — | — | — | X |

All subscriptions follow the pattern: subscribe in constructor, unsubscribe in `Dispose()`.

---

## 6. Selection Synchronization Contract

Selection state originates in `EditorCanvasViewModel` and must propagate to:

1. **PropertyInspectorViewModel** — show properties of selected cabinet
2. **RunSummaryPanelViewModel** — activate the run containing the selected cabinet; highlight slot

**Mechanism:** `ShellViewModel` observes `EditorCanvasViewModel.PropertyChanged` for `SelectedCabinetIds`. On change, it notifies child VMs:

```
EditorCanvasViewModel.SelectedCabinetIds changes
    → ShellViewModel.OnCanvasPropertyChanged
        → PropertyInspectorViewModel.OnSelectionChanged(selectedIds)
        → RunSummaryPanelViewModel.OnSelectionChanged(selectedIds)
```

This is a **Presentation-only coordination pattern** — no application service or event bus needed.

---

## 7. DI Registration Contract

`PresentationServiceRegistration.AddPresentationServices()` must register:

| Registration | Lifetime | Status |
|---|---|---|
| `EditorSession` | Scoped | READY NOW |
| `EditorCanvas` | Scoped | READY NOW |
| `IEditorCanvasHost → EditorCanvasHost` | Scoped | READY NOW |
| `IEditorCanvasSession → EditorCanvasSessionAdapter` | Scoped | READY NOW |
| `IHitTester → DefaultHitTester` | Scoped | READY NOW |
| `ISceneProjector → SceneProjector` | Scoped | READY NOW |
| `EditorCanvasViewModel` | Scoped | READY NOW |
| `ShellViewModel` | Scoped | READY NOW |
| `CatalogPanelViewModel` | Scoped | NEEDS VIEWMODEL |
| `PropertyInspectorViewModel` | Scoped | NEEDS VIEWMODEL |
| `RunSummaryPanelViewModel` | Scoped | NEEDS VIEWMODEL |
| `IssuePanelViewModel` | Scoped | NEEDS VIEWMODEL |
| `StatusBarViewModel` | Scoped | NEEDS VIEWMODEL |

---

## 8. Implementation Readiness Summary

| Component | Status | Blocking Dependencies | Phase |
|---|---|---|---|
| Menu bar | READY NOW | None | Done |
| Toolbar | READY NOW | None | Done |
| Editor canvas (core) | READY NOW | None | Done |
| Canvas input (mouse move, wheel, keys) | NEEDS VIEWMODEL | Editor `BeginCatalogDrag`, viewport zoom | 2 |
| Catalog panel (static list) | NEEDS VIEWMODEL | None | 1 |
| Catalog panel (drag-drop) | NEEDS VIEWMODEL | Canvas input, Editor drag context | 2 |
| Run summary panel | NEEDS VIEWMODEL | `IRunService.GetRunSummary`, selection sync | 1 |
| Property inspector (read-only) | NEEDS VIEWMODEL | Run/cabinet data query (may need new service method) | 1 |
| Property inspector (editable) | NEEDS VIEWMODEL + NEEDS APP SERVICE SUPPORT | `IRunService.SetCabinetOverrideAsync`, property list DTO | 2 |
| Issue panel | NEEDS VIEWMODEL | `IValidationSummaryService` (verify impl exists) | 1 |
| Status bar | NEEDS VIEWMODEL + NEEDS XAML | `IValidationSummaryService`, `ProjectSummaryDto` | 1 |
| Revision history | NEEDS VIEWMODEL | `ISnapshotService` | LATER |
| DisplayFormatter | NEEDS IMPLEMENTATION | `ISettingsProvider` | LATER |
| Design-time data | NEEDS IMPLEMENTATION | All VMs | LATER |

### Phase 1 Build Order

1. `StatusBarViewModel` + status bar XAML (smallest, no selection dependency, validates event wiring pattern)
2. `IssuePanelViewModel` (independent of selection, uses `IValidationSummaryService`)
3. `RunSummaryPanelViewModel` (requires selection sync from canvas)
4. `PropertyInspectorViewModel` (requires selection sync + possibly new service method)
5. `CatalogPanelViewModel` (static list, no service dependency)
6. Wire all child VMs into `ShellViewModel` + update DI registration
7. Replace `MainWindow.xaml` placeholder panels with child VM `DataContext` bindings
