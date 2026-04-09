# Carpenter Studio UI Creation Playbook

Version: 1.0  
Date: 2026-04-08  
Target repo: `whatotherguy/carpenter-studio`

---

## 0. Why you ended up with “no UI” even though the playbook was implemented

Your repo **does have a Presentation layer**, but it is only a thin shell today:

- `CabinetDesigner.Presentation` targets `.NET 8` and WPF only on `net8.0-windows` and references `Application`, `Editor`, and `Rendering`.
- `MainWindow.xaml` is a single `Grid` whose entire visible content is just `<ContentControl Content="{Binding Canvas.CanvasView}" />`.
- `ShellViewModel` currently owns project lifecycle commands, undo/redo, pending project fields, and the canvas VM.
- `EditorCanvasViewModel` handles scene refresh, selection, add/move cabinet calls, and status text.
- The richer presentation spec described a full shell with menu bar, toolbar, catalog panel, property inspector, run summary, issue panel, revision history, and status bar — but those panel VMs appear in the spec, not in the implemented code.

So the repo is not “missing Presentation”; it is missing the **actual shell and workflow UI** that the presentation spec described.

---

## 1. Repo-grounded current state

### Implemented now

- `src/CabinetDesigner.Presentation/CabinetDesigner.Presentation.csproj`
- `src/CabinetDesigner.Presentation/MainWindow.xaml`
- `src/CabinetDesigner.Presentation/MainWindow.xaml.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasHost.cs`
- command helpers and scene projection wiring
- tests for `ShellViewModel` and `EditorCanvasViewModel`

### Planned but not yet built into the repo

The presentation design doc defined these top-level child view models under `ShellViewModel`, but they are not present as implementation files today:

- `CatalogPanelViewModel`
- `PropertyInspectorViewModel`
- `RunSummaryPanelViewModel`
- `IssuePanelViewModel`
- `RevisionHistoryViewModel`
- `StatusBarViewModel`

That means your current app has a canvas-centric core, but no real editor shell around it.

---

## 2. Goal of this playbook

Build the missing desktop UI around the working backend/editor/rendering layers **without rewriting the architecture you already implemented**.

This playbook assumes:

- the orchestrator, application services, editor engine, rendering layer, and persistence already exist
- the correct UI stack remains **WPF + MVVM**
- new UI work should land primarily in `src/CabinetDesigner.Presentation/`
- any missing query/service surface needed by the UI can be added to `Application` only when necessary

---

## 3. UI delivery principles for this repo

1. Do not redesign the core architecture.
2. Do not move business logic into code-behind.
3. Preserve `EditorCanvasViewModel` and `ShellViewModel` as seeds, but expand them into the actual shell envisioned by `docs/ai/outputs/presentation.md`.
4. Prefer vertical slices that leave the application visibly better after every prompt.
5. Every slice must end in something the user can see, click, or verify.
6. New ViewModels should consume DTOs and application services, not domain entities.
7. The canvas remains the center of the experience; everything else is a surrounding editor workflow.

---

## 4. Recommended build order

### Phase A — Make the app visibly exist

1. Create the real WPF shell layout in `MainWindow.xaml`
2. Add a menu bar, toolbar, left catalog region, center canvas region, right inspector region, bottom status bar
3. Wire the shell to existing `ShellViewModel` and `EditorCanvasViewModel`
4. Ensure the window launches with visible chrome even before all panels are fully functional

### Phase B — Add the missing editing panels

5. Build `StatusBarViewModel` + status bar UI
6. Build `CatalogPanelViewModel` + searchable cabinet list UI
7. Build `RunSummaryPanelViewModel` + run slot summary UI
8. Build `PropertyInspectorViewModel` + editable inspector UI shell
9. Build `IssuePanelViewModel` + validation list UI
10. Add revision history surface after the editor workflow panels exist

### Phase C — Connect the shell into workflows

11. Selection sync: canvas ↔ run summary ↔ property inspector ↔ issues
12. Command wiring: add cabinet, move cabinet, save/open/new, undo/redo
13. Empty/loading/error states for every panel
14. Keyboard shortcuts and basic UX polish

