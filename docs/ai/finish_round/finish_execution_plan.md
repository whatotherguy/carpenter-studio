# Finish Execution Plan — Carpenter Studio

Date: 2026-04-18
Mode: PLANNING (concrete, file-level, derived from current source)
Goal: Take the repo from "skeleton pipeline + partially-wired UI" to a state that is safe for user testing and on the path to professional cabinet-shop use.

This document is the contract for the implementation work. Every item names real files that exist today. Every batch has a concrete failure mode it unblocks. Do not treat any batch as done until its acceptance criteria hold.

---

## 1. Unfinished Systems Still Using Skeleton Behavior

These stages return `StageResult.NotImplementedYet(...)` and set empty / zero-valued result objects. The pipeline therefore "succeeds" end-to-end while producing nothing meaningful.

| # | Stage | File | Current Behavior | Downstream Consequence |
|---|---|---|---|---|
| S-1 | Engineering Resolution (stage 4) | [src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs) | `Assemblies = [], FillerRequirements = [], EndConditionUpdates = []` | No fillers, no end conditions, install plan degenerates to a cabinet-only run. Validation's `RunValidationSnapshot` always reports `HasLeftEndCondition = false`. |
| S-2 | Constraint Propagation (stage 5) | [src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs) | `MaterialAssignments = [], HardwareAssignments = [], Violations = []` | ManufacturingProjector falls back to per-part defaults (which are themselves skeleton values from S-3). No real material book-keeping; no hardware. |
| S-3 | Part Generation (stage 6) | [src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs) | `Parts = []` | ManufacturingProjector receives zero parts. Cut list is empty. **Readiness reports IsReady = true because there are no parts to fail validation on.** This is the core false-success vector. |
| S-4 | Costing (stage 9) | [src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs) | All decimals = 0 | A quote of $0.00 is surfaced as a legitimate, completed total. |
| S-5 | Packaging (stage 11) | [src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs) | `SnapshotId = "", RevisionId = default, CreatedAt = UnixEpoch, ContentHash = "", Summary = (0,0,0,0,0)` | Revision approval produces a structurally-valid-looking but empty snapshot with a zero hash. Dangerous for any "locked for manufacture" flow. |

Service-layer unfinished:

