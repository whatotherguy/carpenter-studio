# ViewModel Plan — Phase 1 Panel ViewModels

Date: 2026-04-08
Basis: `ui_inventory_and_contracts.md`, `ui_architecture_reconciliation.md`, `outputs/presentation.md`, current codebase

---

## 0. Service Implementation Reality Check

Before classifying ViewModels, the actual implementation state of the services they depend on must be confirmed:

| Service | Interface | Implemented? | Notes |
|---|---|---|---|
| `IApplicationEventBus` | Yes | **Yes** — `ApplicationEventBus.cs` is complete | Safe to subscribe |
| `IProjectService` | Yes | **Yes** — `ProjectService.cs` is complete | `CurrentProject`, events work |
| `IUndoRedoService` | Yes | **Yes** — `UndoRedoService.cs` is complete | `CanUndo`, `CanRedo` work |
| `IValidationSummaryService` | Yes | **No** — all methods throw `NotImplementedException` | `GetAllIssues()`, `GetIssuesFor()`, `HasManufactureBlockers` all throw |
| `IRunService.GetRunSummary` | Yes | **No** — throws `NotImplementedException` | Cannot read run slot data |
| `IRunService.FindRunContaining` | No | **Not on interface** — method does not exist | Cannot resolve cabinet → run |
| `IRunService.SetCabinetOverrideAsync` | Yes | **No** — throws `NotImplementedException` | Cannot write property overrides |

This changes the Phase 1 classification materially. ViewModels depending on unimplemented services must be structured as **placeholder adapters** — they define the full binding surface but guard against service calls.

---

## 1. Classification Summary

| ViewModel | Classification | Blocking Dependency |
|---|---|---|
| `CatalogPanelViewModel` | **Production-ready now** | None |
| `StatusBarViewModel` | **Placeholder adapter now** | `IValidationSummaryService` (guard; partial data available) |
| `IssuePanelViewModel` | **Placeholder adapter now** | `IValidationSummaryService` (all data blocked) |
| `RunSummaryPanelViewModel` | **Placeholder adapter now** | `IRunService.GetRunSummary` + `FindRunContaining` |
| `PropertyInspectorViewModel` | **Placeholder adapter now** | `IRunService.GetRunSummary` (slot data path) |

---

## 2. ViewModel Specifications

---

### 2.1 CatalogPanelViewModel

**Classification: PRODUCTION-READY NOW**

| Field | Value |
|---|---|
| **Filename** | `src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs` |
| **Namespace** | `CabinetDesigner.Presentation.ViewModels` |
| **Owned by** | `ShellViewModel.Catalog` |
| **Responsibilities** | Expose a static list of cabinet types grouped by category; filter by `SearchText`; support click-to-add in Phase 1 (no drag-drop yet) |

**Constructor dependencies:**

```csharp
public CatalogPanelViewModel()
// No service dependencies for static MVP list.
```

**Properties:**

| Property | Type | Notes |
|---|---|---|
| `AllItems` | `IReadOnlyList<CatalogItemViewModel>` | Hardcoded; set in constructor |
| `FilteredItems` | `IReadOnlyList<CatalogItemViewModel>` | Recomputed on `SearchText` change |
| `SearchText` | `string` (two-way) | Empty string = show all |

**Commands:** None in Phase 1. Phase 2 adds a `BeginDragCommand`.

**Events subscribed to:** None.

**Hardcoded catalog (Phase 1):**

```
Base-24  | Base Cabinet 24"  | Base
Base-30  | Base Cabinet 30"  | Base
Base-36  | Base Cabinet 36"  | Base
Base-42  | Base Cabinet 42"  | Base
Wall-30  | Wall Cabinet 30"  | Wall
Wall-36  | Wall Cabinet 36"  | Wall
Tall-84  | Tall Cabinet 84"  | Tall
Tall-96  | Tall Cabinet 96"  | Tall
```

**`CatalogItemViewModel` (nested class or separate file):**

```csharp
public sealed class CatalogItemViewModel
{
    public string CabinetTypeId { get; init; }
    public string DisplayName { get; init; }
    public string Category { get; init; }
    public string DefaultWidthDisplay { get; init; }
}
```

No `INotifyPropertyChanged` needed — items are immutable.

**Tests required:**