### Phase D — Harden and polish

15. Design-time data / fixtures for all major VMs
16. UI tests for command availability and panel refresh behavior
17. Visual consistency pass and layout persistence pass

---

## 5. Deliverables this playbook writes

Create these files as you go:

- `docs/ai/ui/ui_recovery_plan.md`
- `docs/ai/ui/ui_screen_inventory.md`
- `docs/ai/ui/ui_component_inventory.md`
- `docs/ai/ui/ui_binding_contracts.md`
- `docs/ai/ui/ui_prompt_pack.md`

You can keep the final prompt pack in one file, but the planning docs should exist separately so later runs stay grounded.

---

## 6. Prompt pack

## U0 — Audit the current Presentation project against the intended shell

**Recommended model:** Opus / highest-reasoning model

**Read:**
- `docs/ai/context/architecture_summary.md`
- `docs/ai/outputs/presentation.md`
- `docs/ai/implementation/final_conformance_review.md`
- `src/CabinetDesigner.Presentation/CabinetDesigner.Presentation.csproj`
- `src/CabinetDesigner.Presentation/MainWindow.xaml`
- `src/CabinetDesigner.Presentation/MainWindow.xaml.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasHost.cs`
- `tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs`
- `tests/CabinetDesigner.Tests/Presentation/EditorCanvasViewModelTests.cs`

**Write:**
- `docs/ai/ui/ui_recovery_plan.md`

**Prompt:**

You are auditing the current WPF presentation implementation against the intended presentation architecture. Use the actual repository code as truth.

Produce `docs/ai/ui/ui_recovery_plan.md` with:
- what UI already exists
- what UI is only planned in docs but not implemented
- which ViewModels, controls, screens, and workflows are missing
- which missing items can be built entirely in Presentation
- which missing items require small additions in Application services / DTOs
- a concrete phased implementation order for UI recovery

Rules:
- do not propose web UI or Avalonia; this repo is WPF
- do not rewrite existing architecture unless absolutely necessary
- call out any doc/code mismatches explicitly
- be precise about file paths to create or modify

---

## U1 — Define the real shell and screen inventory for the current app

**Recommended model:** Sonnet-class planning model

