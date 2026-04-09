# UI Recovery Plan

Date: 2026-04-08
Auditor: Claude Opus 4.6
Scope: WPF Presentation layer — actual code vs. `docs/ai/outputs/presentation.md` spec

---

## 1. What UI Already Exists

### 1.1 Implemented ViewModels

| ViewModel | File | Status |
|---|---|---|
| `ShellViewModel` | `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` | **Implemented** — project lifecycle (New/Open/Save/Close), Undo/Redo, event subscriptions, Dispose pattern |
| `EditorCanvasViewModel` | `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs` | **Implemented** — scene refresh, hit-test selection, AddCabinet/MoveCabinet delegations, event subscriptions |
| `EditorCanvasSessionAdapter` | `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasSessionAdapter.cs` | **Implemented** — bridges `EditorSession` to `IEditorCanvasSession` |
| `EditorCanvasHost` | `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasHost.cs` | **Implemented** — wraps `EditorCanvas` for non-WPF-dependent hosting |

### 1.2 Implemented Infrastructure

| Component | File | Status |
|---|---|---|
| `ObservableObject` | `src/CabinetDesigner.Presentation/ObservableObject.cs` | **Implemented** — base MVVM class |
| `RelayCommand` | `src/CabinetDesigner.Presentation/Commands/RelayCommand.cs` | **Implemented** |
| `AsyncRelayCommand` | `src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs` | **Implemented** |
| `SceneProjector` / `ISceneProjector` | `src/CabinetDesigner.Presentation/Projection/SceneProjector.cs` | **Implemented** — moved from Application per F-1 fix |
| `PresentationServiceRegistration` | `src/CabinetDesigner.Presentation/PresentationServiceRegistration.cs` | **Implemented** — DI registration for presentation services |

### 1.3 Implemented Views

| View | File | Status |
|---|---|---|
| `MainWindow` | `src/CabinetDesigner.Presentation/MainWindow.xaml` + `.xaml.cs` | **Implemented** — 3-row layout: toolbar, canvas, status bar |

### 1.4 Implemented Tests

| Test | File | Status |
|---|---|---|
| `ShellViewModelTests` | `tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs` | **Implemented** — Save delegation, ProjectOpenedEvent |
| `EditorCanvasViewModelTests` | `tests/CabinetDesigner.Tests/Presentation/EditorCanvasViewModelTests.cs` | **Implemented** — AddCabinet delegation, DesignChangedEvent, OnMouseDown selection |

### 1.5 Bootstrap / App Layer

| Component | File | Status |
|---|---|---|
| `App` | `src/CabinetDesigner.App/App.xaml.cs` | **Implemented** — DI wiring, migration runner, MainWindow launch |
| `WpfEditorCanvasHost` | `src/CabinetDesigner.App/WpfEditorCanvasHost.cs` | **Implemented** — WPF-specific host |

---

## 2. What Is Planned in Docs but NOT Implemented

### 2.1 Missing ViewModels (spec §4.3–§4.8)

| ViewModel | Spec Section | Status |
|---|---|---|
| `CatalogPanelViewModel` | §4.3 | **NOT IMPLEMENTED** — cabinet type browsing, search/filter, drag initiation |
| `CatalogItemViewModel` | §4.3 | **NOT IMPLEMENTED** — individual catalog entry |
| `PropertyInspectorViewModel` | §4.4 | **NOT IMPLEMENTED** — selected entity properties, inline editing, override indication |
| `PropertyRowViewModel` | §4.4 | **NOT IMPLEMENTED** — individual property row with edit/clear-override commands |
| `RunSummaryPanelViewModel` | §4.5 | **NOT IMPLEMENTED** — active run overview, slot list, total width |
| `RunSlotViewModel` | §4.5 | **NOT IMPLEMENTED** — individual run slot display |
| `IssuePanelViewModel` | §4.6 | **NOT IMPLEMENTED** — validation issue list, severity filtering, go-to-entity |
| `IssueRowViewModel` | §4.6 | **NOT IMPLEMENTED** — individual issue display |
| `RevisionHistoryViewModel` | §4.7 | **NOT IMPLEMENTED** — revision list, snapshot loading, approve action |
| `RevisionRowViewModel` | §4.7 | **NOT IMPLEMENTED** — individual revision display |
| `StatusBarViewModel` | §4.8 | **NOT IMPLEMENTED** — validation summary counts, revision label, save state |

