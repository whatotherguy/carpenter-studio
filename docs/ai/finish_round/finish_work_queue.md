# Finish Work Queue — Carpenter Studio

Date: 2026-04-18
Scope: Implementation-ready queue derived from `finish_execution_plan.md`.
Audience: Codex / implementing agent. Do not skip sections. Do not mark DoD complete without every checkbox.

---

## B0 — Safety Net: Skeletons Become Hard Blockers

### Objective
Prevent any skeleton stage from silently producing a "successful" Full-mode pipeline run. Preserve Preview-mode warning behavior.

### Files to Modify
- `src/CabinetDesigner.Application/ResolutionOrchestrator.cs`
- `src/CabinetDesigner.Application/Pipeline/StageResult.cs` (no signature change — only consumer semantics change)
- `src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs`
- `src/CabinetDesigner.Domain/ManufacturingContext/ManufacturingModels.cs` (extend enum)
- `src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs`
- `tests/CabinetDesigner.Tests/Pipeline/ResolutionOrchestratorTests.cs`

### Key Behaviors
- In `ResolutionOrchestrator.ExecuteStages`, when `result.IsNotImplemented == true` AND `context.Mode == ResolutionMode.Full`: add a `ValidationSeverity.Error` issue with code `STAGE_NOT_IMPLEMENTED` and stop the pipeline (return `false`). Preview mode keeps adding the existing warning.
- `ManufacturingProjector.Project` emits `ManufacturingBlockerCode.NoPartsProduced` when `partResult.Parts.Count == 0`.
- `ManufacturingBlockerCode` gains `NoPartsProduced`.
- `CostingStage.Execute` returns `StageResult.Failed(StageNumber, [new ValidationIssue(Error, "COSTING_NOT_IMPLEMENTED", ...)])` (remove `NotImplementedYet`).
- `PackagingStage.Execute` returns `StageResult.Failed(StageNumber, [new ValidationIssue(Error, "PACKAGING_NOT_IMPLEMENTED", ...)])`.

### Validation Rules
- None new in this batch. This batch supports rule-driven validation by making skeletons fail closed.

### Test Coverage Required
- `ResolutionOrchestratorIncompleteStageTests.Execute_NotImplementedStage_InFullMode_FailsPipeline` (replaces current `Execute_NotImplementedWarning_DoesNotBlockPipeline`).
- `ResolutionOrchestratorIncompleteStageTests.Preview_NotImplementedStage_StillWarnsOnly`.
- `ManufacturingProjectorTests.Project_WithNoParts_EmitsNoPartsProducedBlocker` (new file `tests/CabinetDesigner.Tests/Projection/ManufacturingProjectorTests.cs`).
- `CostingStageTests.Execute_InSkeletonState_FailsWithExplicitCode` (new file, to be replaced in B5).
- `PackagingStageTests.Execute_InSkeletonState_FailsWithExplicitCode` (new file, to be replaced in B6).

### Definition of Done
- [ ] Full-mode pipeline with any skeleton stage returns `CommandResult.Success == false` and an `Error`-severity issue naming the stage.
- [ ] Preview-mode still succeeds with a warning.
- [ ] `ManufacturingBlockerCode.NoPartsProduced` referenced from the projector.
- [ ] All new tests green; old skeleton-success test removed.
- [ ] `dotnet build -warnaserror` and `dotnet test` clean.

---

## B1 — UI Shell: Mount All Panels

### Objective
Render `Catalog`, `PropertyInspector`, `RunSummary`, `IssuePanel`, `StatusBar`, and `ShellToolbar` in `MainWindow.xaml`. No viewmodel logic changes.

### Files to Modify
- `src/CabinetDesigner.App/MainWindow.xaml`
- `src/CabinetDesigner.App/App.xaml` (verify resource dictionaries)
- `src/CabinetDesigner.App/MainWindow.xaml.cs` (only if bindings require)
- `tests/CabinetDesigner.Tests/App/MainWindowSmokeTests.cs` (new)