**Read:**
- `docs/ai/ui/ui_recovery_plan.md`
- `docs/ai/outputs/presentation.md`
- `docs/ai/context/architecture_summary.md`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`

**Write:**
- `docs/ai/ui/ui_screen_inventory.md`
- `docs/ai/ui/ui_component_inventory.md`

**Prompt:**

Define the concrete editor shell for the current Carpenter Studio desktop application.

Write:
- `docs/ai/ui/ui_screen_inventory.md`
- `docs/ai/ui/ui_component_inventory.md`

The inventory must be based on the current architecture and current code, not a hypothetical future product.

Include:
- the main shell regions
- primary editor workflows
- each panel / component to create
- purpose of each component
- state owner for each component
- whether each component is MVP, next, or later

The result must stay aligned to WPF + MVVM and the existing `ShellViewModel` / `EditorCanvasViewModel` seeds.

---

## U2 — Define binding contracts before building controls

**Recommended model:** Sonnet-class planning model

**Read:**
- `docs/ai/ui/ui_recovery_plan.md`
- `docs/ai/ui/ui_screen_inventory.md`
- `docs/ai/ui/ui_component_inventory.md`
- `docs/ai/outputs/presentation.md`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- relevant Application service and DTO files already used by Presentation

**Write:**
- `docs/ai/ui/ui_binding_contracts.md`

**Prompt:**

Create `docs/ai/ui/ui_binding_contracts.md`.

For every shell region and panel, define:
- ViewModel class name
- bound properties
- bound commands
- dependencies required from Application / Editor / Rendering
- placeholder state
- loading state
- empty state
- error state
- selection synchronization behavior
- which existing services and DTOs are already enough
- which small query additions are needed

Do not generate implementation code yet. Produce a repo-grounded contract document that future implementation prompts can follow exactly.

---

## U3 — Build the visible shell chrome around the existing canvas

**Recommended model:** Codex / implementation model

**Read:**
- `docs/ai/ui/ui_recovery_plan.md`
- `docs/ai/ui/ui_screen_inventory.md`
- `docs/ai/ui/ui_binding_contracts.md`
- `src/CabinetDesigner.Presentation/MainWindow.xaml`
- `src/CabinetDesigner.Presentation/MainWindow.xaml.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`

**Write / Modify:**
- modify `src/CabinetDesigner.Presentation/MainWindow.xaml`
- modify `src/CabinetDesigner.Presentation/MainWindow.xaml.cs` only if needed for pure input forwarding / shell initialization
- modify `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`

**Prompt:**

Implement the actual WPF shell layout around the existing canvas.

Requirements:
- keep the canvas in the center
- add a menu bar
- add a top toolbar with New, Open, Save, Undo, Redo
- add left, right, and bottom panel regions even if some initially contain placeholder controls
- bind everything through `ShellViewModel`
- keep code-behind minimal and free of business logic
- preserve existing window title binding
- make the app visibly look like an editor instead of just a blank canvas host

Also add any minimal placeholder properties needed in `ShellViewModel` so the window can render all shell regions immediately.

---

## U4 — Implement the status bar and project-state feedback

**Recommended model:** Codex / implementation model

**Read:**
- `docs/ai/ui/ui_binding_contracts.md`
- `docs/ai/outputs/presentation.md`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- current project / validation / revision related services and DTOs

**Write / Modify:**
- `src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs`
- any supporting XAML view/control files under `src/CabinetDesigner.Presentation/Views/`
- modify `ShellViewModel.cs`
- modify `MainWindow.xaml`
- add/update tests under `tests/CabinetDesigner.Tests/Presentation/`

**Prompt:**

Implement `StatusBarViewModel` and render it in the shell.

The status bar must display, at minimum:
- current project / active project name or ready state
- unsaved/saved indicator
- current canvas status text surfaced from the editor canvas VM or shell composition
- placeholders or real counts for validation issues if already available

Wire it in a way that can expand later without changing the shell structure.

Add focused tests for:
- status updates when a project opens/closes
- save state updates after save
- basic command-driven refresh behavior

---

## U5 — Implement the catalog panel for adding cabinets

**Recommended model:** Codex / implementation model

**Read:**
- `docs/ai/ui/ui_binding_contracts.md`
- `docs/ai/outputs/presentation.md`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- current application services / DTOs related to cabinet types, templates, or adding cabinets

**Write / Modify:**
- `src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs`
- supporting item VM(s)
- catalog XAML control(s)
- modify `ShellViewModel.cs`
- modify `MainWindow.xaml`
- add/update tests

**Prompt:**

Implement a left-side catalog panel for cabinet insertion.

Goals:
- searchable list of cabinet items or placeholder catalog items if the repo does not yet expose a real catalog query
- selection or click path that can trigger adding a cabinet to the active run through the existing application service flow
- no business logic in the view
- clear empty state when no project or no run is active

Prefer a design that can start with seeded items and later swap to real data without rewriting the panel.

---

## U6 — Implement the run summary panel

**Recommended model:** Codex / implementation model

**Read:**
- `docs/ai/ui/ui_binding_contracts.md`
- `docs/ai/outputs/presentation.md`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- run-related application services / DTOs

**Write / Modify:**
- `src/CabinetDesigner.Presentation/ViewModels/RunSummaryPanelViewModel.cs`
- supporting XAML control(s)
- modify `ShellViewModel.cs`
- modify `MainWindow.xaml`
- tests

**Prompt:**

Implement a run summary panel that shows the active run and its cabinets/slots.

Requirements:
- reflect the current run using existing run DTOs where possible
- display cabinet order and widths in a compact summary
- selection in the panel should be designed to sync with canvas selection
- if the current backend surface is insufficient, add only the smallest necessary query method in Application

Keep the panel useful even if only one run is supported initially.

---

## U7 — Implement the property inspector shell and first editable fields

**Recommended model:** Codex / implementation model, possibly Sonnet first if query surface is missing

**Read:**
- `docs/ai/ui/ui_binding_contracts.md`
- `docs/ai/outputs/presentation.md`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`
- run/cabinet override services and DTOs