### 2.2 Missing Views / XAML

The spec (§3.1) defines a full shell layout with docked panels:

| Panel / Region | Status |
|---|---|
| Menu Bar (File, Edit, View, Design, Tools, Help) | **NOT IMPLEMENTED** — current MainWindow has no menu |
| Toolbar (mode-aware: Select, Draw Run, Zoom Fit, Grid) | **NOT IMPLEMENTED** — current toolbar is flat buttons only |
| Catalog Panel (left dock) | **NOT IMPLEMENTED** — no XAML, no ViewModel |
| Property Inspector Panel (right dock) | **NOT IMPLEMENTED** — no XAML, no ViewModel |
| Run Summary Panel (bottom-left dock) | **NOT IMPLEMENTED** — no XAML, no ViewModel |
| Validation Issue Panel (bottom-right dock) | **NOT IMPLEMENTED** — no XAML, no ViewModel |
| Status Bar (structured: issue counts, revision, save state) | **PARTIAL** — current status bar shows `StatusMessage` and `CurrentMode` only |

### 2.3 Missing Presentation Infrastructure

| Component | Spec Section | Status |
|---|---|---|
| `DisplayFormatter` | §7.1 | **NOT IMPLEMENTED** — dimensional formatting (fractional inches, metric), date formatting |
| Design-time data constructors | §9.3 | **NOT IMPLEMENTED** — no `[Obsolete]` parameterless constructors on any ViewModel |
| WPF resource dictionaries / styles | §2.1 | **NOT IMPLEMENTED** — no shared styles, converters, or theme resources |
| Panel visibility persistence | §3.2 | **NOT IMPLEMENTED** — no `ISettingsProvider` integration |

### 2.4 Missing Events

| Event | Defined In Code | Status |
|---|---|---|
| `DesignChangedEvent` | `ApplicationEvents.cs` | **EXISTS** |
| `ProjectOpenedEvent` | `ApplicationEvents.cs` | **EXISTS** |
| `ProjectClosedEvent` | `ApplicationEvents.cs` | **EXISTS** |
| `RevisionApprovedEvent` | `ApplicationEvents.cs` | **EXISTS** |
| `UndoAppliedEvent` | `ApplicationEvents.cs` | **EXISTS** |
| `RedoAppliedEvent` | `ApplicationEvents.cs` | **EXISTS** |
| `ValidationIssuesChangedEvent` | — | **NOT IMPLEMENTED** — referenced in spec §6.2 |
| `SettingsChangedEvent` | — | **NOT IMPLEMENTED** — referenced in spec §6.2 |
| `AutosaveCompletedEvent` | — | **NOT IMPLEMENTED** — referenced in spec §6.2 |

---

## 3. Doc/Code Mismatches

| # | Mismatch | Details |
|---|---|---|
| M-1 | **ShellViewModel child VMs** | Spec §4.1 shows `ShellViewModel` owning 7 child VMs (`Canvas`, `Catalog`, `PropertyInspector`, `RunSummary`, `IssuePanel`, `RevisionHistory`, `StatusBar`). Code only has `Canvas`. |
| M-2 | **ShellViewModel command types** | Spec uses `IAsyncRelayCommand` / `IRelayCommand` interfaces. Code uses concrete `AsyncRelayCommand` / `RelayCommand`. Minor — functional but less testable. |
| M-3 | **MainWindow layout** | Spec §3.1 shows a complex docked panel layout with menu bar, toolbar, left/right/bottom panels, and structured status bar. Code has a simple 3-row grid: flat toolbar, canvas, minimal status bar. |
| M-4 | **EditorCanvasViewModel input methods** | Spec §4.2 defines `OnMouseMove`, `OnMouseDown(double, double, MouseButton)`, `OnMouseUp`, `OnMouseWheel`, `OnKeyDown`. Code only has `OnMouseDown(double, double)` — no button param, no move/up/wheel/key methods. |
| M-5 | **Window title brand** | Spec says "CarpenterStudio" (one word). Code uses "Carpenter Studio" (two words). |
| M-6 | **EditorCanvasViewModel.SceneData** | Spec names the property `SceneData` (type `SceneRenderDataDto`). Code uses `Scene` (type `RenderSceneDto`). |
| M-7 | **PresentationServiceRegistration not used in App** | `App.xaml.cs` manually registers all services instead of calling `AddPresentationServices()`. The registration helper exists but is unused. |

