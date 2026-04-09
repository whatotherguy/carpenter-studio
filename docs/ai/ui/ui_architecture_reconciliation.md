# UI Architecture Reconciliation

Date: 2026-04-08
Basis: `presentation.md` (spec), `ui_gap_audit.md`, `ui_inventory_and_contracts.md`, current codebase

---

## 1. Current Structure

### 1.1 ViewModel Graph

```
ShellViewModel (root)
  ├── Canvas: EditorCanvasViewModel (injected via constructor)
  │     ├── IRunService
  │     ├── IApplicationEventBus
  │     ├── ISceneProjector
  │     ├── IEditorCanvasSession
  │     ├── IHitTester
  │     └── IEditorCanvasHost
  ├── IProjectService
  ├── IUndoRedoService
  └── IApplicationEventBus
```

`ShellViewModel` owns all panel data as static properties:
- `CatalogPanelTitle`, `CatalogPanelSubtitle`, `CatalogItems` — static string lists
- `PropertyInspectorTitle`, `PropertyInspectorSubtitle`, `PropertyInspectorItems` — static string lists
- `RunSummaryTitle`, `RunSummarySubtitle`, `RunSummaryItems` — static string lists
- `IssuePanelTitle`, `IssuePanelSubtitle`, `IssueItems` — static string lists
- `StatusProjectText`, `StatusSaveText` — derived from `ActiveProject`
- `CanvasStatusMessage`, `CanvasCurrentMode` — forwarded from `EditorCanvasViewModel`

### 1.2 XAML Structure

```
MainWindow.xaml
  DockPanel
    ├── Menu (DockPanel.Dock="Top") — bound to ShellViewModel commands
    ├── ToolBarTray (DockPanel.Dock="Top") — bound to ShellViewModel commands
    └── Grid (5-column, 3-row)
          ├── [0,0] Catalog panel — inline XAML, bound to ShellViewModel
          ├── [0,2] Canvas workspace — ContentControl bound to CanvasView
          ├── [0,4] Property inspector — inline XAML, bound to ShellViewModel
          ├── [2,0-1] Run summary — inline XAML, bound to ShellViewModel
          └── [2,3-4] Issue panel — inline XAML, bound to ShellViewModel
          (GridSplitters between all panel columns)
```

No status bar. No UserControls. All `DataContext` is implicitly `ShellViewModel` (set in code-behind constructor).

### 1.3 Source Files

| File | Lines | Role |
|---|---|---|
| `ViewModels/ShellViewModel.cs` | 235 | Root VM: lifecycle, commands, 20+ static panel properties |
| `ViewModels/EditorCanvasViewModel.cs` | 170 | Canvas VM: scene, selection, add/move cabinet |
| `ViewModels/EditorCanvasHost.cs` | 21 | Wraps EditorCanvas UIElement |
| `ViewModels/IEditorCanvasHost.cs` | 7 | Interface |
| `ViewModels/IEditorCanvasSession.cs` | 16 | Interface |
| `ViewModels/EditorCanvasSessionAdapter.cs` | 27 | Adapts EditorSession to Guid-based |
| `Projection/SceneProjector.cs` | 76 | Projects design state to RenderSceneDto |
| `Projection/ISceneProjector.cs` | 8 | Interface |
| `Commands/RelayCommand.cs` | 23 | Synchronous ICommand |
| `Commands/AsyncRelayCommand.cs` | 45 | Async ICommand with reentrancy guard |
| `ObservableObject.cs` | ~30 | INPC base class |
| `PresentationServiceRegistration.cs` | 26 | DI registration |
| `MainWindow.xaml` | 310 | Full shell layout with placeholders |
| `MainWindow.xaml.cs` | 24 | Minimal code-behind: DI ctor, DataContext, Dispose |

### 1.4 Tests

