# Carpenter Studio UI Recovery Prompt Pack

These prompts are designed to build the missing UI on top of the code that already exists in `whatotherguy/carpenter-studio`.

Use them in order.

Recommended model split:
- **Opus / high-reasoning model:** U0, U1, U2
- **Sonnet / structured planning model:** U3, U4
- **Codex / GPT-5.4-mini / implementation model:** U5, U6, U7, U8, U9, U10

---

## U0 — UI Gap Audit Against Actual Repo

**Recommended model:** Opus

```text
You are auditing the existing UI implementation for Carpenter Studio and comparing it to the intended presentation architecture.

READ THESE FILES:
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/presentation.md
- docs/ai/implementation/final_conformance_review.md
- src/CabinetDesigner.Presentation/CabinetDesigner.Presentation.csproj
- src/CabinetDesigner.Presentation/MainWindow.xaml
- src/CabinetDesigner.Presentation/MainWindow.xaml.cs
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasHost.cs
- tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs
- tests/CabinetDesigner.Tests/Presentation/EditorCanvasViewModelTests.cs

TASK:
Produce a repo-grounded audit of what UI exists today versus what the presentation architecture expected.

WRITE TO:
- docs/ai/ui/ui_gap_audit.md

REQUIREMENTS:
1. Be specific about what is already implemented.
2. Be specific about what is only described in docs but missing in code.
3. Identify the smallest viable path to turn the current canvas-only shell into a usable desktop editor UI.
4. Do not propose rewriting working layers outside Presentation unless truly required.
5. Include:
   - existing UI assets
   - missing shell regions
   - missing view models
   - missing WPF views / user controls
   - missing commands and bindings
   - missing tests
   - prioritized implementation order
6. End with a “Do Next” section listing the next 5 concrete UI tasks.

CONSTRAINTS:
- Respect the existing architecture.
- Keep Presentation thin.
- No domain logic in code-behind.
- All design mutations must continue to flow through application services.
```

---

## U1 — Screen Inventory and Component Contract From Existing Code

**Recommended model:** Opus

```text
You are defining the UI that should exist now, based on the current implemented backend/presentation foundation in Carpenter Studio.

READ THESE FILES:
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/presentation.md
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/editor_engine.md
- docs/ai/outputs/rendering.md
- docs/ai/ui/ui_gap_audit.md
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs

TASK:
Define the concrete MVP UI surface that should be built now on top of the current codebase.

WRITE TO:
- docs/ai/ui/ui_inventory_and_contracts.md

REQUIREMENTS:
1. Define the shell layout regions that must exist in MainWindow.
2. Define the MVP screens / regions. Since this is a desktop editor shell, treat docked regions/panels as first-class screens where appropriate.
3. For each region/panel, specify:
   - purpose
   - primary user actions
   - data dependencies
   - owning view model
   - required bindings
   - loading / empty / error states
   - what can be placeholder in phase 1 versus fully functional
4. Define the shared components needed, such as:
   - toolbar
   - project controls strip
   - catalog panel
   - property inspector
   - run summary panel
   - issue panel
   - status bar
   - shell menu
5. Base this on the current code reality, not fantasy future scope.
6. Mark each item as:
   - READY NOW
   - NEEDS VIEWMODEL
   - NEEDS APP SERVICE SUPPORT
   - LATER

IMPORTANT:
The result must be practical enough that implementation prompts can directly consume it.
```

---

## U2 — Presentation Architecture Reconciliation

**Recommended model:** Opus

