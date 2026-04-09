# UI Gap Audit: Current Implementation vs. Presentation Architecture

Date: 2026-04-08
Auditor: Claude Opus 4.6
Sources: `docs/ai/outputs/presentation.md`, codebase scan of `src/CabinetDesigner.Presentation/`

---

## 1. What Exists Today

### 1.1 Source Files in `CabinetDesigner.Presentation`

| File | Role | Status |
|---|---|---|
| `MainWindow.xaml` | Shell window with full 5-region grid layout | **Implemented** |
| `MainWindow.xaml.cs` | Minimal code-behind (DI constructor, DataContext, Dispose) | **Implemented, conformant** |
| `ViewModels/ShellViewModel.cs` | Root VM: project lifecycle, commands, panel title/subtitle bindings | **Implemented** |
| `ViewModels/EditorCanvasViewModel.cs` | Canvas VM: scene projection, hit testing, selection, add/move cabinet | **Implemented** |
| `ViewModels/EditorCanvasHost.cs` | Wraps `EditorCanvas` for WPF hosting | **Implemented** |
| `ViewModels/IEditorCanvasHost.cs` | Abstraction for canvas host | **Implemented** |
| `ViewModels/IEditorCanvasSession.cs` | Abstraction for editor session access from Presentation | **Implemented** |
| `ViewModels/EditorCanvasSessionAdapter.cs` | Adapts `EditorSession` to `IEditorCanvasSession` using Guid primitives | **Implemented** |
| `Projection/SceneProjector.cs` | Projects design state into `RenderSceneDto` | **Implemented** |
| `Projection/ISceneProjector.cs` | Interface for scene projection | **Implemented** |
| `Commands/RelayCommand.cs` | Synchronous relay command | **Implemented** |
| `Commands/AsyncRelayCommand.cs` | Async relay command with reentrancy guard | **Implemented** |
| `ObservableObject.cs` | INotifyPropertyChanged base class | **Implemented** |
| `PresentationServiceRegistration.cs` | DI registration for Presentation layer | **Implemented** |
| `GlobalUsings.cs` | Shared usings | **Implemented** |

### 1.2 Shell Layout (MainWindow.xaml)

The XAML defines a complete 5-region shell matching the spec's layout:

| Region | Grid Position | Content | Binding Status |
|---|---|---|---|
| **Menu bar** | DockPanel.Top | File, Edit, View, Design, Tools, Help | Commands bound (New, Open, Save, Close, Undo, Redo). View/Design/Tools/Help items are `IsEnabled="False"` stubs. |
| **Toolbar** | DockPanel.Top | New, Open, Save, Undo, Redo buttons | Bound to ShellViewModel commands. |
| **Catalog panel** | Row 0, Col 0 (270px) | Title + subtitle + bullet list | Bound to `CatalogPanelTitle`, `CatalogPanelSubtitle`, `CatalogItems`. **Placeholder strings only.** |
| **Editor canvas** | Row 0, Col 2 (star) | Mode badge + `ContentControl` hosting canvas | `CanvasView` and `CanvasCurrentMode` bound. **Functional.** |
| **Property inspector** | Row 0, Col 4 (320px) | Title + subtitle + bullet list | Bound to `PropertyInspectorTitle`, `PropertyInspectorSubtitle`, `PropertyInspectorItems`. **Placeholder strings only.** |
| **Run summary** | Row 2, Col 0-1 (190px) | Title + subtitle + bullet list | Bound to `RunSummaryTitle`, `RunSummarySubtitle`, `RunSummaryItems`. **Placeholder strings only.** |
| **Issue panel** | Row 2, Col 3-4 (190px) | Title + subtitle + bullet list | Bound to `IssuePanelTitle`, `IssuePanelSubtitle`, `IssueItems`. **Placeholder strings only.** |

GridSplitters exist between all panels. Visual theming (brushes, styles, border radius) is polished.

**Missing from shell:** Status bar (spec section 3.1 shows it at the bottom).