| File | Tests | Coverage |
|---|---|---|
| `ShellViewModelTests.cs` | 2 | Save + property changes; project opened + title + commands |
| `EditorCanvasViewModelTests.cs` | 3 | Add cabinet; design changed refreshes; mouse down selects |

---

## 2. Target Structure

### 2.1 ViewModel Graph (after Phase 1)

```
ShellViewModel (root — anchor, preserved)
  ├── Canvas: EditorCanvasViewModel (anchor, preserved)
  ├── Catalog: CatalogPanelViewModel (new)
  ├── PropertyInspector: PropertyInspectorViewModel (new)
  ├── RunSummary: RunSummaryPanelViewModel (new)
  ├── IssuePanel: IssuePanelViewModel (new)
  ├── StatusBar: StatusBarViewModel (new)
  ├── IProjectService (preserved)
  ├── IUndoRedoService (preserved)
  └── IApplicationEventBus (preserved)
```

### 2.2 Binding Strategy

Each panel region in `MainWindow.xaml` binds to a child ViewModel exposed as a public property on `ShellViewModel`. The child VM's `DataContext` is set via XAML binding, not code-behind.

```xml
<!-- Example: Run Summary panel binds to child VM -->
<Border DataContext="{Binding RunSummary}" ...>
    <DockPanel>
        <TextBlock Text="{Binding Title}" ... />
        <ItemsControl ItemsSource="{Binding Slots}" ... />
    </DockPanel>
</Border>
```

The XAML structure of `MainWindow.xaml` does not change — only the `DataContext` and inner bindings of each panel region change.

### 2.3 ShellViewModel Changes

**Remove:** All static panel properties (`CatalogPanelTitle`, `CatalogItems`, `PropertyInspectorTitle`, `PropertyInspectorItems`, `RunSummaryTitle`, `RunSummaryItems`, `IssuePanelTitle`, `IssueItems`, `StatusProjectText`, `StatusSaveText`).

**Add:** Child VM properties and constructor parameters:

```csharp
public CatalogPanelViewModel Catalog { get; }
public PropertyInspectorViewModel PropertyInspector { get; }
public RunSummaryPanelViewModel RunSummary { get; }
public IssuePanelViewModel IssuePanel { get; }
public StatusBarViewModel StatusBar { get; }
```

**Preserve:** `Canvas`, `ActiveProject`, `HasActiveProject`, `WindowTitle`, `WorkspaceTitle`, `WorkspaceSubtitle`, all commands, all event subscriptions, `Dispose()` pattern.

**Add:** Selection change forwarding in `OnCanvasPropertyChanged`:

```csharp
if (e.PropertyName is nameof(EditorCanvasViewModel.SelectedCabinetIds))
{
    PropertyInspector.OnSelectionChanged(Canvas.SelectedCabinetIds);
    RunSummary.OnSelectionChanged(Canvas.SelectedCabinetIds);
}
```

**Add to Dispose:** Dispose child VMs that implement `IDisposable`.

### 2.4 XAML Changes

| Region | Current `DataContext` | Target `DataContext` | XAML Change |
|---|---|---|---|
| Menu bar | ShellViewModel (implicit) | ShellViewModel (implicit) | None |
| Toolbar | ShellViewModel (implicit) | ShellViewModel (implicit) | None |
| Canvas workspace | ShellViewModel (CanvasView binding) | ShellViewModel (CanvasView binding) | None |
| Catalog panel | ShellViewModel (implicit) | `{Binding Catalog}` | Add `DataContext`, update inner bindings |
| Property inspector | ShellViewModel (implicit) | `{Binding PropertyInspector}` | Add `DataContext`, update inner bindings |
| Run summary | ShellViewModel (implicit) | `{Binding RunSummary}` | Add `DataContext`, update inner bindings |
| Issue panel | ShellViewModel (implicit) | `{Binding IssuePanel}` | Add `DataContext`, update inner bindings |
| Status bar | Does not exist | `{Binding StatusBar}` | Add new `DockPanel.Dock="Bottom"` region |