### Key Behaviors
- Grid layout:
  - Row 0 (Auto): `<views:ShellToolbarView DataContext="{Binding}" />` replacing the inline `StackPanel` of buttons.
  - Row 1 (star) split into 3 columns via `ColumnDefinitions`:
    - Col 0 (Auto, min 240): `<views:CatalogPanelView DataContext="{Binding Catalog}" />`.
    - Col 1 (star): `<ContentControl Content="{Binding Canvas.CanvasView}" />`.
    - Col 2 (Auto, min 320): `TabControl` with three tabs:
      - `<views:PropertyInspectorView DataContext="{Binding PropertyInspector}" />`
      - `<views:RunSummaryPanelView DataContext="{Binding RunSummary}" />`
      - `<views:IssuePanelView DataContext="{Binding IssuePanel}" />`
  - Row 2 (Auto): `<views:StatusBarView DataContext="{Binding StatusBar}" />`.
- `xmlns:views="clr-namespace:CabinetDesigner.Presentation.Views;assembly=CabinetDesigner.Presentation"`.
- No changes to `ShellViewModel`.

### Validation Rules
- None new.

### Test Coverage Required
- XAML parse test: instantiate `MainWindow` under STA (pattern from any existing UI-smoke test); assert `FindName` for each mounted panel returns non-null.
- Manual checklist in the test file header (launch → panels visible → resize behaves) — explicitly documented as manual.

### Definition of Done
- [ ] App launches with all six panels visible.
- [ ] B0 blockers appear in `IssuePanel` at runtime.
- [ ] XAML parse test green.
- [ ] No binding warnings in output log for first-paint paths.

---

## B2 — Part Generation (Stage 6)

### Objective
Produce deterministic `GeneratedPart` records per cabinet so Manufacturing has real input.

### Files to Modify
- `src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs` (rewrite)
- `src/CabinetDesigner.Application/Pipeline/Parts/PartGeometry.cs` (new)
- `src/CabinetDesigner.Application/ResolutionOrchestrator.cs` (pass `IDesignStateStore` into `PartGenerationStage`)

### Key Behaviors
- Ctor: `PartGenerationStage(IDesignStateStore stateStore, IAppLogger? logger = null)`.
- For each `RunPlacement` in `context.SpatialResult.Placements`:
  - Resolve `CabinetType` and dimensions from `stateStore.GetCabinet(cabinetId)`.
  - Resolve construction via `cabinet.ConstructionMethod` (from Domain `CabinetContext.CabinetType`).
  - Call `PartGeometry.BuildParts(cabinet)` which returns:
    - `Frameless` base: `LeftSide, RightSide, Top, Bottom, Back, AdjustableShelf × N`.
    - `Frameless` wall: `LeftSide, RightSide, Top, Bottom, Back, AdjustableShelf × N`.
    - `FaceFrame` base: above plus `FrameStile × 2, FrameRail × 2, FrameMullion × (openings-1)`.
    - Tall: extend shelf count per height.
  - `Width`/`Height` computed from cabinet envelope minus panel offsets (`Back` = panel-thickness rabbet is out of scope; use nominal envelope for v1).
  - `MaterialThickness` = `Length.FromInches(0.75m)` default, overridden by cabinet's `MaterialThicknessOverride` if set.
  - `MaterialId` = `default` (resolved in B3).
  - `GrainDirection` = `GrainDirection.Lengthwise` for sides/shelves; `None` for backs/frames.
  - `Edges = new EdgeTreatment(null, null, null, null)` (B3 will fill).
  - `Label = $"{cabinet.DisplayName}-{PartType}"` with stable index for duplicates.
  - `PartId = $"part:{cabinet.Id.Value:D}:{PartType}:{ordinal}"` (deterministic, stable across re-runs).
- Order parts by `(CabinetId, PartType, ordinal)` before returning.
- Full-mode: if no cabinets resolved, return `StageResult.Failed(6, [Error "PART_GEN_EMPTY"])`.
- Preview-mode: `ShouldExecute` remains `Full` only (match existing stage contract).

### Validation Rules
- None in this batch; violations surface from B3 (materials) and B0 (empty parts → manufacturing blocker).

### Test Coverage Required
New file `tests/CabinetDesigner.Tests/Pipeline/PartGenerationStageTests.cs`:
- `Execute_ForFramelessBase_Produces6CorePartsPlusShelves`.
- `Execute_ForFaceFrameBase_AddsStilesRailsAndMullions`.
- `Execute_DimensionsAreDeterministic_AcrossRepeatedRuns`.
- `Execute_WithEmptySpatialPlacements_Fails_WithPartGenEmpty`.
- `Execute_LabelsAreStable_WhenTwoCabinetsShareType`.