### 1.3 ShellViewModel Commands

| Command | Bound | Implemented | Delegates To |
|---|---|---|---|
| `NewProjectCommand` | Menu + toolbar | Yes | `IProjectService.CreateProjectAsync` |
| `OpenProjectCommand` | Menu + toolbar | Yes | `IProjectService.OpenProjectAsync` |
| `SaveCommand` | Menu + toolbar | Yes | `IProjectService.SaveAsync` |
| `CloseProjectCommand` | Menu | Yes | `IProjectService.CloseAsync` |
| `UndoCommand` | Menu + toolbar | Yes | `IUndoRedoService.Undo` |
| `RedoCommand` | Menu + toolbar | Yes | `IUndoRedoService.Redo` |

### 1.4 EditorCanvasViewModel Capabilities

- Scene projection on design changes, undo, redo, project close
- Hit testing with cabinet selection via `IHitTester`
- `AddCabinetToRunAsync` and `MoveCabinetAsync` methods (callable, but no UI trigger besides code)
- Interaction state refresh (selection, hover, mode)
- Event subscriptions with proper `Dispose()` cleanup

### 1.5 Tests

| Test File | Tests | Coverage |
|---|---|---|
| `ShellViewModelTests.cs` | 2 tests: save delegates + property changes; project opened updates title + command state | ShellViewModel lifecycle |
| `EditorCanvasViewModelTests.cs` | 3 tests: add cabinet delegates; design changed refreshes scene; mouse down selects cabinet | EditorCanvasViewModel core paths |

### 1.6 Project References (csproj)

```
Presentation → Application (correct)
Presentation → Editor (correct)
Presentation → Rendering (correct)
```

No reference to `CabinetDesigner.Domain` (F-3 from conformance review was resolved).

---

## 2. What the Architecture Spec Expected But Is Missing

### 2.1 Missing ViewModels

| ViewModel | Spec Section | Purpose | Current State |
|---|---|---|---|
| `CatalogPanelViewModel` | 4.3 | Cabinet type catalog, search/filter, drag initiation | **Missing.** ShellViewModel exposes static string lists as placeholder. |
| `CatalogItemViewModel` | 4.3 | Individual catalog item with type, name, category, width | **Missing.** |
| `PropertyInspectorViewModel` | 4.4 | Selected entity properties, inline editing, overrides | **Missing.** ShellViewModel exposes static placeholder strings. |
| `PropertyRowViewModel` | 4.4 | Individual editable property row with commit/clear commands | **Missing.** |
| `RunSummaryPanelViewModel` | 4.5 | Active run overview: slots, total width, filler status | **Missing.** ShellViewModel exposes static placeholder strings. |
| `RunSlotViewModel` | 4.5 | Individual run slot with cabinet info and selection sync | **Missing.** |
| `IssuePanelViewModel` | 4.6 | Validation issues with severity filtering, entity navigation | **Missing.** ShellViewModel exposes static placeholder strings. |
| `IssueRowViewModel` | 4.6 | Individual issue row with severity, code, message, entity links | **Missing.** |
| `RevisionHistoryViewModel` | 4.7 | Revision list, snapshot loading, approve revision | **Missing.** No shell region for it either. |
| `RevisionRowViewModel` | 4.7 | Individual revision row | **Missing.** |
| `StatusBarViewModel` | 4.8 | Validation summary counts, revision label, save state, autosave | **Missing.** |

### 2.2 Missing WPF Views / UserControls

The spec implies each panel region should be a UserControl with its own DataTemplate or explicit control. Currently, all panel content is inline XAML in `MainWindow.xaml` with bullet-list placeholders.

