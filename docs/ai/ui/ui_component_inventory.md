# UI Component Inventory — Carpenter Studio

Date: 2026-04-08
Source: current code + recovery plan
Priority key: **MVP** = build now | **Next** = phase 2-4 | **Later** = phase 5-7

---

## 1. Shell Structure

### 1.1 MainWindow

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/MainWindow.xaml` + `.xaml.cs` |
| Status | Implemented — 3-row grid (toolbar row, canvas, status bar) |
| Priority | MVP (restructure to docked layout is Next) |
| ViewModel | `ShellViewModel` (set as `DataContext` in `App.xaml.cs`) |
| Current layout | `Grid` with 3 `RowDefinition`s: `Auto` / `*` / `Auto` |
| Target layout | `DockPanel` or nested `Grid` with left/right/bottom panel regions + splitters |

---

## 2. ViewModels

### 2.1 ShellViewModel

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` |
| Status | **Implemented** |
| Priority | MVP |
| Purpose | Root ViewModel. Owns project lifecycle and hosts child ViewModels. |
| State owned | `ActiveProject`, `HasActiveProject`, `WindowTitle`, `PendingProjectName`, `PendingProjectFilePath` |
| Commands | `NewProjectCommand`, `OpenProjectCommand`, `SaveCommand`, `CloseProjectCommand`, `UndoCommand`, `RedoCommand` |
| Application services | `IProjectService`, `IUndoRedoService` |
| Events consumed | `ProjectOpenedEvent`, `ProjectClosedEvent`, `UndoAppliedEvent`, `RedoAppliedEvent` |
| Child VMs (current) | `Canvas` (`EditorCanvasViewModel`) |
| Child VMs (target) | + `Catalog`, `PropertyInspector`, `RunSummary`, `IssuePanel`, `RevisionHistory`, `StatusBar` |
| Notes | Dispose pattern correctly propagates to `Canvas.Dispose()`. Must extend as child VMs are added. |

---

### 2.2 EditorCanvasViewModel

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs` |
| Status | **Implemented** (partial — input handling incomplete) |
| Priority | MVP |
| Purpose | Owns the canvas interaction surface. Bridges the rendering layer to the application layer. Manages selection and scene refresh. |
| State owned | `Scene` (`RenderSceneDto`), `SelectedCabinetIds`, `HoveredCabinetId`, `CurrentMode`, `StatusMessage` |
| Exposed view | `CanvasView` — the WPF render surface from `IEditorCanvasHost` |
| Commands / methods | `AddCabinetToRunAsync`, `MoveCabinetAsync`, `OnMouseDown(x, y)` |
| Application services | `IRunService` |
| Editor layer services | `IEditorCanvasSession`, `IHitTester`, `IEditorCanvasHost` |
| Rendering services | `ISceneProjector` |
| Events consumed | `DesignChangedEvent`, `UndoAppliedEvent`, `RedoAppliedEvent`, `ProjectClosedEvent` |
| Missing input methods | `OnMouseMove`, `OnMouseUp`, `OnMouseWheel`, `OnKeyDown` — needed for drag, hover, zoom, keyboard shortcuts |
| Notes | `OnMouseDown` signature missing `MouseButton` param (mismatch with spec §4.2). Scene property named `Scene` in code, `SceneData` in spec — use code name. |

---

### 2.3 StatusBarViewModel

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs` (to create) |
| Status | **Not implemented** |
| Priority | **Next** (Phase 2) |
| Purpose | Aggregates issue counts, revision label, and save state into a structured status bar. |
| State owned | `ErrorCount`, `WarningCount`, `InfoCount`, `RevisionLabel`, `IsSaved` |
| Application services | `IValidationSummaryService` |
| Events consumed | `ValidationIssuesChangedEvent` (new), `RevisionApprovedEvent`, `ProjectOpenedEvent`, `ProjectClosedEvent` |
| Notes | `ValidationIssuesChangedEvent` must be added to `ApplicationEvents.cs` before this VM can react to live validation changes. |

---

