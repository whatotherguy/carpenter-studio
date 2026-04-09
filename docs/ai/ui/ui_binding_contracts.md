# UI Binding Contracts — Carpenter Studio

Date: 2026-04-08
Grounded in: current code, existing DTOs, existing Application service interfaces

This document is the authoritative contract for every ViewModel in the editor shell.
Implementation prompts must follow it exactly — no properties, commands, or
dependencies may be added without updating this document first.

---

## Conventions

- **Property type** — the C# observable property type exposed to XAML
- **Source** — where the value originates (DTO field, computed, service call, event)
- **CanExecute** — the predicate the command checks before allowing execution
- **Placeholder** — state shown when no project is open
- **Loading** — state shown while an async operation is in progress
- **Empty** — state shown when a project is open but the panel has no data to show
- **Error** — state shown when a command result or service call fails

Naming uses the code's actual property names, not the spec aliases.
All ViewModels implement `IDisposable` and unsubscribe from all events in `Dispose()`.

---

## 1. ShellViewModel

**Class:** `CabinetDesigner.Presentation.ViewModels.ShellViewModel`
**File:** `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
**DataContext of:** `MainWindow`

### 1.1 Bound Properties

| Property | Type | Source | Notes |
|---|---|---|---|
| `WindowTitle` | `string` | Computed from `ActiveProject` | `"Carpenter Studio"` when null; `"<Name> - Carpenter Studio"` when set. Current code uses space-dash-space — spec uses em-dash. Keep current until Phase 7 polish. |
| `HasActiveProject` | `bool` | `ActiveProject is not null` | Gates all project-scope commands. Bound to panel visibility in target layout. |
| `ActiveProject` | `ProjectSummaryDto?` | `IProjectService.CurrentProject`; refreshed on `ProjectOpenedEvent` / `ProjectClosedEvent` | Null when no project. |
| `PendingProjectName` | `string` | Two-way bound `TextBox` (toolbar) | Used as arg to `NewProjectCommand`. Scaffolding — will move to a dialog in Phase 7. |
| `PendingProjectFilePath` | `string` | Two-way bound `TextBox` (toolbar) | Used as arg to `OpenProjectCommand`. Scaffolding — will move to a dialog in Phase 7. |
| `Canvas` | `EditorCanvasViewModel` | Injected child VM | Bound via `ContentControl` on canvas region. |

**Child ViewModels (present):** `Canvas`
**Child ViewModels (target):** + `StatusBar`, `IssuePanel`, `RunSummary`, `RevisionHistory`, `Catalog`, `PropertyInspector`

### 1.2 Bound Commands

| Command | Type | CanExecute | Calls |
|---|---|---|---|
| `NewProjectCommand` | `AsyncRelayCommand` | `!string.IsNullOrWhiteSpace(PendingProjectName)` | `IProjectService.CreateProjectAsync(PendingProjectName)` |
| `OpenProjectCommand` | `AsyncRelayCommand` | `!string.IsNullOrWhiteSpace(PendingProjectFilePath)` | `IProjectService.OpenProjectAsync(PendingProjectFilePath)` |
| `SaveCommand` | `AsyncRelayCommand` | `HasActiveProject` | `IProjectService.SaveAsync()` |
| `CloseProjectCommand` | `AsyncRelayCommand` | `HasActiveProject` | `IProjectService.CloseAsync()` |
| `UndoCommand` | `RelayCommand` | `IUndoRedoService.CanUndo` | `IUndoRedoService.Undo()` |
| `RedoCommand` | `RelayCommand` | `IUndoRedoService.CanRedo` | `IUndoRedoService.Redo()` |

`UndoCommand` and `RedoCommand` call `NotifyCanExecuteChanged()` on `UndoAppliedEvent` and `RedoAppliedEvent`.

### 1.3 Dependencies

| Layer | Interface | Used For |
|---|---|---|
| Application | `IProjectService` | Project lifecycle |
| Application | `IUndoRedoService` | Undo/Redo state and execution |
| Application | `IApplicationEventBus` | Event subscriptions |

### 1.4 States

| State | Condition | `WindowTitle` | `HasActiveProject` | Commands enabled |
|---|---|---|---|---|
| **Placeholder** | No project open | `"Carpenter Studio"` | `false` | New, Open only |
| **Loading** | Async command in flight | unchanged | unchanged | All disabled (AsyncRelayCommand handles this) |
| **Active** | Project open | `"<Name> - Carpenter Studio"` | `true` | All enabled per CanExecute |

No empty or error state on ShellViewModel itself — errors surface via `CommandResultDto.Success == false` and update `Canvas.StatusMessage`.

### 1.5 Selection Synchronization

Not applicable — ShellViewModel does not participate in entity selection.

### 1.6 Service / DTO Sufficiency

**Sufficient today:**
- `IProjectService` — all needed methods exist
- `IUndoRedoService` — all needed members exist
- `ProjectSummaryDto` — all displayed fields present (`Name`, `HasUnsavedChanges`, `CurrentRevisionLabel`)

**Additions needed:** None for ShellViewModel itself.

---

## 2. EditorCanvasViewModel

**Class:** `CabinetDesigner.Presentation.ViewModels.EditorCanvasViewModel`
**File:** `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
**DataContext of:** Not direct — `ShellViewModel.Canvas` is bound via `ContentControl.Content` in `MainWindow`