---

## 3. Delta

### 3.1 New Files

| File | Purpose | Phase |
|---|---|---|
| `ViewModels/StatusBarViewModel.cs` | Validation counts, revision label, save state | 1 |
| `ViewModels/IssuePanelViewModel.cs` | Validation issue list with severity counts | 1 |
| `ViewModels/IssueRowViewModel.cs` | Individual issue row (can be nested class or separate) | 1 |
| `ViewModels/RunSummaryPanelViewModel.cs` | Active run slot list, width summary | 1 |
| `ViewModels/RunSlotViewModel.cs` | Individual slot item (can be nested class or separate) | 1 |
| `ViewModels/PropertyInspectorViewModel.cs` | Selected entity properties (read-only Phase 1) | 1 |
| `ViewModels/PropertyRowViewModel.cs` | Individual property row (can be nested class or separate) | 1 |
| `ViewModels/CatalogPanelViewModel.cs` | Static cabinet type list | 1 |
| `ViewModels/CatalogItemViewModel.cs` | Individual catalog item (can be nested class or separate) | 1 |
| `Tests/Presentation/StatusBarViewModelTests.cs` | StatusBar event/state tests | 1 |
| `Tests/Presentation/IssuePanelViewModelTests.cs` | Issue list tests | 1 |
| `Tests/Presentation/RunSummaryPanelViewModelTests.cs` | Run summary tests | 1 |
| `Tests/Presentation/PropertyInspectorViewModelTests.cs` | Property inspector tests | 1 |
| `Tests/Presentation/CatalogPanelViewModelTests.cs` | Catalog tests | 1 |

### 3.2 Modified Files

| File | Change | Risk |
|---|---|---|
| `ViewModels/ShellViewModel.cs` | Add child VM properties; remove static panel properties; add selection forwarding; update Dispose | **Medium** — central VM, existing tests cover critical paths |
| `MainWindow.xaml` | Add `DataContext` bindings to panel regions; add status bar; update inner panel bindings | **Low** — purely declarative, no logic |
| `PresentationServiceRegistration.cs` | Register 5 new VMs | **Low** |
| `Tests/Presentation/ShellViewModelTests.cs` | Update `CreateShellViewModel` to inject child VM mocks/stubs | **Low** |

### 3.3 No Changes Required

| File / Layer | Reason |
|---|---|
| `EditorCanvasViewModel.cs` | Anchor — no Phase 1 changes needed |
| `EditorCanvasHost.cs` / `IEditorCanvasHost.cs` | No changes |
| `EditorCanvasSessionAdapter.cs` / `IEditorCanvasSession.cs` | No changes |
| `SceneProjector.cs` / `ISceneProjector.cs` | No changes |
| `RelayCommand.cs` / `AsyncRelayCommand.cs` | No changes |
| `ObservableObject.cs` | No changes |
| `MainWindow.xaml.cs` | No changes |
| `CabinetDesigner.Application` | No changes (all needed services exist) |
| `CabinetDesigner.Domain` | No changes |
| `CabinetDesigner.Editor` | No changes |
| `CabinetDesigner.Rendering` | No changes |
| `CabinetDesigner.Persistence` | No changes |

---

## 4. Code-Behind Policy

### 4.1 Allowed

| Location | Permitted Code-Behind |
|---|---|
| `MainWindow.xaml.cs` | DI constructor, `DataContext` assignment, `Dispose` call in `OnClosed`. Already implemented; no additions needed. |
| Future canvas-hosting UserControl | Mouse/keyboard event forwarding to `EditorCanvasViewModel` methods (`OnMouseMove`, `OnMouseUp`, `OnMouseWheel`, `OnKeyDown`). Phase 2 only. |
| Future catalog panel | WPF `DragDrop.DoDragDrop` initiation. Phase 2 only. |

### 4.2 Forbidden