---

## 4. Classification: Presentation-Only vs. Requires Application Changes

### 4.1 Can Be Built Entirely in Presentation

| Item | Rationale |
|---|---|
| `CatalogPanelViewModel` + `CatalogItemViewModel` | Needs a new `ICatalogService` in Application (see §4.2), but the VM itself is pure Presentation |
| `RunSummaryPanelViewModel` + `RunSlotViewModel` | `IRunService.GetRunSummary()` and `RunSummaryDto` already exist |
| `IssuePanelViewModel` + `IssueRowViewModel` | `IValidationSummaryService` already exists with `GetAllIssues()`, `GetIssuesFor()`, `HasManufactureBlockers` |
| `RevisionHistoryViewModel` + `RevisionRowViewModel` | `ISnapshotService` already exists with `GetRevisionHistory()`, `ApproveRevisionAsync()`, `LoadSnapshotAsync()` |
| `StatusBarViewModel` | Consumes existing events + `IValidationSummaryService` |
| `DisplayFormatter` | Pure presentation utility — reads settings, formats strings |
| All XAML views for panels | Pure WPF — binding to ViewModel properties |
| Design-time data constructors | Parameterless constructors on existing/new VMs |
| Menu bar + improved toolbar | XAML + binding to existing commands |
| Panel dock layout | XAML restructuring of `MainWindow.xaml` |
| `EditorCanvasViewModel` input method expansion | Add `OnMouseMove`, `OnMouseUp`, `OnMouseWheel`, `OnKeyDown` — delegates to existing `IEditorInteractionService` |

### 4.2 Requires Small Application Layer Additions

| Item | Application Change Needed |
|---|---|
| `CatalogPanelViewModel` | New `ICatalogService` interface + implementation + `CatalogItemDto` — the domain has cabinet type templates but no application-layer service to query them for the UI |
| `PropertyInspectorViewModel` | Needs `IPropertyInspectionService` or equivalent to read entity properties as key-value pairs. `IRunService.GetRunSummary()` returns run-level data but not per-cabinet property rows with override/inherited indicators |
| `ValidationIssuesChangedEvent` | New event in `ApplicationEvents.cs` — must be published after validation stage completes |
| `SettingsChangedEvent` | New event + `ISettingsProvider` interface in Application |
| `AutosaveCompletedEvent` | New event — published when autosave checkpoint writes succeed |
| Panel visibility persistence | `ISettingsProvider` interface (if not already defined in Infrastructure) |

---

## 5. Phased Implementation Order

### Phase 1 — Foundation & Wiring (no new VMs)

**Goal:** Fix structural issues so new VMs can be added cleanly.

| # | Task | Files to Modify / Create |
|---|---|---|
| 1.1 | Wire `PresentationServiceRegistration` into `App.xaml.cs` — replace manual registrations with `services.AddPresentationServices()` | Modify: `src/CabinetDesigner.App/App.xaml.cs` |
| 1.2 | Create `DisplayFormatter` utility class | Create: `src/CabinetDesigner.Presentation/Formatting/DisplayFormatter.cs` |
| 1.3 | Add missing events: `ValidationIssuesChangedEvent`, `AutosaveCompletedEvent`, `SettingsChangedEvent` | Modify: `src/CabinetDesigner.Application/Events/ApplicationEvents.cs` |
| 1.4 | Expand `EditorCanvasViewModel` input methods: `OnMouseMove`, `OnMouseUp`, `OnMouseWheel`, `OnKeyDown` | Modify: `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs` |
| 1.5 | Add tests for new input methods | Modify: `tests/CabinetDesigner.Tests/Presentation/EditorCanvasViewModelTests.cs` |