### 2.1 Bound Properties

| Property | Type | Source | Notes |
|---|---|---|---|
| `CanvasView` | `object` | `IEditorCanvasHost.View` | WPF `FrameworkElement` wrapped as `object` to avoid WPF dependency in VM. Bound to `ContentControl.Content`. |
| `Scene` | `RenderSceneDto?` | `ISceneProjector.Project()` → `RenderSceneComposer.ApplyInteractionState(...)` — refreshed on `DesignChangedEvent`, `UndoAppliedEvent`, `RedoAppliedEvent`, `ProjectClosedEvent` | Null when no project. |
| `SelectedCabinetIds` | `IReadOnlyList<Guid>` | `IEditorCanvasSession.SelectedCabinetIds` — refreshed after every `OnMouseDown`, design event | `[]` when nothing selected or no project. |
| `HoveredCabinetId` | `Guid?` | `IEditorCanvasSession.HoveredCabinetId` — refreshed on `OnMouseMove` (once implemented) | `null` when no hover. |
| `CurrentMode` | `string` | `IEditorCanvasSession.CurrentMode.ToString()` | Updated on mouse events and `ProjectClosedEvent`. |
| `StatusMessage` | `string` | Set by each operation handler | Human-readable feedback. Bound in status bar (current) and will move to `StatusBarViewModel` (target). Default: `"Ready"`. |

### 2.2 Input Methods (called from code-behind only)

| Method | Signature | Action |
|---|---|---|
| `OnMouseDown` | `(double screenX, double screenY)` | Hit-test → update `_editorSession.SetSelectedCabinetIds(...)` → `RefreshInteractionState()` |
| `OnMouseMove` | `(double screenX, double screenY)` — **to add** | Forward to editor for hover + drag preview |
| `OnMouseUp` | `(double screenX, double screenY)` — **to add** | Commit drag or end operation |
| `OnMouseWheel` | `(double screenX, double screenY, int delta)` — **to add** | Forward to editor for zoom |
| `OnKeyDown` | `(Key key, ModifierKeys modifiers)` — **to add** | Escape → abort, Delete → remove selected |

`OnMouseDown` currently lacks `MouseButton` parameter — add it when implementing remaining input methods.

### 2.3 Async Design Methods (called by toolbar / future drag-drop)

| Method | Signature | Returns | Calls |
|---|---|---|---|
| `AddCabinetToRunAsync` | `(Guid runId, string cabinetTypeId, decimal nominalWidthInches)` | `CommandResultDto` | `IRunService.AddCabinetAsync(...)` |
| `MoveCabinetAsync` | `(Guid cabinetId, Guid sourceRunId, Guid targetRunId, int? targetIndex)` | `CommandResultDto` | `IRunService.MoveCabinetAsync(...)` |

### 2.4 Dependencies

| Layer | Interface | Used For |
|---|---|---|
| Application | `IRunService` | AddCabinet, MoveCabinet |
| Application | `IApplicationEventBus` | Design / undo / project events |
| Editor | `IEditorCanvasSession` | Selection reads and writes, mode, viewport, hover |
| Editor | `IHitTester` | Hit-test on `OnMouseDown` |
| Editor | `IEditorCanvasHost` | `View` exposure, `UpdateScene`, `UpdateViewport` |
| Rendering | `ISceneProjector` | Project design state → `RenderSceneDto` |

### 2.5 States

| State | Condition | `Scene` | `SelectedCabinetIds` | `StatusMessage` |
|---|---|---|---|---|
| **Placeholder** | No project | `null` | `[]` | `"Ready"` |
| **Active / idle** | Project open, nothing selected | populated | `[]` | `"Ready"` |
| **Selected** | Cabinet clicked | populated | `[id]` | `"Cabinet selected."` |
| **Post-command** | Command succeeded | refreshed | unchanged | `"Cabinet added."` / `"Cabinet moved."` |
| **Rejected** | Command failed | unchanged | unchanged | `"Cabinet add rejected."` |
| **Project closed** | `ProjectClosedEvent` | `null` | `[]` | `"Project closed."` |

### 2.6 Selection Synchronization

- `OnMouseDown` → `IHitTester.HitTest(...)` → `IEditorCanvasSession.SetSelectedCabinetIds(...)` → `RefreshInteractionState()` → `SelectedCabinetIds` notifies
- Other panels observe `SelectedCabinetIds` changes:
  - `RunSummaryPanelViewModel` highlights the matching slot
  - `PropertyInspectorViewModel` loads properties for selected IDs
- Reverse sync (slot click → canvas): not implemented in the current pass.

### 2.7 Service / DTO Sufficiency

**Sufficient today:** All listed services exist. `RenderSceneDto` and `RenderSceneComposer` exist.

**Additions needed:**
- `OnMouseMove`, `OnMouseUp`, `OnMouseWheel`, `OnKeyDown` input methods (Phase 1)
- `MouseButton` parameter on `OnMouseDown` (Phase 1)
- `SetSelectionExternal(Guid cabinetId)` method for reverse sync from RunSummary (Phase 3)

---

## 3. StatusBarViewModel

**Class:** `CabinetDesigner.Presentation.ViewModels.StatusBarViewModel`
**File:** `src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs` (to create)
**DataContext of:** Status bar region in `MainWindow` (bottom row)