| Test | Description |
|---|---|
| `AllItems_ContainsExpectedTypes` | Constructor populates 8 items |
| `FilteredItems_ReturnsAll_WhenSearchEmpty` | Empty search shows all |
| `FilteredItems_FiltersCorrectly_WhenSearchMatches` | "Base" returns only base items |
| `FilteredItems_ReturnsEmpty_WhenNoMatch` | Non-matching search yields empty list |
| `SearchText_RaisesPropertyChanged` | Setting SearchText fires INPC for both SearchText and FilteredItems |

---

### 2.2 StatusBarViewModel

**Classification: PLACEHOLDER ADAPTER NOW**

`IValidationSummaryService` is not implemented. Validation count properties (`ErrorCount`, `WarningCount`, `InfoCount`, `HasManufactureBlockers`) default to 0/false and are not updated until the service is implemented. `RevisionLabel` and `SaveStateDisplay` are fully functional via `ProjectOpenedEvent` data.

| Field | Value |
|---|---|
| **Filename** | `src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs` |
| **Namespace** | `CabinetDesigner.Presentation.ViewModels` |
| **Owned by** | `ShellViewModel.StatusBar` |
| **Responsibilities** | Expose validation counts, revision label, save state; subscribe to project and design lifecycle events |

**Constructor dependencies:**

```csharp
public StatusBarViewModel(
    IApplicationEventBus eventBus,
    IValidationSummaryService validationSummaryService)
```

**Properties:**

| Property | Type | Live in Phase 1? | Source |
|---|---|---|---|
| `ErrorCount` | `int` | No (placeholder 0) | `IValidationSummaryService.GetAllIssues()` — blocked |
| `WarningCount` | `int` | No (placeholder 0) | Same — blocked |
| `InfoCount` | `int` | No (placeholder 0) | Same — blocked |
| `HasManufactureBlockers` | `bool` | No (placeholder false) | `IValidationSummaryService.HasManufactureBlockers` — blocked |
| `RevisionLabel` | `string` | **Yes** | `ProjectSummaryDto.CurrentRevisionLabel` from `ProjectOpenedEvent` |
| `HasUnsavedChanges` | `bool` | **Yes** | `ProjectSummaryDto.HasUnsavedChanges` from `ProjectOpenedEvent` |
| `SaveStateDisplay` | `string` | **Yes** | `"Saved"` / `"Unsaved changes"` derived from `HasUnsavedChanges` |
| `StatusMessage` | `string` | **Yes** | Forwarded from `EditorCanvasViewModel.StatusMessage` via `ShellViewModel` property push |

**Commands:** None.

**Events subscribed to:**

| Event | Action |
|---|---|
| `ProjectOpenedEvent` | Set `RevisionLabel` and `HasUnsavedChanges` from `event.Project` |
| `ProjectClosedEvent` | Clear all — reset to defaults |
| `DesignChangedEvent` | Attempt to refresh validation counts (no-op until service is implemented) |
| `UndoAppliedEvent` | Same as `DesignChangedEvent` |
| `RedoAppliedEvent` | Same as `DesignChangedEvent` |

**Placeholder guard pattern:**

```csharp
private void RefreshValidationCounts()
{
    // IValidationSummaryService is not yet implemented.
    // Guard prevents NotImplementedException from crashing the VM.
    // Remove try/catch once the service is implemented.
    try
    {
        var issues = _validationSummaryService.GetAllIssues();
        ErrorCount = issues.Count(i => i.Severity == "Error");
        WarningCount = issues.Count(i => i.Severity == "Warning");
        InfoCount = issues.Count(i => i.Severity == "Info");
        HasManufactureBlockers = _validationSummaryService.HasManufactureBlockers;
    }
    catch (NotImplementedException) { /* placeholder — counts stay at 0 */ }
}
```

This guard must be removed once `ValidationSummaryService` is implemented.

**Tests required:**

| Test | Description |
|---|---|
| `ProjectOpened_SetsRevisionLabel` | `ProjectOpenedEvent` with known `CurrentRevisionLabel` → property matches |
| `ProjectOpened_SetsUnsavedState` | `HasUnsavedChanges = true` → `SaveStateDisplay` = `"Unsaved changes"` |
| `ProjectOpened_Saved_SetsDisplay` | `HasUnsavedChanges = false` → `SaveStateDisplay` = `"Saved"` |
| `ProjectClosed_ResetsAll` | After `ProjectClosedEvent`, all properties return to defaults |
| `Dispose_UnsubscribesFromEvents` | Post-dispose event publish triggers no property change |