| Rule | Rationale |
|---|---|
| No conditional logic based on domain/application state | Keeps code-behind dumb; all decisions live in VMs |
| No direct calls to application services | VMs own service communication |
| No DTO transformation or formatting | Formatting belongs in VMs or DisplayFormatter |
| No command construction or execution | Commands are VM responsibility |
| No event subscription | VMs own event lifecycle |

### 4.3 Phase 1 Code-Behind Additions

**None.** All Phase 1 work is ViewModel creation and XAML binding changes. No new code-behind is required.

---

## 5. Migration Path

The migration is designed to be **incremental and non-breaking** at every step. Each step produces a working build. No step removes working functionality before its replacement is ready.

### Step 1: StatusBarViewModel + Status Bar XAML

**What:** Create `StatusBarViewModel`. Add status bar `DockPanel.Dock="Bottom"` to `MainWindow.xaml`. Inject into `ShellViewModel`.

**Why first:** Simplest new VM — no selection dependency, no child item VMs. Validates the full pattern: new VM → DI registration → ShellViewModel injection → XAML DataContext binding → event subscription → test.

**Migration detail:**
1. Create `StatusBarViewModel.cs` with `IApplicationEventBus`, `IValidationSummaryService` dependencies
2. Subscribe to `DesignChangedEvent`, `ProjectOpenedEvent`, `ProjectClosedEvent`, `UndoAppliedEvent`, `RedoAppliedEvent`
3. Expose: `ErrorCount`, `WarningCount`, `InfoCount`, `RevisionLabel`, `SaveStateDisplay`, `HasManufactureBlockers`
4. Add `StatusBarViewModel` to `ShellViewModel` constructor and expose as `public StatusBarViewModel StatusBar { get; }`
5. Register in `PresentationServiceRegistration`
6. Add XAML: `<Border DockPanel.Dock="Bottom" DataContext="{Binding StatusBar}">` before the main `Grid`
7. Remove `StatusProjectText` and `StatusSaveText` from `ShellViewModel` (these are replaced by `StatusBar.RevisionLabel` and `StatusBar.SaveStateDisplay`)
8. Write `StatusBarViewModelTests.cs`

**Existing test impact:** `ShellViewModelTests` does not reference `StatusProjectText` or `StatusSaveText`, so removal is safe.

### Step 2: IssuePanelViewModel

**What:** Create `IssuePanelViewModel` with `IssueRowViewModel`. Replace issue panel placeholder in `MainWindow.xaml`.

**Migration detail:**
1. Create `IssuePanelViewModel.cs`: inject `IValidationSummaryService`, `IApplicationEventBus`
2. Subscribe to `DesignChangedEvent`, `UndoAppliedEvent`, `RedoAppliedEvent`, `ProjectClosedEvent`
3. Expose: `AllIssues`, `ErrorCount`, `WarningCount`, `InfoCount`, `HasManufactureBlockers`
4. No severity filtering in Phase 1 — expose all issues
5. `GoToEntityCommand` — sets selection on `EditorCanvasViewModel`. Requires ShellViewModel to expose a `SelectEntities(IReadOnlyList<Guid>)` method that forwards to `EditorCanvasSession.SetSelectedCabinetIds`.
6. Add to `ShellViewModel` as `public IssuePanelViewModel IssuePanel { get; }`
7. In XAML: add `DataContext="{Binding IssuePanel}"` to issue panel Border; replace `ItemsControl` bindings
8. Remove `IssuePanelTitle`, `IssuePanelSubtitle`, `IssueItems` from `ShellViewModel`
9. Write `IssuePanelViewModelTests.cs`

### Step 3: RunSummaryPanelViewModel

**What:** Create `RunSummaryPanelViewModel` with `RunSlotViewModel`. Replace run summary placeholder.

