# Carpenter Studio — V1 Finish Prompt Pack

Paste-and-run prompts that take Carpenter Studio from its current
in-progress state to a complete, professional-grade **V1** application:
project + room creation, visual drag-drop canvas editor, full cabinet
CRUD, deterministic pipeline, and cut list export. External pricing,
bids, and install-plan-authoring are explicitly deferred to V2
(`docs/V2_enhancements.md`).

Every prompt in this pack is self-contained. Each one loads
`docs/ai/v1_finish/v1_universal_rules.md` so you never paste rules again —
just paste the prompt and run it.

---

## How to use this pack

1. Run the prompts in the order listed. Each ends with a pointer to the
   next prompt.
2. Never skip the **Read first** list. Those files are how the agent
   grounds itself.
3. Never skip the skill invocations. Every code prompt names specific
   skills.
4. Suggested model split:
   - **Opus / strong reasoning model** for P00 (scope confirmation) and
     P12 (final audit).
   - **Sonnet / Codex / GPT-5.4-mini-level** is fine for P01–P11 because
     each prompt is tightly scoped and lists exact files, behaviors, test
     names, and definition-of-done checkboxes.

---

## Execution order at a glance

| Order | Prompt | Purpose |
|---|---|---|
| 1 | P01 | Fail-closed safety net — remove any lingering skeleton "success" paths |
| 2 | P02 | Decouple costing — pipeline and packaging must not depend on pricing |
| 3 | P03 | UI shell mount + smoke test |
| 4 | P04 | Project + room + wall authoring (create, persist, reload) |
| 5 | P05 | Catalog + canvas drag/drop placement end to end |
| 6 | P06 | Cabinet property inspector — full CRUD on all components |
| 7 | P07 | Part generation + constraint propagation (materials, thickness) without pricing |
| 8 | P08 | Engineering resolution + real workflow state |
| 9 | P09 | Cut list export (CSV + TXT + HTML), deterministic |
| 10 | P10 | Packaging + snapshot integrity without costing dependency |
| 11 | P11 | Validation wiring + service cleanup (no NotImplementedException) |
| 12 | P12 | End-to-end V1 acceptance test + independent audit |

---

## Always-loaded reference documents

Every code prompt in this pack reads these files before doing anything else:

- `docs/ai/v1_finish/v1_universal_rules.md` — mission, engineering rules,
  output rules, skill usage, V2 deferral discipline.
- `docs/V2_enhancements.md` — authoritative V2 deferral list.
- `docs/ai/finish_round/finish_work_queue.md` — historical per-batch spec.
  V1 rules in this pack override any V2-flavored expectations there
  (e.g. costing reconciliation).

If a prompt's instructions appear to conflict with the V1 universal rules,
the universal rules win.

---

# Prompt P01 — Fail-closed safety net

```text
Load rules:
- Read docs/ai/v1_finish/v1_universal_rules.md in full before writing anything.
- Obey its output rules verbatim at the end of this run.

Skills to invoke at the start of this run:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion (before claiming done)

Goal:
Make every pipeline stage "fail closed" in ResolutionMode.Full. A stage that
is unimplemented, that received no valid input, or whose invariants were
violated must return StageResult.Failed with a stable uppercase error code
and stop the pipeline. ResolutionMode.Preview continues to surface the
same conditions as warnings.

Read first:
- src/CabinetDesigner.Application/ResolutionOrchestrator.cs
- src/CabinetDesigner.Application/Pipeline/StageResult.cs
- src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs
- src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs
- src/CabinetDesigner.Domain/ManufacturingContext/ManufacturingModels.cs
- tests/CabinetDesigner.Tests/Pipeline/ResolutionOrchestratorTests.cs (if present)

Implementation requirements:

1. ResolutionOrchestrator.ExecuteStages:
   - When result.IsNotImplemented == true AND context.Mode == ResolutionMode.Full:
     add a ValidationSeverity.Error issue with code "STAGE_NOT_IMPLEMENTED" whose
     Message names the stage ("Stage N '<StageName>' is not implemented.") and
     return false so the pipeline stops immediately.
   - In ResolutionMode.Preview: retain the existing warning behavior — add one
     ValidationSeverity.Warning issue with the same code and message and continue.

2. ManufacturingProjector.Project:
   - When partResult.Parts.Count == 0, emit exactly one
     ManufacturingBlockerCode.NoPartsProduced blocker with a stable message
     "No parts were produced by part generation."
   - If ManufacturingBlockerCode does not already contain NoPartsProduced,
     add it to the enum.

3. CostingStage.Execute:
   - This prompt does NOT implement real costing. Leave CostingStage's existing
     failure paths (COSTING_PRICE_MISSING, COSTING_NO_PARTS) intact.
   - If there is still any branch that returns StageResult.NotImplementedYet,
     replace it with StageResult.Failed(StageNumber, [Error "COSTING_NOT_IMPLEMENTED",
     "CostingStage has no implementation for the current inputs."]). P02 will
     refine this to a non-blocking "not configured" path. For now, skeleton ==
     failure.

4. PackagingStage.Execute:
   - Same pattern. Any NotImplementedYet branch becomes
     StageResult.Failed(StageNumber, [Error "PACKAGING_NOT_IMPLEMENTED", ...]).

5. Every error code must be a stable uppercase string literal, declared once
   as a private const string on the stage and reused.

Required tests (write them first, confirm they fail, then make them pass):

File: tests/CabinetDesigner.Tests/Pipeline/ResolutionOrchestratorIncompleteStageTests.cs
- Execute_NotImplementedStage_InFullMode_FailsPipeline
    * Arrange: orchestrator with a single stage whose Execute returns
      StageResult.NotImplementedYet(stageNumber).
    * Assert: CommandResult.Success == false; Issues contains one Error with
      Code == "STAGE_NOT_IMPLEMENTED".
- Preview_NotImplementedStage_StillWarnsOnly
    * Same arrange, but context.Mode == ResolutionMode.Preview.
    * Assert: CommandResult.Success == true; Issues contains one Warning with
      Code == "STAGE_NOT_IMPLEMENTED".

File: tests/CabinetDesigner.Tests/Projection/ManufacturingProjectorTests.cs
  (create if absent)
- Project_WithNoParts_EmitsNoPartsProducedBlocker
    * Assert blockers list contains a single entry with
      Code == ManufacturingBlockerCode.NoPartsProduced.

Definition of Done (all must be true before you report complete):
- [ ] A Full-mode pipeline run with any skeleton stage returns
      CommandResult.Success == false and a single Error-severity issue
      naming the stage.
- [ ] Preview-mode with the same stage returns Success == true with a Warning.
- [ ] ManufacturingBlockerCode.NoPartsProduced exists and is emitted when parts
      are empty.
- [ ] All new tests green; any existing "skeleton succeeds" tests are removed,
      not commented out.
- [ ] dotnet build -warnaserror and dotnet test are clean.

When done, follow the Output Rules in the universal rules file, then
instruct the user to run P02 next.
```

---

# Prompt P02 — Decouple costing from the V1 pipeline

```text
Load rules:
- Read docs/ai/v1_finish/v1_universal_rules.md in full first.
- Especially section 3 (out of scope for V1) and section 5 (V2 deferral
  discipline). Costing is a V2 feature in V1.
- Obey output rules verbatim at the end.

Skills to invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Goal:
Make the V1 pipeline succeed and produce a deterministic snapshot even when
no pricing catalog is configured. Costing must:
- Not hard-fail when pricing is absent.
- Not block Packaging, Manufacturing, Export, or Validation.
- Emit a clear, non-blocking "not configured" result that downstream consumers
  can read as "cost not available".

Read first:
- src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs
- src/CabinetDesigner.Application/Costing/ICostingPolicy.cs (if present)
- src/CabinetDesigner.Application/Costing/DefaultCostingPolicy.cs (if present)
- src/CabinetDesigner.Application/Services/ICatalogService.cs
- src/CabinetDesigner.Application/Services/CatalogService.cs
- src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs
- src/CabinetDesigner.Application/Pipeline/StageResults/CostingResult.cs (or equivalent)
- src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs

Implementation requirements:

1. Add an explicit "pricing configured" signal on the catalog service:
   - Extend ICatalogService with `bool IsPricingConfigured { get; }`.
   - In CatalogService, return false unless a non-empty internal pricing table
     has been seeded. V1 seeds NO pricing data — return false.
   - Comment the property with `// V2:` noting that V2 enhances this with
     vendor-driven pricing. Reference docs/V2_enhancements.md.