### Phase 2 — Status Bar & Issue Panel (consume existing services)

**Goal:** Surface validation data that the Application layer already provides.

| # | Task | Files to Create / Modify |
|---|---|---|
| 2.1 | Create `StatusBarViewModel` | Create: `src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs` |
| 2.2 | Create `IssuePanelViewModel` + `IssueRowViewModel` | Create: `src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs` |
| 2.3 | Add `StatusBarViewModel` and `IssuePanelViewModel` as children of `ShellViewModel` | Modify: `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` |
| 2.4 | Register new VMs in DI | Modify: `src/CabinetDesigner.Presentation/PresentationServiceRegistration.cs` |
| 2.5 | Publish `ValidationIssuesChangedEvent` from orchestrator/validation stage | Modify: `src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs` or orchestrator post-commit |
| 2.6 | Create XAML panels and update MainWindow layout | Modify: `src/CabinetDesigner.Presentation/MainWindow.xaml`; Create: panel UserControls |
| 2.7 | Add tests for `StatusBarViewModel`, `IssuePanelViewModel` | Create: `tests/CabinetDesigner.Tests/Presentation/StatusBarViewModelTests.cs`, `tests/CabinetDesigner.Tests/Presentation/IssuePanelViewModelTests.cs` |

### Phase 3 — Run Summary Panel (consume existing services)

**Goal:** Show active run data — services and DTOs already exist.

| # | Task | Files to Create / Modify |
|---|---|---|
| 3.1 | Create `RunSummaryPanelViewModel` + `RunSlotViewModel` | Create: `src/CabinetDesigner.Presentation/ViewModels/RunSummaryPanelViewModel.cs` |
| 3.2 | Add as child of `ShellViewModel` | Modify: `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` |
| 3.3 | Wire selection sync: slot click → canvas selection | Modify: `EditorCanvasViewModel` (add `SelectEntity` method or use event) |
| 3.4 | Create XAML panel | Create: `src/CabinetDesigner.Presentation/Views/RunSummaryPanel.xaml` + `.xaml.cs` |
| 3.5 | Register in DI | Modify: `src/CabinetDesigner.Presentation/PresentationServiceRegistration.cs` |
| 3.6 | Add tests | Create: `tests/CabinetDesigner.Tests/Presentation/RunSummaryPanelViewModelTests.cs` |

### Phase 4 — Revision History Panel (consume existing services)

**Goal:** Expose snapshot/revision workflow — `ISnapshotService` already exists.

| # | Task | Files to Create / Modify |
|---|---|---|
| 4.1 | Create `RevisionHistoryViewModel` + `RevisionRowViewModel` | Create: `src/CabinetDesigner.Presentation/ViewModels/RevisionHistoryViewModel.cs` |
| 4.2 | Add as child of `ShellViewModel` | Modify: `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` |
| 4.3 | Create XAML panel | Create: `src/CabinetDesigner.Presentation/Views/RevisionHistoryPanel.xaml` + `.xaml.cs` |
| 4.4 | Register in DI | Modify: `src/CabinetDesigner.Presentation/PresentationServiceRegistration.cs` |
| 4.5 | Add tests | Create: `tests/CabinetDesigner.Tests/Presentation/RevisionHistoryViewModelTests.cs` |

### Phase 5 — Catalog Panel (requires new Application service)

**Goal:** Enable drag-and-drop cabinet placement from a browsable catalog.