| Expected Control | Purpose | Status |
|---|---|---|
| `CatalogPanel.xaml` | Cabinet type browser with search box, categorized list, drag source | **Missing** |
| `PropertyInspectorPanel.xaml` | Property grid with key-value rows, override indicators, edit controls | **Missing** |
| `RunSummaryPanel.xaml` | Run slot strip with clickable slot items | **Missing** |
| `IssuePanelView.xaml` | Issue list with severity icons, filtering, entity navigation | **Missing** |
| `RevisionHistoryPanel.xaml` | Revision list with approval state and snapshot actions | **Missing** |
| `StatusBar.xaml` | Bottom status bar with validation counts, revision label, save state | **Missing** |

### 2.3 Missing Utility Classes

| Class | Spec Section | Purpose | Status |
|---|---|---|---|
| `DisplayFormatter` | 7.1 | Dimensional formatting (fractional inches, decimal, metric) | **Missing** |
| Design-time data constructors | 9.3 | Parameterless ViewModel constructors for XAML designer preview | **Missing** |

### 2.4 Missing Shell Regions

| Region | Description | Status |
|---|---|---|
| **Status bar** | Bottom bar with `[Issues: 0E 2W 1I] | [Revision: Draft v3] | [Saved]` | **Missing from MainWindow.xaml** |
| **Revision history** | Either a panel or a flyout for revision browsing | **Missing from MainWindow.xaml** |

### 2.5 Missing Input Handling

| Input | Spec Section | Purpose | Status |
|---|---|---|---|
| `OnMouseMove` | 4.2 | Hover highlight, drag preview, snap feedback | **Missing** from EditorCanvasViewModel |
| `OnMouseUp` | 4.2 | Drop / commit placement | **Missing** from EditorCanvasViewModel |
| `OnMouseWheel` | 4.2 | Zoom | **Missing** from EditorCanvasViewModel |
| `OnKeyDown` | 4.2 | Escape (abort), Delete (remove), shortcuts | **Missing** from EditorCanvasViewModel |
| Canvas code-behind input forwarding | 5.4 | WPF event → VM method routing | **Missing** (canvas is hosted via `ContentControl`, no input wiring) |
| Drag-drop from catalog to canvas | 4.3, 5.4 | Catalog drag initiation → editor placement | **Missing** |

### 2.6 Missing Commands and Bindings

| Command | Expected Owner | Purpose | Status |
|---|---|---|---|
| `SelectSlotCommand` | `RunSummaryPanelViewModel` | Click slot → select on canvas | **Missing** |
| `GoToEntityCommand` | `IssuePanelViewModel` | Click issue → select affected entity | **Missing** |
| `CommitEditCommand` | `PropertyRowViewModel` | Commit property inline edit | **Missing** |
| `ClearOverrideCommand` | `PropertyRowViewModel` | Revert to inherited value | **Missing** |
| `LoadSnapshotCommand` | `RevisionHistoryViewModel` | View historical snapshot | **Missing** |
| `ApproveRevisionCommand` | `RevisionHistoryViewModel` | Approve current revision | **Missing** |
| Zoom Fit command | Menu (View) | Fit canvas to content | **Stub (IsEnabled=False)** |
| Show Grid command | Menu (View) | Toggle grid overlay | **Stub (IsEnabled=False)** |
| Select Mode command | Menu (Design) | Switch to select mode | **Stub (IsEnabled=False)** |
| Draw Run Mode command | Menu (Design) | Switch to draw-run mode | **Stub (IsEnabled=False)** |

### 2.7 Missing Event Subscriptions

Per spec section 6.2, these ViewModels should subscribe to events:

| ViewModel | Events | Status |
|---|---|---|
| `PropertyInspectorViewModel` | `DesignChangedEvent`, `UndoAppliedEvent`, `RedoAppliedEvent` | **VM missing** |
| `RunSummaryPanelViewModel` | `DesignChangedEvent` | **VM missing** |
| `IssuePanelViewModel` | `DesignChangedEvent`, `ValidationIssuesChangedEvent` | **VM missing** |
| `StatusBarViewModel` | `DesignChangedEvent`, `AutosaveCompletedEvent`, `ValidationIssuesChangedEvent` | **VM missing** |
| `RevisionHistoryViewModel` | `RevisionApprovedEvent` | **VM missing** |