### 2.4 IssuePanelViewModel

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs` (to create) |
| Status | **Not implemented** |
| Priority | **Next** (Phase 2) |
| Purpose | Displays the full validation issue list. Supports severity filtering and entity navigation. |
| State owned | `Issues` (`ObservableCollection<IssueRowViewModel>`), `ActiveSeverityFilter`, `IsEmpty` |
| Commands | `FilterBySeverityCommand`, `GoToEntityCommand` |
| Application services | `IValidationSummaryService` |
| Events consumed | `ValidationIssuesChangedEvent`, `ProjectClosedEvent` |

---

### 2.5 IssueRowViewModel

| Property | Value |
|---|---|
| File | Nested in `IssuePanelViewModel.cs` or own file |
| Status | **Not implemented** |
| Priority | **Next** (Phase 2) |
| Purpose | Represents one validation issue: severity icon, message, affected entity ID. |
| State owned | `Severity`, `Message`, `EntityId`, `CanNavigate` |

---

### 2.6 RunSummaryPanelViewModel

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ViewModels/RunSummaryPanelViewModel.cs` (to create) |
| Status | **Not implemented** |
| Priority | **Next** (Phase 3) |
| Purpose | Shows the active run's slot list, cabinet order, and total width. Drives canvas selection via slot click. |
| State owned | `Slots` (`ObservableCollection<RunSlotViewModel>`), `TotalWidth`, `HasActiveRun` |
| Commands | `SelectSlotCommand` (syncs `EditorCanvasViewModel` selection) |
| Application services | `IRunService` |
| Events consumed | `DesignChangedEvent`, `ProjectClosedEvent` |
| Notes | `IRunService.GetRunSummary()` and `RunSummaryDto` already exist. No Application changes required. |

---

### 2.7 RunSlotViewModel

| Property | Value |
|---|---|
| File | Nested in `RunSummaryPanelViewModel.cs` or own file |
| Status | **Not implemented** |
| Priority | **Next** (Phase 3) |
| Purpose | Represents one slot in a run: cabinet type label, nominal width, position index, filler flag. |
| State owned | `CabinetTypeLabel`, `NominalWidth`, `Index`, `IsFiller`, `IsSelected` |

---

### 2.8 RevisionHistoryViewModel

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ViewModels/RevisionHistoryViewModel.cs` (to create) |
| Status | **Not implemented** |
| Priority | **Next** (Phase 4) |
| Purpose | Lists revision snapshots. Supports loading a snapshot for review and approving the current working revision. |
| State owned | `Revisions` (`ObservableCollection<RevisionRowViewModel>`), `CanApprove` |
| Commands | `LoadSnapshotCommand`, `ApproveRevisionCommand` |
| Application services | `ISnapshotService` |
| Events consumed | `RevisionApprovedEvent`, `ProjectOpenedEvent`, `ProjectClosedEvent` |
| Notes | `ISnapshotService` with `GetRevisionHistory()`, `ApproveRevisionAsync()`, `LoadSnapshotAsync()` already exists. No Application changes required. |

---

### 2.9 RevisionRowViewModel

| Property | Value |
|---|---|
| File | Nested in `RevisionHistoryViewModel.cs` or own file |
| Status | **Not implemented** |
| Priority | **Next** (Phase 4) |
| Purpose | Represents one revision entry: revision number, approval state, timestamp, approver name. |
| State owned | `RevisionId`, `Label`, `ApprovalState`, `CreatedAt`, `ApprovedBy` |

---

### 2.10 CatalogPanelViewModel

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs` (to create) |
| Status | **Not implemented** |
| Priority | **Later** (Phase 5) |
| Purpose | Browsable cabinet type catalog. Supports text search, category filter, and drag initiation for canvas placement. |
| State owned | `Items` (`ObservableCollection<CatalogItemViewModel>`), `SearchText`, `SelectedCategory` |
| Commands | `SearchCommand`, `BeginDragCommand` |
| Application services | `ICatalogService` (new — must be added to Application) |
| Events consumed | `ProjectOpenedEvent`, `ProjectClosedEvent` |
| Notes | Requires new `ICatalogService` + `CatalogItemDto` in Application. Drag initiation requires code-behind in the XAML view (`DragDrop.DoDragDrop`). |

---

### 2.11 CatalogItemViewModel

| Property | Value |
|---|---|
| File | Nested in `CatalogPanelViewModel.cs` or own file |
| Status | **Not implemented** |
| Priority | **Later** (Phase 5) |
| Purpose | One cabinet type entry in the catalog: display name, preview thumbnail, nominal width range, category tag. |
| State owned | `TypeId`, `DisplayName`, `Category`, `MinWidthInches`, `MaxWidthInches`, `ThumbnailPath` |