---

### 2.3 IssuePanelViewModel

**Classification: PLACEHOLDER ADAPTER NOW**

`IValidationSummaryService` is not implemented. The ViewModel exposes the full binding surface but `AllIssues` and `FilteredIssues` remain empty and counts remain 0 until the service is implemented.

| Field | Value |
|---|---|
| **Filename** | `src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs` |
| **Namespace** | `CabinetDesigner.Presentation.ViewModels` |
| **Owned by** | `ShellViewModel.IssuePanel` |
| **Responsibilities** | Expose project-wide validation issues; severity counts; navigate canvas to affected entity |

**Constructor dependencies:**

```csharp
public IssuePanelViewModel(
    IValidationSummaryService validationSummaryService,
    IApplicationEventBus eventBus)
```

`ShellViewModel` must inject a `SelectEntities` delegate or callback to support `GoToEntityCommand` without `IssuePanelViewModel` taking a direct reference to `EditorCanvasViewModel`. See §4 for the coordination pattern.

**Properties:**

| Property | Type | Live in Phase 1? | Source |
|---|---|---|---|
| `AllIssues` | `IReadOnlyList<IssueRowViewModel>` | No (empty) | `IValidationSummaryService.GetAllIssues()` — blocked |
| `FilteredIssues` | `IReadOnlyList<IssueRowViewModel>` | No (empty) | Phase 1: same as `AllIssues`; no filter UI yet |
| `ErrorCount` | `int` | No (placeholder 0) | Computed from `AllIssues` |
| `WarningCount` | `int` | No (placeholder 0) | Computed from `AllIssues` |
| `InfoCount` | `int` | No (placeholder 0) | Computed from `AllIssues` |
| `HasManufactureBlockers` | `bool` | No (placeholder false) | `IValidationSummaryService.HasManufactureBlockers` — blocked |
| `SeverityFilter` | `string?` | No (null = all) | Phase 2 only |

**Commands:**

| Command | Type | Phase |
|---|---|---|
| `GoToEntityCommand` | `RelayCommand<IssueRowViewModel>` | Phase 1 (wired but no-ops until issues exist) |

**Events subscribed to:**

| Event | Action |
|---|---|
| `DesignChangedEvent` | Attempt to refresh all issues (guarded) |
| `UndoAppliedEvent` | Same |
| `RedoAppliedEvent` | Same |
| `ProjectClosedEvent` | Clear all issues |

**Placeholder guard pattern:** Same `try/catch (NotImplementedException)` as `StatusBarViewModel.RefreshValidationCounts()`.

**`IssueRowViewModel` (nested class or separate file):**

```csharp
public sealed class IssueRowViewModel
{
    public string Severity { get; init; }
    public string Code { get; init; }
    public string Message { get; init; }
    public IReadOnlyList<string> AffectedEntityIds { get; init; }
}
```

No INPC needed — rows are rebuilt on refresh.

**Tests required:**

| Test | Description |
|---|---|
| `ProjectClosed_ClearsIssues` | After `ProjectClosedEvent`, `AllIssues` is empty |
| `GoToEntity_WithNoIssues_CanExecuteIsFalse` | No issues → command cannot execute |
| `Dispose_UnsubscribesFromEvents` | Post-dispose events do not trigger refresh |

Note: Refresh-from-service tests cannot be written until `IValidationSummaryService` is implemented. Add them at that point.

---

### 2.4 RunSummaryPanelViewModel

**Classification: PLACEHOLDER ADAPTER NOW**

`IRunService.GetRunSummary` is not implemented. Additionally, `IRunService.FindRunContaining(Guid cabinetId)` does not exist on the interface. The ViewModel exposes the full binding surface with empty/default data. Selection change calls are accepted but result in no data.

| Field | Value |
|---|---|
| **Filename** | `src/CabinetDesigner.Presentation/ViewModels/RunSummaryPanelViewModel.cs` |
| **Namespace** | `CabinetDesigner.Presentation.ViewModels` |
| **Owned by** | `ShellViewModel.RunSummary` |
| **Responsibilities** | Display active run slot list, total width, cabinet count; sync slot selection to canvas |

**Constructor dependencies:**

```csharp
public RunSummaryPanelViewModel(
    IRunService runService,
    IApplicationEventBus eventBus)
```

**Public methods (called by ShellViewModel):**