### 2.8 Missing Tests

| Test | Purpose | Status |
|---|---|---|
| `CatalogPanelViewModelTests` | Search/filter, drag initiation | **Missing (VM missing)** |
| `PropertyInspectorViewModelTests` | Selection change, property refresh, inline edit commit | **Missing (VM missing)** |
| `RunSummaryPanelViewModelTests` | Slot list refresh, slot selection sync | **Missing (VM missing)** |
| `IssuePanelViewModelTests` | Severity filter, entity navigation | **Missing (VM missing)** |
| `StatusBarViewModelTests` | Validation counts, save state | **Missing (VM missing)** |
| `RevisionHistoryViewModelTests` | Revision list, approval | **Missing (VM missing)** |
| ShellViewModel: Close command test | Verify close delegates to service | **Missing** |
| ShellViewModel: Dispose unsubscribes | Verify no stale event handlers | **Missing** |
| EditorCanvasViewModel: Move cabinet delegates | Verify MoveCabinetAsync routing | **Missing** |
| EditorCanvasViewModel: ProjectClosed resets state | Verify scene/selection cleared | **Missing** |
| EditorCanvasViewModel: Undo/Redo refresh scene | Verify RefreshScene called | **Missing** |

---

## 3. Smallest Viable Path to a Usable Desktop Editor

The current state is a **well-structured shell with a working canvas and placeholder panels**. The architecture underneath (Application, Domain, Editor, Rendering, Persistence) is solid and conformant. The gap is entirely in Presentation — the panels show static text instead of live data.

### 3.1 What "Usable" Means (Minimum Bar)

A usable desktop editor must allow a user to:
1. Create/open/save a project (already works)
2. See cabinets rendered on the canvas (already works)
3. Select a cabinet and see its properties (partially works — selection works, inspector is placeholder)
4. See the run structure (placeholder)
5. See validation issues (placeholder)
6. Know the project/save state (no status bar)

### 3.2 What Does NOT Need to Change

- `CabinetDesigner.Domain` — no changes required
- `CabinetDesigner.Application` — no changes required (services and DTOs are already in place)
- `CabinetDesigner.Editor` — no changes required
- `CabinetDesigner.Rendering` — no changes required
- `CabinetDesigner.Persistence` — no changes required
- `EditorCanvasViewModel` — core is solid, needs additive input methods only
- `ShellViewModel` — needs child VM injection, but existing command/lifecycle logic stays

---

## 4. Prioritized Implementation Order

### Phase 1: Live Data in Existing Shell (Highest Impact, Lowest Risk)

These replace placeholder strings with real data using existing Application services and events. No new XAML controls required — the existing `ItemsControl` + `BulletItemTemplate` pattern can be temporarily reused.

| Priority | Task | Effort | Dependencies |
|---|---|---|---|
| **P1.1** | `PropertyInspectorViewModel` — read selection from canvas, query properties from service, expose `PropertyRowViewModel` list | Medium | `IRunService.GetRunSummary`, selection sync |
| **P1.2** | `RunSummaryPanelViewModel` — subscribe to `DesignChangedEvent`, query active run summary, expose slot list | Medium | `IRunService.GetRunSummary` |
| **P1.3** | `IssuePanelViewModel` — subscribe to `DesignChangedEvent`, query validation issues, expose severity-filtered list | Medium | Needs `IValidationSummaryService` (verify it exists) |
| **P1.4** | `StatusBarViewModel` — aggregate validation counts, revision label, save state | Small | Needs data from P1.3 |
| **P1.5** | Wire child VMs into `ShellViewModel` constructor and DI registration | Small | P1.1-P1.4 |

### Phase 2: Shell Controls (Replace Placeholders with Real UI)