### 3.1 Bound Properties

| Property | Type | Source | Placeholder | Notes |
|---|---|---|---|---|
| `ErrorCount` | `int` | `IValidationSummaryService.GetAllIssues()` filtered by `Severity == "Error"` | `0` | Refreshed on `ValidationIssuesChangedEvent` |
| `WarningCount` | `int` | same, `Severity == "Warning"` | `0` | |
| `InfoCount` | `int` | same, `Severity == "Info"` | `0` | |
| `HasManufactureBlockers` | `bool` | `IValidationSummaryService.HasManufactureBlockers` | `false` | Drives warning badge |
| `IssuesSummaryDisplay` | `string` | Computed: `$"{ErrorCount}E {WarningCount}W {InfoCount}I"` | `"—"` | Pure derived — no separate backing field |
| `RevisionLabel` | `string` | `ProjectSummaryDto.CurrentRevisionLabel` | `"—"` | Refreshed on `ProjectOpenedEvent`, `RevisionApprovedEvent` |
| `HasUnsavedChanges` | `bool` | `ProjectSummaryDto.HasUnsavedChanges` | `false` | Refreshed on `DesignChangedEvent`, `SaveCommand` completion |
| `SaveStateDisplay` | `string` | Computed: `HasUnsavedChanges ? "Unsaved changes" : "Saved"` | `"—"` | |
| `IsProjectOpen` | `bool` | `ActiveProject is not null` | `false` | Gates visibility of issue zone and revision zone |

### 3.2 Bound Commands

None. Status bar is read-only display.

### 3.3 Dependencies

| Layer | Interface | Used For |
|---|---|---|
| Application | `IValidationSummaryService` | Issue counts on refresh |
| Application | `IApplicationEventBus` | `ValidationIssuesChangedEvent`, `DesignChangedEvent`, `ProjectOpenedEvent`, `ProjectClosedEvent`, `RevisionApprovedEvent` |

### 3.4 States

| State | `IssuesSummaryDisplay` | `RevisionLabel` | `SaveStateDisplay` |
|---|---|---|---|
| **Placeholder** (no project) | `"—"` | `"—"` | `"—"` |
| **Active, clean** | `"0E 0W 0I"` | `ProjectSummaryDto.CurrentRevisionLabel` | `"Saved"` |
| **Active, dirty** | counts | label | `"Unsaved changes"` |
| **Manufacture blocked** | `"1E 0W 0I"` + badge | label | label |

No loading or error states — status bar always reflects last-known state.

### 3.5 Selection Synchronization

Not applicable.

### 3.6 Service / DTO Sufficiency

**Sufficient today:**
- `IValidationSummaryService.GetAllIssues()` — returns `IReadOnlyList<ValidationIssueSummaryDto>` with `Severity` string
- `IValidationSummaryService.HasManufactureBlockers` — exists
- `ProjectSummaryDto.CurrentRevisionLabel` — exists
- `ProjectSummaryDto.HasUnsavedChanges` — exists

**Additions needed:**
- `ValidationIssuesChangedEvent` — new event in `ApplicationEvents.cs`; must be published after the validation stage completes in the pipeline. Without this event, `StatusBarViewModel` can only refresh on `DesignChangedEvent`, which is correct but less granular.

---

## 4. IssuePanelViewModel

**Class:** `CabinetDesigner.Presentation.ViewModels.IssuePanelViewModel`
**File:** `src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs` (to create)
**DataContext of:** Issue Panel UserControl (bottom-right dock)

### 4.1 Bound Properties

| Property | Type | Source | Placeholder | Notes |
|---|---|---|---|---|
| `AllIssues` | `IReadOnlyList<IssueRowViewModel>` | `IValidationSummaryService.GetAllIssues()` mapped to row VMs | `[]` | Full unfiltered list |
| `FilteredIssues` | `IReadOnlyList<IssueRowViewModel>` | `AllIssues` filtered by `SeverityFilter` | `[]` | Bound to `ItemsControl`/`ListBox` |
| `SeverityFilter` | `string?` | Two-way bound radio/toggle buttons — `null`, `"Error"`, `"Warning"`, `"Info"` | `null` (show all) | Setting triggers refilter |
| `ErrorCount` | `int` | `AllIssues.Count(r => r.Severity == "Error")` | `0` | Tab badge |
| `WarningCount` | `int` | `AllIssues.Count(r => r.Severity == "Warning")` | `0` | |
| `InfoCount` | `int` | `AllIssues.Count(r => r.Severity == "Info")` | `0` | |
| `HasManufactureBlockers` | `bool` | `IValidationSummaryService.HasManufactureBlockers` | `false` | |
| `IsEmpty` | `bool` | `FilteredIssues.Count == 0` | `true` | Gates empty-state message |
| `IsProjectOpen` | `bool` | driven by `ProjectOpenedEvent` / `ProjectClosedEvent` | `false` | Gates placeholder text |

### 4.2 Bound Commands

| Command | Type | CanExecute | Action |
|---|---|---|---|
| `GoToEntityCommand` | `RelayCommand<IssueRowViewModel>` | `row.AffectedEntityIds.Count > 0` | Sets `EditorCanvasViewModel.SelectedCabinetIds` to the first affected entity ID via `SetSelectionExternal`. Does not navigate viewport (Phase 3 scope). |
| `SetSeverityFilterCommand` | `RelayCommand<string?>` | always | Sets `SeverityFilter`, recomputes `FilteredIssues` |