### Definition of Done
- [ ] Every cabinet produced by the pipeline yields a non-empty, ordered part list.
- [ ] `PartGeometry` is pure (no I/O, no clock, no RNG).
- [ ] Repeating identical input produces byte-identical `GeneratedPart` collection.
- [ ] All tests above green; manufacturing projector's `MissingMaterial` blocker fires (materials still `default` until B3).
- [ ] No reverse layering (stage does not reference `Rendering`, `Presentation`).

---

## B3 — Constraint Propagation (Stage 5)

### Objective
Assign real materials to every generated part; emit hardware assignments where a catalog exists; surface resolution violations.

### Files to Modify
- `src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs` (rewrite)
- `src/CabinetDesigner.Application/Services/ICatalogService.cs` (extend)
- `src/CabinetDesigner.Application/Services/CatalogService.cs` (extend)
- `src/CabinetDesigner.Application/ApplicationServiceRegistration.cs` (inject catalog into stage)
- `src/CabinetDesigner.Application/ResolutionOrchestrator.cs` (thread catalog through `CreateDefaultStages`)
- `src/CabinetDesigner.Domain/Validation/Rules/MaterialAssignmentRule.cs` (new)
- `src/CabinetDesigner.Domain/Validation/Rules/HardwareAssignmentRule.cs` (new)

### Key Behaviors
- Extend `ICatalogService`:
  - `MaterialId ResolvePartMaterial(string partType, CabinetCategory category, ConstructionMethod construction)`.
  - `Thickness ResolvePartThickness(string partType, CabinetCategory category)`.
  - `IReadOnlyList<HardwareItemId> ResolveHardwareForOpening(OpeningId openingId, CabinetCategory category)` — returns `[]` when unknown.
- `ConstraintPropagationStage(ICatalogService catalog, IDesignStateStore stateStore)`.
- For each `GeneratedPart`:
  - Determine the chain: `cabinet.MaterialOverrides[PartType]` → `run.MaterialOverrides[PartType]` → `ICatalogService.ResolvePartMaterial(...)`.
  - Emit `MaterialAssignment(PartId, MaterialId, ResolvedThickness, GrainDirection)`.
  - Emit `ConstraintViolation("MATERIAL_UNRESOLVED", …, Error)` if no resolution.
- For each cabinet with `Openings`:
  - Call catalog for hardware; emit `HardwareAssignment(OpeningId, HardwareIds, BoringPattern?)`.
  - If empty and category expects hardware: emit `ConstraintViolation("NO_HARDWARE_CATALOG", …, Warning)`.
- Validation wiring is deferred to B7, but produce the data now.

### Validation Rules (new classes)
- `MaterialAssignmentRule`
  - `RuleCode = "constraint.material_unresolved"`, `Category = ValidationRuleCategory.Constraints`, `Scope = Part`, `PreviewSafe = false`.
  - Evaluates `ConstraintPropagationResult.Violations` with code `MATERIAL_UNRESOLVED`. To keep Domain pure, route via `ValidationContext` — add optional `IReadOnlyList<ConstraintViolationSnapshot> Constraints { get; init; } = []` on `ValidationContext` and populate in `ValidationStage.BuildContext`.
- `HardwareAssignmentRule`
  - `RuleCode = "constraint.hardware_missing"`, `Category = ValidationRuleCategory.Constraints`, `Scope = Cabinet`, `PreviewSafe = false`.
  - Fires on `NO_HARDWARE_CATALOG` violations; severity warning.

### Test Coverage Required
New `tests/CabinetDesigner.Tests/Pipeline/ConstraintPropagationStageTests.cs`:
- `Execute_AssignsDefaultMaterial_FromCatalog`.
- `Execute_RespectsCabinetLevelOverride`.
- `Execute_RespectsRunLevelOverride_WhenCabinetOverrideAbsent`.
- `Execute_EmitsViolation_WhenPartHasNoResolution`.
- `Execute_HardwareEmpty_EmitsWarningViolation`.
- `Execute_IsDeterministic`.

New `tests/CabinetDesigner.Tests/Validation/MaterialAssignmentRuleTests.cs`:
- `Evaluate_NoViolations_ReturnsEmpty`.
- `Evaluate_MaterialUnresolved_EmitsError`.