```text
You are reconciling the intended Presentation architecture with the code that currently exists in Carpenter Studio so implementation can proceed safely.

READ THESE FILES:
- docs/ai/outputs/presentation.md
- docs/ai/ui/ui_gap_audit.md
- docs/ai/ui/ui_inventory_and_contracts.md
- src/CabinetDesigner.Presentation/CabinetDesigner.Presentation.csproj
- src/CabinetDesigner.Presentation/MainWindow.xaml
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs

TASK:
Write a focused architecture reconciliation doc for the UI recovery effort.

WRITE TO:
- docs/ai/ui/ui_architecture_reconciliation.md

REQUIREMENTS:
1. Keep the current `ShellViewModel` and `EditorCanvasViewModel` as anchors unless there is a compelling reason not to.
2. Define which missing view models should be added now.
3. Define which WPF regions should bind directly to `ShellViewModel` child properties.
4. Define where code-behind is allowed and forbidden.
5. Define a safe incremental migration path from the current `MainWindow.xaml` to a full shell.
6. Define test strategy for the new Presentation work.
7. Explicitly call out what should remain placeholder-only in the first UI pass.

OUTPUT SHAPE:
- current structure
- target structure
- delta
- risks
- implementation sequence
```

---

## U3 — Build the Real Main Window Shell

**Recommended model:** Sonnet or Codex

```text
Implement the first real visible desktop shell for Carpenter Studio.

READ THESE FILES:
- docs/ai/ui/ui_gap_audit.md
- docs/ai/ui/ui_inventory_and_contracts.md
- docs/ai/ui/ui_architecture_reconciliation.md
- docs/ai/outputs/presentation.md
- src/CabinetDesigner.Presentation/CabinetDesigner.Presentation.csproj
- src/CabinetDesigner.Presentation/MainWindow.xaml
- src/CabinetDesigner.Presentation/MainWindow.xaml.cs
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasHost.cs

TASK:
Replace the current bare canvas-only `MainWindow.xaml` with a proper WPF editor shell while preserving the existing canvas host in the center.

WRITE / MODIFY:
- src/CabinetDesigner.Presentation/MainWindow.xaml
- src/CabinetDesigner.Presentation/MainWindow.xaml.cs
- any needed resource dictionaries or local styles inside `CabinetDesigner.Presentation`
- docs/ai/implementation/ui_u3_shell_build.md

SHELL MUST INCLUDE:
1. top menu area
2. top toolbar / command strip
3. left catalog pane placeholder
4. central editor canvas region using existing `Canvas.CanvasView`
5. right property inspector placeholder
6. lower right issue panel placeholder
7. lower left or bottom run summary placeholder
8. bottom status bar

BINDING REQUIREMENTS:
- window title stays bound to `WindowTitle`
- save/undo/redo/new/open/close are surfaced visibly if possible with current command set
- canvas remains centered and functional
- placeholder regions must have labels and structured containers, not blank boxes

CONSTRAINTS:
- No business logic in code-behind.
- Keep code-behind limited to safe WPF-only wiring if absolutely needed.
- Do not break existing tests unless replacing them with better tests.
- Make the UI compile cleanly for the current WPF target.

ALSO:
Add concise implementation notes to `docs/ai/implementation/ui_u3_shell_build.md`.
```

---

## U4 — Define Missing Presentation ViewModels and Ownership

**Recommended model:** Sonnet

```text
Define the missing Presentation ViewModels needed to support the new shell in Carpenter Studio.

READ THESE FILES:
- docs/ai/ui/ui_inventory_and_contracts.md
- docs/ai/ui/ui_architecture_reconciliation.md
- docs/ai/outputs/presentation.md
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- src/CabinetDesigner.Application/ (inspect relevant DTOs/services that already exist)

TASK:
Specify the exact ViewModels to add now and how they attach to the shell.

WRITE TO:
- docs/ai/ui/ui_viewmodel_plan.md

REQUIREMENTS:
1. Define the minimal viable ViewModels to add now.
2. For each ViewModel include:
   - filename
   - namespace
   - responsibilities
   - constructor dependencies
   - properties
   - commands
   - events subscribed to
   - tests required
3. Clearly distinguish:
   - production-ready now
   - placeholder adapter now
   - blocked pending more application support
4. Keep the plan incremental and implementable by Codex.
5. Prefer child view models owned by `ShellViewModel`.

LIKELY CANDIDATES:
- CatalogPanelViewModel
- PropertyInspectorViewModel
- RunSummaryPanelViewModel
- IssuePanelViewModel
- StatusBarViewModel

Do not invent unsupported app services unless necessary. Use placeholders when needed, but document them clearly.
```