### 4.3 IssueRowViewModel Properties

| Property | Type | Source DTO field |
|---|---|---|
| `Severity` | `string` | `ValidationIssueSummaryDto.Severity` |
| `Code` | `string` | `ValidationIssueSummaryDto.Code` |
| `Message` | `string` | `ValidationIssueSummaryDto.Message` |
| `AffectedEntityIds` | `IReadOnlyList<string>` | `ValidationIssueSummaryDto.AffectedEntityIds ?? []` |
| `CanNavigate` | `bool` | `AffectedEntityIds.Count > 0` |

### 4.4 Dependencies

| Layer | Interface | Used For |
|---|---|---|
| Application | `IValidationSummaryService` | `GetAllIssues()`, `HasManufactureBlockers` |
| Application | `IApplicationEventBus` | `ValidationIssuesChangedEvent`, `ProjectOpenedEvent`, `ProjectClosedEvent` |
| Presentation | `EditorCanvasViewModel` | `SetSelectionExternal(Guid)` for GoTo action |

### 4.5 States

| State | `FilteredIssues` | `IsEmpty` | Panel display |
|---|---|---|---|
| **Placeholder** (no project) | `[]` | `true` | `"No project open"` |
| **Empty** (project open, no issues) | `[]` | `true` | `"No issues found"` |
| **Populated** | issue rows | `false` | issue list |
| **Filter active, no match** | `[]` | `true` | `"No issues match filter"` |

No loading state — `GetAllIssues()` is synchronous.

### 4.6 Selection Synchronization