### Definition of Done
- [ ] Every `GeneratedPart` has a non-`default` `MaterialId` for the built-in catalog cabinets.
- [ ] Unresolvable materials produce an Error violation; missing hardware produces a Warning.
- [ ] `ManufacturingProjector.ResolveMaterialId` now hits the resolved path; its `MissingMaterial` blocker becomes an unreachable safety net for the default catalog.
- [ ] Tests green.

---

## B4 — Engineering Resolution (Stage 4) + Workflow State

### Objective
Produce assemblies, filler requirements, end conditions. Replace hardcoded workflow snapshot.

### Files to Modify
- `src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs` (rewrite)
- `src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs` (thread real `WorkflowStateSnapshot`)
- `src/CabinetDesigner.Application/ResolutionOrchestrator.cs` (pass `IDesignStateStore`, `ICurrentPersistedProjectState`)
- `src/CabinetDesigner.Application/ApplicationServiceRegistration.cs` (DI wiring)

### Key Behaviors
- `EngineeringResolutionStage(IDesignStateStore stateStore)`.
- Per run (`stateStore.GetAllRuns()`):
  - For each cabinet slot, emit `AssemblyResolution(cabinetId, assemblyType, parameters)`. `assemblyType` = `$"{Category}CabinetAssembly"`. Parameters: `toe_kick`, `shelf_count`, `door_count`, etc., stringified with invariant culture.
  - Compute gap: `gap = run.Capacity - run.OccupiedLength`. If `gap > Length.FromInches(0.125m)`, emit one `FillerRequirement(runId, gap, "Run end filler")`.
  - Emit `EndConditionUpdate(runId, LeftEndCondition, RightEndCondition)` from `stateStore.GetWall(run.WallId)` attachment metadata. When attachment is missing, use `EndCondition.OpenEnd`.
- In `ValidationStage.BuildContext`:
  - `WorkflowStateSnapshot(ApprovalState: currentState?.Revision.State.ToString() ?? "Draft", HasUnapprovedChanges: currentState?.Checkpoint is { IsClean: false }, HasPendingManufactureBlockers: context.ManufacturingResult.Plan.Readiness.Blockers.Count > 0)`.

### Validation Rules
- No new rule classes in this batch; existing `WorkflowUnapprovedChangesRule` now has real input.

### Test Coverage Required
New `tests/CabinetDesigner.Tests/Pipeline/EngineeringResolutionStageTests.cs`:
- `Execute_ProducesOneAssemblyPerCabinet`.
- `Execute_EmitsFiller_WhenRunEndGapExceedsTolerance`.
- `Execute_NoFiller_WhenGapWithinTolerance`.
- `Execute_EndConditions_DerivedFromWallAttachment`.
- `Execute_IsDeterministic`.

Update `tests/CabinetDesigner.Tests/Pipeline/ValidationStageTests.cs` (create if absent):
- `Execute_WorkflowSnapshot_ReflectsCheckpointDirtyFlag`.
- `Execute_WorkflowSnapshot_ReflectsManufacturingBlockers`.

### Definition of Done
- [ ] Filler install steps now populate in `InstallProjector.BuildFillerSteps` during integration runs.
- [ ] `WorkflowUnapprovedChangesRule` can fire against a dirty checkpoint.
- [ ] All tests green.

---

## B5 — Costing (Stage 9)

### Objective
Real cost totals + breakdown reconciled to the penny.

### Files to Modify
- `src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs` (rewrite)
- `src/CabinetDesigner.Application/Costing/ICostingPolicy.cs` (new)
- `src/CabinetDesigner.Application/Costing/DefaultCostingPolicy.cs` (new)
- `src/CabinetDesigner.Application/Services/ICatalogService.cs` (extend pricing methods)
- `src/CabinetDesigner.Application/Services/CatalogService.cs` (seed pricing)
- `src/CabinetDesigner.Application/ApplicationServiceRegistration.cs` (DI)
- `src/CabinetDesigner.Application/ResolutionOrchestrator.cs` (wire CostingStage deps)

### Key Behaviors
- `ICostingPolicy`:
  - `decimal GetLaborRate(ManufacturingOperationKind kind)`.
  - `decimal InstallRatePerStep { get; }`.
  - `decimal MarkupFraction { get; }` (e.g. 0.20m).
  - `decimal TaxFraction { get; }` (e.g. 0.08m).