**Migration detail:**
1. Create `RunSummaryPanelViewModel.cs`: inject `IRunService`, `IApplicationEventBus`
2. Subscribe to `DesignChangedEvent`, `UndoAppliedEvent`, `RedoAppliedEvent`, `ProjectClosedEvent`
3. Expose: `HasActiveRun`, `TotalWidthDisplay`, `CabinetCountDisplay`, `Slots`
4. `OnSelectionChanged(IReadOnlyList<Guid> cabinetIds)` — called by ShellViewModel when canvas selection changes. Must resolve cabinet → run. Use `IRunService.GetRunSummary(RunId)` once run ID is known.
5. **Run ID resolution:** The selected cabinet's run can be determined by iterating `IDesignStateStore.GetAllRuns()` and checking slots. However, Presentation must not reference `IDesignStateStore` directly. **Solution:** Add a thin query to `IRunService`:
   ```csharp
   RunId? FindRunContaining(Guid cabinetId);
   ```
   This is a 3-line method in `RunService` delegating to `IDesignStateStore`. If adding this method is deferred, Phase 1 can activate the run summary only when the user selects a run directly (not via cabinet selection), leaving the feature partial.
6. `SelectSlotCommand` — selects the slot's cabinet on the canvas via ShellViewModel coordination
7. Add to `ShellViewModel`; add selection forwarding in `OnCanvasPropertyChanged`
8. Remove `RunSummaryTitle`, `RunSummarySubtitle`, `RunSummaryItems` from `ShellViewModel`
9. Write `RunSummaryPanelViewModelTests.cs`

### Step 4: PropertyInspectorViewModel

**What:** Create `PropertyInspectorViewModel` with `PropertyRowViewModel`. Replace property inspector placeholder. Read-only in Phase 1.

**Migration detail:**
1. Create `PropertyInspectorViewModel.cs`: inject `IRunService`, `IValidationSummaryService`, `IApplicationEventBus`
2. Subscribe to `DesignChangedEvent`, `UndoAppliedEvent`, `RedoAppliedEvent`, `ProjectClosedEvent`
3. `OnSelectionChanged(IReadOnlyList<Guid> cabinetIds)` — called by ShellViewModel
4. When a single cabinet is selected, display its known properties as `PropertyRowViewModel` items:
   - **Phase 1 properties (read-only):** CabinetTypeId, NominalWidth (formatted), NominalDepth (formatted), RunId, SlotIndex
   - Source: `RunSlotSummaryDto` from `IRunService.GetRunSummary(runId)` (run summary already has slot data with type, width, index)
5. `CommitEditCommand` and `ClearOverrideCommand` — **stub in Phase 1** (`CanExecute = false`)
6. `EntityIssues` — `IValidationSummaryService.GetIssuesFor(cabinetId)` scoped to selected entity
7. Add to `ShellViewModel`; selection forwarding already wired in Step 3
8. Remove `PropertyInspectorTitle`, `PropertyInspectorSubtitle`, `PropertyInspectorItems` from `ShellViewModel`
9. Write `PropertyInspectorViewModelTests.cs`

### Step 5: CatalogPanelViewModel

**What:** Create `CatalogPanelViewModel` with `CatalogItemViewModel`. Replace catalog placeholder.

**Migration detail:**
1. Create `CatalogPanelViewModel.cs` — no service dependency at MVP
2. Expose `AllItems` and `FilteredItems` as `IReadOnlyList<CatalogItemViewModel>`
3. Hardcode a static list of 6-8 cabinet types:
   ```
   Base 24", Base 30", Base 36", Base 42"
   Wall 30", Wall 36"
   Tall 84", Tall 96"
   ```
4. `SearchText` (two-way) — filters `AllItems` into `FilteredItems`. Simple `Contains` match.
5. No drag-drop in Phase 1. Catalog items are display-only for now.
6. Add to `ShellViewModel`
7. Remove `CatalogPanelTitle`, `CatalogPanelSubtitle`, `CatalogItems` from `ShellViewModel`
8. Write `CatalogPanelViewModelTests.cs`