2. Introduce CostingStatus on CostingResult:
   - Add `enum CostingStatus { NotConfigured, Calculated, Failed }` in the
     same namespace as CostingResult.
   - Add a new property `CostingStatus Status { get; init; }` on CostingResult
     with default NotConfigured.
   - Add `string? StatusReason { get; init; }` for human-readable detail.
   - All existing monetary properties remain; when Status == NotConfigured,
     they are all 0m and CabinetBreakdowns is empty.

3. Rewrite CostingStage.Execute flow:
   - First thing: if `!_catalog.IsPricingConfigured`:
     * Populate `context.CostingResult = new CostingResult { Status =
       CostingStatus.NotConfigured, StatusReason = "Pricing catalog not
       configured. Cost calculation skipped. See docs/V2_enhancements.md.",
       ... zeros ... }`.
     * Log once at Information level: "Costing skipped — pricing catalog not
       configured."
     * Return StageResult.Succeeded(StageNumber). DO NOT return Failed.
   - Otherwise, keep the existing material/hardware/labor/install calculation
     and set Status = CostingStatus.Calculated on success.
   - On recoverable calculation problems (missing specific price rows, etc.),
     keep the existing failure behavior with COSTING_PRICE_MISSING — but only
     when IsPricingConfigured is true. A configured catalog with a hole is a
     genuine problem; no catalog at all is V1's intended state.

4. Update PackagingStage so it never fails just because costing is
   NotConfigured:
   - When ContextCostingResult.Status == CostingStatus.NotConfigured,
     serialize a canonical `"costing": {"status": "not_configured"}` object
     instead of the full breakdown. Its bytes must still be included in
     ContentHash so the hash reflects "no pricing" reproducibly.
   - If the existing Packaging logic reads properties like Subtotal/Total to
     decide validity, gate those reads on Status == Calculated.

5. Update any validation rule or projector that depended on a non-zero Total:
   - Search for `CostingResult.Total` / `CostingResult.Subtotal` usages; any
     that enforce "total > 0" must either be removed or guarded on
     `Status == Calculated`.

6. Add a `// V2:` comment at each place where real pricing would reactivate:
   - CatalogService.IsPricingConfigured getter.
   - CostingStage branch that computes totals.
   - PackagingStage serialization path for costing.

Required tests:

File: tests/CabinetDesigner.Tests/Pipeline/CostingStageDecoupledTests.cs
- Execute_NoPricingConfigured_ReturnsSuccessWithNotConfiguredStatus
    * Arrange: ICatalogService stub with IsPricingConfigured == false.
    * Assert: StageResult.Succeeded, context.CostingResult.Status ==
      CostingStatus.NotConfigured, Total == 0m, CabinetBreakdowns empty,
      StatusReason not null/empty.
- Execute_PricingConfigured_StillCalculates
    * Arrange: stub with IsPricingConfigured == true + minimal pricing data.
    * Assert: Status == CostingStatus.Calculated and monetary fields non-zero.

File: tests/CabinetDesigner.Tests/Pipeline/PackagingStageCostDecouplingTests.cs
- Execute_CostNotConfigured_PackagingStillSucceeds
    * Assert StageResult.Succeeded, ContentHash non-empty, snapshot JSON
      contains `"status":"not_configured"` for the costing block.
- Execute_ContentHash_DeterministicWhenCostNotConfigured
    * Run the stage twice with identical inputs and assert ContentHash is
      byte-identical.

File: tests/CabinetDesigner.Tests/Services/CatalogServicePricingTests.cs
- IsPricingConfigured_DefaultV1Instance_IsFalse
    * Assert false for a default-constructed CatalogService.

Definition of Done:
- [ ] ICatalogService.IsPricingConfigured exists and returns false for the
      default V1 catalog.
- [ ] CostingResult has Status + StatusReason; NotConfigured is the default.
- [ ] CostingStage succeeds (not fails) when pricing is not configured.
- [ ] PackagingStage produces a deterministic snapshot whether costing is
      configured or not.
- [ ] No V1-critical stage (Parts, Manufacturing, Install, Validation,
      Packaging) reads non-zero monetary values without gating on
      CostingStatus.Calculated.
- [ ] // V2: comments added at every decoupling point.
- [ ] docs/V2_enhancements.md "V1 Extension Points to Watch" bullet list is
      updated with the new file:line markers.
- [ ] dotnet build -warnaserror + dotnet test clean.

Follow output rules, then point the user to P03.
```

---

# Prompt P03 — Shell mount + smoke test

```text
Load rules:
- Read docs/ai/v1_finish/v1_universal_rules.md.
- Obey output rules.

Skills to invoke:
- superpowers:executing-plans
- superpowers:test-driven-development

Goal:
Every main shell surface — Catalog, Canvas, Property Inspector, Run Summary,
Issue Panel, Status Bar, Shell Toolbar — is mounted in MainWindow.xaml, binds
cleanly, and a smoke test asserts they exist.