| # | Location | Symptom |
|---|---|---|
| S-6 | [src/CabinetDesigner.Application/Services/RunService.cs:44](src/CabinetDesigner.Application/Services/RunService.cs#L44) | `DeleteRunAsync` throws `NotImplementedException`. |
| S-7 | [src/CabinetDesigner.Application/Services/RunService.cs:121](src/CabinetDesigner.Application/Services/RunService.cs#L121) | `SetCabinetOverrideAsync` throws `NotImplementedException`. |

---

## 2. Systems That Exist But Are Below Professional Quality

These technically run, but behave in ways that will betray a real shop.

| # | File / Line | Problem |
|---|---|---|
| Q-1 | [src/CabinetDesigner.App/MainWindow.xaml](src/CabinetDesigner.App/MainWindow.xaml) | The main window only mounts `Canvas.CanvasView`. `CatalogPanelView`, `PropertyInspectorView`, `IssuePanelView`, `RunSummaryPanelView`, `ShellToolbarView`, and `StatusBarView` all exist under [src/CabinetDesigner.Presentation/Views/](src/CabinetDesigner.Presentation/Views/) and are bound in `ShellViewModel`, but are **never rendered**. The user cannot see validation issues, inspect a cabinet, add from the catalog via UI, or view run summaries. This alone makes the app not user-testable. |
| Q-2 | [src/CabinetDesigner.Application/Services/ProjectService.cs:68-76](src/CabinetDesigner.Application/Services/ProjectService.cs#L68-L76) | `OpenProjectAsync(filePath)` ignores `filePath` for resolution — it calls `ListRecentAsync(1)` and takes the single row. If the store has more than one project it opens the wrong one. The `filePath` is only stored in the summary DTO. |
| Q-3 | [src/CabinetDesigner.Application/Services/ProjectService.cs:145](src/CabinetDesigner.Application/Services/ProjectService.cs#L145) | `CreateProjectAsync` constructs a `WorkingRevision` in memory but never persists it; only a `ProjectRecord + RevisionRecord + AutosaveCheckpoint` are saved. Re-open of a fresh project will fail at `WorkingRevisionRepository.LoadAsync`. |
| Q-4 | [src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs:67-71](src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs#L67-L71) | `WorkflowStateSnapshot` is hardcoded: `ApprovalState = "Draft"`, `HasUnapprovedChanges = false`, `HasPendingManufactureBlockers = false`. The workflow-unapproved-changes rule will never fire in practice. |
| Q-5 | [src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs:52-55](src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs#L52-L55) | Only `RunOverCapacityRule` and `WorkflowUnapprovedChangesRule` are wired. No material-assignment rule, no manufacturing-readiness rule, no install-readiness rule. Stages 7 and 8 produce `Readiness.Blockers`, but those are already surfaced as stage `Issues`, not by validation — meaning `IValidationSummaryService` / `IssuePanel` never see them through the rules engine. |
| Q-6 | [src/CabinetDesigner.Application/ResolutionOrchestrator.cs:259-261](src/CabinetDesigner.Application/ResolutionOrchestrator.cs#L259-L261) | `CreateNotImplementedIssue` emits severity `Warning`. As long as stages 4/5/6/9/11 are skeletons, every successful run is accompanied by warnings the user cannot see (Q-1) and the pipeline reports `Success = true`. |
| Q-7 | [src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs:117-126](src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs#L117-L126) | `Readiness.IsReady` is true whenever `blockers.Count == 0`. With `Parts = []` (from S-3) there are zero parts to validate, so readiness is trivially true. There is no `NoPartsProduced` blocker. |
| Q-8 | [src/CabinetDesigner.Application/Services/SnapshotService.cs:60-66](src/CabinetDesigner.Application/Services/SnapshotService.cs#L60-L66) | `ApproveRevisionAsync` builds snapshot blobs as ad-hoc anonymous JSON with only `{ schema_version, revision_id, project, parts }`. No cut-list, manufacturing plan, install plan, cost totals, or content hash. Any "locked for manufacture" downstream is meaningless. |
| Q-9 | [src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs:166](src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs#L166) and [src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs:174](src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs#L174) | Both catch `NotImplementedException` as a control-flow mechanism. This silently masks S-6/S-7-style service gaps. Acceptable as a transition tool; unacceptable once S-6/S-7 land — the `catch` blocks must be removed or narrowed. |

---

## 3. Dangerous "False Success" Paths

These are the places where a naive reviewer or a user-testing round would conclude "the pipeline works" while actually producing nothing.

1. **Empty-parts readiness** — S-3 returns `Parts = []`. `ManufacturingPlanningStage` calls the projector, which emits `Plan.Readiness.IsReady = true` because there are no parts to fail. `InstallProjector` then accepts the ready manufacturing plan and happily generates install steps straight from `spatial.Placements`, yielding an install plan with cabinets but no fastening-meaningful cut list. The whole pipeline succeeds with a warning-only "STAGE_NOT_IMPLEMENTED".
2. **Zero-dollar quote** — S-4 returns `Total = 0m`. If/when a quote or summary surface is added, `$0.00` will be reported as the costed value.
3. **Empty snapshot on approval** — S-5 + Q-8 combine to let a user "approve" a revision and produce a snapshot with an empty content hash, zero part count, and a Unix-epoch timestamp. Any subsequent "locked for manufacture" promotion would be operating on nothing.
4. **Warning-only skeleton detection** — Q-6. Skeleton warnings exist but MainWindow (Q-1) never renders the IssuePanel, so the user literally cannot see them. The app looks green.
5. **Workflow gate never fires** — Q-4. `HasUnapprovedChanges = false` is hardcoded, so the unapproved-changes rule cannot block approval in practice.
6. **OpenProject with multiple projects** — Q-2. First user-testing session with two saved projects opens whichever one happens to come back first from `ListRecentAsync(1)`, silently ignoring the chosen file.

**Rule:** No batch is complete while any of its downstream false-success vectors remain. Each implementation batch below has an explicit "kill switch" — the validation rule or blocker that must replace the old false-success.

---

## 4. Minimum Safe Implementation Sequence

Ordering reasoning:
- Data flows forward through the pipeline: you cannot test Manufacturing without Parts; you cannot test Costing without Manufacturing; you cannot test Packaging without everything before it. So stages must be filled in pipeline order.
- UI shell must be real before user testing even begins, but it can be parallelized after B0.
- Workflow-state plumbing (Q-4) is a prerequisite for any meaningful "approve revision" gate, so it lands before B6.

### Batch 0 — Safety Net: Turn Skeletons into Hard Blockers

**Purpose:** Before any feature work, close the false-success path so that a half-finished batch can never pass for done.

**Files:**
- [src/CabinetDesigner.Application/ResolutionOrchestrator.cs](src/CabinetDesigner.Application/ResolutionOrchestrator.cs) — change `CreateNotImplementedIssue` severity from `Warning` to `Error` when `context.Mode == ResolutionMode.Full`, OR have it fail the `CommandResult` explicitly. Keep warning behavior for `Preview`.
- [src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs](src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs) — add a `NoPartsProduced` blocker when `partResult.Parts.Count == 0`. New enum value on `ManufacturingBlockerCode`.
- [src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs) — while still stubbed, make it emit a `StageResult.Failed` with a `COSTING_NOT_IMPLEMENTED` error rather than `NotImplementedYet`, so a skeleton costing run cannot produce a "completed" $0 total.
- [src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs) — same pattern: `PACKAGING_NOT_IMPLEMENTED` error until B6 lands.
- [tests/CabinetDesigner.Tests/Pipeline/ResolutionOrchestratorTests.cs](tests/CabinetDesigner.Tests/Pipeline/ResolutionOrchestratorTests.cs) — the existing `ResolutionOrchestratorIncompleteStageTests.Execute_NotImplementedWarning_DoesNotBlockPipeline` **must flip**: a Full-mode not-implemented stage should now fail the pipeline. Add a `Preview_NotImplementedStage_StillWarnsOnly` counterpart.
- New: `tests/CabinetDesigner.Tests/Projection/ManufacturingProjectorTests.cs` — `Project_WithNoParts_EmitsNoPartsProducedBlocker`.

**Acceptance:** Running the full pipeline with any skeleton stage still present produces `CommandResult.Success == false` with a stable, visible error code per missing stage. No test asserting "skeleton pipeline succeeds" remains.

**Kill switch:** the STAGE_NOT_IMPLEMENTED warning codepath.

---

### Batch 1 — UI Shell: Mount the Panels That Already Exist

**Purpose:** Without this, there is no surface on which user testing can occur. This batch does not implement anything new in the viewmodels; it only wires them to the window.

**Files:**
- [src/CabinetDesigner.App/MainWindow.xaml](src/CabinetDesigner.App/MainWindow.xaml) — replace the single-`ContentControl` layout with a 3-column dock:
  - Left pane: `CatalogPanelView` bound to `Catalog`.
  - Center: existing `Canvas.CanvasView` content.
  - Right pane (tabbed or stacked): `PropertyInspectorView` bound to `PropertyInspector`, `RunSummaryPanelView` bound to `RunSummary`, `IssuePanelView` bound to `IssuePanel`.
  - Bottom row: `StatusBarView` bound to `StatusBar` (replacing the ad-hoc status textblocks).
  - Top toolbar: `ShellToolbarView` (replacing today's inline buttons).
- [src/CabinetDesigner.App/App.xaml](src/CabinetDesigner.App/App.xaml) — ensure the Presentation resource dictionaries for the views are merged (currently each view xaml declares its own; verify nothing is relying on `MainWindow` merging).
- [src/CabinetDesigner.App/MainWindow.xaml.cs](src/CabinetDesigner.App/MainWindow.xaml.cs) — no code change needed unless DataContext wiring for the toolbar's button commands moves.

**Tests:**
- Add `tests/CabinetDesigner.Tests/App/MainWindowSmokeTests.cs` as a manual check list (XAML parse test at minimum — many solutions already have a `XamlLoadTests` pattern; use it if present, otherwise add one).
- Update any existing UI-smoke test that asserts the single-pane layout.

**Acceptance:** Launching the app shows catalog, canvas, property inspector, run summary, issues, and status bar on first paint. Creating a project surfaces a visible run in the canvas and a visible row in the issue panel when a skeleton blocker fires (depends on B0).

**Kill switch:** the "issues are invisible" component of Q-1.

---

### Batch 2 — Part Generation (Stage 6)

**Purpose:** Produce `GeneratedPart` entries per cabinet so that downstream stages have real inputs.

**Files:**
- Replace stub body of [src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs) with a generator that, for each cabinet resolved in `SpatialResult.Placements`:
  - Pulls the cabinet's dimensions, construction method, and category from `IDesignStateStore` (threaded through the ctor, not the `new InMemoryDesignStateStore()` default; see constructor pattern in `SpatialResolutionStage`).
  - Emits parts: `LeftSide`, `RightSide`, `Top`, `Bottom`, `Back`, plus shelves per category defaults. Face-frame construction adds a face-frame part set.
  - Derives `Width`/`Height`/`MaterialThickness`/`GrainDirection` deterministically (no RNG). Initial `MaterialId` should be `default`; the constraint stage (B3) resolves real materials.
  - Labels each part `"{cabinet-label}-{PartType}"`.
  - Returns `StageResult.Failed` with `PART_GEN_EMPTY` if no cabinets are resolved and the run pipeline is Full-mode.
- Add a helper: `src/CabinetDesigner.Application/Pipeline/Parts/PartGeometry.cs` — pure math for side/top/bottom/back dimensions given cabinet envelope and thickness. Keep Domain-free of this to preserve layering.
- Remove the skeleton `IAppLogger?` ctor or keep it but require `IDesignStateStore` as primary dependency. Update [src/CabinetDesigner.Application/ResolutionOrchestrator.cs:210](src/CabinetDesigner.Application/ResolutionOrchestrator.cs#L210) `CreateDefaultStages` to pass `stateStore`.

**Tests:**
- `tests/CabinetDesigner.Tests/Pipeline/PartGenerationStageTests.cs`:
  - `Execute_ForFramelessBaseCabinet_Generates6Parts`.
  - `Execute_ForFaceFrameBaseCabinet_AlsoEmitsFaceFrameParts`.
  - `Execute_DimensionsAreDeterministic_OnRepeatedRuns`.
  - `Execute_WithEmptySpatialPlacements_InFullMode_Fails`.
- Update any existing pipeline integration tests that assumed `Parts = []`.

**Acceptance:** Running the pipeline on a project with one base cabinet produces a non-empty `CutList` with correct per-part dimensions. ManufacturingProjector's readiness is driven by real material/thickness validation, not emptiness.

**Kill switch:** S-3.

---

### Batch 3 — Constraint Propagation (Stage 5)

**Purpose:** Assign real materials and (where defined) hardware to parts/openings, and emit violations.

**Files:**
- Replace stub body of [src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs) to:
  - Consume `context.PartResult.Parts` (produced in B2) and `ICatalogService` (already registered; wire via ctor).
  - For each part, look up its cabinet's material override chain: cabinet → run → project-default. Use whichever resolution pathway the existing `CatalogService` exposes; if it doesn't, add `ICatalogService.ResolvePartMaterial(PartType, CabinetCategory, ConstructionMethod)` with a deterministic default book.
  - Build `HardwareAssignment`s for opening-bearing cabinets (door/drawer). For the first iteration, it is acceptable to emit an empty list when no hardware catalog exists, but emit a `ConstraintViolation` of severity `Warning` and code `NO_HARDWARE_CATALOG` so the gap is visible.
  - Emit error-level `ConstraintViolation`s for unresolvable materials (code `MATERIAL_UNRESOLVED`).
- Update [src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs) to surface `ConstraintPropagationResult.Violations` via the rules engine (add `MaterialAssignmentRule`, `HardwareAssignmentRule`).
- [src/CabinetDesigner.Application/ApplicationServiceRegistration.cs](src/CabinetDesigner.Application/ApplicationServiceRegistration.cs) — inject `ICatalogService` into the stage construction.

**Tests:**
- `tests/CabinetDesigner.Tests/Pipeline/ConstraintPropagationStageTests.cs`:
  - `Execute_AssignsDefaultMaterial_WhenNoOverride`.
  - `Execute_RespectsCabinetLevelMaterialOverride`.
  - `Execute_EmitsViolation_WhenPartCannotResolveMaterial`.
  - `Execute_IsDeterministic_ForOrderedParts`.
- Add `tests/CabinetDesigner.Tests/Validation/MaterialAssignmentRuleTests.cs`.

**Acceptance:** Every generated part has a non-default `MaterialId` for the standard catalog. Missing overrides generate visible validation errors, not silent `default`-material values.

**Kill switch:** S-2; the `materialId == default` branch in `ManufacturingProjector.ValidatePart` becomes an unreachable safety net rather than the default path.

---

### Batch 4 — Engineering Resolution (Stage 4)

**Purpose:** Produce assemblies, filler requirements, and end conditions so install planning and validation can operate on real data.

**Files:**
- Replace stub of [src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs). For each run in `SpatialResolutionResult.RunSummaries`:
  - Build an `AssemblyResolution` per cabinet capturing its assembly type (e.g. `"BaseCabinetAssembly"` vs `"WallCabinetAssembly"`) and resolved parameters (shelf count, toe-kick, etc.).
  - Compute `FillerRequirement`s by comparing `OccupiedLength` to `Capacity`; emit one filler per wall-end gap larger than 1/8".
  - Compute `EndConditionUpdate`s from the run's wall attachment: use `WallContext` / `RunContext` abstractions already in Domain.
- Pull `IDesignStateStore` into the stage ctor (same pattern as B2) and update `CreateDefaultStages` accordingly.
- [src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs) — replace the `WorkflowStateSnapshot` hardcoding (Q-4). Thread real workflow state from `ICurrentPersistedProjectState`; emit `HasUnapprovedChanges = checkpoint.IsClean == false`.

**Tests:**
- `tests/CabinetDesigner.Tests/Pipeline/EngineeringResolutionStageTests.cs`:
  - `Execute_ProducesAssemblyPerCabinet`.
  - `Execute_ProducesFillerForWallGap_LargerThanTolerance`.
  - `Execute_ProducesEndConditionsMatchingWallDirection`.
- Update `ValidationStageTests` (add or verify) to confirm `HasUnapprovedChanges` flows from checkpoint state.

**Acceptance:** Install plan now sees real fillers/end conditions; `InstallProjector.BuildFillerSteps` stops being a no-op. Validation's workflow rule can actually fire.

**Kill switch:** S-1 and Q-4.

---

### Batch 5 — Costing (Stage 9)

**Purpose:** Replace the zero-quote with a real material + hardware + labor + install total.

**Files:**
- Replace stub of [src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs). Ctor: `(ICatalogService catalog, ICostingPolicy policy, IAppLogger? logger)`.
  - `MaterialCost` = Σ over `ManufacturingResult.Plan.MaterialGroups` of `group.TotalSquareFootage * catalog.GetMaterialPrice(group.MaterialId, group.MaterialThickness)`.
  - `HardwareCost` = Σ over `ConstraintResult.HardwareAssignments` using `catalog.GetHardwarePrice(HardwareItemId)`.
  - `LaborCost` = Σ over `ManufacturingResult.Plan.Operations` using `policy.GetLaborRate(OperationKind)`.
  - `InstallCost` from `InstallResult.Plan.Steps.Count * policy.InstallRatePerStep`.
  - `Subtotal = Material + Hardware + Labor + Install`. `Markup = Subtotal * policy.MarkupFraction`. `Tax = (Subtotal + Markup) * policy.TaxFraction`. `Total = Subtotal + Markup + Tax`.
  - `CabinetBreakdowns` per cabinet.
  - Emit `RevisionDelta` if a prior approved revision exists via `ISnapshotRepository`.
- New: `src/CabinetDesigner.Application/Costing/ICostingPolicy.cs` + `DefaultCostingPolicy.cs` (shop rates; explicit, constructor-injectable, deterministic).
- [src/CabinetDesigner.Application/ApplicationServiceRegistration.cs](src/CabinetDesigner.Application/ApplicationServiceRegistration.cs) — register `ICostingPolicy` and thread it in.
- Remove the B0 "COSTING_NOT_IMPLEMENTED" hard-fail now that costing is real.

**Tests:**
- `tests/CabinetDesigner.Tests/Pipeline/CostingStageTests.cs`:
  - `Execute_MaterialCost_MatchesCatalogPricePerSqFt`.
  - `Execute_TotalEqualsSubtotalPlusMarkupPlusTax`.
  - `Execute_CabinetBreakdowns_SumToTopLineCosts`.
  - `Execute_RevisionDelta_PopulatedWhenPriorSnapshotExists`.
  - `Execute_IsDeterministic_OnRepeatedRuns`.

**Acceptance:** Total is non-zero for any non-empty project. Breakdown rows reconcile to top-line. No rounding drift beyond one minor unit.

**Kill switch:** S-4.

---

### Batch 6 — Packaging (Stage 11) + Snapshot Integrity

**Purpose:** Produce a real, hash-addressed snapshot of every resolution output so "Approve Revision" means something.

**Files:**
- Replace stub of [src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs):
  - Pull `context.PartResult`, `ManufacturingResult`, `InstallResult`, `CostingResult`, `ValidationResult`.
  - Serialize deterministic JSON (sorted keys, invariant culture, explicit schema version) per artifact. Compute `ContentHash = SHA-256 hex` over concatenated artifact bytes.
  - Emit `Summary` with real counts (cabinets from `SpatialResult`, runs from `RunSummaries`, parts from `Plan.CutList.Count`, validation issues from `ValidationResult.Result.AllBaseIssues`, total from `CostingResult.Total`).
  - `RevisionId` = `_workingRevisionSource.CaptureCurrentState().Revision.Id`; `CreatedAt = clock.Now`.
  - Fail the stage if `ValidationResult.Result.IsValid == false` (packaging an invalid design is unsafe).
- [src/CabinetDesigner.Application/Services/SnapshotService.cs](src/CabinetDesigner.Application/Services/SnapshotService.cs) — replace ad-hoc anonymous-object blobs with the real serialized artifacts from `PackagingResult`. Store the `ContentHash` alongside the `ApprovedSnapshot` (requires a Domain-level additive field or a persistence-layer column; prefer additive).
- Remove the B0 "PACKAGING_NOT_IMPLEMENTED" hard-fail.

**Tests:**
- `tests/CabinetDesigner.Tests/Pipeline/PackagingStageTests.cs`:
  - `Execute_ProducesStableHash_ForIdenticalInputs`.
  - `Execute_ProducesDifferentHash_WhenCostChanges`.
  - `Execute_FailsStage_WhenValidationIsInvalid`.
- `tests/CabinetDesigner.Tests/Persistence/SnapshotApprovalContentTests.cs`:
  - `ApproveRevision_SnapshotBlob_ContainsManufacturingAndCost`.
  - `ApproveRevision_ContentHash_IsStoredAndReturned`.

**Acceptance:** Two identical designs produce byte-identical snapshots; any material/cost change produces a different hash. Approval is rejected when validation is red.

**Kill switch:** S-5, Q-8.

---

### Batch 7 — Validation Wiring, Services, and UI Finalization

**Purpose:** Close the remaining professional-quality gaps and make user testing robust.

**Files:**
- [src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs](src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs) — register:
  - `MaterialAssignmentRule` (from B3).
  - `HardwareAssignmentRule` (from B3).
  - `ManufacturingReadinessRule` — surfaces `ManufacturingPlan.Readiness.Blockers` through the engine.
  - `InstallReadinessRule` — surfaces `InstallPlan.Readiness.Blockers`.
- [src/CabinetDesigner.Application/Services/RunService.cs:44](src/CabinetDesigner.Application/Services/RunService.cs#L44) — implement `DeleteRunAsync`. Requires new `DeleteRunCommand` in `CabinetDesigner.Domain.Commands.Structural` + a handler in the resolution pipeline's interaction stage. Pattern follows `CreateRunCommand`.
- [src/CabinetDesigner.Application/Services/RunService.cs:121](src/CabinetDesigner.Application/Services/RunService.cs#L121) — implement `SetCabinetOverrideAsync`. Requires new `SetCabinetOverrideCommand` + handler.
- [src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs:166](src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs#L166) and [src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs:174](src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs#L174) — remove the `catch (NotImplementedException)` handlers; they are no longer needed.
- [src/CabinetDesigner.Application/Services/ProjectService.cs](src/CabinetDesigner.Application/Services/ProjectService.cs):
  - `OpenProjectAsync` — use `filePath` to select or load the intended project. If the persistence layer is single-file per `.db`, then explicitly assert `ListRecentAsync` returns exactly one row and fail loudly otherwise.
  - `CreateProjectAsync` — persist the initial `WorkingRevision` (empty but valid) via `IWorkingRevisionRepository.SaveAsync` inside the existing unit-of-work.

**Tests:**
- `tests/CabinetDesigner.Tests/Services/RunServiceTests.cs` — add `DeleteRunAsync_RemovesRun`, `SetCabinetOverrideAsync_UpdatesOverride`.
- `tests/CabinetDesigner.Tests/Services/ProjectServiceTests.cs` — `OpenProject_WithMultipleProjectsInStore_FailsLoudly`, `CreateProject_PersistsWorkingRevision`.
- `tests/CabinetDesigner.Tests/Validation/ManufacturingReadinessRuleTests.cs`, `InstallReadinessRuleTests.cs`.

**Acceptance:** Issue panel shows real material/manufacturing/install blockers from the rules engine (not just stage issues). `DeleteRun` and `SetCabinetOverride` work from UI paths. Opening and re-opening a project preserves state.

**Kill switch:** Q-2, Q-3, Q-5, Q-9, S-6, S-7.

---

## 5. Batch List in Execution Order

| Order | Batch | One-line purpose | Blocking for |
|---|---|---|---|
| B0 | Safety net | Make skeleton stages fail the pipeline | all |
| B1 | UI shell | Mount catalog, inspector, run summary, issues, status bar in MainWindow | user testing |
| B2 | Part generation | Produce real parts from cabinets | B3, B5, B6 |
| B3 | Constraint propagation | Assign materials & hardware | B5, B6 |
| B4 | Engineering resolution | Produce assemblies, fillers, end conditions; thread real workflow state | B6, validation rule coverage |
| B5 | Costing | Produce real totals + breakdown | B6 |
| B6 | Packaging + snapshot integrity | Deterministic, hash-addressed snapshots | approvals / lock-for-manufacture |
| B7 | Validation wiring, service completion, UI cleanup | Close remaining services and make rules-engine reflect pipeline reality | user testing, professional use |

Parallelization note: B1 can start in parallel with B2 after B0 merges. B3 and B4 can overlap but both must finish before B5. B6 cannot start until B5 is in.

---

## 6. Go / No-Go Lines

### User Testing Go/No-Go

**GO for user testing after:** B0 + B1 + B2 + B3 + B4 complete AND their tests pass AND a smoke test of "create project → add run → add cabinet → see cut list → see issues panel" works end-to-end.

Explanation: user testing needs a visible UI (B1), real parts (B2), real materials (B3), real runs/fillers (B4), and the safety net (B0) guaranteeing that any remaining gap surfaces as a blocker rather than as silent success. Costing and packaging can remain wiped/hard-failed at this stage because user testing will not exercise approval.

**NO-GO for user testing if any of the following hold:**
- B0 is not in, so skeleton stages still report `Success = true` with warnings.
- B1 is not in, so the user literally cannot see panels.
- B2 is not in, so the cut list is empty.
- B3 is not in, so materials are `default` on parts shown to users.
- Any new test added for these batches is skipped or flaky.

### Professional Use Go/No-Go

**GO for professional cabinet-shop use after:** B0–B7 all complete AND:
- Determinism tests pass for the full pipeline (snapshot hashes are stable across runs).
- Costing reconciles: `Σ CabinetBreakdowns ≡ top-line Subtotal` within one minor currency unit.
- Approving a revision with a validation error is rejected.
- `NotImplementedException` catch blocks are gone.
- `OpenProjectAsync` fails loudly on ambiguous state rather than picking the first row.

**NO-GO for professional use if any of the following hold:**
- B5 or B6 is not in: zero-dollar quotes or empty snapshots are possible.
- B7 is not in: manufacturing/install blockers do not surface through the rules engine into the IssuePanel, and `DeleteRun` / `SetCabinetOverride` still throw.
- Q-4 (workflow state hardcoded) is not fixed (this is folded into B4).
- Any stage can still silently return `NotImplementedYet` in Full mode.

---

## 7. Non-Goals / Out of Scope for This Round

These are intentionally **not** in scope; they should be tracked but not attempted during finish round:
- Multi-project workspaces (implied by Q-2 only to the extent of making open deterministic).
- New hardware catalog editor UI — B3 assumes the existing `ICatalogService` surface is enough to resolve known hardware.
- Advanced nesting/optimization in `ManufacturingProjector` beyond the existing sorted groups + basic operations.
- Network or multi-user concerns.

---

## 8. Cross-Cutting Invariants Each Batch Must Preserve

- **Determinism.** Every stage must produce byte-identical output for byte-identical input. Add or extend `DeterminismTests` whenever a batch touches a stage.
- **Fail closed.** Missing data → validation error or stage failure, never an empty-but-"successful" output.
- **UI-thread affinity.** WPF viewmodels continue to use `DispatchIfNeeded` when reacting to event bus messages crossing threads.
- **Layering.** Domain → Application → Presentation → App. Do not introduce reverse references.
- **No swallowed exceptions** except where the UI surfaces them via `StatusBarViewModel` / `IssuePanelViewModel`.

---

## 9. Definition of Done for Each Batch

1. Named skeleton/stub is removed (not just bypassed).
2. Every acceptance-test listed under the batch is green.
3. The false-success vector the batch targets ("kill switch") is closed — demonstrated by a regression test that would have passed under the old skeleton and now fails.
4. No `STAGE_NOT_IMPLEMENTED` issue codes remain for the stages touched by the batch.
5. `dotnet test` and `dotnet build -warnaserror` are clean on the full solution.