```csharp
// Called by ShellViewModel when EditorCanvasViewModel.SelectedCabinetIds changes.
public void OnSelectionChanged(IReadOnlyList<Guid> selectedCabinetIds);
```

**Properties:**

| Property | Type | Live in Phase 1? | Source |
|---|---|---|---|
| `HasActiveRun` | `bool` | No (false) | Derived: `ActiveRun is not null` |
| `ActiveRunId` | `Guid?` | No (null) | Resolved via `FindRunContaining` — not on interface yet |
| `TotalWidthDisplay` | `string` | No (`"—"`) | `RunSummaryDto.TotalNominalWidthInches` — blocked |
| `CabinetCountDisplay` | `string` | No (`"—"`) | `RunSummaryDto.CabinetCount` — blocked |
| `Slots` | `IReadOnlyList<RunSlotViewModel>` | No (empty) | `RunSummaryDto.Slots` — blocked |

**Commands:**

| Command | Type | Phase |
|---|---|---|
| `SelectSlotCommand` | `RelayCommand<RunSlotViewModel>` | Phase 1 (wired; routes through `ShellViewModel.SelectEntities`) |

**Events subscribed to:**

| Event | Action |
|---|---|
| `DesignChangedEvent` | If `HasActiveRun`, attempt to refresh summary (guarded) |
| `UndoAppliedEvent` | Same |
| `RedoAppliedEvent` | Same |
| `ProjectClosedEvent` | Clear all — `HasActiveRun = false`, `Slots = []` |

**App service gap (must be resolved before this VM goes live):**

Add to `IRunService`:
```csharp
Guid? FindRunContaining(Guid cabinetId);
```
Implementation in `RunService` delegates to `IDesignStateStore.GetAllRuns()` scan. Until this exists, `OnSelectionChanged` is a no-op.

**`RunSlotViewModel` (nested class or separate file):**

```csharp
public sealed class RunSlotViewModel
{
    public Guid CabinetId { get; init; }
    public string CabinetTypeId { get; init; }
    public string WidthDisplay { get; init; }  // e.g. "36\""
    public int Index { get; init; }
    public bool IsSelected { get; set; }        // updated by selection sync
}
```

**Tests required:**

| Test | Description |
|---|---|
| `ProjectClosed_ClearsState` | `HasActiveRun = false`, `Slots` empty after `ProjectClosedEvent` |
| `OnSelectionChanged_WithNoRun_DoesNothing` | No-op without `FindRunContaining` on interface |
| `Dispose_UnsubscribesFromEvents` | Post-dispose events do not trigger refresh |

Note: Full selection + slot tests require `IRunService.FindRunContaining` and `GetRunSummary` to be implemented.

---

### 2.5 PropertyInspectorViewModel

**Classification: PLACEHOLDER ADAPTER NOW**

`IRunService.GetRunSummary` is not implemented, so cabinet slot data (type ID, width, depth, slot index) cannot be read. The ViewModel accepts selection but shows "No data available" until the service is implemented.

| Field | Value |
|---|---|
| **Filename** | `src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs` |
| **Namespace** | `CabinetDesigner.Presentation.ViewModels` |
| **Owned by** | `ShellViewModel.PropertyInspector` |
| **Responsibilities** | Display read-only properties of the selected cabinet; show associated validation issues (Phase 1: all guarded until services are live) |

**Constructor dependencies:**

```csharp
public PropertyInspectorViewModel(
    IRunService runService,
    IValidationSummaryService validationSummaryService,
    IApplicationEventBus eventBus)
```

**Public methods (called by ShellViewModel):**

```csharp
public void OnSelectionChanged(IReadOnlyList<Guid> selectedCabinetIds);
```

**Properties:**

| Property | Type | Live in Phase 1? | Source |
|---|---|---|---|
| `HasSelection` | `bool` | **Yes** | `selectedCabinetIds.Count > 0` |
| `SelectedEntityLabel` | `string?` | No (`null`) | `RunSlotSummaryDto.CabinetTypeId` — blocked |
| `Properties` | `IReadOnlyList<PropertyRowViewModel>` | No (empty) | `IRunService.GetRunSummary` slot lookup — blocked |
| `EntityIssues` | `IReadOnlyList<ValidationIssueSummaryDto>` | No (empty) | `IValidationSummaryService.GetIssuesFor` — blocked |

**Commands:** None in Phase 1. `CommitEditCommand` and `ClearOverrideCommand` are Phase 2 (stubs with `CanExecute = false` may be declared now if it helps XAML binding stability).

