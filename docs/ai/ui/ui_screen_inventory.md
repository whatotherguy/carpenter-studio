# UI Screen Inventory — Carpenter Studio

Date: 2026-04-08
Source: current code (`MainWindow.xaml`, `ShellViewModel.cs`, `EditorCanvasViewModel.cs`) + recovery plan

---

## 1. Application Shape

Carpenter Studio is a **single-window desktop application**. There is one screen: the editor shell. No splash screen, no separate dialogs yet, no multi-document tabs.

The window is started from `App.xaml.cs`, DI-wired, and bound to `ShellViewModel`. `MainWindow` is the only WPF Window.

---

## 2. Main Shell — Region Map

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  Title Bar   [Project Name — Carpenter Studio]                    [_][□][X]     │
├─────────────────────────────────────────────────────────────────────────────────┤
│  [PLANNED] Menu Bar   File  Edit  View  Design  Tools  Help                     │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Toolbar Row     [New][Open][Save][Close][Undo][Redo]     ← current (flat)      │
│  [PLANNED]       [Select][Draw Run] │ [Zoom Fit][Grid] │ [Save]                 │
├──────────────┬──────────────────────────────────────┬──────────────────────────┤
│              │                                      │                          │
│  [PLANNED]   │                                      │  [PLANNED]               │
│  Catalog     │          Editor Canvas               │  Property Inspector      │
│  Panel       │          (Rendering host)            │  Panel                   │
│              │                                      │                          │
├──────────────┤                                      ├──────────────────────────┤
│              │                                      │                          │
│  [PLANNED]   │                                      │  [PLANNED]               │
│  Run Summary │                                      │  Validation Issue        │
│  Panel       │                                      │  Panel                   │
│              │                                      │                          │
├──────────────┴──────────────────────────────────────┴──────────────────────────┤
│  Status Bar   [StatusMessage]                              [CurrentMode]        │
│  [PLANNED]    [Issues: 0E 2W 1I] │ [Revision: Draft v3] │ [Saved ✓]           │
└─────────────────────────────────────────────────────────────────────────────────┘
```

Legend:
- No tag = **implemented**
- `[PLANNED]` = specified, not yet implemented

---

## 3. Shell Regions

### 3.1 Title Bar

**Status:** Implemented (OS chrome)

WPF `Window.Title` bound to `ShellViewModel.WindowTitle`.

Current output: `"Carpenter Studio"` (no project) or `"<ProjectName> - Carpenter Studio"` (with project).
Spec target: `"<ProjectName> — CarpenterStudio"` (em-dash, no space in brand name).

---

### 3.2 Menu Bar

**Status:** Not implemented

Spec defines: `File | Edit | View | Design | Tools | Help`

State owner: `ShellViewModel` (commands already exist: New, Open, Save, Close, Undo, Redo).
No ViewModel additions needed — menu bar is a XAML-only addition binding to existing commands.

---

### 3.3 Toolbar

**Status:** Partial — flat button row implemented; mode-aware toolbar not implemented

Current: `Row 0` in `MainWindow.xaml` — a `DockPanel` with inline text boxes and buttons (New / Open / Save / Close / Undo / Redo). The project name and file path text boxes are UI scaffolding, not a final UX pattern.

Target: Distinct toolbar groups — Undo/Redo | interaction modes (Select, Draw Run) | view controls (Zoom Fit, Grid toggle) | Save button.

State owner: `ShellViewModel` (project commands), `EditorCanvasViewModel` (mode awareness via `CurrentMode`).

---

### 3.4 Editor Canvas (center)

**Status:** Implemented

`Row 1` in `MainWindow.xaml`. A `ContentControl` bound to `Canvas.CanvasView` — the WPF render surface produced by `IEditorCanvasHost` and populated by `CabinetDesigner.Rendering`.

State owner: `EditorCanvasViewModel` — scene data, selection, hover, mode.

---

### 3.5 Catalog Panel (left dock)

**Status:** Not implemented

Cabinet type browser. Supports search, filter, and drag initiation to place cabinets on the canvas.

State owner: `CatalogPanelViewModel` (to be created). Requires new `ICatalogService` in Application.

---

### 3.6 Property Inspector Panel (right dock)

**Status:** Not implemented

Displays editable properties for the selected entity (cabinet or run). Shows which values are overrides vs. inherited from the parameter hierarchy. Provides per-property clear-override command.

State owner: `PropertyInspectorViewModel` (to be created). Requires new `IPropertyInspectionService` in Application.

---

### 3.7 Run Summary Panel (bottom-left dock)

**Status:** Not implemented

Overview of the active run: slot list, cabinet order, total width, filler allocations. Clicking a slot syncs canvas selection.

State owner: `RunSummaryPanelViewModel` (to be created). Consumes existing `IRunService.GetRunSummary()`.

---

### 3.8 Validation Issue Panel (bottom-right dock)

**Status:** Not implemented

List of all validation issues for the current design. Severity filter (error / warning / info). Click-to-select navigates canvas to the offending entity.

State owner: `IssuePanelViewModel` (to be created). Consumes existing `IValidationSummaryService`.

---

### 3.9 Status Bar

**Status:** Partial

`Row 2` in `MainWindow.xaml`. Current: left-aligned `StatusMessage` (text bound to `Canvas.StatusMessage`) and right-aligned `CurrentMode` (bound to `Canvas.CurrentMode`).

Target: structured three-zone bar — issue summary counts (`0E 2W 1I`), revision state (`Draft v3`), save state (`Saved ✓`).

State owner target: `StatusBarViewModel` (to be created). Consumes events + `IValidationSummaryService`.

---

## 4. Primary Editor Workflows

| # | Workflow | Entry | Canvas action | Affected region(s) |
|---|---|---|---|---|
| W-1 | New project | Toolbar New button → `NewProjectCommand` | Canvas clears | Toolbar, Canvas, Status Bar |
| W-2 | Open project | Toolbar Open button → `OpenProjectCommand` | Scene loads | Toolbar, Canvas, Run Summary, Status Bar |
| W-3 | Save | Toolbar Save button → `SaveCommand` | None | Status Bar (save state) |
| W-4 | Undo / Redo | Toolbar → `UndoCommand` / `RedoCommand` | Scene redraws | Canvas, Run Summary, Issue Panel, Status Bar |
| W-5 | Place cabinet | Drag from Catalog → drop on canvas | Cabinet appears | Canvas, Run Summary, Issue Panel, Status Bar |
| W-6 | Select entity | Mouse down on canvas → `OnMouseDown` | Entity highlighted | Canvas, Property Inspector, Run Summary |
| W-7 | Edit property | Property Inspector → property row edit | Design mutation via Application | Canvas, Issue Panel, Status Bar |
| W-8 | Navigate issue | Issue Panel row click | Canvas pan + select offending entity | Canvas, Property Inspector |
| W-9 | Approve revision | Revision History → Approve | Snapshot frozen | Status Bar (revision label) |
| W-10 | Close project | Toolbar Close → `CloseProjectCommand` | Canvas clears | All panels reset |

Workflows W-1 through W-4 and W-6 are partially functional today. W-5 and W-7 through W-10 require missing ViewModels and Application services.

---

## 5. What Does Not Exist Yet (summary)

| Missing | Blocks |
|---|---|
| Menu bar XAML | W-1 through W-10 (keyboard shortcut surface) |
| Mode-aware toolbar | W-5 (Draw Run mode entry) |
| Docked panel layout in MainWindow.xaml | All panels |
| Catalog Panel | W-5 |
| Property Inspector Panel | W-7 |
| Run Summary Panel | W-6 context, W-4 refresh |
| Validation Issue Panel | W-8 |
| Structured Status Bar | W-8, W-9 revision label, save state |
| `ICatalogService` | W-5 |
| `IPropertyInspectionService` | W-7 |
| `ValidationIssuesChangedEvent` | Issue Panel reactive refresh |