---

### 2.12 PropertyInspectorViewModel

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs` (to create) |
| Status | **Not implemented** |
| Priority | **Later** (Phase 6) |
| Purpose | Shows editable properties of the currently selected entity. Indicates which values are overrides vs. inherited. Provides per-property override clear. |
| State owned | `EntityLabel`, `Properties` (`ObservableCollection<PropertyRowViewModel>`), `HasSelection` |
| Application services | `IPropertyInspectionService` (new — must be added to Application) |
| Events consumed | Selection change from `EditorCanvasViewModel` (via shared event or direct subscription), `ProjectClosedEvent` |
| Notes | Requires new `IPropertyInspectionService` + `EntityPropertyDto`. Must reflect the 7-level parameter hierarchy (global shop → project → room → run → cabinet → opening → local override). |

---

### 2.13 PropertyRowViewModel

| Property | Value |
|---|---|
| File | Nested in `PropertyInspectorViewModel.cs` or own file |
| Status | **Not implemented** |
| Priority | **Later** (Phase 6) |
| Purpose | One property row: label, current value (formatted), source level, edit mode, clear-override command. |
| State owned | `PropertyKey`, `DisplayLabel`, `DisplayValue`, `SourceLevel`, `IsOverride`, `IsEditing` |
| Commands | `ClearOverrideCommand`, `BeginEditCommand`, `CommitEditCommand` |

---

## 3. Infrastructure ViewModels / Helpers

### 3.1 ObservableObject (base class)

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ObservableObject.cs` |
| Status | **Implemented** |
| Priority | MVP |
| Purpose | Base class for all ViewModels. Implements `INotifyPropertyChanged`, provides `SetProperty` and `OnPropertyChanged`. |

---

### 3.2 RelayCommand

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Commands/RelayCommand.cs` |
| Status | **Implemented** |
| Priority | MVP |
| Purpose | Synchronous `ICommand` implementation. Wraps `Action` + `Func<bool>` CanExecute. |

---

### 3.3 AsyncRelayCommand

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs` |
| Status | **Implemented** |
| Priority | MVP |
| Purpose | Async `ICommand` implementation for `Task`-returning operations (project lifecycle, save, approve). |

---

### 3.4 DisplayFormatter

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Formatting/DisplayFormatter.cs` (to create) |
| Status | **Not implemented** |
| Priority | **Next** |
| Purpose | Formats `Length` values to display strings (fractional inches: `35 3⁄4″`, decimal inches, metric). Formats dates. Used by all panel ViewModels that surface dimensional values. |
| Notes | Pure presentation utility — no domain or Application dependency beyond `Length` DTO. |

---

### 3.5 EditorCanvasSessionAdapter

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasSessionAdapter.cs` |
| Status | **Implemented** |
| Priority | MVP |
| Purpose | Bridges `EditorSession` (Editor layer) to `IEditorCanvasSession` (Presentation contract). Keeps Presentation decoupled from Editor internals. |

---

### 3.6 EditorCanvasHost / WpfEditorCanvasHost

| Property | Value |
|---|---|
| Files | `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasHost.cs` (non-WPF wrapper), `src/CabinetDesigner.App/WpfEditorCanvasHost.cs` (WPF-specific) |
| Status | **Implemented** |
| Priority | MVP |
| Purpose | Wraps the Rendering canvas as a WPF-bindable `View` object. Exposes `UpdateScene` and `UpdateViewport`. Keeps `EditorCanvasViewModel` testable without WPF runtime. |

---

### 3.7 SceneProjector / ISceneProjector

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Projection/SceneProjector.cs` |
| Status | **Implemented** |
| Priority | MVP |
| Purpose | Projects the current design state from Application DTOs into `RenderSceneDto` for the rendering layer. Lives in Presentation, not Application. |

---

### 3.8 PresentationServiceRegistration

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/PresentationServiceRegistration.cs` |
| Status | **Implemented but unused** — `App.xaml.cs` registers services manually instead |
| Priority | **Next** (Phase 1 fix) |
| Purpose | Extension method `AddPresentationServices(IServiceCollection)` — centralises DI registration for all Presentation ViewModels and adapters. |
| Notes | Must be wired into `App.xaml.cs` and kept current as new ViewModels are added. |

---

## 4. XAML Views