**Events subscribed to:**

| Event | Action |
|---|---|
| `DesignChangedEvent` | If selection active, attempt to refresh properties (guarded) |
| `UndoAppliedEvent` | Same |
| `RedoAppliedEvent` | Same |
| `ProjectClosedEvent` | Clear selection and all properties |

**`PropertyRowViewModel` (nested class or separate file — Phase 1 shape):**

```csharp
public sealed class PropertyRowViewModel
{
    public string Key { get; init; }
    public string DisplayName { get; init; }
    public string DisplayValue { get; init; }
    // Phase 2 additions: IsOverridden, InheritedFrom, IsEditable, CommitEditCommand, ClearOverrideCommand
}
```

**App service gap (must be resolved before full display works):**

The `RunSlotSummaryDto` (accessible via `IRunService.GetRunSummary`) contains: `CabinetId`, `CabinetTypeId`, `NominalWidthInches`, `Index`. This is sufficient for Phase 1 read-only display. No new DTO is needed — only `GetRunSummary` must be implemented.

Depth is not currently in `RunSlotSummaryDto`. If needed for Phase 1, either:
1. Accept omission (display width only)
2. Add `NominalDepthInches` to `RunSlotSummaryDto` (preferred when `GetRunSummary` is implemented)

**Tests required:**

| Test | Description |
|---|---|
| `OnSelectionChanged_WithIds_SetsHasSelection` | `HasSelection = true` immediately (no service call needed) |
| `OnSelectionChanged_Empty_ClearsHasSelection` | `HasSelection = false` on empty selection |
| `ProjectClosed_ClearsSelection` | `HasSelection = false` after `ProjectClosedEvent` |
| `Dispose_UnsubscribesFromEvents` | Post-dispose events do not trigger refresh |

---

## 3. ShellViewModel Changes Required

### 3.1 Constructor signature (after Phase 1)

```csharp
public ShellViewModel(
    IProjectService projectService,
    IUndoRedoService undoRedoService,
    IApplicationEventBus eventBus,
    EditorCanvasViewModel canvas,
    CatalogPanelViewModel catalog,
    PropertyInspectorViewModel propertyInspector,
    RunSummaryPanelViewModel runSummary,
    IssuePanelViewModel issuePanel,
    StatusBarViewModel statusBar)
```

### 3.2 New public properties

```csharp
public CatalogPanelViewModel Catalog { get; }
public PropertyInspectorViewModel PropertyInspector { get; }
public RunSummaryPanelViewModel RunSummary { get; }
public IssuePanelViewModel IssuePanel { get; }
public StatusBarViewModel StatusBar { get; }
```

### 3.3 Properties to remove

These static placeholder properties become dead weight once child VMs own their data:

```
CatalogPanelTitle, CatalogPanelSubtitle, CatalogItems
PropertyInspectorTitle, PropertyInspectorSubtitle, PropertyInspectorItems
RunSummaryTitle, RunSummarySubtitle, RunSummaryItems
IssuePanelTitle, IssuePanelSubtitle, IssueItems
StatusProjectText, StatusSaveText
```

**Removal is safe** — existing `ShellViewModelTests` does not assert any of these properties.

### 3.4 Selection forwarding addition

In `OnCanvasPropertyChanged`:

```csharp
if (e.PropertyName is nameof(EditorCanvasViewModel.SelectedCabinetIds))
{
    PropertyInspector.OnSelectionChanged(Canvas.SelectedCabinetIds);
    RunSummary.OnSelectionChanged(Canvas.SelectedCabinetIds);
}
```

### 3.5 SelectEntities coordination method

`IssuePanelViewModel.GoToEntityCommand` needs to select entities on the canvas. Add to `ShellViewModel`:

```csharp
internal void SelectEntities(IReadOnlyList<Guid> entityIds)
{
    Canvas.SetSelectedCabinetIds(entityIds);
}
```

`EditorCanvasViewModel.SetSelectedCabinetIds` does not yet exist as a public method. Add it:

```csharp
// EditorCanvasViewModel.cs — add:
public void SetSelectedCabinetIds(IReadOnlyList<Guid> ids)
{
    _editorSession.SetSelectedCabinetIds(ids);
    RefreshInteractionState();
}
```