| Priority | Task | Effort | Dependencies |
|---|---|---|---|
| **P2.1** | `PropertyInspectorPanel.xaml` — key-value grid with override indicators | Medium | P1.1 |
| **P2.2** | `RunSummaryPanel.xaml` — horizontal slot strip with selection | Small | P1.2 |
| **P2.3** | `IssuePanelView.xaml` — severity-colored list with filtering | Medium | P1.3 |
| **P2.4** | `StatusBar.xaml` + wire into `MainWindow.xaml` bottom dock | Small | P1.4 |
| **P2.5** | `CatalogPanel.xaml` — categorized list with search box (no drag yet) | Medium | Needs catalog data source |

### Phase 3: Interaction Completeness

| Priority | Task | Effort | Dependencies |
|---|---|---|---|
| **P3.1** | Canvas input wiring: `OnMouseMove`, `OnMouseUp`, `OnMouseWheel`, `OnKeyDown` | Medium | EditorCanvasViewModel additive |
| **P3.2** | Drag-drop from catalog to canvas | Large | P2.5, P3.1, Editor layer drag context |
| **P3.3** | Property inline editing (`CommitEditCommand`, `ClearOverrideCommand`) | Medium | P2.1 |
| **P3.4** | Mode switching commands (Select, Draw Run) + menu/toolbar binding | Small | Editor mode support |
| **P3.5** | View commands (Zoom Fit, Show Grid) | Small | Rendering layer support |

### Phase 4: Secondary Panels and Polish

| Priority | Task | Effort | Dependencies |
|---|---|---|---|
| **P4.1** | `RevisionHistoryViewModel` + panel | Medium | `ISnapshotService` |
| **P4.2** | `DisplayFormatter` for dimensional values | Small | `ISettingsProvider` |
| **P4.3** | Design-time data constructors for all VMs | Small | All VMs |
| **P4.4** | Full test coverage for all new VMs | Medium | All VMs |

---

## 5. Do Next: Top 5 Concrete Tasks

1. **Create `PropertyInspectorViewModel`** — New file `ViewModels/PropertyInspectorViewModel.cs`. Inject `IRunService` and `IApplicationEventBus`. On selection change (called from `ShellViewModel` when `EditorCanvasViewModel.SelectedCabinetIds` changes), query the selected cabinet's properties and expose them as `PropertyRowViewModel` items. Subscribe to `DesignChangedEvent` for refresh. Include matching `PropertyInspectorViewModelTests.cs`.

2. **Create `RunSummaryPanelViewModel`** — New file `ViewModels/RunSummaryPanelViewModel.cs`. Inject `IRunService` and `IApplicationEventBus`. Subscribe to `DesignChangedEvent`, query active run via `IRunService.GetRunSummary`, expose `RunSlotViewModel` list with `TotalWidthDisplay` and `CabinetCountDisplay`. Include matching tests.

3. **Create `IssuePanelViewModel`** — New file `ViewModels/IssuePanelViewModel.cs`. Inject validation summary service and `IApplicationEventBus`. Subscribe to `DesignChangedEvent`. Expose `IssueRowViewModel` list with severity filtering. Expose `ErrorCount`, `WarningCount`, `InfoCount`. Include matching tests.

4. **Create `StatusBarViewModel` and add status bar to `MainWindow.xaml`** — New file `ViewModels/StatusBarViewModel.cs` with validation counts, revision label, save state. Add a `DockPanel.Dock="Bottom"` status bar region to `MainWindow.xaml` before the main grid. Wire bindings. Include matching tests.

5. **Wire child ViewModels into `ShellViewModel`** — Update `ShellViewModel` constructor to accept `CatalogPanelViewModel`, `PropertyInspectorViewModel`, `RunSummaryPanelViewModel`, `IssuePanelViewModel`, `StatusBarViewModel` as child VMs (per spec section 4.1). Replace static placeholder properties with delegations to child VMs. Update `PresentationServiceRegistration.cs` with new DI registrations. Update `MainWindow.xaml` panel `DataContext` bindings to use child VMs. Update existing tests.