---

## U5 — Implement Status Bar and Shell Command Surface

**Recommended model:** Codex / GPT-5.4-mini

```text
Implement the first useful non-canvas UI surface for Carpenter Studio: shell command surface and status bar.

READ THESE FILES:
- docs/ai/ui/ui_viewmodel_plan.md
- src/CabinetDesigner.Presentation/MainWindow.xaml
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/Commands/RelayCommand.cs
- src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs
- tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs

TASK:
Add a real status area and surface the existing shell commands in the visible UI.

WRITE / MODIFY:
- src/CabinetDesigner.Presentation/MainWindow.xaml
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- add any minimal supporting view model(s) if needed
- tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs
- docs/ai/implementation/ui_u5_status_and_commands.md

REQUIREMENTS:
1. Show New, Open, Save, Close, Undo, Redo in the visible shell.
2. Show project state in the UI:
   - active project name if present
   - whether a project is open
   - current status text
3. Add a proper bottom status bar with useful shell-level information.
4. Do not fake functionality that already exists in commands.
5. Add or update tests for command visibility-driving properties and state refresh behavior.
6. Keep code simple and architecture-conformant.

IMPORTANT:
If a small new property is needed on `ShellViewModel` for display-only purposes, add it there rather than hiding logic in XAML.
```

---

## U6 — Implement Catalog Panel as MVP Placeholder With Real Hook Points

**Recommended model:** Codex / GPT-5.4-mini

```text
Implement the first version of the Catalog panel for Carpenter Studio.

READ THESE FILES:
- docs/ai/ui/ui_inventory_and_contracts.md
- docs/ai/ui/ui_viewmodel_plan.md
- docs/ai/outputs/presentation.md
- src/CabinetDesigner.Presentation/MainWindow.xaml
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- src/CabinetDesigner.Application/ (inspect for any existing DTOs/services useful for catalog population)

TASK:
Add a left-side catalog panel with a real ViewModel, even if initial data is placeholder-backed.

WRITE / MODIFY:
- new files under `src/CabinetDesigner.Presentation/ViewModels/`
- optional new simple views/user controls under `src/CabinetDesigner.Presentation/`
- src/CabinetDesigner.Presentation/MainWindow.xaml
- tests/CabinetDesigner.Tests/Presentation/
- docs/ai/implementation/ui_u6_catalog_panel.md

REQUIREMENTS:
1. The catalog panel must be visibly real, not just a labeled border.
2. Support:
   - search/filter text
   - item list
   - category/display name/default width style display
3. If the backend catalog is not implemented yet, use clearly marked placeholder data through a presentation-safe approach.
4. Structure the ViewModel so future drag/drop or add actions can attach cleanly.
5. Do not add domain logic.
6. Add tests for filtering behavior and initial state.

IMPORTANT:
Optimize for architecture and future extensibility, but still deliver visible UI now.
```

---

## U7 — Implement Property Inspector as Selection-Aware MVP

**Recommended model:** Codex / GPT-5.4-mini

```text
Implement a useful first-pass Property Inspector for Carpenter Studio.

READ THESE FILES:
- docs/ai/ui/ui_inventory_and_contracts.md
- docs/ai/ui/ui_viewmodel_plan.md
- docs/ai/outputs/presentation.md
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- tests/CabinetDesigner.Tests/Presentation/EditorCanvasViewModelTests.cs
- inspect existing Application DTOs/services for any currently available selected-entity data support

TASK:
Add a right-side Property Inspector panel that reacts to selection state as far as current code allows.

WRITE / MODIFY:
- new files under `src/CabinetDesigner.Presentation/ViewModels/`
- src/CabinetDesigner.Presentation/MainWindow.xaml
- tests/CabinetDesigner.Tests/Presentation/
- docs/ai/implementation/ui_u7_property_inspector.md

REQUIREMENTS:
1. When nothing is selected, show a clear empty state.
2. When a cabinet is selected, show at minimum:
   - selected cabinet ID or label
   - placeholder/editability status
   - any currently available data from existing services or scene DTOs
3. If full property editing is not currently possible, design the inspector to support read-only now and editable later.
4. Connect selection updates from the existing canvas VM/shell structure in a clean way.
5. No domain bypasses.
6. Add tests for selection/no-selection transitions.

IMPORTANT:
If true property editing is blocked by missing app service support, say so in the implementation note and still deliver the read-only shell.
```