- `ICatalogService` extensions:
  - `decimal GetMaterialPricePerSquareFoot(MaterialId id, Thickness thickness)`.
  - `decimal GetHardwarePrice(HardwareItemId id)`.
- `CostingStage(ICatalogService catalog, ICostingPolicy policy, ISnapshotRepository? snapshots = null)`.
- Computation:
  - `area(part) = part.CutWidth.Inches * part.CutHeight.Inches / 144m`.
  - `MaterialCost = Σ area(part) * catalog.GetMaterialPricePerSquareFoot(part.MaterialId, part.MaterialThickness)` over `manufacturingResult.Plan.CutList`.
  - `HardwareCost = Σ catalog.GetHardwarePrice(id)` over all `HardwareAssignment.HardwareIds`.
  - `LaborCost = Σ policy.GetLaborRate(operation.Kind)` over `manufacturingResult.Plan.Operations`.
  - `InstallCost = installResult.Plan.Steps.Count * policy.InstallRatePerStep`.
  - `Subtotal = Material + Hardware + Labor + Install`.
  - `Markup = round(Subtotal * policy.MarkupFraction, 2)`.
  - `Tax = round((Subtotal + Markup) * policy.TaxFraction, 2)`.
  - `Total = Subtotal + Markup + Tax`.
  - `CabinetBreakdowns`: one per cabinet, sums of parts/hardware/labor attributed to that cabinet.
  - `RevisionDelta`: if a prior approved snapshot exists, populate `CostDelta(PreviousTotal, CurrentTotal, Difference, Summary)`; else `null`.
- All rounding uses `decimal.Round(x, 2, MidpointRounding.ToEven)`.

### Validation Rules
- None new. Costing failure paths (e.g. missing price) emit a `StageResult.Failed` with code `COSTING_PRICE_MISSING`.

### Test Coverage Required
New `tests/CabinetDesigner.Tests/Pipeline/CostingStageTests.cs`:
- `Execute_MaterialCost_MatchesSumOfPartAreaTimesPrice`.
- `Execute_HardwareCost_SumsAllHardwareAssignments`.
- `Execute_LaborCost_SumsPerOperationRates`.
- `Execute_Total_EqualsSubtotalPlusMarkupPlusTax`.
- `Execute_CabinetBreakdowns_ReconcileToTopLine_WithinOneCent`.
- `Execute_RevisionDelta_NullWhenNoPriorSnapshot`.
- `Execute_RevisionDelta_PopulatedWhenPriorSnapshotExists`.
- `Execute_MissingMaterialPrice_FailsStage`.
- `Execute_IsDeterministic`.

### Definition of Done
- [ ] Non-empty project produces a non-zero, reconciled total.
- [ ] `Σ CabinetBreakdowns ≡ Subtotal` within 1¢.
- [ ] B0's `COSTING_NOT_IMPLEMENTED` path is gone.
- [ ] Tests green.

---

## B6 — Packaging (Stage 11) + Snapshot Integrity

### Objective
Produce deterministic, hash-addressed snapshots that reflect full pipeline output; refuse packaging invalid designs.

### Files to Modify
- `src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs` (rewrite)
- `src/CabinetDesigner.Application/Packaging/DeterministicJson.cs` (new helper: sorted keys, invariant culture, no indentation)
- `src/CabinetDesigner.Application/Services/SnapshotService.cs` (consume real `PackagingResult`)
- `src/CabinetDesigner.Domain/ProjectContext/ApprovedSnapshot.cs` (add `string ContentHash` field, additive)
- `src/CabinetDesigner.Infrastructure/...SnapshotRepository` (if a matching persistence class exists; add column/serialization for hash)
- `src/CabinetDesigner.Application/ResolutionOrchestrator.cs` (wire stage deps)

### Key Behaviors
- `PackagingStage(IWorkingRevisionSource workingRevisionSource, IClock clock)`.
- Gather artifacts: `parts`, `manufacturing_plan`, `install_plan`, `costing`, `validation`.
- Serialize each with `DeterministicJson.Serialize(...)` — sorted property order, invariant culture, explicit `schema_version = 1`.
- Concatenate bytes in fixed order (`parts, manufacturing, install, costing, validation`) and compute `ContentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()`.
- Emit `PackagingResult`:
  - `SnapshotId = $"snap:{revisionId.Value:D}:{contentHash[..16]}"`.
  - `RevisionId = current.Revision.Id`.
  - `CreatedAt = clock.Now`.
  - `ContentHash = contentHash`.
  - `Summary = new SnapshotSummary(cabinetCount, runCount, partCount, validationIssueCount, costing.Total)`.