### 4.1 MainWindow.xaml

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/MainWindow.xaml` |
| Status | **Implemented** — 3-row scaffold |
| Priority | MVP (restructure to docked layout is **Next**) |
| Current | Row 0: flat toolbar (`DockPanel` with text boxes + buttons). Row 1: `ContentControl` bound to `Canvas.CanvasView`. Row 2: status bar (`DockPanel` with `StatusMessage` and `CurrentMode`). |
| Target | Menu bar → toolbar → docked panels (left/right/bottom) surrounding canvas → structured status bar. Use `Grid` with `GridSplitter` or nested `DockPanel`s. |

---

### 4.2 CatalogPanel.xaml (UserControl)

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Views/CatalogPanel.xaml` (to create) |
| Status | **Not implemented** |
| Priority | **Later** (Phase 5) |
| DataContext | `CatalogPanelViewModel` |
| Notes | Drag initiation requires code-behind (`DragDrop.DoDragDrop`) — acceptable WPF pattern for drag source. |

---

### 4.3 PropertyInspectorPanel.xaml (UserControl)

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Views/PropertyInspectorPanel.xaml` (to create) |
| Status | **Not implemented** |
| Priority | **Later** (Phase 6) |
| DataContext | `PropertyInspectorViewModel` |
| Notes | Property rows need inline edit controls (`TextBox` switching visibility on `IsEditing`). Override indicator uses a value converter or `DataTrigger`. |

---

### 4.4 RunSummaryPanel.xaml (UserControl)

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Views/RunSummaryPanel.xaml` (to create) |
| Status | **Not implemented** |
| Priority | **Next** (Phase 3) |
| DataContext | `RunSummaryPanelViewModel` |

---

### 4.5 IssuePanelView.xaml (UserControl)

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Views/IssuePanelView.xaml` (to create) |
| Status | **Not implemented** |
| Priority | **Next** (Phase 2) |
| DataContext | `IssuePanelViewModel` |

---

### 4.6 RevisionHistoryPanel.xaml (UserControl)

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Views/RevisionHistoryPanel.xaml` (to create) |
| Status | **Not implemented** |
| Priority | **Next** (Phase 4) |
| DataContext | `RevisionHistoryViewModel` |

---

### 4.7 WPF Resource Dictionaries / Themes

| Property | Value |
|---|---|
| File | `src/CabinetDesigner.Presentation/Themes/Generic.xaml` (to create) |
| Status | **Not implemented** |
| Priority | **Later** (Phase 7) |
| Purpose | Shared `Style`s, `Brush`es, `DataTemplate`s, `IValueConverter`s (e.g. severity-to-icon, bool-to-visibility). Reduces per-view repetition. |

---

## 5. Component Dependency Map

```
ShellViewModel
├── EditorCanvasViewModel
│   ├── IRunService
│   ├── IEditorCanvasSession (via EditorCanvasSessionAdapter)
│   ├── IHitTester
│   ├── IEditorCanvasHost (via WpfEditorCanvasHost)
│   └── ISceneProjector (SceneProjector)
├── [Next]  StatusBarViewModel → IValidationSummaryService
├── [Next]  IssuePanelViewModel → IValidationSummaryService
├── [Next]  RunSummaryPanelViewModel → IRunService
├── [Next]  RevisionHistoryViewModel → ISnapshotService
├── [Later] CatalogPanelViewModel → ICatalogService (new)
└── [Later] PropertyInspectorViewModel → IPropertyInspectionService (new)
```

All ViewModels also receive `IApplicationEventBus` for event subscriptions.

---

## 6. Application-Layer Additions Required Before Panel VMs Can Be Built

| Addition | Needed By | Status |
|---|---|---|
| `ValidationIssuesChangedEvent` in `ApplicationEvents.cs` | `StatusBarViewModel`, `IssuePanelViewModel` | Not implemented |
| Publish `ValidationIssuesChangedEvent` from validation stage | Reactive issue refresh | Not implemented |
| `ICatalogService` + `CatalogService` + `CatalogItemDto` | `CatalogPanelViewModel` | Not implemented |
| `IPropertyInspectionService` + `PropertyInspectionService` + `EntityPropertyDto` | `PropertyInspectorViewModel` | Not implemented |
| `AutosaveCompletedEvent`, `SettingsChangedEvent` | `StatusBarViewModel` (save state) | Not implemented |