Read first:
- src/CabinetDesigner.App/MainWindow.xaml
- src/CabinetDesigner.App/App.xaml
- src/CabinetDesigner.App/MainWindow.xaml.cs
- src/CabinetDesigner.Presentation/Views/*.xaml (confirm each named view exists)
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs

Implementation requirements:

1. MainWindow.xaml layout (Grid):
   Rows:
   - Row 0 (Auto): ShellToolbarView
   - Row 1 (star): three-column layout
       * Col 0 (Auto, MinWidth 240): CatalogPanelView, DataContext=Binding Catalog
       * Col 1 (star): ContentControl Content=Binding Canvas.CanvasView
       * Col 2 (Auto, MinWidth 320): TabControl with three tabs —
           - PropertyInspectorView (Binding PropertyInspector)
           - RunSummaryPanelView (Binding RunSummary)
           - IssuePanelView (Binding IssuePanel)
   - Row 2 (Auto): StatusBarView (Binding StatusBar)

   Use `xmlns:views="clr-namespace:CabinetDesigner.Presentation.Views;assembly=CabinetDesigner.Presentation"`.

2. Remove any inline placeholder StackPanel / Button markup left from prior
   alpha iterations. The shell must reflect only bound views.

3. Ensure App.xaml merges any ResourceDictionary required by the mounted views
   (typography, colors, shared styles). Do not add new styles.

4. MainWindow.xaml.cs:
   - Constructor performs InitializeComponent and sets DataContext via the
     existing composition root. No code-behind logic beyond that.

5. Confirm the launching App.xaml.cs constructs ShellViewModel with required
   dependencies. If it already does, do not touch.

Required tests:

File: tests/CabinetDesigner.Tests/App/MainWindowSmokeTests.cs (create if absent)
- Test class must run under [STAThread] or Xunit's `[Fact]` with the existing
  STA helper used elsewhere in tests/.
- MainWindow_Instantiates_WithoutBindingExceptions
    * Arrange composition root, construct MainWindow, call EnsureHandle or
      equivalent. No unhandled binding exceptions.
- MainWindow_AllExpectedPanels_Present
    * Use VisualTreeHelper / LogicalTreeHelper to assert one instance of each
      of: ShellToolbarView, CatalogPanelView, PropertyInspectorView,
      RunSummaryPanelView, IssuePanelView, StatusBarView is reachable from
      MainWindow's visual tree after measure+arrange.

Definition of Done:
- [ ] App launches and all six panels render.
- [ ] Smoke test green.
- [ ] No binding warnings in Debug output on first paint (add a DataBinding
      trace listener in the smoke test to assert zero SystemError-level
      binding failures).
- [ ] dotnet build -warnaserror + dotnet test clean.

Follow output rules. Next prompt: P04.
```

---

# Prompt P04 — Project, room, and wall authoring

```text
Load rules: docs/ai/v1_finish/v1_universal_rules.md. Obey output rules.

Skills:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Goal:
Users can create a new project, create one or more rooms inside it, add walls
to a room (with length, thickness, and orientation), and have everything
round-trip through persistence. This is a V1 must-have — it is the starting
point of every design session.

Read first:
- src/CabinetDesigner.Domain/SpatialContext/Room.cs
- src/CabinetDesigner.Domain/SpatialContext/Wall.cs
- src/CabinetDesigner.Domain/ProjectContext/*.cs
- src/CabinetDesigner.Application/Services/ProjectService.cs
- src/CabinetDesigner.Application/Services/RoomService.cs (create if absent)
- src/CabinetDesigner.Persistence/Repositories/*.cs (all)
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/ProjectStartupViewModel.cs (if present)
- src/CabinetDesigner.Editor/EditorSession.cs

Implementation requirements:

1. Service layer — create a RoomService with these methods, all async:
   - Task<Room> CreateRoomAsync(string name, Length ceilingHeight, CancellationToken ct)
   - Task<Wall> AddWallAsync(RoomId, Point2D start, Point2D end, Thickness thickness, CancellationToken ct)
   - Task RemoveWallAsync(WallId, CancellationToken ct)
   - Task RenameRoomAsync(RoomId, string newName, CancellationToken ct)
   - Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken ct)
   Each method:
   - Opens the existing unit of work (follow patterns in ProjectService).
   - Validates inputs (non-empty name, positive ceiling height, non-zero wall
     length).
   - Tracks the mutation through IDeltaTracker so undo works.
   - Persists via the working revision repository.

2. ProjectService must support a proper "new project" flow:
   - CreateProjectAsync(string name): creates a Project, initial revision, and
     persists an empty WorkingRevision. Test that round-trip close/open works.
   - OpenProjectAsync(ProjectId): opens the named project and does not
     silently pick "first" — if ambiguous (e.g. multiple projects match),
     throw InvalidOperationException with a clear message.
   - ListProjectsAsync for the startup UI.

3. UI layer — ProjectStartupView (create if absent, add to shell):
   - Lists recent projects (ProjectService.ListProjectsAsync).
   - "New project" button → modal with Name field → creates and opens.
   - "Open project" selection → opens the chosen project and transitions the
     shell out of startup mode into editor mode.
   The main shell (ShellViewModel) has a clear state:
     `ShellMode { Startup, Editor }`. Startup shows ProjectStartupView
     centered; Editor shows the full shell from P03.

4. Room management UI — inside the editor shell, add a RoomsPanelView
   (docked with Catalog on the left, or as a tab there) that:
   - Lists rooms in the current project.
   - "Add room" button → modal → RoomService.CreateRoomAsync.
   - Selecting a room switches the Canvas to that room's context
     (EditorSession.SetActiveRoom(RoomId)).

5. Wall authoring — Canvas-level tool:
   - Shell toolbar gets a "Draw wall" toggle. While active, clicks on the
     canvas create wall endpoints (snap to grid + existing wall endpoints
     via existing SnapResolver).
   - Escape exits wall-drawing mode.
   - Walls render in the Canvas via the existing rendering adapter.

6. Persistence:
   - Ensure RoomMapper persists ceiling height, wall list, wall thickness,
     wall start/end points, and obstacle list in a schema-stable way. If
     anything is currently skipped, add it.
   - A round-trip test (below) will enforce this.

Required tests:

File: tests/CabinetDesigner.Tests/Services/RoomServiceTests.cs
- CreateRoomAsync_PersistsRoom_WithName_AndCeilingHeight
- CreateRoomAsync_RejectsEmptyName
- CreateRoomAsync_RejectsNonPositiveCeilingHeight
- AddWallAsync_PersistsWall_WithExactStartEndThickness
- AddWallAsync_RejectsZeroLengthWall
- RemoveWallAsync_RemovesWall_AndLeavesOtherWallsIntact
- ListRoomsAsync_IncludesRoomsCreatedThisSession

File: tests/CabinetDesigner.Tests/Services/ProjectServiceTests.cs
- CreateProjectAsync_PersistsEmptyWorkingRevision
- CreateProjectAsync_RoundTrip_OpenAgain_ReturnsSameProjectId
- OpenProjectAsync_MultipleMatches_Throws

File: tests/CabinetDesigner.Tests/Persistence/RoomRoundTripTests.cs
- SaveRoomWithWallsAndObstacles_LoadAgain_PreservesAllFields
- WallThicknessAndEndpoints_RoundTripExactly_ToShopTolerance

File: tests/CabinetDesigner.Tests/Presentation/ProjectStartupViewModelTests.cs
- NewProject_Command_CreatesProject_AndEntersEditorMode
- OpenProject_WithSelection_SwitchesShellMode

Definition of Done:
- [ ] User can click "New Project", name it, and see the editor shell load.
- [ ] User can add a room, add walls, and those walls render on the canvas.
- [ ] Closing and reopening the app reloads rooms and walls exactly.
- [ ] All the above tests green.
- [ ] No V2 feature accidentally wired in (no mention of bids, pricing,
      vendor catalogs here).
- [ ] dotnet build -warnaserror + dotnet test clean.

Follow output rules. Next: P05.
```

---

# Prompt P05 — Catalog + canvas drag/drop placement

```text
Load rules: docs/ai/v1_finish/v1_universal_rules.md. Obey output rules.

Skills:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Goal:
User drags a cabinet template from the Catalog panel onto the canvas and drops
it into a run (existing or new). On drop, a Cabinet domain object is created,
added to a Run, placed next to any existing cabinets in that run, and rendered.
Drag-hover previews placement. Invalid drops (no room, zero ceiling height,
would overflow run capacity) are rejected with a status-bar message.

Read first:
- src/CabinetDesigner.Presentation/Views/CatalogPanelView.xaml(.cs)
- src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasSessionAdapter.cs
- src/CabinetDesigner.Editor/EditorSession.cs
- src/CabinetDesigner.Editor/DragContext.cs
- src/CabinetDesigner.Editor/DragExecution.cs
- src/CabinetDesigner.Editor/Snap/*.cs
- src/CabinetDesigner.Application/Services/CatalogService.cs
- src/CabinetDesigner.Application/Services/RunService.cs (create if absent)
- src/CabinetDesigner.Rendering/*.cs (canvas rendering hooks)
- src/CabinetDesigner.Domain/SpatialContext/Run.cs

Implementation requirements:

1. Catalog content (V1):
   CatalogService seeds and exposes at least the following cabinet templates.
   Each has a stable CabinetTypeId, DisplayName, Category, ConstructionMethod,
   NominalWidth, Depth, Height, and DefaultOpenings. No pricing, no vendor.
   - base-standard-24:    Base, Frameless, 24" W x 24" D x 34.5" H, one door
   - base-drawer-18:      Base, Frameless, 18" W x 24" D x 34.5" H, 3 drawers
   - wall-standard-30:    Wall, Frameless, 30" W x 12" D x 30" H, two doors
   - tall-pantry-24:      Tall, Frameless, 24" W x 24" D x 84" H, two doors
   - base-faceframe-24:   Base, FaceFrame, 24" W x 24" D x 34.5" H, one door
   These must be real cabinets that produce real parts through the pipeline.
   Do not mark any as "coming soon" or similar.

2. CatalogPanelView:
   - Displays the five templates as draggable items.
   - Initiates a WPF DragDrop with a payload type "CatalogTemplateDragPayload"
     carrying the CabinetTypeId.

3. Canvas drop target:
   - EditorCanvas handles DragOver / Drop.
   - On DragOver: show placement preview via existing DragContext. Snap to run
     endpoints and to the nearest wall front face using existing SnapResolver.
   - On Drop: resolve CabinetTypeId via CatalogService, then call
     RunService.PlaceCabinetAsync(runId, cabinetTemplate, position).
     - If no run exists at the target location, create a new run hugging the
       nearest wall (RunService.CreateRunAsync based on wall and start point).
     - If the drop would exceed run capacity, abort the drop and push a
       "Cannot place cabinet: run capacity exceeded." message to
       StatusBarViewModel.

4. RunService methods (create file if absent):
   - Task<Run> CreateRunAsync(RoomId, WallId, Length start, Length initialLength, CancellationToken ct)
   - Task<Cabinet> PlaceCabinetAsync(RunId, string cabinetTypeId, CancellationToken ct)
   - Task DeleteCabinetAsync(CabinetId, CancellationToken ct)
   - Task DeleteRunAsync(RunId, CancellationToken ct) — rejects if run has
     cabinets (issue ValidationIssue Error "RUN_NOT_EMPTY"). (If this already
     exists behind a NotImplementedException, finish it here.)

5. EditorCanvasSessionAdapter boundary fix:
   - This file is in the Presentation layer. Strip any imports of
     CabinetDesigner.Domain.Geometry and CabinetDesigner.Domain.Identifiers.
     Presentation speaks Guid + plain UI-facing models. Push CabinetId
     construction / Length conversions into the Editor or Application layer.
   - Verify CabinetDesigner.Presentation.csproj does not reference the
     Domain project.

6. Selection + delete:
   - Left-click on a cabinet in the canvas selects it and highlights it in
     the PropertyInspector (raise EditorSession.SelectionChanged with
     Guid[]).
   - Pressing Delete (or a "Delete" command in the toolbar) calls
     RunService.DeleteCabinetAsync.

7. Resize:
   - Dragging the left or right edge of a selected cabinet resizes its
     NominalWidth, clamped to [minCabinetWidth, maxCabinetWidth] per category
     (constants in Domain — re-use existing bounds; do not invent new ones).
   - Out-of-range pointer snaps to the nearest valid width (this is existing
     "orange handle" behavior — ensure regression holds).

Required tests:

File: tests/CabinetDesigner.Tests/Services/RunServiceTests.cs
- CreateRunAsync_PersistsRun_AtRequestedPosition
- PlaceCabinetAsync_AppendsToEndOfRun_InInsertionOrder
- PlaceCabinetAsync_ExceedsCapacity_Throws_WithStableMessage
- DeleteCabinetAsync_RemovesCabinet_AndCompactsRun
- DeleteRunAsync_Rejects_WhenRunHasCabinets

File: tests/CabinetDesigner.Tests/Services/CatalogServiceTemplatesTests.cs
- Catalog_IncludesAllFiveV1Templates
- Template_base_standard_24_HasExpectedDimensions
- Template_base_faceframe_24_HasFaceFrameConstruction
- (...one per listed V1 template confirming dimensions and construction)

File: tests/CabinetDesigner.Tests/Presentation/CanvasDropTests.cs
- OnDropOfCatalogTemplate_InsideRun_AppendsCabinetToRun
- OnDropOfCatalogTemplate_OutsideAnyRun_CreatesNewRunAndPlacesCabinet
- OnDropWhenCapacityExceeded_AbortsDrop_AndPushesStatusMessage

File: tests/CabinetDesigner.Tests/Presentation/PresentationLayeringTests.cs
  (create if absent)
- EditorCanvasSessionAdapter_DoesNotImportDomainNamespaces
    * Use System.Reflection to confirm no Domain types are referenced from
      the adapter.
- Presentation_Csproj_DoesNotReferenceDomainProject
    * Parse the csproj and assert no <ProjectReference> points at the Domain
      project.

Definition of Done:
- [ ] User can drag any of the five V1 templates from the Catalog into a
      room and see the cabinet appear in the canvas.
- [ ] Resizing a cabinet respects min/max bounds with the orange constraint
      indicator.
- [ ] Deleting a cabinet removes it from the run and the canvas.
- [ ] Presentation layer does not reference Domain.
- [ ] All above tests green.
- [ ] dotnet build -warnaserror + dotnet test clean.

Output rules. Next: P06.
```

---

# Prompt P06 — Cabinet property inspector (full CRUD)

```text
Load rules: docs/ai/v1_finish/v1_universal_rules.md. Obey output rules.

Skills:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Goal:
Selecting a cabinet populates the PropertyInspector with ALL properties a real
cabinet maker needs to edit in V1, and editing them mutates the domain through
commands that are tracked for undo/redo. Property coverage is complete — no
read-only "coming soon" fields.

V1 property coverage (every one must be editable or explicitly labeled
"derived"):
- Identity: DisplayName (derived from CabinetTypeId + instance ordinal, read-only).
- Dimensions: NominalWidth, Depth, Height. (Width is clamped by category bounds.)
- Category: Base | Wall | Tall. Changing category rebuilds the cabinet
  through the catalog (validates dimensions; if invalid, reject with a
  StatusBar message).
- Construction: Frameless | FaceFrame. Changing construction rebuilds parts
  on next pipeline run.
- Openings: ordered list of CabinetOpening (Door | Drawer | FalseFront),
  each with width and height. Add, remove, reorder. Reordering is explicit
  (up/down buttons).
- Shelves: integer ShelfCount override (0–6 typical, clamped).
- Toe Kick Height: Length override (default 4.5"), only for Base/Tall.
- Material overrides: per-part-type (Side, Top, Bottom, Back, Shelf,
  FrameStile, FrameRail, FrameMullion) a MaterialId picker with "(catalog
  default)" as the first option.
- Thickness overrides: per-part-type a decimal Length input with "(catalog
  default)".
- Notes: free-form string persisted on the cabinet as an override value
  under key "notes".

Read first:
- src/CabinetDesigner.Domain/CabinetContext/Cabinet.cs
- src/CabinetDesigner.Domain/CabinetContext/CabinetOpening.cs
- src/CabinetDesigner.Domain/Commands/Modification/*.cs
- src/CabinetDesigner.Presentation/Views/PropertyInspectorView.xaml(.cs)
- src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs
- src/CabinetDesigner.Editor/EditorSession.cs
- src/CabinetDesigner.Application/Pipeline/Stages/InteractionInterpretationStage.cs

Implementation requirements:

1. Domain commands (create each if absent):
   - ResizeCabinetCommand(CabinetId, Length NewWidth, Length NewDepth, Length NewHeight)
   - SetCabinetConstructionCommand(CabinetId, ConstructionMethod)
   - SetCabinetCategoryCommand(CabinetId, CabinetCategory)
   - AddOpeningCommand(CabinetId, OpeningType, Length Width, Length Height, int? insertIndex)
   - RemoveOpeningCommand(CabinetId, OpeningId)
   - ReorderOpeningCommand(CabinetId, OpeningId, int newIndex)
   - SetCabinetOverrideCommand(CabinetId, string OverrideKey, OverrideValue Value)
   - RemoveCabinetOverrideCommand(CabinetId, string OverrideKey)
   Each command:
   - Implements ValidateStructure (reject invalid keys, non-positive lengths,
     opening indices out of range).
   - Is wired through InteractionInterpretationStage.
   - Is tracked by IDeltaTracker (undo/redo round-trips).

2. PropertyInspectorViewModel:
   - Displays every V1 property above for the selected cabinet.
   - Edits fire commands (never mutate domain directly from the viewmodel).
   - Validation (e.g. width clamp) is surfaced in-field (red border + tooltip
     "min 9 inches" or similar) and also via StatusBarViewModel.
   - Selection of multiple cabinets shows only properties that match across
     the selection; differing values show an em-dash placeholder and are
     editable (applies to all selected).

3. Undo/redo:
   - Every inspector edit must be a single IDeltaTracker entry so Ctrl+Z
     undoes the logical edit (not per-keystroke).

Required tests:

File: tests/CabinetDesigner.Tests/Domain/Commands/CabinetModificationCommandsTests.cs
- One [Fact] per command asserting:
  * ValidateStructure rejects invalid inputs with a stable code.
  * Apply produces the expected domain mutation.
  * Apply is deterministic (same inputs → same state).

File: tests/CabinetDesigner.Tests/Presentation/PropertyInspectorViewModelTests.cs
- SelectingSingleCabinet_PopulatesAllV1Properties
- EditingWidth_FiresResizeCabinetCommand_Once
- EditingOpening_AddRemoveReorder_RoundTripsThroughCommandPipeline
- SettingMaterialOverride_FiresSetOverrideCommand
- ClearingMaterialOverride_FiresRemoveOverrideCommand
- MultiSelect_ShowsBlankForDifferingValues_AndEditAppliesToAll
- UndoAfterWidthChange_RestoresPreviousWidth

File: tests/CabinetDesigner.Tests/Presentation/PropertyInspectorCoverageTests.cs
- AllV1InspectorFields_ArePresent_AndEditable
    * Reflects over PropertyInspectorViewModel's observable property set
      and asserts each of the V1 properties (listed in this prompt) is
      present and bindable.

Definition of Done:
- [ ] Every V1 property in the list above is visible and editable in the
      inspector.
- [ ] No read-only "Coming soon" / "Not implemented" placeholder exists.
- [ ] Every inspector edit goes through a domain command tracked for undo.
- [ ] Multi-select editing works.
- [ ] All tests green.
- [ ] dotnet build -warnaserror + dotnet test clean.

Output rules. Next: P07.
```

---

# Prompt P07 — Parts + constraint propagation without pricing

```text
Load rules: docs/ai/v1_finish/v1_universal_rules.md. Obey output rules.

Skills:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Goal:
Every cabinet in a full-mode pipeline run produces a complete, deterministic
list of parts with real material and thickness assigned (from shop defaults,
not placeholders). Hardware assignments are produced when openings exist; when
no hardware catalog entry is available for an opening category, emit a warning
(NOT an error — V2 owns real hardware catalogs).

Read first:
- src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs
- src/CabinetDesigner.Application/Pipeline/Parts/PartGeometry.cs
- src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs
- src/CabinetDesigner.Application/Services/ICatalogService.cs
- src/CabinetDesigner.Application/Services/CatalogService.cs
- src/CabinetDesigner.Domain/Validation/Rules/*.cs

Implementation requirements:

1. PartGeometry.BuildParts(cabinet) must return the V1-complete part set for:
   - Frameless Base / Wall / Tall: LeftSide, RightSide, Top, Bottom, Back,
     AdjustableShelf × ShelfCount (default 1 for base, 2 for wall, 3 for tall).
   - FaceFrame Base / Wall / Tall: all frameless parts above, plus
     FrameStile × 2, FrameRail × 2, FrameMullion × (openings - 1, minimum 0).
   - Opening-derived parts (V1): per Door opening produce a Door part with
     width/height matching opening. Per Drawer opening produce a DrawerFront
     part matching opening; DrawerBox parts (Bottom, Front, Back, LeftSide,
     RightSide) sized from opening minus standard V1 clearances:
     * box height = opening height - 1"
     * box depth = cabinet depth - 1"
     * box width = opening width - 1"
     * box bottom thickness = 0.25"
     * box side thickness = 0.5"
     These constants live in PartGeometry as named `static readonly Length`
     fields commented as V1 shop defaults (`// V2:` for vendor-driven
     clearances).

   All Width/Height values computed deterministically from cabinet envelope
   minus panel offsets. Part IDs are stable:
   `part:{cabinet.Id.Value:D}:{PartType}:{ordinal}`.
   Ordering: by (CabinetId, PartType, ordinal).

2. ConstraintPropagationStage must:
   - For each GeneratedPart, resolve MaterialId and Thickness using
     precedence:
     1. cabinet.MaterialOverrides[PartType] (if set)
     2. run.MaterialOverrides[PartType] (if set)
     3. catalog.ResolvePartMaterial(partType, cabinet.Category, cabinet.Construction)
   - Thickness uses the same precedence with ResolvePartThickness.
   - Emit one MaterialAssignment(PartId, MaterialId, Thickness, GrainDirection)
     per part.
   - If no material resolves, emit ConstraintViolation(MATERIAL_UNRESOLVED, Error).
   - For each CabinetOpening, call
     catalog.ResolveHardwareForOpening(openingId, category). If the list is
     non-empty, emit HardwareAssignment. If empty and category would
     typically use hardware (Door → hinges, Drawer → slides), emit
     ConstraintViolation("NO_HARDWARE_CATALOG", Warning, message
     "No hardware catalog configured for <category> opening. V2 will
     integrate vendor hardware."). Do NOT fail the stage.

3. CatalogService V1 seed data (materials + thickness only, NO pricing):
   - Materials: `Default-3/4-Plywood`, `Default-1/2-Plywood`,
     `Default-1/4-Plywood`, `Default-Hardwood-Maple`.
     These are stable MaterialIds.
   - ResolvePartMaterial defaults:
     * LeftSide, RightSide, Top, Bottom, AdjustableShelf, DrawerFront, Door →
       `Default-3/4-Plywood` (Frameless). For FaceFrame Stile/Rail/Mullion →
       `Default-Hardwood-Maple`.
     * Back → `Default-1/4-Plywood`.
     * DrawerBox sides/front/back → `Default-1/2-Plywood`.
     * DrawerBox bottom → `Default-1/4-Plywood`.
   - ResolvePartThickness defaults reflect above: 0.75 / 0.5 / 0.25 inches.
   - ResolveHardwareForOpening returns an empty list for V1 (no hardware
     catalog). Mark the method with `// V2:`.
   - IsPricingConfigured → false (per P02).

4. Domain validation rules:
   - MaterialAssignmentRule (Category=Constraints, Scope=Part, PreviewSafe=false)
     evaluates MATERIAL_UNRESOLVED violations and emits Error issues.
   - HardwareAssignmentRule (Category=Constraints, Scope=Cabinet,
     PreviewSafe=false) evaluates NO_HARDWARE_CATALOG and emits Warning
     issues.
   Both take their snapshot input from ValidationContext.Constraints.

Required tests:

File: tests/CabinetDesigner.Tests/Pipeline/PartGenerationStageTests.cs
- Execute_ForFramelessBase_Produces_ExpectedParts
- Execute_ForFaceFrameBase_AddsStilesRailsAndMullions
- Execute_ForTall_ShelfCountDefaultsToThree
- Execute_ForWall_ShelfCountDefaultsToTwo
- Execute_ForCabinetWithTwoDoorOpenings_ProducesTwoDoorParts
- Execute_ForCabinetWithThreeDrawers_ProducesThreeDrawerFronts_AndThreeDrawerBoxSets
- Execute_DimensionsAreDeterministic_AcrossRepeatedRuns
- Execute_LabelsAreStable_WhenTwoCabinetsShareType
- Execute_WithEmptySpatialPlacements_Fails_WithPartGenEmpty

File: tests/CabinetDesigner.Tests/Pipeline/ConstraintPropagationStageTests.cs
- Execute_AssignsDefaultMaterial_FromCatalog_ForEveryPart
- Execute_RespectsCabinetLevelOverride
- Execute_RespectsRunLevelOverride_WhenCabinetOverrideAbsent
- Execute_EmitsError_WhenPartHasNoResolution
- Execute_HardwareEmpty_EmitsWarningViolation_NotError
- Execute_IsDeterministic

File: tests/CabinetDesigner.Tests/Validation/MaterialAssignmentRuleTests.cs
- Evaluate_NoViolations_ReturnsEmpty
- Evaluate_MaterialUnresolved_EmitsError

File: tests/CabinetDesigner.Tests/Validation/HardwareAssignmentRuleTests.cs
- Evaluate_NoHardwareCatalog_EmitsWarning

Definition of Done:
- [ ] Every cabinet in the five V1 templates produces a non-empty part list.
- [ ] Every GeneratedPart has a non-default MaterialId and a sane Thickness.
- [ ] No V1 scenario fails the pipeline due to missing hardware — it warns.
- [ ] Hardware path is marked `// V2:` and listed in docs/V2_enhancements.md.
- [ ] All tests green; existing determinism tests still pass.
- [ ] dotnet build -warnaserror + dotnet test clean.

Output rules. Next: P08.
```

---

# Prompt P08 — Engineering resolution + real workflow state

```text
Load rules: docs/ai/v1_finish/v1_universal_rules.md. Obey output rules.

Skills:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Goal:
EngineeringResolutionStage produces one assembly per cabinet, computes filler
requirements for run gaps, and derives end conditions from wall attachment
metadata. ValidationStage builds a real WorkflowStateSnapshot from persisted
revision / checkpoint state. No hardcoded "Draft" placeholders.

Read first:
- src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs
- src/CabinetDesigner.Application/ResolutionOrchestrator.cs
- src/CabinetDesigner.Application/ApplicationServiceRegistration.cs
- src/CabinetDesigner.Application/State/IDesignStateStore.cs
- src/CabinetDesigner.Domain/Validation/WorkflowStateSnapshot.cs

Implementation requirements:

1. EngineeringResolutionStage(IDesignStateStore stateStore):
   For each Run in stateStore.GetAllRuns():
   - For each cabinet in the run: emit AssemblyResolution(cabinetId,
     assemblyType, parameters).
       * assemblyType = `"{Category}CabinetAssembly"` (e.g. "BaseCabinetAssembly").
       * parameters is a sorted dictionary<string,string> using invariant
         culture. Keys include `"toe_kick"` (inches), `"shelf_count"`,
         `"door_count"`, `"drawer_count"`.
   - Compute gap = run.Capacity - run.OccupiedLength. When gap >
     Length.FromInches(0.125m), emit one FillerRequirement(runId, gap,
     description "Run end filler").
   - Emit EndConditionUpdate(runId, LeftEndCondition, RightEndCondition) derived
     from stateStore.GetWall(run.WallId):
       * If wall exists and has attachment metadata at run endpoint →
         EndCondition.Wall.
       * If neighboring run abuts → EndCondition.AdjacentCabinet.
       * Otherwise EndCondition.OpenEnd.
   Ordering of emitted items is stable (by run.Id then cabinet.Id).

2. ValidationStage.BuildContext:
   - Replace any hardcoded WorkflowStateSnapshot with:
     ```
     new WorkflowStateSnapshot(
         ApprovalState: currentState?.Revision.State.ToString() ?? "Draft",
         HasUnapprovedChanges: currentState?.Checkpoint is { IsClean: false },
         HasPendingManufactureBlockers: context.ManufacturingResult.Plan.Readiness.Blockers.Count > 0)
     ```
   - currentState is supplied via ICurrentPersistedProjectState (inject it;
     ResolutionOrchestrator threads it through).
   - Also populate the constraint snapshot list on ValidationContext
     (required by P07's rules).

3. DI wiring:
   - ApplicationServiceRegistration must register the new stage
     constructors and ICurrentPersistedProjectState.

Required tests:

File: tests/CabinetDesigner.Tests/Pipeline/EngineeringResolutionStageTests.cs
- Execute_ProducesOneAssemblyPerCabinet_InStableOrder
- Execute_EmitsFiller_WhenRunEndGapExceedsTolerance
- Execute_NoFiller_WhenGapWithinTolerance
- Execute_EndConditions_WallMetadataPresent_EmitsWall
- Execute_EndConditions_AdjacentRun_EmitsAdjacentCabinet
- Execute_EndConditions_NoAttachment_EmitsOpenEnd
- Execute_IsDeterministic

File: tests/CabinetDesigner.Tests/Pipeline/ValidationStageWorkflowTests.cs
- Execute_WorkflowSnapshot_ReflectsDraftState_WhenNoProject
- Execute_WorkflowSnapshot_ReflectsCheckpointDirtyFlag
- Execute_WorkflowSnapshot_ReflectsManufacturingBlockers

Definition of Done:
- [ ] Install projector receives filler steps for runs that need them.
- [ ] WorkflowUnapprovedChangesRule fires when checkpoint is dirty.
- [ ] Hardcoded "Draft" string is gone from ValidationStage.
- [ ] All above tests green; existing determinism tests still pass.
- [ ] dotnet build -warnaserror + dotnet test clean.

Output rules. Next: P09.
```

---

# Prompt P09 — Cut list export (CSV + TXT + HTML)

```text
Load rules: docs/ai/v1_finish/v1_universal_rules.md. Obey output rules.

Skills:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Goal:
Generate a deterministic cut list export in three formats — CSV, plain text,
and printable HTML — from the manufacturing plan produced by the pipeline.
This is the single most important V1 deliverable: it's what the shop actually
uses.

Read first:
- src/CabinetDesigner.Application/Pipeline/Stages/ManufacturingPlanningStage.cs
- src/CabinetDesigner.Application/Pipeline/StageResults/ManufacturingPlanResult.cs
- src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs
- src/CabinetDesigner.Application/Services/SnapshotService.cs

Implementation requirements:

1. Create a new assembly-local namespace:
   `src/CabinetDesigner.Application/Export/` containing:
   - ICutListExporter.cs
   - CutListExporter.cs (implements all three formats)
   - CutListExportRequest.cs (record)
   - CutListExportResult.cs (record with byte arrays for each format)

2. ICutListExporter:
   ```
   public interface ICutListExporter
   {
       CutListExportResult Export(CutListExportRequest request);
   }
   ```
   CutListExportRequest carries:
   - ManufacturingPlan Plan
   - ProjectSummary Summary (projectName, revisionLabel, generatedAtUtc,
     generatedBy)
   - IReadOnlyList<MaterialAssignment> Materials
   CutListExportResult carries:
   - byte[] Csv
   - byte[] Txt
   - byte[] Html
   - string ContentHash (SHA-256 of the concatenated CSV|TXT|HTML bytes)

3. CSV format (comma, CRLF line endings, UTF-8 BOM):
   Header row:
   `Cabinet,PartType,Label,Width(in),Height(in),Thickness(in),Material,GrainDirection,EdgeTop,EdgeBottom,EdgeLeft,EdgeRight,Qty`
   One row per GeneratedPart. Numeric values use CultureInfo.InvariantCulture,
   3 decimal places. Quote any field containing a comma or quote (RFC 4180).
   Ordering: CabinetDisplayName asc, PartType asc (ordinal), Label asc
   (ordinal).
   Qty is always 1 in V1 (per-ordinal rows; duplicate parts get separate
   rows). `// V2:` comment at the Qty logic explaining that V2 may collapse.

4. TXT format (plain text, LF line endings, UTF-8 no BOM):
   ```
   Carpenter Studio Cut List
   Project: <name>
   Revision: <label>
   Generated: <ISO-8601 UTC>
   ------------------------------------------------
   Cabinet: <DisplayName>
     - <Label>: <Width>" x <Height>" x <Thickness>" <Material> [grain: <Dir>]
     - ...
   Cabinet: <DisplayName>
     - ...
   ------------------------------------------------
   Total parts: <N>
   ```
   Numbers: 3 decimals, invariant culture. Sorted deterministically.

5. HTML format:
   Single self-contained HTML5 document with inline CSS (no external assets).
   Contains:
   - `<title>Cut List — <project> — <revision></title>`
   - A project summary header.
   - One `<section>` per cabinet with an `<h2>` (cabinet name) and a `<table>`
     listing parts (same columns as CSV).
   - A footer with "Total parts: N" and "Generated: <ISO-8601 UTC>".
   - No JavaScript. Printable via browser print dialog.
   Content must be byte-deterministic: attribute order stable, no timestamp
   beyond the generatedAtUtc passed in request, no random IDs.

6. Determinism:
   - The exporter must be pure: no DateTime.Now, no clock, no locale that
     depends on environment. All culture-sensitive formatting uses
     InvariantCulture.
   - ContentHash is `Convert.ToHexString(SHA256.HashData(<bytes>)).ToLowerInvariant()`
     of the three buffers concatenated in order: CSV, TXT, HTML.

7. UI integration — Shell toolbar button "Export Cut List":
   - Opens a Save-location folder picker (WPF).
   - Runs the pipeline in Full mode on the current project.
   - If pipeline fails validation: show the IssuePanel and push a StatusBar
     message; do not produce partial exports.
   - If it succeeds: call ICutListExporter.Export and write three files
     `<project>-<revision>.cutlist.csv`, `.txt`, `.html` to the chosen
     folder. Push a StatusBar message "Cut list exported to <folder>."
   - A second button "Preview HTML" writes the HTML to a temp file and
     Process.Start's the default browser.

8. DI:
   - Register ICutListExporter in ApplicationServiceRegistration.

Required tests:

File: tests/CabinetDesigner.Tests/Export/CutListExporterTests.cs
- Export_WithSampleManufacturingPlan_ProducesNonEmptyCsvTxtHtml
- Export_IsDeterministic_AcrossRepeatedRuns (byte-exact equality)
- Export_CsvHeader_MatchesSpec
- Export_CsvRowCount_MatchesPartCount
- Export_TxtContainsOneSectionPerCabinet
- Export_HtmlIsSelfContained_NoExternalReferences
- Export_ContentHashChanges_WhenAPartIsRemoved
- Export_HandlesCabinetWithCommaInName (RFC 4180 quoting)
- Export_UsesInvariantCulture (simulate German decimal separator; dims still
  use `.`)

File: tests/CabinetDesigner.Tests/Export/CutListExportRoundTripTests.cs
- EndToEndPipeline_ExportForSampleRoom_ContentHashIsDeterministic
    * Build a sample project with 1 base + 1 wall cabinet, run the full
      pipeline, export, re-run, assert ContentHash equal.

File: tests/CabinetDesigner.Tests/Presentation/ShellExportCommandTests.cs
- ExportCutList_InvalidDesign_DoesNotProduceFiles_AndSurfacesIssues
- ExportCutList_ValidDesign_WritesThreeFiles_ToChosenFolder

Definition of Done:
- [ ] User clicks "Export Cut List", picks a folder, and gets three files
      with deterministic, inspect-able content.
- [ ] Running the same project twice produces byte-identical output.
- [ ] HTML opens in a browser and prints cleanly.
- [ ] All tests green.
- [ ] dotnet build -warnaserror + dotnet test clean.

Output rules. Next: P10.
```

---

# Prompt P10 — Packaging + snapshot integrity without costing

```text
Load rules: docs/ai/v1_finish/v1_universal_rules.md. Obey output rules.

Skills:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Goal:
PackagingStage produces a deterministic, hash-addressed ApprovedSnapshot that
reflects the full V1 pipeline output and rejects invalid designs. Snapshot
must NOT require CostingResult.Status == Calculated; it simply serializes
whatever status is present.

Read first:
- src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs
- src/CabinetDesigner.Application/Packaging/DeterministicJson.cs (create if absent)
- src/CabinetDesigner.Application/Services/SnapshotService.cs
- src/CabinetDesigner.Domain/ProjectContext/ApprovedSnapshot.cs
- src/CabinetDesigner.Persistence/Repositories/SnapshotRepository.cs (or equivalent)

Implementation requirements:

1. DeterministicJson helper (create if absent):
   - Serializes using System.Text.Json with:
     * JsonSerializerOptions with sorted property order (custom
       JsonTypeInfoResolver that sorts JsonPropertyInfo by Name Ordinal).
     * No indentation.
     * InvariantCulture.
     * Explicit schema_version = 1 on every top-level artifact.
   - Expose `byte[] Serialize<T>(T value)`.

2. PackagingStage.Execute:
   - Collect artifacts: parts, manufacturing_plan, install_plan,
     constraint_assignments, validation_summary, costing.
     * costing is serialized as `{ "status": "not_configured" }` when
       CostingResult.Status == NotConfigured. Otherwise serialize its full
       body.
   - Serialize each with DeterministicJson. Concatenate bytes in the fixed
     order: parts, manufacturing_plan, install_plan,
     constraint_assignments, validation_summary, costing.
   - ContentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant().
   - Build PackagingResult:
     * SnapshotId = `$"snap:{revisionId.Value:D}:{contentHash[..16]}"`
     * RevisionId, CreatedAt (from IClock), ContentHash,
     * Summary = SnapshotSummary(cabinetCount, runCount, partCount,
       validationIssueCount, CostingStatus).
   - If context.ValidationResult.Result.IsValid == false:
     StageResult.Failed(11, [Error "PACKAGING_INVALID_DESIGN"]).

3. SnapshotService.ApproveRevisionAsync:
   - Reads PackagingResult (via the existing singleton store pattern used
     for ValidationResultStore; add IPackagingResultStore if missing).
   - Persists the ApprovedSnapshot including ContentHash.
   - ApprovedSnapshot gains `string ContentHash { get; init; }` (additive).

4. Persistence:
   - SnapshotRepository schema stores ContentHash. Add a migration/row
     column following the existing persistence patterns (SQLite or the
     repo's current storage mechanism — read before writing).
   - Round-trip: save snapshot, load back, assert ContentHash equal.

Required tests:

File: tests/CabinetDesigner.Tests/Pipeline/PackagingStageTests.cs
- Execute_ProducesStableHash_ForIdenticalInputs
- Execute_HashChanges_WhenPartListChanges
- Execute_HashChanges_WhenMaterialChanges
- Execute_FailsStage_WhenValidationIsInvalid
- Execute_SnapshotSummary_ReflectsCounts
- Execute_CostingNotConfigured_ProducesStableHash
- Execute_CostingCalculated_ProducesDifferentHashThanNotConfigured

File: tests/CabinetDesigner.Tests/Persistence/SnapshotApprovalContentTests.cs
- ApproveRevision_SnapshotBlob_ContainsManufacturingCutList
- ApproveRevision_ContentHash_StoredAndReturnedFromRead
- ApproveRevision_InvalidDesign_IsRejected

Definition of Done:
- [ ] Two runs of the same project produce identical ContentHash whether or
      not costing is configured.
- [ ] Approval is rejected when validation is not valid.
- [ ] Snapshot row in persistence includes ContentHash and round-trips.
- [ ] Any old path that required Costing.Status == Calculated to package is
      removed.
- [ ] All tests green.
- [ ] dotnet build -warnaserror + dotnet test clean.

Output rules. Next: P11.
```

---

# Prompt P11 — Validation wiring + service cleanup

```text
Load rules: docs/ai/v1_finish/v1_universal_rules.md. Obey output rules.

Skills:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Goal:
Route manufacturing and install blockers through the rules engine, finish any
remaining NotImplementedException service methods, and remove stale
`catch (NotImplementedException)` clauses. AsyncRelayCommand surfaces all
exceptions.

Read first:
- src/CabinetDesigner.Domain/Validation/Rules/*.cs
- src/CabinetDesigner.Domain/Validation/ValidationContext.cs
- src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs
- src/CabinetDesigner.Application/Services/RunService.cs
- src/CabinetDesigner.Application/Services/ProjectService.cs
- src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs
- src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs
- src/CabinetDesigner.Persistence/Repositories/WorkingRevisionRepository.cs

Implementation requirements:

1. New validation rules (Domain):
   - ManufacturingReadinessRule: RuleCode "manufacturing.readiness",
     Category Manufacturing, Scope Project, PreviewSafe false. Evaluates
     ValidationContext.ManufacturingBlockers; emits
     ValidationSeverity.ManufactureBlocker per blocker.
   - InstallReadinessRule: RuleCode "install.readiness", Category Install,
     Scope Project, PreviewSafe false. Evaluates InstallBlockers.
   - If ValidationRuleCategory lacks Manufacturing / Install / Constraints
     enum members, add them.

2. ValidationContext (Domain):
   - Add `IReadOnlyList<ManufacturingBlockerSnapshot> ManufacturingBlockers
     { get; init; } = []`.
   - Add `IReadOnlyList<InstallBlockerSnapshot> InstallBlockers { get; init; } = []`.
   - Define the snapshot records in Domain (stable, pure-data).

3. ValidationStage.BuildContext populates both blocker lists from the
   pipeline results.

4. Service cleanup:
   - RunService.DeleteRunAsync, SetCabinetOverrideAsync — if either still
     throws NotImplementedException, implement them. DeleteRunAsync rejects
     non-empty runs (emit ValidationIssue Error "RUN_NOT_EMPTY").
   - ProjectService.OpenProjectAsync — if multiple projects match the
     resolution, throw InvalidOperationException with a clear message.
   - ProjectService.CreateProjectAsync — ensure a WorkingRevision is
     persisted in the same UoW.

5. Presentation cleanup:
   - Remove every `catch (NotImplementedException)` block under
     src/CabinetDesigner.Presentation/ (e.g. IssuePanelViewModel.cs,
     StatusBarViewModel.cs). Let exceptions flow through the existing
     error surfacing pipeline.

6. AsyncRelayCommand:
   - Require an IAppLogger (or IApplicationEventBus) at construction. Drop
     the "optional _onException" overload. All exceptions must be logged
     AND surfaced via the event bus. Never silently swallowed.
   - Update every call site that relied on the optional overload.

7. WorkingRevisionRepository reconstruction:
   - Keep existing saved-order reconstruction logic.
   - Add two new tests (below): one for corruption recovery, one for
     replay determinism.

Required tests:

File: tests/CabinetDesigner.Tests/Validation/ManufacturingReadinessRuleTests.cs
- Evaluate_NoBlockers_ReturnsEmpty
- Evaluate_OneBlocker_EmitsManufactureBlockerIssue

File: tests/CabinetDesigner.Tests/Validation/InstallReadinessRuleTests.cs
(parallel cases)

File: tests/CabinetDesigner.Tests/Services/RunServiceTests.cs (extend)
- DeleteRunAsync_RemovesRun_WhenEmpty
- DeleteRunAsync_Rejects_WhenRunHasCabinets
- SetCabinetOverrideAsync_UpdatesOverride
- SetCabinetOverrideAsync_RejectsEmptyKey

File: tests/CabinetDesigner.Tests/Services/ProjectServiceTests.cs (extend)
- OpenProjectAsync_WithMultipleProjectsInStore_Throws
- CreateProjectAsync_PersistsEmptyWorkingRevision

File: tests/CabinetDesigner.Tests/Presentation/AsyncRelayCommandTests.cs
- Execute_ExceptionThrown_IsLoggedAndSurfacedOnEventBus
- Constructor_RequiresLogger

File: tests/CabinetDesigner.Tests/Persistence/WorkingRevisionRepositoryTests.cs (extend)
- LoadAsync_ReconstructsRunSlotsInSavedOrder
- LoadAsync_ReplayIsDeterministic_AcrossMultipleLoads
- LoadAsync_CorruptedRowThrows_WithStableExceptionType

Definition of Done:
- [ ] No `throw new NotImplementedException(...)` in src/.
- [ ] No `catch (NotImplementedException)` in src/.
- [ ] IssuePanel surfaces manufacturing + install blockers.
- [ ] AsyncRelayCommand requires a logger.
- [ ] All tests green.
- [ ] dotnet build -warnaserror + dotnet test clean.

Output rules. Next: P12.
```

---

# Prompt P12 — End-to-end V1 acceptance + independent audit

```text
Load rules: docs/ai/v1_finish/v1_universal_rules.md. Obey output rules
verbatim at end.

Skills:
- superpowers:executing-plans
- superpowers:verification-before-completion
- Agent tool with subagent_type "feature-dev:code-reviewer" for the final
  independent audit.

Goal:
Prove V1 is complete. Produce a sample project end-to-end test, run all
quality gates, and dispatch an independent reviewer to audit against
`v1_universal_rules.md` section 9.

Read first:
- docs/ai/v1_finish/v1_universal_rules.md (section 9 definition of done)
- tests/CabinetDesigner.Tests/Pipeline/EndToEndPipelineTests.cs (create if absent)

Implementation requirements:

1. Create an end-to-end acceptance test file:

File: tests/CabinetDesigner.Tests/Acceptance/V1AcceptanceTests.cs

- FullV1Workflow_CreateProjectAddRoomPlaceCabinetsExportCutList_Succeeds
    * Build a project with one room (ceiling height 96"), one wall (10 ft),
      and a single run containing one base-standard-24 and one
      base-faceframe-24 cabinet.
    * Run ResolutionOrchestrator in Full mode.
    * Assert CommandResult.Success == true.
    * Assert PartResult.Parts.Count > 0.
    * Assert CostingResult.Status == CostingStatus.NotConfigured.
    * Run CutListExporter.Export against the manufacturing plan.
    * Assert CSV, TXT, HTML non-empty and ContentHash non-empty.

- FullV1Workflow_IsDeterministic
    * Run the above scenario twice; assert identical:
      - PartGenerationResult bytes (via DeterministicJson)
      - ManufacturingPlan bytes
      - CutListExportResult.ContentHash
      - PackagingResult.ContentHash

- FullV1Workflow_InvalidDesign_PackagingRejected
    * Produce a room + cabinet that intentionally violates a validation rule
      (e.g. cabinet exceeds run capacity).
    * Assert PackagingStage fails with PACKAGING_INVALID_DESIGN.

- V1CodeHealth_NoNotImplementedExceptionInSrc
    * Scan src/ for `throw new NotImplementedException` and `catch (NotImplementedException)`.
    * Assert count == 0.

- V1Layering_PresentationDoesNotReferenceDomain
    * Parse CabinetDesigner.Presentation.csproj.
    * Assert no ProjectReference to CabinetDesigner.Domain.

- V1CostingDecoupled_PipelineSucceeds_WhenNoPricingConfigured
    * Ensure CatalogService.IsPricingConfigured == false.
    * Run full pipeline; assert Success == true and CostingStatus ==
      NotConfigured.

2. Run the full quality gates:
   - `dotnet build -warnaserror`
   - `dotnet test`
   Both must be clean. If not, stop and report.

3. Dispatch an independent code reviewer (do NOT do this yourself):
   ```
   Agent tool with subagent_type = "feature-dev:code-reviewer"
   Prompt: "Audit Carpenter Studio against
     docs/ai/v1_finish/v1_universal_rules.md section 9 (Definition of V1
     Complete). Do not run tests — read the source and test files and
     tell me, for each checkbox, whether the claim is defensible from the
     code alone. Flag any V2 coupling that would break V1 if the V2
     feature were absent. Report findings as a bulleted list under each
     checkbox. Max 400 words."
   ```
   Surface the agent's findings in your output section 4.

Definition of Done:
- [ ] V1AcceptanceTests.cs exists and every test is green.
- [ ] `dotnet build -warnaserror` clean.
- [ ] `dotnet test` clean.
- [ ] Independent code-reviewer audit performed and findings recorded.
- [ ] v1_universal_rules.md §9 checklist is defensible from the code.
- [ ] docs/V2_enhancements.md "V1 Extension Points to Watch" list is
      complete (one bullet per `// V2:` marker found in src/).

Follow output rules verbatim.
```

---

## Appendix — Running the pack

1. Create a dedicated branch: `git checkout -b v1-finish`.
2. Run P01 → P12 in order. Review changes between each prompt; commit per
   prompt with message `v1-finish: <prompt name>`.
3. When P12 reports clean, cut a tag `v1.0.0-candidate` and hand off to QA
   for a manual end-to-end walkthrough using the universal rules §9
   checklist.