### Step 6: Final Wiring and Cleanup

**What:** Verify all static placeholder properties are removed from `ShellViewModel`. Update all tests. Verify build and test pass.

1. Audit `ShellViewModel` — confirm no orphaned panel properties remain
2. Update `ShellViewModelTests` factory method to inject all child VM dependencies
3. Run full test suite: `dotnet test`
4. Verify XAML bindings render correctly (manual or screenshot test)

---

## 6. Test Strategy

### 6.1 Pattern

Every new ViewModel gets a matching test class following the established pattern from `ShellViewModelTests` and `EditorCanvasViewModelTests`:

```csharp
public sealed class SomeViewModelTests
{
    [Fact]
    public void EventRefreshesProperties()
    {
        // 1. Construct VM with recording/stub dependencies
        // 2. Publish event via ApplicationEventBus
        // 3. Assert observable properties updated
    }

    [Fact]
    public void CommandDelegatesToService()
    {
        // 1. Construct VM with recording service
        // 2. Execute command
        // 3. Assert service method was called with expected args
    }

    [Fact]
    public void CanExecuteReflectsState()
    {
        // 1. Construct VM in state where command should be disabled
        // 2. Assert CanExecute == false
        // 3. Change state
        // 4. Assert CanExecute == true
    }

    [Fact]
    public void DisposeUnsubscribesFromEvents()
    {
        // 1. Construct VM
        // 2. Dispose VM
        // 3. Publish event
        // 4. Assert no property changes fired (no stale handler)
    }
}
```

### 6.2 Test Dependencies

Each test class creates its own recording/stub implementations. Do not create a shared "test helpers" project — keep test doubles local to each test class (consistent with existing test files).