Pass `ShellViewModel.SelectEntities` as an `Action<IReadOnlyList<Guid>>` delegate to `IssuePanelViewModel` and `RunSummaryPanelViewModel` via constructor injection or a post-construction setter. Constructor injection is preferred:

```csharp
public IssuePanelViewModel(
    IValidationSummaryService validationSummaryService,
    IApplicationEventBus eventBus,
    Action<IReadOnlyList<Guid>> selectEntities)  // coordination callback
```

### 3.6 Dispose chain

```csharp
public void Dispose()
{
    // existing unsubscribes...
    Canvas.Dispose();
    // new — dispose child VMs that implement IDisposable
    StatusBar.Dispose();
    IssuePanel.Dispose();
    RunSummary.Dispose();
    PropertyInspector.Dispose();
    // CatalogPanelViewModel has no events, no Dispose needed
}
```

---

## 4. DI Registration Updates

`PresentationServiceRegistration.cs` must add:

```csharp
services.AddScoped<CatalogPanelViewModel>();
services.AddScoped<PropertyInspectorViewModel>();
services.AddScoped<RunSummaryPanelViewModel>();
services.AddScoped<IssuePanelViewModel>();
services.AddScoped<StatusBarViewModel>();
```

The `Action<IReadOnlyList<Guid>>` coordination delegate for `IssuePanelViewModel` and `RunSummaryPanelViewModel` cannot be satisfied by the DI container directly (it depends on a `ShellViewModel` instance). Options:

1. **Factory delegate registered in DI** — register a factory that creates `IssuePanelViewModel` using the already-resolved `ShellViewModel`. This creates a circular dependency.
2. **Post-construction injection** — add a `SetSelectionCallback(Action<IReadOnlyList<Guid>> callback)` method called by `ShellViewModel` constructor. This is the simpler, lower-risk approach.
3. **Weak delegate via event** — `ShellViewModel` subscribes to an event raised by `IssuePanelViewModel`. Avoid — this is indirect for no gain.

**Recommendation: Option 2.** `ShellViewModel` calls `IssuePanel.SetSelectionCallback(SelectEntities)` and `RunSummary.SetSelectionCallback(SelectEntities)` in its own constructor, after all child VMs are assigned.

---

## 5. Build Order

Build in this order. Each step produces a working build.

| Step | Task | Blocking? |
|---|---|---|
| 1 | `CatalogPanelViewModel` + `CatalogItemViewModel` — no service deps | Not blocked |
| 2 | `StatusBarViewModel` — partial live data (revision label + save state) | Not blocked |
| 3 | `IssuePanelViewModel` + `IssueRowViewModel` — guarded empty state | Not blocked |
| 4 | `RunSummaryPanelViewModel` + `RunSlotViewModel` — guarded empty state | Not blocked |
| 5 | `PropertyInspectorViewModel` + `PropertyRowViewModel` — guarded empty state | Not blocked |
| 6 | Wire all into `ShellViewModel`: add child VM properties, remove static props, add selection forwarding, update Dispose | Requires steps 1–5 |
| 7 | Add `EditorCanvasViewModel.SetSelectedCabinetIds` public method | Requires step 6 |
| 8 | Update `PresentationServiceRegistration` | Requires steps 1–6 |
| 9 | Update XAML: add `DataContext` to panel borders; add status bar region | Requires steps 1–6 |
| 10 | Update `ShellViewModelTests` factory to inject child VM stubs | Requires steps 1–6 |
| 11 | Implement `IRunService.FindRunContaining` + `GetRunSummary` (Application layer) | Required to unblock RunSummary + PropertyInspector |
| 12 | Implement `IValidationSummaryService` (Application layer) | Required to unblock IssuePanel + StatusBar counts |

---

## 6. What Is NOT in Phase 1

| Feature | Reason | Phase |
|---|---|---|
| Drag-drop from catalog to canvas | Requires Editor drag context + canvas input methods | 2 |
| Property inline editing | `SetCabinetOverrideAsync` not implemented | 2 |
| Override/inheritance display in PropertyInspector | No DTO support yet | 2 |
| Severity filter UI in IssuePanel | Display-only service not implemented yet | 2 |
| Canvas mouse-move / hover highlight | `OnMouseMove` not implemented | 2 |
| `RevisionHistoryViewModel` | No panel region planned | LATER |
| `DisplayFormatter` / unit preferences | Inline `$"{value}\""` is sufficient for now | LATER |
| Design-time data constructors | Not blocking any Phase 1 test | LATER |