---

## U8 — Implement Run Summary and Issue Panel MVP

**Recommended model:** Codex / GPT-5.4-mini

```text
Implement the lower shell panels for Carpenter Studio: Run Summary and Issue Panel.

READ THESE FILES:
- docs/ai/ui/ui_inventory_and_contracts.md
- docs/ai/ui/ui_viewmodel_plan.md
- docs/ai/outputs/presentation.md
- src/CabinetDesigner.Presentation/MainWindow.xaml
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- inspect current Application services/events for available run summary and validation support

TASK:
Add the first useful lower panels to the shell.

WRITE / MODIFY:
- new files under `src/CabinetDesigner.Presentation/ViewModels/`
- src/CabinetDesigner.Presentation/MainWindow.xaml
- tests/CabinetDesigner.Tests/Presentation/
- docs/ai/implementation/ui_u8_runsummary_issues.md

REQUIREMENTS:
1. Run Summary panel:
   - visible list/summary area
   - empty state when no active run is available
   - use real run data if the service support exists
   - otherwise provide a clearly marked placeholder structure, not fake business behavior
2. Issue Panel:
   - visible issue list structure
   - severity column / badge area
   - empty state if no issues are available
   - use current validation support if available
3. Keep both panels incremental and safe.
4. Add tests for empty state behavior and any real populated-state behavior that current code supports.

IMPORTANT:
Do not block delivery just because these panels are not fully backed yet. Build the UI surface and attach real support where possible.
```

---

## U9 — Refactor MainWindow Into Maintainable UserControls

**Recommended model:** Codex / GPT-5.4-mini

```text
Refactor the Carpenter Studio shell UI into maintainable XAML pieces after the MVP shell exists.

READ THESE FILES:
- src/CabinetDesigner.Presentation/MainWindow.xaml
- all newly created Presentation ViewModels and UI files from the UI recovery work
- docs/ai/ui/ui_architecture_reconciliation.md

TASK:
Break the growing shell into maintainable user controls without changing behavior.

WRITE / MODIFY:
- create user controls under `src/CabinetDesigner.Presentation/Views/` or equivalent
- update `MainWindow.xaml`
- add or update any needed resource dictionaries
- docs/ai/implementation/ui_u9_shell_refactor.md

REQUIREMENTS:
1. Extract logical regions into separate controls.
2. Keep bindings clean and MVVM-friendly.
3. Do not move business logic into code-behind.
4. Keep the shell visually and behaviorally equivalent.
5. Make future UI iteration easier.

PREFERRED EXTRACTIONS:
- ShellToolbarView
- CatalogPanelView
- PropertyInspectorView
- RunSummaryPanelView
- IssuePanelView
- StatusBarView
```

---

## U10 — Presentation Test Pass and UI Fit/Finish Pass

**Recommended model:** Codex / GPT-5.4-mini

```text
Do a focused stabilization pass on the new Carpenter Studio UI work.

READ THESE FILES:
- all files changed by U3 through U9
- tests/CabinetDesigner.Tests/Presentation/

TASK:
Improve robustness, clean up obvious UI debt, and strengthen Presentation tests.

WRITE / MODIFY:
- Presentation files as needed
- Presentation tests as needed
- docs/ai/implementation/ui_u10_stabilization.md

REQUIREMENTS:
1. Remove obvious duplication in the new Presentation code.
2. Improve naming consistency.
3. Ensure placeholder regions are clearly labeled as placeholder where applicable.
4. Strengthen tests around:
   - shell state transitions
   - selection-driven UI
   - filtering
   - empty states
   - command state refresh
5. Do not expand scope into unrelated backend refactors.
6. End the implementation note with:
   - what is now visibly usable
   - what is still placeholder
   - what next prompt should tackle
```