Reusable stubs across multiple test classes (e.g., `ApplicationEventBus`) are already the real implementation (it's lightweight enough to use directly).

### 6.3 Required Tests Per ViewModel

| ViewModel | Minimum Tests |
|---|---|
| `StatusBarViewModel` | Event refreshes counts; ProjectOpened sets revision label; ProjectClosed resets; Dispose unsubscribes |
| `IssuePanelViewModel` | Event refreshes issue list; severity counts correct; GoToEntity command calls selection; Dispose unsubscribes |
| `RunSummaryPanelViewModel` | OnSelectionChanged activates run; DesignChanged refreshes; SelectSlot syncs to canvas; empty state when no run; Dispose unsubscribes |
| `PropertyInspectorViewModel` | OnSelectionChanged shows properties; DesignChanged refreshes if entity affected; no selection shows empty; Dispose unsubscribes |
| `CatalogPanelViewModel` | Items populated on construction; SearchText filters items; empty search returns all |
| `ShellViewModel` (updates) | Child VMs injected and accessible; selection forwarding works; Dispose chains to children |

### 6.4 What Is NOT Tested in Phase 1

- XAML binding correctness (requires WPF runtime; tested manually)
- Code-behind (none added in Phase 1)
- Drag-drop (Phase 2)
- Canvas input methods beyond `OnMouseDown` (Phase 2)
- DisplayFormatter (Phase 2)

---

## 7. Risks

### 7.1 ShellViewModel Constructor Bloat

**Risk:** Adding 5 child VMs to the constructor takes it from 4 parameters to 9.

**Mitigation:** This is acceptable for a root composition ViewModel. The DI container handles construction. Do not introduce a factory or builder pattern — it adds complexity with no benefit. If the parameter count becomes uncomfortable later, child VMs can be grouped into a `ShellPanelGroup` record, but this is not needed now.

### 7.2 Selection Synchronization Race Conditions

**Risk:** Selection changes propagate from `EditorCanvasViewModel` → `ShellViewModel` → `PropertyInspectorViewModel` / `RunSummaryPanelViewModel`. If any of these trigger async service calls, there is a risk of stale data from a previous selection arriving after a newer selection.

**Mitigation:** All selection-triggered reads (`IRunService.GetRunSummary`, `IValidationSummaryService.GetIssuesFor`) are synchronous methods that read from in-memory state. No async race. If future changes introduce async reads, add a selection generation counter and discard stale results.

### 7.3 Run ID Resolution for Property Inspector / Run Summary

**Risk:** No `IRunService` method currently resolves cabinet → run. Without it, selecting a cabinet cannot automatically activate the run summary.

**Mitigation options (in preference order):**
1. **Add `IRunService.FindRunContaining(Guid cabinetId) → Guid?`** — 3-line implementation in `RunService` delegating to `IDesignStateStore`. Preferred. Small, focused, no architecture impact.
2. **Iterate `IRunService.GetRunSummary` for all runs** — viable but inefficient. Avoid.
3. **Defer run activation to Phase 2** — run summary only shows data when a run is directly known (e.g., after `AddCabinetToRunAsync` returns). Acceptable fallback.

**Recommendation:** Option 1. The method is trivial and avoids a limitation that would confuse users.

### 7.4 XAML DataContext Binding Breakage

**Risk:** Changing panel `DataContext` from implicit ShellViewModel to child VMs will break any bindings that still reference ShellViewModel properties.

**Mitigation:** The workspace header area (canvas title/subtitle/mode badge) remains bound to ShellViewModel directly (it is inside the canvas column, not inside a panel that gets re-bound). Menu and toolbar are at the top level and keep implicit ShellViewModel context. Only the 5 panel Borders get `DataContext` overrides. Verify at each step that no orphaned bindings exist by building and running the app.

### 7.5 Existing Test Breakage

**Risk:** `ShellViewModelTests.CreateShellViewModel` constructs `ShellViewModel` with 4 parameters. Adding child VMs changes the constructor signature.

**Mitigation:** Update the factory method to create and inject all child VM stubs. This is the last step in the migration sequence, done after all child VMs exist and have their own tests.

---

## 8. Phase 1 Placeholder Policy

### 8.1 Fully Functional in Phase 1

| Component | Detail |
|---|---|
| StatusBarViewModel | All bindings live: validation counts, revision label, save state |
| IssuePanelViewModel | All issues displayed, severity counts computed, GoToEntity navigates |
| RunSummaryPanelViewModel | Active run slots shown when cabinet selected, slot click syncs selection |
| CatalogPanelViewModel | Static list displayed, search filters items |
| PropertyInspectorViewModel | Read-only properties displayed for selected cabinet |

### 8.2 Placeholder / Stub in Phase 1

| Component | What's Placeholder | Phase 2 Target |
|---|---|---|
| Catalog panel | No drag-drop; items are display-only | Drag initiation → canvas placement |
| Property inspector | Read-only; `CommitEditCommand` and `ClearOverrideCommand` disabled | Inline editing, override indicators |
| Property inspector | No override/inheritance display | Inherited-from labels, override badges |
| Canvas input | Only `OnMouseDown` implemented | `OnMouseMove`, `OnMouseUp`, `OnMouseWheel`, `OnKeyDown` |
| Menu items | View/Design/Tools/Help items remain `IsEnabled="False"` | Mode switching, zoom, grid toggle |
| Toolbar | No mode buttons | Select/DrawRun toggle buttons |
| DisplayFormatter | Dimensions formatted as `$"{value}\""` inline | User-preference-driven formatting |
| Revision history | Not started | RevisionHistoryViewModel + panel |
| Design-time data | Not implemented | Parameterless constructors with sample data |

### 8.3 Do NOT Build in Phase 1

- `RevisionHistoryViewModel` (no panel region, no urgency)
- `DisplayFormatter` / `ISettingsProvider` integration (inline formatting is sufficient)
- Design-time data constructors
- Canvas mouse-move / hover highlight / drag preview
- Keyboard shortcut handling
- Panel collapse/expand persistence
- Multi-document / tab support