- `GoToEntityCommand` → `EditorCanvasViewModel.SetSelectionExternal(Guid.Parse(affectedEntityIds[0]))` — selection flows canvas-ward only.
- Issue panel does not react to canvas selection changes (it shows all issues, not just those for selected entity — that is `PropertyInspector`'s job).

### 4.7 Service / DTO Sufficiency

**Sufficient today:**
- `IValidationSummaryService.GetAllIssues()` — returns full `ValidationIssueSummaryDto` list with all fields needed
- `ValidationIssueSummaryDto` — `Severity`, `Code`, `Message`, `AffectedEntityIds` all present

**Additions needed:**
- `ValidationIssuesChangedEvent` — same as StatusBar requirement
- `EditorCanvasViewModel.SetSelectionExternal(Guid)` — new method on EditorCanvasViewModel (Phase 1 / Phase 2)

---

## 5. RunSummaryPanelViewModel

**Class:** `CabinetDesigner.Presentation.ViewModels.RunSummaryPanelViewModel`
**File:** `src/CabinetDesigner.Presentation/ViewModels/RunSummaryPanelViewModel.cs`
**DataContext of:** Run Summary Panel UserControl (bottom-left dock)

### 5.1 Bound Properties

| Property | Type | Source | Placeholder | Notes |
|---|---|---|---|---|
| `IsProjectOpen` | `bool` | `ICurrentPersistedProjectState.CurrentState is not null` | `false` | Gates placeholder vs. live content |
| `HasActiveRun` | `bool` | `IRunSummaryService.GetCurrentSummary(...)` returns a run | `false` | Gates live run content |
| `HasSelection` | `bool` | `EditorCanvasViewModel.SelectedCabinetIds.Count > 0` | `false` | Mirrors canvas selection |
| `ActiveRunDisplay` | `string` | Computed from live run state | `"No active run selected"` | Current code uses a neutral label instead of a specific run id |
| `TotalWidthDisplay` | `string` | `RunSummaryDto.TotalNominalWidthInches` formatted as inches | `"-"` | e.g., `"144\""` |
| `CabinetCountDisplay` | `string` | `RunSummaryDto.CabinetCount` formatted as singular/plural | `"-"` | e.g., `"1 cabinet"`, `"2 cabinets"` |
| `SlotCountDisplay` | `string` | `RunSummaryDto.Slots.Count` formatted as singular/plural | `"0 slots"` | |
| `SelectionSummaryDisplay` | `string` | Selected cabinet count formatted for the shell badge | `"0 selected"` | |
| `StatusMessage` | `string` | Computed from project/selection/run state | `"Open a project to see the run summary."` | Live helper text shown under the summary strip |
| `SourceLabel` | `string` | Computed from project/run state | `"No project open"` | Also used in the panel badge and empty-state header |
| `EmptyStateText` | `string` | Computed from project/run state | `"Open a project to see the run summary."` | Honest placeholder/empty copy |
| `Slots` | `IReadOnlyList<RunSlotViewModel>` | `IRunSummaryService.GetCurrentSummary(...)` mapped to slot VMs | `[]` | Highlighted from canvas selection |

### 5.2 RunSlotViewModel Properties

| Property | Type | Source DTO field | Notes |
|---|---|---|---|
| `CabinetId` | `Guid` | `RunSlotSummaryDto.CabinetId` | Used for selection sync |
| `CabinetTypeId` | `string` | `RunSlotSummaryDto.CabinetTypeId` | Display label until catalog gives a human name |
| `WidthDisplay` | `string` | `RunSlotSummaryDto.NominalWidthInches` formatted as inches | e.g., `"36\""` |
| `Index` | `int` | `RunSlotSummaryDto.Index` | Slot order |
| `IsSelected` | `bool` | Matches any ID in `EditorCanvasViewModel.SelectedCabinetIds` | Updated on selection change |

### 5.3 Bound Commands

None. The panel is read-only and selection updates flow in from the canvas.

### 5.4 Dependencies

| Layer | Interface | Used For |
|---|---|---|
| Application | `IRunSummaryService` | Project-safe run summary projection for the current selection |
| Application | `ICurrentPersistedProjectState` | Project open/close state |
| Application | `IApplicationEventBus` | `DesignChangedEvent`, `ProjectOpenedEvent`, `ProjectClosedEvent`, `UndoAppliedEvent`, `RedoAppliedEvent` |
| Presentation | `EditorCanvasViewModel` | `SelectedCabinetIds` observation |

### 5.5 States

| State | `HasActiveRun` | `Slots` | Panel display |
|---|---|---|---|
| **Placeholder** (no project) | `false` | `[]` | `"No project open"` |
| **Empty** (project open, no runs) | `false` | `[]` | `"No runs in design"` |
| **Active** | `true` | slot rows | summary strip |
| **Selection-driven** | `true` | slot rows with matching highlight | badge and status text update from current selection |

### 5.6 Selection Synchronization

- Canvas → panel: `ShellViewModel` forwards `EditorCanvasViewModel.SelectedCabinetIds` into `RunSummaryPanelViewModel.OnSelectionChanged(...)`, which recomputes the highlight state for each slot.
- The panel does not push selection back into the canvas yet.

### 5.7 Service / DTO Sufficiency

**Sufficient today:**
- `IRunSummaryService.GetCurrentSummary(IReadOnlyList<Guid>)` — returns the active summary projection for the current selection and project state
- `RunSummaryDto` + `RunSlotSummaryDto` — all needed fields present for the panel
- `ICurrentPersistedProjectState` — project open/close detection already exists

**Additions needed:**
- A reverse selection hook if run-slot click-to-select is introduced later.
- A richer run identifier display if the UI later needs to expose the actual run id instead of a neutral "Active run" label.

---

## 6. RevisionHistoryViewModel

**Class:** `CabinetDesigner.Presentation.ViewModels.RevisionHistoryViewModel`
**File:** `src/CabinetDesigner.Presentation/ViewModels/RevisionHistoryViewModel.cs` (to create)
**DataContext of:** Revision History Panel UserControl (right dock, below Property Inspector — or tab)

### 6.1 Bound Properties

| Property | Type | Source | Placeholder | Notes |
|---|---|---|---|---|
| `Revisions` | `IReadOnlyList<RevisionRowViewModel>` | `ISnapshotService.GetRevisionHistory()` mapped | `[]` | Ordered newest-first |
| `SelectedRevision` | `RevisionRowViewModel?` | Two-way bound `ListBox.SelectedItem` | `null` | |
| `HasRevisions` | `bool` | `Revisions.Count > 0` | `false` | |
| `CanApprove` | `bool` | `HasActiveProject && !IsLoading` | `false` | Drives approve button enabled state |
| `IsLoading` | `bool` | `true` while `ApproveRevisionCommand` or `LoadSnapshotCommand` is in flight | `false` | |
| `IsProjectOpen` | `bool` | Driven by project events | `false` | |

### 6.2 RevisionRowViewModel Properties

| Property | Type | Source DTO field | Notes |
|---|---|---|---|
| `RevisionId` | `Guid` | `RevisionDto.RevisionId` | |
| `Label` | `string` | `RevisionDto.Label` | e.g., `"Draft v3"` |
| `CreatedAtDisplay` | `string` | `DisplayFormatter.FormatDate(RevisionDto.CreatedAt)` | e.g., `"Apr 8, 2026 10:34 AM"` |
| `ApprovalState` | `string` | `RevisionDto.ApprovalState` | Raw string from DTO |
| `IsApproved` | `bool` | `RevisionDto.IsApproved` | Drives approved badge |
| `IsLocked` | `bool` | `RevisionDto.IsLocked` | Drives lock icon |

### 6.3 Bound Commands

| Command | Type | CanExecute | Action |
|---|---|---|---|
| `LoadSnapshotCommand` | `AsyncRelayCommand<RevisionRowViewModel>` | `row != null && row.RevisionId != Guid.Empty` | `ISnapshotService.LoadSnapshotAsync(row.RevisionId)` → refreshes scene |
| `ApproveRevisionCommand` | `AsyncRelayCommand` | `CanApprove` | `ISnapshotService.ApproveRevisionAsync(label)` — label is current `RevisionLabel` from `ProjectSummaryDto` |

After `ApproveRevisionCommand` completes, call `ISnapshotService.GetRevisionHistory()` and rebuild `Revisions`.

### 6.4 Dependencies

| Layer | Interface | Used For |
|---|---|---|
| Application | `ISnapshotService` | `GetRevisionHistory()`, `ApproveRevisionAsync`, `LoadSnapshotAsync` |
| Application | `IApplicationEventBus` | `RevisionApprovedEvent`, `ProjectOpenedEvent`, `ProjectClosedEvent` |

### 6.5 States

| State | `Revisions` | `CanApprove` | Panel display |
|---|---|---|---|
| **Placeholder** (no project) | `[]` | `false` | `"No project open"` |
| **Empty** (project open, no revisions yet) | `[]` | `true` | `"No revisions yet"` + Approve button |
| **Populated** | revision rows | `true` | list + Approve button |
| **Loading** | unchanged | `false` | progress indicator on button |

### 6.6 Selection Synchronization

Not applicable — revision history does not interact with canvas selection.

### 6.7 Service / DTO Sufficiency

**Sufficient today:**
- `ISnapshotService` — all three needed methods exist
- `RevisionDto` — all needed fields present (`RevisionId`, `Label`, `CreatedAt`, `ApprovalState`, `IsApproved`, `IsLocked`)

**Additions needed:**
- `DisplayFormatter.FormatDate(DateTimeOffset)` — for `CreatedAtDisplay` (Phase 1 infrastructure task)
- `ApproveRevisionCommand` needs a label string. Options: hard-code `"Approved"`, or derive from current `ProjectSummaryDto.CurrentRevisionLabel`. Keep simple — use `"Approved"` as default, make it a VM parameter later.

---

## 7. CatalogPanelViewModel

**Class:** `CabinetDesigner.Presentation.ViewModels.CatalogPanelViewModel`
**File:** `src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs` (to create)
**DataContext of:** Catalog Panel UserControl (left dock)

### 7.1 Bound Properties

| Property | Type | Source | Placeholder | Notes |
|---|---|---|---|---|
| `AllItems` | `IReadOnlyList<CatalogItemViewModel>` | `ICatalogService.GetAllItemsAsync()` — loaded on `ProjectOpenedEvent` | `[]` | Full catalog, static within session |
| `FilteredItems` | `IReadOnlyList<CatalogItemViewModel>` | `AllItems` filtered by `SearchText` and `SelectedCategory` | `[]` | Bound to `ListBox` |
| `SearchText` | `string` | Two-way bound `TextBox` | `""` | Setting triggers refilter |
| `SelectedCategory` | `string?` | Two-way bound category picker — `null` = all | `null` | Setting triggers refilter |
| `Categories` | `IReadOnlyList<string>` | Distinct `CatalogItemViewModel.Category` values from `AllItems` | `[]` | Drives category picker |
| `IsEmpty` | `bool` | `FilteredItems.Count == 0` | `true` | |
| `IsLoading` | `bool` | `true` while catalog loads on project open | `false` | |
| `IsProjectOpen` | `bool` | Driven by project events | `false` | |

### 7.2 CatalogItemViewModel Properties

| Property | Type | Source DTO field | Notes |
|---|---|---|---|
| `TypeId` | `string` | `CatalogItemDto.TypeId` | Passed to `AddCabinetToRunAsync` |
| `DisplayName` | `string` | `CatalogItemDto.DisplayName` | e.g., `"Base Cabinet"` |
| `Category` | `string` | `CatalogItemDto.Category` | e.g., `"Base"`, `"Wall"`, `"Tall"` |
| `Description` | `string` | `CatalogItemDto.Description` | Short description |
| `DefaultWidthDisplay` | `string` | `DisplayFormatter.FormatInches(CatalogItemDto.DefaultNominalWidthInches)` | e.g., `"36\""` |

### 7.3 Bound Commands / Methods

| Command / Method | Type | CanExecute / When | Action |
|---|---|---|---|
| `ClearSearchCommand` | `RelayCommand` | `!string.IsNullOrEmpty(SearchText)` | Clears `SearchText` |
| `BeginDrag(item)` | method (called from code-behind) | `IsProjectOpen && item != null` | Packages `item.TypeId` into drag data object; code-behind calls `DragDrop.DoDragDrop(...)`. No ViewModel state change. |

Drag-drop completion (drop on canvas) triggers `EditorCanvasViewModel.AddCabinetToRunAsync(runId, typeId, nominalWidth)`. The canvas code-behind handles the `Drop` event and calls this method. The `runId` comes from the `PreviewResultDto.Candidates[0].RunId` surfaced during the drag preview phase (future work).

### 7.4 Dependencies

| Layer | Interface | Used For |
|---|---|---|
| Application | `ICatalogService` (**new**) | `GetAllItemsAsync()` |
| Application | `IApplicationEventBus` | `ProjectOpenedEvent`, `ProjectClosedEvent` |

### 7.5 States

| State | `IsLoading` | `FilteredItems` | Panel display |
|---|---|---|---|
| **Placeholder** (no project) | `false` | `[]` | `"Open a project to browse the catalog"` |
| **Loading** (project just opened) | `true` | `[]` | spinner |
| **Empty** (no items match filter) | `false` | `[]` | `"No items match your search"` |
| **Populated** | `false` | item list | catalog grid |

### 7.6 Selection Synchronization

Not applicable. Catalog panel is a drag source only — it does not observe or affect canvas selection.

### 7.7 Service / DTO Sufficiency

**Additions needed (this panel drives the only new Application service):**
- `ICatalogService` — new interface in `src/CabinetDesigner.Application/Services/ICatalogService.cs`
  - `Task<IReadOnlyList<CatalogItemDto>> GetAllItemsAsync(CancellationToken ct = default)`
- `CatalogItemDto` — new DTO in `src/CabinetDesigner.Application/DTOs/CatalogItemDto.cs`
  - Fields: `TypeId (string)`, `DisplayName (string)`, `Category (string)`, `Description (string)`, `DefaultNominalWidthInches (decimal)`
- `CatalogService` — implementation that reads from domain's cabinet template registry (Phase 5)

---

## 8. PropertyInspectorViewModel

**Class:** `CabinetDesigner.Presentation.ViewModels.PropertyInspectorViewModel`
**File:** `src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs`
**DataContext of:** Property Inspector Panel UserControl (right dock)

### 8.1 Bound Properties

| Property | Type | Source | Placeholder | Notes |
|---|---|---|---|---|
| `IsProjectOpen` | `bool` | Driven by project events | `false` | Gates project placeholder vs. live selection copy |
| `HasSelection` | `bool` | `SelectedCabinetIds.Count > 0` | `false` | Gates panel content |
| `HasSingleSelection` | `bool` | `SelectedCabinetIds.Count == 1` | `false` | Unlocks the nominal-width editor card |
| `SelectedEntityLabel` | `string` | Selected cabinet render DTO label or fallback text | `"No cabinet selected"` | e.g., `"Base Cabinet 36\" (abcd1234)"` |
| `SelectionSummaryDisplay` | `string` | Derived from selection count | `"Nothing selected"` | Badge copy |
| `Properties` | `IReadOnlyList<PropertyRowViewModel>` | Built from projected cabinet scene data | `[]` | Read-only rows for the selected cabinet |
| `StatusMessage` | `string` | Computed from project/selection/edit state | `"Open a project to inspect properties."` | Helper copy under the summary badges |
| `SourceLabel` | `string` | Computed from project/selection/edit state | `"No project open"` | Badge copy |
| `EditabilityStatusDisplay` | `string` | Computed from edit state | `"No editable properties"` | Second badge in the header |
| `EmptyStateText` | `string` | Computed from project/selection/edit state | `"Open a project to inspect properties."` | Honest empty-state copy |
| `PropertySummaryDisplay` | `string` | Computed from `Properties.Count` when single-selected | `"0 details"` | Used by the shell tests and the panel summary text |
| `NominalWidthDisplay` | `string` | Current cabinet width formatted as inches | `"-"` | Shown in the width editor card |
| `NominalWidthEditValue` | `string` | Two-way bound edit text box | `""` | Raw numeric inches, no quote suffix |
| `IsEditingNominalWidth` | `bool` | Set by edit commands | `false` | Switches the width card between view and edit modes |
| `CanEditNominalWidth` | `bool` | Single selection with a projected cabinet | `false` | Drives edit button availability |
| `HasError` | `bool` | `LastErrorMessage is not null` | `false` | Shows the inline error text if a resize is rejected |
| `LastErrorMessage` | `string?` | Validation or command failure text | `null` | Inline error copy |

### 8.2 PropertyRowViewModel Properties

| Property | Type | Source DTO field | Notes |
|---|---|---|---|
| `Key` | `string` | Hard-coded view model key | e.g., `"cabinet-id"` |
| `DisplayName` | `string` | Hard-coded label | e.g., `"Cabinet Label"` |
| `DisplayValue` | `string` | Projected cabinet data or selection summary | e.g., `"Base Cabinet 36\""` |
| `IsEditable` | `bool` | View model sets this only for rows that represent editable state | Currently only the editability row is shown as editable-capable, while the actual edit flow lives in the width card |
| `IsPlaceholder` | `bool` | Placeholder flag for future styling | `false` |

### 8.3 Methods Called by Shell/Canvas

| Method | When | Action |
|---|---|---|
| `OnSelectionChanged(IReadOnlyList<Guid> selectedIds, RenderSceneDto? scene = null)` | Called by `ShellViewModel` when `EditorCanvasViewModel.SelectedCabinetIds` changes | Rebuilds the read-only rows and width editor state from the projected scene |

### 8.4 Dependencies

| Layer | Interface | Used For |
|---|---|---|
| Application | `IRunService` | `ResizeCabinetAsync` for the nominal-width edit flow |
| Application | `IApplicationEventBus` | `ProjectOpenedEvent`, `ProjectClosedEvent` |
| Presentation | `RenderSceneDto` | Selected cabinet projection and width display |

### 8.5 States

| State | `HasSelection` | `Properties` | Panel display |
|---|---|---|---|
| **Placeholder** (no project) | `false` | `[]` | `"No project open"` |
| **Empty** (project open, nothing selected) | `false` | `[]` | `"No cabinet selected. Click a cabinet on the canvas to inspect it."` |
| **Single select** | `true` | cabinet rows plus editable width card | full property grid and width editor |
| **Multi-select** | `true` | reduced rows (selection summary only) | `"Multiple selection is not yet expanded in the property inspector."` |

### 8.6 Selection Synchronization

- `EditorCanvasViewModel.SelectedCabinetIds` → `ShellViewModel` observes the property change → calls `PropertyInspectorViewModel.OnSelectionChanged(ids, scene)`.
- The property inspector remains receive-only for selection. It does not write back to canvas selection.

### 8.7 Service / DTO Sufficiency

**Already sufficient:**
- `IRunService.ResizeCabinetAsync(ResizeCabinetRequestDto)` — exists and is the first supported edit path now exposed in the inspector
- `RenderSceneDto` + `CabinetRenderDto` — provide the current selected cabinet label and width
- `IApplicationEventBus` project events — drive project open/close reset behavior

**Additions needed:**
- A richer property-editing service only if more editable fields are introduced later

---

## 9. Cross-Cutting Binding Rules

### 9.1 Placeholder Enforcement

Every panel VM exposes `IsProjectOpen : bool`. XAML binds panel content visibility to this flag. The placeholder text is a `TextBlock` in the same region, visible when `!IsProjectOpen`. No service calls are made when `IsProjectOpen == false`.

### 9.2 Loading State

Any VM that performs async work exposes `IsLoading : bool`. The XAML `DataTrigger` overlays a spinner or disables inputs while `IsLoading == true`. `AsyncRelayCommand` automatically prevents re-entry; `IsLoading` is a complementary display property only.

### 9.3 Empty State Text

| Panel | Empty text | Condition |
|---|---|---|
| Catalog | `"No items match your search"` | `FilteredItems.Count == 0 && IsProjectOpen` |
| Catalog (no project) | `"Open a project to browse the catalog"` | `!IsProjectOpen` |
| Issue Panel | `"No issues found"` | `FilteredIssues.Count == 0 && IsProjectOpen` |
| Run Summary | `"No runs in design"` | `!HasActiveRun && IsProjectOpen` |
| Revision History | `"No revisions yet"` | `!HasRevisions && IsProjectOpen` |
| Property Inspector | `"Select a cabinet to inspect"` | `!HasSelection && IsProjectOpen` |

### 9.4 Error State

Command failures surface via `CommandResultDto.Success == false`. The calling VM sets a `LastErrorMessage : string?` property and `HasError : bool`. XAML binds a `Border` visibility to `HasError`. The error clears on the next successful command or on `ProjectClosedEvent`.

`StatusBar` does not display error state — errors surface on the individual panel that issued the failed command.

### 9.5 Selection Sync Topology

```
EditorCanvasViewModel.SelectedCabinetIds
        │ (PropertyChanged)
        ├──▶ ShellViewModel selection fan-out
        │         ├──▶ PropertyInspectorViewModel.OnSelectionChanged(ids, scene)
        │         └──▶ RunSummaryPanelViewModel.OnSelectionChanged(ids) → updates IsSelected on slots
        │
        └──▶ IssuePanelViewModel (does NOT filter by selection — shows all issues)

IssuePanelViewModel.GoToEntityCommand
        └──▶ EditorCanvasViewModel.SetSelectionExternal(cabinetId)
                └──▶ (same as above)
```

`SetSelectionExternal` must guard against loops: if the incoming ID is already the only selected ID, return early without calling `RefreshInteractionState()`.

### 9.6 DisplayFormatter Usage

All dimensional values pass through `DisplayFormatter` before becoming bound `string` properties. `DisplayFormatter` is injected into any VM that formats lengths or dates. It reads display preferences (unit system, fractional vs. decimal) from `ISettingsProvider` (Phase 7) — until then, defaults to fractional inches.

| VM | Formatted values |
|---|---|
| `RunSummaryPanelViewModel` | `TotalWidthDisplay`, `RunSlotViewModel.WidthDisplay` |
| `CatalogPanelViewModel` | `CatalogItemViewModel.DefaultWidthDisplay` |
| `PropertyInspectorViewModel` | `PropertyRowViewModel.DisplayValue`, `NominalWidthDisplay`, `NominalWidthEditValue` |
| `RevisionHistoryViewModel` | `RevisionRowViewModel.CreatedAtDisplay` |

---

## 10. Application Additions Required (Summary)

| Addition | Type | Needed By | Priority |
|---|---|---|---|
| `ValidationIssuesChangedEvent` in `ApplicationEvents.cs` | Event | `StatusBarViewModel`, `IssuePanelViewModel` | Next (Phase 2) |
| Publish `ValidationIssuesChangedEvent` from validation pipeline stage | Pipeline change | Same | Next (Phase 2) |
| `IRunService.GetRunIdForCabinet(Guid cabinetId) : Guid?` | Service method | `RunSummaryPanelViewModel` | Next (Phase 3) |
| `ICatalogService` + `CatalogService` | Service + impl | `CatalogPanelViewModel` | Later (Phase 5) |
| `CatalogItemDto` | DTO | `CatalogPanelViewModel` | Later (Phase 5) |
| `PropertyInspectorViewModel` width editor | Presentation | `IRunService.ResizeCabinetAsync` | Now |

## 11. Presentation Additions Required (Summary)

| Addition | Needed By | Priority |
|---|---|---|
| `EditorCanvasViewModel.SetSelectionExternal(Guid)` | `IssuePanelViewModel`, `RunSummaryPanelViewModel` | Next (Phase 2) |
| `EditorCanvasViewModel.OnMouseMove/Up/Wheel/KeyDown` input methods | Drag, hover, zoom, keyboard | Next (Phase 1) |
| `ShellViewModel` selection fan-out to child VMs | `PropertyInspectorViewModel`, `RunSummaryPanelViewModel` | Now |
| `DisplayFormatter` utility class | `RunSummaryPanelViewModel`, `RevisionHistoryViewModel`, `CatalogPanelViewModel`, `PropertyInspectorViewModel` | Next (Phase 1) |
| `PresentationServiceRegistration` wired into `App.xaml.cs` | All new VMs reach DI | Next (Phase 1) |