**Write / Modify:**
- `src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs`
- supporting row VM(s)
- supporting XAML control(s)
- modify `ShellViewModel.cs`
- modify `MainWindow.xaml`
- tests

**Prompt:**

Implement the first real property inspector.

Scope for MVP:
- show current selection summary
- show a small, high-confidence set of editable cabinet properties that are already supported by the application layer
- support inline editing through commands/services, not direct state mutation
- include empty state when nothing is selected
- include disabled/read-only state when a property is not editable yet

Do not try to build the final full inspector in one shot. Build a stable, extensible first version.

---

## U8 — Implement the issue panel from current validation data

**Recommended model:** Codex / implementation model

**Read:**
- `docs/ai/ui/ui_binding_contracts.md`
- `docs/ai/outputs/presentation.md`
- validation-related services / DTOs / event types already in the repo

**Write / Modify:**
- `src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs`
- supporting XAML control(s)
- modify `ShellViewModel.cs`
- modify `MainWindow.xaml`
- tests

**Prompt:**

Implement the validation/issues panel in the shell.

Requirements:
- show current issues if validation services are already available
- otherwise create the panel shell and bind it to a minimal placeholder provider with a clear TODO seam
- support severity grouping or filtering if easy
- selecting an issue should be designed to navigate/select the affected entity later, even if the first version only updates shell state

Do not invent fake validation rules in the UI layer.

---

## U9 — Synchronize selection and shell state across panels

**Recommended model:** Sonnet for design, then Codex for implementation

**Read:**
- all current Presentation ViewModels
- `docs/ai/ui/ui_binding_contracts.md`
- tests under `tests/CabinetDesigner.Tests/Presentation/`

**Write / Modify:**
- relevant Presentation ViewModels
- tests

**Prompt:**

Implement shell-level selection synchronization.

Target behavior:
- canvas selection updates inspector selection context
- canvas selection updates run summary selection state
- issue panel navigation can focus the selected entity
- status bar reflects current selection or current ready state

Prefer a simple, explicit composition strategy in `ShellViewModel` over clever hidden coupling.

---

## U10 — Add design-time data and UI-focused tests

**Recommended model:** Codex / implementation model

**Read:**
- all created Presentation ViewModels and views
- `docs/ai/outputs/presentation.md`
- current tests

**Write / Modify:**
- design-time sample support
- new presentation tests

**Prompt:**

Add design-time data and presentation-layer tests for the new shell.

Include tests for:
- shell command availability
- panel visibility and empty states
- project-open / project-close behavior
- selection propagation
- status refresh behavior

Keep tests WPF-light and ViewModel-heavy.

---

## 7. Acceptance gates

A UI slice is not done until all of these are true:

1. The app window visibly shows editor chrome, not just the canvas host.
2. New/Open/Save/Undo/Redo are visibly available in the shell.
3. At least one left-side authoring panel exists and is useful.
4. At least one right-side inspection panel exists and is useful.
5. A bottom status bar exists and reflects real application state.
6. There is a clear empty state when no project is open.
7. The repo has tests for the new ViewModel behavior.

---

## 8. What not to do

- Do not pause to build a full design system before the shell exists.
- Do not replace WPF.
- Do not put orchestrator logic into `MainWindow.xaml.cs`.
- Do not block on perfect catalog data before building the panel skeleton.
- Do not attempt every panel in one giant prompt.
- Do not rewrite the canvas/editor/rendering architecture just to get panels on screen.

---

## 9. Immediate next step

Run **U0**, then **U1**, then **U3**.

That sequence will:
- confirm the exact repo gap
- define the concrete UI inventory
- make the application visibly become an editor in the very next implementation step

Once U3 lands, the repo should finally stop feeling like “all backend, no UI.”