- If `context.ValidationResult.Result.IsValid == false`: `StageResult.Failed(11, [Error "PACKAGING_INVALID_DESIGN"])`.
- `SnapshotService.ApproveRevisionAsync` reads `context.PackagingResult` (via a new `IPackagingResultStore` or by returning the result on the pipeline result) and persists it in place of the anonymous JSON blobs. Simpler path: have `PackagingStage` publish to a singleton `IPackagingResultStore` that `SnapshotService` reads — mirrors existing `IValidationResultStore` pattern.

### Validation Rules
- None new.

### Test Coverage Required
New `tests/CabinetDesigner.Tests/Pipeline/PackagingStageTests.cs`:
- `Execute_ProducesStableHash_ForIdenticalInputs`.
- `Execute_HashChanges_WhenCostChanges`.
- `Execute_HashChanges_WhenPartListChanges`.
- `Execute_FailsStage_WhenValidationIsInvalid`.
- `Execute_SnapshotSummary_ReflectsCounts`.

New `tests/CabinetDesigner.Tests/Persistence/SnapshotApprovalContentTests.cs`:
- `ApproveRevision_SnapshotBlob_ContainsManufacturingCutList`.
- `ApproveRevision_SnapshotBlob_ContainsCostTotal`.
- `ApproveRevision_ContentHash_StoredAndReturnedFromRead`.

### Definition of Done
- [ ] Two runs of the same project produce identical `ContentHash`.
- [ ] Approval is rejected when validation is not valid.
- [ ] Snapshot row in persistence includes `ContentHash`.
- [ ] B0's `PACKAGING_NOT_IMPLEMENTED` path is gone.
- [ ] Tests green.

---

## B7 — Validation Wiring, Service Completion, UI Cleanup

### Objective
Route manufacturing/install blockers through the rules engine; implement the two `NotImplementedException` service methods; fix `ProjectService` professional-quality gaps; remove stale `catch (NotImplementedException)`.

### Files to Modify
- `src/CabinetDesigner.Domain/Validation/Rules/ManufacturingReadinessRule.cs` (new)
- `src/CabinetDesigner.Domain/Validation/Rules/InstallReadinessRule.cs` (new)
- `src/CabinetDesigner.Domain/Validation/ValidationContext.cs` (add `IReadOnlyList<ManufacturingBlockerSnapshot>` + `IReadOnlyList<InstallBlockerSnapshot>`; define the snapshot records in Domain; populated in Application layer)
- `src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs` (register new rules + populate new snapshots)
- `src/CabinetDesigner.Domain/Commands/Structural/DeleteRunCommand.cs` (new)
- `src/CabinetDesigner.Domain/Commands/Modification/SetCabinetOverrideCommand.cs` (new)
- Corresponding handlers in `src/CabinetDesigner.Application/Pipeline/Stages/InteractionInterpretationStage.cs` (extend existing command dispatch)
- `src/CabinetDesigner.Application/Services/RunService.cs` (implement `DeleteRunAsync`, `SetCabinetOverrideAsync`)
- `src/CabinetDesigner.Application/Services/ProjectService.cs`:
  - `OpenProjectAsync`: if `_projectRepository.ListRecentAsync(2, ct)` returns >1 row, throw `InvalidOperationException("Ambiguous project state; filePath-based resolution not implemented.")`.
  - `CreateProjectAsync`: within the existing UOW, call `_workingRevisionRepository.SaveAsync(new WorkingRevision(revision, [], [], [], [], []), ct)`.
- `src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs:166` — remove `catch (NotImplementedException)` block; let exceptions propagate via existing error path.
- `src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs:174` — same.

### Key Behaviors
- `DeleteRunCommand(RunId runId, CommandOrigin, …)`:
  - `ValidateStructure`: reject if `runId` is default.
  - Handler removes run + orphans cabinets to un-runned state (or rejects if not empty — pick policy: **reject when non-empty**; emit `ValidationIssue Error "RUN_NOT_EMPTY"`).
- `SetCabinetOverrideCommand(CabinetId, string OverrideKey, OverrideValue Value, ...)`:
  - `ValidateStructure`: reject empty key.
  - Handler updates `cabinet.Overrides[key] = value`.