| # | Task | Files to Create / Modify |
|---|---|---|
| 5.1 | Define `ICatalogService` + `CatalogItemDto` | Create: `src/CabinetDesigner.Application/Services/ICatalogService.cs`, `src/CabinetDesigner.Application/DTOs/CatalogItemDto.cs` |
| 5.2 | Implement `CatalogService` (reads from domain template registry) | Create: `src/CabinetDesigner.Application/Services/CatalogService.cs` |
| 5.3 | Create `CatalogPanelViewModel` + `CatalogItemViewModel` | Create: `src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs` |
| 5.4 | Add as child of `ShellViewModel` | Modify: `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` |
| 5.5 | Wire drag initiation → `EditorCanvasViewModel` placement mode | Modify: `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs` |
| 5.6 | Create XAML panel with drag support (code-behind for `DragDrop.DoDragDrop`) | Create: `src/CabinetDesigner.Presentation/Views/CatalogPanel.xaml` + `.xaml.cs` |
| 5.7 | Register in DI | Modify: `src/CabinetDesigner.Presentation/PresentationServiceRegistration.cs` |
| 5.8 | Add tests | Create: `tests/CabinetDesigner.Tests/Presentation/CatalogPanelViewModelTests.cs` |

### Phase 6 — Property Inspector (requires new Application service)

**Goal:** Enable inline property editing of selected entities.

| # | Task | Files to Create / Modify |
|---|---|---|
| 6.1 | Define `IPropertyInspectionService` + `EntityPropertyDto` | Create: `src/CabinetDesigner.Application/Services/IPropertyInspectionService.cs`, `src/CabinetDesigner.Application/DTOs/EntityPropertyDto.cs` |
| 6.2 | Implement `PropertyInspectionService` (reads cabinet/run properties, override state, inherited-from source) | Create: `src/CabinetDesigner.Application/Services/PropertyInspectionService.cs` |
| 6.3 | Create `PropertyInspectorViewModel` + `PropertyRowViewModel` | Create: `src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs` |
| 6.4 | Add as child of `ShellViewModel` | Modify: `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` |
| 6.5 | Wire selection change → property refresh | Modify: VM subscription to selection changes |
| 6.6 | Create XAML panel | Create: `src/CabinetDesigner.Presentation/Views/PropertyInspectorPanel.xaml` + `.xaml.cs` |
| 6.7 | Register in DI | Modify: `src/CabinetDesigner.Presentation/PresentationServiceRegistration.cs` |
| 6.8 | Add tests | Create: `tests/CabinetDesigner.Tests/Presentation/PropertyInspectorViewModelTests.cs` |

### Phase 7 — Shell Polish

**Goal:** Complete the shell layout to match spec §3.1.

| # | Task | Files to Create / Modify |
|---|---|---|
| 7.1 | Add Menu Bar (File, Edit, View, Design, Tools, Help) | Modify: `src/CabinetDesigner.Presentation/MainWindow.xaml` |
| 7.2 | Convert toolbar to mode-aware design (Select, Draw Run, Zoom Fit, Grid toggle) | Modify: `src/CabinetDesigner.Presentation/MainWindow.xaml` |
| 7.3 | Restructure MainWindow to docked panel layout (Grid with splitters or DockPanel) | Modify: `src/CabinetDesigner.Presentation/MainWindow.xaml` |
| 7.4 | Create shared WPF resource dictionaries (styles, brushes, converters) | Create: `src/CabinetDesigner.Presentation/Themes/Generic.xaml` or similar |
| 7.5 | Add design-time data constructors to all VMs | Modify: all ViewModel `.cs` files |
| 7.6 | Integrate `ISettingsProvider` for panel visibility persistence | Requires: `ISettingsProvider` in Application/Infrastructure |

---

## 6. Summary

**Implemented:** 2 ViewModels (`ShellViewModel`, `EditorCanvasViewModel`), 2 adapters/hosts, 1 XAML window, MVVM infrastructure (commands, ObservableObject), scene projection, DI registration, 2 test files.

**Missing:** 11 ViewModels, 5+ XAML panels/controls, menu bar, improved toolbar, docked panel layout, `DisplayFormatter`, design-time data, 3 application events, 2 application services (`ICatalogService`, `IPropertyInspectionService`), shared styles/themes.

**Complexity breakdown:**
- ~60% of missing work is pure Presentation (ViewModels + XAML consuming existing Application services)
- ~25% requires small Application additions (new services, DTOs, events)
- ~15% is polish (styles, design-time data, panel persistence, menu bar)

The existing foundation is solid — the MVVM pattern, command routing, event subscription/disposal lifecycle, and scene projection are all correctly implemented. Recovery is additive, not corrective.