- Both commands tracked through `IDeltaTracker` so undo/redo works.

### Validation Rules
- `ManufacturingReadinessRule`
  - `RuleCode = "manufacturing.readiness"`, `Category = ValidationRuleCategory.Manufacturing`, `Scope = Project`, `PreviewSafe = false`.
  - Evaluates `context.ManufacturingBlockers`; emits `ValidationSeverity.ManufactureBlocker` per blocker.
- `InstallReadinessRule`
  - `RuleCode = "install.readiness"`, `Category = ValidationRuleCategory.Install`, `Scope = Project`, `PreviewSafe = false`.
  - Evaluates `context.InstallBlockers`.
- Add `ValidationRuleCategory.Manufacturing`, `ValidationRuleCategory.Install`, `ValidationRuleCategory.Constraints` if not already in the enum.

### Test Coverage Required
- `tests/CabinetDesigner.Tests/Services/RunServiceTests.cs`:
  - `DeleteRunAsync_RemovesRun_WhenEmpty`.
  - `DeleteRunAsync_Rejects_WhenRunHasCabinets`.
  - `SetCabinetOverrideAsync_UpdatesOverride`.
  - `SetCabinetOverrideAsync_RejectsEmptyKey`.
- `tests/CabinetDesigner.Tests/Services/ProjectServiceTests.cs`:
  - `OpenProjectAsync_WithMultipleProjectsInStore_Throws`.
  - `CreateProjectAsync_PersistsEmptyWorkingRevision`.
- `tests/CabinetDesigner.Tests/Validation/ManufacturingReadinessRuleTests.cs`:
  - `Evaluate_NoBlockers_ReturnsEmpty`.
  - `Evaluate_OneBlocker_EmitsManufactureBlockerIssue`.
- `tests/CabinetDesigner.Tests/Validation/InstallReadinessRuleTests.cs`: parallel cases.
- Update `tests/CabinetDesigner.Tests/Presentation/ViewModels/IssuePanelViewModelTests.cs` (and StatusBar equivalent) to drop the NotImplementedException path.
- Integration: `tests/CabinetDesigner.Tests/Pipeline/EndToEndPipelineTests.cs` (new) exercising B2–B6 together:
  - `FullPipeline_OnSampleProject_ProducesValidApprovedSnapshot`.
  - `FullPipeline_IsDeterministic`.

### Definition of Done
- [ ] No `throw new NotImplementedException(...)` remains in production code under `src/`.
- [ ] No `catch (NotImplementedException)` remains in production code under `src/`.
- [ ] `IssuePanel` displays manufacturing and install blockers in addition to run capacity / workflow.
- [ ] `OpenProjectAsync` no longer silently chooses the first project.
- [ ] `CreateProjectAsync` → close → reopen round-trip works.
- [ ] End-to-end pipeline test green.

---

## Cross-Batch Guardrails (apply to every batch)

- Determinism: extend `DeterminismTests` whenever a stage is touched.
- Layering: Domain → Application → Presentation → App only. No `Rendering` from `Application`. No `Domain` reference added to `Presentation.csproj`.
- UI-thread: any new viewmodel event handler uses existing `DispatchIfNeeded` pattern.
- No `catch (Exception)` without either (a) logging via `IAppLogger`, or (b) surfacing via `IApplicationEventBus` / `StatusBarViewModel`.
- Error codes are stable string literals — do not reformat once published.
- `dotnet build -warnaserror` and `dotnet test` must be clean before closing a batch.

---

## Completion Order & Dependencies

```
B0 ─┬─► B1 (UI shell; parallel OK after B0)
    └─► B2 ──► B3 ─┐
                   ├─► B5 ──► B6 ──► B7
            B4 ────┘
```

- B1 may ship in parallel with B2 but should not merge before B0.
- B3 and B4 may overlap but both must land before B5.
- B6 requires B5's `CostingResult`.
- B7 closes out validation + service gaps last, so its rule tests see real blockers from B2–B6.

---

## Go / No-Go Reminder (from execution plan)

- **User testing GO**: B0 + B1 + B2 + B3 + B4 complete and green.
- **Professional use GO**: B0–B7 complete, determinism verified, costing reconciles, approval rejects invalid, no `NotImplementedException` remaining.
